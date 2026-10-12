using AbsoluteZero.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    public static class CasualLobbySkin
    {
        static readonly string[] Panels = { "ModeSelectPanel", "RoomPanel", "SettingsPanel", "ClosetPanel", "SoloSelectionPanel" };

        public static void Apply(Transform mainUI)
        {
            foreach (var name in Panels)
            {
                var root = mainUI.Find(name);
                if (root == null) continue;
                if (name == "RoomPanel") RoomLayout(root);
                CasualUiSkin.At(root, name == "SoloSelectionPanel" ? "Body" : "PanelBG", CasualUiSkin.Cream);
                CasualUiSkin.Labels(root);
                var title = root.Find("PanelBG/Title")?.GetComponent<TMP_Text>();
                if (title != null && name != "ClosetPanel") TitlePlate(title);
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.name == "Dim") continue;
                    Color color = button.name.Contains("Leave") || button.name == "Close" ? CasualUiSkin.Peach
                        : button.name.Contains("Back") || button.name.Contains("Unequip") ? CasualUiSkin.Butter : CasualUiSkin.Mint;
                    CasualUiSkin.Button(button, color, button.name == "ItemTemplate" || button.name == "ProfileTemplate");
                }
                foreach (var slider in root.GetComponentsInChildren<Slider>(true)) CasualUiSkin.Slider(slider);
                foreach (var scroll in root.GetComponentsInChildren<Scrollbar>(true))
                {
                    CasualUiSkin.Surface(scroll.GetComponent<Image>(), CasualUiSkin.Cream, CasualUiSkin.Pill, 28);
                    CasualUiSkin.Surface(scroll.targetGraphic as Image, CasualUiSkin.Mint, CasualUiSkin.Pill, 22);
                }
                foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
                {
                    CasualUiSkin.Surface(input.targetGraphic as Image, CasualUiSkin.Sky, CasualUiSkin.Card, 9);
                    input.caretColor = CasualUiSkin.Ink;
                    if (input.placeholder is TMP_Text placeholder) CasualUiSkin.Label(placeholder, CasualUiSkin.Muted);
                }
            }
            var closet = mainUI.Find("ClosetPanel");
            CasualUiSkin.At(closet, "PanelBG/Body/PreviewArea", CasualUiSkin.Mint, CasualUiSkin.Card);
            var viewport = closet != null ? closet.Find("PanelBG/Body/Catalog/Scroll/Viewport")?.GetComponent<Image>() : null;
            if (viewport != null) viewport.color = new Color(1, 1, 1, .01f);
            var bindings = closet != null ? closet.GetComponent<ClosetViewBindings>() : null;
            if (bindings != null) Cell(bindings.ItemTemplate);
            var room = mainUI.Find("RoomPanel");
            CasualUiSkin.At(room, "PanelBG/LobbyCode", CasualUiSkin.Sky, CasualUiSkin.Pill, 10);
            CasualUiSkin.At(room, "PanelBG/LogScroll", CasualUiSkin.Cream, CasualUiSkin.Card, 10);
            // Clip viewports stay unframed; only their obsolete dark fill is cleared.
            var solo = mainUI.Find("SoloSelectionPanel");
            foreach (var path in new[] { "Body/Profiles/Viewport", "Body/DetailsScroll/Viewport" })
            {
                var image = solo != null ? solo.Find(path)?.GetComponent<Image>() : null;
                if (image != null) image.color = new Color(1, 1, 1, .05f);
            }
        }

        public static void Cell(ClosetItemCellBindings cell)
        {
            if (cell == null) return;
            CasualUiSkin.Button(cell.Button, CasualUiSkin.Cream, true);
            CasualUiSkin.Surface(cell.SelectionBorder, CasualUiSkin.Mint, CasualUiSkin.Card, 4);
            CasualUiSkin.At(cell.SelectionBorder.transform, "Interior", CasualUiSkin.Cream, CasualUiSkin.Card, 4);
            CasualUiSkin.Label(cell.EquippedBadge, new Color(.17f, .38f, .30f));
        }

        public static void RoomLayout(Transform root, int seats = 4, bool inLobby = false)
        {
            int rows = Mathf.Clamp(seats, 2, 4);
            float listHeight = rows * (rows == 2 ? 55f : 46f) + (rows - 1) * 8f;
            float height = 478f + listHeight + (inLobby ? 0f : 62f);
            Rect(root, "PanelBG", 0, 0, 760, height);
            float top = height * .5f - 30f;
            void Place(string name, float width, float h, float gap = 14f)
            {
                Rect(root, "PanelBG/" + name, 0, top - h * .5f, width, h);
                top -= h + gap;
            }
            Place("ModeLabel", 620, 30);
            Place("Title", 500, 48, 20);
            Place("LobbyCode", 500, 48);
            if (!inLobby)
            {
                Rect(root, "PanelBG/CreateBtn", -230, top - 24, 160, 48);
                Rect(root, "PanelBG/JoinInput", 0, top - 24, 260, 48);
                Rect(root, "PanelBG/JoinBtn", 230, top - 24, 160, 48);
                top -= 62;
            }
            Place("PlayerList", 640, listHeight);
            Place("LogScroll", 640, 78, 10);
            Place("StatusText", 640, 28);
            Rect(root, "PanelBG/StartBtn", -155, top - 26, 280, 52);
            Rect(root, "PanelBG/LeaveBtn", 155, top - 26, 280, 52);
            top -= 64;
            Place("BackLink", 260, 36, 0);
            var list = root.Find("PanelBG/PlayerList")?.GetComponent<VerticalLayoutGroup>();
            if (list != null)
            {
                list.childAlignment = TextAnchor.UpperCenter;
                list.childForceExpandHeight = false;
                list.childControlHeight = true;
                list.spacing = 8;
                list.padding = new RectOffset();
            }
            var title = root.Find("PanelBG/Title")?.GetComponent<TMP_Text>();
            if (title != null) TitlePlate(title);
        }

        static void Rect(Transform root, string path, float x, float y, float width, float height)
        {
            if (root.Find(path) is not RectTransform rect) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        }

        static void TitlePlate(TMP_Text title)
        {
            var textRect = title.rectTransform;
            var plate = textRect.parent.Find("CasualTitlePlate") as RectTransform;
            if (plate == null)
            {
                var go = new GameObject("CasualTitlePlate", typeof(RectTransform), typeof(Image));
                plate = go.GetComponent<RectTransform>(); plate.SetParent(textRect.parent, false);
            }
            plate.anchorMin = textRect.anchorMin; plate.anchorMax = textRect.anchorMax;
            plate.pivot = textRect.pivot; plate.anchoredPosition = textRect.anchoredPosition;
            plate.sizeDelta = new Vector2(420, textRect.sizeDelta.y + 18);
            plate.SetAsFirstSibling();
            var image = plate.GetComponent<Image>(); CasualUiSkin.Surface(image, CasualUiSkin.Butter, CasualUiSkin.Pill, 7);
            image.raycastTarget = false;
        }
    }
}
