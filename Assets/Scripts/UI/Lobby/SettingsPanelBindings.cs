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
        public event Action Cancelled;
        void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Cancelled?.Invoke();
        }
        public bool IsComplete => Master != null && Bgm != null && Sfx != null && Fullscreen != null && Shake != null
            && Close != null && Dim != null && Retry != null && Status != null
            && MasterValue != null && BgmValue != null && SfxValue != null;
    }
}
