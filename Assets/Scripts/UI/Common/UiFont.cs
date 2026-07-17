using TMPro;
using UnityEngine;

namespace AbsoluteZero.UI.Common
{
    /// <summary>
    /// 런타임 한글 폰트 헬퍼 — TMP 기본 폰트(LiberationSans)에 한글 글리프가 없어
    /// 한글 텍스트("턴 결과", "준비 시간" 등)가 □로 깨지는 문제 해결.
    /// OS 폰트 파일(맑은 고딕)에서 동적 TMP 폰트를 생성한다 (에셋/재배포 불필요, 글리프 on-demand).
    /// </summary>
    public static class UiFont
    {
        static TMP_FontAsset _korean;
        static bool _tried;

        public static TMP_FontAsset Korean
        {
            get
            {
                if (_korean != null) return _korean;      // 살아있는 캐시만 재사용
                if (!ReferenceEquals(_korean, null))       // 파괴된 캐시(fake null) → 재생성 허용
                {
                    _korean = null;
                    _tried = false;
                }
                if (_tried) return null;                   // 한글 OS 폰트가 없는 환경 — 재시도 안 함
                _tried = true;

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
                            _korean = asset;
                            // 전역 TMP 폴백에도 등록 — Apply를 거치지 않은 텍스트까지 한글 자동 해결
                            if (TMP_Settings.fallbackFontAssets != null &&
                                !TMP_Settings.fallbackFontAssets.Contains(asset))
                            {
                                TMP_Settings.fallbackFontAssets.Add(asset);
                            }
                            Debug.Log($"[UiFont] 한글 폰트 로드: {System.IO.Path.GetFileName(path)} (전역 폴백 등록)");
                            return _korean;
                        }
                    }
                }
                Debug.LogWarning("[UiFont] 한글 OS 폰트를 찾지 못함 — TMP 기본 폰트 사용 (한글 □ 표시)");
                return null;
            }
        }

        /// <summary>가능하면 한글 지원 폰트로 교체 (실패 시 기본 폰트 유지)</summary>
        public static void Apply(TMP_Text tmp)
        {
            var font = Korean;
            if (font != null) tmp.font = font;
        }
    }
}
