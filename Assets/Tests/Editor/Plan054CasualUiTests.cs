using System.Reflection;
using AbsoluteZero.UI.Common;
using AbsoluteZero.UI.Game.Build;
using AbsoluteZero.UI.LobbyUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.Tests
{
    public sealed class Plan054CasualUiTests
    {
        [TestCase("Panel")] [TestCase("Pill")] [TestCase("Card")]
        public void ArtIsAvailableAndHasSlicingBorders(string name)
        {
            var sprite = Resources.Load<Sprite>("UI/Casual/" + name);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.border.x, Is.GreaterThan(0));
            Assert.That(sprite.border.y, Is.GreaterThan(0));
            Assert.That(sprite.border.x + sprite.border.z, Is.LessThanOrEqualTo(sprite.rect.width));
            Assert.That(sprite.border.y + sprite.border.w, Is.LessThanOrEqualTo(sprite.rect.height));
            Assert.That(CasualUiSkin.Font, Is.Not.Null);
        }

        [Test]
        public void SkinPreservesCallbacksRaycastAndDisabledState()
        {
            var go = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                var button = go.GetComponent<Button>(); var image = go.GetComponent<Image>();
                button.targetGraphic = image; image.raycastTarget = false;
                int clicks = 0; button.onClick.AddListener(() => clicks++); button.interactable = false;
                CasualUiSkin.Button(button, CasualUiSkin.Mint);
                CasualUiSkin.Button(button, CasualUiSkin.Peach);
                Assert.That(button.interactable, Is.False);
                Assert.That(image.raycastTarget, Is.False);
                button.onClick.Invoke(); Assert.That(clicks, Is.EqualTo(1));
                Assert.That(go.GetComponents<Image>().Length, Is.EqualTo(1));
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ToggleWithoutNotifyCanRefreshBothStatesWithoutDuplicatingThumb()
        {
            var go = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
            try
            {
                var toggle = go.GetComponent<Toggle>(); toggle.targetGraphic = go.GetComponent<Image>();
                int callbacks = 0; toggle.onValueChanged.AddListener(_ => callbacks++);
                toggle.SetIsOnWithoutNotify(false); CasualUiSkin.Toggle(toggle);
                var thumb = go.transform.Find("CasualThumb") as RectTransform;
                Assert.That(thumb.anchoredPosition.x, Is.LessThan(0));
                toggle.SetIsOnWithoutNotify(true); CasualUiSkin.Toggle(toggle); CasualUiSkin.Toggle(toggle);
                Assert.That(thumb.anchoredPosition.x, Is.GreaterThan(0));
                Assert.That(go.transform.childCount, Is.EqualTo(1));
                Assert.That(callbacks, Is.Zero);
                Assert.That(thumb.GetComponent<Image>().raycastTarget, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void LobbySkinLeavesArtworkAndDimInputContractAlone()
        {
            var go = new GameObject("MainUI", typeof(RectTransform));
            var panel = new GameObject("SettingsPanel", typeof(RectTransform)); panel.transform.SetParent(go.transform);
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button)); dim.transform.SetParent(panel.transform);
            var original = new Color(0, 0, 0, .4f); dim.GetComponent<Image>().color = original;
            try
            {
                CasualLobbySkin.Apply(go.transform); CasualLobbySkin.Apply(go.transform);
                Assert.That(dim.GetComponent<Image>().sprite, Is.Null);
                Assert.That(dim.GetComponent<Image>().color, Is.EqualTo(original));
                Assert.That(dim.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(go.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase("BuildGhostSkillPanel")] [TestCase("BuildEnvironmentPanel")] [TestCase("BuildCinematicOverlay")]
        public void CombatChromeBuildsHiddenAndPreservesInputOwners(string method)
        {
            var canvas = new GameObject("Canvas", typeof(Canvas)); var refs = new GameHudRefs();
            try
            {
                typeof(GameHudBuilder).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { refs, canvas.transform });
                Assert.That(canvas.transform.GetChild(0).gameObject.activeSelf, Is.False);
                if (refs.CinematicPlate != null)
                {
                    Assert.That(refs.CinematicPlate.raycastTarget, Is.False);
                    Assert.That(refs.CinematicPlate.color.a, Is.Zero);
                }
                if (refs.FrostStrikeButton != null)
                {
                    Assert.That(refs.FrostStrikeButton.targetGraphic.raycastTarget, Is.True);
                    refs.FrostStrikeButton.interactable = false;
                    Assert.That(refs.ChillAuraButton.interactable, Is.True);
                }
            }
            finally { Object.DestroyImmediate(canvas); }
        }
    }
}
