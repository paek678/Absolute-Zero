using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo.Configuration;
using UnityEngine;

namespace AbsoluteZero.UI.LobbyUI
{
    [Serializable]
    public sealed class SoloLobbyProfile
    {
        public string Title;
        [TextArea] public string Description;
        public SoloDuelDefinitionSO Encounter;
    }

    public readonly struct SoloProfileStatus
    {
        public readonly bool CanStart;
        public readonly string Details, Error;
        public SoloProfileStatus(bool canStart, string details, string error = null)
        { CanStart = canStart; Details = details; Error = error; }
    }

    public interface ISoloSelectionView
    {
        event Action<int> Selected;
        event Action StartClicked;
        event Action BackClicked;
        void SetVisible(bool visible);
        void Render(IReadOnlyList<string> titles, int selected, string details, string status,
            bool canStart, bool canSelect, bool canBack);
    }

    // Owns menu intent and its lifetime only; the app coordinator owns the session.
    public sealed class SoloSelectionPresenter : IDisposable
    {
        readonly ISoloSelectionView _view;
        readonly SoloLobbyProfile[] _profiles;
        readonly string[] _titles;
        readonly Func<SoloDuelDefinitionSO, SoloProfileStatus> _inspect;
        readonly Func<SoloDuelDefinitionSO, Task<Result<Unit>>> _start;
        readonly Func<Task> _stopOwnedStart;
        readonly Func<bool> _canLaunch;
        bool _active, _starting, _closing, _disposed;
        int _selected, _revision;
        TaskCompletionSource<Result<Unit>> _pending;
        public event Action Closed;
        public bool IsBusy => _starting || _closing;

        public SoloSelectionPresenter(ISoloSelectionView view, IEnumerable<SoloLobbyProfile> profiles,
            Func<SoloDuelDefinitionSO, SoloProfileStatus> inspect,
            Func<SoloDuelDefinitionSO, Task<Result<Unit>>> start, Func<Task> stopOwnedStart, Func<bool> canLaunch)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            // Fixtures never become ordinary menu choices, even in a development player.
            _profiles = (profiles ?? Array.Empty<SoloLobbyProfile>())
                .Where(p => p == null || p.Encounter == null || p.Encounter.Readiness != SoloConfigurationReadiness.ValidationFixture)
                .Select(p => p == null ? new SoloLobbyProfile() : new SoloLobbyProfile { Title = p.Title, Description = p.Description, Encounter = p.Encounter }).ToArray();
            _titles = _profiles.Select(p => string.IsNullOrWhiteSpace(p.Title) ? "설정되지 않은 봇" : p.Title).ToArray();
            _inspect = inspect; _start = start; _stopOwnedStart = stopOwnedStart; _canLaunch = canLaunch;
            _view.Selected += Select;
            _view.StartClicked += StartClicked;
            _view.BackClicked += BackClicked;
        }

        public bool Open()
        {
            if (_disposed || IsBusy || !_canLaunch()) return false;
            _active = true; _revision++;
            _view.SetVisible(true); Render(); return true;
        }

        public void Hide()
        {
            if (_disposed) return;
            _active = false; _revision++; _view.SetVisible(false);
        }

        void Select(int index)
        {
            if (!_active || _disposed || IsBusy || index < 0 || index >= _profiles.Length || !_canLaunch()) return;
            _selected = index; Render();
        }

        SoloProfileStatus Inspect()
        {
            if (_profiles.Length == 0) return new SoloProfileStatus(false, "등록된 봇이 없습니다.", "플레이할 봇 설정이 필요합니다.");
            try
            {
                var encounter = _profiles[_selected].Encounter;
                // Reject ambiguity in the explicit menu before resolving one member.
                if (encounter != null && _profiles.Count(p => p.Encounter != null && p.Encounter.ConfigId == encounter.ConfigId) > 1)
                    return new SoloProfileStatus(false, "", "봇 설정 ID가 중복되어 있습니다.");
                return _inspect(encounter);
            }
            catch (Exception error) { return new SoloProfileStatus(false, "", "설정을 확인하지 못했습니다: " + error.Message); }
        }

        void Render(string message = null)
        {
            if (_disposed || !_active) return;
            var state = Inspect();
            string description = _profiles.Length == 0 ? "" : _profiles[_selected].Description;
            _view.Render(_titles, _selected, description + "\n\n" + state.Details,
                message ?? state.Error ?? "봇을 선택한 뒤 시작하세요.",
                !IsBusy && state.CanStart && _canLaunch(), !IsBusy && _canLaunch(), !_closing);
        }

        async void StartClicked() => await StartAsync();
        public async Task StartAsync()
        {
            if (_disposed || !_active || IsBusy || !_canLaunch()) return;
            var current = Inspect();
            if (!current.CanStart) { Render(current.Error); return; }
            _starting = true;
            int revision = ++_revision;
            var completion = _pending = new TaskCompletionSource<Result<Unit>>();
            var encounter = _profiles[_selected].Encounter;
            Render("봇 대전 준비 중... 뒤로가기로 취소할 수 있습니다.");
            Result<Unit> result;
            try { result = await _start(encounter); }
            catch (Exception error) { result = Result<Unit>.Failure(OperationErrorCode.InvalidState, error.Message); }
            completion.TrySetResult(result);
            if (_disposed || !_active || revision != _revision) return;
            _starting = false;
            Render(result.IsFailure ? "시작 실패: " + result.ErrorMessage : "경기로 이동합니다...");
        }

        async void BackClicked() => await BackAsync();
        public async Task BackAsync()
        {
            if (_disposed || !_active || _closing) return;
            ++_revision;
            _closing = true;
            var pending = _pending;
            Render("로비로 돌아가는 중...");
            try
            {
                await _stopOwnedStart();
                if (pending != null) await pending.Task;
                if (_disposed) return;
                _starting = false; _closing = false; _pending = null;
                Hide(); Closed?.Invoke();
            }
            catch (Exception error)
            {
                if (_disposed) return;
                _starting = false; _closing = false;
                Render("종료하지 못했습니다. 뒤로가기로 재시도하세요: " + error.Message);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _active = false; ++_revision;
            _view.Selected -= Select; _view.StartClicked -= StartClicked; _view.BackClicked -= BackClicked;
            // Normal successful scene unload must not stop the persistent session.
            // Explicit Back owns cancellation; app shutdown owns final cleanup.
        }
    }
}
