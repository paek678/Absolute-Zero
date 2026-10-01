#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        int _naturalObservedScore = -1;
        int _naturalRequestedScore = -1;
        uint _naturalForcedRound;
        uint _naturalReadyRound;
        int _naturalReadyTurn = -1;
        float _naturalScoreChangedAt;
        float _naturalRequestAt;
        uint _naturalRequestId = 0x8000F200;

        void UpdateNaturalKillTail(PlayerState local, PlayerState[] players,
            TurnManager turn, MatchNetworkState state)
        {
            if (state.KillScores.Count < 4) return;
            int score = state.KillScores[1];
            if (score > 5) { Fail("Natural flow exceeded five ghost kills"); return; }
            if (score != _naturalObservedScore)
            {
                if (_naturalObservedScore >= 0 && score != _naturalObservedScore + 1)
                { Fail("Natural ghost score skipped a kill"); return; }
                _naturalObservedScore = score;
                _naturalScoreChangedAt = Time.unscaledTime;
                Debug.Log($"[VISUAL] NATURAL_KILL_SCORE local={_localSeat} score={score} "
                    + $"round={state.GhostRoundEpoch.Value} turn={turn.TurnNumber.Value}");
                if (score > 0) Capture($"natural-kill-{score:00}");
            }
            if (score == 5 || turn.CurrentPhase.Value != TurnPhase.PrepPhase
                || state.IsDeathmatchGrantViewBlocked) return;

            var ghost = players.FirstOrDefault(p => p.PlayerIndex == 1);
            byte targetSeat = (byte)(score % 2 == 0 ? 0 : 2);
            var target = players.FirstOrDefault(p => p.PlayerIndex == targetSeat);
            if (ghost == null || target == null) return;
            if (ghost.CurrentLifeState.Value == LifeState.Alive)
            {
                if (_role == "host" && _naturalForcedRound != state.GhostRoundEpoch.Value
                    && turn.DebugForceGhostForVisual(1))
                {
                    _naturalForcedRound = state.GhostRoundEpoch.Value;
                    Debug.Log($"[VISUAL] NATURAL_GHOST_FORCED round={_naturalForcedRound} score={score}");
                }
                return;
            }
            if (ghost.CurrentLifeState.Value != LifeState.Ghost
                || target.CurrentLifeState.Value != LifeState.Alive) return;

            byte cooldown = state.ServerGetCooldown(1, GhostSkillService.SKILL_GRUDGE);
            if (_naturalRequestedScore == score)
            {
                if (Time.unscaledTime - _naturalRequestAt > 8f)
                {
                    Debug.LogWarning($"[VISUAL] NATURAL_RETRY score={score} turn={turn.TurnNumber.Value}");
                    _naturalRequestedScore = -1;
                }
                return;
            }
            if (cooldown > 0)
            {
                if (local.CurrentLifeState.Value == LifeState.Alive
                    && (_naturalReadyRound != state.GhostRoundEpoch.Value
                        || _naturalReadyTurn != turn.TurnNumber.Value))
                {
                    _naturalReadyRound = state.GhostRoundEpoch.Value;
                    _naturalReadyTurn = turn.TurnNumber.Value;
                    ProbeInventoryCommands.Ready(local);
                    Debug.Log($"[VISUAL] NATURAL_COOLDOWN_READY local={_localSeat} "
                        + $"score={score} turn={turn.TurnNumber.Value}");
                }
                return;
            }
            if (Time.unscaledTime - _naturalScoreChangedAt < 1.2f) return;
            if (_role == "host" && target.Temperature.Value > 2f)
            {
                target.Temperature.Value = 2f;
                Debug.Log($"[VISUAL] NATURAL_TARGET_ARMED score={score} target={targetSeat} "
                    + $"round={state.GhostRoundEpoch.Value} turn={turn.TurnNumber.Value}");
            }
            if (_localSeat != 1 || target.Temperature.Value > 2f) return;
            _naturalRequestedScore = score;
            _naturalRequestAt = Time.unscaledTime;
            uint requestId = ++_naturalRequestId;
            turn.UseGhostSkillRpc(GhostSkillService.SKILL_GRUDGE, targetSeat,
                state.GhostMatchEpoch.Value, state.GhostRoundEpoch.Value,
                turn.TurnNumber.Value, requestId);
            Debug.Log($"[VISUAL] NATURAL_GRUDGE_REQUEST score={score} target={targetSeat} "
                + $"round={state.GhostRoundEpoch.Value} turn={turn.TurnNumber.Value} request={requestId}");
        }
    }
}
#endif
