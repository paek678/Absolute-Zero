using System;
using System.Collections;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.UI.Game.Presenters
{
    /// <summary>Client-side world presentation for authoritative ghost skill events.</summary>
    public sealed class GhostSkillVFXPresenter : MonoBehaviour
    {
        public static GhostSkillVFXPresenter Instance { get; private set; }
        MatchCompositionRoot _match;
        bool IsCurrent => _match != null && _match == MatchCompositionRoot.Instance
            && _match.IsSessionCurrent && _match.gameObject.scene == gameObject.scene;
        static Sprite _orbSprite;
        static Sprite _ringSprite;
        readonly System.Collections.Generic.List<GameObject> _liveEffects = new();
        readonly GameObject[] _possessionMarkers = new GameObject[4];

        public static event Action<byte, byte, byte> PresentationStarted;
        public static event Action<byte, byte, byte> PresentationImpact;

        void OnEnable()
        {
            _match = MatchCompositionRoot.Instance;
            Instance = this;
            TurnManager.OnGhostSkillUsedSequenced += OnGhostSkillUsed;
            CombatVFXManager.OnSuppressedItemCue += ShowSuppressedItemCue;
        }

        void OnDisable()
        {
            TurnManager.OnGhostSkillUsedSequenced -= OnGhostSkillUsed;
            CombatVFXManager.OnSuppressedItemCue -= ShowSuppressedItemCue;
            StopAllCoroutines();
            foreach (var effect in _liveEffects)
                if (effect != null) Destroy(effect);
            _liveEffects.Clear();
            for (int i = 0; i < _possessionMarkers.Length; i++)
            {
                if (_possessionMarkers[i] != null) Destroy(_possessionMarkers[i]);
                _possessionMarkers[i] = null;
            }
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!IsCurrent) { if (Instance == this) OnDisable(); return; }
            byte possessed = _match.NetworkState?.GhostPossessedMask.Value ?? 0;
            for (byte seat = 0; seat < _possessionMarkers.Length; seat++)
            {
                bool active = (possessed & (1 << seat)) != 0;
                var marker = _possessionMarkers[seat];
                if (!active)
                {
                    if (marker != null) Destroy(marker);
                    _possessionMarkers[seat] = null;
                    continue;
                }
                Vector3 position = ResolveSeatPosition(seat) + Vector3.up * 0.15f;
                if (marker == null)
                {
                    marker = CreateSprite($"PossessedSeat{seat}", GetRingSprite(), position,
                        new Color(0.56f, 0.38f, 0.96f, 0.75f), 114);
                    marker.transform.localScale = Vector3.one * 1.4f;
                    _possessionMarkers[seat] = marker;
                }
                else marker.transform.position = position;
            }
        }

        public void ShowSuppressedItemCue(byte actorSeat)
        { if (IsCurrent && isActiveAndEnabled) StartCoroutine(PlaySuppressedItemCue(actorSeat)); }

        IEnumerator PlaySuppressedItemCue(byte actorSeat)
        {
            var position = ResolveSeatPosition(actorSeat) + Vector3.up * 0.3f;
            var ring = CreateSprite("PossessionItemSuppressed", GetRingSprite(), position,
                new Color(0.58f, 0.4f, 1f, 0.92f), 120);
            _liveEffects.Add(ring);
            const float duration = 0.6f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (ring != null)
                {
                    float t = Mathf.Clamp01(elapsed / duration);
                    ring.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.7f, t);
                    var color = ring.GetComponent<SpriteRenderer>().color;
                    color.a = 0.92f * (1f - t);
                    ring.GetComponent<SpriteRenderer>().color = color;
                }
                yield return null;
            }
            _liveEffects.Remove(ring);
            if (ring != null) Destroy(ring);
        }

        void OnGhostSkillUsed(byte ghostSeat, byte skillIndex, byte targetSeat, uint castId)
        {
            if (!IsCurrent || !isActiveAndEnabled) return;
            StartCoroutine(Play(ghostSeat, skillIndex, targetSeat, castId));
        }

        IEnumerator Play(byte ghostSeat, byte skillIndex, byte targetSeat, uint castId)
        {
            Vector3 from = ResolveSeatPosition(ghostSeat) + Vector3.up * 0.8f;
            Vector3 to = ResolveSeatPosition(targetSeat) + Vector3.up * 0.85f;
            Color color = skillIndex == GhostSkillService.SKILL_GRUDGE
                ? new Color(0.35f, 0.92f, 1f, 0.95f)
                : new Color(0.45f, 0.55f, 1f, 0.92f);

            var cast = CreateSprite("GhostCastPulse", GetRingSprite(), from, color, 116);
            var projectile = CreateSprite(skillIndex == GhostSkillService.SKILL_GRUDGE
                ? "GrudgeProjectile" : "PossessionWisp", GetOrbSprite(), from, color, 118);
            PresentationStarted?.Invoke(ghostSeat, skillIndex, targetSeat);

            const float duration = 0.72f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = t * t * (3f - 2f * t);
                if (projectile != null)
                {
                    Vector3 position = Vector3.Lerp(from, to, smooth);
                    position.y += Mathf.Sin(t * Mathf.PI) * (skillIndex == 0 ? 1.25f : 0.72f);
                    projectile.transform.position = position;
                    projectile.transform.localScale = Vector3.one
                        * (0.72f + Mathf.Sin(t * Mathf.PI * 5f) * 0.1f);
                }
                if (cast != null)
                {
                    cast.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.35f, t);
                    var faded = color;
                    faded.a = 0.8f * (1f - t);
                    cast.GetComponent<SpriteRenderer>().color = faded;
                }
                yield return null;
            }

            if (cast != null) Destroy(cast);
            if (projectile != null) Destroy(projectile);

            var impact = CreateSprite(skillIndex == GhostSkillService.SKILL_GRUDGE
                ? "GrudgeImpact" : "PossessionImpact", GetRingSprite(), to, color, 119);
            PresentationImpact?.Invoke(ghostSeat, skillIndex, targetSeat);
            CombatVFXManager.Instance?.SignalGhostImpact(castId);
            elapsed = 0f;
            float impactDuration = skillIndex == GhostSkillService.SKILL_GRUDGE ? 0.55f : 1.1f;
            while (elapsed < impactDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / impactDuration);
                if (impact != null)
                {
                    float size = skillIndex == GhostSkillService.SKILL_GRUDGE
                        ? Mathf.Lerp(0.45f, 3.15f, t)
                        : Mathf.Lerp(0.7f, 4.15f, t);
                    impact.transform.localScale = Vector3.one * size;
                    var faded = color;
                    faded.a = Mathf.Lerp(0.88f, 0f, t);
                    impact.GetComponent<SpriteRenderer>().color = faded;
                }
                yield return null;
            }
            if (impact != null) Destroy(impact);
        }

        static Vector3 ResolveSeatPosition(byte seat)
        {
            foreach (var marker in FindObjectsByType<PlayerSeatMarker>(FindObjectsSortMode.None))
                if (marker != null && marker.Player != null && marker.SeatIndex == seat)
                    return marker.transform.position;

            // The local player has no remote seat marker. In every perspective the
            // local seat occupies the south slot, so this is also the target fallback.
            return new Vector3(0f, 0.65f, 0.8f);
        }

        static GameObject CreateSprite(string name, Sprite sprite, Vector3 position, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return go;
        }

        static Sprite GetOrbSprite() => _orbSprite != null
            ? _orbSprite : _orbSprite = CreateRadialSprite("GhostOrb", false);

        static Sprite GetRingSprite() => _ringSprite != null
            ? _ringSprite : _ringSprite = CreateRadialSprite("GhostRing", true);

        static Sprite CreateRadialSprite(string name, bool ring)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name + "Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float normalized = Vector2.Distance(new Vector2(x, y), center) / center.x;
                float alpha = ring
                    ? Mathf.Clamp01(1f - Mathf.Abs(normalized - 0.72f) * 12f)
                    : Mathf.Clamp01(1f - normalized);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
            sprite.name = name;
            return sprite;
        }
    }
}
