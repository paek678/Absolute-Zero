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
                _ => Get(ITEM_SET)
            };
        }

        public static Sprite GetStayItemSprite()
        {
            return Get("objtest1_8");
        }
    }
}
