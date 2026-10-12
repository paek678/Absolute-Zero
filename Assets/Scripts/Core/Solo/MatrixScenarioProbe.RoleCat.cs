#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class MatrixScenarioProbe
    {
        void HandleRoleCat(TurnManager tm, PlayerState[] players, PlayerState local)
        {
            if (players.Length != _count || tm.CurrentPhase.Value != TurnPhase.PrepPhase) return;
            int turn = tm.TurnNumber.Value;
            int catTurns = _count - 1;
            if (turn > catTurns + 3) { Pass("ROLE_CAT_COMPLETED local=" + local.PlayerIndex); return; }
            string attack = turn <= catTurns ? "Cat" : turn == catTurns + 1 ? "Ice Cream"
                : turn == catTurns + 2 ? "Fan" : "Warm Tea";
            if (_nm.IsServer && _specialTurn != turn)
            {
                _specialTurn = turn;
                foreach (var player in players)
                {
                    player.FanSpeed.Value = 0;
                    player.Temperature.Value = 37;
                    var inventory = player.GetInventory();
                    // Deterministic item stimulus only; selection and resolution use production RPCs.
                    string required = player.PlayerIndex == 0 ? attack : "Warm Tea";
                    if (!Enumerable.Range(0, inventory.SlotStates.Count).Any(i => inventory.GetItemData(i)?.ItemName == required && inventory.SlotStates[i].IsUsable))
                        inventory.GrantSpecificItem((short)Array.FindIndex(ItemManager.Instance.GetAllItems(), i => i.ItemName == required));
                }
            }
            if (_turn != turn) { _turn = turn; _phaseTime = Time.unscaledTime; _acted = _ready = false; }
            float elapsed = Time.unscaledTime - _phaseTime;
            if (!_acted && elapsed > 2)
            {
                _acted = true;
                string item = local.PlayerIndex == 0 ? attack : "Warm Tea";
                byte target = item == "Warm Tea" ? ActionIntent.NoTarget : (byte)Mathf.Min(turn, _count - 1);
                Select(local, item, target);
            }
            if (!_ready && elapsed > 4 + local.PlayerIndex * .2f)
            { _ready = true; ProbeInventoryCommands.Ready(local); }
        }
    }
}
#endif
