using System.Collections;
using AbsoluteZero.Core.Common;
using UnityEngine;
using UnityEngine.UI;

public class AnimationTestRunner : MonoBehaviour
{
    Animator _anim;
    SpriteRenderer[] _spriteRenderers;
    Material[] _cachedMaterials;

    SpriteRenderer _freezeRenderer;
    GameObject _freezeObj;
    int _freezeCount;
    Sprite _freeze1, _freeze2, _freeze3;

    Coroutine _dmgCo;
    Coroutine _freezeCo;
    Coroutine _cycleCo;

    GameObject _hitPrefab;
    GameObject _iceBreakPrefab;
    GameObject _finalBreakPrefab;

    Canvas _canvas;
    Text _statusText;

    static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");

    static readonly WaitForSeconds _wait0167 = new(0.167f);
    static readonly WaitForSeconds _wait03 = new(0.3f);
    static readonly WaitForSeconds _wait05 = new(0.5f);
    static readonly WaitForSeconds _wait06 = new(0.6f);
    static readonly WaitForSeconds _wait1 = new(1f);
    static readonly WaitForSeconds _wait11 = new(1.1f);
    static readonly WaitForSeconds _wait15 = new(1.5f);

    readonly string[] _cycleTriggers = {
        "attack", "defence", "drink", "button",
        "eat", "feed", "jump", "hug", "heal", "card",
        "disappoint", "swing"
    };

    Transform _enemyTransform;

    void Start()
    {
        var enemy = GameObject.Find("EnemyPlayer");
        if (enemy == null) return;

        _enemyTransform = enemy.transform;
        _anim = enemy.GetComponent<Animator>();

        _spriteRenderers = enemy.GetComponentsInChildren<SpriteRenderer>(true);
        _cachedMaterials = new Material[_spriteRenderers.Length];
        for (int i = 0; i < _spriteRenderers.Length; i++)
            _cachedMaterials[i] = _spriteRenderers[i].material;

        BuildFreezeObject(_enemyTransform);
        LoadParticlePrefabs();
        BuildTestUI();
    }

    void BuildFreezeObject(Transform parent)
    {
        _freeze1 = Resources.Load<Sprite>("freeze1");
        _freeze2 = Resources.Load<Sprite>("freeze2");
        _freeze3 = Resources.Load<Sprite>("freeze3");
        Debug.Log($"[AnimTest] Freeze sprites: f1={_freeze1 != null} f2={_freeze2 != null} f3={_freeze3 != null}");

        _freezeObj = parent.Find("FreezeObject")?.gameObject;
        if (_freezeObj != null)
        {
            _freezeRenderer = _freezeObj.GetComponent<SpriteRenderer>();
            _freezeObj.SetActive(false);
            Debug.Log("[AnimTest] Found existing FreezeObject in hierarchy");
            return;
        }

        if (_freeze1 == null) return;

        _freezeObj = new GameObject("FreezeObject");
        _freezeObj.transform.SetParent(parent, false);
        _freezeObj.transform.localPosition = new Vector3(0.18f, 0.08f, 0f);

        _freezeRenderer = _freezeObj.AddComponent<SpriteRenderer>();
        _freezeRenderer.sprite = _freeze1;
        _freezeRenderer.sortingOrder = 20;

        var bodyRenderer = parent.Find("body")?.GetComponent<SpriteRenderer>();
        if (bodyRenderer != null)
        {
            _freezeRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            _freezeRenderer.material = bodyRenderer.sharedMaterial;
        }

        _freezeObj.SetActive(false);
    }

    void LoadParticlePrefabs()
    {
        _hitPrefab = LoadPrefab("Assets/MainFolder/Prefabs/HitEffect.prefab");
        if (_hitPrefab == null)
            _hitPrefab = LoadPrefab("Assets/Art/gameGem/MainFolder/Prefabs/HitEffect.prefab");

        _iceBreakPrefab = LoadPrefab("Assets/MainFolder/Prefabs/IceBreakEffect.prefab");
        if (_iceBreakPrefab == null)
            _iceBreakPrefab = LoadPrefab("Assets/Art/gameGem/MainFolder/Prefabs/IceBreakEffect.prefab");

        _finalBreakPrefab = LoadPrefab("Assets/MainFolder/Prefabs/FinalBreakEffect.prefab");
        if (_finalBreakPrefab == null)
            _finalBreakPrefab = LoadPrefab("Assets/Art/gameGem/MainFolder/Prefabs/FinalBreakEffect.prefab");
    }

    GameObject LoadPrefab(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }

    void BuildTestUI()
    {
        var canvasGO = new GameObject("TestCanvas");
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        CreateButton("Damage", new Vector2(-120, -250), new Color(0.9f, 0.2f, 0.2f), OnDamageClicked);
        CreateButton("Freeze", new Vector2(120, -250), new Color(0.2f, 0.5f, 0.9f), OnFreezeClicked);
        CreateButton("Reset", new Vector2(0, -320), new Color(0.4f, 0.4f, 0.4f), OnResetClicked);
        CreateButton("Cycle All", new Vector2(0, -180), new Color(0.2f, 0.7f, 0.3f), OnCycleClicked);

        var statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(canvasGO.transform, false);
        _statusText = statusGO.AddComponent<Text>();
        _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _statusText.fontSize = 28;
        _statusText.alignment = TextAnchor.UpperCenter;
        _statusText.color = Color.white;
        var stRect = statusGO.GetComponent<RectTransform>();
        stRect.anchorMin = new Vector2(0.5f, 1f);
        stRect.anchorMax = new Vector2(0.5f, 1f);
        stRect.pivot = new Vector2(0.5f, 1f);
        stRect.anchoredPosition = new Vector2(0, -10);
        stRect.sizeDelta = new Vector2(600, 50);

        var outline = statusGO.AddComponent<Outline>();
        outline.effectColor = Color.black;
    }

    void CreateButton(string label, Vector2 pos, Color color, UnityEngine.Events.UnityAction action)
    {
        var btnGO = new GameObject($"Btn_{label}");
        btnGO.transform.SetParent(_canvas.transform, false);

        var img = btnGO.AddComponent<Image>();
        img.color = color;

        var btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(action);

        var rect = btnGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(180, 55);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        var txt = textGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 24;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        var tRect = textGO.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;
    }

    // === Animation Cycle ===

    IEnumerator CycleAllAnimations()
    {
        yield return _wait05;

        foreach (var trigger in _cycleTriggers)
        {
            SetStatus($"Playing: {trigger}");
            _anim.SetTrigger(trigger);
            yield return _wait1;
            _anim.SetTrigger("end");
            yield return _wait03;
        }

        SetStatus("Cycle complete — use Damage/Freeze buttons");
        _cycleCo = null;
    }

    // === Damage Flow (spec: flash 0.15s + wait 0.5s = ~0.65s) ===

    void OnDamageClicked()
    {
        if (_dmgCo != null) StopCoroutine(_dmgCo);
        _dmgCo = StartCoroutine(DamageCoroutine());
    }

    IEnumerator DamageCoroutine()
    {
        SetStatus("Damage!");
        _anim.SetTrigger("damage");

        if (_hitPrefab != null && _enemyTransform != null)
        {
            var particle = Instantiate(_hitPrefab, _enemyTransform.position, _enemyTransform.rotation);
            ForceParticle2DRendering(particle, 100);
        }

        float elapsed = 0f;
        const float flashDuration = 0.15f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            SetFlash(Mathf.Lerp(1f, 0f, elapsed / flashDuration));
            yield return null;
        }
        SetFlash(0f);

        yield return _wait05;
        _anim.SetTrigger("end");
        SetStatus("Idle");
        _dmgCo = null;
    }

    // === Freeze Flow ===

    void OnFreezeClicked()
    {
        _freezeCount++;

        if (_freezeCount > 2)
        {
            StartCoroutine(DieCoroutine());
            return;
        }

        if (_freezeCo != null) StopCoroutine(_freezeCo);
        _freezeCo = StartCoroutine(FreezeCoroutine());
    }

    IEnumerator FreezeCoroutine()
    {
        SetStatus($"Freeze! ({_freezeCount}/3)");

        if (_freezeRenderer != null)
            _freezeRenderer.color = new Color(1f, 1f, 1f, 0.6f);

        _anim.SetTrigger("freeze");

        yield return _wait0167;
        if (_freezeObj != null)
        {
            _freezeObj.SetActive(true);
            if (_freeze1 != null) _freezeRenderer.sprite = _freeze1;
            Debug.Log($"[AnimTest] FreezeObj activated. active={_freezeObj.activeSelf}, pos={_freezeObj.transform.position}, sprite={_freezeRenderer.sprite?.name}, color={_freezeRenderer.color}, sortLayer={_freezeRenderer.sortingLayerID}, sortOrder={_freezeRenderer.sortingOrder}");
        }

        yield return _wait0167;
        if (_freezeRenderer != null && _freeze2 != null)
            _freezeRenderer.sprite = _freeze2;

        yield return _wait0167;
        if (_freezeRenderer != null && _freeze3 != null)
            _freezeRenderer.sprite = _freeze3;

        yield return _wait06;

        if (_iceBreakPrefab != null && _enemyTransform != null)
        {
            var particle = Instantiate(_iceBreakPrefab, _enemyTransform.position, _enemyTransform.rotation);
            ForceParticle2DRendering(particle, 100);
        }

        if (_freezeObj != null) _freezeObj.SetActive(false);
        _anim.SetTrigger("end");
        SetStatus("Idle");
        _freezeCo = null;
    }

    IEnumerator DieCoroutine()
    {
        SetStatus("DEATH! (Freeze 3/3)");

        if (_freezeRenderer != null)
            _freezeRenderer.color = new Color(1f, 1f, 1f, 1f);

        _anim.SetTrigger("freeze");

        yield return _wait0167;
        if (_freezeObj != null)
        {
            _freezeObj.SetActive(true);
            if (_freeze1 != null) _freezeRenderer.sprite = _freeze1;
        }

        yield return _wait0167;
        if (_freezeRenderer != null && _freeze2 != null)
            _freezeRenderer.sprite = _freeze2;

        yield return _wait0167;
        if (_freezeRenderer != null && _freeze3 != null)
            _freezeRenderer.sprite = _freeze3;

        yield return _wait15;

        CameraShake.Instance?.Shake(0.5f, 0.3f);

        if (_finalBreakPrefab != null && _enemyTransform != null)
        {
            var particle = Instantiate(_finalBreakPrefab, _enemyTransform.position, _enemyTransform.rotation);
            ForceParticle2DRendering(particle, 100);
        }

        if (_enemyTransform != null) _enemyTransform.gameObject.SetActive(false);
        SetStatus("DEAD — Press Reset");
    }

    // === Reset ===

    void OnResetClicked()
    {
        if (_enemyTransform != null)
        {
            _enemyTransform.gameObject.SetActive(true);
            _anim = _enemyTransform.GetComponent<Animator>();
            _anim.SetTrigger("end");
        }

        _freezeCount = 0;
        if (_freezeObj != null)
            _freezeObj.SetActive(false);

        SetFlash(0f);
        SetStatus("Reset — Idle");
    }

    void OnCycleClicked()
    {
        if (_cycleCo != null) StopCoroutine(_cycleCo);
        OnResetClicked();
        _cycleCo = StartCoroutine(CycleAllAnimations());
    }

    // === Helpers ===

    static void ForceParticle2DRendering(GameObject go, int order)
    {
        foreach (var psr in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            psr.sortingLayerName = "Default";
            psr.sortingOrder = order;
            if (psr.sharedMaterial != null)
            {
                psr.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                psr.material.SetInt("_ZWrite", 0);
            }
        }
    }

    void SetFlash(float amount)
    {
        if (_cachedMaterials == null) return;
        for (int i = 0; i < _cachedMaterials.Length; i++)
            if (_cachedMaterials[i] != null)
                _cachedMaterials[i].SetFloat(FlashAmount, amount);
    }

    void SetStatus(string msg)
    {
        if (_statusText != null) _statusText.text = msg;
    }
}
