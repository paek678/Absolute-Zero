using System;
using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AbsoluteZero.Tests
{
    public class Plan042AppearanceTests
    {
        readonly List<Object> _objects = new();
        GameObject _root;
        SpriteRenderer _body;
        CosmeticVisualController _controller;
        CosmeticAtlasRenderer _renderer;
        Sprite _idle, _attack, _robot, _robotAttack, _wing;
        CosmeticRegistrySO _registry;
        CharacterAppearanceProfileSO _profile;
        CosmeticAtlasSO _baseAtlas, _equipment;
        bool _current;
        T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        Sprite Sprite() => Keep(UnityEngine.Sprite.Create(Keep(new Texture2D(4, 4)), new Rect(0,0,4,4), Vector2.one * .5f));
        [SetUp] public void Setup()
        {
            _root = new GameObject("AppearanceTest");
            var child = new GameObject("body"); child.transform.SetParent(_root.transform);
            _body = child.AddComponent<SpriteRenderer>();
            _idle = Sprite(); _attack = Sprite(); _robot = Sprite(); _robotAttack = Sprite(); _wing = Sprite(); _body.sprite = _idle;
            _baseAtlas = Keep(ScriptableObject.CreateInstance<CosmeticAtlasSO>());
            _baseAtlas.Bindings.Add(new CosmeticAtlasBinding { RendererPath="body", Source=_idle, Replacement=_robot });
            _baseAtlas.Bindings.Add(new CosmeticAtlasBinding { RendererPath="body", Source=_attack, Replacement=_robotAttack });
            _profile = Keep(ScriptableObject.CreateInstance<CharacterAppearanceProfileSO>()); _profile.Atlases.Add(_baseAtlas);
            Assert.True(_profile.TrySnapshot(out var snapshot, out var error), error);
            _controller = new CosmeticVisualController(); _controller.BindCharacter(_root.transform, CosmeticView.Character, snapshot);
            _current = true; _controller.IsCurrent = () => _current;
            _renderer = _root.GetComponent<CosmeticAtlasRenderer>();
            _equipment = Keep(ScriptableObject.CreateInstance<CosmeticAtlasSO>());
            _equipment.Bindings.Add(new CosmeticAtlasBinding { RendererPath="body", Overlay=true, Replacement=_wing, SortOffset=-1 });
            var item=Keep(ScriptableObject.CreateInstance<CosmeticItemSO>());
            var so=new SerializedObject(item); so.FindProperty("_id").stringValue="back_01";so.FindProperty("_part").enumValueIndex=(int)CosmeticPart.Back;
            so.FindProperty("_atlas").objectReferenceValue=_equipment;so.ApplyModifiedPropertiesWithoutUndo();
            _registry=Keep(ScriptableObject.CreateInstance<CosmeticRegistrySO>());
            var reg=new SerializedObject(_registry);reg.FindProperty("_allItems").arraySize=1;reg.FindProperty("_allItems").GetArrayElementAtIndex(0).objectReferenceValue=item;reg.ApplyModifiedPropertiesWithoutUndo();
            _registry.RefreshEditorLookup();
        }
        [TearDown] public void Cleanup()
        { _controller?.Clear(); if(_root!=null) Object.DestroyImmediate(_root); foreach(var o in _objects) if(o!=null)Object.DestroyImmediate(o);_objects.Clear(); }
        AppearanceApplyReport Equip() => _controller.ApplyFromDto("{\"v\":1,\"back\":\"back_01\"}",_registry);
        [Test] public void BaseSurvivesPendingEquipUnequipAndLatestAnimation()
        {
            Assert.True(_controller.ApplyFromDto("",_registry).Success);Assert.AreSame(_robot,_body.sprite);
            Assert.True(Equip().Success);Assert.AreSame(_robot,_body.sprite);
            Assert.True(_controller.ApplyFromDto("",_registry).Success);Assert.AreEqual(1,_renderer.LayerCount);
            _body.sprite=_attack;_renderer.ApplyFrame();Assert.AreSame(_robotAttack,_body.sprite);
            _controller.ResetEquipment();Assert.AreEqual(0,_renderer.LayerCount);Assert.AreSame(_robotAttack,_body.sprite);
            _controller.Clear();Assert.AreSame(_attack,_body.sprite);
        }
        [Test] public void DuplicateEquipmentDoesNotAllocateLayers()
        { Assert.True(Equip().Success);var layer=_body.transform.GetChild(0);for(int i=0;i<10;i++)Assert.True(Equip().Success);Assert.AreEqual(1,_renderer.LayerCount);Assert.AreSame(layer,_body.transform.GetChild(0)); }
        [Test] public void EmptyRequiredAtlasRejectsInvisibleSuccessAndCanRetry()
        {
            _controller.ResetEquipment();var binding=_equipment.Bindings[0];_equipment.Bindings.Clear();
            Assert.False(Equip().Success);Assert.AreSame(_robot,_body.sprite);Assert.AreEqual(0,_renderer.LayerCount);
            _equipment.Bindings.Add(binding);Assert.True(Equip().Success);Assert.AreEqual(1,_renderer.LayerCount);
        }
        [TestCase("hidden")] [TestCase("cache")] [TestCase("policy")]
        public void CommitFailureRestoresPreviousPoseAndCanRetry(string stage)
        {
            _controller.ResetEquipment(); _body.sprite=_attack;_renderer.ApplyFrame();
            _renderer.DebugFault=s=>{if(s==stage)throw new InvalidOperationException("injected");};
            var report=Equip();Assert.False(report.Success);Assert.True(report.Restored);Assert.AreSame(_robotAttack,_body.sprite);Assert.AreEqual(0,_renderer.LayerCount);
            _renderer.DebugFault=null;Assert.True(Equip().Success);Assert.AreEqual(1,_renderer.LayerCount);
        }
        [Test] public void MissingPathRejectsWholeCandidateAndKeepsPrevious()
        {
            Assert.True(Equip().Success);var layer=_body.transform.GetChild(0);
            _equipment.Bindings.Add(new CosmeticAtlasBinding {RendererPath="missing",Overlay=true,Replacement=_wing});
            // A changed canonical snapshot forces a candidate even if the same asset was edited in Editor.
            _controller.ResetEquipment();Assert.AreEqual(0,_renderer.LayerCount);
            Assert.False(Equip().Success);Assert.AreEqual(0,_renderer.LayerCount);Assert.AreSame(_robot,_body.sprite);
        }
        [TestCase("{\"v\":2}")] [TestCase("{\"v\":1,\"back\":\"missing\"}")]
        public void InvalidSnapshotPreservesAcceptedAppearance(string input)
        {Assert.True(Equip().Success);Assert.False(_controller.ApplyFromDto(input,_registry).Success);Assert.AreEqual(1,_renderer.LayerCount);Assert.AreSame(_robot,_body.sprite);}
        [Test] public void SuspendedRequestsWaitAndResumeUsesLatestSnapshot()
        {
            Equip();_controller.Suspend();Assert.False(_controller.ApplyFromDto("{\"v\":1}",_registry).Success);
            _body.sprite=_attack;_controller.Resume();Assert.True(_controller.ApplyFromDto("{\"v\":1}",_registry).Success);
            Assert.AreEqual(0,_renderer.LayerCount);Assert.AreSame(_robotAttack,_body.sprite);
        }
        [Test] public void StaleBindingDoesNotRestoreOldParticipant()
        {
            _controller.ResetEquipment();_renderer.DebugFault=s=>{if(s=="cache"){_current=false;throw new Exception("stale");}};
            Assert.False(Equip().Success);Assert.AreSame(_idle,_body.sprite);Assert.AreEqual(0,_renderer.LayerCount);
        }
        [Test] public void DiagnosticFailureDoesNotRollBackCommittedResult()
        {
            _controller.Applied=_=>throw new Exception("diagnostic-only");LogAssert.Expect(LogType.Warning,"[AppearanceReport] diagnostic-only");
            Assert.True(Equip().Success);Assert.AreEqual(1,_renderer.LayerCount);_controller.Applied=null;_controller.Clear();_controller.Clear();
        }
        [Test] public void SnapshotDoesNotChangeWhenSourceProfileEdited()
        {_baseAtlas.Bindings[0].Replacement=_wing;_baseAtlas.Bindings.Clear();Assert.True(_controller.ResetEquipment().Success);Assert.AreSame(_robot,_body.sprite);}
        [Test] public void PreviewPropagatesMissingBindingAndCanRetry()
        {
            using var preview=new PrivateCosmeticPreview(_root.transform,_registry,8);
            var snapshot=new CosmeticSnapshot(new CosmeticDto {back="back_01"},1);
            Assert.True(preview.Apply(snapshot));_equipment.Bindings[0].RendererPath="missing";
            Assert.True(preview.Apply(new CosmeticSnapshot(new CosmeticDto(),2)));
            Assert.False(preview.Apply(snapshot));_equipment.Bindings[0].RendererPath="body";Assert.True(preview.Apply(snapshot));
            Assert.True(preview.Root.GetComponentsInChildren<Transform>(true).All(t=>t.gameObject.layer==8));
        }
        [Test] public void OverlayFollowsVisibilityTintSortingAndOptionalMirroredOffset()
        {
            _equipment.Bindings[0].LocalPosition=new Vector3(2,0,0);_equipment.Bindings[0].MirrorPositionWithFlipX=true;
            _renderer.LayersVisible=false;Assert.True(Equip().Success);
            var r=_body.transform.GetChild(0).GetComponent<SpriteRenderer>();Assert.False(r.enabled);
            _renderer.LayersVisible=true;_body.flipX=true;_body.sortingOrder=7;_body.color=Color.cyan;_renderer.ApplyFrame();
            Assert.True(r.enabled);Assert.AreEqual(6,r.sortingOrder);Assert.AreEqual(-2,r.transform.localPosition.x);Assert.AreEqual(Color.cyan,r.color);
        }
        [Test] public void ConflictingProfileRejectedBeforeSceneMutation()
        { _baseAtlas.Bindings.Add(new CosmeticAtlasBinding {RendererPath="body",Source=_idle,Replacement=_wing});Assert.False(_profile.TrySnapshot(out _,out _));Assert.AreSame(_idle,_body.sprite); }
        [Test] public void OldOwnerCleanupCannotClearNewParticipantOnSameVisual()
        {
            Equip();var next=new CosmeticVisualController();next.BindCharacter(_root.transform);
            try {
                Assert.True(next.ApplyFromDto("{\"v\":1,\"back\":\"back_01\"}",_registry).Success);
                Assert.False(Equip().Success);_controller.Suspend();_controller.Clear();
                Assert.AreEqual(1,_renderer.LayerCount);Assert.AreSame(_idle,_body.sprite);
                Assert.True(_body.transform.GetChild(0).gameObject.activeSelf);
            } finally {next.Clear();}
        }
        [Test] public void LifecycleCallbacksReapplyRetainedSnapshotWithoutEvent()
        {
            Equip();string current="{\"v\":1,\"back\":\"back_01\"}";int replay=0;
            _renderer.Reactivated=()=>{replay++;_controller.ApplyFromDto(current,_registry);};
            // EditMode dispatch is explicit. Actual SetActive is exercised by the opt-in player probe.
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            typeof(CosmeticAtlasRenderer).GetMethod("OnDisable",flags).Invoke(_renderer,null);
            current="{\"v\":1}";
            typeof(CosmeticAtlasRenderer).GetMethod("OnEnable",flags).Invoke(_renderer,null);
            Assert.AreEqual(1,replay);Assert.AreEqual(0,_renderer.LayerCount);Assert.AreSame(_robot,_body.sprite);
        }
        [Test] public void ClosetSurfaceFailureRetainsSuccessfulPreviewAndRetries()
        {
            var go=Keep(new GameObject("Closet"));
            var surface=go.AddComponent<AbsoluteZero.UI.LobbyUI.ClosetPreviewSurface>();
            var view=go.GetComponent<AbsoluteZero.UI.LobbyUI.ClosetViewBindings>();
            var hint=Keep(new GameObject("Hint",typeof(RectTransform)));hint.transform.SetParent(go.transform);
            view.PreviewHint=hint.AddComponent<TMPro.TextMeshProUGUI>();
            var image=Keep(new GameObject("Image",typeof(RectTransform)));image.transform.SetParent(go.transform);
            view.Preview=image.AddComponent<UnityEngine.UI.RawImage>();
            var preview=new PrivateCosmeticPreview(_root.transform,_registry,8);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(AbsoluteZero.UI.LobbyUI.ClosetPreviewSurface).GetField("_visual",flags).SetValue(surface,preview);
            var good=new CosmeticSnapshot(new CosmeticDto(),0);Assert.True(surface.Show(good));var originalRoot=surface.VisualRoot;
            _equipment.Bindings[0].RendererPath="missing";
            var request=new CosmeticSnapshot(new CosmeticDto{back="back_01"},1);Assert.False(surface.Show(request));
            Assert.AreSame(originalRoot,surface.VisualRoot);Assert.True(view.PreviewHint.gameObject.activeSelf);
            Assert.AreSame(good,typeof(AbsoluteZero.UI.LobbyUI.ClosetPreviewSurface).GetField("_applied",flags).GetValue(surface));
            _equipment.Bindings[0].RendererPath="body";Assert.True(surface.Show(request));Assert.False(view.PreviewHint.gameObject.activeSelf);
            surface.Release();
        }
    }
}
