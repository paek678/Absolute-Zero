using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    public sealed class SoloSelectionBindings : MonoBehaviour
    {
        public RectTransform Content;
        public Button OptionTemplate;
        public TMP_Text Details, Status;
        public Button StartButton, BackButton;
        public bool IsComplete => Content != null && OptionTemplate != null && Details != null && Status != null
            && StartButton != null && BackButton != null;
    }
}
