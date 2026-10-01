using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Turn
{
    // Called synchronously by the server phase owner. No timer, coroutine or phase state.
    internal static class PrepTurnOperations
    {
        internal static void RemoveKidsItems(PlayerState[] players, bool multi, EnvironmentRuleService environment)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if (multi && players[i].CurrentLifeState.Value != LifeState.Alive) continue;
                environment.RemoveRandomUnusedItem(players[i].GetInventory());
            }
        }

        internal static void ApplyMultiTick(PlayerState[] players, PlayerModifiers[] modifiers, TemperatureSystem temperature,
            bool skipFirstFanTick, float recoveryRate)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || players[i].CurrentLifeState.Value != LifeState.Alive)
                    continue;
                if (!skipFirstFanTick)
                    temperature.ApplyFanTick(players[i], modifiers[i].FanSpeedMultiplier);
                temperature.ApplyRecoveryTick(players[i], recoveryRate, modifiers[i].RecoveryMultiplier);
            }
        }

        internal static void GrantMultiThresholds(PlayerState[] players, TemperatureSystem temperature,
            ItemDropTable dropTable, int maxRandom)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || players[i].CurrentLifeState.Value != LifeState.Alive)
                    continue;
                var inventory = players[i].GetInventory();
                if (inventory != null)
                    temperature.CheckThresholds(players[i], inventory,
                        inventory.GetThresholdGranted(), dropTable, true, maxRandom);
            }
        }

        internal static void ApplyDuelTick(PlayerState[] players, TemperatureSystem temperature,
            bool skipFirstFanTick, float recoveryRate, ItemDropTable dropTable)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if (!skipFirstFanTick) temperature.ApplyFanTick(players[i]);
                temperature.ApplyRecoveryTick(players[i], recoveryRate);
                var inventory = players[i].GetInventory();
                if (inventory != null)
                    temperature.CheckThresholds(players[i], inventory,
                        inventory.GetThresholdGranted(), dropTable);
            }
        }

        internal static bool AllReady(PlayerState[] players, bool multi)
        {
            bool allReady = true;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if (multi && players[i].CurrentLifeState.Value != LifeState.Alive) continue;
                if (!players[i].IsReady.Value) { allReady = false; break; }
            }
            return allReady;
        }

        internal static void CompleteReady(PlayerState[] players, bool multi, RoundLifecycleService lifecycle)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if (multi && players[i].CurrentLifeState.Value != LifeState.Alive) continue;
                if (!players[i].IsReady.Value) lifecycle.ForceReady(players[i]);
                lifecycle.RevertFanUpgrade(players[i]);
            }
        }

        internal static byte FirstReadySeat(PlayerState[] players, bool multi)
        {
            byte firstSeat = byte.MaxValue;
            float earliestTimestamp = float.MaxValue;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if (multi && players[i].CurrentLifeState.Value != LifeState.Alive) continue;
                var q = players[i].GetActionQueue();
                if (q.readyTimestamp > 0f && q.readyTimestamp < earliestTimestamp)
                {
                    earliestTimestamp = q.readyTimestamp;
                    firstSeat = (byte)i;
                }
            }
            return firstSeat;
        }

        internal static double LastEmoteTime(PlayerState[] players, bool multi)
        {
            double lastEmote = 0;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if (multi && players[i].CurrentLifeState.Value != LifeState.Alive) continue;
                lastEmote = System.Math.Max(lastEmote, players[i].LastEmoteServerTime);
            }
            return lastEmote;
        }
    }
}
