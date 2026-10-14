using AbsoluteZero.Core.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LobbyButtonAudio : MonoBehaviour
    {
        Button _button;

        void OnEnable()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(PlayClick);
        }

        void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(PlayClick);
        }

        void PlayClick() => GameAudioManager.Instance?.PlayButtonClick();
    }
}
