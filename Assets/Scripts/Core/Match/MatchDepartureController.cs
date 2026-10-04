using System;
using System.Threading.Tasks;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Match
{
    // Scene-scoped policy adapter; the existing session owner performs shutdown/navigation.
    [DisallowMultipleComponent]
    public sealed class MatchDepartureController : MonoBehaviour
    {
        MatchCompositionRoot _match;
        NetworkManager _network;
        Task _leaving;
        public bool IsLeaving => _leaving != null;

        void Awake() => _match = GetComponent<MatchCompositionRoot>();
        void Update()
        {
            var current = NetworkManager.Singleton;
            if (ReferenceEquals(_network, current)) return;
            Unsubscribe();
            _network = current;
            if (_network != null) _network.OnClientDisconnectCallback += OnDisconnected;
        }

        public static bool MustEnd(GameMode mode, int connectedParticipants)
            => mode == GameMode.OneVsOne ? connectedParticipants < 2
                : mode == GameMode.Multi && connectedParticipants < 3;

        void OnDisconnected(ulong departed)
        {
            if (IsLeaving || _match == null || !_match.IsSessionCurrent || _match.ActiveConfig == null
                || _network == null || !ReferenceEquals(_network, NetworkManager.Singleton)
                || !_network.IsServer || _network.ShutdownInProgress) return;
            bool participantLeft = false;
            int connected = 0;
            foreach (var participant in _match.Participants)
            {
                if (participant.ControllerKind != PlayerControllerKind.Human || !participant.ClientId.HasValue) continue;
                ulong id = participant.ClientId.Value;
                if (id == departed) participantLeft = true;
                else if (_network.ConnectedClients.ContainsKey(id)) connected++;
            }
            if (!participantLeft || !MustEnd(_match.ActiveConfig.Mode, connected)) return;
            Debug.Log($"[MatchDeparture] Participant left; {connected} connected. Returning everyone to lobby.");
            ObserveLeave();
        }

        async void ObserveLeave()
        {
            try { await LeaveAsync(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        public Task LeaveAsync()
        {
            if (_leaving != null) return _leaving;
            if (_match == null || !ReferenceEquals(MatchCompositionRoot.Instance, _match)) return Task.CompletedTask;
            // Stop the host phase driver before asynchronous cleanup; do not award a forfeit win.
            var turn = TurnManager.Instance;
            if (turn != null && turn.gameObject.scene == gameObject.scene) turn.StopAllCoroutines();
            _leaving = LeaveOwnedAsync();
            return _leaving;
        }

        async Task LeaveOwnedAsync()
        {
            await Task.Yield(); // Publish the single in-flight task before shutdown callbacks.
            try
            {
                var router = AppBootstrapper.Instance?.SessionRouter;
                if (router?.Current != null) { await router.StopAsync(); return; }
                // Direct local validation/legacy scene entry has no application session lease.
                var network = NetworkManager.Singleton;
                network?.Shutdown();
                float deadline = Time.realtimeSinceStartup + 5f;
                while (network != null && (network.IsListening || network.ShutdownInProgress))
                {
                    if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("Match network shutdown timed out.");
                    await Task.Yield();
                }
                if (this != null && ReferenceEquals(MatchCompositionRoot.Instance, _match)
                    && SceneManager.GetActiveScene().name != "LobbyScene")
                {
                    var load = SceneManager.LoadSceneAsync("LobbyScene");
                    while (load != null && !load.isDone) await Task.Yield();
                }
            }
            catch { _leaving = null; throw; }
        }

        void Unsubscribe()
        {
            if (!ReferenceEquals(_network, null)) _network.OnClientDisconnectCallback -= OnDisconnected;
            _network = null;
        }
        void OnDestroy() => Unsubscribe();
    }
}
