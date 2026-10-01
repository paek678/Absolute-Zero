#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class VisualFourPlayerProbe
    {
        string _ghostLifecycleCase;
        bool _lifecycleFirstGhostForced;
        bool _lifecycleRoundEndForced;
        bool _lifecycleRevivedGhostForced;
        bool _lifecycleFirstPossessionSent;
        bool _lifecycleRejectedPossessionSent;
        bool _lifecycleFinalCapture;
        uint _lifecycleMatchEpoch;
        uint _lifecycleFirstRoundEpoch;
        int _lifecyclePresentations;

        void UpdateGhostLifecycleCase(PlayerState local, PlayerState[] players,
            TurnManager turn, MatchNetworkState state)
        {
            if (_ghostLifecycleCase != "round-revival")
            {
                Fail("Unknown ghost lifecycle case: " + _ghostLifecycleCase);
                return;
            }
            if (players.Length != 4 || turn.CurrentPhase.Value != TurnPhase.PrepPhase)
                return;
            if (_lifecycleMatchEpoch == 0)
            {
                _lifecycleMatchEpoch = state.GhostMatchEpoch.Value;
                _lifecycleFirstRoundEpoch = state.GhostRoundEpoch.Value;
                if (_lifecycleMatchEpoch == 0 || _lifecycleFirstRoundEpoch == 0) return;
            }
            if (state.GhostMatchEpoch.Value != _lifecycleMatchEpoch)
            {
                Fail("Ghost match epoch changed during same-match round transition");
                return;
            }

            if (state.GhostRoundEpoch.Value == _lifecycleFirstRoundEpoch)
            {
                if (_role == "host" && !_lifecycleFirstGhostForced)
                    _lifecycleFirstGhostForced = turn.DebugForceGhostForVisual(1);
                if (_lifecyclePresentations < 1) return;
                if (_localSeat == 1 && !_lifecycleFirstPossessionSent)
                {
                    _lifecycleFirstPossessionSent = true;
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_POSSESSION,
                        2, 0x8000D101);
                    StartCoroutine(ExpectLedgerReply(0x8000D101, GhostSkillRequestResult.Accepted));
                }
                if ((state.GhostPossessionSpentMask.Value & 2) == 0
                    || (state.GhostPossessedMask.Value & 4) == 0) return;
                if (_role == "host" && !_lifecycleRoundEndForced)
                    _lifecycleRoundEndForced = turn.DebugForceTwoGhostsForTopUp(0, 2);
                return;
            }

            if (state.GhostRoundEpoch.Value != _lifecycleFirstRoundEpoch + 1)
            {
                Fail("Unexpected ghost round epoch after revival");
                return;
            }
            if ((state.GhostPossessionSpentMask.Value & 2) == 0
                || state.GhostPossessedMask.Value != 0)
            {
                Fail("Possession charge or marker reset incorrectly at round boundary");
                return;
            }
            if (_lifecyclePresentations < 2) return;
            if (_role == "host" && !_lifecycleRevivedGhostForced)
            {
                bool revived = players.All(p => p.CurrentLifeState.Value == LifeState.Alive);
                if (revived)
                {
                    Debug.Log($"[VISUAL] LIFECYCLE_REVIVED local={_localSeat} match={state.GhostMatchEpoch.Value} round={state.GhostRoundEpoch.Value} spent={state.GhostPossessionSpentMask.Value}");
                    _lifecycleRevivedGhostForced = turn.DebugForceGhostForVisual(1);
                }
            }
            if (_lifecyclePresentations < 3 || _lifecycleRejectedPossessionSent)
                return;
            if (_localSeat == 1)
            {
                _lifecycleRejectedPossessionSent = true;
                SendLedgerRequest(turn, state, GhostSkillService.SKILL_POSSESSION,
                    2, 0x8000D102);
                StartCoroutine(FinishGhostLifecycleAfterReply(state));
            }
            else if (!_lifecycleFinalCapture)
            {
                _lifecycleFinalCapture = true;
                StartCoroutine(FinishGhostLifecycleObserver(state));
            }
        }

        IEnumerator FinishGhostLifecycleAfterReply(MatchNetworkState state)
        {
            yield return ExpectLedgerReply(0x8000D102, GhostSkillRequestResult.Unavailable);
            if (_finishing) yield break;
            yield return FinishGhostLifecycle(state);
        }

        IEnumerator FinishGhostLifecycleObserver(MatchNetworkState state)
        {
            yield return new WaitForSecondsRealtime(1f);
            yield return FinishGhostLifecycle(state);
        }

        IEnumerator FinishGhostLifecycle(MatchNetworkState state)
        {
            if (state.GhostMatchEpoch.Value != _lifecycleMatchEpoch
                || state.GhostRoundEpoch.Value != _lifecycleFirstRoundEpoch + 1
                || (state.GhostPossessionSpentMask.Value & 2) == 0
                || state.GhostPossessedMask.Value != 0)
            {
                Fail("Ghost lifecycle state diverged after revived ghost request");
                yield break;
            }
            Debug.Log($"[VISUAL] LIFECYCLE_CHECK local={_localSeat} match={state.GhostMatchEpoch.Value} round={state.GhostRoundEpoch.Value} spent={state.GhostPossessionSpentMask.Value} possessed={state.GhostPossessedMask.Value}");
            Capture("lifecycle-round-revival");
            yield return Finish("lifecycle-round-revival");
        }

        void OnGhostLifecyclePresentationSettled(uint sequence)
        {
            if (_ghostLifecycleCase != "round-revival") return;
            _lifecyclePresentations++;
            Debug.Log($"[VISUAL] LIFECYCLE_PRESENTATION local={_localSeat} ordinal={_lifecyclePresentations} seq={sequence}");
        }
    }
}
#endif
