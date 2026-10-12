using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.UI.Game.Build;
using UnityEngine;

namespace AbsoluteZero.UI.Game.Presenters
{
    public sealed class ParticipantBannerPresenter : MonoBehaviour
    {
        IGameDataBridge _bridge;
        GameHudRefs _refs;
        bool _subscribed;
        public void Initialize(IGameDataBridge bridge, GameHudRefs refs)
        { Unbind(); _bridge = bridge; _refs = refs; Bind(); }
        void OnEnable() => Bind();
        void OnDisable() => Unbind();
        void OnDestroy() => Unbind();
        void Bind()
        {
            if (_bridge == null || _subscribed || !isActiveAndEnabled) return;
            _subscribed = true;
            _bridge.OnSeatRegistered += SeatChanged; _bridge.OnSeatSnapshotChanged += SeatChanged;
            _bridge.OnSeatUnregistered += SeatRemoved; _bridge.OnPresentationChanged += PresentationChanged;
            _bridge.OnMatchSnapshotChanged += MatchChanged;
            Refresh();
        }
        void Unbind()
        {
            if (!_subscribed) return;
            _subscribed = false;
            _bridge.OnSeatRegistered -= SeatChanged; _bridge.OnSeatSnapshotChanged -= SeatChanged;
            _bridge.OnSeatUnregistered -= SeatRemoved; _bridge.OnPresentationChanged -= PresentationChanged;
            _bridge.OnMatchSnapshotChanged -= MatchChanged;
        }
        void SeatChanged(byte seat, SeatSnapshot snapshot) => Refresh();
        void SeatRemoved(byte seat) => Refresh();
        void PresentationChanged(CombatPresentationHudSnapshot snapshot) => Refresh();
        void MatchChanged(MatchSnapshot snapshot) => Refresh();
        void Refresh()
        {
            if (_refs?.NameTexts == null) return;
            var match = _bridge.CurrentMatch;
            var playback = _bridge.CurrentPresentation;
            for (byte seat = 0; seat < _refs.NameTexts.Length; seat++)
            {
                bool exists = _bridge.TryGetSeat(seat, out var participant);
                bool first = exists && playback.Active && playback.FirstSeat == seat;
                bool acting = exists && playback.Active && playback.CurrentSeat == seat;
                _refs.NameTexts[seat].text = exists ? participant.DisplayName ?? ParticipantDisplayName.Fallback(seat) : "빈 자리";
                _refs.SeatLabels[seat].text = $"P{seat + 1}" + (exists && participant.IsLocal ? " · 나" : "")
                    + (exists && participant.LifeState == LifeState.Ghost ? " · 유령" : "");
                if (_refs.ScoreLabels != null && seat < _refs.ScoreLabels.Length && _refs.ScoreLabels[seat] != null)
                    _refs.ScoreLabels[seat].text = exists ? ScoreCaption(match, seat) : "";
                _refs.ActionLabels[seat].text = first && acting ? "선공 · 행동 중" : first ? "선공" : acting ? "행동 중" : "";
                _refs.ActionLabels[seat].color = first ? new Color(.48f, .25f, .08f) : new Color(.23f, .20f, .18f);
                if (_refs.ActionBadges != null && seat < _refs.ActionBadges.Length && _refs.ActionBadges[seat] != null)
                {
                    // The fill, frame and label have one lifetime; no empty border after playback.
                    _refs.ActionBadgeRoots[seat].SetActive(first || acting);
                    _refs.ActionBadges[seat].color = acting ? new Color(1f, .85f, .49f) : new Color(1f, .94f, .76f);
                }
                if (_refs.ActiveFrames != null && seat < _refs.ActiveFrames.Length && _refs.ActiveFrames[seat] != null)
                    _refs.ActiveFrames[seat].enabled = acting && _refs.ActiveFrames[seat].sprite != null;
                _refs.NameBoxes[seat].color = acting ? new Color(.98f, .79f, .58f)
                    : !exists ? new Color(.80f, .82f, .81f)
                    : participant.LifeState == LifeState.Ghost ? new Color(.82f, .78f, .90f)
                    : participant.IsLocal ? new Color(.74f, .86f, .82f) : new Color(.77f, .85f, .93f);
            }
        }

        static string ScoreCaption(MatchSnapshot match, byte seat)
        {
            // Multi scores count credited kills, while Duel/Solo count round wins.
            // Wait for the seat's replicated score instead of substituting another seat.
            if (match.Mode == GameMode.Multi)
                return match.KillScores != null && seat < match.KillScores.Length
                    ? $"{match.KillScores[seat]}킬" : "";
            if (match.Mode != GameMode.OneVsOne && match.Mode != GameMode.Solo) return "";

            return seat switch
            {
                0 => $"{match.P1RoundWins}승",
                1 => $"{match.P2RoundWins}승",
                _ => ""
            };
        }
    }
}
