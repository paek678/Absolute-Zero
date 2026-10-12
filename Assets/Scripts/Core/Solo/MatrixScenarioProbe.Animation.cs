#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class MatrixScenarioProbe
    {
        // Explicit recording fixture; real item selection/resolution/presentation,
        // with server-granted catalog items and controlled starting temperatures.
        static readonly string[] ShowcaseItems = {
            "Fan", "Windbreaker", "Ice Cream", "Mask",
            "Warm Tea", "Hot Americano", "Hot Pack", "Smartphone",
            "Water Gun", "Hand Fan", "Buldak Noodles", "Soda",
            "Ice Cream", "Iced Americano", "Samgyetang", "Hug T-shirt",
            "Screwdriver", "Claw Machine", "Blue Tape", "Warm Tea", "Cat", "Fan", "Red Card", "Fan"
        };

        void HandleItemShowcase(TurnManager tm, PlayerState[] players, PlayerState local)
        {
            if (players.Length != _count || tm.CurrentPhase.Value != TurnPhase.PrepPhase) return;
            int turn = tm.TurnNumber.Value;
            int first = (turn - 1) * _count;
            if (first >= ShowcaseItems.Length) { Pass("ITEM_SHOWCASE_COMPLETED"); return; }
            string Required(int seat) => first + seat < ShowcaseItems.Length ? ShowcaseItems[first + seat] : "Warm Tea";
            if (_nm.IsServer && _specialTurn != turn)
            {
                _specialTurn = turn;
                foreach (var player in players)
                {
                    player.FanSpeed.Value = 0;
                    player.Temperature.Value = 30;
                    player.IsBasicBlocked.Value = false;
                    var inv = player.GetInventory();
                    inv.SlotStates.Clear();
                    inv.GrantSpecificItem((short)Array.FindIndex(ItemManager.Instance.GetAllItems(), i => i.ItemName == Required(player.PlayerIndex)));
                    // A spare item keeps steal/reroll visibly meaningful.
                    inv.GrantSpecificItem((short)Array.FindIndex(ItemManager.Instance.GetAllItems(), i => i.ItemName == "Warm Tea"));
                }
            }
            if (_turn != turn) { _turn = turn; _phaseTime = Time.unscaledTime; _acted = _ready = false; }
            float elapsed = Time.unscaledTime - _phaseTime;
            if (!_acted && elapsed > 2)
            {
                _acted = true;
                string name = Required(local.PlayerIndex);
                var item = Array.Find(ItemManager.Instance.GetAllItems(), i => i.ItemName == name);
                byte target = item.GetTargetMode() == TargetMode.Self
                    ? ActionIntent.NoTarget : (byte)(local.PlayerIndex ^ 1);
                Select(local, name, target);
            }
            if (!_ready && elapsed > 4 + local.PlayerIndex * .2f)
            { _ready = true; ProbeInventoryCommands.Ready(local); }
        }

        void HandleAnimationSequence(TurnManager tm, PlayerState[] players, PlayerState local)
        {
            if (players.Length != _count || tm.CurrentPhase.Value != TurnPhase.PrepPhase) return;
            int turn = tm.TurnNumber.Value;
            if (turn > 6) { Pass("ANIMATION_SEQUENCE_COMPLETED local=" + local.PlayerIndex); return; }
            string Required(int seat) => turn == 2 ? "Warm Tea" : turn == 3 ? "Hand Fan"
                : turn == 4 && seat != 0 ? "Windbreaker" : turn == 5 ? "Ice Cream" : "Fan";
            if (_nm.IsServer && _specialTurn != turn)
            {
                _specialTurn = turn;
                foreach (var player in players)
                {
                    player.FanSpeed.Value = 0;
                    player.Temperature.Value = 37;
                    var inv = player.GetInventory();
                    inv.SlotStates.Clear();
                    inv.GrantSpecificItem((short)Array.FindIndex(ItemManager.Instance.GetAllItems(), i => i.ItemName == Required(player.PlayerIndex)));
                }
            }
            if (_turn != turn) { _turn = turn; _phaseTime = Time.unscaledTime; _acted = _ready = false; }
            float elapsed = Time.unscaledTime - _phaseTime;
            if (!_acted && elapsed > 2)
            {
                _acted = true;
                string item = Required(local.PlayerIndex);
                byte target = item == "Warm Tea" || item == "Windbreaker" ? ActionIntent.NoTarget : (byte)((local.PlayerIndex + 1) % _count);
                Select(local, item, target);
            }
            if (!_ready && elapsed > 4 + local.PlayerIndex * .2f)
            { _ready = true; ProbeInventoryCommands.Ready(local); }
        }
    }
}
#endif
