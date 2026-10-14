using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using AbsoluteZero.Core.Network;
using Unity.Services.Lobbies.Models;

namespace AbsoluteZero.Core.Session
{
    // Lobby protocol validation is independent of UI choices and transport IDs.
    internal readonly struct OnlineLobbySettings
    {
        internal readonly GameMode Mode;
        internal readonly int PlayerCount;
        internal readonly bool Starting, Started;
        internal readonly string RelayCode, HostPresence;
        internal OnlineLobbySettings(GameMode mode, int count, bool starting, bool started, string relay, string presence)
        { Mode = mode; PlayerCount = count; Starting = starting; Started = started; RelayCode = relay; HostPresence = presence; }
    }

    internal static class OnlineLobbyJoinPolicy
    {
        internal const float HostPresenceTimeoutSeconds = 60f;
        internal const string RejoinBlockedMessage = "이미 시작된 경기에는 다시 참가할 수 없습니다. 로비로 돌아가 새 방을 만들거나 다른 방에 참가해 주세요.";
        internal const string InvalidMetadataMessage = "방 설정을 확인할 수 없습니다. 모두 같은 최신 버전으로 실행한 뒤 새 방을 만들어 주세요.";

        internal static bool TryRead(Lobby lobby, out OnlineLobbySettings settings)
        {
            settings = default;
            if (lobby?.Data == null || string.IsNullOrEmpty(lobby.Id) || string.IsNullOrEmpty(lobby.HostId)) return false;
            string modeName = Value(lobby, "GameMode");
            GameMode mode = modeName == "OneVsOne" ? GameMode.OneVsOne : modeName == "Multi" ? GameMode.Multi : GameMode.None;
            if (!int.TryParse(Value(lobby, "PlayerCount"), NumberStyles.None, CultureInfo.InvariantCulture, out int count)
                || (mode == GameMode.OneVsOne ? count != 2 : mode != GameMode.Multi || count < 3 || count > 4)
                || lobby.MaxPlayers != count || !bool.TryParse(Value(lobby, "GameStarted"), out bool started)
                || !bool.TryParse(Value(lobby, "GameStarting"), out bool starting)
                || string.IsNullOrEmpty(Value(lobby, "HostPresence"))) return false;
            string relay = Value(lobby, "RelayJoinCode");
            if (relay == null || (!started && relay.Length != 0)) return false;
            settings = new OnlineLobbySettings(mode, count, starting, started, relay, Value(lobby, "HostPresence"));
            return true;
        }

        internal static string Value(Lobby lobby, string key)
            => lobby?.Data != null && lobby.Data.TryGetValue(key, out var value) ? value?.Value : null;

        internal static bool ContainsPlayer(Lobby lobby, string playerId)
            => !string.IsNullOrEmpty(playerId) && lobby?.Players != null && lobby.Players.Exists(p => p?.Id == playerId);

        // Query only this authenticated user's memberships. Never use a public
        // query result or the first joined lobby as authority to remove a player.
        internal static async Task<Result<Lobby>> RecoverMembershipAsync(ILobbyGateway gateway,
            string code, string playerId, Func<bool> isCurrent)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrEmpty(playerId))
                return Result<Lobby>.Failure(OperationErrorCode.InvalidState, InvalidMetadataMessage);
            var ids = await AwaitReadAsync(gateway.GetJoinedLobbyIdsAsync(), isCurrent);
            if (ids.IsFailure) return Result<Lobby>.Failure(ids.ErrorCode, ids.ErrorMessage);
            if (ids.Value == null || ids.Value.Count > 100)
                return Result<Lobby>.Failure(OperationErrorCode.InvalidState, InvalidMetadataMessage);
            Lobby match = null;
            foreach (string id in new HashSet<string>(ids.Value))
            {
                if (!isCurrent()) return Result<Lobby>.Failure(OperationErrorCode.Cancelled, "Lobby recovery cancelled");
                if (string.IsNullOrEmpty(id)) continue;
                var read = await AwaitReadAsync(gateway.GetAsync(id), isCurrent);
                if (read.IsFailure)
                {
                    if (read.ErrorCode == OperationErrorCode.LobbyNotFound) continue;
                    return read;
                }
                var lobby = read.Value;
                if (lobby?.Id != id || !string.Equals(lobby.LobbyCode, code.Trim(), StringComparison.OrdinalIgnoreCase)
                    || !ContainsPlayer(lobby, playerId)) continue;
                if (match != null) return Result<Lobby>.Failure(OperationErrorCode.InvalidState, InvalidMetadataMessage);
                match = lobby;
            }
            return match != null ? Result<Lobby>.Success(match)
                : Result<Lobby>.Failure(OperationErrorCode.LobbyNotFound, "기존 참가 정보를 확인할 수 없습니다. 방 코드를 다시 확인해 주세요.");
        }

        internal static async Task<Result<T>> AwaitReadAsync<T>(Task<Result<T>> request, Func<bool> isCurrent)
        {
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!request.IsCompleted && isCurrent() && deadline.Elapsed.TotalSeconds < 30)
                await Task.WhenAny(request, Task.Delay(50));
            if (!isCurrent() || !request.IsCompleted)
            {
                // SDK requests cannot be canceled; observe a late fault but never
                // publish its result into a replaced session.
                _ = ObserveAsync(request);
                return Result<T>.Failure(!isCurrent() ? OperationErrorCode.Cancelled : OperationErrorCode.Timeout,
                    "방 정보 확인이 중단되었습니다. 연결 상태를 확인한 뒤 다시 참가해 주세요.");
            }
            try { return await request; }
            catch (Exception e) { return Result<T>.Failure(OperationErrorCode.Unexpected, e.Message); }
        }

        static async Task ObserveAsync<T>(Task<T> task) { try { await task; } catch { } }
    }

    // Monotonic local observation avoids dependence on synchronized PC clocks.
    // A healthy waiting room can wait indefinitely; start and host loss cannot.
    internal sealed class OnlineLobbyWait
    {
        readonly string _lobbyId, _hostId, _playerId;
        readonly GameMode _mode;
        readonly int _count;
        readonly float _startTimeout;
        string _presence;
        float _lastPresence, _startTime = -1;
        internal OnlineLobbyWait(Lobby lobby, string playerId, OnlineLobbySettings settings, float now, float startTimeout)
        {
            _lobbyId = lobby.Id; _hostId = lobby.HostId; _playerId = playerId;
            _mode = settings.Mode; _count = settings.PlayerCount;
            _presence = settings.HostPresence; _lastPresence = now; _startTimeout = startTimeout;
        }

        // Success with a null relay code means a valid room is still waiting.
        internal Result<string> Observe(Lobby lobby, float now)
        {
            if (lobby?.Id != _lobbyId || lobby.HostId != _hostId ||
                !OnlineLobbyJoinPolicy.ContainsPlayer(lobby, _hostId) || !OnlineLobbyJoinPolicy.ContainsPlayer(lobby, _playerId))
                return Result<string>.Failure(OperationErrorCode.LobbyNotFound, "방장이 나갔거나 방이 닫혔습니다. 새 방에 참가해 주세요.");
            if (!OnlineLobbyJoinPolicy.TryRead(lobby, out var settings) || settings.Mode != _mode || settings.PlayerCount != _count)
                return Result<string>.Failure(OperationErrorCode.InvalidState, OnlineLobbyJoinPolicy.InvalidMetadataMessage);
            if (settings.HostPresence != _presence) { _presence = settings.HostPresence; _lastPresence = now; }
            if (settings.Started && !string.IsNullOrEmpty(settings.RelayCode)) return Result<string>.Success(settings.RelayCode);
            if ((settings.Starting || settings.Started) && _startTime < 0) _startTime = now;
            if (_startTime >= 0 && now - _startTime >= _startTimeout)
                return Result<string>.Failure(OperationErrorCode.Timeout, "게임 연결 준비 시간이 초과되었습니다. 새 방에서 다시 시작해 주세요.");
            if (now - _lastPresence >= OnlineLobbyJoinPolicy.HostPresenceTimeoutSeconds)
                return Result<string>.Failure(OperationErrorCode.Timeout, "방장과의 연결을 확인할 수 없습니다. 새 방에 참가해 주세요.");
            return Result<string>.Success(null);
        }
    }
}
