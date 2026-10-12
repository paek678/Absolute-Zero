using System.Reflection;
using AbsoluteZero.Core.Common;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.UI.Game.Build;
using AbsoluteZero.UI.Game.Presenters;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.Tests
{
    public sealed class Plan046PresentationTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject _root;
        MatchHudPresenter _hud;
        TextMeshProUGUI _timer;

        [SetUp] public void SetUp()
        {
            _root = new GameObject("Countdown fixture");
            _hud = _root.AddComponent<MatchHudPresenter>();
            var label = new GameObject("Time", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(_root.transform);
            _timer = label.GetComponent<TextMeshProUGUI>();
            typeof(MatchHudPresenter).GetField("_timerText", Private).SetValue(_hud, _timer);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        void Snapshot(int remaining, TurnPhase phase = TurnPhase.PrepPhase)
            => typeof(MatchHudPresenter).GetMethod("HandleMatchSnapshotChanged", Private).Invoke(_hud,
                new object[] { new MatchSnapshot { CurrentPhase = phase, RemainingTime = remaining, PrepDuration = 30 } });

        // Call only the timer path: score/crown are not part of this focused fixture.
        void Timer(int remaining, TurnPhase phase = TurnPhase.PrepPhase)
            => typeof(MatchHudPresenter).GetMethod("UpdateTimerDisplay", Private).Invoke(_hud,
                new object[] { new MatchSnapshot { CurrentPhase = phase, RemainingTime = remaining, PrepDuration = 30 } });
        bool Warning => (bool)typeof(MatchHudPresenter).GetField("_countdownWarningActive", Private).GetValue(_hud);

        [TestCase(6, false)] [TestCase(5, true)] [TestCase(1, true)] [TestCase(0, false)]
        public void CountdownWarningWorksWithoutBanner(int remaining, bool expected)
        {
            Timer(remaining);
            Assert.That(_timer.text, Is.EqualTo(remaining.ToString()));
            Assert.That(Warning, Is.EqualTo(expected));
            Timer(remaining);
            Assert.That(Warning, Is.EqualTo(expected));
        }

        [Test] public void PhaseExitStopsWarningAndClearsTimer()
        {
            Timer(4); Assert.That(Warning, Is.True);
            Timer(4, TurnPhase.AttackPhase);
            Assert.That(Warning, Is.False); Assert.That(_timer.text, Is.Empty);
        }

        [Test] public void DisabledPresenterCannotRestartWarningFromSubscribedSnapshot()
        {
            Timer(4); Assert.That(Warning, Is.True);
            _hud.enabled = false;
            // Ordinary MonoBehaviours do not receive lifecycle callbacks in Edit Mode.
            typeof(MatchHudPresenter).GetMethod("OnDisable", Private).Invoke(_hud, null);
            Assert.That(Warning, Is.False);
            Snapshot(3);
            Assert.That(Warning, Is.False);
            Assert.That(_timer.text, Is.EqualTo("4"));
        }

        [Test] public void TargetMarkerRejectsBehindCameraAndOutsideViewport()
        {
            var cameraObject = new GameObject("Fixture camera");
            cameraObject.transform.SetParent(_root.transform);
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0,0,-10);
            camera.orthographic = true; camera.orthographicSize = 5;
            camera.pixelRect = new Rect(0,0,1920,1080);
            Assert.That(TargetingArrowPresenter.TryProjectVisible(camera,Vector3.zero,out _), Is.True);
            Assert.That(TargetingArrowPresenter.TryProjectVisible(camera,new Vector3(0,0,-20),out _), Is.False);
            Assert.That(TargetingArrowPresenter.TryProjectVisible(camera,new Vector3(100,0,0),out _), Is.False);
            var marker = _root.AddComponent<TargetingArrowPresenter>(); marker.Initialize();
            var target = new GameObject("Target"); target.transform.SetParent(_root.transform);
            Assert.That(marker.Show(camera,target.transform,null),Is.True);
            Assert.That(marker.IsVisible,Is.True);
            var graphic = _root.transform.Find("TargetingArrowCanvas/TargetMarker").GetComponent<MaskableGraphic>();
            Assert.That(graphic.raycastTarget,Is.False);
            target.transform.position = new Vector3(0,0,-20);
            Assert.That(marker.Show(camera,target.transform,null),Is.False);
            Assert.That(marker.IsVisible,Is.False);
        }

        [Test] public void WardrobeRetainsPartIndicesWhileHidingOnlyBottom()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Closet/ClosetPanel.prefab");
            var view = prefab.GetComponent<AbsoluteZero.UI.LobbyUI.ClosetViewBindings>();
            Assert.That(view.Validate(out var error),Is.True,error);
            Assert.That(view.Tabs.Length,Is.EqualTo(5));
            for(int i=0;i<view.Tabs.Length;i++) Assert.That(view.Tabs[i].gameObject.activeSelf,
                Is.EqualTo(i != (int)AbsoluteZero.Core.Cosmetic.CosmeticPart.Bottom));
            Assert.That(view.Tabs[(int)AbsoluteZero.Core.Cosmetic.CosmeticPart.Tail].name,Is.EqualTo("Tab_Tail"));
        }
    }
}
