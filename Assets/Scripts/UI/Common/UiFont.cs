using TMPro;
using UnityEngine;

namespace AbsoluteZero.UI.Common
{
    public static class UiFont
    {
        static TMP_FontAsset _korean;
        static bool _tried;

        public static TMP_FontAsset Korean
        {
            get
            {
                if (_korean != null) return _korean;
                if (!ReferenceEquals(_korean, null))
                {
                    _korean = null;
                    _tried = false;
                }
                if (_tried) return null;
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
                            if (TMP_Settings.fallbackFontAssets != null &&
                                !TMP_Settings.fallbackFontAssets.Contains(asset))
                            {
                                TMP_Settings.fallbackFontAssets.Add(asset);
                            }
                            Debug.Log($"[UiFont] Korean font loaded: {System.IO.Path.GetFileName(path)}");
                            return _korean;
                        }
                    }
                }
                Debug.LogWarning("[UiFont] Korean OS font not found");
                return null;
            }
        }

        public static void Apply(TMP_Text tmp)
        {
            var font = Korean;
            if (font != null) tmp.font = font;
        }
    }
}
