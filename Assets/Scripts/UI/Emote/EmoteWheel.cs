using System.Collections;
using AbsoluteZero.Core.Emote;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.Core.Common;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Emote
{
    /// <summary>
    /// 도발 이모티콘 휠 — 준비완료 버튼에 부착. 준비 완료 후 버튼을 누르고 있으면
    /// 이모티콘 5개가 부채꼴로 페이드-인, 원하는 쪽으로 드래그 후 떼면 선택 발동.
    /// 조건: 로컬 플레이어 IsReady && PrepPhase. 발동 시 OnFire(id) 콜백(네트워크 전송용).
    /// </summary>
    public class EmoteWheel : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public System.Action<int> OnFire;

        const float ArcRadius = 200f;
        const float IconSize = 128f;
        const float Deadzone = 55f;
        const float SelectMaxAngle = 42f;   // 이 각도 이내로 드래그해야 선택
        const float OpenDur = 0.22f;
        const float CloseDur = 0.13f;

        Canvas _canvas;
        RectTransform _root;      // 버튼 화면좌표에 놓이는 컨테이너
        RectTransform[] _icons;
        CanvasGroup[] _iconCg;
        Vector2[] _arcPos;
        float[] _curScale;

        bool _built;
        bool _open;
        int _selected = -1;
        Coroutine _anim;
        PlayerState _localPlayer;

        // ─── 입력 ───────────────────────────────────────────────

        public void OnPointerDown(PointerEventData e)
        {
            if (!CanEmote()) return;   // 준비 전 → 준비 버튼 onClick이 처리
            Open();
        }

        public void OnDrag(PointerEventData e) { /* 선택은 Update에서 폴링 */ }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_open) return;
            int sel = _selected;
            Close();
            if (sel >= 0)
                Fire(sel);
        }

        bool CanEmote()
        {
            var ps = LocalPlayer();
            if (ps == null || !ps.IsReady.Value) return false;
            var tm = TurnManager.Instance;
            return tm != null && tm.CurrentPhase.Value == TurnPhase.PrepPhase;
        }

        PlayerState LocalPlayer()
        {
            if (_localPlayer != null) return _localPlayer;
            var nm = NetworkManager.Singleton;
            var obj = nm != null ? nm.SpawnManager?.GetLocalPlayerObject() : null;
            if (obj != null) _localPlayer = obj.GetComponent<PlayerState>();
            return _localPlayer;
        }

        // ─── 열기/닫기 ──────────────────────────────────────────

        void Open()
        {
            EnsureBuilt();
            _open = true;
            _selected = -1;

            var screen = ButtonScreenPos();
            _root.anchoredPosition = screen;
            _root.gameObject.SetActive(true);

            for (int i = 0; i < _icons.Length; i++)
            {
                _icons[i].anchoredPosition = Vector2.zero;
                _icons[i].localScale = Vector3.zero;
                _iconCg[i].alpha = 0f;
                _curScale[i] = 0f;
            }

            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(OpenRoutine());
        }

        void Close()
        {
            _open = false;
            _selected = -1;
            if (!_built) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(CloseRoutine());
        }

        IEnumerator OpenRoutine()
        {
            float t = 0f;
            while (t < OpenDur)
            {
                t += Time.deltaTime;
                float baseP = Mathf.Clamp01(t / OpenDur);
                for (int i = 0; i < _icons.Length; i++)
                {
                    float p = Mathf.Clamp01(baseP - i * 0.03f);   // 살짝 스태거
                    float e = EaseOutBack(p);
                    _icons[i].anchoredPosition = _arcPos[i] * e;
                    _iconCg[i].alpha = Mathf.Clamp01(p * 1.4f);
                }
                yield return null;
            }
            for (int i = 0; i < _icons.Length; i++)
            {
                _icons[i].anchoredPosition = _arcPos[i];
                _iconCg[i].alpha = 1f;
            }
            _anim = null;
        }

        IEnumerator CloseRoutine()
        {
            float t = 0f;
            var start = new Vector3[_icons.Length];
            for (int i = 0; i < _icons.Length; i++) start[i] = _icons[i].localScale;
            while (t < CloseDur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / CloseDur);
                for (int i = 0; i < _icons.Length; i++)
                {
                    _icons[i].localScale = Vector3.Lerp(start[i], Vector3.zero, p);
                    _iconCg[i].alpha = 1f - p;
                }
                yield return null;
            }
            _root.gameObject.SetActive(false);
            _anim = null;
        }

        // ─── 선택 폴링 + 하이라이트 ─────────────────────────────

        void Update()
        {
            if (!_open || _icons == null) return;

            // 프렙 종료 등으로 더 이상 도발 불가하면 휠 자동 닫기 (열린 채 남지 않도록)
            if (!CanEmote()) { Close(); return; }

            _selected = ComputeSelection();

            for (int i = 0; i < _icons.Length; i++)
            {
                bool sel = i == _selected;
                float targetScale = sel ? 1.4f : (_selected >= 0 ? 0.82f : 1f);
                _curScale[i] = Mathf.Lerp(_curScale[i], targetScale, 1f - Mathf.Exp(-18f * Time.deltaTime));
                _icons[i].localScale = Vector3.one * _curScale[i];

                float targetAlpha = sel ? 1f : (_selected >= 0 ? 0.5f : 1f);
                if (_anim == null)   // 오픈 애니 끝난 뒤에만 알파 제어
                    _iconCg[i].alpha = Mathf.Lerp(_iconCg[i].alpha, targetAlpha, 1f - Mathf.Exp(-18f * Time.deltaTime));

                // 선택된 아이콘 살짝 바깥으로 밀기
                Vector2 lift = sel ? _arcPos[i].normalized * 16f : Vector2.zero;
                _icons[i].anchoredPosition = Vector2.Lerp(_icons[i].anchoredPosition, _arcPos[i] + lift,
                    1f - Mathf.Exp(-18f * Time.deltaTime));
            }
        }

        int ComputeSelection()
        {
            Vector2 ptr = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Vector2 dir = ptr - ButtonScreenPos();
            if (dir.magnitude < Deadzone) return -1;

            float ptrAng = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            int best = -1;
            float bestDelta = SelectMaxAngle;
            for (int i = 0; i < _arcPos.Length; i++)
            {
                float iconAng = Mathf.Atan2(_arcPos[i].y, _arcPos[i].x) * Mathf.Rad2Deg;
                float delta = Mathf.Abs(Mathf.DeltaAngle(ptrAng, iconAng));
                if (delta < bestDelta) { bestDelta = delta; best = i; }
            }
            return best;
        }

        Vector2 ButtonScreenPos()
        {
            var cam = Camera.main;
            if (cam == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var sp = cam.WorldToScreenPoint(transform.position);
            return new Vector2(sp.x, sp.y);
        }

        // ─── 발동 ───────────────────────────────────────────────

        void Fire(int id)
        {
            OnFire?.Invoke(id);
            LocalPlayer()?.SendEmoteServerRpc((byte)id);   // 네트워크 전송 → 상대 머리 위 말풍선
            StartCoroutine(LocalConfirmPop(id));           // 보낸 사람 로컬 확인 연출
        }

        IEnumerator LocalConfirmPop(int id)
        {
            var go = new GameObject($"EmoteConfirm_{id}");
            go.transform.SetParent(_canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = ButtonScreenPos() + new Vector2(0f, ArcRadius + 40f);
            rt.sizeDelta = new Vector2(220f, 240f);
            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            AddSprite(rt, "Text", EmoteCatalog.Text(id), new Vector2(0f, 96f), new Vector2(220f, 96f));
            AddSprite(rt, "Char", EmoteCatalog.Char(id), new Vector2(0f, -20f), new Vector2(200f, 160f));

            float t = 0f;
            const float inDur = 0.14f, hold = 0.55f, outDur = 0.3f;
            while (t < inDur) { t += Time.deltaTime; float p = t / inDur; cg.alpha = p; rt.localScale = Vector3.one * (0.6f + 0.5f * EaseOutBack(p)); yield return null; }
            cg.alpha = 1f; rt.localScale = Vector3.one;
            yield return new WaitForSeconds(hold);
            t = 0f;
            while (t < outDur) { t += Time.deltaTime; float p = t / outDur; cg.alpha = 1f - p; rt.anchoredPosition += new Vector2(0f, 40f * Time.deltaTime); yield return null; }
            Destroy(go);
        }

        // ─── 구성 ───────────────────────────────────────────────

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var canvasGO = new GameObject("EmoteWheelCanvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 120;

            var rootGO = new GameObject("WheelRoot");
            rootGO.transform.SetParent(canvasGO.transform, false);
            _root = rootGO.AddComponent<RectTransform>();
            _root.anchorMin = _root.anchorMax = Vector2.zero;   // 좌하단 기준 = 스크린 픽셀
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = Vector2.zero;

            int n = EmoteCatalog.Count;
            _icons = new RectTransform[n];
            _iconCg = new CanvasGroup[n];
            _arcPos = new Vector2[n];
            _curScale = new float[n];

            // 위쪽 부채꼴: 150° → 30° (n등분)
            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.Lerp(150f, 30f, n == 1 ? 0.5f : i / (float)(n - 1)) * Mathf.Deg2Rad;
                _arcPos[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ArcRadius;

                var go = new GameObject($"Emote{i}");
                go.transform.SetParent(_root, false);
                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(IconSize, IconSize);
                rt.anchoredPosition = Vector2.zero;
                _iconCg[i] = go.AddComponent<CanvasGroup>();

                // 배경 원(대비)
                var bg = new GameObject("Bg");
                bg.transform.SetParent(rt, false);
                var bgRt = bg.AddComponent<RectTransform>();
                bgRt.sizeDelta = new Vector2(IconSize, IconSize);
                var bgImg = bg.AddComponent<Image>();
                bgImg.sprite = MakeCircle();
                bgImg.color = new Color(0f, 0f, 0f, 0.42f);
                bgImg.raycastTarget = false;

                AddSprite(rt, "Char", EmoteCatalog.Char(i), Vector2.zero, new Vector2(IconSize * 0.86f, IconSize * 0.86f));

                _icons[i] = rt;
            }

            _root.gameObject.SetActive(false);
        }

        static void AddSprite(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            if (sprite != null) { img.sprite = sprite; img.preserveAspect = true; }
            else img.color = new Color(1f, 1f, 1f, 0.25f);
            img.raycastTarget = false;
        }

        static Sprite _circle;
        static Sprite MakeCircle()
        {
            if (_circle != null) return _circle;
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color[s * s];
            var c = new Vector2(s * 0.5f - 0.5f, s * 0.5f - 0.5f);
            float r = s * 0.5f - 1f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
                }
            tex.SetPixels(px); tex.Apply(); tex.filterMode = FilterMode.Bilinear;
            _circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            return _circle;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = 1.70158f + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        // 버튼 GO가 비활성화되면(프렙 종료로 준비 UI가 꺼질 때) 코루틴이 멈추므로 즉시 숨김/정리
        void OnDisable()
        {
            if (_anim != null) { StopCoroutine(_anim); _anim = null; }
            _open = false;
            _selected = -1;
            if (_root != null) _root.gameObject.SetActive(false);

            // 발동 확인 팝이 코루틴 정지로 남아있을 수 있어 정리
            if (_canvas != null)
            {
                for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
                {
                    var child = _canvas.transform.GetChild(i);
                    if (_root != null && child == _root.transform) continue;
                    Destroy(child.gameObject);
                }
            }
        }

        void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
