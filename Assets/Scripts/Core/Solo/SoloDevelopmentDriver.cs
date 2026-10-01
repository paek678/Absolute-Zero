using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.Core.Player;
#endif

namespace AbsoluteZero.Core.Solo
{
    // Explicit T07 vertical-slice driver. No normal or release launch fallback.
    public sealed class SoloDevelopmentDriver : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PrepInputKey _key;
        ulong _request;
        bool _begun;
        void Awake()
        {
            enabled = Array.IndexOf(Environment.GetCommandLineArgs(), "--solo-scripted-slice") >= 0;
        }
        void Update()
        {
            if (AppBootstrapper.Instance?.SessionRouter.LaunchContext?.Solo?.IsValidationFixture != true) return;
            var turn = TurnManager.Instance;
            if (turn == null || !turn.TryGetPrepInputSnapshot(out var snapshot)) return;
            var bot = turn.GetPlayer(1);
            if (bot == null || bot.IsReady.Value) return;
            var commands = bot.GetBotCommands();
            if (commands == null) return;
            if (_key != snapshot.Key) { _key = snapshot.Key; _begun = false; }
            if (!_begun)
            {
                _begun = true;
                var inventory = bot.GetInventory();
                if (inventory.SlotStates.Count == 0) { commands.Ready(_key); return; }
                commands.BeginUse(_key, ++_request, inventory.SlotStates[0].CopyId, 0);
            }
            var result = commands.Poll(_key, _request);
            if (result.Status != PlayerActionStatus.Pending) commands.Ready(_key);
        }
#endif
    }
}
