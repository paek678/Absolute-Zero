using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace AbsoluteZero.Core.Session
{
    public sealed class LobbyGateway : ILobbyGateway
    {
        const string LogPrefix = "[LobbyGateway]";

        public async Task<Result<Lobby>> CreateAsync(string name, int maxPlayers, CreateLobbyOptions options)
        {
            return await WrapLobbyCall(
                () => LobbyService.Instance.CreateLobbyAsync(name, maxPlayers, options),
                "Create");
        }

        public async Task<Result<Lobby>> JoinByCodeAsync(string code, JoinLobbyByCodeOptions options)
        {
            return await WrapLobbyCall(
                () => LobbyService.Instance.JoinLobbyByCodeAsync(code, options),
                "JoinByCode");
        }

        public async Task<Result<Lobby>> JoinByIdAsync(string id, JoinLobbyByIdOptions options)
        {
            return await WrapLobbyCall(
                () => LobbyService.Instance.JoinLobbyByIdAsync(id, options),
                "JoinById");
        }

        public async Task<Result<Lobby>> QuickJoinAsync(QuickJoinLobbyOptions options)
        {
            return await WrapLobbyCall(
                () => LobbyService.Instance.QuickJoinLobbyAsync(options),
                "QuickJoin");
        }

        public async Task<Result<List<Lobby>>> QueryAsync(QueryLobbiesOptions options)
        {
            try
            {
                var response = await LobbyService.Instance.QueryLobbiesAsync(options);
                return Result<List<Lobby>>.Success(response.Results);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"{LogPrefix} Query failed: {e.Message}");
                return Result<List<Lobby>>.Failure(MapLobbyError(e), e.Message);
            }
        }

        public async Task<Result<Lobby>> GetAsync(string lobbyId)
        {
            return await WrapLobbyCall(
                () => LobbyService.Instance.GetLobbyAsync(lobbyId),
                "Get");
        }

        public async Task<Result<Lobby>> UpdateAsync(string lobbyId, UpdateLobbyOptions options)
        {
            return await WrapLobbyCall(
                () => LobbyService.Instance.UpdateLobbyAsync(lobbyId, options),
                "Update");
        }

        public async Task<Result<Lobby>> UpdatePlayerAsync(string lobbyId, string playerId, UpdatePlayerOptions options)
        {
            return await WrapLobbyCall(
                () => LobbyService.Instance.UpdatePlayerAsync(lobbyId, playerId, options),
                "UpdatePlayer");
        }

        public async Task<Result<Unit>> DeleteAsync(string lobbyId)
        {
            return await WrapLobbyVoidCall(
                () => LobbyService.Instance.DeleteLobbyAsync(lobbyId),
                "Delete", true);
        }

        public async Task<Result<Unit>> RemovePlayerAsync(string lobbyId, string playerId)
        {
            return await WrapLobbyVoidCall(
                () => LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId),
                "RemovePlayer", true);
        }

        public async Task<Result<Unit>> SendHeartbeatAsync(string lobbyId)
        {
            return await WrapLobbyVoidCall(
                () => LobbyService.Instance.SendHeartbeatPingAsync(lobbyId),
                "Heartbeat");
        }

        static async Task<Result<Lobby>> WrapLobbyCall(Func<Task<Lobby>> call, string opName)
        {
            try
            {
                var lobby = await call();
                return Result<Lobby>.Success(lobby);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"{LogPrefix} {opName} failed: {e.Message}");
                return Result<Lobby>.Failure(MapLobbyError(e), e.Message);
            }
        }

        static async Task<Result<Unit>> WrapLobbyVoidCall(Func<Task> call, string opName, bool alreadyRemovedIsSuccess = false)
        {
            try
            {
                await call();
                return Result<Unit>.Success(Unit.Value);
            }
            catch (LobbyServiceException e)
            {
                // A host can delete the lobby before departing clients remove
                // themselves. The requested absence is already satisfied.
                if (alreadyRemovedIsSuccess && IsAlreadyAbsent(e.Reason))
                    return Result<Unit>.Success(Unit.Value);
                Debug.LogError($"{LogPrefix} {opName} failed: {e.Message}");
                return Result<Unit>.Failure(MapLobbyError(e), e.Message);
            }
        }

        static OperationErrorCode MapLobbyError(LobbyServiceException e)
        {
            return e.Reason switch
            {
                LobbyExceptionReason.LobbyNotFound => OperationErrorCode.LobbyNotFound,
                LobbyExceptionReason.LobbyConflict => OperationErrorCode.LobbyConflict,
                LobbyExceptionReason.RateLimited => OperationErrorCode.RateLimited,
                _ => OperationErrorCode.Unexpected
            };
        }

        // Removal can race host deletion or an earlier successful removal. These
        // specific responses already satisfy the requested absence, not an auth failure.
        static bool IsAlreadyAbsent(LobbyExceptionReason reason)
            => reason == LobbyExceptionReason.LobbyNotFound || reason == LobbyExceptionReason.PlayerNotFound;
    }
}
