using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace AbsoluteZero.Core.Player
{
    public class AZPlayerVisual : NetworkBehaviour
    {
        static readonly Dictionary<string, string> TriggerFallback = new()
        {
            { "use", "attack" },
            { "gun", "attack" },
            { "tape", "attack" },
            { "fan", "attack" },
            { "mask", "defence" },
            { "feed", "attack" },
            { "disappoint", "damage" },
            { "jump", "attack" },
            { "hug", "attack" },
        };

        Transform _visualRoot;
        Animator _animator;
        PlayerState _playerState;
        SpriteRenderer[] _spriteRenderers;
        Material[] _cachedMaterials;
        Coroutine _flashCoroutine;
        Coroutine _animEndCoroutine;
        uint _animationVersion;
        Coroutine _deathCoroutine;
        Coroutine _bindCoroutine;
        IReadOnlyPlayerRegistry _registry;
        MatchViewBindings _targetViews;
        bool _roleResolved;
        bool _localHuman;
        bool _presentationReady;
        public bool IsLocalHuman => _roleResolved && LocalMatchPerspective.IsLocalHuman(_playerState);
        public bool IsPresentationReady => _presentationReady && IsSpawned
            && (_localHuman ? FPSVisualController.Instance != null && FPSVisualController.Instance.IsPresentationUsable
                : _visualRoot != null && _animator != null && _animator.runtimeAnimatorController != null
                    && _itemRenderer != null)
            && LocalMatchPerspective.TryResolveCurrent(out var perspective)
            && perspective.TryGetBinding(_playerState.PlayerIndex, out var binding)
            && ReferenceEquals(binding.State, _playerState);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static System.Func<PlayerState, bool> DebugHoldPresentationBinding;
        public int DebugPresentationBindCount { get; private set; }
        public string DebugLastCombatTrigger { get; private set; }
        public int DebugCombatAnimationCount { get; private set; }
#endif
        bool _isDead;
        bool _deathPresentationCompleted;
        bool _pendingGhostTransition;
        bool _isGhost;
        bool _characterVisible = true;
        float _ghostAlpha = 1f;
        GameObject _ghostPlaceholder;
        SpriteRenderer _ghostPlaceholderRenderer;
        Animator _ghostAnimator;
        const float GhostVisualScale = 0.78f;
        const float GhostGrowDuration = 0.6f;
        static readonly Vector3 GhostRestPosition = new(0f, 0.25f, -0.08f);
        float _ghostGrowStartedAt = -1f;
        float _ghostBottomLocalY;
        static readonly int GhostIdleState = Animator.StringToHash("Base Layer.Ghost_Idle");
        static readonly int GhostAttackState = Animator.StringToHash("Base Layer.Ghost_Attack");
        public bool IsDead => _isDead;
        public bool IsGhost => _isGhost;
        public bool IsGhostTransitionPending => _pendingGhostTransition;
        Vector3 _deathSavedPos;

        SpriteRenderer _freezeRenderer;
        SpriteRenderer _fanRenderer;

        Transform _itemTransform;
        SpriteRenderer _itemRenderer;
        Sprite _freeze1;
        Sprite _freeze2;
        Sprite _freeze3;

        ParticleSystem _iceBreakParticle;
        ParticleSystem _finalBreakParticle;

        CosmeticVisualController _cosmeticController;
        PlayerBinding _appearanceBinding;
        public AppearanceApplyReport AppearanceReport => _cosmeticController?.LastReport;

        readonly WaitForSeconds _waitFlashEnd = new(0.5f);
        readonly WaitForSeconds _waitAnimEnd = new(0.6f);
        static readonly WaitForSeconds _waitFreezeTick = new(0.167f);
        static readonly WaitForSeconds _waitFreezeHold = new(1.5f);
        public const float GhostRevealDelay = 0.5f;
        static readonly WaitForSeconds _waitGhostReveal = new(GhostRevealDelay);
        static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");
        static readonly int IsWindHash = Animator.StringToHash("isWind");
        static readonly int DegreeHash = Animator.StringToHash("degree");

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _playerState = GetComponent<PlayerState>();
            _roleResolved = _presentationReady = false;
            _registry = MatchCompositionRoot.Instance?.Registry;
            if (_registry != null) _registry.Registered += OnBindingReady;
            if (_playerState != null) _playerState.BindingReady += OnBindingReady;
            TryResolveRole();
            _bindCoroutine = StartCoroutine(DeferredBindRoutine());
            TurnManager.OnGhostSkillUsedSequenced += OnGhostSkillUsed;
        }

        void OnBindingReady(PlayerBinding binding) => TryResolveRole();

        void TryResolveRole()
        {
            if (_roleResolved || !IsSpawned || _playerState == null || !_playerState.IsParticipantReady
                || !LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !perspective.TryGetBinding(_playerState.PlayerIndex, out var binding)
                || !ReferenceEquals(binding.State, _playerState)) return;
            _localHuman = perspective.IsHumanSeat(_playerState.PlayerIndex);
            _roleResolved = true;
            if (!_localHuman) _playerState.CurrentLifeState.OnValueChanged += OnLifeStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            TurnManager.OnGhostSkillUsedSequenced -= OnGhostSkillUsed;
            HideGhostPlaceholder();
            _ghostPlaceholder = null;
            _ghostPlaceholderRenderer = null;
            _ghostAnimator = null;
            StopAllCoroutines();
            _bindCoroutine = null;
            _flashCoroutine = _animEndCoroutine = _deathCoroutine = null;
            _presentationReady = _roleResolved = false;
            if (_registry != null) _registry.Registered -= OnBindingReady;
            _registry = null;
            if (_playerState != null)
            {
                _playerState.BindingReady -= OnBindingReady;
                _playerState.CurrentLifeState.OnValueChanged -= OnLifeStateChanged;
                FPSVisualController.ReleaseLocalHuman(_playerState);
            }
            _cosmeticController?.Clear();
            _cosmeticController = null;
            _appearanceBinding = null;
            ClearSeatMarker();
            _visualRoot = null;
            _animator = null;
            _isDead = _deathPresentationCompleted = _pendingGhostTransition = _isGhost = false;
            _characterVisible = true;
            base.OnNetworkDespawn();
        }

        void OnLifeStateChanged(LifeState prev, LifeState curr)
        {
            if (prev != LifeState.Ghost && curr == LifeState.Ghost
                && !_deathPresentationCompleted && _deathCoroutine == null)
            {
                _isDead = true;
                _pendingGhostTransition = true;
                return;
            }
            SyncGhostFromLifeState();
        }

        IEnumerator DeferredBindRoutine()
        {
            float identityDeadline = Time.realtimeSinceStartup + MatchCompositionRoot.InitializationTimeout;
            while (!_roleResolved)
            {
                if (!IsSpawned || MatchCompositionRoot.Instance == null) yield break;
                if (MatchCompositionRoot.Instance.InitializationFailure != null) yield break;
                TryResolveRole();
                if (_roleResolved) break;
                if (Time.realtimeSinceStartup >= identityDeadline)
                {
                    Debug.LogError("[PlayerVisual] Seat identity was not assigned before the initialization deadline");
                    yield break;
                }
                yield return null;
            }
            float elapsed = 0f;
            const float timeout = 5f;

            while (elapsed < timeout)
            {
                if (!IsSpawned || MatchCompositionRoot.Instance == null
                    || !MatchCompositionRoot.Instance.IsSessionCurrent) yield break;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DebugHoldPresentationBinding?.Invoke(_playerState) == true)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                    continue;
                }
#endif
                if (_localHuman)
                {
                    if (FPSVisualController.TryBindLocalHuman(_playerState))
                    {
                        _presentationReady = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        DebugPresentationBindCount++;
#endif
                        _bindCoroutine = null;
                        yield break;
                    }
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                    continue;
                }
                int visualSlot = ComputeVisualSlot();
                if (visualSlot >= 0)
                {
                    var views = MatchViewBindings.ForScene(gameObject.scene);
                    string slotName = views != null ? views.GetRemoteVisual(visualSlot)?.name : ResolveSlotName(visualSlot);
                    if (TryBindToSlot(slotName, visualSlot))
                    {
                        Debug.Log($"[PlayerVisual] Bound to {slotName} (slot={visualSlot}, seat={_playerState.PlayerIndex})");
                        SyncGhostFromLifeState(initialSnapshot: true);
                        _presentationReady = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        DebugPresentationBindCount++;
#endif
                        _bindCoroutine = null;
                        yield break;
                    }
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            Debug.LogError($"[PlayerVisual] Visual binding failed after {timeout}s — seat={_playerState?.PlayerIndex ?? -1}");
            _bindCoroutine = null;
        }

        int ComputeVisualSlot()
        {
            if (_playerState == null || !LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !perspective.TryGetBinding(_playerState.PlayerIndex, out var binding)
                || !ReferenceEquals(binding.State, _playerState)) return -1;
            return GetRemoteVisualSlot(binding.Identity.PlayerIndex, perspective.HumanSeat,
                MatchCompositionRoot.Instance.ActiveConfig.Mode == GameMode.Multi);
        }

        // Stable seats retain their visual slot even while other seats spawn/despawn.
        public static int GetRemoteVisualSlot(int seat, int localSeat, bool multi = true)
        {
            if (seat < 0 || seat >= 4 || localSeat < 0 || localSeat >= 4 || seat == localSeat)
                return -1;
            if (!multi) return seat < 2 && localSeat < 2 ? 0 : -1;
            // Local south -> next seat west -> opposite north -> previous seat east.
            // Use the fixed four-seat ring even after a departure; never compact holes.
            return (seat - localSeat + 4) % 4 - 1;
        }

        GameObject FindSceneVisual(string slotName)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                    if (candidate.name == slotName) return candidate.gameObject;
            return null;
        }

        string ResolveSlotName(int visualSlot)
        {
            string indexed = $"EnemyPlayer_{visualSlot}";
            if (FindSceneVisual(indexed) != null) return indexed;
            if (visualSlot == 0 && FindSceneVisual("EnemyPlayer") != null)
                return "EnemyPlayer";
            return indexed;
        }

        bool TryBindToSlot(string slotName, int visualSlot)
        {
            var views = MatchViewBindings.ForScene(gameObject.scene);
            var enemyGO = views != null ? views.GetRemoteVisual(visualSlot)?.gameObject : FindSceneVisual(slotName);
            if (enemyGO == null) return false;
            var claimed = enemyGO.GetComponent<PlayerSeatMarker>();
            if (claimed != null && claimed.Player != null && claimed.Player != _playerState)
                return false;
            if (MapCharacterLayout.TryFind(gameObject.scene, out var layout))
            {
                var mode = MatchCompositionRoot.Instance?.ActiveConfig?.Mode ?? GameMode.OneVsOne;
                if (!layout.TryApplyRemoteVisual(MapCharacterLayout.ModeFor(mode), ComputeVisualSlot(), enemyGO.transform))
                    return false;
            }
            enemyGO.SetActive(true);

            _visualRoot = enemyGO.transform;
            _animator = enemyGO.GetComponent<Animator>();
            if (_animator == null)
                _animator = enemyGO.GetComponentInChildren<Animator>();

            Debug.Log($"[PlayerVisual] {slotName} bound: animator={(_animator != null)}, controller={(_animator?.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "NONE")}");

            _spriteRenderers = enemyGO.GetComponentsInChildren<SpriteRenderer>(true);
            _cachedMaterials = new Material[_spriteRenderers.Length];
            for (int i = 0; i < _spriteRenderers.Length; i++)
                _cachedMaterials[i] = _spriteRenderers[i].material;

            Debug.Log($"[PlayerVisual] {slotName}: {_spriteRenderers.Length} sprite renderers found");

            var fanChild = _visualRoot.Find("fan") ?? _visualRoot.Find("Fan");
            if (fanChild != null)
                _fanRenderer = fanChild.GetComponent<SpriteRenderer>();

            _itemTransform = _visualRoot.Find("item");
            if (_itemTransform != null)
            {
                _itemRenderer = _itemTransform.GetComponent<SpriteRenderer>();
                _itemTransform.gameObject.SetActive(false);
            }

            BuildFreezeObject(_visualRoot);
            EnsureGhostPlaceholder();

            var iceBreakT = _visualRoot.Find("IceBreakEffect");
            if (iceBreakT != null)
                _iceBreakParticle = iceBreakT.GetComponent<ParticleSystem>();

            var finalBreakT = _visualRoot.Find("FinalBreakEffect");
            if (finalBreakT != null)
                _finalBreakParticle = finalBreakT.GetComponent<ParticleSystem>();

            Debug.Log($"[PlayerVisual] Particles: iceBreak={(_iceBreakParticle != null)}, finalBreak={(_finalBreakParticle != null)}");

            InitCosmeticController(_visualRoot);

            // Retained state may arrive before this visual binds; never rely solely on change events.
            ApplyRemoteCosmetic(_playerState.CosmeticDataNV.Value.ToString());

            SetupSeatMarker(enemyGO);

            return true;
        }

        void SetupSeatMarker(GameObject visualGO)
        {
            var marker = visualGO.GetComponent<PlayerSeatMarker>();
            if (marker == null)
                marker = visualGO.AddComponent<PlayerSeatMarker>();
            marker.SeatIndex = (byte)_playerState.PlayerIndex;
            marker.Player = _playerState;
            _targetViews = MatchViewBindings.ForScene(gameObject.scene);
            if (_registry != null && _registry.TryGetByPlayerIndex(marker.SeatIndex, out var binding))
                _targetViews?.RegisterTarget(binding, marker);

            var col = visualGO.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = visualGO.AddComponent<BoxCollider>();
                col.size = new Vector3(1.5f, 2f, 0.5f);
                col.center = new Vector3(0f, 1f, 0f);
            }
            col.enabled = true;
        }

        void ClearSeatMarker()
        {
            if (_visualRoot == null) return;
            var marker = _visualRoot.GetComponent<PlayerSeatMarker>();
            if (marker != null && marker.Player != _playerState) return;
            if (marker != null)
            {
                _targetViews?.ReleaseTarget(marker, _playerState);
                _targetViews = null;
                marker.Player = null;
                marker.SeatIndex = byte.MaxValue;
            }
            var col = _visualRoot.GetComponent<BoxCollider>();
            if (col != null)
                col.enabled = false;
            // The authored scene visual is not the despawned network object.
            // Hide only the visual still owned by this participant.
            _visualRoot.gameObject.SetActive(false);
        }

        void BuildFreezeObject(Transform visual)
        {
            var views = MatchViewBindings.ForScene(gameObject.scene);
            _freeze1 = views != null ? views.GetSprite(MatchSpriteRole.Freeze1) : Resources.Load<Sprite>("freeze1");
            _freeze2 = views != null ? views.GetSprite(MatchSpriteRole.Freeze2) : Resources.Load<Sprite>("freeze2");
            _freeze3 = views != null ? views.GetSprite(MatchSpriteRole.Freeze3) : Resources.Load<Sprite>("freeze3");
            if (_freeze1 == null) return;

            var existing = visual.Find("freezeice");
            if (existing != null)
            {
                _freezeRenderer = existing.GetComponent<SpriteRenderer>();
                existing.gameObject.SetActive(false);
                return;
            }

            var freezeGO = new GameObject("freezeice");
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

            BeginAnimation(triggerName);
            _animEndCoroutine = StartCoroutine(AnimEndRoutine(_animationVersion));
        }

        IEnumerator AnimEndRoutine(uint version)
        {
            yield return _waitAnimEnd;
            if (_animator != null && version == _animationVersion)
                _animator.SetTrigger("end");
            _animEndCoroutine = null;
        }

        public void SetWind(bool active)
        {
            if (_animator != null)
                _animator.SetBool(IsWindHash, active
                    && MatchCompositionRoot.Instance?.ActiveConfig?.Mode != GameMode.Multi);
        }

        public void PlayDamageFlash(bool preserveCombatAnimation = false)
        {
            Debug.Log("[PlayerVisual] PlayDamageFlash");
            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(DamageFlashRoutine(preserveCombatAnimation));
        }

        IEnumerator DamageFlashRoutine(bool preserveCombatAnimation)
        {
            uint version = _animationVersion;
            if (_animator != null && !preserveCombatAnimation)
            {
                BeginAnimation("damage");
                version = _animationVersion;
            }

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

            if (_animator != null && !preserveCombatAnimation && version == _animationVersion)
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
            tint.a = _ghostAlpha;

            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] != null && _spriteRenderers[i] != _ghostPlaceholderRenderer)
                    _spriteRenderers[i].color = tint;
            }

            if (_fanRenderer != null && _playerState.IsFanUpgraded.Value)
                _fanRenderer.color = new Color(0.4f, 0.6f, 1f, _ghostAlpha);

            if (_animator != null)
            {
                SetWind(_playerState.IsFanActive.Value);
                float degree = temp >= 20f ? 0f : temp >= 10f ? 1f : 2f;
                _animator.SetFloat(DegreeHash, degree);
            }

            if (_freezeRenderer != null && _freezeRenderer.gameObject.activeSelf && temp >= 37f)
                _freezeRenderer.gameObject.SetActive(false);
        }

        bool _endsMatch;

        void LateUpdate()
        {
            UpdateGhostGrowth();
            // The freeze clip also keys freezeice active state. Presentation owns
            // the break boundary, so the clip cannot uncover the body early.
            if (_deathCoroutine != null && _freezeRenderer != null)
                _freezeRenderer.gameObject.SetActive(_characterVisible);
            if (!_characterVisible)
            {
                SetCharacterRenderersVisible(false);
                if (_freezeRenderer != null) _freezeRenderer.gameObject.SetActive(false);
                if (_itemRenderer != null) _itemRenderer.gameObject.SetActive(false);
            }
        }

        public void PlayDeathSequence(bool endsMatch = false)
        {
            if (_deathCoroutine != null || _deathPresentationCompleted) return;
            ++_animationVersion;
            Debug.Log($"[PlayerVisual] PlayDeathSequence START (endsMatch={endsMatch})");
            _endsMatch = endsMatch;
            _deathSavedPos = _visualRoot != null ? _visualRoot.position : transform.position;

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
                SetFlashAmount(0f);
            }
            if (_animEndCoroutine != null)
            {
                StopCoroutine(_animEndCoroutine);
                _animEndCoroutine = null;
            }

            if (_animator != null)
            {
                foreach (var p in _animator.parameters)
                    if (p.type == AnimatorControllerParameterType.Trigger)
                        _animator.ResetTrigger(p.nameHash);
                _animator.Play("playerA_freeze 0", 0, 0f);
            }

            _isDead = true;
            _ghostAlpha = 1f;
            SetCharacterRenderersVisible(true);
            HideGhostPlaceholder();
            _deathCoroutine = StartCoroutine(DeathRoutine());
        }

        public Coroutine PlayDeathSequenceAndWait(bool endsMatch)
        {
            PlayDeathSequence(endsMatch);
            return _deathCoroutine;
        }

        public void SettleDeathPresentation()
        {
            if (_deathCoroutine != null)
            {
                StopCoroutine(_deathCoroutine);
                _deathCoroutine = null;
            }
            if (_freezeRenderer != null)
                _freezeRenderer.gameObject.SetActive(false);
            StopBreakParticles();

            if (_playerState != null
                && _playerState.CurrentLifeState.Value == LifeState.Ghost)
            {
                _isDead = true;
                ApplyGhostAppearanceAfterDeath();
            }
            _deathPresentationCompleted = true;
        }

        IEnumerator DeathRoutine()
        {
            yield return null;

            GameAudioManager.Instance?.PlayFreeze();

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

            GameAudioManager.Instance?.PlayIceBreak();
            CameraShake.Instance?.Shake(0.5f, 0.3f);
            PlayBreakParticles(_endsMatch);
            // Hide in the same frame as emission; never disable the visual root,
            // because its particles and eventual ghost must keep rendering.
            _ghostAlpha = 0f;
            SetCharacterRenderersVisible(false);
            if (_freezeRenderer != null)
                _freezeRenderer.gameObject.SetActive(false);
            if (_itemRenderer != null)
                _itemRenderer.gameObject.SetActive(false);

            yield return _waitGhostReveal;

            var mcr = MatchCompositionRoot.Instance;
            bool isMulti = mcr != null && mcr.ActiveConfig != null
                           && mcr.ActiveConfig.Mode == GameMode.Multi;

            if (isMulti || _endsMatch)
                ApplyGhostAppearanceAfterDeath();

            if (_animator != null)
                _animator.Play("Idle_Tree", 0, 0f);

            _deathPresentationCompleted = true;
            _deathCoroutine = null;
        }

        public void ReviveVisual()
        {
            if (!_isDead) return;
            ++_animationVersion;
            Debug.Log("[PlayerVisual] ReviveVisual");

            if (_deathCoroutine != null)
            {
                StopCoroutine(_deathCoroutine);
                _deathCoroutine = null;
            }

            if (_freezeRenderer != null)
                _freezeRenderer.gameObject.SetActive(false);

            StopBreakParticles();

            if (_visualRoot != null)
                _visualRoot.position = _deathSavedPos;

            if (_animator != null)
            {
                foreach (var p in _animator.parameters)
                    if (p.type == AnimatorControllerParameterType.Trigger)
                        _animator.ResetTrigger(p.nameHash);
                _animator.SetFloat(DegreeHash, 0f);
                _animator.Play("Idle_Tree", 0, 0f);
            }

            _isDead = false;
            _deathPresentationCompleted = false;
            _pendingGhostTransition = false;
            _isGhost = false;
            _ghostAlpha = 1f;
            SetCharacterRenderersVisible(true);
            HideGhostPlaceholder();
        }

        void SyncGhostFromLifeState(bool initialSnapshot = false)
        {
            if (_playerState == null) return;
            var mcr = MatchCompositionRoot.Instance;
            bool isMulti = mcr != null && mcr.ActiveConfig != null
                           && mcr.ActiveConfig.Mode == GameMode.Multi;
            if (!isMulti) return;

            bool shouldBeGhost = _playerState.CurrentLifeState.Value == LifeState.Ghost;

            if (shouldBeGhost)
            {
                if (_deathCoroutine != null) return;
                if (!initialSnapshot && !_deathPresentationCompleted)
                {
                    _isDead = true;
                    _pendingGhostTransition = true;
                    return;
                }
                _isDead = true;
                _isGhost = true;
                ShowGhostPlaceholder(animate: !initialSnapshot);
                _pendingGhostTransition = false;
            }
            else
            {
                _pendingGhostTransition = false;
                _isGhost = false;
                _ghostAlpha = 1f;
                SetCharacterRenderersVisible(true);
                HideGhostPlaceholder();
            }
        }

        void ApplyGhostAppearanceAfterDeath()
        {
            _pendingGhostTransition = false;
            _isGhost = true;
            ShowGhostPlaceholder();
        }

        void EnsureGhostPlaceholder()
        {
            if (_visualRoot == null || _ghostPlaceholder != null) return;
            var existing = _visualRoot.Find("GhostPlaceholder");
            _ghostPlaceholder = existing != null ? existing.gameObject : new GameObject("GhostPlaceholder");
            if (existing == null)
                _ghostPlaceholder.transform.SetParent(_visualRoot, false);

            // Keep the authored Ghost_Sprite curves relative to their own animator.
            // Size/seat placement belongs to the parent, preserving the sprite aspect ratio.
            _ghostPlaceholder.transform.localPosition = GhostRestPosition;
            _ghostPlaceholder.transform.localRotation = Quaternion.identity;
            _ghostPlaceholder.transform.localScale = Vector3.one * GhostVisualScale;
            var artwork = _ghostPlaceholder.transform.Find("Ghost_Sprite");
            if (artwork == null)
            {
                artwork = new GameObject("Ghost_Sprite").transform;
                artwork.SetParent(_ghostPlaceholder.transform, false);
            }
            _ghostPlaceholderRenderer = artwork.GetComponent<SpriteRenderer>();
            if (_ghostPlaceholderRenderer == null) _ghostPlaceholderRenderer = artwork.gameObject.AddComponent<SpriteRenderer>();
            _ghostPlaceholderRenderer.sprite = Resources.LoadAll<Sprite>("ghost").FirstOrDefault(sprite => sprite.name == "ghost_0");
            _ghostPlaceholderRenderer.color = Color.white;
            var bodyRenderer = _spriteRenderers?.FirstOrDefault(renderer => renderer != null && renderer != _ghostPlaceholderRenderer);
            if (bodyRenderer != null) _ghostPlaceholderRenderer.sharedMaterial = bodyRenderer.sharedMaterial;
            _ghostPlaceholderRenderer.sortingOrder = 85;
            _ghostAnimator = _ghostPlaceholder.GetComponent<Animator>();
            if (_ghostAnimator == null) _ghostAnimator = _ghostPlaceholder.AddComponent<Animator>();
            _ghostAnimator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("GhostA");
            _ghostAnimator.applyRootMotion = false;
            _ghostAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _ghostPlaceholder.SetActive(false);
        }

        void ShowGhostPlaceholder(bool animate = true)
        {
            EnsureGhostPlaceholder();
            _ghostAlpha = 0f;
            SetCharacterRenderersVisible(false);
            if (_ghostPlaceholder != null)
            {
                bool wasVisible = _ghostPlaceholder.activeSelf;
                _ghostPlaceholder.SetActive(true);
                _ghostPlaceholderRenderer.enabled = true;
                _ghostPlaceholderRenderer.color = Color.white;
                if (!wasVisible)
                {
                    CompleteGhostGrowth();
                    if (_ghostAnimator.runtimeAnimatorController != null)
                    {
                        _ghostAnimator.Play(GhostIdleState, 0, 0f);
                        _ghostAnimator.Update(0f);
                    }
                    if (animate && _ghostPlaceholderRenderer.sprite != null)
                    {
                        // Pin the idle sprite's bottom, preserving both axes equally.
                        // Freeze the authored bob until the emergence has completed.
                        _ghostBottomLocalY = _ghostPlaceholderRenderer.sprite.bounds.min.y;
                        _ghostAnimator.speed = 0f;
                        _ghostGrowStartedAt = Time.unscaledTime;
                        UpdateGhostGrowth();
                    }
                }
            }
        }

        void UpdateGhostGrowth()
        {
            if (_ghostGrowStartedAt < 0f || _ghostPlaceholder == null) return;
            if (!_ghostPlaceholder.activeInHierarchy)
            {
                CompleteGhostGrowth();
                return;
            }
            float t = Mathf.Clamp01((Time.unscaledTime - _ghostGrowStartedAt) / GhostGrowDuration);
            float scale = GhostVisualScale * Mathf.Lerp(0.05f, 1f, Mathf.SmoothStep(0f, 1f, t));
            _ghostPlaceholder.transform.localScale = Vector3.one * scale;
            _ghostPlaceholder.transform.localPosition = GhostRestPosition
                + Vector3.up * (_ghostBottomLocalY * (GhostVisualScale - scale));
            if (t >= 1f) CompleteGhostGrowth();
        }

        void CompleteGhostGrowth()
        {
            _ghostGrowStartedAt = -1f;
            if (_ghostPlaceholder != null)
            {
                _ghostPlaceholder.transform.localScale = Vector3.one * GhostVisualScale;
                _ghostPlaceholder.transform.localPosition = GhostRestPosition;
            }
            if (_ghostAnimator != null) _ghostAnimator.speed = 1f;
        }

        void HideGhostPlaceholder()
        {
            CompleteGhostGrowth();
            if (_ghostPlaceholder != null) _ghostPlaceholder.SetActive(false);
        }

        void OnGhostSkillUsed(byte ghostSeat, byte skillIndex, byte targetSeat, uint castId)
        {
            // Observe the accepted, sequenced skill event for this logical seat only.
            // The local human uses the first-person view and has no world avatar.
            if (!_isGhost || !IsPresentationReady || IsLocalHuman || !isActiveAndEnabled
                || _playerState.PlayerIndex != ghostSeat || _ghostAnimator == null
                || !_ghostAnimator.isActiveAndEnabled || _ghostAnimator.runtimeAnimatorController == null) return;
            // An immediately accepted skill must not wait for a cosmetic reveal.
            CompleteGhostGrowth();
            _ghostAnimator.Play(GhostAttackState, 0, 0f);
        }

        void SetCharacterRenderersVisible(bool visible)
        {
            _characterVisible = visible;
            var atlas = _visualRoot != null ? _visualRoot.GetComponent<CosmeticAtlasRenderer>() : null;
            if (atlas != null) atlas.LayersVisible = visible;
            if (_spriteRenderers == null) return;
            foreach (var renderer in _spriteRenderers)
            {
                if (renderer == null || renderer == _ghostPlaceholderRenderer
                    || renderer == _freezeRenderer || renderer == _itemRenderer)
                    continue;
                renderer.enabled = visible;
            }
        }

        void PlayBreakParticles(bool heavy = false)
        {
            if (_iceBreakParticle != null)
            {
                _iceBreakParticle.gameObject.SetActive(true);
                _iceBreakParticle.Play();
            }
            if (heavy && _finalBreakParticle != null)
            {
                _finalBreakParticle.gameObject.SetActive(true);
                _finalBreakParticle.Play();
            }
        }

        void StopBreakParticles()
        {
            if (_iceBreakParticle != null)
            {
                _iceBreakParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _iceBreakParticle.gameObject.SetActive(false);
            }
            if (_finalBreakParticle != null)
            {
                _finalBreakParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _finalBreakParticle.gameObject.SetActive(false);
            }
        }

        public void SetItemSprite(Sprite sprite)
        {
            if (_itemRenderer == null) return;
            TakeItemSpriteOwnership();
            _itemRenderer.sprite = sprite;
            WorldInteractionLayer.ApplyHeld(_itemRenderer);
        }

        void TakeItemSpriteOwnership()
        {
            // The authored prop resolver selects a tea cup whenever animation
            // enables this object. Combat owns the prop image; body resolvers remain active.
            var resolver = _itemRenderer != null ? _itemRenderer.GetComponent<SpriteResolver>() : null;
            if (resolver != null) resolver.enabled = false;
        }

        public void PrepareDefenseSprite(Sprite sprite)
        {
            SetItemSprite(sprite);
            if (_itemTransform != null)
                _itemTransform.gameObject.SetActive(sprite != null);
        }

        public void ClearItemSprite()
        {
            TakeItemSpriteOwnership();
            if (_itemRenderer != null)
                _itemRenderer.sprite = null;
            if (_itemTransform != null)
                _itemTransform.gameObject.SetActive(false);
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

            BeginAnimation(resolved);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugLastCombatTrigger = resolved;
            DebugCombatAnimationCount++;
#endif
        }

        bool HasParameter(string paramName)
        {
            if (_animator == null) return false;
            foreach (var p in _animator.parameters)
                if (p.name == paramName) return true;
            return false;
        }

        void ResetAnimationTriggers()
        {
            foreach (var p in _animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger)
                    _animator.ResetTrigger(p.nameHash);
        }

        void BeginAnimation(string trigger)
        {
            ++_animationVersion;
            ResetAnimationTriggers();
            // Authored actions leave only through "end". Start from their entry
            // pose so a previous damage/action state cannot queue this action behind it.
            if (_animator.HasState(0, Animator.StringToHash("Idle_Tree")))
            {
                _animator.Play("Idle_Tree", 0, 0f);
                _animator.Update(0f);
            }
            _animator.SetTrigger(trigger);
        }

        public void ReturnToIdle()
        {
            ++_animationVersion;
            Debug.Log("[PlayerVisual] ReturnToIdle");
            if (_animEndCoroutine != null)
            {
                StopCoroutine(_animEndCoroutine);
                _animEndCoroutine = null;
            }
            if (_animator != null)
            {
                ResetAnimationTriggers();
                _animator.SetTrigger("end");
            }
            ClearItemSprite();
        }

        public Animator GetAnimator() => _animator;

        public Transform GetVisualRoot() => _visualRoot;

        public Vector3 GetVisualPosition() =>
            _visualRoot != null ? _visualRoot.position : transform.position;

        void InitCosmeticController(Transform root)
        {
            _cosmeticController?.Clear();
            _cosmeticController = new CosmeticVisualController();
            CharacterAppearanceSnapshot profile = null;
            if (LocalMatchPerspective.TryResolveCurrent(out var perspective)
                && perspective.TryGetBinding(_playerState.PlayerIndex, out var binding)
                && ReferenceEquals(binding.State, _playerState))
            {
                _appearanceBinding = binding;
                var match = MatchCompositionRoot.Instance;
                var launch = AppBootstrapper.Instance?.SessionRouter.LaunchContext;
                if (_playerState.IsBotControlled && match?.ActiveConfig?.Mode == GameMode.Solo
                    && launch?.Mode == GameMode.Solo && binding.Participant != null
                    && match.Generation == binding.Participant.Generation
                    && launch.Generation == binding.Participant.Generation)
                    profile = launch.Solo?.Appearance;
            }
            _cosmeticController.BindCharacter(root, CosmeticView.Character, profile);
            _cosmeticController.IsCurrent = IsAppearanceCurrent;
            _cosmeticController.Synchronize = SynchronizeAppearance;
            root.GetComponent<CosmeticAtlasRenderer>().Reactivated = ResumeAppearance;
        }

        bool IsAppearanceCurrent() => IsSpawned && isActiveAndEnabled && _visualRoot != null
            && _appearanceBinding?.IsValid == true && LocalMatchPerspective.TryResolveCurrent(out var perspective)
            && perspective.TryGetBinding(_playerState.PlayerIndex, out var current)
            && ReferenceEquals(current, _appearanceBinding);

        void SynchronizeAppearance()
        {
            RefreshRendererCache();
            // A cosmetic refresh during the half-second gap must not reveal the body.
            SetCharacterRenderersVisible(_characterVisible);
            if (_playerState == null || _spriteRenderers == null) return;
            var tint = Color.Lerp(new Color(.7f, .85f, 1f), Color.white, Mathf.Clamp01(_playerState.Temperature.Value / 37f));
            tint.a = _ghostAlpha;
            foreach (var renderer in _spriteRenderers)
                if (renderer != null && renderer != _ghostPlaceholderRenderer) renderer.color = tint;
        }

        void OnDisable() => _cosmeticController?.Suspend();
        void OnEnable()
        {
            ResumeAppearance();
        }

        void ResumeAppearance()
        {
            if (!IsSpawned || _cosmeticController == null) return;
            if (!IsAppearanceCurrent()) { _cosmeticController.Clear(); _cosmeticController = null; return; }
            _cosmeticController.Resume();
            ApplyRemoteCosmetic(_playerState.CosmeticDataNV.Value.ToString());
        }

        public void ApplyRemoteCosmetic(string json)
        {
            // Before binding, the retained NetworkVariable is the source of truth.
            if (_cosmeticController == null) return;

            var service = CosmeticProfileService.Instance;
            if (service != null && service.Registry != null)
            {
                var report = _cosmeticController.ApplyFromDto(json, service.Registry);
                if (!report.Success && isActiveAndEnabled)
                    Debug.LogWarning($"[Appearance] seat={_playerState.PlayerIndex} stage={report.Stage}: {report.Error}");
            }
        }

        void RefreshRendererCache()
        {
            if (_visualRoot == null) return;
            _spriteRenderers = _visualRoot.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(r => r.name != "_cosmetic_atlas_layer" || r.gameObject.activeSelf).ToArray();
            _cachedMaterials = new Material[_spriteRenderers.Length];
            for (int i = 0; i < _spriteRenderers.Length; i++)
                _cachedMaterials[i] = _spriteRenderers[i].name == "_cosmetic_atlas_layer"
                    ? _spriteRenderers[i].sharedMaterial : _spriteRenderers[i].material;
        }
    }
}
