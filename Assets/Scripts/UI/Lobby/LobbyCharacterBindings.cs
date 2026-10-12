using AbsoluteZero.Core.Cosmetic;
using UnityEngine;

namespace AbsoluteZero.UI.LobbyUI
{
    /// <summary>Main-panel lifetime owns a local, visual-only equipped character.</summary>
    public sealed class LobbyCharacterBindings : MonoBehaviour
    {
        public GameObject VisualPrefab;
        public CosmeticRegistrySO Registry;
        public SpriteRenderer Fallback;
        public Sprite LobbyFace;
        // Visible pixels inside the original flattened lobby sprite, in normalized bottom-left coordinates.
        public Rect VisibleRect = new(164f / 512, 118f / 512, 184f / 512, 312f / 512);
        LobbyCharacterPresenter _presenter;
        void OnEnable() => TryBind();
        void Update() { if (_presenter == null) TryBind(); }
        void TryBind()
        {
            var profile = CosmeticProfileService.Instance;
            if (profile?.Equipment == null || VisualPrefab == null || Registry == null || Fallback == null) return;
            _presenter = new LobbyCharacterPresenter(VisualPrefab.transform, Registry, Fallback, profile.Equipment, profile.EquipState, VisibleRect, LobbyFace);
        }
        void OnDisable()
        {
            _presenter?.Dispose(); _presenter = null;
            if (Fallback != null) Fallback.enabled = false;
        }
    }
}
