#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
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
        string _ghostLedgerCase;
        bool _ledgerGhostsForced;
        bool _ledgerDeathSettled;
        int _ledgerStartedTurn;
        readonly Dictionary<uint, GhostSkillRequestResult> _ledgerReplies = new();

        void UpdateGhostLedgerCase(PlayerState local, PlayerState[] players,
            TurnManager turn, MatchNetworkState state)
        {
            if (_ghostLedgerCase != "contention" && _ghostLedgerCase != "contention-reverse")
            {
                Fail("Unknown ghost ledger case: " + _ghostLedgerCase);
                return;
            }
            if (turn.CurrentPhase.Value != TurnPhase.PrepPhase || players.Length != 4)
                return;
            if (_role == "host" && !_ledgerGhostsForced)
                _ledgerGhostsForced = turn.DebugForceTwoGhostsForTopUp(1, 3);
            if (!_ledgerDeathSettled || turn.TurnNumber.Value <= _ledgerStartedTurn)
                return;
            int currentTurn = turn.TurnNumber.Value;
            if (currentTurn > (_ghostLedgerCase == "contention-reverse" ? 1 : 3)) return;
            _ledgerStartedTurn = currentTurn;
            StartCoroutine(_ghostLedgerCase == "contention-reverse"
                ? RunReverseLedgerTurn(local, turn, state)
                : RunLedgerTurn(local, turn, state, currentTurn));
        }

        IEnumerator RunReverseLedgerTurn(PlayerState local, TurnManager turn,
            MatchNetworkState state)
        {
            byte seat = (byte)_localSeat;
            Debug.Log($"[VISUAL] LEDGER_REVERSE_START local={seat}");
            if (seat == 3)
            {
                SendLedgerRequest(turn, state, GhostSkillService.SKILL_GRUDGE, 0, 0x8000E301);
                yield return ExpectLedgerReply(0x8000E301, GhostSkillRequestResult.Accepted);
                yield return WaitForLedgerCondition(() =>
                    (state.GhostPossessedMask.Value & 4) != 0,
                    "reverse first possession");
                if (_finishing) yield break;
                SendLedgerRequest(turn, state, GhostSkillService.SKILL_POSSESSION, 2, 0x8000E302);
                yield return ExpectLedgerReply(0x8000E302, GhostSkillRequestResult.TargetAlreadyAffected);
                if (_finishing) yield break;
                SendLedgerRequest(turn, state, GhostSkillService.SKILL_POSSESSION, 0, 0x8000E303);
                yield return ExpectLedgerReply(0x8000E303, GhostSkillRequestResult.Accepted);
            }
            else if (seat == 1)
            {
                yield return WaitForLedgerCondition(() =>
                    (state.GhostDamageMask.Value & 1) != 0,
                    "reverse first grudge");
                if (_finishing) yield break;
                SendLedgerRequest(turn, state, GhostSkillService.SKILL_GRUDGE, 0, 0x8000E101);
                yield return ExpectLedgerReply(0x8000E101, GhostSkillRequestResult.TargetAlreadyAffected);
                if (_finishing) yield break;
                SendLedgerRequest(turn, state, GhostSkillService.SKILL_POSSESSION, 2, 0x8000E102);
                yield return ExpectLedgerReply(0x8000E102, GhostSkillRequestResult.Accepted);
            }
            if (_finishing) yield break;
            yield return WaitForLedgerCondition(() =>
                state.GhostDamageMask.Value == 1
                && state.GhostPossessionSpentMask.Value == 0x0A
                && state.GhostPossessedMask.Value == 0x05,
                "reverse commits");
            if (_finishing) yield break;
            Debug.Log($"[VISUAL] LEDGER_REVERSE_END local={seat} damage=1 spent=10 possessed=5");
            Capture("ledger-reverse-end");
            if (seat != 0 && seat != 2) yield break;
            yield return new WaitForSecondsRealtime(1f);
            ProbeInventoryCommands.Ready(local);
        }

        IEnumerator RunLedgerTurn(PlayerState local, TurnManager turn,
            MatchNetworkState state, int turnNumber)
        {
            byte seat = (byte)_localSeat;
            byte expectedSpent = turnNumber == 1 ? (byte)0 : (byte)0x0A;
            byte expectedCd1 = turnNumber == 1 ? (byte)0
                : turnNumber == 2 ? (byte)1 : (byte)0;
            byte expectedCd3 = turnNumber == 3 ? (byte)1 : (byte)0;
            yield return WaitForLedgerCondition(() =>
                state.GhostPossessionSpentMask.Value == expectedSpent
                && state.GhostPossessedMask.Value == 0
                && state.GhostDamageMask.Value == 0
                && GetLedgerCooldown(state, 1) == expectedCd1
                && GetLedgerCooldown(state, 3) == expectedCd3,
                "turn-start state " + turnNumber);
            if (_finishing) yield break;
            Debug.Log($"[VISUAL] LEDGER_START turn={turnNumber} local={seat} spent={state.GhostPossessionSpentMask.Value} cd1={expectedCd1} cd3={expectedCd3}");
            Capture($"ledger-turn-{turnNumber}-start");

            if (turnNumber == 1)
            {
                if (seat == 1)
                {
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_GRUDGE, 0, 0x8000C101);
                    yield return ExpectLedgerReply(0x8000C101, GhostSkillRequestResult.Accepted);
                    yield return WaitForLedgerCondition(() =>
                        (state.GhostPossessedMask.Value & (1 << 2)) != 0,
                        "first possession");
                    if (_finishing) yield break;
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_POSSESSION, 2, 0x8000C102);
                    yield return ExpectLedgerReply(0x8000C102, GhostSkillRequestResult.TargetAlreadyAffected);
                    if (_finishing) yield break;
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_POSSESSION, 0, 0x8000C103);
                    yield return ExpectLedgerReply(0x8000C103, GhostSkillRequestResult.Accepted);
                }
                else if (seat == 3)
                {
                    yield return WaitForLedgerCondition(() =>
                        (state.GhostDamageMask.Value & 1) != 0,
                        "first grudge");
                    if (_finishing) yield break;
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_GRUDGE, 0, 0x8000C301);
                    yield return ExpectLedgerReply(0x8000C301, GhostSkillRequestResult.TargetAlreadyAffected);
                    if (_finishing) yield break;
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_POSSESSION, 2, 0x8000C302);
                    yield return ExpectLedgerReply(0x8000C302, GhostSkillRequestResult.Accepted);
                }
                else
                {
                    yield return WaitForLedgerCondition(() =>
                        state.GhostDamageMask.Value == 1
                        && state.GhostPossessionSpentMask.Value == 0x0A
                        && state.GhostPossessedMask.Value == 0x05,
                        "turn-one commits");
                }
            }
            else if (turnNumber == 2)
            {
                if (seat == 1)
                {
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_GRUDGE, 0, 0x8000C104);
                    yield return ExpectLedgerReply(0x8000C104, GhostSkillRequestResult.Unavailable);
                    if (_finishing) yield break;
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_POSSESSION, 2, 0x8000C105);
                    yield return ExpectLedgerReply(0x8000C105, GhostSkillRequestResult.Unavailable);
                }
                else if (seat == 3)
                {
                    yield return new WaitForSecondsRealtime(1f);
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_GRUDGE, 0, 0x8000C303);
                    yield return ExpectLedgerReply(0x8000C303, GhostSkillRequestResult.Accepted);
                    if (_finishing) yield break;
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_POSSESSION, 2, 0x8000C304);
                    yield return ExpectLedgerReply(0x8000C304, GhostSkillRequestResult.Unavailable);
                }
                else
                    yield return WaitForLedgerCondition(() => state.GhostDamageMask.Value == 1,
                        "turn-two grudge");
            }
            else
            {
                if (seat == 1)
                {
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_GRUDGE, 2, 0x8000C106);
                    yield return ExpectLedgerReply(0x8000C106, GhostSkillRequestResult.Accepted);
                    if (_finishing) yield break;
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_POSSESSION, 0, 0x8000C107);
                    yield return ExpectLedgerReply(0x8000C107, GhostSkillRequestResult.Unavailable);
                }
                else if (seat == 3)
                {
                    SendLedgerRequest(turn, state, GhostSkillService.SKILL_GRUDGE, 0, 0x8000C305);
                    yield return ExpectLedgerReply(0x8000C305, GhostSkillRequestResult.Unavailable);
                }
                else
                    yield return WaitForLedgerCondition(() => state.GhostDamageMask.Value == 4,
                        "turn-three grudge");
            }
            if (_finishing) yield break;

            byte expectedDamage = turnNumber == 3 ? (byte)4 : (byte)1;
            byte expectedPossessed = turnNumber == 1 ? (byte)5 : (byte)0;
            yield return WaitForLedgerCondition(() =>
                state.GhostDamageMask.Value == expectedDamage
                && state.GhostPossessionSpentMask.Value == 0x0A
                && state.GhostPossessedMask.Value == expectedPossessed,
                "turn-end ledger " + turnNumber);
            if (_finishing) yield break;
            Debug.Log($"[VISUAL] LEDGER_END turn={turnNumber} local={seat} damage={expectedDamage} spent=10 possessed={expectedPossessed}");
            Capture($"ledger-turn-{turnNumber}-end");
            if (seat != 0 && seat != 2) yield break;
            yield return new WaitForSecondsRealtime(1f);
            if (_finishing || turn.TurnNumber.Value != turnNumber) yield break;
            ProbeInventoryCommands.Ready(local);
            Debug.Log($"[VISUAL] LEDGER_READY turn={turnNumber} local={seat}");
        }

        static byte GetLedgerCooldown(MatchNetworkState state, byte seat)
        {
            for (int i = 0; i < state.GhostCooldowns.Count; i++)
            {
                var value = state.GhostCooldowns[i];
                if (value.Seat == seat && value.Skill == GhostSkillService.SKILL_GRUDGE)
                    return value.RemainingTurns;
            }
            return 0;
        }

        void SendLedgerRequest(TurnManager turn, MatchNetworkState state,
            byte skill, byte target, uint requestId)
        {
            turn.UseGhostSkillRpc(skill, target, state.GhostMatchEpoch.Value,
                state.GhostRoundEpoch.Value, turn.TurnNumber.Value, requestId);
            Debug.Log($"[VISUAL] LEDGER_REQUEST turn={turn.TurnNumber.Value} local={_localSeat} id={requestId} skill={skill} target={target}");
        }

        void OnLedgerRequestResult(uint requestId, GhostSkillRequestResult result)
        {
            if (_ghostLedgerCase != "contention" && _ghostLedgerCase != "contention-reverse"
                && _ghostLifecycleCase != "round-revival") return;
            _ledgerReplies[requestId] = result;
            Debug.Log($"[VISUAL] LEDGER_REPLY local={_localSeat} id={requestId} result={result}");
        }

        IEnumerator ExpectLedgerReply(uint requestId, GhostSkillRequestResult expected)
        {
            yield return WaitForLedgerCondition(() => _ledgerReplies.ContainsKey(requestId),
                "reply " + requestId);
            if (_finishing) yield break;
            if (_ledgerReplies[requestId] != expected)
                Fail($"Ghost request {requestId} returned {_ledgerReplies[requestId]}, expected {expected}");
        }

        IEnumerator WaitForLedgerCondition(Func<bool> condition, string label)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!_finishing && !condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!_finishing && !condition()) Fail("Ghost ledger timeout: " + label);
        }

        void OnLedgerPresentationSettled(uint sequence)
        {
            if (_ghostLedgerCase != "contention" && _ghostLedgerCase != "contention-reverse") return;
            if (!_ledgerDeathSettled)
            {
                _ledgerDeathSettled = true;
                Debug.Log($"[VISUAL] LEDGER_DEATH_SETTLED local={_localSeat} seq={sequence}");
                return;
            }
            int turn = TurnManager.Instance?.TurnNumber.Value ?? 0;
            var state = MatchCompositionRoot.Instance?.NetworkState;
            if (_ghostLedgerCase == "contention-reverse")
            {
                if (turn != 1 || state == null || state.GhostDamageMask.Value != 1
                    || state.GhostPossessionSpentMask.Value != 0x0A) return;
                Debug.Log($"[VISUAL] LEDGER_REVERSE_CHECK local={_localSeat} seq={sequence} damage={state.GhostDamageMask.Value} spent={state.GhostPossessionSpentMask.Value}");
                Capture("ledger-reverse-settled");
                StartCoroutine(Finish("ledger-contention-reverse"));
                return;
            }
            if (turn != 3 || state == null || state.GhostDamageMask.Value != 4) return;
            Debug.Log($"[VISUAL] LEDGER_CHECK local={_localSeat} seq={sequence} turn=3 spent={state.GhostPossessionSpentMask.Value} damage={state.GhostDamageMask.Value}");
            Capture("ledger-final-settled");
            StartCoroutine(Finish("ledger-contention"));
        }
    }
}
#endif
