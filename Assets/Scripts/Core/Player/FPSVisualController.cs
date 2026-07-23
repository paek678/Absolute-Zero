using System.Collections.Generic;
using AbsoluteZero.Core.Common;
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

            InitFromScene();
        }

        void OnDestroy()
        {
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
            if (Instance != null) return;

            var existing = Object.FindAnyObjectByType<FPSVisualController>();
            if (existing != null)
            {
                Instance = existing;
                existing.InitFromScene();
                Debug.Log("[FPS] EnsureInstance — found existing in scene");
                return;
            }

            var cam = Camera.main;
            if (cam == null)
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
            if (Instance != null)
            {
                Debug.Log("[FPS] Build skipped — Instance already exists");
                return Instance;
            }

            Debug.Log($"[FPS] Build START (fallback) — parent={cameraTransform.name}");

            var root = new GameObject("FPS");
            root.transform.SetParent(cameraTransform, false);

            var spawnMarker = GameObject.Find("FPSAnimSpawn");
            if (spawnMarker != null)
            {
                root.transform.position = spawnMarker.transform.position;
                Debug.Log($"[FPS] Build — using FPSAnimSpawn position: {spawnMarker.transform.position}");
            }
            else
            {
                root.transform.localPosition = Vector3.zero;
            }
            root.transform.localRotation = Quaternion.identity;

            var sortGroup = root.AddComponent<SortingGroup>();
            sortGroup.sortingOrder = 0;

            var ctrl = Resources.Load<RuntimeAnimatorController>("FPS/FPSA");
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
            var spawnMarker = GameObject.Find("FPSAnimSpawn");
            if (spawnMarker != null)
            {
                transform.position = spawnMarker.transform.position;
                Debug.Log($"[FPS] ApplySpawnMarkerPosition: {spawnMarker.transform.position}");
            }
        }

        void CacheSprites()
        {
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
                Sprite sprite = null;
                if (!string.IsNullOrEmpty(itemName))
                    sprite = GameSprites.GetItemSprite(itemName);
                if (sprite == null)
                    _spriteCache.TryGetValue(trigger, out sprite);
                _itemRenderer.sprite = sprite;
            }

            _animator.SetTrigger(trigger);
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
