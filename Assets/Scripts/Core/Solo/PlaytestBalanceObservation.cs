#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    [Serializable]
    public struct BalanceActionIdentity
    {
        public long id;
        public int source, target, item;
        public uint copy;
    }

    [Serializable]
    public sealed class BalanceObservation
    {
        public int version = 1;
        public BalanceActionIdentity action;
        public string reason;
        public long effect;
        public int target = -1, defenseItem = -1;
        public float before, after, requested, mitigated, clipped;
        public double seconds;
        public int[] order;
        public PlaytestItemRecord previousSlot, nextSlot;
    }

    // Immutable-at-emission DTOs only. No replicated state, gameplay timing or random draws.
    public sealed partial class PlaytestTelemetry
    {
        long _nextAction, _nextEffect;
        double _prepOpened = -1;
        BalanceActionIdentity _balanceAction;
        public BalanceActionIdentity CurrentBalanceAction => _balanceAction;
        bool BalanceServer => !_closed && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        public void Balance(string kind, BalanceObservation data)
        {
            if (!BalanceServer) return;
            Record("balance_" + kind, "", data.action.source, data.action.copy, balance: data);
        }

        public static BalanceObservation Impact(BalanceActionIdentity action, int target, float before,
            float after, float requested, float mitigated = 0, int defenseItem = -1, string reason = "immediate")
        {
            float blocked = Mathf.Min(Mathf.Abs(requested), Mathf.Max(0, mitigated));
            return new BalanceObservation { action = action, target = target, before = before, after = after,
                requested = requested, mitigated = blocked,
                clipped = Mathf.Max(0, Mathf.Abs(requested) - blocked - Mathf.Abs(after - before)),
                defenseItem = defenseItem, reason = reason };
        }

        public static BalanceActionScope BeginItem(int actor, int target, int item, uint copy)
        {
            var recorder = Current;
            if (recorder == null || !recorder.BalanceServer || recorder._balanceAction.id != 0) return default;
            var id = new BalanceActionIdentity { id = ++recorder._nextAction, source = actor, target = target, item = item, copy = copy };
            recorder._balanceAction = id;
            recorder.Balance("action_begin", new BalanceObservation { action = id });
            return new BalanceActionScope(recorder, id);
        }

        public readonly struct BalanceActionScope : IDisposable
        {
            readonly PlaytestTelemetry _owner;
            readonly BalanceActionIdentity _id;
            internal BalanceActionScope(PlaytestTelemetry owner, BalanceActionIdentity id) { _owner = owner; _id = id; }
            public void Dispose()
            {
                if (_owner == null) return;
                _owner.Balance("action_end", new BalanceObservation { action = _id });
                _owner._balanceAction = default;
            }
        }

        public void PrepOpened()
        {
            _prepOpened = Time.realtimeSinceStartupAsDouble;
            Balance("prep_open", new BalanceObservation());
        }
        public void ReadyAccepted(PlayerState player)
            => Balance("ready", new BalanceObservation {
                action = new BalanceActionIdentity { source = player.PlayerIndex, item = -1 },
                seconds = _prepOpened < 0 ? -1 : Time.realtimeSinceStartupAsDouble - _prepOpened,
                reason = player.IsBotControlled ? "bot" : "human_driver" });
        public void ActionOrder(int[] seats)
            => Balance("order", new BalanceObservation { order = (int[])seats.Clone() });
        public void Consumed(int actor, ItemSlotNetData slot)
            => Balance("consume", new BalanceObservation { action = _balanceAction.id != 0 ? _balanceAction :
                new BalanceActionIdentity { source = actor, item = slot.ItemId, copy = slot.CopyId },
                before = slot.RemainingUses, after = slot.IsUnlimited ? slot.RemainingUses : Math.Max(0, slot.RemainingUses - 1),
                reason = slot.IsUnlimited ? "unlimited" : "limited" });

        public long Scheduled(int target, EffectType type, float value, int delay)
        {
            long id = ++_nextEffect;
            Balance("scheduled", new BalanceObservation { action = _balanceAction, effect = id, target = target,
                requested = value, seconds = delay, reason = type.ToString() });
            return id;
        }
        public void ScheduledEnd(BalanceActionIdentity action, long effect, int target, string reason)
            => Balance("schedule_end", new BalanceObservation { action = action, effect = effect, target = target, reason = reason });

        public void MultiCommitted(MultiCombatResolution resolution, ItemSlotNetData[] consumedSlots)
        {
            if (!BalanceServer) return;
            if (resolution.BalanceImpacts != null)
                foreach (var impact in resolution.BalanceImpacts)
                { impact.action = _balanceAction; Balance("impact", impact); }
            for (int i = 0; _balanceAction.id != 0 && i < resolution.EventCount; i++)
            {
                var evt = resolution.OrderedEvents[i];
                Balance("item_result", new BalanceObservation { action = _balanceAction, target = evt.TargetPlayer,
                    reason = evt.Type.ToString(), defenseItem = evt.DefenseItemId });
            }
            for (int i = 0; i < resolution.InventoryChangeCount; i++)
            {
                var change = resolution.InventoryChanges[i];
                if (!change.Consumed) continue;
                var slot = consumedSlots[i];
                bool standalone = _balanceAction.id == 0;
                using var scope = BeginItem(change.SeatIndex, change.SeatIndex, change.ItemId, slot.CopyId);
                if (standalone)
                    for (int e = 0; e < resolution.EventCount; e++)
                    {
                        var evt = resolution.OrderedEvents[e];
                        if (evt.SourcePlayer == change.SeatIndex)
                            Balance("item_result", new BalanceObservation { action = _balanceAction, target = evt.TargetPlayer, reason = evt.Type.ToString() });
                    }
                Consumed(change.SeatIndex, slot);
                if (resolution.ModifierChanges[change.SeatIndex].ActiveDefense.HasValue)
                    Balance("defense_active", new BalanceObservation { action = _balanceAction,
                        defenseItem = change.ItemId, requested = resolution.ModifierChanges[change.SeatIndex].ActiveDefense.Value.BlockAmount });
            }
        }
    }
}
#endif
