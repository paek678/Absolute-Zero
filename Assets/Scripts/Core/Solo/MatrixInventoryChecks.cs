#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class MatrixScenarioProbe
    {
        static void Require(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            Debug.Log("[MATRIX] ASSERT " + name);
        }
        void CheckServices(PlayerState[] players)
        {
            var root = MatchCompositionRoot.Instance;
            var roster = root.Roster;
            var state = root.NetworkState;
            var inventories = players.Select(p => Enumerable.Range(0, p.GetInventory().SlotStates.Count)
                .Select(i => p.GetInventory().SlotStates[i]).ToArray()).ToArray();
            var temperatures = players.Select(p => p.Temperature.Value).ToArray();
            var modifiers = new PlayerModifiers[_count];
            for (int i = 0; i < _count; i++) modifiers[i].Reset();
            var death = new AuthoritativeDeathService(roster, state);
            using var ghosts = new GhostSkillService(roster, modifiers);
            try
            {
                state.ServerBeginGhostMatch();
                ghosts.BeginMatch(state, _count);
                ghosts.BeginTurn(state, 1);
                roster.SetLifeState(0, LifeState.Ghost);
                players[1].Temperature.Value = 30;
                Require(ghosts.TryUseGrudge(0, 1, state, death, players, roster), "grudge first use accepted");
                Require(players[1].Temperature.Value == 27 && state.ServerGetCooldown(0, 0) == 2, "grudge damage and cooldown");
                Require(!ghosts.TryUseGrudge(0, 1, state, death, players, roster) && players[1].Temperature.Value == 27,
                    "grudge duplicate blocked");
                Require(ghosts.TryUsePossession(0, 2, state, roster), "possession accepted");
                Require((state.GhostPossessionSpentMask.Value & 1) != 0 && (state.GhostPossessedMask.Value & 4) != 0,
                    "possession spent and target marked");
                Require(!ghosts.TryUsePossession(0, 3, state, roster), "possession is one use per match");
                ghosts.BeginTurn(state, 2);
                Require((state.GhostPossessedMask.Value & 4) == 0, "possession marker expires next turn");
                Require(death.TryKill(1, DamageSource.None), "natural death accepted");
                Require(state.KillScores[0] == 0, "natural death awards no kill");
                Require(!death.TryKill(1, DamageSource.Create(0, DamageOrigin.Item)), "duplicate death rejected");
                Require(death.TryKill(2, DamageSource.Create(2, DamageOrigin.Item)) && state.KillScores[2] == 0,
                    "self kill awards no score");
                Require(death.TryKill(3, DamageSource.Create(0, DamageOrigin.GhostFrost)) && state.KillScores[0] == 1,
                    "ghost receives kill credit");
                Require(death.ConsumeDeathMask() == 14 && death.ConsumeDeathMask() == 0, "death mask consumed once");
                ghosts.Dispose(); ghosts.Dispose();
                Require(!ghosts.TryUseGrudge(0, 1, state, death, players, roster), "disposed ghost service rejects calls");
            }
            finally
            {
                for (int i = 0; i < _count; i++)
                {
                    roster.SetLifeState((byte)i, LifeState.Alive);
                    players[i].Temperature.Value = temperatures[i];
                    var slots = players[i].GetInventory().SlotStates;
                    slots.Clear(); foreach (var slot in inventories[i]) slots.Add(slot);
                }
                state.ServerResetKillScores(); state.GhostCooldowns.Clear();
            }
        }
        void CheckInventory(PlayerState[] players)
        {
            var a = players[0].GetInventory(); var b = players[1].GetInventory();
            var savedA = Enumerable.Range(0, a.SlotStates.Count).Select(i => a.SlotStates[i]).ToArray();
            var savedB = Enumerable.Range(0, b.SlotStates.Count).Select(i => b.SlotStates[i]).ToArray();
            float savedTemperature = players[0].Temperature.Value;
            var items = ItemManager.Instance.GetAllItems();
            short attack = (short)Array.FindIndex(items, i => i.ItemName == "Ice Cream");
            short alternate = (short)Array.FindIndex(items, i => i.ItemName == "Hot Pack");
            short sub = (short)Array.FindIndex(items, i => i.SlotType == ItemSlotType.Sub && i.Persistence == ItemPersistence.RandomConsumable);
            var queue = players[0].GetActionQueue();
            try
            {
                a.SlotStates.Clear(); b.SlotStates.Clear();
                a.GrantSpecificItem(attack);
                var copy = a.SlotStates[0]; copy.RemainingUses = 2; a.SlotStates[0] = copy;
                a.SlotStates.Add(copy); a.SlotStates.Add(copy);
                var constrained = new ItemDropTable(new[] { items[attack], items[alternate] });
                Require(a.FillRandomSlotsWithSeparateCopies(4, constrained) == 1, "top-up grants only one empty place");
                Require(a.SlotStates.Count == 4 && a.SlotStates[0].RemainingUses == 2
                    && a.SlotStates[1].RemainingUses == 2 && a.SlotStates[2].RemainingUses == 2,
                    "top-up preserves existing copies and uses");
                Require(a.SlotStates[3].ItemId == alternate, "top-up enforces three-copy limit");
                Require(a.FillRandomSlotsWithSeparateCopies(4, constrained) == 0, "full capacity grants nothing");

                if (_count == 4)
                {
                    short redCard = (short)Array.FindIndex(items, i => i.ItemName == "Red Card");
                    short soda = (short)Array.FindIndex(items, i => i.ItemName == "Soda");
                    Require(redCard >= 0 && soda >= 0, "top-up fixture items exist");
                    a.SlotStates.Clear(); b.SlotStates.Clear();
                    Require(a.GrantSpecificItem(attack) && a.GrantSpecificItem(alternate)
                        && a.GrantSpecificItem(redCard) && b.GrantSpecificItem(soda),
                        "top-up fixture inventories prepared");
                    var beforeA = Enumerable.Range(0, a.SlotStates.Count).Select(i => a.SlotStates[i]).ToArray();
                    var beforeB = Enumerable.Range(0, b.SlotStates.Count).Select(i => b.SlotStates[i]).ToArray();
                    queue.SetSelected(0, items[attack], 1);
                    var ruleForTopUp = MatchCompositionRoot.Instance.ActiveConfig.Rule;
                    bool preparedA = ItemManager.Instance.TryPrepareDeathmatchItems(a,
                        ruleForTopUp, out var planA);
                    bool preparedB = ItemManager.Instance.TryPrepareDeathmatchItems(b,
                        ruleForTopUp, out var planB);
                    Require(preparedA && preparedB,
                        "two-survivor top-up prepares both recipients");
                    Require(planA.CanApply() && planB.CanApply()
                        && planA.TryApply() && planB.TryApply(),
                        "two-survivor top-up commits both recipients");
                    Require(a.GetRandomSlotCount() == 4 && b.GetRandomSlotCount() == 4,
                        "three and one random items filled to four");
                    Require(beforeA.Select((slot, i) => a.SlotStates[i].Equals(slot)).All(v => v)
                        && beforeB.Select((slot, i) => b.SlotStates[i].Equals(slot)).All(v => v)
                        && queue.selectedAction.HasValue && queue.selectedAction.Value.SlotIndex == 0,
                        "top-up preserves old copies and selected slot");
                    planB.Restore(); planA.Restore();
                    Require(a.SlotStates.Count == beforeA.Length && b.SlotStates.Count == beforeB.Length
                        && beforeA.Select((slot, i) => a.SlotStates[i].Equals(slot)).All(v => v)
                        && beforeB.Select((slot, i) => b.SlotStates[i].Equals(slot)).All(v => v),
                        "top-up rollback restores both exact inventories");

                    a.SlotStates.Clear(); b.SlotStates.Clear();
                    Require(a.GrantSpecificItem(attack) && a.GrantSpecificItem(alternate)
                        && a.GrantSpecificItem(redCard) && a.GrantSpecificItem(soda),
                        "four and zero top-up fixture prepared");
                    var fourBefore = Enumerable.Range(0, a.SlotStates.Count)
                        .Select(i => a.SlotStates[i]).ToArray();
                    Require(ItemManager.Instance.TryPrepareDeathmatchItems(a, ruleForTopUp, out planA)
                        && ItemManager.Instance.TryPrepareDeathmatchItems(b, ruleForTopUp, out planB)
                        && planA.TryApply() && planB.TryApply(),
                        "four and zero top-up commits");
                    Require(a.GetRandomSlotCount() == 4 && b.GetRandomSlotCount() == 4
                        && fourBefore.Select((slot, i) => a.SlotStates[i].Equals(slot)).All(v => v),
                        "full survivor stays unchanged and empty survivor receives four");
                    planB.Restore(); planA.Restore();

                    a.SlotStates.RemoveAt(a.SlotStates.Count - 1);
                    Require(b.GrantSpecificItem(soda), "stale recipient fixture prepared");
                    var threeBefore = Enumerable.Range(0, a.SlotStates.Count)
                        .Select(i => a.SlotStates[i]).ToArray();
                    Require(ItemManager.Instance.TryPrepareDeathmatchItems(a, ruleForTopUp, out planA)
                        && ItemManager.Instance.TryPrepareDeathmatchItems(b, ruleForTopUp, out planB)
                        && planA.TryApply(), "first recipient applied before stale second recipient");
                    var changedSecond = b.SlotStates[0];
                    changedSecond.RemainingUses = (byte)(changedSecond.RemainingUses + 1);
                    b.SlotStates[0] = changedSecond;
                    Require(!planB.TryApply(), "stale second recipient rejected");
                    planB.Restore(); planA.Restore();
                    Require(threeBefore.Select((slot, i) => a.SlotStates[i].Equals(slot)).All(v => v)
                        && a.SlotStates.Count == threeBefore.Length
                        && b.SlotStates.Count == 1 && b.SlotStates[0].Equals(changedSecond),
                        "rollback restores applied recipient without overwriting untouched recipient");
                    Require(ItemManager.Instance.TryPrepareDeathmatchItems(a, ruleForTopUp, out planA)
                        && planA.TryApply(), "independent-write rollback fixture prepared");
                    int lastIndex = a.SlotStates.Count - 1;
                    var changedApplied = a.SlotStates[lastIndex];
                    changedApplied.RemainingUses = 42;
                    a.SlotStates[lastIndex] = changedApplied;
                    bool refusedStaleRestore = false;
                    try { planA.Restore(); }
                    catch (InvalidOperationException) { refusedStaleRestore = true; }
                    Require(refusedStaleRestore && a.SlotStates[lastIndex].Equals(changedApplied),
                        "rollback refuses to overwrite an independent change to an applied recipient");
                    queue.Clear();
                }

                a.SlotStates.Clear(); a.GrantSpecificItem(attack);
                queue.SetSelected(0, items[attack], 1);
                b.StealRandomItem(a);
                Require(!queue.selectedAction.HasValue && a.SlotStates[0].IsEmpty, "stealing selected copy cancels selection");
                a.SlotStates.Clear(); a.GrantSpecificItem(sub);
                queue.SetSelected(0, items[sub], 1);
                a.RerollAllRandom(new ItemDropTable(new[] { items[sub] }));
                Require(!queue.selectedAction.HasValue, "reroll cancels even same-ID replacement");
                a.SlotStates.Clear(); a.SlotStates.Add(ItemSlotNetData.Empty); a.GrantSpecificItem(attack);
                queue.SetSelected(1, items[attack], 1);
                a.CompactSlots();
                Require(queue.selectedAction.HasValue && queue.selectedAction.Value.SlotIndex == 0,
                    "compaction retains selected copy");
                queue.Clear();

                var rule = MatchCompositionRoot.Instance.ActiveConfig.Rule;
                var table = ItemManager.Instance.GetRuleAwareDropTable(rule);
                CheckInventoryDrawContracts(players, attack, alternate, sub, table);
                {
                    Require(table.Roll(i => i is SpecialItemDataSO s && s.SpecialEffect == SpecialEffectType.RevealOpponent) == null,
                        "actual mode table cannot roll inactive Tarot");
                    Require(ItemManager.Instance.GetDropTable().Roll(i => !ItemAvailability.IsEnabled(i)) == null,
                        "legacy drop table also excludes inactive entries");
                    var thresholds = new bool[3];
                    a.SlotStates.Clear(); players[0].Temperature.Value = 9;
                    new TemperatureSystem().CheckThresholds(players[0], a, thresholds, table, _count > 2, rule.MaxRandomItems);
                    Require(thresholds.All(x => x), "30/20/10 thresholds recorded");
                    Require(Enumerable.Range(0, a.SlotStates.Count).All(i => a.GetItemData(i) is not SpecialItemDataSO s
                        || s.SpecialEffect != SpecialEffectType.RevealOpponent), "threshold grants exclude Tarot");
                    string before = string.Join(",", Enumerable.Range(0, a.SlotStates.Count).Select(i => a.SlotStates[i].ItemId + "/" + a.SlotStates[i].RemainingUses));
                    new TemperatureSystem().CheckThresholds(players[0], a, thresholds, table, _count > 2, rule.MaxRandomItems);
                    Require(before == string.Join(",", Enumerable.Range(0, a.SlotStates.Count).Select(i => a.SlotStates[i].ItemId + "/" + a.SlotStates[i].RemainingUses)),
                        "threshold grant is idempotent");
                }
                short inactive = (short)Array.FindIndex(items, i => !ItemAvailability.IsEnabled(i));
                Require(inactive >= 0, "inactive entry retains its catalog index");
                a.SlotStates.Clear(); b.SlotStates.Clear();
                Require(!a.GrantSpecificItem(inactive) && a.SlotStates.Count == 0,
                    "explicit inactive grant rejected without mutation");
                b.SlotStates.Add(b.AssignNewCopyId(new ItemSlotNetData { ItemId = inactive, RemainingUses = 1 }));
                uint beforeSteal = b.CurrentFingerprint();
                a.StealRandomItem(b);
                Require(a.SlotStates.Count == 0 && b.CurrentFingerprint() == beforeSteal,
                    "legacy steal cannot transfer stale inactive entry");
                if (_count > 2)
                {
                    var mutator = new SeatInventoryMutator(players, MatchCompositionRoot.Instance.Roster, table, _count);
                    Require(mutator.TryPrepareMutation(InventoryMutationType.StealFromTarget, 0, 1, out var steal)
                        && steal.TryApply() && a.SlotStates.Count == 0 && b.CurrentFingerprint() == beforeSteal,
                        "prepared Multi steal cannot transfer stale inactive entry");
                }
            }
            finally
            {
                a.SlotStates.Clear(); foreach (var slot in savedA) a.SlotStates.Add(slot);
                b.SlotStates.Clear(); foreach (var slot in savedB) b.SlotStates.Add(slot);
                players[0].Temperature.Value = savedTemperature;
                queue.Clear();
            }
        }
    }
}
#endif
