using System.Collections.Generic;
using System.Reflection;
using AbsoluteZero.Core.Network;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.UI.Game.Build;
using AbsoluteZero.UI.Game.Presenters;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.Tests
{
    public sealed class Plan047BannerTests
    {
        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void BannersKeepSparseSeatIdentityAndDoNotCaptureInput(int count)
        {
            var owner = new GameObject("Banner test");
            var bridge = owner.AddComponent<GameDataBridge>();
            var refs = new GameHudRefs();
            var canvas = new GameObject("Canvas", typeof(Canvas));
            typeof(GameHudBuilder).GetMethod("BuildProgressHud", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { refs, canvas.transform, count });
            var seats = (Dictionary<byte, SeatSnapshot>)typeof(GameDataBridge)
                .GetField("_seats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bridge);
            seats[(byte)(count - 1)] = new SeatSnapshot { SeatIndex = (byte)(count - 1), DisplayName = "가나다라마바사아", IsLocal = true };
            try
            {
                owner.AddComponent<ParticipantBannerPresenter>().Initialize(bridge, refs);
                Assert.That(refs.NameTexts[count - 1].text, Is.EqualTo("가나다라마바사아"));
                Assert.That(refs.SeatLabels[count - 1].text, Does.StartWith("P" + count));
                Assert.That(refs.NameTexts[0].text, Is.EqualTo("빈 자리"));
                foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
                foreach (var text in refs.NameTexts)
                {
                    Assert.That(text.richText, Is.False);
                    Assert.That(text.font.faceInfo.familyName, Is.EqualTo("Gmarket Sans TTF"));
                    text.ForceMeshUpdate();
                    Assert.That(text.isTextOverflowing, Is.False, "Eight-character Korean names must fit");
                }
                for (int seat = 0; seat < count; seat++)
                {
                    var card = refs.NameBoxes[seat].transform.parent.GetComponent<RectTransform>();
                    var scoreText = refs.ScoreLabels[seat];
                    scoreText.text = "12킬";
                    refs.SeatLabels[seat].text = $"P{seat + 1} · 나 · 유령";
                    refs.SeatLabels[seat].ForceMeshUpdate();
                    scoreText.ForceMeshUpdate();
                    Assert.That(scoreText.font.faceInfo.familyName, Is.EqualTo("Gmarket Sans TTF"));
                    Assert.That(scoreText.isTextOverflowing, Is.False);
                    Assert.That(scoreText.isTextTruncated, Is.False);
                    Assert.That(refs.SeatLabels[seat].isTextTruncated, Is.False, "The local ghost label must fit beside the score.");
                    var scoreInk = VisibleTextBounds(scoreText, card);
                    var seatInk = VisibleTextBounds(refs.SeatLabels[seat], card);
                    var nameInk = VisibleTextBounds(refs.NameTexts[seat], card);
                    Assert.That(scoreInk.min.x, Is.GreaterThan(seatInk.max.x + 2), "Seat and score ink must remain separate.");
                    Assert.That(scoreInk.max.x, Is.LessThan(card.rect.xMax - 6));
                    Assert.That(scoreInk.max.y, Is.LessThan(card.rect.yMax - 4));
                    Assert.That(scoreInk.min.y, Is.GreaterThan(nameInk.max.y + 2), "Score must clear the nickname.");
                    var badge = refs.ActionBadgeRoots[seat];
                    var text = refs.ActionLabels[seat];
                    Assert.That(badge.activeSelf, Is.False, "Idle seats must not leave an empty badge border.");
                    badge.SetActive(true);
                    var badgeRect = badge.GetComponent<RectTransform>();
                    var border = badge.transform.Find("BadgeBorder").GetComponent<Image>();
                    Assert.That(border.sprite, Is.Not.Null);
                    Assert.That(border.type, Is.EqualTo(Image.Type.Sliced));
                    foreach (string caption in new[] { "선공", "행동 중", "선공 · 행동 중" })
                    {
                        text.text = caption;
                        Assert.That(text.font.faceInfo.familyName, Is.EqualTo("Gmarket Sans TTF"));
                        text.ForceMeshUpdate();
                        Assert.That(text.isTextOverflowing, Is.False, caption);
                        Assert.That(text.isTextTruncated, Is.False, caption);
                        Assert.That(text.textInfo.lineCount, Is.EqualTo(1), caption);
                        foreach (var character in text.textInfo.characterInfo)
                        {
                            if (!character.isVisible) continue;
                            var lower = badgeRect.InverseTransformPoint(text.transform.TransformPoint(character.bottomLeft));
                            var upper = badgeRect.InverseTransformPoint(text.transform.TransformPoint(character.topRight));
                            Assert.That(lower.x, Is.GreaterThanOrEqualTo(badgeRect.rect.xMin + 6), caption);
                            Assert.That(upper.x, Is.LessThanOrEqualTo(badgeRect.rect.xMax - 6), caption);
                            Assert.That(lower.y, Is.GreaterThanOrEqualTo(badgeRect.rect.yMin + 2), caption);
                            Assert.That(upper.y, Is.LessThanOrEqualTo(badgeRect.rect.yMax - 2), caption);
                        }
                    }
                    badge.SetActive(false);
                }
                var round = (RectTransform)refs.RoundText.transform.parent;
                var firstCardRect = refs.NameBoxes[0].transform.parent.GetComponent<RectTransform>();
                Assert.That(round.anchoredPosition.y - round.rect.height / 2,
                    Is.GreaterThan(firstCardRect.rect.yMax + 6), "Round ribbon must be above the cards.");
                Assert.That(refs.RoundText.text, Is.EqualTo("라운드 0"));
                var canvasRect = canvas.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920, 1080);
                typeof(GameHudBuilder).GetMethod("BuildMyHpBar", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { refs, canvas.transform });
                var hpOutline = refs.MyHpSlider.transform.Find("Outline");
                var hpBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, hpOutline);
                var firstCard = refs.NameBoxes[0].transform.parent;
                var cardBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, firstCard);
                Assert.That(cardBounds.min.x, Is.GreaterThan(hpBounds.max.x + 4f), "Name border must clear the HP outline");
                foreach (var frame in canvas.GetComponentsInChildren<Image>())
                {
                    if (frame.name != "WhiteFrame") continue;
                    Assert.That(frame.sprite, Is.Not.Null, "Generated banner must be imported into Resources");
                    Assert.That(frame.sprite.border.x, Is.GreaterThan(0));
                    Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced));
                    Assert.That(frame.color, Is.EqualTo(Color.white), "State tint must preserve white borders");
                }
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(canvas); }
        }

        [TestCase(GameMode.OneVsOne, 2)]
        [TestCase(GameMode.Solo, 2)]
        [TestCase(GameMode.Multi, 4)]
        public void BannersObserveSeatScoresAndRecoverAfterEnable(GameMode mode, int count)
        {
            var owner = new GameObject("Score binding test");
            var bridge = owner.AddComponent<GameDataBridge>();
            var refs = new GameHudRefs();
            var canvas = new GameObject("Canvas", typeof(Canvas));
            try
            {
                typeof(GameHudBuilder).GetMethod("BuildProgressHud", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { refs, canvas.transform, count });
                var seats = (Dictionary<byte, SeatSnapshot>)typeof(GameDataBridge)
                    .GetField("_seats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bridge);
                seats[0] = new SeatSnapshot { SeatIndex = 0, DisplayName = "첫번째" };
                seats[(byte)(count - 1)] = new SeatSnapshot { SeatIndex = (byte)(count - 1), DisplayName = "마지막", IsLocal = true };
                var match = new MatchSnapshot
                {
                    Mode = mode, RoundNumber = 7, P1RoundWins = 1, P2RoundWins = 2,
                    KillScores = new[] { 3, 0, 1, 4 }
                };
                SetMatchAndNotify(bridge, match);
                var presenter = owner.AddComponent<ParticipantBannerPresenter>();
                presenter.Initialize(bridge, refs);
                Assert.That(refs.ScoreLabels[0].text, Is.EqualTo(mode == GameMode.Multi ? "3킬" : "1승"));
                Assert.That(refs.ScoreLabels[count - 1].text, Is.EqualTo(mode == GameMode.Multi ? "4킬" : "2승"));
                if (mode == GameMode.Multi)
                {
                    Assert.That(refs.ScoreLabels[1].text, Is.Empty, "A vacant seat must not borrow the next participant's score.");
                    match.KillScores = new[] { 3 };
                    SetMatchAndNotify(bridge, match);
                    Assert.That(refs.ScoreLabels[3].text, Is.Empty, "A score that has not replicated yet must not become a win count.");
                }
                presenter.enabled = false;
                match.P1RoundWins = 2;
                match.KillScores = new[] { 5, 0, 1, 4 };
                SetMatchAndNotify(bridge, match);
                presenter.enabled = true;
                Assert.That(refs.ScoreLabels[0].text, Is.EqualTo(mode == GameMode.Multi ? "5킬" : "2승"));
                seats.Remove((byte)(count - 1));
                SetMatchAndNotify(bridge, match);
                Assert.That(refs.ScoreLabels[count - 1].text, Is.Empty, "Departure must clear the departed seat's score.");

                var matchPresenter = owner.AddComponent<MatchHudPresenter>();
                typeof(MatchHudPresenter).GetField("_roundText", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(matchPresenter, refs.RoundText);
                typeof(MatchHudPresenter).GetMethod("UpdateRoundDisplay", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(matchPresenter, new object[] { match });
                Assert.That(refs.RoundText.text, Is.EqualTo("라운드 7"), "Round numbering must be read unchanged from the match snapshot.");
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(canvas); }
        }

        static void SetMatchAndNotify(GameDataBridge bridge, MatchSnapshot match)
        {
            typeof(GameDataBridge).GetField("_currentMatch", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(bridge, match);
            typeof(GameDataBridge).GetField("_matchDirty", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(bridge, true);
            typeof(GameDataBridge).GetMethod("FlushMatch", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(bridge, null);
        }

        static Bounds VisibleTextBounds(TextMeshProUGUI text, Transform relativeTo)
        {
            var bounds = new Bounds();
            bool hasBounds = false;
            foreach (var character in text.textInfo.characterInfo)
            {
                if (!character.isVisible) continue;
                var lower = relativeTo.InverseTransformPoint(text.transform.TransformPoint(character.bottomLeft));
                var upper = relativeTo.InverseTransformPoint(text.transform.TransformPoint(character.topRight));
                if (!hasBounds) { bounds = new Bounds(lower, Vector3.zero); hasBounds = true; }
                bounds.Encapsulate(lower); bounds.Encapsulate(upper);
            }
            Assert.That(hasBounds, Is.True, text.name + " must contain visible text.");
            return bounds;
        }
    }
}
