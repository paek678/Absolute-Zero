using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Turn
{
    // Borrowed match state is read only during one synchronous attack operation.
    // Each action gets a fresh snapshot after the previous committed action/top-up.
    internal sealed class MultiCombatCapture
    {
        readonly PlayerState[] _players;
        readonly PlayerModifiers[] _modifiers;
        readonly float[] _tempsAtTurnStart;
        readonly ItemManager _itemManager;
        readonly MatchCompositionRoot _match;
        readonly IGameModeRule _gameRule;
        readonly EnvironmentType _environment;
        readonly GhostSkillService _ghostSkillService;
        internal PlayerState[] Players => _players;
        internal MatchRoster Roster => _match.Roster;
        internal IGameModeRule Rule => _gameRule;
        internal ItemManager Items => _itemManager;

        internal MultiCombatCapture(PlayerState[] players, PlayerModifiers[] modifiers,
            float[] temperaturesAtTurnStart, ItemManager items, MatchCompositionRoot match,
            IGameModeRule rule, EnvironmentType environment, GhostSkillService ghostSkills)
        {
            _players = players; _modifiers = modifiers; _tempsAtTurnStart = temperaturesAtTurnStart;
            _itemManager = items; _match = match; _gameRule = rule;
            _environment = environment; _ghostSkillService = ghostSkills;
        }

        internal static ItemEffectRuleSnapshot[] BuildItemRules(ItemManager items)
        {
            var allItems = items.GetAllItems();
            if (allItems == null) return System.Array.Empty<ItemEffectRuleSnapshot>();
            var rules = new ItemEffectRuleSnapshot[allItems.Length];
            for (int i = 0; i < allItems.Length; i++)
            {
                if (allItems[i] == null) continue;
                rules[i] = ItemEffectRuleSnapshot.From(allItems[i], (short)i);
            }
            return rules;
        }

        internal ActionIntent[] BuildActionIntents()
        {
            var intents = new ActionIntent[_players.Length];
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null) { intents[i] = ActionIntent.Empty; continue; }
                if (_players[i].CurrentLifeState.Value != LifeState.Alive)
                    { intents[i] = ActionIntent.Empty; continue; }
                var q = _players[i].GetActionQueue();
                if (!q.selectedAction.HasValue) { intents[i] = ActionIntent.Empty; continue; }

                var action = q.selectedAction.Value;
                var inv = _players[i].GetInventory();
                if (action.SlotIndex < 0 || action.SlotIndex >= inv.SlotStates.Count)
                {
                    Debug.LogError($"[TurnManager] BuildActionIntents: P{i} SlotIndex {action.SlotIndex} out of range ({inv.SlotStates.Count}) — skipping");
                    intents[i] = ActionIntent.Empty;
                    continue;
                }
                if ((action.CopyId == 0
                    || inv.SlotStates[action.SlotIndex].CopyId != action.CopyId
                    || !inv.SlotStates[action.SlotIndex].IsUsable
                    || inv.GetItemData(action.SlotIndex) != action.ItemData))
                {
                    intents[i] = ActionIntent.Empty;
                    continue;
                }
                short itemId = inv.SlotStates[action.SlotIndex].ItemId;
                byte targetSeat = action.TargetSeat;

                intents[i] = new ActionIntent(
                    sourceSeat: (byte)i,
                    slotIndex: (byte)action.SlotIndex,
                    itemId: itemId,
                    targetSeat: targetSeat,
                    readyServerTick: (int)(q.readyTimestamp * 1000f));
            }
            return intents;
        }

        internal ActionIntent BuildActionIntentForSeat(int seat)
        {
            if (seat < 0 || seat >= _players.Length || _players[seat] == null
                || _players[seat].CurrentLifeState.Value != LifeState.Alive)
                return ActionIntent.Empty;
            var queue = _players[seat].GetActionQueue();
            if (!queue.selectedAction.HasValue) return ActionIntent.Empty;
            var action = queue.selectedAction.Value;
            var inventory = _players[seat].GetInventory();
            if (inventory == null || action.SlotIndex >= inventory.SlotStates.Count)
                return ActionIntent.Empty;
            var slot = inventory.SlotStates[action.SlotIndex];
            if (!slot.IsUsable || action.CopyId == 0 || slot.CopyId != action.CopyId
                || inventory.GetItemData(action.SlotIndex) != action.ItemData)
                return ActionIntent.Empty;
            return new ActionIntent((byte)seat, action.SlotIndex, slot.ItemId,
                action.TargetSeat, (int)(queue.readyTimestamp * 1000f));
        }

        internal MatchCombatSnapshot BuildSnapshot(ItemEffectRuleSnapshot[] itemRules)
        {
            int count = _players.Length;
            var currentTemps = new float[count];
            var mods = new PlayerModifiers[count];
            var lifeStates = new LifeState[count];
            var inventories = new InventorySnapshot[count];
            var isReady = new bool[count];
            var killScores = new int[count];

            var mcr = _match;
            var roster = mcr?.Roster;

            for (int i = 0; i < count; i++)
            {
                if (_players[i] == null)
                {
                    currentTemps[i] = 0f;
                    lifeStates[i] = LifeState.Ghost;
                    inventories[i] = new InventorySnapshot((byte)i, System.Array.Empty<SlotSnapshot>());
                    continue;
                }
                currentTemps[i] = _players[i].Temperature.Value;
                mods[i] = _modifiers[i];
                lifeStates[i] = roster != null ? roster.GetLifeState((byte)i) : LifeState.Alive;
                isReady[i] = _players[i].IsReady.Value;

                var inv = _players[i].GetInventory();
                var slotCount = inv.SlotStates.Count;
                var slots = new SlotSnapshot[slotCount];
                for (int s = 0; s < slotCount; s++)
                {
                    var slot = inv.SlotStates[s];
                    slots[s] = new SlotSnapshot(slot.ItemId, slot.IsUnlimited, slot.RemainingUses);
                }
                inventories[i] = new InventorySnapshot((byte)i, slots);
            }

            if (mcr?.NetworkState?.KillScores != null)
                for (int i = 0; i < count && i < mcr.NetworkState.KillScores.Count; i++)
                    killScores[i] = mcr.NetworkState.KillScores[i];

            var ruleSnap = _gameRule != null
                ? GameModeRuleSnapshot.From(_gameRule)
                : default;

            return new MatchCombatSnapshot(
                _tempsAtTurnStart, currentTemps, mods, lifeStates,
                inventories, System.Array.Empty<ScheduledEffectSnapshot>(),
                killScores, isReady, _environment, ruleSnap, itemRules,
                _ghostSkillService?.Ledger.PossessedMask ?? 0);
        }

        internal int[] CaptureKillScores()
        {
            var scores = _match?.NetworkState?.KillScores;
            var copy = new int[_players.Length];
            if (scores != null)
                for (int i = 0; i < copy.Length && i < scores.Count; i++) copy[i] = scores[i];
            return copy;
        }
    }
}
