#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class VisualFourPlayerProbe
    {
        string _possessionCase;
        bool _possessionStaged;
        bool _possessionRequested;
        bool _possessionBeforeCaptured;
        bool _possessionSelected;
        bool _possessionReady;
        bool _possessionBatchSeen;
        bool _possessionSettling;
        bool _possessionCueSeen;
        bool _possessionMiniGameSubmitted;
        bool _possessionCounterSelected;
        bool _possessionRendererRemoved;
        bool _possessionAckFaultArmed;
        float _possessionObservedAt;
        float _possessionBatchAt;
        float _possessionCueAt;
        float _possessionAttackAt;
        uint _possessionCopyId;
        byte _possessionUsesBefore;
        short _possessionItemId = -1;
        PlayerState _possessionTicketOwner;

        static string PossessionItemName(string scenario)
        {
            switch (scenario)
            {
                case "attack": return "Ice Cream";
                case "attack-force": return "Ice Cream";
                case "attack-drop-ack": return "Ice Cream";
                case "attack-late-ack": return "Ice Cream";
                case "attack-disconnect": return "Ice Cream";
                case "heal": return "Warm Tea";
                case "defense": return "Mask";
                case "defense-counter": return "Mask";
                case "defense-counter-missing-renderer": return "Mask";
                case "multi-use": return "Smartphone";
                case "unlimited": return "Fan";
                case "minigame-success":
                case "minigame-failure": return "Hug T-shirt";
                default: return null;
            }
        }

        void UpdatePossessionCase(PlayerState local, PlayerState[] players,
            TurnManager turn, MatchNetworkState state)
        {
            string itemName = PossessionItemName(_possessionCase);
            if (itemName == null)
            {
                Fail("Unknown possession case: " + _possessionCase);
                return;
            }
            var manager = ItemManager.Instance;
            if (manager == null || turn.CurrentPhase.Value != TurnPhase.PrepPhase
                || players.Length != 4) return;

            if (_possessionItemId < 0)
            {
                var items = manager.GetAllItems();
                if (items == null) return;
                for (short i = 0; i < items.Length; i++)
                    if (items[i] != null && items[i].ItemName == itemName)
                    {
                        _possessionItemId = i;
                        break;
                    }
                if (_possessionItemId < 0)
                {
                    Fail("Possession item absent: " + itemName);
                    return;
                }
            }

            if (_role == "host" && !_possessionStaged)
            {
                var target = players.FirstOrDefault(p => p.PlayerIndex == 2);
                var inventory = target?.GetInventory();
                if (inventory == null || !inventory.IsRegistryReady) return;
                bool alreadyOwned = false;
                for (int i = 0; i < inventory.SlotStates.Count; i++)
                    if (!inventory.SlotStates[i].IsEmpty
                        && inventory.SlotStates[i].ItemId == _possessionItemId)
                    { alreadyOwned = true; break; }
                if (!alreadyOwned && !inventory.GrantSpecificItem(_possessionItemId))
                {
                    Fail("Could not stage possession item " + itemName);
                    return;
                }
                if (_possessionCase == "defense-counter"
                    || _possessionCase == "defense-counter-missing-renderer")
                {
                    short attackId = FindPossessionItemId(manager, "Ice Cream");
                    var attacker = players.FirstOrDefault(p => p.PlayerIndex == 0);
                    var attackInventory = attacker?.GetInventory();
                    if (attackId < 0 || attackInventory == null || !attackInventory.IsRegistryReady)
                        return;
                    bool hasAttack = false;
                    for (int i = 0; i < attackInventory.SlotStates.Count; i++)
                        if (!attackInventory.SlotStates[i].IsEmpty
                            && attackInventory.SlotStates[i].ItemId == attackId)
                        { hasAttack = true; break; }
                    if (!hasAttack && !attackInventory.GrantSpecificItem(attackId))
                    {
                        Fail("Could not stage defense counter attack");
                        return;
                    }
                }
                target.Temperature.Value = 20f;
                _possessionStaged = true;
                Debug.Log($"[VISUAL] POSSESSION_STAGED case={_possessionCase} actor=1 target=2 item={itemName} id={_possessionItemId}");
            }
            if (_role == "host" && _possessionStaged && !_ghostForced)
                _ghostForced = turn.DebugForceGhostForVisual(1);

            var ghost = players.FirstOrDefault(p => p.PlayerIndex == 1);
            if (ghost == null || ghost.CurrentLifeState.Value != LifeState.Ghost) return;
            if (!_ghostCaptureStarted)
            {
                _ghostCaptureStarted = true;
                _ghostObservedAt = Time.unscaledTime;
                StartCoroutine(CaptureGhostTransition());
            }

            if (_localSeat == 1 && !_possessionRequested
                && Time.unscaledTime - _ghostObservedAt >= 3.5f)
            {
                _possessionRequested = true;
                turn.UseGhostSkillRpc(GhostSkillService.SKILL_POSSESSION, 2,
                    state.GhostMatchEpoch.Value, state.GhostRoundEpoch.Value,
                    turn.TurnNumber.Value, 0x8000AB01u);
                Debug.Log($"[VISUAL] POSSESSION_REQUEST case={_possessionCase} actor=1 target=2");
            }

            if ((state.GhostPossessedMask.Value & (1 << 2)) == 0) return;
            if ((_possessionCase == "attack-drop-ack" || _possessionCase == "attack-late-ack")
                && _localSeat == 2 && !_possessionAckFaultArmed)
            {
                var vfx = CombatVFXManager.Instance;
                if (vfx == null) return;
                _possessionAckFaultArmed = true;
                bool drop = _possessionCase == "attack-drop-ack";
                vfx.DebugSetNextPresentationAckFault(drop ? 0f : 12f, drop);
                Debug.Log($"[VISUAL] POSSESSION_ACK_FAULT case={_possessionCase} local={_localSeat} drop={drop}");
            }
            if (_possessionCase == "defense-counter-missing-renderer"
                && _localSeat == 2 && !_possessionRendererRemoved)
            {
                var attacker = players.FirstOrDefault(p => p.PlayerIndex == 0);
                var visual = attacker?.GetComponent<AZPlayerVisual>();
                if (visual == null)
                {
                    Fail("Missing actor visual before renderer removal");
                    return;
                }
                var field = typeof(AZPlayerVisual).GetField("_itemRenderer",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var renderer = field?.GetValue(visual) as SpriteRenderer;
                if (renderer == null) return;
                _possessionRendererRemoved = true;
                Destroy(renderer);
                Debug.Log($"[VISUAL] POSSESSION_RENDERER_REMOVED case={_possessionCase} local={_localSeat} actor=0");
            }
            if (_possessionObservedAt == 0f)
            {
                _possessionObservedAt = Time.unscaledTime;
                Debug.Log($"[VISUAL] POSSESSION_MARKER case={_possessionCase} local={_localSeat} mask={state.GhostPossessedMask.Value}");
                Capture("possession-marker");
            }

            var targetPlayer = players.FirstOrDefault(p => p.PlayerIndex == 2);
            var targetInventory = targetPlayer?.GetInventory();
            if (targetInventory == null) return;
            if (!_possessionBeforeCaptured && Time.unscaledTime - _possessionObservedAt > 1f)
            {
                for (byte slot = 0; slot < targetInventory.SlotStates.Count; slot++)
                {
                    var value = targetInventory.SlotStates[slot];
                    if (value.IsEmpty || value.ItemId != _possessionItemId) continue;
                    _possessionCopyId = value.CopyId;
                    _possessionUsesBefore = value.RemainingUses;
                    _possessionBeforeCaptured = true;
                    Debug.Log($"[VISUAL] POSSESSION_BEFORE case={_possessionCase} local={_localSeat} copy={_possessionCopyId} uses={_possessionUsesBefore}");
                    break;
                }
            }
            if (!_possessionBeforeCaptured) return;

            if ((_possessionCase == "defense-counter"
                || _possessionCase == "defense-counter-missing-renderer") && _localSeat == 0
                && !_possessionCounterSelected
                && Time.unscaledTime - _possessionObservedAt > 2f)
            {
                short attackId = FindPossessionItemId(manager, "Ice Cream");
                var attackInventory = local.GetInventory();
                if (attackId < 0 || attackInventory == null) return;
                for (byte slot = 0; slot < attackInventory.SlotStates.Count; slot++)
                {
                    var value = attackInventory.SlotStates[slot];
                    if (value.IsEmpty || value.ItemId != attackId) continue;
                    _possessionCounterSelected = true;
                    ProbeInventoryCommands.SelectItem(local, slot, 2);
                    Debug.Log($"[VISUAL] POSSESSION_COUNTER_SELECT case={_possessionCase} slot={slot} copy={value.CopyId} target=2");
                    break;
                }
            }

            if (_localSeat == 2 && !_possessionSelected
                && Time.unscaledTime - _possessionObservedAt > 1.5f)
            {
                byte slot = byte.MaxValue;
                for (byte i = 0; i < targetInventory.SlotStates.Count; i++)
                    if (targetInventory.SlotStates[i].CopyId == _possessionCopyId
                        && !targetInventory.SlotStates[i].IsEmpty)
                    { slot = i; break; }
                if (slot == byte.MaxValue) return;
                var item = targetInventory.GetItemData(slot);
                if (item == null) return;
                _possessionSelected = true;
                if (item.RequiresMiniGame && _possessionTicketOwner == null)
                {
                    _possessionTicketOwner = local;
                    local.OnMiniGameStart += OnPossessionMiniGameStart;
                }
                byte targetSeat = item.GetTargetMode() == TargetMode.Self
                    ? ActionIntent.NoTarget : (byte)0;
                ProbeInventoryCommands.SelectItem(local, slot, targetSeat);
                Debug.Log($"[VISUAL] POSSESSION_SELECT case={_possessionCase} slot={slot} copy={_possessionCopyId} target={targetSeat}");
            }

            if (_localSeat != 1 && !_possessionReady)
            {
                bool targetCanReady = _possessionCase == "minigame-failure"
                    ? _possessionMiniGameSubmitted && Time.unscaledTime - _possessionObservedAt > 3f
                    : local.HasSelectedItem.Value;
                if (_localSeat == 2 && !targetCanReady) return;
                if (_localSeat != 2 && Time.unscaledTime - _possessionObservedAt < 4f) return;
                _possessionReady = true;
                ProbeInventoryCommands.Ready(local);
                Debug.Log($"[VISUAL] POSSESSION_READY case={_possessionCase} local={_localSeat} selected={local.HasSelectedItem.Value}");
            }
        }

        static short FindPossessionItemId(ItemManager manager, string name)
        {
            var items = manager.GetAllItems();
            if (items == null) return -1;
            for (short i = 0; i < items.Length; i++)
                if (items[i] != null && items[i].ItemName == name) return i;
            return -1;
        }

        void OnPossessionMiniGameStart(MiniGameTicket ticket)
        {
            if (string.IsNullOrEmpty(_possessionCase) || _possessionMiniGameSubmitted) return;
            StartCoroutine(SubmitPossessionMiniGame(ticket));
        }

        IEnumerator SubmitPossessionMiniGame(MiniGameTicket ticket)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            if (_possessionTicketOwner == null || !_possessionTicketOwner.IsSpawned) yield break;
            bool success = _possessionCase != "minigame-failure";
            _possessionMiniGameSubmitted = true;
            _possessionTicketOwner.SubmitMiniGameResultServerRpc(ticket.Slot, success,
                ticket.MatchEpoch, ticket.RoundEpoch, ticket.Turn,
                ticket.AttemptId, ticket.CopyId);
            Debug.Log($"[VISUAL] POSSESSION_MINIGAME case={_possessionCase} success={success} attempt={ticket.AttemptId}");
        }

        void UnsubscribePossessionTicket()
        {
            if (_possessionTicketOwner == null) return;
            _possessionTicketOwner.OnMiniGameStart -= OnPossessionMiniGameStart;
            _possessionTicketOwner = null;
        }

        void OnPossessionCombatResult(CombatResolutionBatchNetData batch)
        {
            if (_fullMatch) OnFullMatchCombatResult(batch);
            if (string.IsNullOrEmpty(_possessionCase)) return;
            _possessionBatchSeen = true;
            _possessionBatchAt = Time.realtimeSinceStartup;
            if (_possessionCase == "attack-force" && _localSeat == 2)
                StartCoroutine(ForcePossessionPresentation(batch.ResultSequence));
            if (_possessionCase == "attack-disconnect" && _localSeat == 3)
                StartCoroutine(DisconnectDuringPossessionPresentation(batch.ResultSequence));
            int suppressed = 0;
            int mainEffects = 0;
            for (int i = 0; i < batch.EventCount; i++)
            {
                if ((CombatEventType)batch.Events[i].EventType == CombatEventType.SuppressedItemUse
                    && batch.Events[i].ActorSeat == 2)
                    suppressed++;
                if ((CombatEventType)batch.Events[i].EventType == CombatEventType.MainEffect
                    && batch.Events[i].ActorSeat == 0)
                    mainEffects++;
            }
            bool temperatureStable = true;
            for (int i = 0; i < batch.SeatCount; i++)
                temperatureStable &= Mathf.Abs(batch.TempAfter[i] - batch.TempBefore[i]) < 0.01f;
            var schedule = MultiPresentationSchedule.Build(batch,
                id => ItemManager.Instance?.GetItemData(id)?.AnimDuration ?? 0f,
                id => ItemManager.Instance?.GetItemData(id)?.Category == ItemCategory.Defense);
            Debug.Log($"[VISUAL] POSSESSION_BATCH case={_possessionCase} local={_localSeat} seq={batch.ResultSequence} suppressed={suppressed} tempsStable={temperatureStable} events={batch.EventCount} actions={schedule.ActionCount} at={_possessionBatchAt:F3}");
            bool expectedSuppression = _possessionCase != "minigame-failure";
            bool counter = _possessionCase == "defense-counter"
                || _possessionCase == "defense-counter-missing-renderer";
            bool expectedTemps = counter
                ? batch.TempAfter[2] < batch.TempBefore[2]
                    && Mathf.Abs(batch.TempAfter[0] - batch.TempBefore[0]) < 0.01f
                : temperatureStable;
            int expectedActions = _possessionCase == "defense" || _possessionCase == "minigame-failure"
                ? 0 : 1;
            if (!expectedTemps || suppressed != (expectedSuppression ? 1 : 0)
                || schedule.ActionCount != expectedActions || mainEffects != (counter ? 1 : 0))
                Fail("Possession batch violated consume-only contract");
        }

        IEnumerator ForcePossessionPresentation(uint sequence)
        {
            yield return new WaitForSecondsRealtime(1f);
            var vfx = CombatVFXManager.Instance;
            if (vfx == null || !vfx.HasPendingPresentation(sequence))
            {
                Fail("Force-settlement target was not active");
                yield break;
            }
            Debug.Log($"[VISUAL] POSSESSION_FORCE case={_possessionCase} local={_localSeat} seq={sequence}");
            vfx.ForceSettleMultiPresentation(sequence);
        }

        IEnumerator DisconnectDuringPossessionPresentation(uint sequence)
        {
            yield return new WaitForSecondsRealtime(1f);
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient)
            {
                Fail("Disconnect fixture lost connection before VFX checkpoint");
                yield break;
            }
            Debug.Log($"[VISUAL] POSSESSION_DISCONNECT case={_possessionCase} local={_localSeat} seq={sequence}");
            Capture("possession-disconnect");
            nm.Shutdown();
            yield return Finish("possession-disconnect");
        }

        void OnPossessionCue(byte actorSeat)
        {
            if (_fullMatch) OnFullMatchSuppressionCue(actorSeat);
            if (string.IsNullOrEmpty(_possessionCase) || actorSeat != 2) return;
            _possessionCueSeen = true;
            _possessionCueAt = Time.realtimeSinceStartup;
            Debug.Log($"[VISUAL] POSSESSION_CUE case={_possessionCase} local={_localSeat} at={_possessionCueAt:F3}");
            StartCoroutine(CapturePossessionCue());
        }

        void OnPossessionAttackerChanged(int actorSeat)
        {
            if ((_possessionCase != "defense-counter"
                && _possessionCase != "defense-counter-missing-renderer") || actorSeat != 0) return;
            _possessionAttackAt = Time.realtimeSinceStartup;
            Debug.Log($"[VISUAL] POSSESSION_ATTACK_START case={_possessionCase} local={_localSeat} at={_possessionAttackAt:F3}");
        }

        IEnumerator CapturePossessionCue()
        {
            yield return new WaitForEndOfFrame();
            Capture("possession-cue");
        }

        void OnPossessionPresentationSettled(uint sequence)
        {
            if (_possessionCase == "attack-disconnect" && _localSeat == 3) return;
            if (!_possessionBatchSeen || _possessionSettling) return;
            _possessionSettling = true;
            float elapsed = Time.realtimeSinceStartup - _possessionBatchAt;
            Debug.Log($"[VISUAL] POSSESSION_TIMING case={_possessionCase} local={_localSeat} seq={sequence} elapsed={elapsed:F3} cueOffset={_possessionCueAt - _possessionBatchAt:F3} attackOffset={_possessionAttackAt - _possessionBatchAt:F3}");
            bool shortDefense = _possessionCase == "defense" || _possessionCase == "minigame-failure";
            if ((shortDefense && elapsed > 2.5f)
                || (!shortDefense && _possessionCase != "attack-force" && elapsed < 3f)
                || (_possessionCase == "attack-force" && _localSeat == 2 && elapsed > 2.5f)
                || (_possessionCase == "attack-force" && _localSeat != 2 && elapsed < 3f)
                || ((_possessionCase == "defense-counter"
                    || _possessionCase == "defense-counter-missing-renderer")
                    && (_possessionAttackAt <= 0f || _possessionCueAt > _possessionAttackAt)))
            {
                Fail("Possession presentation timing violated action budget");
                return;
            }
            StartCoroutine(FinishPossessionCase(sequence));
        }

        IEnumerator FinishPossessionCase(uint sequence)
        {
            float deadline = Time.unscaledTime + 8f;
            byte expectedUses = _possessionUsesBefore == byte.MaxValue
                ? byte.MaxValue : (byte)(_possessionUsesBefore - 1);
            while (Time.unscaledTime < deadline)
            {
                var target = CurrentPlayers().FirstOrDefault(p => p.PlayerIndex == 2);
                var inventory = target?.GetInventory();
                if (inventory != null)
                {
                    bool foundCopy = false;
                    byte actualUses = 0;
                    for (int i = 0; i < inventory.SlotStates.Count; i++)
                    {
                        var slot = inventory.SlotStates[i];
                        if (slot.CopyId != _possessionCopyId) continue;
                        foundCopy = true;
                        actualUses = slot.RemainingUses;
                        break;
                    }
                    // A one-use copy is removed by the authoritative inventory writer.
                    bool costMatches = foundCopy
                        ? actualUses == expectedUses
                        : expectedUses == 0;
                    bool cueExpected = _possessionCase != "minigame-failure";
                    if (costMatches && (!cueExpected || _possessionCueSeen))
                    {
                        if (_possessionCase == "attack-force" && _localSeat == 2)
                        {
                            while (Time.realtimeSinceStartup - _possessionBatchAt < 3.6f)
                                yield return null;
                        }
                        if (_possessionCase == "attack-drop-ack"
                            || _possessionCase == "attack-late-ack"
                            || _possessionCase == "attack-disconnect")
                        {
                            float progressDeadline = Time.realtimeSinceStartup + 24f;
                            while (Time.realtimeSinceStartup < progressDeadline
                                && (TurnManager.Instance == null
                                    || TurnManager.Instance.TurnNumber.Value < 2
                                    || (_possessionCase == "attack-late-ack"
                                        && Time.realtimeSinceStartup - _possessionBatchAt < 17f)))
                                yield return null;
                            if (TurnManager.Instance == null
                                || TurnManager.Instance.TurnNumber.Value < 2)
                            {
                                Fail("Turn did not advance after presentation ACK fault");
                                yield break;
                            }
                            Debug.Log($"[VISUAL] POSSESSION_ACK_RECOVERED case={_possessionCase} local={_localSeat} turn={TurnManager.Instance.TurnNumber.Value}");
                        }
                        Debug.Log($"[VISUAL] POSSESSION_CHECK case={_possessionCase} local={_localSeat} seq={sequence} copy={_possessionCopyId} before={_possessionUsesBefore} after={actualUses} cue={_possessionCueSeen}");
                        Capture("possession-settled");
                        StartCoroutine(Finish("possession-" + _possessionCase));
                        yield break;
                    }
                }
                yield return null;
            }
            Fail("Possession inventory/cue did not converge");
        }
    }
}
#endif
