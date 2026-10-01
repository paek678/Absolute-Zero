#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    public sealed partial class VisualFourPlayerProbe
    {
        string _miniTicketCase;
        bool _miniStaged;
        bool _miniSubscribed;
        bool _miniFirstSelected;
        bool _miniReplaced;
        bool _miniSecondSelected;
        bool _miniStaleSubmitted;
        bool _miniFreshSubmitted;
        bool _miniFinished;
        bool _miniTopUpTriggered;
        bool _miniRoundFirstGhost;
        bool _miniRoundEndForced;
        bool _miniRoundSecondStaged;
        uint _miniInitialRoundEpoch;
        float _miniStartAt;
        float _miniFirstPendingAt;
        float _miniSecondObservedAt;
        float _miniStaleAt;
        float _miniFreshAt;
        uint _miniFirstCopy;
        uint _miniSecondCopy;
        MiniGameTicket _miniFirstTicket;
        MiniGameTicket _miniSecondTicket;
        PlayerState _miniTicketOwner;

        void UpdateMiniTicketCase(PlayerState local, PlayerState[] players,
            TurnManager turn, MatchNetworkState state)
        {
            bool topUp = _miniTicketCase == "replacement-topup";
            bool round = _miniTicketCase == "round";
            if (_miniTicketCase != "replacement" && !topUp && !round)
            {
                Fail("Unknown mini-ticket case: " + _miniTicketCase);
                return;
            }
            if (players.Length != 4 || turn.CurrentPhase.Value != TurnPhase.PrepPhase)
                return;
            if (_miniStartAt == 0f) _miniStartAt = Time.unscaledTime;
            if (Time.unscaledTime - _miniStartAt > (round ? 45f : 24f))
            {
                Fail("Mini-ticket replacement timed out");
                return;
            }

            var actor = players.FirstOrDefault(p => p.PlayerIndex == 2);
            var inventory = actor?.GetInventory();
            if (inventory == null || !inventory.IsRegistryReady) return;
            short hugId = (short)Array.FindIndex(ItemManager.Instance.GetAllItems(),
                item => item != null && item.ItemName == "Hug T-shirt");
            if (hugId < 0) { Fail("Hug T-shirt missing"); return; }
            if (_miniInitialRoundEpoch == 0) _miniInitialRoundEpoch = state.GhostRoundEpoch.Value;

            if (_role == "host" && !_miniStaged)
            {
                inventory.SlotStates.Clear();
                if (!inventory.GrantSpecificItem(hugId))
                {
                    Fail("Could not stage first minigame item");
                    return;
                }
                _miniFirstCopy = inventory.SlotStates[0].CopyId;
                _miniStaged = true;
                Debug.Log($"[VISUAL] MINI_STAGED copy={_miniFirstCopy}");
            }

            if (_localSeat == 2 && !_miniSubscribed)
            {
                _miniSubscribed = true;
                _miniTicketOwner = local;
                local.OnMiniGameStart += OnMiniTicketStarted;
            }
            if (_localSeat == 2 && !_miniFirstSelected && inventory.SlotStates.Count == 1
                && inventory.GetItemData(0)?.ItemName == "Hug T-shirt"
                && Time.unscaledTime - _miniStartAt > 1.3f)
            {
                _miniFirstSelected = true;
                _miniFirstCopy = inventory.SlotStates[0].CopyId;
                ProbeInventoryCommands.SelectItem(local, 0, 0);
                Debug.Log($"[VISUAL] MINI_SELECT_A copy={_miniFirstCopy}");
            }

            if (round && state.GhostRoundEpoch.Value == _miniInitialRoundEpoch)
            {
                if (_role == "host" && actor.DebugPendingMiniGameAttemptId != 0
                    && !_miniRoundFirstGhost)
                    _miniRoundFirstGhost = turn.DebugForceGhostForVisual(1);
                if (_role == "host" && _miniRoundFirstGhost && _settledTurns >= 1
                    && !_miniRoundEndForced)
                    _miniRoundEndForced = turn.DebugForceTwoGhostsForTopUp(0, 3);
                return;
            }
            if (round && state.GhostRoundEpoch.Value != _miniInitialRoundEpoch + 1)
            {
                Fail("Unexpected round epoch in minigame ticket scenario");
                return;
            }
            if (round && _role == "host" && !_miniRoundSecondStaged)
            {
                inventory.SlotStates.Clear();
                if (!inventory.GrantSpecificItem(hugId))
                {
                    Fail("Could not stage next-round minigame item");
                    return;
                }
                _miniSecondCopy = inventory.SlotStates[0].CopyId;
                _miniRoundSecondStaged = true;
                Debug.Log($"[VISUAL] MINI_ROUND_STAGED a={_miniFirstCopy} b={_miniSecondCopy} round={state.GhostRoundEpoch.Value}");
            }
            if (!round && _role == "host" && !_miniReplaced && actor.DebugPendingMiniGameAttemptId != 0)
            {
                if (_miniFirstPendingAt == 0f) _miniFirstPendingAt = Time.unscaledTime;
                if (Time.unscaledTime - _miniFirstPendingAt < 1.2f) return;
                if (inventory.SlotStates.Count != 1 || inventory.SlotStates[0].CopyId != _miniFirstCopy)
                {
                    Fail("First minigame binding changed before replacement");
                    return;
                }
                inventory.SlotStates.RemoveAt(0);
                if (!inventory.GrantSpecificItem(hugId))
                {
                    Fail("Could not stage replacement item");
                    return;
                }
                _miniSecondCopy = inventory.SlotStates[0].CopyId;
                if (_miniSecondCopy == _miniFirstCopy)
                {
                    Fail("Replacement reused CopyId");
                    return;
                }
                _miniReplaced = true;
                Debug.Log($"[VISUAL] MINI_REPLACED slot=0 item={hugId} a={_miniFirstCopy} b={_miniSecondCopy}");
            }

            if (_localSeat == 2 && !_miniSecondSelected && _miniFirstTicket.AttemptId != 0
                && inventory.SlotStates.Count == 1
                && inventory.SlotStates[0].CopyId != _miniFirstCopy
                && (!round || state.GhostRoundEpoch.Value == _miniInitialRoundEpoch + 1))
            {
                _miniSecondSelected = true;
                _miniSecondCopy = inventory.SlotStates[0].CopyId;
                ProbeInventoryCommands.SelectItem(local, 0, 0);
                Debug.Log($"[VISUAL] MINI_SELECT_B copy={_miniSecondCopy}");
            }
            if (topUp && _role == "host" && !_miniTopUpTriggered
                && actor.DebugPendingMiniGameAttemptId >= 2)
            {
                _miniTopUpTriggered = turn.DebugForceTwoGhostsForTopUp(1, 3);
                if (_miniTopUpTriggered)
                    Debug.Log($"[VISUAL] MINI_TOPUP_TRIGGERED copy={inventory.SlotStates[0].CopyId}");
            }
            if (_localSeat == 2 && _miniSecondTicket.AttemptId != 0 && !_miniStaleSubmitted
                && Time.unscaledTime - _miniSecondObservedAt > 0.8f
                && (!topUp || (state.DeathmatchGrant.Value.Stage == DeathmatchGrantStage.Completed
                               && state.InventoryReadModel?.CommittedGrantTransaction
                                  == state.DeathmatchGrant.Value.TransactionId)))
            {
                _miniStaleSubmitted = true;
                _miniStaleAt = Time.unscaledTime;
                local.SubmitMiniGameResultServerRpc(_miniFirstTicket.Slot, true,
                    _miniFirstTicket.MatchEpoch, _miniFirstTicket.RoundEpoch,
                    _miniFirstTicket.Turn, _miniFirstTicket.AttemptId, _miniFirstTicket.CopyId);
                local.SubmitMiniGameResultServerRpc(_miniFirstTicket.Slot, false,
                    _miniFirstTicket.MatchEpoch, _miniFirstTicket.RoundEpoch,
                    _miniFirstTicket.Turn, _miniFirstTicket.AttemptId, _miniFirstTicket.CopyId);
                Debug.Log($"[VISUAL] MINI_STALE_SENT a={_miniFirstTicket.AttemptId} b={_miniSecondTicket.AttemptId}");
                Capture("mini-b-before-stale-check");
            }
            if (_localSeat == 2 && _miniStaleSubmitted && !_miniFreshSubmitted
                && Time.unscaledTime - _miniStaleAt > 0.7f)
            {
                if (GameObject.Find("MiniGame_HugCharacter") == null || local.HasSelectedItem.Value
                    || inventory.SlotStates.Count < 1
                    || inventory.SlotStates[0].CopyId != _miniSecondCopy)
                {
                    Fail("Stale result altered the newer ticket, UI, or item");
                    return;
                }
                _miniFreshSubmitted = true;
                _miniFreshAt = Time.unscaledTime;
                Debug.Log($"[VISUAL] MINI_STALE_INERT copy={_miniSecondCopy} uses={inventory.SlotStates[0].RemainingUses}");
                Capture("mini-b-after-stale-check");
                local.SubmitMiniGameResultServerRpc(_miniSecondTicket.Slot, true,
                    _miniSecondTicket.MatchEpoch, _miniSecondTicket.RoundEpoch,
                    _miniSecondTicket.Turn, _miniSecondTicket.AttemptId, _miniSecondTicket.CopyId);
                local.SubmitMiniGameResultServerRpc(_miniSecondTicket.Slot, true,
                    _miniSecondTicket.MatchEpoch, _miniSecondTicket.RoundEpoch,
                    _miniSecondTicket.Turn, _miniSecondTicket.AttemptId, _miniSecondTicket.CopyId);
                Debug.Log($"[VISUAL] MINI_B_DUPLICATE_SENT attempt={_miniSecondTicket.AttemptId}");
            }
            if (_miniFreshSubmitted && !_miniFinished && local.HasSelectedItem.Value
                && Time.unscaledTime - _miniFreshAt > 0.5f)
            {
                _miniFinished = true;
                Debug.Log($"[VISUAL] MINI_B_QUEUED copy={_miniSecondCopy}");
                Capture("mini-b-queued");
                StartCoroutine(Finish("mini-ticket-replacement"));
            }
            else if (_localSeat != 2 && !_miniFinished && actor.HasSelectedItem.Value)
            {
                _miniFinished = true;
                Debug.Log($"[VISUAL] MINI_OBSERVER_QUEUED local={_localSeat} copy={inventory.SlotStates[0].CopyId}");
                Capture("mini-observer-queued");
                StartCoroutine(Finish("mini-ticket-replacement"));
            }
        }

        void OnMiniTicketStarted(MiniGameTicket ticket)
        {
            if (_miniFirstTicket.AttemptId == 0)
            {
                _miniFirstTicket = ticket;
                Debug.Log($"[VISUAL] MINI_TICKET_A attempt={ticket.AttemptId} copy={ticket.CopyId}");
                StartCoroutine(CaptureMiniTicketView("mini-a-active", 0.7f));
            }
            else
            {
                _miniSecondTicket = ticket;
                _miniSecondObservedAt = Time.unscaledTime;
                Debug.Log($"[VISUAL] MINI_TICKET_B attempt={ticket.AttemptId} copy={ticket.CopyId}");
                StartCoroutine(CaptureMiniTicketView("mini-b-active", 0.55f));
            }
        }

        IEnumerator CaptureMiniTicketView(string label, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Capture(label);
        }

        void UnsubscribeMiniTicket()
        {
            if (_miniTicketOwner != null)
                _miniTicketOwner.OnMiniGameStart -= OnMiniTicketStarted;
            _miniTicketOwner = null;
        }
    }
}
#endif
