using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Match;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.UI.Game.Build;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game.Presenters
{
    /// <summary>Playback roles and replicated debuffs occupy fixed thermometer-local slots.</summary>
    public sealed class CombatRoleIconPresenter : MonoBehaviour
    {
        HudIcon _attacker, _target;
        readonly HudIcon[] _possession = new HudIcon[4];
        IGameDataBridge _bridge;
        GameHudRefs _refs;
        public bool AttackerVisible => _attacker != null && _attacker.IsVisible;
        public bool TargetVisible => _target != null && _target.IsVisible;

        public void Initialize(IGameDataBridge bridge, GameHudRefs refs)
        {
            _bridge = bridge; _refs = refs;
            _attacker = Create("AttackerRoleIcon", "UI_attacker");
            _target = Create("RecipientRoleIcon", "UI_target");
            for (int seat = 0; seat < _possession.Length; seat++)
                _possession[seat] = Create("PossessionStatusIcon" + seat, "skill_capture");
        }

        HudIcon Create(string name, string resource)
        {
            var sprite = Resources.Load<Sprite>(resource);
            if (sprite == null) { Debug.LogError("Missing combat role sprite: " + resource); return null; }
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            go.SetActive(false);
            return new HudIcon((RectTransform)go.transform);
        }

        void OnDisable() => Hide();
        void OnDestroy()
        {
            // Icons belong to this presenter even after reparenting into thermometer canvases.
            _attacker?.Dispose(); _target?.Dispose();
            foreach (var icon in _possession) icon?.Dispose();
        }
        void Hide()
        {
            _attacker?.Hide(); _target?.Hide();
            foreach (var icon in _possession) icon?.Hide();
        }

        void LateUpdate()
        {
            var match = MatchCompositionRoot.Instance;
            if (_bridge == null || match == null || !match.IsSessionCurrent
                || match.gameObject.scene != gameObject.scene)
            { Hide(); return; }
            if (CombatVFXManager.Instance != null
                && CombatVFXManager.Instance.TryGetItemParticipants(out var actor, out var target))
            { ShowRole(_attacker, actor); ShowRole(_target, target); }
            else { _attacker?.Hide(); _target?.Hide(); }

            byte mask = match.NetworkState?.GhostPossessedMask.Value ?? 0;
            bool hasPerspective = LocalMatchPerspective.TryResolveCurrent(out var perspective);
            for (int seat = 0; seat < _possession.Length; seat++)
            {
                var icon = _possession[seat];
                if (icon == null) continue;
                if ((mask & (1 << seat)) == 0 || !hasPerspective
                    || !perspective.TryGetBinding(seat, out var binding))
                { icon.Hide(); continue; }
                bool local = perspective.IsHumanSeat(seat);
                var bar = local ? _refs.MyHpSlider?.transform as RectTransform : RemoteBar(binding.State.PlayerIndex);
                // Reserve a separate status slot so starting/ending an attack never moves the debuff.
                icon.Show(bar, local ? 0 : 1, local ? 64f : 120f);
            }
        }

        RectTransform RemoteBar(int seat)
        {
            int slot = AZPlayerVisual.GetRemoteVisualSlot(seat, _bridge.LocalSeatIndex,
                _bridge.CurrentMatch.Mode == Core.Network.GameMode.Multi);
            return slot >= 0 && slot < _refs.OppBars.Count
                ? _refs.OppBars[slot].HpSlider?.transform as RectTransform : null;
        }

        void ShowRole(HudIcon icon, AZPlayerVisual visual)
        {
            if (icon == null) return;
            if (visual == null || visual.IsLocalHuman) { icon.Hide(); return; }
            var state = visual.GetComponent<PlayerState>();
            icon.Show(state != null ? RemoteBar(state.PlayerIndex) : null, 0, 120f);
        }

        sealed class HudIcon
        {
            readonly RectTransform _rect;
            public bool IsVisible => _rect != null && _rect.gameObject.activeInHierarchy;
            public HudIcon(RectTransform rect) => _rect = rect;
            public void Hide() { if (_rect != null) _rect.gameObject.SetActive(false); }
            public void Dispose() { if (_rect != null) Object.Destroy(_rect.gameObject); }
            public void Show(RectTransform bar, int lane, float size)
            {
                if (_rect == null) return;
                if (bar == null) { Hide(); return; }
                if (_rect.parent != bar)
                {
                    _rect.SetParent(bar, false);
                    _rect.anchorMin = _rect.anchorMax = new Vector2(1f, 0f);
                    _rect.pivot = new Vector2(.5f, .5f);
                    _rect.sizeDelta = new Vector2(size, size);
                    _rect.anchoredPosition = new Vector2(-size * .5f - 10f - lane * (size + 8f), -size * .5f - 4f);
                    _rect.localScale = Vector3.one;
                    _rect.localRotation = Quaternion.identity;
                }
                _rect.gameObject.SetActive(true);
            }
        }
    }
}
