using System.Collections;
using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    public class EnvironmentVFXManager : MonoBehaviour
    {
        public static EnvironmentVFXManager Instance { get; private set; }

        const float RISE_DURATION = 0.6f;
        const float SINK_DURATION = 0.5f;
        const float SLIDE_DURATION = 0.8f;
        const float RISE_OFFSET = 2f;
        const float SLIDE_OFFSET = 8f;

        public const float STEAL_STAGING_DURATION = 3.2f;
        public const float BLANKET_STAGING_DURATION = 3f;

        static readonly WaitForSeconds _waitStealPause = new(0.4f);
        static readonly WaitForSeconds _waitBlanketHold = new(0.6f);

        GameObject _kidGroup;
        GameObject _ambulanceGroup;
        GameObject _coolerObj;
        GameObject _teaObj;
        GameObject _catObj;
        Light _directionalLight;

        Color _defaultLightColor;
        float _defaultLightIntensity;
        EnvironmentType _currentEnv;

        Vector3[] _kidLocalPositions;
        Vector3 _ambulanceLocalPosition;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        void Start()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    _directionalLight = light;
                    _defaultLightColor = light.color;
                    _defaultLightIntensity = light.intensity;
                    break;
                }
            }

            BuildKidGroup();
            BuildAmbulanceGroup();
            BuildCooler();
            BuildTea();
            BuildCat();

            TurnManager.OnEnvironmentAnnounced += OnEnvironmentAnnounced;
        }

        void OnDestroy()
        {
            TurnManager.OnEnvironmentAnnounced -= OnEnvironmentAnnounced;
            if (Instance == this) Instance = null;
        }

        void OnEnvironmentAnnounced(EnvironmentType env)
        {
            Debug.Log($"[EnvVFX] OnEnvironmentAnnounced: {env}");
            ResetVisuals();
            _currentEnv = env;

            switch (env)
            {
                case EnvironmentType.SunnyDay:
                    StartCoroutine(LerpLight(new Color(1f, 0.95f, 0.7f), 3f));
                    if (_coolerObj != null) _coolerObj.SetActive(true);
                    if (_teaObj != null) _teaObj.SetActive(true);
                    break;
                case EnvironmentType.HeatWaveWarning:
                    StartCoroutine(LerpLight(new Color(1f, 0.6f, 0.5f), 2.5f));
                    break;
                case EnvironmentType.CoolBreeze:
                    StartCoroutine(LerpLight(new Color(0.8f, 0.9f, 1f), 1.8f));
                    if (_coolerObj != null) _coolerObj.SetActive(true);
                    break;
                case EnvironmentType.Kids:
                    StartCoroutine(KidsRiseRoutine());
                    if (_catObj != null) _catObj.SetActive(true);
                    break;
                case EnvironmentType.Ambulance:
                    StartCoroutine(AmbulanceSlideRoutine());
                    break;
            }
        }

        void ResetVisuals()
        {
            StopAllCoroutines();

            if (_directionalLight != null)
            {
                _directionalLight.color = _defaultLightColor;
                _directionalLight.intensity = _defaultLightIntensity;
            }

            if (_kidGroup != null) _kidGroup.SetActive(false);
            if (_ambulanceGroup != null) _ambulanceGroup.SetActive(false);
            if (_coolerObj != null) _coolerObj.SetActive(false);
            if (_teaObj != null) _teaObj.SetActive(false);
            if (_catObj != null) _catObj.SetActive(false);

            _currentEnv = EnvironmentType.None;
        }

        IEnumerator LerpLight(Color targetColor, float targetIntensity)
        {
            if (_directionalLight == null) yield break;

            Color startColor = _directionalLight.color;
            float startIntensity = _directionalLight.intensity;
            const float duration = 1.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                _directionalLight.color = Color.Lerp(startColor, targetColor, t);
                _directionalLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
                yield return null;
            }

            _directionalLight.color = targetColor;
            _directionalLight.intensity = targetIntensity;
        }

        IEnumerator KidsRiseRoutine()
        {
            Debug.Log($"[EnvVFX] KidsRiseRoutine START — kidGroup={(_kidGroup != null)}, positions={(_kidLocalPositions != null ? _kidLocalPositions.Length : 0)}");
            if (_kidGroup == null || _kidLocalPositions == null) yield break;
            _kidGroup.SetActive(true);

            int count = Mathf.Min(_kidGroup.transform.childCount, _kidLocalPositions.Length);
            for (int i = 0; i < count; i++)
                _kidGroup.transform.GetChild(i).localPosition = _kidLocalPositions[i] + Vector3.down * RISE_OFFSET;

            float t = 0f;
            while (t < RISE_DURATION)
            {
                t += Time.deltaTime;
                float p = 1f - (1f - Mathf.Clamp01(t / RISE_DURATION));
                float eased = 1f - (1f - p) * (1f - p);
                for (int i = 0; i < count; i++)
                    _kidGroup.transform.GetChild(i).localPosition =
                        Vector3.Lerp(_kidLocalPositions[i] + Vector3.down * RISE_OFFSET, _kidLocalPositions[i], eased);
                yield return null;
            }

            for (int i = 0; i < count; i++)
                _kidGroup.transform.GetChild(i).localPosition = _kidLocalPositions[i];
        }

        IEnumerator AmbulanceSlideRoutine()
        {
            Debug.Log($"[EnvVFX] AmbulanceSlideRoutine START — ambulanceGroup={(_ambulanceGroup != null)}, children={(_ambulanceGroup != null ? _ambulanceGroup.transform.childCount : 0)}");
            if (_ambulanceGroup == null || _ambulanceGroup.transform.childCount == 0) yield break;
            _ambulanceGroup.SetActive(true);

            var rescue = _ambulanceGroup.transform.GetChild(0);
            Vector3 start = _ambulanceLocalPosition + Vector3.left * SLIDE_OFFSET;
            rescue.localPosition = start;

            float t = 0f;
            while (t < SLIDE_DURATION)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / SLIDE_DURATION);
                float eased = 1f - (1f - p) * (1f - p);
                rescue.localPosition = Vector3.Lerp(start, _ambulanceLocalPosition, eased);
                yield return null;
            }
            rescue.localPosition = _ambulanceLocalPosition;
        }

        public void PlayKidsStealStaging()
        {
            Debug.Log($"[EnvVFX] PlayKidsStealStaging — kidGroup={(_kidGroup != null)}");
            if (_kidGroup == null) return;
            StartCoroutine(KidsStealRoutine());
        }

        IEnumerator KidsStealRoutine()
        {
            int count = _kidGroup.transform.childCount;
            Debug.Log($"[EnvVFX] KidsStealRoutine START — children={count}");
            if (count == 0) yield break;

            var leftKid = _kidGroup.transform.GetChild(0);
            var kidAnim = leftKid.GetComponent<Animator>();
            Debug.Log($"[EnvVFX] KidsSteal: kidAnim={kidAnim != null}, controller={((kidAnim != null && kidAnim.runtimeAnimatorController != null) ? kidAnim.runtimeAnimatorController.name : "NONE")}");
            Vector3 leftStart = leftKid.localPosition;

            yield return StartCoroutine(SinkTransform(leftKid, leftStart, SINK_DURATION));

            var oppSpawnRoot = GameObject.Find("OppItemSpawnRoot");
            Vector3 stealPos = oppSpawnRoot != null
                ? oppSpawnRoot.transform.position + new Vector3(1.5f, 0f, 0.5f)
                : new Vector3(1.5f, 0.5f, 7f);

            leftKid.position = stealPos + Vector3.down * RISE_OFFSET;
            leftKid.gameObject.SetActive(true);
            if (kidAnim != null) { kidAnim.SetTrigger("ready"); Debug.Log("[EnvVFX] KidsSteal: trigger 'ready'"); }
            yield return StartCoroutine(RiseTransform(leftKid, stealPos, RISE_DURATION));

            if (kidAnim != null) { kidAnim.SetTrigger("steal"); Debug.Log("[EnvVFX] KidsSteal: trigger 'steal'"); }
            GameAudioManager.Instance?.PlayKidSteal();
            yield return _waitStealPause;

            yield return StartCoroutine(SinkTransform(leftKid, stealPos, SINK_DURATION));

            leftKid.localPosition = leftStart;
            leftKid.gameObject.SetActive(count > 0);
            if (kidAnim != null) { kidAnim.SetTrigger("skew"); Debug.Log("[EnvVFX] KidsSteal: trigger 'skew'"); }
            Debug.Log("[EnvVFX] KidsStealRoutine DONE");
        }

        public void PlayAmbulanceBlanketStaging(bool healSelf)
        {
            Debug.Log($"[EnvVFX] PlayAmbulanceBlanketStaging healSelf={healSelf}");
            StartCoroutine(AmbulanceBlanketRoutine(healSelf));
        }

        IEnumerator AmbulanceBlanketRoutine(bool healSelf)
        {
            GameObject blanket = null;

            if (healSelf)
            {
                blanket = CreateBlanketOverlay();
                if (blanket != null)
                {
                    var canvasGroup = blanket.GetComponent<CanvasGroup>();
                    yield return StartCoroutine(FadeCanvasGroup(canvasGroup, 0f, 0.7f, 0.5f));
                    yield return _waitBlanketHold;
                    yield return StartCoroutine(FadeCanvasGroup(canvasGroup, 0.7f, 0f, 1f));
                    Destroy(blanket);
                }
            }
            else
            {
                if (_ambulanceGroup != null && _ambulanceGroup.transform.childCount > 0)
                {
                    var rescue = _ambulanceGroup.transform.GetChild(0);
                    var oppSpawnRoot = GameObject.Find("EnemyPlayer");
                    if (oppSpawnRoot != null)
                    {
                        Vector3 targetPos = oppSpawnRoot.transform.position + new Vector3(1f, 0f, 0f);
                        Vector3 startPos = targetPos + Vector3.down * RISE_OFFSET;
                        rescue.position = startPos;
                        rescue.gameObject.SetActive(true);
                        yield return StartCoroutine(RiseTransform(rescue, targetPos, RISE_DURATION));

                        var rescueAnim = rescue.GetComponent<Animator>();
                        if (rescueAnim != null)
                        {
                            rescueAnim.SetTrigger("complete");
                            Debug.Log($"[EnvVFX] AmbulanceBlanket: rescue trigger 'complete', controller={((rescueAnim.runtimeAnimatorController != null) ? rescueAnim.runtimeAnimatorController.name : "NONE")}");
                        }
                    }
                }

                yield return _waitBlanketHold;

                if (_ambulanceGroup != null && _ambulanceGroup.transform.childCount > 0)
                {
                    var rescue = _ambulanceGroup.transform.GetChild(0);
                    yield return StartCoroutine(SinkTransform(rescue, rescue.position, SINK_DURATION));
                    rescue.localPosition = _ambulanceLocalPosition;
                }
            }
        }

        GameObject CreateBlanketOverlay()
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return null;

            var go = new GameObject("BlanketOverlay");
            go.transform.SetParent(canvas.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.85f, 0.75f, 0.6f, 0f);
            img.raycastTarget = false;

            go.AddComponent<CanvasGroup>();
            return go;
        }

        IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            cg.alpha = to;
        }

        IEnumerator RiseTransform(Transform target, Vector3 endPos, float duration)
        {
            Vector3 startPos = target.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float eased = 1f - (1f - Mathf.Clamp01(t / duration)) * (1f - Mathf.Clamp01(t / duration));
                target.position = Vector3.Lerp(startPos, endPos, eased);
                yield return null;
            }
            target.position = endPos;
        }

        IEnumerator SinkTransform(Transform target, Vector3 startPos, float duration)
        {
            Vector3 endPos = startPos + Vector3.down * RISE_OFFSET;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = p * p;
                target.position = Vector3.Lerp(startPos, endPos, eased);
                yield return null;
            }
            target.position = endPos;
            target.gameObject.SetActive(false);
        }

        void BuildKidGroup()
        {
            _kidGroup = new GameObject("KidVFX");
            _kidGroup.transform.SetParent(transform);

            var litMat = Resources.Load<Material>("sprite3DMat");
            var idleSprite = Resources.Load<Sprite>("Environment/kid_idle");
            var kidCtrl = Resources.Load<RuntimeAnimatorController>("Environment/kidA");

            Debug.Log($"[EnvVFX] BuildKidGroup: sprite={idleSprite != null}, ctrl={kidCtrl != null}, mat={litMat != null}");
            if (idleSprite == null) { Debug.LogWarning("[EnvVFX] BuildKidGroup: kid_idle sprite MISSING"); return; }

            Vector3[] positions = { new(-3f, 0.5f, 6f), new(3.5f, 0.5f, 7f) };
            _kidLocalPositions = new Vector3[positions.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                var go = new GameObject($"Kid_{i}");
                go.transform.SetParent(_kidGroup.transform);
                go.transform.position = positions[i];
                _kidLocalPositions[i] = go.transform.localPosition;
                if (i == 1) go.transform.localScale = new Vector3(-1f, 1f, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = idleSprite;
                sr.sortingOrder = 2;
                if (litMat != null) sr.material = litMat;

                if (kidCtrl != null)
                {
                    var anim = go.AddComponent<Animator>();
                    anim.runtimeAnimatorController = kidCtrl;
                    anim.applyRootMotion = false;
                }
            }

            _kidGroup.SetActive(false);
        }

        void BuildAmbulanceGroup()
        {
            _ambulanceGroup = new GameObject("AmbulanceVFX");
            _ambulanceGroup.transform.SetParent(transform);

            var litMat = Resources.Load<Material>("sprite3DMat");
            var readySprite = Resources.Load<Sprite>("Environment/rescue_ready");
            var rescueCtrl = Resources.Load<RuntimeAnimatorController>("Environment/rescueA");

            Debug.Log($"[EnvVFX] BuildAmbulanceGroup: sprite={readySprite != null}, ctrl={rescueCtrl != null}, mat={litMat != null}");
            if (readySprite == null) { Debug.LogWarning("[EnvVFX] BuildAmbulanceGroup: rescue_ready sprite MISSING"); return; }

            var go = new GameObject("Rescue");
            go.transform.SetParent(_ambulanceGroup.transform);
            go.transform.position = new Vector3(4f, 0.5f, 8f);
            _ambulanceLocalPosition = go.transform.localPosition;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = readySprite;
            sr.sortingOrder = 1;
            if (litMat != null) sr.material = litMat;

            if (rescueCtrl != null)
            {
                var anim = go.AddComponent<Animator>();
                anim.runtimeAnimatorController = rescueCtrl;
                anim.applyRootMotion = false;
            }

            _ambulanceGroup.SetActive(false);
        }

        void BuildCooler()
        {
            var coolerCtrl = Resources.Load<RuntimeAnimatorController>("coolerA");
            var coolerSprite = GameSprites.Get(GameSprites.ITEM_FAN);
            var litMat = Resources.Load<Material>("sprite3DMat");

            _coolerObj = new GameObject("Cooler");
            _coolerObj.transform.SetParent(transform);
            _coolerObj.transform.position = new Vector3(-0.19f, 1.39f, -4.18f);
            _coolerObj.transform.localScale = new Vector3(4f, 4f, 1f);
            _coolerObj.transform.rotation = Quaternion.Euler(78.3f, 0f, -206.6f);

            var sr = _coolerObj.AddComponent<SpriteRenderer>();
            if (coolerSprite != null) sr.sprite = coolerSprite;
            sr.sortingOrder = 2;
            if (litMat != null) sr.material = litMat;

            if (coolerCtrl != null)
            {
                var anim = _coolerObj.AddComponent<Animator>();
                anim.runtimeAnimatorController = coolerCtrl;
            }

            _coolerObj.SetActive(false);
        }

        void BuildTea()
        {
            var teaCtrl = Resources.Load<RuntimeAnimatorController>("teaA");
            var teaSprite = GameSprites.Get(GameSprites.ITEM_TEA);
            var litMat = Resources.Load<Material>("sprite3DMat");

            _teaObj = new GameObject("Tea");
            _teaObj.transform.SetParent(transform);
            _teaObj.transform.position = new Vector3(0.31f, 2.48f, -5.1f);
            _teaObj.transform.localScale = new Vector3(3f, 3f, 1f);

            var sr = _teaObj.AddComponent<SpriteRenderer>();
            if (teaSprite != null) sr.sprite = teaSprite;
            sr.sortingOrder = 2;
            if (litMat != null) sr.material = litMat;

            if (teaCtrl != null)
            {
                var anim = _teaObj.AddComponent<Animator>();
                anim.runtimeAnimatorController = teaCtrl;
            }

            _teaObj.SetActive(false);
        }

        void BuildCat()
        {
            var catCtrl = Resources.Load<RuntimeAnimatorController>("catA");
            var catSprite = GameSprites.Get(GameSprites.ITEM_CAT);
            var litMat = Resources.Load<Material>("sprite3DMat");

            _catObj = new GameObject("Cat");
            _catObj.transform.SetParent(transform);
            _catObj.transform.position = new Vector3(-3.48f, 1.13f, -3.82f);

            var sr = _catObj.AddComponent<SpriteRenderer>();
            if (catSprite != null) sr.sprite = catSprite;
            sr.sortingOrder = 2;
            if (litMat != null) sr.material = litMat;

            if (catCtrl != null)
            {
                var anim = _catObj.AddComponent<Animator>();
                anim.runtimeAnimatorController = catCtrl;
            }

            _catObj.SetActive(false);
        }
    }
}
