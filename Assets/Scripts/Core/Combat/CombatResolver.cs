using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Combat
{
    public class CombatResolver
    {
        public CombatResult Resolve(
            ActionQueue p1Queue, ActionQueue p2Queue,
            PlayerModifiers[] modifiers,
            PlayerState p1, PlayerState p2,
            TemperatureSystem tempSystem,
            BuffDebuffSystem buffSystem)
        {
            var result = new CombatResult();

            int firstIdx = DetermineOrder(p1Queue, p2Queue, p1, p2);
            int secondIdx = 1 - firstIdx;
            result.FirstPlayerIndex = firstIdx;

            ApplyDefense(p1Queue, modifiers, 0, p1);
            ApplyDefense(p2Queue, modifiers, 1, p2);

            var firstQueue = firstIdx == 0 ? p1Queue : p2Queue;
            var secondQueue = secondIdx == 0 ? p1Queue : p2Queue;
            var firstPlayer = firstIdx == 0 ? p1 : p2;
            var secondPlayer = secondIdx == 0 ? p1 : p2;

            if (firstQueue.selectedAction.HasValue
                && !modifiers[firstIdx].ActionNeutralized
                && firstQueue.selectedAction.Value.ItemData is not DefenseItemDataSO)
            {
                result.Events.Add(ExecuteMain(
                    firstQueue.selectedAction.Value, firstPlayer, secondPlayer,
                    firstIdx, secondIdx, modifiers, tempSystem, buffSystem));
            }

            if (tempSystem.IsDead(secondPlayer))
            {
                result.WinnerIndex = firstIdx;
                return result;
            }
            if (tempSystem.IsDead(firstPlayer))
            {
                result.WinnerIndex = secondIdx;
                return result;
            }

            if (secondQueue.selectedAction.HasValue
                && !modifiers[secondIdx].ActionNeutralized
                && secondQueue.selectedAction.Value.ItemData is not DefenseItemDataSO)
            {
                result.Events.Add(ExecuteMain(
                    secondQueue.selectedAction.Value, secondPlayer, firstPlayer,
                    secondIdx, firstIdx, modifiers, tempSystem, buffSystem));
            }

            if (tempSystem.IsDead(firstPlayer))
                result.WinnerIndex = secondIdx;
            else if (tempSystem.IsDead(secondPlayer))
                result.WinnerIndex = firstIdx;

            return result;
        }

        int DetermineOrder(ActionQueue p1, ActionQueue p2, PlayerState p1State, PlayerState p2State)
        {
            if (p1.readyTimestamp < p2.readyTimestamp) return 0;
            if (p2.readyTimestamp < p1.readyTimestamp) return 1;

            if (p1State.Temperature.Value < p2State.Temperature.Value) return 0;
            if (p2State.Temperature.Value < p1State.Temperature.Value) return 1;

            return 0;
        }

        CombatEvent ExecuteMain(QueuedAction action,
                                 PlayerState user, PlayerState target,
                                 int userIdx, int targetIdx,
                                 PlayerModifiers[] modifiers,
                                 TemperatureSystem tempSystem, BuffDebuffSystem buffSystem)
        {
            short capturedItemId = user.GetInventory().SlotStates[action.SlotIndex].ItemId;

            var ctx = new ItemContext
            {
                User = user,
                Target = target,
                UserIndex = userIdx,
                TargetIndex = targetIdx,
                UserInventory = user.GetInventory(),
                TargetInventory = target.GetInventory(),
                AllModifiers = modifiers,
                TempSystem = tempSystem,
                BuffSystem = buffSystem,
                SlotIndex = action.SlotIndex,
                UserSlot = user.GetInventory().SlotStates[action.SlotIndex],
            };

            action.ItemData.ExecuteEffect(ctx);
            user.GetInventory().ConsumeItem(action.SlotIndex);

            return new CombatEvent
            {
                Type = CombatEventType.MainEffect,
                SourcePlayer = userIdx,
                TargetPlayer = targetIdx,
                ItemId = capturedItemId,
                UserResultTemp = user.Temperature.Value,
                TargetResultTemp = target.Temperature.Value
            };
        }

        void ApplyDefense(ActionQueue queue, PlayerModifiers[] modifiers, int playerIdx, PlayerState player)
        {
            if (queue.selectedAction.HasValue && queue.selectedAction.Value.ItemData is DefenseItemDataSO defItem)
            {
                modifiers[playerIdx].ActiveDefense = new DefenseInfo
                {
                    Filter = defItem.Filter,
                    BlockAmount = defItem.BlockAmount
                };
                player.GetInventory().ConsumeItem(queue.selectedAction.Value.SlotIndex);
            }
        }
    }
}
