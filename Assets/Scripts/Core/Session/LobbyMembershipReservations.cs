using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AbsoluteZero.Core.Session
{
    // Local cancellation cannot retract a Lobby SDK request. Reserve only its
    // remote membership until the real request settles, across session owners.
    internal sealed class LobbyMembershipReservations
    {
        internal static readonly LobbyMembershipReservations Shared = new();
        internal const string PendingMessage = "이 방의 이전 참가·퇴장 처리가 아직 진행 중입니다. 잠시 후 다시 시도하거나 다른 방에 참가해 주세요.";
        readonly object _gate = new();
        readonly Dictionary<(string code, string player), JoinReservation> _joins = new();
        readonly Dictionary<(string lobby, string player), Removal> _removals = new();

        sealed class JoinReservation : IDisposable
        {
            readonly LobbyMembershipReservations _owner;
            internal readonly (string code, string player) Key;
            internal JoinReservation(LobbyMembershipReservations owner, (string, string) key) { _owner = owner; Key = key; }
            public void Dispose()
            {
                lock (_owner._gate)
                    if (_owner._joins.TryGetValue(Key, out var current) && ReferenceEquals(current, this))
                        _owner._joins.Remove(Key);
            }
        }
        sealed class Removal
        {
            internal readonly string Code;
            internal readonly TaskCompletionSource<Result<Unit>> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Removal(string code) { Code = Normalize(code); }
        }
        static string Normalize(string code) => code?.Trim().ToUpperInvariant() ?? "";

        internal bool TryReserveJoin(string code, string playerId, out IDisposable reservation)
        {
            reservation = null;
            var key = (Normalize(code), playerId);
            if (key.Item1.Length == 0 || string.IsNullOrEmpty(playerId)) return false;
            lock (_gate)
            {
                if (_joins.ContainsKey(key)) return false;
                foreach (var pending in _removals)
                    if (pending.Key.player == playerId && pending.Value.Code == key.Item1) return false;
                var acquired = new JoinReservation(this, key);
                _joins.Add(key, acquired);
                reservation = acquired;
                return true;
            }
        }

        internal Task<Result<Unit>> RemoveAsync(string lobbyId, string playerId, string code,
            Func<Task<Result<Unit>>> send)
        {
            var key = (lobbyId, playerId);
            Removal removal;
            lock (_gate)
            {
                if (_removals.TryGetValue(key, out var pending)) return pending.Done.Task;
                removal = new Removal(code);
                _removals.Add(key, removal);
            }
            _ = CompleteRemovalAsync(key, removal, send);
            return removal.Done.Task;
        }

        async Task CompleteRemovalAsync((string lobby, string player) key, Removal removal, Func<Task<Result<Unit>>> send)
        {
            Result<Unit> result;
            try { result = await send(); }
            catch (Exception error) { result = Result<Unit>.Failure(OperationErrorCode.Unexpected, error.Message); }
            // The old server mutation has now actually settled. A new join is
            // safe even if the UI continuation has not run yet.
            lock (_gate)
            {
                if (_removals.TryGetValue(key, out var pending) && ReferenceEquals(pending, removal))
                    _removals.Remove(key);
            }
            removal.Done.TrySetResult(result);
        }
    }
}
