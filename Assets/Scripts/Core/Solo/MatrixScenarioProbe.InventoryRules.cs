#if UNITY_EDITOR || DEVELOPMENT_BUILD
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class MatrixScenarioProbe
    {
        // Exercise actual authoritative writers, including exactly where a no-op still draws.
        void CheckInventoryDrawContracts(PlayerState[] players, short firstId, short secondId, short subId, ItemDropTable table)
        {
            var actor = players[0].GetInventory();
            var target = players[1].GetInventory();
            var random = Random.state;
            var mutator = new SeatInventoryMutator(players, Match.MatchCompositionRoot.Instance.Roster, table, _count);
            try
            {
                for (int policy = 0; policy < (_count > 2 ? 2 : 1); policy++)
                {
                    bool prepared = policy == 1;
                    actor.SlotStates.Clear(); target.SlotStates.Clear();
                    var before = Random.state;
                    Steal();
                    Require(Random.state.Equals(before), "empty steal has zero random draws policy=" + policy);

                    // Full destination: candidate draw still occurs, but neither inventory changes.
                    for (int i = 0; i < 12; i++) actor.SlotStates.Add(Copy(secondId, 1));
                    target.SlotStates.Add(Copy(firstId, 2));
                    uint actorBefore = actor.CurrentFingerprint(), targetBefore = target.CurrentFingerprint();
                    Random.InitState(3706); before = Random.state;
                    Random.Range(0, 1); var afterDraw = Random.state; Random.state = before;
                    Steal();
                    Require(Random.state.Equals(afterDraw) && actor.CurrentFingerprint() == actorBefore
                        && target.CurrentFingerprint() == targetBefore, "full steal consumes one draw without mutation policy=" + policy);

                    actor.SlotStates.Clear(); target.SlotStates.Clear();
                    target.SlotStates.Add(ItemSlotNetData.Empty);
                    target.SlotStates.Add(Copy(firstId, 2));
                    target.SlotStates.Add(Copy(secondId, 255));
                    target.SlotStates.Add(Copy(secondId, 3));
                    Random.InitState(9306); before = Random.state;
                    int selected = new[] { 1, 3 }[Random.Range(0, 2)];
                    afterDraw = Random.state; Random.state = before;
                    var victim = target.SlotStates[selected];
                    Steal();
                    Require(Random.state.Equals(afterDraw) && actor.SlotStates.Count == 1
                        && actor.SlotStates[0].ItemId == victim.ItemId && actor.SlotStates[0].RemainingUses == victim.RemainingUses
                        && target.SlotStates[selected].IsEmpty && !target.SlotStates[selected == 1 ? 3 : 1].IsEmpty,
                        "candidate order skips holes and unlimited copies policy=" + policy);

                    actor.SlotStates.Clear(); target.SlotStates.Clear();
                    actor.SlotStates.Add(Copy(firstId, 250)); target.SlotStates.Add(Copy(firstId, 10));
                    uint retainedCopy = actor.SlotStates[0].CopyId;
                    Steal();
                    Require(actor.SlotStates.Count == 1 && actor.SlotStates[0].RemainingUses == 254
                        && actor.SlotStates[0].CopyId == retainedCopy && target.SlotStates[0].IsEmpty,
                        "stack saturates below unlimited and retains destination identity policy=" + policy);

                    if (prepared)
                    {
                        actor.SlotStates.Clear(); target.SlotStates.Clear();
                        target.SlotStates.Add(Copy(firstId, 1));
                        Require(mutator.TryPrepareMutation(InventoryMutationType.StealFromTarget, 0, 1, out var plan),
                            "stale steal prepared");
                        var changed = target.SlotStates[0]; changed.RemainingUses = 2; target.SlotStates[0] = changed;
                        var beforeApply = Random.state;
                        Require(!plan.TryApply() && actor.SlotStates.Count == 0 && target.SlotStates[0].Equals(changed)
                            && Random.state.Equals(beforeApply), "stale steal rejects atomically without another draw");
                    }

                    actor.SlotStates.Clear();
                    short mainId = (short)System.Array.FindIndex(ItemManager.Instance.GetAllItems(),
                        item => item != null && item.SlotType == ItemSlotType.Main);
                    Require(mainId >= 0, "reroll fixture resolves a real Main item");
                    var untouchedMain = Copy(mainId, 2);
                    var untouchedUnlimited = Copy(subId, 255);
                    actor.SlotStates.Add(untouchedMain); actor.SlotStates.Add(Copy(subId, 2));
                    actor.SlotStates.Add(untouchedUnlimited); actor.SlotStates.Add(ItemSlotNetData.Empty);
                    actor.SlotStates.Add(Copy(subId, 1));
                    uint rerollCopy1 = actor.SlotStates[1].CopyId, rerollCopy4 = actor.SlotStates[4].CopyId;
                    var subItem = ItemManager.Instance.GetItemData(subId);
                    var rerollTable = new ItemDropTable(new[] { subItem });
                    before = Random.state;
                    Random.Range(0f, subItem.DropWeight); Random.Range(0f, subItem.DropWeight);
                    afterDraw = Random.state; Random.state = before;
                    if (prepared)
                    {
                        var reroll = new SeatInventoryMutator(players, Match.MatchCompositionRoot.Instance.Roster, rerollTable, _count);
                        Require(reroll.TryPrepareMutation(InventoryMutationType.RerollTarget, 1, 0, out var plan)
                            && plan.TryApply(), "prepared reroll transaction applies");
                    }
                    else actor.RerollAllRandom(rerollTable);
                    Require(Random.state.Equals(afterDraw) && actor.SlotStates.Count == 5
                        && actor.SlotStates[0].Equals(untouchedMain) && actor.SlotStates[2].Equals(untouchedUnlimited)
                        && actor.SlotStates[3].IsEmpty && actor.SlotStates[1].CopyId != rerollCopy1
                        && actor.SlotStates[4].CopyId != rerollCopy4,
                        "reroll draws once per finite Sub in slot order and replaces copy identities policy=" + policy);

                    void Steal()
                    {
                        if (prepared)
                            Require(mutator.TryPrepareMutation(InventoryMutationType.StealFromTarget, 0, 1, out var plan)
                                && plan.TryApply(), "prepared steal transaction applies");
                        else actor.StealRandomItem(target);
                    }
                }
            }
            finally { Random.state = random; }

            ItemSlotNetData Copy(short id, byte uses)
                => actor.AssignNewCopyId(new ItemSlotNetData { ItemId = id, RemainingUses = uses });
        }
    }
}
#endif
