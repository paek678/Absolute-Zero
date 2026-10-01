#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class MatrixScenarioProbe
    {
        void CheckTemperaturePolicies(PlayerState[] players)
        {
            var inventories = players.Select(p => Enumerable.Range(0, p.GetInventory().SlotStates.Count)
                .Select(i => p.GetInventory().SlotStates[i]).ToArray()).ToArray();
            var temperatures = players.Select(p => p.Temperature.Value).ToArray();
            var fans = players.Select(p => p.IsFanActive.Value).ToArray();
            var ready = players.Select(p => p.IsReady.Value).ToArray();
            var speeds = players.Select(p => p.FanSpeed.Value).ToArray();
            var upgrades = players.Select(p => p.IsFanUpgraded.Value).ToArray();
            var lives = players.Select(p => p.CurrentLifeState.Value).ToArray();
            var thresholds = players.Select(p => (bool[])p.GetInventory().GetThresholdGranted().Clone()).ToArray();
            var random = Random.state;
            var temperature = new TemperatureSystem();
            var lifecycle = new RoundLifecycleService();
            var environment = new EnvironmentRuleService();
            var actor = players[0]; var inventory = actor.GetInventory();
            var items = ItemManager.Instance.GetAllItems();
            short itemId = (short)Array.FindIndex(items, x => x != null && x.ItemName == "Ice Cream");
            short fanId = (short)Array.FindIndex(items, x => x != null && x.ItemName == "Fan");
            var table = new ItemDropTable(new[] { items[itemId] });
            try
            {
                inventory.SlotStates.Clear();
                var flags = inventory.GetThresholdGranted(); Array.Clear(flags, 0, flags.Length);
                int total = 0;
                for (int i = 0; i < 3; i++)
                {
                    actor.Temperature.Value = new[] { 30f, 20f, 10f }[i];
                    temperature.CheckThresholds(actor, inventory, flags, table, _count > 2);
                    total += _count > 2 ? 1 : i + 1;
                    Require(flags.Take(i + 1).All(x => x) && inventory.SlotStates.Count == 1
                        && inventory.SlotStates[0].RemainingUses == total * items[itemId].MaxUses,
                        "threshold grant count and inclusive boundary index=" + i);
                    uint fingerprint = inventory.CurrentFingerprint();
                    temperature.CheckThresholds(actor, inventory, flags, table, _count > 2);
                    Require(inventory.CurrentFingerprint() == fingerprint, "threshold never grants twice index=" + i);
                }
                inventory.ResetForNewRound();
                Require(inventory.GetThresholdGranted().All(x => !x), "new round clears threshold flags");
                Require(Enumerable.Range(0, inventory.SlotStates.Count).All(i => inventory.GetItemData(i)?.Persistence
                    != ItemPersistence.RandomConsumable), "new round removes random items and restores basics");

                foreach (EnvironmentType env in Enum.GetValues(typeof(EnvironmentType)))
                {
                    actor.Temperature.Value = 20f; actor.IsFanActive.Value = false; actor.IsReady.Value = true;
                    temperature.ApplyRecoveryTick(actor, environment.GetRecoveryRate(env));
                    float rate = env == EnvironmentType.SunnyDay ? 2f : env == EnvironmentType.CoolBreeze ? 0f : 1f;
                    Require(actor.Temperature.Value == 20f + rate, "actual server recovery for " + env);
                }
                actor.Temperature.Value = 5f; actor.IsFanActive.Value = true; actor.FanSpeed.Value = 2f;
                temperature.ApplyFanTick(actor, 1.5f);
                Require(actor.Temperature.Value == 2f, "actual fan multiplier applied once");
                actor.IsFanUpgraded.Value = true;
                lifecycle.RevertFanUpgrade(actor); lifecycle.RevertFanUpgrade(actor);
                Require(actor.FanSpeed.Value == 1f && !actor.IsFanUpgraded.Value, "fan upgrade reverts once");

                inventory.SlotStates.Clear(); inventory.GrantSpecificItem(fanId); inventory.GrantSpecificItem(itemId);
                var permanent = inventory.SlotStates[0];
                actor.GetActionQueue().SetSelected(1, items[itemId], 1);
                var before = Random.state; Random.Range(0, 1); var after = Random.state; Random.state = before;
                environment.RemoveRandomUnusedItem(inventory);
                Require(Random.state.Equals(after) && inventory.SlotStates.Count == 1
                    && inventory.SlotStates[0].Equals(permanent) && !actor.GetActionQueue().selectedAction.HasValue,
                    "Kids removes random consumable, keeps permanent and invalidates selected copy");
                before = Random.state; environment.RemoveRandomUnusedItem(inventory);
                Require(Random.state.Equals(before), "Kids with no candidate does not draw");

                if (_count > 2)
                {
                    var roster = MatchCompositionRoot.Instance.Roster;
                    for (int i = 0; i < _count; i++) players[i].Temperature.Value = 20f;
                    Require(environment.DetermineAmbulanceTargetMulti(players, roster) == 0,
                        "Multi ambulance tie chooses first alive seat");
                    roster.SetLifeState(0, LifeState.Ghost);
                    players[0].Temperature.Value = 1f;
                    Require(environment.DetermineAmbulanceTargetMulti(players, roster) == 1,
                        "Multi ambulance excludes lower-temperature ghost");
                }
                else Require(environment.DetermineAmbulanceTarget(20f, 20f) == -1,
                    "duel ambulance tie leaves both unchanged");
                if (_count > 2) lifecycle.ResetPlayersForNewRound(players);
                else lifecycle.ResetPlayersForNewRound(players[0], players[1]);
                Require(players.All(p => p.Temperature.Value == 37f && !p.IsReady.Value && !p.IsFanActive.Value
                    && p.FanSpeed.Value == 1f && !p.IsFanUpgraded.Value), "actual round reset restores temperature/fan/readiness");
            }
            finally
            {
                Random.state = random;
                for (int i = 0; i < players.Length; i++)
                {
                    var p = players[i]; var inv = p.GetInventory();
                    p.Temperature.Value = temperatures[i]; p.IsFanActive.Value = fans[i]; p.IsReady.Value = ready[i];
                    p.FanSpeed.Value = speeds[i]; p.IsFanUpgraded.Value = upgrades[i];
                    MatchCompositionRoot.Instance.Roster.SetLifeState((byte)i, lives[i]);
                    inv.SlotStates.Clear(); foreach (var slot in inventories[i]) inv.SlotStates.Add(slot);
                    Array.Copy(thresholds[i], inv.GetThresholdGranted(), thresholds[i].Length);
                    p.GetActionQueue().Clear();
                }
            }
        }
    }
}
#endif
