using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    public static class GameSprites
    {
        static Sprite[] _all;
        static readonly Dictionary<string, Sprite> _lookup = new();

        // UI sprites (objtest1)
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

        // UI sprites (UIsprite1)
        public const string UI_BAR_BG = "UIsprite1_5";
        public const string UI_BAR_FILL = "UIsprite1_4";
        public const string UI_BAR_OUTLINE = "UIsprite1_3";
        public const string UI_THERMO_ICON = "UIsprite1_2";
        public const string UI_TIMER_BG = "UIsprite1_7";
        public const string UI_TIMER_OUTLINE = "UIsprite1_6";
        public const string UI_TIMER_LINE = "UIsprite1_14";
        public const string UI_TIMER_HAND = "UIsprite1_11";
        public const string UI_GIFT_LINE = "UIsprite1_21";
        public const string UI_GIFT_ICON_A = "UIsprite1_27";
        public const string UI_GIFT_ICON_B = "UIsprite1_23";
        public const string UI_GIFT_ICON_C = "UIsprite1_22";
        public const string UI_BTN_0 = "UIsprite1_0";
        public const string UI_BTN_1 = "UIsprite1_1";

        // Button sprites
        public const string BTN_DEFAULT = "btn_default";
        public const string BTN_PRESSED = "btn_pressed";

        // Alarm sprite
        public const string ALARM = "attacktime_0";

        static void EnsureLoaded()
        {
            if (_all != null) return;
            _all = Resources.LoadAll<Sprite>("objtest1");
            foreach (var s in _all)
                _lookup[s.name] = s;

            var uiSprites = Resources.LoadAll<Sprite>("UIsprite1");
            foreach (var s in uiSprites)
                _lookup[s.name] = s;

            var alarmSprites = Resources.LoadAll<Sprite>("attacktime");
            foreach (var s in alarmSprites)
                _lookup[s.name] = s;

            var btnDefault = Resources.Load<Sprite>("btn_default");
            if (btnDefault != null) _lookup[btnDefault.name] = btnDefault;

            var btnPressed = Resources.Load<Sprite>("btn_pressed");
            if (btnPressed != null) _lookup[btnPressed.name] = btnPressed;
        }

        public static Sprite Get(string name)
        {
            EnsureLoaded();
            return _lookup.TryGetValue(name, out var s) ? s : null;
        }

        static readonly Dictionary<string, string> ItemSpriteMap = new()
        {
            { "Fan", "Fan" },
            { "Cat", "Cat" },
            { "Windbreaker", "Windbreaker" },
            { "Warm Tea", "WarmTea" },
            { "Hand Fan", "HandFan" },
            { "Hug T-shirt", "HugTshirt" },
            { "Ice Cream", "IceCream" },
            { "Iced Americano", "IcedAmericano" },
            { "Hot Americano", "HotAmericano" },
            { "Buldak Noodles", "BuldakNoodles" },
            { "Soda", "Soda" },
            { "Blue Tape", "BlueTape" },
            { "Tarot Card", "TarotCard" },
            { "Hot Pack", "HotPack" },
            { "Smartphone", "Smartphone" },
            { "Water Gun", "WaterGun" },
            { "Red Card", "RedCard" },
            { "Samgyetang", "Samgyetang" },
            { "Mask", "Mask" },
            { "Screwdriver", "Screwdriver" },
            { "Claw Machine", "ClawMachine" },
        };

        static readonly Dictionary<string, Sprite> _itemSpriteCache = new();

        public static Sprite GetItemSprite(string itemName)
        {
            if (_itemSpriteCache.TryGetValue(itemName, out var cached))
                return cached;

            if (ItemSpriteMap.TryGetValue(itemName, out var fileName))
            {
                var sprite = Resources.Load<Sprite>($"ItemSprite/{fileName}");
                if (sprite != null)
                {
                    _itemSpriteCache[itemName] = sprite;
                    return sprite;
                }
                Debug.LogWarning($"[GameSprites] ItemSprite/{fileName} not found for '{itemName}'");
            }

            var fallback = Get(ITEM_FAN);
            _itemSpriteCache[itemName] = fallback;
            return fallback;
        }

        public static Sprite GetStayItemSprite()
        {
            return Get("objtest1_8");
        }
    }
}
