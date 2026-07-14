using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AbsoluteZero.UI.TestUI
{
    /// <summary>
    /// 테스트 UI 공용 빌더 (테스트 전용) — 런타임 Canvas/텍스트/버튼 생성 + 한글 폰트.
    /// TMP 기본 폰트(LiberationSans)에 한글 글리프가 없어 OS 폰트 파일(맑은 고딕)에서
    /// 동적 TMP 폰트를 생성해 사용한다 (에셋/재배포 불필요).
    /// </summary>
    public static class TestUiKit
    {
        static TMP_FontAsset _koreanFont;
        static bool _koreanFontTried;

        public static TMP_FontAsset GetKoreanFont()
        {
            if (_koreanFont != null) return _koreanFont;      // 살아있는 캐시만 재사용
            if (!ReferenceEquals(_koreanFont, null))          // 파괴된 캐시(fake null) → 정리 후 재생성
            {
                _koreanFont = null;
                _koreanFontTried = false;
            }
            if (_koreanFontTried) return null;                // OS 폰트 자체가 없는 환경 — 재시도 안 함
            _koreanFontTried = true;

            // CreateDynamicFontFromOSFont 경유는 이 환경에서 실패 (Editor.log 확인)
            // → OS 폰트 "파일 경로"에서 직접 로드 (맑은 고딕 우선)
            string[] keywords = { "malgun.ttf", "malgun", "nanumgothic", "gulim", "batang" };
            var paths = Font.GetPathsToOSFonts();
            foreach (var keyword in keywords)
            {
                foreach (var path in paths)
                {
                    if (!System.IO.Path.GetFileName(path).ToLowerInvariant().Contains(keyword)) continue;
                    var font = new Font(path);
                    var asset = TMP_FontAsset.CreateFontAsset(font);
                    if (asset != null)
                    {
                        _koreanFont = asset;
                        return _koreanFont;
                    }
                }
            }
            Debug.LogWarning("[TestUiKit] 한글 OS 폰트를 찾지 못함 — TMP 기본 폰트 사용 (한글 □ 표시)");
            return null;
        }

        /// <summary>Canvas + Scaler + Raycaster + (필요 시) EventSystem 생성, root Transform 반환</summary>
        public static Transform CreateCanvas(string name)
        {
            var canvasGO = new GameObject(name);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<InputSystemUIInputModule>();
            }
            return canvasGO.transform;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name,
            Vector2 pos, Vector2 size, string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            var koreanFont = GetKoreanFont();
            if (koreanFont != null) tmp.font = koreanFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        public static Image CreatePanel(Transform parent, string name,
            Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static Sprite _whiteSprite;

        /// <summary>스프라이트 없는 Image는 Filled 타입에서 fillAmount가 무시됨 → 흰색 스프라이트 필수</summary>
        static Sprite WhiteSprite()
        {
            if (_whiteSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            return _whiteSprite;
        }

        /// <summary>온도 바 (fill Image 반환)</summary>
        public static Image CreateBar(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            CreatePanel(parent, name + "BG", pos, size, new Color(0.15f, 0.15f, 0.2f));
            var fill = CreatePanel(parent, name, pos, size, new Color(0.9f, 0.25f, 0.2f));
            fill.sprite = WhiteSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            return fill;
        }

        public static Button CreateButton(Transform parent, string name,
            Vector2 pos, Vector2 size, string label, Color color, out TextMeshProUGUI labelText)
        {
            var img = CreatePanel(parent, name, pos, size, color);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            labelText = CreateText(img.transform, "Label", Vector2.zero, size, label, 19);
            return btn;
        }

        /// <summary>온도 구간 색상: 37 RED → 30 PINK → 20 SKY → 10 BLUE (GAME_DESIGN)</summary>
        public static Color TempColor(float value)
        {
            return value > 30f ? new Color(0.9f, 0.25f, 0.2f)
                 : value > 20f ? new Color(0.95f, 0.55f, 0.65f)
                 : value > 10f ? new Color(0.5f, 0.8f, 0.95f)
                 : new Color(0.25f, 0.4f, 0.9f);
        }
    }
}
