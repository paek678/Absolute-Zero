using System.Collections;
using System.Collections.Generic;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Player
{
    public class AZPlayerVisual : NetworkBehaviour
    {
        static readonly Dictionary<string, string> TriggerFallback = new()
        {
            { "use", "attack" },
            { "gun", "attack" },
            { "tape", "attack" },
            { "fan", "swing" },
            { "mask", "defence" },
        };

        Transform _visualRoot;
        Animator _animator;
        PlayerState _playerState;
        SpriteRenderer[] _spriteRenderers;
        Material[] _cachedMaterials;
        Coroutine _flashCoroutine;
        Coroutine _animEndCoroutine;

        SpriteRenderer _freezeRenderer;
        Sprite _freeze1;
        Sprite _freeze2;
        Sprite _freeze3;

        readonly WaitForSeconds _waitFlashEnd = new(0.5f);
        readonly WaitForSeconds _waitAnimEnd = new(0.6f);
        static readonly WaitForSeconds _waitFreezeTick = new(0.167f);
        static readonly WaitForSeconds _waitFreezeHold = new(1.5f);
        static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");
        static readonly int IsWindHash = Animator.StringToHash("isWind");

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _playerState = GetComponent<PlayerState>();

            if (IsOwner)
            {
                Debug.Log($"[PlayerVisual] OnNetworkSpawn IsOwner — initializing FPS");
                FPSVisualController.EnsureInstance();
                return;
            }

            Debug.Log($"[PlayerVisual] OnNetworkSpawn IsRemote — setting up EnemyPlayer visuals");
            var enemyGO = GameObject.Find("EnemyPlayer");
            if (enemyGO == null)
            {
                Debug.LogWarning("[PlayerVisual] EnemyPlayer GameObject NOT FOUND");
                return;
            }

            _visualRoot = enemyGO.transform;
            _animator = enemyGO.GetComponent<Animator>();
            if (_animator == null)
                _animator = enemyGO.GetComponentInChildren<Animator>();

            Debug.Log($"[PlayerVisual] EnemyPlayer: animator={(_animator != null)}, controller={(_animator != null && _animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "NONE")}");

            _spriteRenderers = enemyGO.GetComponentsInChildren<SpriteRenderer>(true);
            _cachedMaterials = new Material[_spriteRenderers.Length];
            for (int i = 0; i < _spriteRenderers.Length; i++)
                _cachedMaterials[i] = _spriteRenderers[i].material;

            Debug.Log($"[PlayerVisual] EnemyPlayer: {_spriteRenderers.Length} sprite renderers found");
            BuildFreezeObject(_visualRoot);
        }

        void BuildFreezeObject(Transform visual)
        {
            _freeze1 = Resources.Load<Sprite>("freeze1");
            _freeze2 = Resources.Load<Sprite>("freeze2");
            _freeze3 = Resources.Load<Sprite>("freeze3");
            if (_freeze1 == null) return;

            var existing = visual.Find("FreezeObject");
            if (existing != null)
            {
                _freezeRenderer = existing.GetComponent<SpriteRenderer>();
                existing.gameObject.SetActive(false);
                return;
            }

            var freezeGO = new GameObject("FreezeObject");
            freezeGO.transform.SetParent(visual, false);
            freezeGO.transform.localPosition = new Vector3(0.18f, 0.08f, 0f);

            _freezeRenderer = freezeGO.AddComponent<SpriteRenderer>();
            _freezeRenderer.sortingOrder = 20;
            _freezeRenderer.color = new Color(1f, 1f, 1f, 0.6f);

            var bodyRenderer = visual.Find("body")?.GetComponent<SpriteRenderer>();
            if (bodyRenderer != null)
            {
                _freezeRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
                _freezeRenderer.material = bodyRenderer.sharedMaterial;
            }

            freezeGO.SetActive(false);
        }

        public void PlayAnimation(string triggerName)
        {
            if (_animator == null) return;

            if (_animEndCoroutine != null)
                StopCoroutine(_animEndCoroutine);

            _animator.SetTrigger(triggerName);
            _animEndCoroutine = StartCoroutine(AnimEndRoutine());
        }

        IEnumerator AnimEndRoutine()
        {
            yield return _waitAnimEnd;
            if (_animator != null)
                _animator.SetTrigger("end");
            _animEndCoroutine = null;
        }

        public void SetWind(bool active)
        {
            if (_animator != null)
                _animator.SetBool(IsWindHash, active);
        }

        public void PlayDamageFlash()
        {
            Debug.Log("[PlayerVisual] PlayDamageFlash");
            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(DamageFlashRoutine());
        }

        IEnumerator DamageFlashRoutine()
        {
            if (_animator != null)
                _animator.SetTrigger("damage");

            float duration = 0.15f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float flash = Mathf.Lerp(1f, 0f, elapsed / duration);
                SetFlashAmount(flash);
                yield return null;
            }

            SetFlashAmount(0f);
            yield return _waitFlashEnd;

            if (_animator != null)
                _animator.SetTrigger("end");

            _flashCoroutine = null;
        }

        void SetFlashAmount(float amount)
        {
            if (_cachedMaterials == null) return;
            for (int i = 0; i < _cachedMaterials.Length; i++)
            {
                if (_cachedMaterials[i] != null)
                    _cachedMaterials[i].SetFloat(FlashAmount, amount);
            }
        }

        void Update()
        {
            if (_spriteRenderers == null || _spriteRenderers.Length == 0 || _playerState == null) return;

            float temp = _playerState.Temperature.Value;
            float normalized = Mathf.Clamp01(temp / 37f);
            Color tint = Color.Lerp(new Color(0.7f, 0.85f, 1f), Color.white, normalized);

            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] != null)
                    _spriteRenderers[i].color = tint;
            }

            if (_animator != null)
                _animator.SetBool(IsWindHash, _playerState.IsFanActive.Value);

            if (_freezeRenderer != null && _freezeRenderer.gameObject.activeSelf && temp >= 37f)
                _freezeRenderer.gameObject.SetActive(false);
        }

        public void PlayDeathSequence()
        {
            Debug.Log("[PlayerVisual] PlayDeathSequence START");
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            if (_animator != null)
                _animator.SetTrigger("freeze");

            if (_freezeRenderer != null)
            {
                _freezeRenderer.color = new Color(1f, 1f, 1f, 1f);
                _freezeRenderer.sprite = _freeze1;
                _freezeRenderer.gameObject.SetActive(true);
            }

            yield return _waitFreezeTick;
            if (_freezeRenderer != null && _freeze2 != null)
                _freezeRenderer.sprite = _freeze2;

            yield return _waitFreezeTick;
            if (_freezeRenderer != null && _freeze3 != null)
                _freezeRenderer.sprite = _freeze3;

            yield return _waitFreezeHold;

            CameraShake.Instance?.Shake(0.5f, 0.3f);
            CombatVFXManager.Instance?.PlayFinalBreakAt(GetVisualPosition());
        }

        public void PlayCombatAnimation(string triggerName)
        {
            if (_animator == null)
            {
                Debug.LogWarning($"[PlayerVisual] PlayCombatAnimation('{triggerName}') — _animator is NULL");
                return;
            }
            if (_animEndCoroutine != null)
            {
                StopCoroutine(_animEndCoroutine);
                _animEndCoroutine = null;
            }

            string resolved = triggerName;
            if (!HasParameter(triggerName) && TriggerFallback.TryGetValue(triggerName, out var fb))
            {
                Debug.Log($"[PlayerVisual] PlayCombatAnimation: '{triggerName}' NOT in animator → fallback '{fb}'");
                resolved = fb;
            }
            else
            {
                Debug.Log($"[PlayerVisual] PlayCombatAnimation: '{triggerName}' found in animator → direct trigger");
            }

            _animator.SetTrigger(resolved);
        }

        bool HasParameter(string paramName)
        {
            if (_animator == null) return false;
            foreach (var p in _animator.parameters)
                if (p.name == paramName) return true;
            return false;
        }

        public void ReturnToIdle()
        {
            Debug.Log("[PlayerVisual] ReturnToIdle");
            if (_animEndCoroutine != null)
            {
                StopCoroutine(_animEndCoroutine);
                _animEndCoroutine = null;
            }
            if (_animator != null)
                _animator.SetTrigger("end");
        }

        public Animator GetAnimator() => _animator;

        public Vector3 GetVisualPosition() =>
            _visualRoot != null ? _visualRoot.position : transform.position;
    }
}
