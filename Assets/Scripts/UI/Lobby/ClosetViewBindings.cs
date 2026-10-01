using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AbsoluteZero.UI.LobbyUI
{
    // Serialized view contract. Equipment, saving and network state belong to services.
    public sealed class ClosetViewBindings : MonoBehaviour
    {
        public UnityEngine.UI.Button[] Tabs;
        public UnityEngine.UI.ScrollRect Scroll;
        public RectTransform Content;
        public ClosetItemCellBindings ItemTemplate;
        public UnityEngine.UI.RawImage Preview;
        public TMP_Text PreviewHint, SelectedName, EquippedName, EmptyMessage, SaveStatus, PublicationStatus;
        public UnityEngine.UI.Button Equip, Unequip, ResetPreview, Close, Dim;
        public event Action<bool> VisibilityChanged;
        void OnEnable() => VisibilityChanged?.Invoke(true);
        void OnDisable() => VisibilityChanged?.Invoke(false);

        public bool Validate(out string error)
        {
            var seen = new HashSet<UnityEngine.Object>();
            var missing = new List<string>();
            void Required(UnityEngine.Object value, string name)
            {
                if (value == null) missing.Add(name + " missing");
                else if (!seen.Add(value)) missing.Add(name + " duplicated");
                else if (value is Component c && !c.transform.IsChildOf(transform)) missing.Add(name + " outside panel");
            }
            if (Tabs == null || Tabs.Length != 5) missing.Add("Exactly five tabs required");
            else for (int i = 0; i < Tabs.Length; i++) Required(Tabs[i], "Tab " + i);
            Required(Scroll, nameof(Scroll)); Required(Content, nameof(Content));
            Required(ItemTemplate, nameof(ItemTemplate)); Required(Preview, nameof(Preview));
            Required(PreviewHint, nameof(PreviewHint)); Required(SelectedName, nameof(SelectedName));
            Required(EquippedName, nameof(EquippedName)); Required(EmptyMessage, nameof(EmptyMessage));
            Required(SaveStatus, nameof(SaveStatus)); Required(PublicationStatus, nameof(PublicationStatus));
            Required(Equip, nameof(Equip)); Required(Unequip, nameof(Unequip));
            Required(ResetPreview, nameof(ResetPreview)); Required(Close, nameof(Close)); Required(Dim, nameof(Dim));
            if (Scroll != null && (Scroll.content != Content || Scroll.viewport == null
                || Content == null || !Content.IsChildOf(Scroll.viewport))) missing.Add("Scroll/viewport/content wiring");
            if (Preview != null && Preview.raycastTarget) missing.Add("Preview must not intercept input");
            if (ItemTemplate != null && !ItemTemplate.Validate(out var cellError)) missing.Add(cellError);
            error = string.Join("; ", missing);
            return missing.Count == 0;
        }
    }
}
