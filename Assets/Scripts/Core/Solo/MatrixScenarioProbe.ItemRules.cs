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
        int _ruleStagedTurn;

        void RunDelayedItemRules(TurnManager tm, PlayerState[] players, PlayerState local)
        {
            if (players.Length != _count || tm.CurrentPhase.Value != TurnPhase.PrepPhase) return;
            int turn = tm.TurnNumber.Value;
            if (_turn != turn)
            {
                _turn = turn;
                _phaseTime = Time.unscaledTime;
                _acted = _ready = false;
            }
            if (_nm.IsServer && _ruleStagedTurn != turn)
            {
                _ruleStagedTurn = turn;
                // Delayed effects fire at the next Attack start, not its Prep start.
                if (turn > 2)
                {
                    var applied = tm.GetBuffSystem().LastAppliedEffects;
                    int expectedCount = turn == 3 ? 2 : 1;
                    if (applied.Count != expectedCount)
                    { Fail("delayed item count at turn " + turn + ": " + applied.Count); return; }
                    foreach (var effect in applied)
                    {
                        float expected = Mathf.Clamp(effect.TemperatureBefore + effect.Value, 0f, 37f);
                        if (effect.Type != EffectType.TempChange || effect.TargetSeat != 0
                            || !Mathf.Approximately(expected, effect.TemperatureAfter)
                            || (turn == 3 && effect.Value != 15f && effect.Value != -7f)
                            || (turn == 4 && effect.Value != 20f))
                        { Fail("delayed item outcome at turn " + turn); return; }
                        byte source = _count == 2 ? DamageSource.InvalidSeat : (byte)(effect.Value == -7f ? 1 : 0);
                        if (effect.SourceSeat != source)
                        { Fail("delayed source attribution changed"); return; }
                    }
                    Debug.Log("[MATRIX] RULE_DELAY_VERIFIED turn=" + turn + " count=" + applied.Count);
                }
                if (turn < 4)
                {
                    foreach (var player in players)
                    {
                        player.FanSpeed.Value = 0f;
                        player.Temperature.Value = 20f;
                        player.GetInventory().SlotStates.Clear();
                    }
                    if (turn <= 2) Grant(players[0], turn == 1 ? "Soda" : "Buldak Noodles");
                    if (turn == 1) Grant(players[1], "Samgyetang");
                }
            }
            float elapsed = Time.unscaledTime - _phaseTime;
            if (turn >= 4)
            {
                if (elapsed > 1.5f) Pass("ITEM_RULE_DELAY_COMPLETED turns=3 local=" + local.PlayerIndex);
                return;
            }
            if (!_acted && elapsed > 2f)
            {
                _acted = true;
                if (local.PlayerIndex == 0 && turn <= 2) Select(local, turn == 1 ? "Soda" : "Buldak Noodles", 0);
                else if (local.PlayerIndex == 1 && turn == 1) Select(local, "Samgyetang", 0);
            }
            if (!_ready && elapsed > 4f)
            {
                bool shouldSelect = (local.PlayerIndex == 0 && turn <= 2) || (local.PlayerIndex == 1 && turn == 1);
                if (shouldSelect && !local.HasSelectedItem.Value)
                { Fail("delayed item was not accepted before Ready"); return; }
                _ready = true;
                ProbeInventoryCommands.Ready(local);
            }

            void Grant(PlayerState player, string name)
            {
                short id = (short)Array.FindIndex(ItemManager.Instance.GetAllItems(), item => item != null && item.ItemName == name);
                if (id < 0 || !player.GetInventory().GrantSpecificItem(id)) Fail("delayed fixture item missing: " + name);
            }
        }
    }
}
#endif
