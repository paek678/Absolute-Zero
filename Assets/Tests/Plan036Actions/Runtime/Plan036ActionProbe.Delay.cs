#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Turn;

namespace AbsoluteZero.Validation.Actions
{
    public sealed partial class Plan036ActionProbe
    {
        sealed class ControlledClock : IBotActionClock { public double Time; public double Now => Time; }
        void VerifyDelayMatrix(PrepInputKey key)
        {
            var inventory = _bot.GetInventory();
            var saved = new List<ItemSlotNetData>();
            foreach (var slot in inventory.SlotStates) saved.Add(slot);
            var settings = AppBootstrapper.Instance.SessionRouter.LaunchContext.Solo;
            float humanTemp = _human.Temperature.Value;
            float botTemp = _bot.Temperature.Value;
            try
            {
                for (short id = 0; id < Catalog.Length; id++)
                {
                    inventory.SlotStates.Clear();
                    if (!ItemAvailability.IsEnabled(Catalog[id]))
                    {
                        Check(!inventory.GrantSpecificItem(id) && inventory.SlotStates.Count == 0,
                            "inactive catalog item cannot be explicitly granted " + id);
                        var stale = inventory.AssignNewCopyId(new ItemSlotNetData { ItemId = id, RemainingUses = 1 });
                        inventory.SlotStates.Add(stale); // Explicit stale-save/debug injection, not a production grant.
                        uint staleBefore = inventory.CurrentFingerprint();
                        Check(_bot.ServerValidateBotItem(key, stale.CopyId, 0, out _).Reason == PlayerActionReason.ItemUnavailable
                            && inventory.CurrentFingerprint() == staleBefore && !_bot.HasSelectedItem.Value,
                            "inactive catalog item is rejected by shared bot admission without consumption " + id);
                        VerifyHumanInactiveItem(id);
                        continue;
                    }
                    Check(inventory.GrantSpecificItem(id), "delay fixture grants actual catalog item " + id);
                    uint copy = inventory.SlotStates[0].CopyId;
                    var item = Catalog[id];
                    byte target = item.GetTargetMode() == TargetMode.Self ? ActionIntent.NoTarget : (byte)0;
                    _human.IsReady.Value = false;
                    var clock = new ControlledClock { Time = _bot.NetworkManager.ServerTime.Time };
                    var adapter = _bot.DebugCreateBotCommands(clock);
                    uint before = inventory.CurrentFingerprint();
                    var begin = adapter.BeginUse(key, 2, copy, target);
                    Check(begin.Status == PlayerActionStatus.Pending && settings.TryGetItemDelay(id, out float delay)
                        && Math.Abs(begin.DueTime - clock.Time - delay) < 0.0001
                        && !_bot.HasSelectedItem.Value && inventory.CurrentFingerprint() == before,
                        "catalog " + id + " begins configured delay without queue/effect/consume");
                    Check(adapter.Ready(key).Reason == PlayerActionReason.BotUsePending
                        && adapter.CancelSelection(key).Reason == PlayerActionReason.BotUsePending
                        && adapter.BeginUse(key, 3, copy, target).Reason == PlayerActionReason.BotUsePending
                        && _bot.ServerPressBotReady(key).Reason == PlayerActionReason.BotUsePending
                        && _bot.ServerCancelBotSelection(key).Reason == PlayerActionReason.BotUsePending,
                        "catalog " + id + " pending blocks Ready/cancel/second request and direct seam bypass");
                    _bot.ServerValidateBotItem(key, copy, target, out var candidate);
                    Check(_bot.ServerQueueBotItem(key, candidate).Reason == PlayerActionReason.BotUsePending,
                        "catalog " + id + " direct queue cannot bypass its pending delay");
                    clock.Time = begin.DueTime - 0.001;
                    Check(adapter.Poll(key, begin.OperationId).Status == PlayerActionStatus.Pending,
                        "catalog " + id + " waits until due");
                    clock.Time = begin.DueTime;
                    Check(adapter.Poll(key, begin.OperationId).Status == PlayerActionStatus.Queued
                        && adapter.Poll(key, begin.OperationId).Status == PlayerActionStatus.Queued
                        && adapter.BeginUse(key, 2, copy, target).Status == PlayerActionStatus.Queued
                        && _bot.GetActionQueue().selectedAction.Value.CopyId == copy
                        && inventory.CurrentFingerprint() == before && _botMiniGames == 0
                        && _human.Temperature.Value == humanTemp && _bot.Temperature.Value == botTemp,
                        "catalog " + id + " queues once at due, retains copy and changes no effects/uses");
                    Check(adapter.CancelSelection(key).Status == PlayerActionStatus.Cancelled,
                        "catalog " + id + " queued cancellation preserves inventory");
                }
                _human.IsReady.Value = false;
                inventory.SlotStates.Clear();
                inventory.GrantSpecificItem(ItemId("Water Gun"));
                uint attackCopy = inventory.SlotStates[0].CopyId;
                foreach (string scenario in new[] { "removed", "replacement", "target-dead", "actor-dead", "forced-ready", "basic-blocked", "deadline", "compaction" })
                {
                    inventory.SlotStates.Clear(); inventory.GrantSpecificItem(ItemId(scenario == "basic-blocked" ? "Fan" : "Water Gun"));
                    attackCopy = inventory.SlotStates[0].CopyId;
                    if (scenario == "compaction") inventory.SlotStates.Insert(0, ItemSlotNetData.Empty);
                    var clock = new ControlledClock { Time = _bot.NetworkManager.ServerTime.Time };
                    var adapter = _bot.DebugCreateBotCommands(clock);
                    var begin = adapter.BeginUse(key, 1, attackCopy, 0);
                    Check(begin.Status == PlayerActionStatus.Pending, scenario + " starts real pending attack");
                    if (scenario == "removed") inventory.SlotStates.Clear();
                    if (scenario == "replacement") inventory.SlotStates[0] = inventory.AssignNewCopyId(inventory.SlotStates[0]);
                    if (scenario == "target-dead") _human.Temperature.Value = 0;
                    if (scenario == "actor-dead") _bot.Temperature.Value = 0;
                    if (scenario == "forced-ready") _bot.IsReady.Value = true;
                    if (scenario == "basic-blocked") _bot.IsBasicBlocked.Value = true;
                    if (scenario == "compaction")
                    {
                        inventory.CompactSlots();
                    }
                    clock.Time = begin.DueTime;
                    if (scenario == "deadline") { _turn.TryGetPrepInputSnapshot(out var window); clock.Time = window.Deadline; }
                    uint before = inventory.CurrentFingerprint();
                    var result = adapter.Poll(key, 1);
                    Check(result.Status == (scenario == "compaction" ? PlayerActionStatus.Queued : PlayerActionStatus.Cancelled)
                        && inventory.CurrentFingerprint() == before,
                        scenario + " revalidates current state without debit");
                    _human.Temperature.Value = humanTemp; _bot.Temperature.Value = botTemp; _bot.IsReady.Value = false; _bot.IsBasicBlocked.Value = false;
                    if (scenario == "compaction") adapter.CancelSelection(key);
                    else Check(adapter.Poll(key, 1).Status == PlayerActionStatus.Cancelled,
                        scenario + " cancelled operation cannot resurrect");
                }
                Check(_botMiniGames == 0, "enabled delayed catalog items bypass only bot mini-game UI; inactive entries reject");
            }
            finally
            {
                _human.IsReady.Value = false; _bot.IsReady.Value = false; _bot.IsBasicBlocked.Value = false;
                _human.Temperature.Value = humanTemp; _bot.Temperature.Value = botTemp;
                _bot.GetBotCommands()?.Dispose();
                inventory.SlotStates.Clear(); foreach (var slot in saved) inventory.SlotStates.Add(slot);
                _bot.DebugCreateBotCommands(new NgoBotActionClock(_bot.NetworkManager));
            }
        }
        void VerifyHumanInactiveItem(short id)
        {
            var inventory = _human.GetInventory();
            var saved = new List<ItemSlotNetData>();
            foreach (var slot in inventory.SlotStates) saved.Add(slot);
            bool wasReady = _bot.IsReady.Value;
            try
            {
                inventory.SlotStates.Clear();
                inventory.SlotStates.Add(inventory.AssignNewCopyId(new ItemSlotNetData { ItemId = id, RemainingUses = 1 }));
                uint before = inventory.CurrentFingerprint();
                int tickets = _humanTickets.Count;
                // The obsolete Tarot rule required a Ready target. Ensure that
                // old condition cannot accidentally make this rejection pass.
                _bot.IsReady.Value = true;
                _human.SelectItemServerRpc(0, 1);
                Check(!_human.HasSelectedItem.Value && !_human.GetActionQueue().selectedAction.HasValue
                    && _humanTickets.Count == tickets && inventory.CurrentFingerprint() == before,
                    "human RPC rejects inactive entry even with Ready opponent; no ticket, queue or debit");
            }
            finally
            {
                _bot.IsReady.Value = wasReady;
                inventory.SlotStates.Clear(); foreach (var slot in saved) inventory.SlotStates.Add(slot);
            }
        }
        async Task VerifyActualClockDelay(PrepInputKey key, PlayerActionCandidate candidate)
        {
            _bot.ServerCancelBotSelection(key);
            var adapter = _bot.GetBotCommands();
            int uses = Uses(_bot.GetInventory(), candidate.CopyId);
            var begin = adapter.BeginUse(key, 1, candidate.CopyId, candidate.TargetSeat);
            Check(begin.Status == PlayerActionStatus.Pending, "normal NGO clock begins actual combat item delay");
            await Until(() => adapter.Poll(key, begin.OperationId).Status != PlayerActionStatus.Pending, 5, "actual NGO clock bot delay");
            Check(adapter.Poll(key, begin.OperationId).Status == PlayerActionStatus.Queued
                && Uses(_bot.GetInventory(), candidate.CopyId) == uses,
                "normal NGO clock delay queues before existing actual combat consumes");
        }
    }
}
#endif
