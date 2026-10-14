using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    public sealed class SettingsPanelBindings : MonoBehaviour
    {
        public Slider Master, Bgm, Sfx;
        public Toggle Fullscreen, Shake;
        public Button Close, Dim, Retry;
        public TMP_Text Status, MasterValue, BgmValue, SfxValue;
        public UnityEngine.UI.Button Quit { get; private set; }
        // MatchMenu owns Escape while this panel is used during a match.
        public bool HandleEscape { get; set; } = true;
        public event Action Cancelled;
        void Update()
        {
            if (HandleEscape && Keyboard.current?.escapeKey.wasPressedThisFrame == true) Cancelled?.Invoke();
        }
        public void EnsureQuitButton()
        {
            if (Quit != null) return;
            Quit = Instantiate(Close, Close.transform.parent);
            Quit.name = "QuitBtn";
            Quit.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            Quit.GetComponentInChildren<TMP_Text>(true).text = "게임 종료";
            PlaceFooter(Quit, -240);
            PlaceFooter(Retry, 0);
            PlaceFooter(Close, 240);
            AbsoluteZero.UI.Common.CasualUiSkin.Button(Quit, AbsoluteZero.UI.Common.CasualUiSkin.Peach);
        }
        static void PlaceFooter(UnityEngine.UI.Button button, float x)
        {
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(x, -325);
            rect.sizeDelta = new Vector2(210, 55);
        }
        public bool IsComplete => Master != null && Bgm != null && Sfx != null && Fullscreen != null && Shake != null
            && Close != null && Dim != null && Retry != null && Status != null
            && MasterValue != null && BgmValue != null && SfxValue != null;
    }
}
