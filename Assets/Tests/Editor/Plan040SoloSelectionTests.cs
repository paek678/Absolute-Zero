using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.UI.LobbyUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public class Plan040SoloSelectionTests
    {
        sealed class View : ISoloSelectionView
        {
            public event Action<int> Selected;
            public event Action StartClicked, BackClicked;
            public bool Visible, CanStart, CanSelect, CanBack;
            public int RenderCount, Count, SelectedIndex;
            public string Status;
            public void SetVisible(bool visible) => Visible = visible;
            public void Render(IReadOnlyList<string> titles, int selected, string details, string status, bool canStart, bool canSelect, bool canBack)
            { Count = titles.Count; SelectedIndex = selected; Status = status; CanStart = canStart; CanSelect = canSelect; CanBack = canBack; RenderCount++; }
            public void Choose(int index) => Selected?.Invoke(index);
            public void Start() => StartClicked?.Invoke();
            public void Back() => BackClicked?.Invoke();
        }
        View _view;
        SoloSelectionPresenter _presenter;
        SoloDuelDefinitionSO _encounter;
        TaskCompletionSource<Result<Unit>> _launch;
        TaskCompletionSource<bool> _stop;
        bool _available, _valid;
        int _starts, _stops, _closed;
        readonly List<UnityEngine.Object> _copies = new();

        [SetUp]
        public void SetUp()
        {
            _encounter = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>("Assets/Data/Solo/Encounters/DefaultSoloDuel.asset");
            _view = new View(); _available = _valid = true; _starts = _stops = _closed = 0;
            _launch = new TaskCompletionSource<Result<Unit>>(); _stop = new TaskCompletionSource<bool>();
            Make(new[] { new SoloLobbyProfile { Title = "baseline", Encounter = _encounter } });
        }
        void Make(IEnumerable<SoloLobbyProfile> profiles)
        {
            _presenter?.Dispose();
            _presenter = new SoloSelectionPresenter(_view, profiles, e => new SoloProfileStatus(_valid && e != null, "details", _valid ? null : "invalid"),
                e => { _starts++; _available = false; return _launch.Task; },
                () => { _stops++; return _stop.Task; }, () => _available);
            _presenter.Closed += () => _closed++;
        }
        [TearDown] public void TearDown() { _presenter.Dispose(); foreach (var copy in _copies) UnityEngine.Object.DestroyImmediate(copy); _copies.Clear(); }

        [Test] public async Task OpeningAndBackingOutDoesNotLaunch()
        {
            Assert.IsTrue(_presenter.Open()); Assert.IsTrue(_view.CanStart); Assert.AreEqual(0, _starts);
            _stop.SetResult(true); await _presenter.BackAsync();
            Assert.IsFalse(_view.Visible); Assert.AreEqual(1,_closed); Assert.AreEqual(0,_starts);
        }
        [Test] public async Task DoubleStartAndSelectionWhilePendingAreIgnored()
        {
            _presenter.Open(); var first = _presenter.StartAsync(); var duplicate = _presenter.StartAsync(); _view.Choose(1);
            Assert.AreEqual(1,_starts); Assert.IsFalse(_view.CanSelect); Assert.IsFalse(_view.CanStart); Assert.IsTrue(_view.CanBack);
            _launch.SetResult(Result<Unit>.Success(Unit.Value)); await first; await duplicate;
            Assert.AreEqual(1,_starts); Assert.AreEqual(0,_view.SelectedIndex);
        }
        [Test] public async Task StartRevalidatesAfterPreview()
        {
            _presenter.Open(); _valid = false; await _presenter.StartAsync();
            Assert.AreEqual(0,_starts); Assert.IsFalse(_view.CanStart); Assert.AreEqual("invalid",_view.Status);
        }
        [Test] public async Task BackDuringStartWaitsForCleanupAndIgnoresLateResult()
        {
            _presenter.Open(); var start = _presenter.StartAsync(); var back = _presenter.BackAsync();
            Assert.IsFalse(_view.CanBack); Assert.IsTrue(_presenter.IsBusy); Assert.AreEqual(0,_closed);
            _available = true; _stop.SetResult(true); Assert.IsFalse(back.IsCompleted);
            _launch.SetResult(Result<Unit>.Failure(OperationErrorCode.Cancelled,"old failure")); await start; await back;
            Assert.AreEqual(1,_closed); Assert.IsFalse(_view.Visible); Assert.IsFalse(_view.Status.Contains("old failure"));
            Assert.IsTrue(_presenter.Open()); Assert.IsTrue(_view.CanStart);
        }
        [Test] public async Task FailedStartCanRetry()
        {
            _presenter.Open(); var first = _presenter.StartAsync(); _available = true;
            _launch.SetResult(Result<Unit>.Failure(OperationErrorCode.InvalidState,"injected")); await first;
            Assert.IsTrue(_view.CanStart); Assert.That(_view.Status,Does.Contain("injected"));
            _launch = new TaskCompletionSource<Result<Unit>>(); var retry = _presenter.StartAsync();
            Assert.AreEqual(2,_starts); _launch.SetResult(Result<Unit>.Success(Unit.Value)); await retry;
        }
        [Test] public async Task FailedCleanupRetainsBlockAndAllowsBackRetry()
        {
            _presenter.Open(); var start = _presenter.StartAsync(); var back = _presenter.BackAsync();
            _stop.SetException(new InvalidOperationException("cleanup injected")); await back;
            Assert.IsFalse(_view.CanStart); Assert.IsTrue(_view.CanBack); Assert.AreEqual(0,_closed);
            _stop = new TaskCompletionSource<bool>(); var retry = _presenter.BackAsync();
            _available = true; _stop.SetResult(true); _launch.SetResult(Result<Unit>.Failure(OperationErrorCode.Cancelled,"late"));
            await start; await retry; Assert.AreEqual(2,_stops); Assert.AreEqual(1,_closed);
        }
        [Test] public async Task SceneUnloadDisposesViewWithoutStoppingSuccessfulTransfer()
        {
            _presenter.Open(); var start = _presenter.StartAsync(); _presenter.Dispose(); int renders = _view.RenderCount;
            _launch.SetResult(Result<Unit>.Success(Unit.Value)); await start;
            _view.Start(); _view.Back(); _view.Choose(0);
            Assert.AreEqual(renders,_view.RenderCount); Assert.AreEqual(0,_stops); Assert.AreEqual(1,_starts);
        }
        [Test] public void OnlineOwnershipBlocksOpen()
        { _available = false; Assert.IsFalse(_presenter.Open()); Assert.IsFalse(_view.Visible); Assert.AreEqual(0,_starts); }
        [Test] public async Task EmptyOrNullProfilesCannotStart()
        {
            Make(Array.Empty<SoloLobbyProfile>()); _presenter.Open(); await _presenter.StartAsync(); Assert.IsFalse(_view.CanStart);
            Make(new SoloLobbyProfile[]{null}); _presenter.Open(); await _presenter.StartAsync(); Assert.IsFalse(_view.CanStart); Assert.AreEqual(0,_starts);
        }
        [Test] public void FixturesAreExcludedAndDuplicateIdsAreBlocked()
        {
            var fixture = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>("Assets/Tests/Plan036Behavior/Configuration/FixtureSoloDuel.asset");
            Make(new[] { new SoloLobbyProfile { Encounter = fixture }, new SoloLobbyProfile { Encounter = _encounter } });
            _presenter.Open(); Assert.AreEqual(1,_view.Count); Assert.IsTrue(_view.CanStart);
            Make(new[] { new SoloLobbyProfile { Encounter = _encounter }, new SoloLobbyProfile { Encounter = _encounter } });
            _presenter.Open(); Assert.IsFalse(_view.CanStart); Assert.That(_view.Status,Does.Contain("ID"));
        }
        [Test] public async Task HiddenAndInvalidIndicesDoNotLaunchOrChangeSelection()
        {
            await _presenter.StartAsync(); Assert.AreEqual(0,_starts);
            _presenter.Open(); _view.Choose(-1); _view.Choose(500); Assert.AreEqual(0,_view.SelectedIndex);
        }
        [Test] public void MenuEntryIsCopiedSoCallerCannotReplaceItsProfile()
        {
            var entry = new SoloLobbyProfile { Encounter = _encounter,Title = "baseline" };
            Make(new[] { entry }); entry.Encounter = null; _presenter.Open(); Assert.IsTrue(_view.CanStart);
        }
        [Test] public async Task SynchronousStartExceptionDoesNotLeaveTheMenuBusy()
        {
            _presenter.Dispose();
            _presenter = new SoloSelectionPresenter(_view,new[]{new SoloLobbyProfile {Encounter=_encounter}},
                _=>new SoloProfileStatus(true,""),_=>throw new InvalidOperationException("sync start"),
                ()=>Task.CompletedTask,()=>true);
            _presenter.Open(); await _presenter.StartAsync();
            Assert.IsFalse(_presenter.IsBusy); Assert.IsTrue(_view.CanStart); Assert.That(_view.Status,Does.Contain("sync start"));
        }
        [Test] public async Task SelectedProfileRatherThanDefaultIsPassedToNewLaunch()
        {
            var second = UnityEngine.Object.Instantiate(_encounter); _copies.Add(second);
            using (var data = new SerializedObject(second)) {
                data.FindProperty("configId").stringValue = "test.menu.second";
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            _presenter.Dispose(); SoloDuelDefinitionSO launched = null;
            _presenter = new SoloSelectionPresenter(_view,new[]{new SoloLobbyProfile{Encounter=_encounter},new SoloLobbyProfile{Encounter=second}},
                _=>new SoloProfileStatus(true,""),e=>{launched=e;return Task.FromResult(Result<Unit>.Failure(OperationErrorCode.InvalidState,"test"));},
                ()=>Task.CompletedTask,()=>true);
            _presenter.Open(); _view.Choose(1); await _presenter.StartAsync();
            Assert.AreSame(second,launched);
            await _presenter.BackAsync(); _presenter.Open(); _view.Choose(0); await _presenter.StartAsync();
            Assert.AreSame(_encounter,launched);
        }
    }
}
