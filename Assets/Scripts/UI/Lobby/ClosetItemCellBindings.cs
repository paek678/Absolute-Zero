using TMPro;
using UnityEngine;

namespace AbsoluteZero.UI.LobbyUI
{
    public sealed class ClosetItemCellBindings : MonoBehaviour
    {
        public UnityEngine.UI.Button Button;
        public UnityEngine.UI.Image Icon, SelectionBorder;
        public TMP_Text Name, Fallback, EquippedBadge;
        public bool Validate(out string error)
        {
            bool valid = Button != null && Icon != null && SelectionBorder != null
                && Name != null && Fallback != null && EquippedBadge != null
                && Name != Fallback && Name != EquippedBadge && Fallback != EquippedBadge;
            error = valid ? null : "Item cell has missing/duplicate references";
            return valid;
        }
    }
}
