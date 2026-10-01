using System.Collections.Generic;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player.Identity;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbsoluteZero.Core.Player
{
    public class FPSVisualController : MonoBehaviour
    {
        public static FPSVisualController Instance { get; private set; }

        Animator _animator;
        SpriteRenderer _itemRenderer;
        bool _initialized;
        CosmeticVisualController _cosmetics;
        PlayerBinding _humanBinding;
        internal PlayerBinding BoundHuman => _humanBinding;
        public bool IsPresentationUsable => _initialized && _animator != null
            && _animator.runtimeAnimatorController != null && _itemRenderer != null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string DebugLastTrigger { get; private set; }
        public int DebugAnimationCount { get; private set; }
#endif

        public static bool TryBindLocalHuman(PlayerState state)
        {
            if (!LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !ReferenceEquals(perspective.HumanBinding.State, state)) return false;
            EnsureInstance();
            if (Instance == null || Instance.gameObject.scene != state.gameObject.scene) return false;
            Instance._humanBinding = perspective.HumanBinding;
            Instance.InitFromScene();
            var views = MatchViewBindings.ForScene(state.gameObject.scene);
            if (views != null && !views.RegisterFps(Instance, perspective.HumanBinding)) return false;
            // Replay retained cosmetics: metadata or the DTO may precede this view.
            Instance.ApplyCosmeticDto(state.CosmeticDataNV.Value.ToString());
            return Instance._initialized;
        }

        public static void ReleaseLocalHuman(PlayerState state)
        {
            if (Instance == null || !ReferenceEquals(Instance._humanBinding?.State, state)) return;
            MatchViewBindings.ForScene(Instance.gameObject.scene)?.ReleaseFps(Instance);
            Instance._humanBinding = null;
            Instance._cosmetics?.Clear();
            Instance._cosmetics = null;
            Instance.ReturnToIdle();
        }

        public void ApplyCosmeticDto(string json)
        {
            if (!LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !ReferenceEquals(perspective.HumanBinding, _humanBinding)) return;
            var registry = CosmeticProfileService.Instance?.Registry;
            if (registry == null) return;
            if (_cosmetics == null)
            {
                _cosmetics = new CosmeticVisualController();
                _cosmetics.BindCharacter(transform, CosmeticView.FirstPerson);
            }
            _cosmetics.ApplyFromDto(json, registry);
        }
        readonly Dictionary<string, Sprite> _spriteCache = new();

        const string IdleState = "New State";

        static readonly string[] SpriteNames =
        {
            "gun", "tape", "fan", "mask", "card", "eat", "hug"
        };

        static readonly Dictionary<string, string> TriggerToSprite = new()
        {
            { "swing", "fan" },
            { "defence", "mask" },
            { "use", "gun" },
            { "feed", "eat" },
        };

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            // Scene references may exist before participant metadata. The local
            // human's ready binding owns FPS initialization and cosmetic replay.
        }

        void OnDestroy()
        {
            MatchViewBindings.ForScene(gameObject.scene)?.ReleaseFps(this);
            _cosmetics?.Clear();
            if (Instance == this) Instance = null;
        }

        void InitFromScene()
        {
            if (_initialized) return;

            _animator = GetComponent<Animator>();
            var itemChild = transform.Find("item");
            if (itemChild != null)
                _itemRenderer = itemChild.GetComponent<SpriteRenderer>();

            CacheSprites();
            _initialized = true;

            Debug.Log($"[FPS] InitFromScene — animator={(_animator != null)}, " +
                      $"itemRenderer={(_itemRenderer != null)}, sprites={_spriteCache.Count}");
        }

        public static void EnsureInstance()
        {
            if (!LocalMatchPerspective.TryResolveCurrent(out var perspective)) return;
            var scene = perspective.HumanBinding.State.gameObject.scene;
            if (Instance != null && Instance.gameObject.scene == scene) return;
            Instance = null;

            var views = MatchViewBindings.ForScene(scene);
            FPSVisualController existing = views != null ? views.AuthoredFps : null;
            if (views == null) foreach (var root in scene.GetRootGameObjects())
            {
                existing = root.GetComponentInChildren<FPSVisualController>(true);
                if (existing != null) break;
            }
            if (existing != null)
            {
                Instance = existing;
                existing.InitFromScene();
                Debug.Log("[FPS] EnsureInstance — found existing in scene");
                return;
            }

            var cam = views != null ? views.GameplayCamera : Camera.main;
            if (cam == null || cam.gameObject.scene != scene)
            {
                Debug.LogWarning("[FPS] EnsureInstance — Camera.main is NULL");
                return;
            }

            var fpsTransform = cam.transform.Find("FPS");
            if (fpsTransform != null)
            {
                var ctrl = fpsTransform.GetComponent<FPSVisualController>();
                if (ctrl != null)
                {
                    Instance = ctrl;
                    ctrl.InitFromScene();
                    Debug.Log("[FPS] EnsureInstance — found FPS under camera");
                    return;
                }
            }

            Debug.LogWarning("[FPS] EnsureInstance — FPS not found in scene, falling back to Build");
            Build(cam.transform);
        }

        public static FPSVisualController Build(Transform cameraTransform)
        {
            if (cameraTransform == null || !LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || cameraTransform.gameObject.scene != perspective.HumanBinding.State.gameObject.scene) return null;
            if (Instance != null)
            {
                Debug.Log("[FPS] Build skipped — Instance already exists");
                return Instance;
            }

            Debug.Log($"[FPS] Build START (fallback) — parent={cameraTransform.name}");

            var root = new GameObject("FPS");
            root.transform.SetParent(cameraTransform, false);

            var views = MatchViewBindings.ForScene(cameraTransform.gameObject.scene);
            var spawnMarker = views != null ? views.FpsSpawn : GameObject.Find("FPSAnimSpawn")?.transform;
            if (spawnMarker != null)
            {
                root.transform.position = spawnMarker.position;
                Debug.Log($"[FPS] Build — using FPSAnimSpawn position: {spawnMarker.position}");
            }
            else
            {
                root.transform.localPosition = Vector3.zero;
            }
            root.transform.localRotation = Quaternion.identity;

            var sortGroup = root.AddComponent<SortingGroup>();
            sortGroup.sortingOrder = 0;

            var ctrl = views != null ? views.FpsController : Resources.Load<RuntimeAnimatorController>("FPS/FPSA");
            var anim = root.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var controller = root.AddComponent<FPSVisualController>();
            controller._animator = anim;

            BuildChild(root.transform, "hand1", new Vector3(-1.57f, -1.5f, 2f), Vector3.one);
            BuildChild(root.transform, "hand2", new Vector3(1.48f, -1.44f, 2f), Vector3.one);
            var itemSR = BuildChild(root.transform, "item",
                new Vector3(0f, -0.98f, 2f), new Vector3(1.5f, 1.5f, 1f));
            controller._itemRenderer = itemSR;

            var particleGO = new GameObject("Particle");
            particleGO.transform.SetParent(root.transform, false);
            particleGO.transform.localPosition = new Vector3(0f, 1.04f, 0f);
            particleGO.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            particleGO.AddComponent<ParticleSystem>();
            var psr = particleGO.GetComponent<ParticleSystemRenderer>();
            if (psr != null) psr.sortingOrder = 55;
            particleGO.SetActive(false);

            controller.CacheSprites();
            controller._initialized = true;
            Debug.Log($"[FPS] Build DONE — FPSA ctrl={(ctrl != null ? "OK" : "MISSING")}");
            return controller;
        }

        static SpriteRenderer BuildChild(Transform parent, string name, Vector3 localPos, Vector3 localScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.AddComponent<SpriteRenderer>();
            go.SetActive(false);
            return go.GetComponent<SpriteRenderer>();
        }

        void ApplySpawnMarkerPosition()
        {
            var views = MatchViewBindings.ForScene(gameObject.scene);
            var spawnMarker = views != null ? views.FpsSpawn : GameObject.Find("FPSAnimSpawn")?.transform;
            if (spawnMarker != null)
            {
                transform.position = spawnMarker.position;
                Debug.Log($"[FPS] ApplySpawnMarkerPosition: {spawnMarker.position}");
            }
        }

        void CacheSprites()
        {
            var views = MatchViewBindings.ForScene(gameObject.scene);
            if (views != null)
            {
                foreach (var name in SpriteNames) _spriteCache[name] = views.GetFpsSprite(name);
                foreach (var trigger in TriggerToSprite.Keys) _spriteCache[trigger] = views.GetFpsSprite(trigger);
                return;
            }
            foreach (var name in SpriteNames)
            {
                var sprites = Resources.LoadAll<Sprite>($"FPS/FPS_{name}");
                if (sprites.Length > 0)
                    _spriteCache[name] = sprites[0];
            }
            foreach (var kv in TriggerToSprite)
            {
                if (!_spriteCache.ContainsKey(kv.Key) && _spriteCache.TryGetValue(kv.Value, out var fallback))
                    _spriteCache[kv.Key] = fallback;
            }
            Debug.Log($"[FPS] CacheSprites — {_spriteCache.Count} cached ({SpriteNames.Length} direct + {TriggerToSprite.Count} fallback)");
        }

        public void PlayFPSAnimation(string trigger, string itemName = null)
            => PlayAnimation(trigger, string.IsNullOrEmpty(itemName) ? null : GameSprites.GetItemSprite(itemName), itemName);

        public void PlayFPSItemAnimation(string trigger, ItemDataSO item)
            => PlayAnimation(trigger, GameSprites.GetItemSpriteFor(item), item?.ItemName);

        void PlayAnimation(string trigger, Sprite itemSprite, string itemName)
        {
            if (_animator == null)
            {
                Debug.LogWarning($"[FPS] PlayFPSAnimation('{trigger}') — _animator is NULL");
                return;
            }

            _animator.Play(IdleState, 0, 0f);
            _animator.Update(0f);

            if (_itemRenderer != null)
            {
                Sprite sprite = itemSprite;
                if (sprite == null)
                    _spriteCache.TryGetValue(trigger, out sprite);
                _itemRenderer.sprite = sprite;
            }

            _animator.SetTrigger(trigger);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugLastTrigger = trigger;
            DebugAnimationCount++;
#endif
            Debug.Log($"[FPS] PlayFPSAnimation('{trigger}', item='{itemName}')");
        }

        public void ReturnToIdle()
        {
            if (_animator == null) return;
            _animator.Play(IdleState, 0, 0f);
            if (_itemRenderer != null)
                _itemRenderer.sprite = null;
        }
    }
}
