using UnityEngine;

namespace AbsoluteZero.Core.Emote
{
    /// <summary>
    /// 도발 이모티콘 카탈로그 — Resources/Emoticon/{이름}_캐릭터, _텍스트 스프라이트 로더.
    /// 배열 인덱스 = 네트워크로 주고받는 emote id (0~Count-1).
    /// </summary>
    public static class EmoteCatalog
    {
        // 순서 = emote id (네트워크 동기화). 파일명과 일치해야 함.
        // slow=느려요, tongue=메롱, sneer=비웃음, excited=신난다, lose=지겠는데
        static readonly string[] _names = { "slow", "tongue", "sneer", "excited", "lose" };

        static Sprite[] _char;
        static Sprite[] _text;

        public static int Count => _names.Length;
        public static string Name(int i) => (i >= 0 && i < _names.Length) ? _names[i] : "?";

        public static Sprite Char(int i)
        {
            Load();
            return (i >= 0 && i < _char.Length) ? _char[i] : null;
        }

        public static Sprite Text(int i)
        {
            Load();
            return (i >= 0 && i < _text.Length) ? _text[i] : null;
        }

        static void Load()
        {
            if (_char != null) return;
            _char = new Sprite[_names.Length];
            _text = new Sprite[_names.Length];
            for (int i = 0; i < _names.Length; i++)
            {
                _char[i] = Resources.Load<Sprite>($"Emoticon/{_names[i]}_char");
                _text[i] = Resources.Load<Sprite>($"Emoticon/{_names[i]}_text");
                if (_char[i] == null || _text[i] == null)
                    Debug.LogWarning($"[EmoteCatalog] Missing sprite for '{_names[i]}' (char={_char[i] != null}, text={_text[i] != null})");
            }
        }
    }
}
