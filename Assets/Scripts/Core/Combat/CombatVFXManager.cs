using System.Collections;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    public class CombatVFXManager : MonoBehaviour
    {
        public static CombatVFXManager Instance { get; private set; }

        [SerializeField] GameObject _hitEffectPrefab;
        [SerializeField] GameObject _iceBreakEffectPrefab;
        [SerializeField] GameObject _finalBreakEffectPrefab;

        static readonly WaitForSeconds _waitIntro = new(0.5f);
        static readonly WaitForSeconds _waitBriefPause = new(0.3f);
        static readonly WaitForSeconds _waitDamageReact = new(0.7f);
        static readonly WaitForSeconds _waitDeathSequence = new(1.6f);

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        void Start()
        {
            TurnManager.OnCombatResult += OnCombatResult;
        }

        void OnDestroy()
        {
            TurnManager.OnCombatResult -= OnCombatResult;
            if (Instance == this) Instance = null;
        }

        void OnCombatResult(CombatResultData result)
        {
            Debug.Log($"[CombatVFX] OnCombatResult received — winner={result.WinnerIndex}, firstIdx={result.FirstPlayerIndex}, P1Main={result.P1MainItemId}, P2Main={result.P2MainItemId}");
            StartCoroutine(PlayCombatVFXSequence(result));
        }

        IEnumerator PlayCombatVFXSequence(CombatResultData result)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) yield break;

            int firstIdx = result.FirstPlayerIndex;
            int secondIdx = 1 - firstIdx;

            short firstItemId = firstIdx == 0 ? result.P1MainItemId : result.P2MainItemId;
            short secondItemId = secondIdx == 0 ? result.P1MainItemId : result.P2MainItemId;

            int deadIdx = result.WinnerIndex >= 0 ? 1 - result.WinnerIndex : -1;
            bool firstActionKilled = result.WinnerIndex >= 0
                && result.EventCount == 1
                && result.Event0Source == (byte)firstIdx;

            yield return _waitIntro;

            Debug.Log($"[CombatVFX] Sequence: first=P{firstIdx}(item={firstItemId}), second=P{secondIdx}(item={secondItemId}), deadIdx={deadIdx}");

            if (firstItemId >= 0)
            {
                Debug.Log($"[CombatVFX] Playing FIRST item sequence: P{firstIdx} item={firstItemId}");
                yield return StartCoroutine(PlayItemSequence(firstIdx, firstItemId, nm));
            }

            if (firstActionKilled && deadIdx >= 0)
            {
                Debug.Log($"[CombatVFX] First action killed P{deadIdx} — playing death sequence");
                var deadVisual = GetPlayerVisual(deadIdx, nm);
                if (deadVisual != null) deadVisual.PlayDeathSequence();
                yield return _waitDeathSequence;
                yield break;
            }

            if (firstItemId >= 0 && secondItemId >= 0)
                yield return _waitBriefPause;

            if (secondItemId >= 0)
            {
                Debug.Log($"[CombatVFX] Playing SECOND item sequence: P{secondIdx} item={secondItemId}");
                yield return StartCoroutine(PlayItemSequence(secondIdx, secondItemId, nm));
            }

            if (!firstActionKilled && deadIdx >= 0)
            {
                Debug.Log($"[CombatVFX] Second action killed P{deadIdx} — playing death sequence");
                var deadVisual = GetPlayerVisual(deadIdx, nm);
                if (deadVisual != null) deadVisual.PlayDeathSequence();
                yield return _waitDeathSequence;
            }
        }

        IEnumerator PlayItemSequence(int userIdx, short itemId, NetworkManager nm)
        {
            var itemData = ItemManager.Instance?.GetItemData(itemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[CombatVFX] PlayItemSequence: itemData NULL for itemId={itemId}");
                yield break;
            }

            int targetIdx = 1 - userIdx;
            var userVisual = GetPlayerVisual(userIdx, nm);
            var targetVisual = GetPlayerVisual(targetIdx, nm);

            bool hasUserAnim = userVisual != null && !string.IsNullOrEmpty(itemData.AnimTrigger);
            bool isAttack = itemData.Category == ItemCategory.Attack;
            bool isRecovery = itemData.Category == ItemCategory.Recovery;

            Debug.Log($"[CombatVFX] PlayItemSequence: P{userIdx} '{itemData.ItemName}' trigger='{itemData.AnimTrigger}' cat={itemData.Category} hasAnim={hasUserAnim} userVisual={userVisual != null} targetVisual={targetVisual != null}");
            Debug.Log($"[CombatVFX]   effectDelay={itemData.EffectDelay} effectHitCount={itemData.EffectHitCount} effectInterval={itemData.EffectInterval} animDuration={itemData.AnimDuration} oppTrigger='{itemData.OpponentAnimTrigger}'");

            if (hasUserAnim)
            {
                Debug.Log($"[CombatVFX] → userVisual.PlayCombatAnimation('{itemData.AnimTrigger}')");
                userVisual.PlayCombatAnimation(itemData.AnimTrigger);

                bool isLocalUser = (int)nm.LocalClientId == userIdx;
                if (isLocalUser)
                {
                    var fps = FPSVisualController.Instance;
                    Debug.Log($"[CombatVFX] → FPS isLocalUser=true, FPSInstance={fps != null}");
                    if (fps != null) fps.PlayFPSAnimation(itemData.AnimTrigger);
                }

                float animLen = GetAnimDuration(itemData, userVisual.GetAnimator());

                if (itemData.EffectHitCount > 0 && itemData.EffectDelay > 0f)
                {
                    yield return new WaitForSeconds(itemData.EffectDelay);
                    float remaining = animLen - itemData.EffectDelay;

                    for (int h = 0; h < itemData.EffectHitCount; h++)
                    {
                        if (isAttack && targetVisual != null)
                        {
                            targetVisual.PlayDamageFlash();
                            PlayHitAt(GetPlayerWorldPos(targetIdx));
                        }
                        else if (isRecovery && userVisual != null)
                        {
                            PlayHitAt(GetPlayerWorldPos(userIdx));
                        }

                        if (h < itemData.EffectHitCount - 1 && itemData.EffectInterval > 0f)
                        {
                            yield return new WaitForSeconds(itemData.EffectInterval);
                            remaining -= itemData.EffectInterval;
                        }
                    }

                    if (remaining > 0f)
                        yield return new WaitForSeconds(remaining);
                }
                else
                {
                    yield return new WaitForSeconds(animLen);
                }

                Debug.Log($"[CombatVFX] → userVisual.ReturnToIdle()");
                userVisual.ReturnToIdle();
                if (isLocalUser)
                {
                    var fps = FPSVisualController.Instance;
                    if (fps != null) fps.ReturnToIdle();
                }
            }
            else if ((isAttack || isRecovery) && itemData.EffectHitCount > 0)
            {
                if (isAttack && targetVisual != null)
                {
                    targetVisual.PlayDamageFlash();
                    PlayHitAt(GetPlayerWorldPos(targetIdx));
                }
                else if (isRecovery)
                {
                    PlayHitAt(GetPlayerWorldPos(userIdx));
                }
                yield return _waitDamageReact;
            }

            if (!string.IsNullOrEmpty(itemData.OpponentAnimTrigger) && targetVisual != null)
            {
                Debug.Log($"[CombatVFX] → opponent anim: targetVisual.PlayCombatAnimation('{itemData.OpponentAnimTrigger}')");
                targetVisual.PlayCombatAnimation(itemData.OpponentAnimTrigger);
                float oppLen = GetAnimDuration(itemData, targetVisual.GetAnimator());
                yield return new WaitForSeconds(oppLen);
                targetVisual.ReturnToIdle();
            }

            Debug.Log($"[CombatVFX] PlayItemSequence DONE: P{userIdx} '{itemData.ItemName}'");
        }

        float GetAnimDuration(ItemDataSO itemData, Animator animator)
        {
            if (itemData.AnimDuration > 0f) return itemData.AnimDuration;
            if (animator == null) return 0.8f;
            animator.Update(0f);
            var info = animator.GetCurrentAnimatorStateInfo(0);
            return info.length > 0f ? info.length : 0.8f;
        }

        AZPlayerVisual GetPlayerVisual(int playerIndex, NetworkManager nm)
        {
            foreach (var kvp in nm.SpawnManager.SpawnedObjects)
            {
                var netObj = kvp.Value;
                if (netObj == null) continue;
                if (netObj.IsPlayerObject && (int)netObj.OwnerClientId == playerIndex)
                    return netObj.GetComponent<AZPlayerVisual>();
            }
            return null;
        }

        public void PlayHitAt(Vector3 pos)
        {
            Debug.Log($"[CombatVFX] PlayHitAt({pos}) — prefab={(_hitEffectPrefab != null)}");
            SpawnParticle(_hitEffectPrefab, pos);
        }

        public void PlayIceBreakAt(Vector3 pos)
        {
            Debug.Log($"[CombatVFX] PlayIceBreakAt({pos}) — prefab={(_iceBreakEffectPrefab != null)}");
            SpawnParticle(_iceBreakEffectPrefab, pos);
        }

        public void PlayFinalBreakAt(Vector3 pos)
        {
            Debug.Log($"[CombatVFX] PlayFinalBreakAt({pos}) — prefab={(_finalBreakEffectPrefab != null)}");
            SpawnParticle(_finalBreakEffectPrefab, pos);
        }

        void SpawnParticle(GameObject prefab, Vector3 pos)
        {
            if (prefab == null) return;
            var go = Instantiate(prefab, pos, Quaternion.identity);
            foreach (var psr in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                psr.sortingLayerName = "Default";
                psr.sortingOrder = 100;
                if (psr.sharedMaterial != null)
                {
                    psr.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                    psr.material.SetInt("_ZWrite", 0);
                }
            }
            Destroy(go, 3f);
        }

        Vector3 GetPlayerWorldPos(int playerIndex)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return Vector3.zero;

            foreach (var kvp in nm.SpawnManager.SpawnedObjects)
            {
                var netObj = kvp.Value;
                if (netObj == null) continue;
                if (netObj.IsPlayerObject && (int)netObj.OwnerClientId == playerIndex)
                {
                    var visual = netObj.GetComponent<AZPlayerVisual>();
                    if (visual != null)
                        return visual.GetVisualPosition();
                    return netObj.transform.position;
                }
            }

            var sp = GameObject.Find(playerIndex == 0 ? "SpawnPoint_1" : "SpawnPoint_2");
            return sp != null ? sp.transform.position + Vector3.up * 1.5f : Vector3.zero;
        }
    }
}
