using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

namespace AbsoluteZero.Core.Session
{
    public enum CosmeticPublicationStatus { Failed, Published, NotApplicable, Cancelled, Superseded }

    public readonly struct CosmeticPublicationResult
    {
        public CosmeticPublicationStatus Status { get; }
        public long Revision { get; }
        public MatchSessionLease Lease { get; }
        public string Error { get; }
        public CosmeticPublicationResult(CosmeticPublicationStatus status, long revision,
            MatchSessionLease lease, string error = null)
        { Status = status; Revision = revision; Lease = lease; Error = error; }
    }

    // One online-lobby owner. Unity/NGO lifecycle remains with the coordinator.
    internal sealed class LobbySessionWork : IDisposable
    {
        sealed class Publication
        {
            internal readonly string Dto;
            internal readonly long Revision;
            internal readonly TaskCompletionSource<CosmeticPublicationResult> Completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Publication(string dto, long revision) { Dto = dto; Revision = revision; }
        }

        readonly ILobbyGateway _gateway;
        readonly LobbyWriteReservations _reservations;
        readonly Func<bool> _ownsSession;
        readonly Action<Lobby> _changed;
        readonly Action _lost;
        readonly Action<string> _diagnostic;
        readonly Func<Task> _retryDelay;
        readonly string _lobbyId, _playerId;
        readonly MatchSessionLease _lease;
        readonly float _heartbeatInterval, _pollInterval;
        bool _disposed, _polling, _heartbeating, _draining, _gameActive;
        float _heartbeatRemaining, _pollRemaining;
        Publication _inFlight, _pending;
        long _latestRevision = -1;
        string _latestDto;

        internal Lobby Current { get; private set; }
        internal long MetadataEpoch { get; private set; }
        internal long ConfirmedRevision { get; private set; } = -1;
        internal bool IsCurrent => !_disposed && _ownsSession();

        internal LobbySessionWork(ILobbyGateway gateway, Lobby initial, string playerId,
            MatchSessionLease lease, Func<bool> ownsSession, Action<Lobby> changed,
            Action lost, Action<string> diagnostic, float heartbeatInterval = 15f,
            float pollInterval = 2f, LobbyWriteReservations reservations = null,
            Func<Task> retryDelay = null)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            Current = initial ?? throw new ArgumentNullException(nameof(initial));
            _lobbyId = initial.Id;
            _playerId = playerId;
            _lease = lease;
            _ownsSession = ownsSession;
            _changed = changed;
            _lost = lost;
            _diagnostic = diagnostic;
            _heartbeatInterval = Math.Max(0.1f, heartbeatInterval);
            _pollInterval = Math.Max(0.1f, pollInterval);
            _reservations = reservations ?? LobbyWriteReservations.Shared;
            _retryDelay = retryDelay ?? (() => Task.Delay(TimeSpan.FromSeconds(_pollInterval)));
        }

        // Lobby.Version orders public/member snapshots only. No private-data ordering
        // is inferred here. A successful command can have a rejected cache snapshot.
        internal bool TryAccept(Lobby snapshot, long capturedMetadataEpoch)
        {
            if (!IsCurrent || snapshot == null || snapshot.Id != _lobbyId) return false;
            if (capturedMetadataEpoch != MetadataEpoch || snapshot.Version < Current.Version)
                return false; // The next bounded scheduled poll supplies a fresh view.
            if (snapshot.Version == Current.Version) return false;
            Current = snapshot;
            // A view subscriber cannot turn an already successful cloud write into a
            // failed publication or strand callers waiting behind it.
            try { _changed?.Invoke(snapshot); }
            catch (Exception error) { _diagnostic?.Invoke("Lobby subscriber failed: " + error.Message); }
            return true;
        }

        internal void Tick(float deltaTime, bool isHost, bool gameActive)
        {
            if (!IsCurrent) { Dispose(); return; }
            if (_gameActive != gameActive)
            {
                _gameActive = gameActive;
                _heartbeatRemaining = _heartbeatInterval;
                _pollRemaining = _pollInterval;
            }
            if (gameActive) return;
            if (isHost && (_heartbeatRemaining -= deltaTime) <= 0f)
            { _heartbeatRemaining = _heartbeatInterval; _ = HeartbeatAsync(); }
            if ((_pollRemaining -= deltaTime) <= 0f)
            { _pollRemaining = _pollInterval; _ = PollAsync(); }
        }

        internal async Task PollAsync()
        {
            if (!IsCurrent || _polling) return;
            _polling = true;
            long epoch = MetadataEpoch;
            try
            {
                var result = await _gateway.GetAsync(_lobbyId);
                if (!IsCurrent) return;
                if (result.IsSuccess && result.Value != null) TryAccept(result.Value, epoch);
                else if (result.ErrorCode == OperationErrorCode.LobbyNotFound ||
                         result.ErrorCode == OperationErrorCode.LobbyConflict) _lost?.Invoke();
                else _diagnostic?.Invoke(result.ErrorMessage ?? "Lobby poll returned no snapshot");
            }
            catch (Exception error) { if (IsCurrent) _diagnostic?.Invoke(error.Message); }
            finally { _polling = false; }
        }

        async Task HeartbeatAsync()
        {
            if (!IsCurrent || _heartbeating) return;
            _heartbeating = true;
            try
            {
                var result = await _gateway.SendHeartbeatAsync(_lobbyId);
                if (IsCurrent && result.IsFailure) _diagnostic?.Invoke(result.ErrorMessage);
            }
            catch (Exception error) { if (IsCurrent) _diagnostic?.Invoke(error.Message); }
            finally { _heartbeating = false; }
        }

        internal Task<CosmeticPublicationResult> PublishAsync(string dto, long revision)
        {
            if (!IsCurrent) return Task.FromResult(Outcome(CosmeticPublicationStatus.Cancelled, revision));
            if (string.IsNullOrEmpty(_playerId) || dto == null || revision < 0)
                return Task.FromResult(Outcome(CosmeticPublicationStatus.Failed, revision, "Invalid publication identity"));
            if (revision < _latestRevision)
                return Task.FromResult(Outcome(CosmeticPublicationStatus.Superseded, revision));
            if (revision == _latestRevision && dto != _latestDto)
                return Task.FromResult(Outcome(CosmeticPublicationStatus.Failed, revision, "Revision has different equipment"));
            if (_inFlight?.Revision == revision && _inFlight.Dto == dto) return _inFlight.Completion.Task;
            if (_pending?.Revision == revision && _pending.Dto == dto) return _pending.Completion.Task;
            if (ConfirmedRevision == revision && _latestDto == dto)
                return Task.FromResult(Outcome(CosmeticPublicationStatus.Published, revision));
            _latestRevision = revision;
            _latestDto = dto;
            var request = new Publication(dto, revision);
            if (_draining)
            {
                Complete(_pending, CosmeticPublicationStatus.Superseded);
                _pending = request;
            }
            else
            {
                _inFlight = request;
                _draining = true;
                _ = DrainAsync();
            }
            return request.Completion.Task;
        }

        async Task DrainAsync()
        {
            int retries = 0;
            try
            {
                while (_inFlight != null && IsCurrent)
                {
                    var request = _inFlight;
                    var result = await _reservations.Run(_lobbyId, _playerId, () =>
                        IsCurrent ? _gateway.UpdatePlayerAsync(_lobbyId, _playerId, new UpdatePlayerOptions
                        {
                            Data = new Dictionary<string, PlayerDataObject>
                            { ["CosmeticData"] = new(PlayerDataObject.VisibilityOptions.Member, request.Dto) }
                        }) : Task.FromResult(Result<Lobby>.Failure(OperationErrorCode.Cancelled, "Session ended")));
                    if (!IsCurrent) { Dispose(); return; }
                    if (result.IsSuccess && result.Value != null && result.Value.Id == _lobbyId)
                    {
                        MetadataEpoch++;
                        ConfirmedRevision = request.Revision;
                        TryAccept(result.Value, MetadataEpoch);
                        Complete(request, CosmeticPublicationStatus.Published);
                        _inFlight = _pending;
                        _pending = null;
                        retries = 0;
                    }
                    else if (result.ErrorCode == OperationErrorCode.RateLimited && retries++ == 0)
                    {
                        await _retryDelay();
                        if (!IsCurrent) { Dispose(); return; }
                        if (_pending != null)
                        {
                            Complete(request, CosmeticPublicationStatus.Superseded);
                            _inFlight = _pending;
                            _pending = null;
                        }
                    }
                    else
                    {
                        FailAll(result.ErrorMessage ?? "Lobby write returned no valid snapshot");
                        return;
                    }
                }
            }
            catch (Exception error)
            {
                if (IsCurrent) FailAll(error.Message);
                else Dispose();
            }
            finally { _draining = false; }
        }

        CosmeticPublicationResult Outcome(CosmeticPublicationStatus status, long revision, string error = null)
            => new(status, revision, _lease, error);
        void Complete(Publication request, CosmeticPublicationStatus status, string error = null)
        { request?.Completion.TrySetResult(Outcome(status, request.Revision, error)); }
        void FailAll(string error)
        {
            Complete(_inFlight, CosmeticPublicationStatus.Failed, error);
            Complete(_pending, CosmeticPublicationStatus.Failed, "Preceding publication failed: " + error);
            _inFlight = _pending = null;
        }
        public void Dispose()
        {
            _disposed = true;
            Complete(_inFlight, CosmeticPublicationStatus.Cancelled);
            Complete(_pending, CosmeticPublicationStatus.Cancelled);
            _inFlight = _pending = null;
            // The reservations observe the dispatched SDK task until its actual completion.
        }
    }
}
