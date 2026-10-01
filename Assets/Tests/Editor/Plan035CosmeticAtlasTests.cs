using System.Collections.Generic;
using AbsoluteZero.Core.Cosmetic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public class Plan035CosmeticAtlasTests
    {
        GameObject _root;
        SpriteRenderer _renderer;
        CosmeticAtlasRenderer _view;
        CosmeticAtlasSO _atlas;
        readonly List<Object> _objects = new();
        Sprite _idle, _drink, _outfit;
        Sprite Sprite()
        {
            var texture = new Texture2D(2, 2); _objects.Add(texture);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            _objects.Add(sprite); return sprite;
        }
        [SetUp] public void Setup()
        {
            _root = new GameObject("CosmeticTest");
            var body = new GameObject("body"); body.transform.SetParent(_root.transform);
            _renderer = body.AddComponent<SpriteRenderer>();
            _idle = Sprite(); _drink = Sprite(); _outfit = Sprite();
            _renderer.sprite = _idle;
            _view = _root.AddComponent<CosmeticAtlasRenderer>();
            _atlas = ScriptableObject.CreateInstance<CosmeticAtlasSO>(); _objects.Add(_atlas);
            _atlas.Bindings.Add(new CosmeticAtlasBinding { RendererPath = "body", Source = _idle, Replacement = _outfit });
        }
        [TearDown] public void Cleanup()
        {
            _view.Clear(); Object.DestroyImmediate(_root);
            foreach (var value in _objects) Object.DestroyImmediate(value);
            _objects.Clear();
        }
        [Test] public void PoseChangesFollowAnimatorAndMissingPoseKeepsOriginal()
        {
            _view.Add(_atlas, CosmeticView.Character);
            Assert.AreSame(_outfit, _renderer.sprite);
            _view.ApplyFrame(); Assert.AreSame(_outfit, _renderer.sprite);
            _renderer.sprite = _drink; _view.ApplyFrame(); Assert.AreSame(_drink, _renderer.sprite);
            _renderer.sprite = _idle; _view.ApplyFrame(); Assert.AreSame(_outfit, _renderer.sprite);
        }
        [Test] public void UnequipRestoresLatestPoseAndDoesNotOverwriteNewAnimation()
        {
            _atlas.Bindings.Add(new CosmeticAtlasBinding { RendererPath = "body", Source = _drink, Replacement = _outfit });
            _view.Add(_atlas, CosmeticView.Character);
            _renderer.sprite = _drink; _view.ApplyFrame(); _view.Clear(); Assert.AreSame(_drink, _renderer.sprite);
            _view.Add(_atlas, CosmeticView.Character);
            _renderer.sprite = _idle; _view.Clear(); Assert.AreSame(_idle, _renderer.sprite);
        }
        [Test] public void EmptyReplacementAndWrongViewAreInert()
        {
            _view.Add(_atlas, CosmeticView.FirstPerson); Assert.AreSame(_idle, _renderer.sprite);
            _atlas.Bindings[0].Replacement = null;
            _view.Add(_atlas, CosmeticView.Character); Assert.AreSame(_idle, _renderer.sprite);
        }
        [Test] public void OverlayTracksPoseAndGhostVisibilityAndCleansUp()
        {
            _atlas.Bindings[0].Overlay = true;
            _view.Add(_atlas, CosmeticView.Character);
            var overlay = _renderer.transform.GetChild(0).GetComponent<SpriteRenderer>();
            Assert.True(overlay.enabled);
            _renderer.enabled = false; _view.ApplyFrame(); Assert.False(overlay.enabled);
            _renderer.enabled = true; _renderer.sprite = _drink; _view.ApplyFrame(); Assert.False(overlay.enabled);
            _renderer.sprite = _idle; _view.ApplyFrame(); Assert.True(overlay.enabled);
            _view.Clear(); _view.Clear(); Assert.AreEqual(0, _renderer.transform.childCount);
        }
        [Test] public void AtlasIsReadOnlyAndOtherCharacterIsNotModified()
        {
            var other = new GameObject("Other");
            try {
                var renderer = other.AddComponent<SpriteRenderer>(); renderer.sprite = _idle;
                _view.Add(_atlas, CosmeticView.Character);
                Assert.AreSame(_idle, _atlas.Bindings[0].Source);
                Assert.AreSame(_outfit, _atlas.Bindings[0].Replacement);
                Assert.AreSame(_idle, renderer.sprite);
            } finally { Object.DestroyImmediate(other); }
        }
        [Test] public void InvalidVersionDoesNotApplyEquipment()
        {
            var registry = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            var controller = new CosmeticVisualController(); controller.BindCharacter(_root.transform);
            controller.ApplyFromDto("{\"v\":99,\"head\":\"hat_01\"}", registry);
            Assert.AreEqual(1, _root.GetComponentsInChildren<SpriteRenderer>().Length);
        }
        [Test] public void UnknownPathsDoNotMoveOrDisableOriginalParts()
        {
            _atlas.Bindings[0].RendererPath = "missing/path";
            _view.Add(_atlas, CosmeticView.Character);
            Assert.AreSame(_idle, _renderer.sprite); Assert.True(_renderer.enabled);
        }
        [Test] public void RegisteredAtlasSetsFitWireDtoAndRejectWrongPartOrUnknownId()
        {
            var service = _root.AddComponent<CosmeticProfileService>();
            var so = new SerializedObject(service);
            so.FindProperty("_registry").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
            Assert.True(service.TryValidateAndCanonicalizeDto("{\"v\":1,\"head\":\"hat_01\",\"top\":\"top_ref\",\"back\":\"back_ref\",\"bottom\":\"low_ref\",\"tail\":\"tail_ref\"}", out var canonical));
            Assert.LessOrEqual(canonical.Length, 125);
            Assert.False(service.TryValidateAndCanonicalizeDto("{\"v\":1,\"head\":\"top_ref\"}", out _));
            Assert.False(service.TryValidateAndCanonicalizeDto("{\"v\":1,\"head\":\"missing\"}", out _));
        }
    }
}
