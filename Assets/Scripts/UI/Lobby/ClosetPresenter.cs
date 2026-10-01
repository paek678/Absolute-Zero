using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Session;

namespace AbsoluteZero.UI.LobbyUI
{
    public interface IClosetView
    {
        event Action<CosmeticPart> OnTabChanged;
        event Action<CosmeticItemSO> OnEquipClicked;
        event Action<CosmeticPart> OnUnequipClicked;
        event Action OnCloseClicked;
        event Action<CosmeticItemSO> OnItemSelected;
        event Action OnResetPreview;
        event Action<bool> OnVisibilityChanged;
        CosmeticPart CurrentTab { get; }
        void RenderItems(IReadOnlyList<CosmeticItemSO> items, CosmeticSnapshot snapshot);
        void SetStatus(string message);
        void SetSelection(CosmeticSnapshot equipped, CosmeticItemSO selected, CosmeticSnapshot preview);
        void SetPublicationStatus(string message);
    }

    public sealed class ClosetPresenter : IDisposable
    {
        readonly IClosetView _view;
        readonly Func<CosmeticEquipmentService> _equipment;
        readonly Func<CosmeticSnapshot, Task<CosmeticPublicationResult>> _publish;
        bool _active, _disposed, _closing;
        long _generation;
        long _publicationSequence;
        CosmeticItemSO _selected;
        CosmeticPublicationResult? _lastPublication;
        public event Action CloseCompleted;
        public ClosetPresenter(IClosetView view, Func<CosmeticEquipmentService> equipment,
            Func<CosmeticSnapshot, Task<CosmeticPublicationResult>> publish)
        {
            _view = view; _equipment = equipment; _publish = publish;
            view.OnTabChanged += ChangeTab;
            view.OnEquipClicked += Equip;
            view.OnUnequipClicked += Unequip;
            view.OnCloseClicked += Close;
            view.OnItemSelected += Select;
            view.OnResetPreview += ResetPreview;
            view.OnVisibilityChanged += SetActive;
        }
        public void SetActive(bool active)
        {
            if (_disposed || _active == active) return;
            _active = active; _generation++;
            _selected = null;
            if (active)
            {
                _view.SetStatus("닫으면 착용한 모습이 이 PC에 저장됩니다");
                var revision = _equipment()?.Snapshot.Revision;
                _view.SetPublicationStatus(_lastPublication.HasValue && _lastPublication.Value.Revision == revision
                    ? PublicationMessage(_lastPublication.Value.Status) : "온라인 외형은 다음 경기 입장 시 적용됩니다");
                Refresh(_view.CurrentTab);
            }
        }
        void ChangeTab(CosmeticPart part) { _selected = null; Refresh(part); }
        void Refresh(CosmeticPart part)
        {
            if (_disposed || !_active) return;
            var service = _equipment();
            if (service == null) { _view.SetStatus("장비 정보를 준비하지 못했습니다. 다시 열어주세요."); return; }
            _view.RenderItems(service.GetItems(part), service.Snapshot);
            RenderSelection(service);
        }
        void Select(CosmeticItemSO item)
        {
            if (_disposed || !_active || _closing || item == null || item.Part != _view.CurrentTab) return;
            var service = _equipment();
            if (service == null) return;
            foreach (var candidate in service.GetItems(item.Part))
                if (candidate == item) { _selected = candidate; RenderSelection(service); return; }
        }
        void ResetPreview()
        {
            if (_disposed || !_active || _closing) return;
            _selected = null;
            var service = _equipment();
            if (service != null) RenderSelection(service);
        }
        void RenderSelection(CosmeticEquipmentService service)
        {
            var equipped = service.Snapshot;
            var dto = equipped.ToDto();
            if (_selected != null) CosmeticCodec.SetPart(dto, _selected.Part, _selected.Id);
            _view.SetSelection(equipped, _selected, new CosmeticSnapshot(dto, equipped.Revision));
        }
        void Equip(CosmeticItemSO item)
        {
            if (_disposed || !_active || _closing || item == null || item != _selected || item.Part != _view.CurrentTab) return;
            Mutate(item.Part, item.Id);
        }
        void Unequip(CosmeticPart part)
        {
            if (_disposed || !_active || _closing || part != _view.CurrentTab) return;
            Mutate(part, "");
        }
        void Mutate(CosmeticPart part, string id)
        {
            var result = _equipment()?.Set(part, id) ?? default;
            _view.SetStatus(result.Success ? "착용 변경됨 · 닫으면 이 PC에 저장됩니다" : result.Error ?? "장비 정보를 준비하지 못했습니다.");
            _selected = null;
            var service = _equipment();
            if (service != null) RenderSelection(service);
        }
        void Close()
        {
            if (_disposed || !_active || _closing) return;
            _closing = true;
            try
            {
                var service = _equipment();
                if (service == null) { _view.SetStatus("장비 정보를 준비하지 못했습니다. 다시 시도해 주세요."); return; }
                long generation = _generation;
                _selected = null;
                RenderSelection(service);
                var result = service.Save();
                if (_disposed || !_active || generation != _generation) return;
                if (!result.Success) { _view.SetStatus("저장 실패: 닫기를 눌러 다시 시도해 주세요."); return; }
                var saved = service.Snapshot;
                if (saved.Revision != result.Revision)
                { _view.SetStatus("장비가 변경되었습니다. 닫기를 눌러 다시 저장해 주세요."); return; }
                // Navigation completes locally. Cloud completion never navigates a later screen.
                _active = false;
                _generation++;
                CloseCompleted?.Invoke();
                if (_publish != null) _ = Publish(saved, ++_publicationSequence);
            }
            finally { _closing = false; }
        }
        async Task Publish(CosmeticSnapshot saved, long sequence)
        {
            try
            {
                var result = await _publish(saved);
                if (!_disposed && sequence == _publicationSequence && result.Revision == saved.Revision
                    && _equipment()?.Snapshot.Revision == saved.Revision) _lastPublication = result;
                if (result.Status == CosmeticPublicationStatus.Failed)
                    UnityEngine.Debug.LogWarning("[ClosetPresenter] Outfit saved locally; lobby publication failed.");
            }
            catch (Exception error)
            {
                if (!_disposed && sequence == _publicationSequence && _equipment()?.Snapshot.Revision == saved.Revision)
                    _lastPublication = new CosmeticPublicationResult(CosmeticPublicationStatus.Failed, saved.Revision, null);
                UnityEngine.Debug.LogWarning("[ClosetPresenter] Lobby publication failed: " + error.Message);
            }
        }
        static string PublicationMessage(CosmeticPublicationStatus status) => status switch
        {
            CosmeticPublicationStatus.Published => "이전 저장: 온라인 로비 반영 완료",
            CosmeticPublicationStatus.Failed => "이 PC에는 저장됨 · 온라인 반영 실패",
            CosmeticPublicationStatus.NotApplicable => "이 PC에는 저장됨 · 다음 경기 입장 시 적용",
            _ => "이 PC에는 저장됨 · 온라인 반영 미확인"
        };
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _active = false; _generation++;
            _view.OnTabChanged -= ChangeTab;
            _view.OnEquipClicked -= Equip;
            _view.OnUnequipClicked -= Unequip;
            _view.OnCloseClicked -= Close;
            _view.OnItemSelected -= Select;
            _view.OnResetPreview -= ResetPreview;
            _view.OnVisibilityChanged -= SetActive;
            _selected = null;
        }
    }
}
