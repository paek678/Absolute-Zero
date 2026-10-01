using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Session;
using AbsoluteZero.UI.LobbyUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AbsoluteZero.Tests
{
    public class Plan037EquipmentTests
    {
        readonly List<Object> _owned = new();
        CosmeticRegistrySO _registry;
        CosmeticItemSO _hat, _top;
        CosmeticEquipState _state;
        CosmeticEquipmentService _service;
        Store _store;
        static void Field(object obj, string name, object value)
            => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); _owned.Add(a); return a; }
        CosmeticItemSO Item(string id, CosmeticPart part)
        {
            var a = Asset<CosmeticItemSO>(); Field(a, "_id", id); Field(a, "_part", part); return a;
        }
        void Rebuild(params CosmeticItemSO[] items)
        {
            Field(_registry, "_allItems", items.ToList());
            typeof(CosmeticRegistrySO).GetMethod("RebuildLookups", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_registry, null);
        }
        [SetUp] public void Setup()
        {
            _registry = Asset<CosmeticRegistrySO>(); _hat = Item("hat_01", CosmeticPart.Head); _top = Item("top_01", CosmeticPart.Top);
            Rebuild(_hat, _top); _state = new(); _store = new(); _service = new(_state, _registry, _store);
        }
        [TearDown] public void Cleanup()
        {
            foreach (var item in _owned) if (item != null) Object.DestroyImmediate(item);
            _owned.Clear();
        }
        sealed class Store : ICosmeticStore
        {
            public string Json = ""; public bool Fail; public int Writes; public Action DuringWrite;
            public string Read() => Json;
            public void Write(string json)
            {
                Writes++; DuringWrite?.Invoke();
                if (Fail) throw new InvalidOperationException("injected storage failure");
                Json = json;
            }
        }
        [Test] public void CommandsAreAtomicNoOpAndKeepOtherParts()
        {
            int events = 0; _state.OnEquipChanged += () => events++;
            Assert.True(_service.Set(CosmeticPart.Head, _hat.Id).Success);
            Assert.True(_service.Set(CosmeticPart.Head, _hat.Id).Success);
            Assert.True(_service.Set(CosmeticPart.Tail, "").Success);
            Assert.AreEqual(1, events); Assert.AreEqual(1, _service.Snapshot.Revision);
            Assert.True(_service.Set(CosmeticPart.Top, _top.Id).Success);
            Assert.AreEqual(_hat.Id, _service.Snapshot.Head);
            Assert.False(_service.Apply(new CosmeticDto { head = "", top = _hat.Id }).Success);
            Assert.AreEqual(2, events); Assert.AreEqual(_top.Id, _service.Snapshot.Top);
            Assert.True(_service.Set(CosmeticPart.Head, "").Success);
            Assert.AreEqual(_top.Id, _service.Snapshot.Top); Assert.AreEqual(3, events);
        }
        [Test] public void SnapshotAndCatalogCannotMutateTheSource()
        {
            _service.Set(CosmeticPart.Head, _hat.Id); var snapshot = _service.Snapshot;
            snapshot.ToDto().head = "bad";
            _registry.GetByPart(CosmeticPart.Head).Clear();
            Assert.Throws<NotSupportedException>(() => ((IList<CosmeticItemSO>)_service.GetItems(CosmeticPart.Head)).Clear());
            Assert.AreEqual(1, _registry.GetByPart(CosmeticPart.Head).Count);
            _service.Set(CosmeticPart.Head, ""); Assert.AreEqual(_hat.Id, snapshot.Head);
        }
        [TestCase("missing")][TestCase("top_01")][TestCase("123456789")]
        public void InvalidCandidateNeverChangesState(string id)
        {
            Assert.False(_service.Set(CosmeticPart.Head, id).Success);
            Assert.AreEqual(0, _service.Snapshot.Revision); Assert.AreEqual("", _service.Snapshot.Head);
        }
        [Test] public void DuplicateAndUndefinedPartsAreExcluded()
        {
            var duplicate = Item(_hat.Id, CosmeticPart.Head);
            var bad = Item("badpart", (CosmeticPart)40);
            LogAssert.Expect(LogType.Error, "[CosmeticRegistry] Duplicate Id 'hat_01' (2 items) — all excluded");
            LogAssert.Expect(LogType.Error, "[CosmeticRegistry] 'badpart': Type Overlay not allowed for Part 40");
            Rebuild(_hat, duplicate, bad);
            Assert.False(_service.Set(CosmeticPart.Head, _hat.Id).Success);
            Assert.False(_service.Set((CosmeticPart)40, bad.Id).Success);
        }
        [Test] public void SaveFailureKeepsDirtyEquipmentAndExplicitRetryWorks()
        {
            _service.Set(CosmeticPart.Head, _hat.Id); _store.Fail = true;
            Assert.False(_service.Save().Success); Assert.AreEqual(-1, _service.SavedRevision);
            Assert.AreEqual(_hat.Id, _service.Snapshot.Head);
            _store.Fail = false; Assert.True(_service.Save().Success);
            Assert.AreEqual(1, _service.SavedRevision);
            var reloaded = new CosmeticEquipmentService(new CosmeticEquipState(), _registry, _store);
            Assert.True(reloaded.Load().Success); Assert.AreEqual(_hat.Id, reloaded.Snapshot.Head);
        }
        [Test] public void SaveCompletionMarksOnlyCapturedRevision()
        {
            _service.Set(CosmeticPart.Head, _hat.Id);
            _store.DuringWrite = () => _service.Set(CosmeticPart.Top, _top.Id);
            Assert.AreEqual(1, _service.Save().Revision);
            Assert.AreEqual(1, _service.SavedRevision); Assert.AreEqual(2, _service.Snapshot.Revision);
            Assert.AreEqual("", JsonUtility.FromJson<CosmeticDto>(_store.Json).top);
        }
        [TestCase("{broken")][TestCase("{\"v\":99}")][TestCase("")]
        public void MalformedSaveUsesLegacyEmptyFallback(string raw)
        {
            _store.Json = raw; Assert.True(_service.Load().Success); Assert.AreEqual("", _service.Snapshot.Head);
        }
        [Test] public void UnknownSaveIdSkipsOnlyThatPart()
        {
            _store.Json = JsonUtility.ToJson(new CosmeticDto { head = "missing", top = _top.Id });
            LogAssert.Expect(LogType.Warning, "[CosmeticEquipState] Id 'missing' not found in registry — skipping");
            Assert.True(_service.Load().Success); Assert.AreEqual("", _service.Snapshot.Head); Assert.AreEqual(_top.Id, _service.Snapshot.Top);
        }
        [Test] public void CodecRejectsOversizedAndVersionButNormalizesComparison()
        {
            Assert.False(CosmeticCodec.TryParse(new string('a', 126), out _));
            Assert.False(CosmeticCodec.TryEncode(new CosmeticDto { v = 2 }, _registry, out _));
            Assert.True(CosmeticCodec.SameIds(new CosmeticDto { head = null }, new CosmeticDto()));
            Assert.False(default(CosmeticOperationResult).Success);
        }
        string Json(string id = "") => JsonUtility.ToJson(new CosmeticDto { head = id });
        [Test] public void LateDependenciesAndSynchronousHostAcceptanceSendOnce()
        {
            var tracker = new CosmeticSubmissionTracker(0); int sent = 0;
            tracker.Tick(1, null, null, "", _ => sent++);
            tracker.Tick(4, _registry, new CosmeticDto { head = _hat.Id }, "", s => { sent++; tracker.Observe(s); });
            Assert.AreEqual(CosmeticSubmissionStatus.Accepted, tracker.Status);
            tracker.Tick(50, _registry, new CosmeticDto(), Json(_hat.Id), _ => sent++);
            Assert.AreEqual(1, sent); Assert.AreEqual(CosmeticSubmissionStatus.Accepted, tracker.Status);
        }
        [Test] public void RetainedMatchingOrDifferentStateNeverResubmits()
        {
            foreach (bool match in new[] { true, false })
            {
                var tracker = new CosmeticSubmissionTracker(0); int sent = 0;
                tracker.Tick(0, _registry, new CosmeticDto { head = _hat.Id }, Json(match ? _hat.Id : ""), _ => sent++);
                Assert.AreEqual(0, sent);
                Assert.AreEqual(match ? CosmeticSubmissionStatus.Accepted : CosmeticSubmissionStatus.AcceptedDifferent, tracker.Status);
            }
        }
        [Test] public void TimeoutReportsUnconfirmedAndLateAcceptanceDoesNotRetry()
        {
            var tracker = new CosmeticSubmissionTracker(0); int sent = 0;
            tracker.Tick(0, _registry, new CosmeticDto { head = _hat.Id }, "", _ => sent++);
            tracker.Tick(10, _registry, new CosmeticDto(), "", _ => sent++);
            Assert.AreEqual(CosmeticSubmissionStatus.AcceptanceUnconfirmed, tracker.Status);
            tracker.Observe(Json(_hat.Id)); Assert.AreEqual(CosmeticSubmissionStatus.AcceptedLate, tracker.Status);
            tracker.Tick(100, _registry, new CosmeticDto(), Json(_hat.Id), _ => sent++); Assert.AreEqual(1, sent);
        }
        [Test] public void InvalidAndMissingDependenciesNeverDispatch()
        {
            var tracker = new CosmeticSubmissionTracker(0);
            tracker.Tick(0, _registry, new CosmeticDto { head = "bad" }, "", _ => Assert.Fail());
            Assert.AreEqual(CosmeticSubmissionStatus.InvalidLocalData, tracker.Status);
            var waiting = new CosmeticSubmissionTracker(0);
            waiting.Tick(5, null, null, "", _ => Assert.Fail());
            Assert.AreEqual(CosmeticSubmissionStatus.DependenciesUnavailable, waiting.Status);
            waiting.Tick(6, _registry, new CosmeticDto(), "", _ => Assert.Fail());
        }
        [Test] public void DisposalIgnoresOldBindingAndFreshBindingIsIndependent()
        {
            var old = new CosmeticSubmissionTracker(0);
            old.Tick(0, _registry, new CosmeticDto(), "", _ => { }); old.Dispose();
            old.Observe(Json()); Assert.AreEqual(CosmeticSubmissionStatus.Cancelled, old.Status);
            old.Tick(1, _registry, new CosmeticDto(), "", _ => Assert.Fail());
            var fresh = new CosmeticSubmissionTracker(1); int sent = 0;
            fresh.Tick(1, _registry, new CosmeticDto(), "", _ => sent++); Assert.AreEqual(1, sent);
        }
        sealed class View : IClosetView
        {
            public event Action<CosmeticPart> OnTabChanged;
            public event Action<CosmeticItemSO> OnEquipClicked;
            public event Action<CosmeticPart> OnUnequipClicked;
            public event Action OnCloseClicked;
            public event Action<CosmeticItemSO> OnItemSelected;
            public event Action OnResetPreview;
            public event Action<bool> OnVisibilityChanged;
            public CosmeticPart CurrentTab { get; set; } = CosmeticPart.Head;
            public CosmeticSnapshot Preview;
            public CosmeticItemSO Selected;
            public string Publication;
            public void SetSelection(CosmeticSnapshot equipped, CosmeticItemSO selected, CosmeticSnapshot preview)
            { Selected = selected; Preview = preview; }
            public void SetPublicationStatus(string message) => Publication = message;
            public string Status; public int Renders;
            public void RenderItems(IReadOnlyList<CosmeticItemSO> items, CosmeticSnapshot snapshot) => Renders++;
            public void SetStatus(string message) => Status = message;
            public void Select(CosmeticItemSO item) => OnItemSelected?.Invoke(item);
            public void Equip(CosmeticItemSO item) { Select(item); OnEquipClicked?.Invoke(item); }
            public void EquipWithoutSelect(CosmeticItemSO item) => OnEquipClicked?.Invoke(item);
            public void Reset() => OnResetPreview?.Invoke();
            public void Visible(bool visible) => OnVisibilityChanged?.Invoke(visible);
            public void Close() => OnCloseClicked?.Invoke();
            public void Tab() => OnTabChanged?.Invoke(CurrentTab);
            public void Unequip() => OnUnequipClicked?.Invoke(CurrentTab);
        }
        [Test] public void DraftSelectionDoesNotMutateSaveOrPublishAndTabResetDiscardIt()
        {
            var view = new View(); int published = 0;
            using var presenter = new ClosetPresenter(view, () => _service, _ => { published++; return Task.FromResult(default(CosmeticPublicationResult)); });
            presenter.SetActive(true); view.Select(_hat);
            Assert.AreEqual(_hat.Id, view.Preview.Head); Assert.AreEqual("", _service.Snapshot.Head);
            Assert.AreEqual(0, _store.Writes); Assert.AreEqual(0, published);
            view.Reset(); Assert.AreEqual("", view.Preview.Head); Assert.IsNull(view.Selected);
            view.Select(_hat); view.CurrentTab = CosmeticPart.Top; view.Tab();
            Assert.AreEqual("", view.Preview.Head); Assert.IsNull(view.Selected);
            view.EquipWithoutSelect(_hat); Assert.AreEqual("", _service.Snapshot.Head);
        }
        [Test] public void DisableDropsDraftAndStopsCommandsUntilReenabled()
        {
            var view = new View(); using var presenter = new ClosetPresenter(view, () => _service, null);
            view.Visible(true); view.Select(_hat); view.Visible(false); view.Equip(_hat);
            Assert.AreEqual("", _service.Snapshot.Head); Assert.AreEqual(0, _store.Writes);
            view.Visible(true); Assert.IsNull(view.Selected); Assert.AreEqual("", view.Preview.Head);
            view.Equip(_hat); Assert.AreEqual(_hat.Id, _service.Snapshot.Head);
        }
        [Test] public void ClosetRejectsForeignSelectionAndCommitsOnlyTheCurrentPart()
        {
            var view = new View(); using var presenter = new ClosetPresenter(view, () => _service, null);
            presenter.SetActive(true);
            var forged = Item(_hat.Id, CosmeticPart.Head);
            view.Equip(forged); view.Equip(_top);
            Assert.AreEqual(0, _service.Snapshot.Revision);
            view.Equip(_hat); view.CurrentTab = CosmeticPart.Top; view.Tab(); view.Equip(_top);
            Assert.AreEqual(_hat.Id, _service.Snapshot.Head); Assert.AreEqual(_top.Id, _service.Snapshot.Top);
            view.Unequip(); Assert.AreEqual(_hat.Id, _service.Snapshot.Head); Assert.AreEqual("", _service.Snapshot.Top);
        }
        [Test] public async Task ClosedPublicationFailureIsTruthfulOnReopenWithoutUndoingLocalSave()
        {
            var view = new View(); var cloud = new TaskCompletionSource<CosmeticPublicationResult>();
            using var presenter = new ClosetPresenter(view, () => _service, _ => cloud.Task);
            presenter.SetActive(true); view.Equip(_hat); view.Close();
            LogAssert.Expect(LogType.Warning, "[ClosetPresenter] Outfit saved locally; lobby publication failed.");
            cloud.SetResult(new CosmeticPublicationResult(CosmeticPublicationStatus.Failed, _service.Snapshot.Revision, null));
            await Task.Yield(); presenter.SetActive(true);
            StringAssert.Contains("온라인 반영 실패", view.Publication);
            Assert.AreEqual(_hat.Id, _service.Snapshot.Head); StringAssert.Contains(_hat.Id, _store.Json);
        }
        [Test] public async Task ClosetCloseIsLocalSingleFlightAndLateCloudCannotNavigate()
        {
            var view = new View(); var cloud = new TaskCompletionSource<CosmeticPublicationResult>();
            int publications = 0, navigation = 0;
            using var presenter = new ClosetPresenter(view, () => _service, _ => { publications++; return cloud.Task; });
            presenter.CloseCompleted += () => navigation++;
            presenter.SetActive(true); view.Equip(_hat); view.Close(); view.Close();
            Assert.AreEqual(1, navigation); Assert.AreEqual(1, _store.Writes); Assert.AreEqual(1, publications);
            presenter.SetActive(true); view.Unequip();
            cloud.SetResult(new CosmeticPublicationResult(CosmeticPublicationStatus.Published, 1, null));
            await Task.Yield(); Assert.AreEqual(1, navigation); Assert.AreEqual("", _service.Snapshot.Head);
        }
        [Test] public void ClosetSaveFailureStaysOpenAndDisposeUnsubscribes()
        {
            var view = new View(); int closed = 0;
            var presenter = new ClosetPresenter(view, () => _service, null); presenter.CloseCompleted += () => closed++;
            presenter.SetActive(true); view.Equip(_hat); _store.Fail = true; view.Close();
            Assert.AreEqual(0, closed); StringAssert.Contains("저장 실패", view.Status);
            _store.Fail = false; view.Close(); Assert.AreEqual(1, closed);
            presenter.Dispose(); presenter.Dispose(); int renders = view.Renders;
            view.Equip(_top); view.Tab(); view.Close();
            Assert.AreEqual(2, _store.Writes); Assert.AreEqual(renders, view.Renders);
        }
        [Test] public void ReentrantSaveCannotCloseNewerScreen()
        {
            var view = new View(); int closed = 0;
            using var presenter = new ClosetPresenter(view, () => _service, null); presenter.CloseCompleted += () => closed++;
            presenter.SetActive(true);
            _store.DuringWrite = () => { presenter.SetActive(false); presenter.SetActive(true); };
            view.Close(); Assert.AreEqual(0, closed);
        }
        [Test] public void ClosetCannotPublishUnsavedRevisionFromReentrantStore()
        {
            var view = new View(); int closed = 0, published = 0;
            using var presenter = new ClosetPresenter(view, () => _service, snapshot =>
            { published++; return Task.FromResult(new CosmeticPublicationResult(CosmeticPublicationStatus.NotApplicable, snapshot.Revision, null)); });
            presenter.CloseCompleted += () => closed++;
            presenter.SetActive(true);
            _store.DuringWrite = () => _service.Set(CosmeticPart.Head, _hat.Id);
            view.Close(); Assert.AreEqual(0, closed); Assert.AreEqual(0, published);
            _store.DuringWrite = null; view.Close(); Assert.AreEqual(1, closed); Assert.AreEqual(1, published);
        }
        [Test] public void LoadingIntoExistingStatePublishesOneNewRevision()
        {
            _service.Set(CosmeticPart.Head, _hat.Id); int events = 0;
            _state.OnEquipChanged += () => events++;
            _store.Json = JsonUtility.ToJson(new CosmeticDto { top = _top.Id });
            Assert.True(_service.Load().Success);
            Assert.AreEqual(2, _service.Snapshot.Revision); Assert.AreEqual(1, events);
            Assert.True(_service.Load().Success); Assert.AreEqual(1, events);
        }

        [Test] public void PreviewCopiesOnlyVisualsAndIsolatesNewOverlaysWithoutTouchingSource()
        {
            var source = new GameObject("source"); _owned.Add(source);
            source.AddComponent<BoxCollider>();
            var body = new GameObject("body"); body.transform.SetParent(source.transform, false);
            var head = new GameObject("head"); head.transform.SetParent(body.transform, false);
            var sr = head.AddComponent<SpriteRenderer>(); sr.sortingOrder = 17; sr.flipX = true;
            var texture = new Texture2D(2, 2); _owned.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f); _owned.Add(sprite); sr.sprite = sprite;
            var atlas = Asset<CosmeticAtlasSO>();
            atlas.Bindings.Add(new CosmeticAtlasBinding { RendererPath = "body/head", Overlay = true, Replacement = sprite });
            Field(_hat, "_atlas", atlas);
            var preview = new PrivateCosmeticPreview(source.transform, _registry, 30);
            Assert.IsEmpty(preview.Root.GetComponentsInChildren<Collider>(true));
            Assert.True(preview.Apply(new CosmeticSnapshot(new CosmeticDto { head = _hat.Id }, 0)));
            Assert.True(preview.Root.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == 30));
            Assert.AreEqual(0, head.transform.childCount); Assert.AreEqual(17, sr.sortingOrder); Assert.True(sr.flipX);
            preview.Apply(new CosmeticSnapshot(new CosmeticDto(), 1)); preview.Dispose(); preview.Dispose();
            Assert.True(sprite != null); Assert.True(texture != null); Assert.True(source != null);
        }
    }
}
