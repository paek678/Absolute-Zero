using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    public static class GameSprites
    {
        static Sprite[] _all;
        static readonly Dictionary<string, Sprite> _lookup = new();

        // UI sprites
        public const string HP_BAR_FRAME = "objtest1_0";
        public const string CLOCK = "objtest1_1";
        public const string HP_BAR_FILL = "objtest1_2";
        public const string READY_TEXT = "objtest1_20";

        // Character
        public const string PLAYER = "objtest1_3";

        // Basic Items (have dedicated sprites)
        public const string ITEM_FAN = "objtest1_12";
        public const string ITEM_CAT = "objtest1_10";
        public const string ITEM_WINDBREAKER = "objtest1_11";
        public const string ITEM_TEA = "objtest1_13";
        public const string ITEM_BROOM = "objtest1_8";

        // Collection sprites
        public const string ITEM_SET = "objtest1_4";
        public const string ITEM_DISPLAY = "objtest1_14";

        static void EnsureLoaded()
        {
            if (_all != null) return;
            _all = Resources.LoadAll<Sprite>("objtest1");
            foreach (var s in _all)
                _lookup[s.name] = s;
        }

        public static Sprite Get(string name)
        {
            EnsureLoaded();
            return _lookup.TryGetValue(name, out var s) ? s : null;
        }

        public static Sprite GetItemSprite(string itemName)
        {
            return itemName switch
            {
                "Fan" => Get(ITEM_FAN),
                "Hand Fan" => Get(ITEM_BROOM),
                "Cat" => Get(ITEM_CAT),
                "Windbreaker" => Get(ITEM_WINDBREAKER),
                "Warm Tea" => Get(ITEM_TEA),
                // 랜덤 아이템 개별 아트는 아직 없음 — 회색 사각형 플레이스홀더 (2026-07-18 합의)
                _ => Placeholder()
            };
        }

        static Sprite _placeholder;

        /// <summary>아트 없는 아이템용 회색 사각형 (1×1 유닛, 기존 아이템과 동일한 바닥 피벗)</summary>
        static Sprite Placeholder()
        {
            if (_placeholder == null)
            {
                const int size = 4;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var gray = new Color(0.62f, 0.62f, 0.66f, 1f);
                var pixels = new Color[size * size];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = gray;
                tex.SetPixels(pixels);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                _placeholder = Sprite.Create(tex, new Rect(0, 0, size, size),
                    new Vector2(0.5f, 0f), size);   // PPU=size → 월드 1×1
            }
            return _placeholder;
        }

        public static Sprite GetStayItemSprite()
        {
            return Get("objtest1_8");
        }
    }
}
