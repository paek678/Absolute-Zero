using System.Collections;
using System.Collections.Generic;
using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Turn;
using UnityEngine;
using UnityEngine.Rendering;

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
        static readonly WaitForSeconds _waitSink = new(SINK_DURATION);

        bool _kidsUsed;

        GameObject _kidGroup;
        GameObject _ambulanceGroup;
        GameObject _coolerObj;
        GameObject _teaObj;
        GameObject _catObj;
        GameObject _windObj;
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
            BuildWindParticle();

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
                    if (_windObj != null) _windObj.SetActive(true);
                    break;
                case EnvironmentType.Kids:
                    Debug.Log("[EnvVFX] Kids activated — KidsRiseRoutine");
                    StartCoroutine(KidsRiseRoutine());
                    break;
                case EnvironmentType.Ambulance:
                    Debug.Log("[EnvVFX] Ambulance activated — AmbulanceSlideRoutine");
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
            if (_windObj != null) _windObj.SetActive(false);

            _currentEnv = EnvironmentType.None;
            _kidsUsed = false;
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
            Debug.Log($"[EnvVFX] PlayKidsStealStaging — kidGroup={(_kidGroup != null)} kidsUsed={_kidsUsed}");
            if (_kidGroup == null || _kidsUsed) return;
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

            Vector3 stealPos = new Vector3(2.47f, 1.4f, 14f);

            leftKid.position = stealPos + Vector3.down * RISE_OFFSET;
            leftKid.gameObject.SetActive(true);
            if (kidAnim != null) { kidAnim.SetTrigger("ready"); Debug.Log("[EnvVFX] KidsSteal: trigger 'ready'"); }
            yield return StartCoroutine(RiseTransform(leftKid, stealPos, RISE_DURATION));

            if (kidAnim != null) { kidAnim.SetTrigger("steal"); Debug.Log("[EnvVFX] KidsSteal: trigger 'steal'"); }
            GameAudioManager.Instance?.PlayKidSteal();
            yield return _waitStealPause;

            yield return StartCoroutine(SinkTransform(leftKid, stealPos, SINK_DURATION));

            for (int i = 1; i < count; i++)
            {
                var kid = _kidGroup.transform.GetChild(i);
                if (kid.gameObject.activeSelf)
                    StartCoroutine(SinkTransform(kid, kid.position, SINK_DURATION));
            }
            if (count > 1)
                yield return _waitSink;

            _kidGroup.SetActive(false);
            _kidsUsed = true;
            Debug.Log("[EnvVFX] KidsStealRoutine DONE — all kids removed");
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
            }

            if (_ambulanceGroup != null && _ambulanceGroup.transform.childCount > 0)
            {
                var rescue = _ambulanceGroup.transform.GetChild(0);
                if (rescue.gameObject.activeSelf)
                    yield return StartCoroutine(SinkTransform(rescue, rescue.position, SINK_DURATION));
            }
            _ambulanceGroup.SetActive(false);
            Debug.Log("[EnvVFX] AmbulanceBlanketRoutine DONE — ambulance removed");
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

            var sprites = Resources.LoadAll<Sprite>("Environment/kid_idle");
            var kidCtrl = Resources.Load<RuntimeAnimatorController>("Environment/kidA");

            if (sprites == null || sprites.Length < 9)
            {
                Debug.LogWarning($"[EnvVFX] BuildKidGroup: kid_idle sprites incomplete ({(sprites != null ? sprites.Length : 0)}/9)");
                return;
            }

            var map = new Dictionary<string, Sprite>();
            foreach (var s in sprites) map[s.name] = s;

            Vector3[] positions = { new(-12.4f, 1.14f, 5.51f), new(-12.4f, 1.14f, 8.5f) };
            _kidLocalPositions = new Vector3[positions.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                var root = new GameObject($"Kid_{i}");
                root.transform.SetParent(_kidGroup.transform);
                root.transform.position = positions[i];
                _kidLocalPositions[i] = root.transform.localPosition;
                if (i == 1) root.transform.localScale = new Vector3(-1f, 1f, 1f);

                root.AddComponent<SortingGroup>();

                var body = BuildPart("body", root.transform, Vector3.zero, map["kid_idle_6"], 3);
                var head = BuildPart("head", body.transform, new Vector3(0f, 0.99f, 0f), map["kid_idle_1"], 4);
                BuildPart("frontHair", head.transform, new Vector3(0f, 0.45f, 0f), map["kid_idle_0"], 5);
                BuildPart("backHair", head.transform, new Vector3(0f, 0.06f, 0f), map["kid_idle_7"], 0);
                BuildPart("arm1", body.transform, new Vector3(-0.29f, 0.167f, 0f), map["kid_idle_2"], 1);
                BuildPart("arm2", body.transform, new Vector3(0.203f, 0.218f, 0f), map["kid_idle_3"], 3);
                var lower = BuildPart("lowerBody", root.transform, new Vector3(-0.01f, -0.77f, 0f), map["kid_idle_8"], 2);
                BuildPart("leg1", lower.transform, new Vector3(-0.286f, -0.339f, 0f), map["kid_idle_4"], 1);
                BuildPart("leg2", lower.transform, new Vector3(0.291f, -0.37f, 0f), map["kid_idle_5"], 1);

                if (kidCtrl != null)
                {
                    var anim = root.AddComponent<Animator>();
                    anim.runtimeAnimatorController = kidCtrl;
                    anim.applyRootMotion = false;
                }
            }

            _kidGroup.SetActive(false);
            Debug.Log($"[EnvVFX] BuildKidGroup DONE — {positions.Length} kids (9-part hierarchy each)");
        }

        void BuildAmbulanceGroup()
        {
            _ambulanceGroup = new GameObject("AmbulanceVFX");
            _ambulanceGroup.transform.SetParent(transform);

            var sprites = Resources.LoadAll<Sprite>("Environment/rescue_ready");
            var rescueCtrl = Resources.Load<RuntimeAnimatorController>("Environment/rescueA");

            if (sprites == null || sprites.Length < 7)
            {
                Debug.LogWarning($"[EnvVFX] BuildAmbulanceGroup: rescue_ready sprites incomplete ({(sprites != null ? sprites.Length : 0)}/7)");
                return;
            }

            var map = new Dictionary<string, Sprite>();
            foreach (var s in sprites) map[s.name] = s;

            var root = new GameObject("Rescue");
            root.transform.SetParent(_ambulanceGroup.transform);
            root.transform.position = new Vector3(2.47f, 1.4f, 14f);
            _ambulanceLocalPosition = root.transform.localPosition;

            root.AddComponent<SortingGroup>();

            var body = BuildPart("body", root.transform, Vector3.zero, map["rescue_ready_3"], 2);
            var head = BuildPart("head", body.transform, new Vector3(0f, 0.99f, 0f), map["rescue_ready_2"], 5);
            BuildPart("frontHair", head.transform, new Vector3(0f, 0.45f, 0f), map["rescue_ready_4"], 6);
            BuildPart("arm1", body.transform, new Vector3(-0.903f, 0.252f, 0f), map["rescue_ready_0"], 4);
            BuildPart("arm2", body.transform, new Vector3(0.86f, 0.222f, 0f), map["rescue_ready_1"], 4);
            BuildPart("lowerBody", root.transform, new Vector3(-0.01f, -1.41f, 0f), map["rescue_ready_5"], 0);
            BuildPart("cloth", root.transform, new Vector3(-0.01f, -0.62f, 0f), map["rescue_ready_6"], 3);

            if (rescueCtrl != null)
            {
                var anim = root.AddComponent<Animator>();
                anim.runtimeAnimatorController = rescueCtrl;
                anim.applyRootMotion = false;
            }

            _ambulanceGroup.SetActive(false);
            Debug.Log("[EnvVFX] BuildAmbulanceGroup DONE — Rescue (7-part hierarchy)");
        }

        static GameObject BuildPart(string name, Transform parent, Vector3 localPos, Sprite sprite, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortOrder;
            return go;
        }

        void BuildCooler()
        {
            var coolerCtrl = Resources.Load<RuntimeAnimatorController>("coolerA");
            var coolerSprite = GameSprites.Get(GameSprites.ITEM_FAN);

            _coolerObj = new GameObject("Cooler");
            _coolerObj.transform.SetParent(transform);
            _coolerObj.transform.position = new Vector3(-0.19f, 1.39f, -4.18f);
            _coolerObj.transform.localScale = new Vector3(4f, 4f, 1f);

            var sr = _coolerObj.AddComponent<SpriteRenderer>();
            if (coolerSprite != null) sr.sprite = coolerSprite;
            sr.sortingOrder = 2;

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

            _teaObj = new GameObject("Tea");
            _teaObj.transform.SetParent(transform);
            _teaObj.transform.position = new Vector3(0.31f, 2.48f, -5.1f);
            _teaObj.transform.localScale = new Vector3(3f, 3f, 1f);

            var sr = _teaObj.AddComponent<SpriteRenderer>();
            if (teaSprite != null) sr.sprite = teaSprite;
            sr.sortingOrder = 2;

            if (teaCtrl != null)
            {
                var anim = _teaObj.AddComponent<Animator>();
                anim.runtimeAnimatorController = teaCtrl;
            }

            _teaObj.SetActive(false);
        }

        void BuildWindParticle()
        {
            _windObj = new GameObject("WindVFX");
            _windObj.transform.SetParent(transform);
            _windObj.transform.position = new Vector3(0f, 2f, 3f);

            var ps = _windObj.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = 4f;
            main.startSpeed = 0f;
            main.startSize = 0.12f;
            main.startColor = new Color(0.85f, 0.93f, 1f, 0.4f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.maxParticles = 25;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 4f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(12f, 3f, 8f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;

            vel.x = new ParticleSystem.MinMaxCurve(4f, new AnimationCurve(
                new Keyframe(0f, -0.6f),
                new Keyframe(0.3f, -1f),
                new Keyframe(0.7f, -0.8f),
                new Keyframe(1f, -0.5f)
            ));

            vel.y = new ParticleSystem.MinMaxCurve(2f, new AnimationCurve(
                new Keyframe(0f, -0.2f),
                new Keyframe(0.3f, -0.6f),
                new Keyframe(0.6f, -0.8f),
                new Keyframe(1f, -1f)
            ));

            vel.z = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.1f),
                new Keyframe(0.5f, -0.1f),
                new Keyframe(1f, 0f)
            ));

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.1f, 1f),
                new Keyframe(0.85f, 1f),
                new Keyframe(1f, 0f)
            ));

            var trails = ps.trails;
            trails.enabled = true;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.15f);
            trails.dieWithParticles = true;
            trails.sizeAffectsWidth = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)
            ));

            var psr = _windObj.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.trailMaterial = CreateWindMaterial(new Color(0.85f, 0.93f, 1f, 0.5f));
            psr.material = CreateWindMaterial(new Color(1f, 1f, 1f, 0f));

            _windObj.SetActive(false);
            Debug.Log("[EnvVFX] BuildWindParticle DONE");
        }

        static Material CreateWindMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.color = color;
            if (shader.name.Contains("Universal"))
            {
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Surface", 1f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }
            return mat;
        }

        void BuildCat()
        {
            var catCtrl = Resources.Load<RuntimeAnimatorController>("catA");
            var catSprite = GameSprites.Get(GameSprites.ITEM_CAT);

            _catObj = new GameObject("Cat");
            _catObj.transform.SetParent(transform);
            _catObj.transform.position = new Vector3(-3.48f, 1.13f, -3.82f);

            var sr = _catObj.AddComponent<SpriteRenderer>();
            if (catSprite != null) sr.sprite = catSprite;
            sr.sortingOrder = 2;

            if (catCtrl != null)
            {
                var anim = _catObj.AddComponent<Animator>();
                anim.runtimeAnimatorController = catCtrl;
            }

            _catObj.SetActive(false);
        }
    }
}
