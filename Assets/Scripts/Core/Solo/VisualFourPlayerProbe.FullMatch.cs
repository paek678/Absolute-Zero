#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class VisualFourPlayerProbe
    {
        bool _fullGhostForced;
        bool _fullPossessionRequested;
        bool _fullItemSelected;
        bool _fullReady;
        bool _fullSuppressedSeen;
        bool _fullCueSeen;
        bool _fullLethalArmed;
        bool _fullGrudgeRequested;
        float _fullTailStartedAt;

        void UpdateFullMatchTail(PlayerState local, PlayerState[] players,
            TurnManager turn, MatchNetworkState state)
        {
            if (_fullTailStartedAt == 0f) _fullTailStartedAt = Time.unscaledTime;
            if (Time.unscaledTime - _fullTailStartedAt > (_fullNatural ? 300f : 65f))
            {
                Fail("Full-match tail timed out");
                return;
            }
            if (players.Length != 4 || turn.CurrentPhase.Value != TurnPhase.PrepPhase)
                return;

            var ghost = players.FirstOrDefault(p => p.PlayerIndex == 1);
            var target = players.FirstOrDefault(p => p.PlayerIndex == 0);
            if (ghost == null || target == null) return;
            if (_settledTurns == 4)
            {
                if (_role == "host" && !_fullGhostForced)
                {
                    _fullGhostForced = turn.DebugForceGhostForVisual(1);
                    if (_fullGhostForced)
                        Debug.Log("[VISUAL] FULL_GHOST_FORCED actor=1 after=4-turns");
                }
                return;
            }

            if (_settledTurns >= 5 && !_fullSuppressedSeen)
            {
                if (ghost.CurrentLifeState.Value != LifeState.Ghost) return;
                if (_localSeat == 1 && !_fullPossessionRequested)
                {
                    _fullPossessionRequested = true;
                    turn.UseGhostSkillRpc(GhostSkillService.SKILL_POSSESSION, 2,
                        state.GhostMatchEpoch.Value, state.GhostRoundEpoch.Value,
                        turn.TurnNumber.Value, 0x8000F101);
                    Debug.Log("[VISUAL] FULL_POSSESSION_REQUEST actor=1 target=2");
                }
                if ((state.GhostPossessedMask.Value & (1 << 2)) == 0) return;
                if (_localSeat == 2 && !_fullItemSelected)
                {
                    var inventory = local.GetInventory();
                    byte fanSlot = byte.MaxValue;
                    for (byte i = 0; i < inventory.SlotStates.Count; i++)
                        if (inventory.GetItemData(i)?.ItemName == "Fan")
                        { fanSlot = i; break; }
                    if (fanSlot == byte.MaxValue) { Fail("Full-match Fan missing"); return; }
                    _fullItemSelected = true;
                    ProbeInventoryCommands.SelectItem(local, fanSlot, 0);
                    Debug.Log($"[VISUAL] FULL_POSSESSED_SELECT actor=2 slot={fanSlot} target=0");
                }
                if (_localSeat != 1 && !_fullReady
                    && (_localSeat != 2 || local.HasSelectedItem.Value))
                {
                    _fullReady = true;
                    ProbeInventoryCommands.Ready(local);
                    Debug.Log($"[VISUAL] FULL_READY local={_localSeat} selected={local.HasSelectedItem.Value}");
                }
                return;
            }

            if (_settledTurns < 6 || !_fullSuppressedSeen || !_fullCueSeen)
                return;
            if (_fullNatural)
            {
                UpdateNaturalKillTail(local, players, turn, state);
                return;
            }
            if (_role == "host" && !_fullLethalArmed)
            {
                for (int i = 0; i < 4; i++) state.ServerAddKill(1);
                target.Temperature.Value = 2f;
                _fullLethalArmed = true;
                Debug.Log($"[VISUAL] FULL_LETHAL_ARMED actor=1 target=0 kills={state.KillScores[1]} temp={target.Temperature.Value}");
                Capture("full-lethal-ready");
            }
            if (_localSeat == 1 && !_fullGrudgeRequested
                && state.KillScores.Count > 1 && state.KillScores[1] == 4
                && target.Temperature.Value <= 2f)
            {
                _fullGrudgeRequested = true;
                turn.UseGhostSkillRpc(GhostSkillService.SKILL_GRUDGE, 0,
                    state.GhostMatchEpoch.Value, state.GhostRoundEpoch.Value,
                    turn.TurnNumber.Value, 0x8000F102);
                Debug.Log("[VISUAL] FULL_GRUDGE_REQUEST actor=1 target=0");
            }
        }

        void OnFullMatchCombatResult(CombatResolutionBatchNetData batch)
        {
            int suppressed = 0;
            for (int i = 0; i < batch.EventCount; i++)
                if ((CombatEventType)batch.Events[i].EventType == CombatEventType.SuppressedItemUse
                    && batch.Events[i].ActorSeat == 2)
                    suppressed++;
            if (suppressed != 1) return;
            _fullSuppressedSeen = true;
            Debug.Log($"[VISUAL] FULL_SUPPRESSED local={_localSeat} seq={batch.ResultSequence} count={suppressed}");
            Capture("full-suppression-batch");
        }

        void OnFullMatchSuppressionCue(byte actorSeat)
        {
            if (actorSeat != 2) return;
            _fullCueSeen = true;
            Debug.Log($"[VISUAL] FULL_SUPPRESSION_CUE local={_localSeat} actor={actorSeat}");
            StartCoroutine(CapturePossessionCue());
        }
    }
}
#endif
