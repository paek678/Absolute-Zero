using System;
using System.IO;
using AbsoluteZero.UI.LobbyUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace AbsoluteZero.EditorTools
{
    public static class ClosetLayoutSetup
    {
        public const string PrefabPath = "Assets/Prefabs/UI/Closet/ClosetPanel.prefab";
        public const string FixturePath = "Assets/Tests/Plan037Closet/ClosetLayoutFixture.unity";
        static readonly Color Cream = new(.96f, .94f, .89f), Ink = new(.13f, .19f, .18f);
        static readonly Color Teal = new(.19f, .39f, .40f), Muted = new(.34f, .40f, .38f);
        static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MalgunGothic SDF.asset");

        [MenuItem("Absolute Zero/Closet/Build Candidate Layout")]
        public static void BuildCandidate()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("ClosetPanel", typeof(RectTransform));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                Stretch((RectTransform)root.transform);
                var view = root.AddComponent<ClosetViewBindings>();
                view.Dim = Button(root.transform, "Dim", "", new Color(.035f, .07f, .07f, .86f));
                Stretch((RectTransform)view.Dim.transform);
                var panel = Panel(root.transform, "PanelBG", Cream);
                Anchors(panel, new Vector2(.045f, .055f), new Vector2(.955f, .945f), Vector2.zero, Vector2.zero);
                var title = Text(panel, "Title", "옷장", 44, Ink);
                Anchors(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(30, -72), new Vector2(-170, -14));
                title.fontStyle = FontStyles.Bold;
                var subtitle = Text(panel, "Subtitle", "마음에 드는 스타일을 골라 입혀보세요", 24, Muted);
                Anchors(subtitle.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(32, -108), new Vector2(-150, -70));
                view.Close = Button(panel, "Close", "닫기", Teal);
                Anchors((RectTransform)view.Close.transform, Vector2.one, Vector2.one, new Vector2(-142, -82), new Vector2(-26, -24));
                var body = Rect(panel, "Body");
                Stretch(body, new Vector2(28, 180), new Vector2(-28, -124));
                var previewArea = Panel(body, "PreviewArea", new Color(.81f, .87f, .85f));
                Anchors(previewArea, Vector2.zero, new Vector2(.40f, 1), Vector2.zero, new Vector2(-12, 0));
                var previewLabel = Text(previewArea, "PreviewLabel", "미리보기", 28, Teal);
                Anchors(previewLabel.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -54), new Vector2(-24, -8));
                var raw = Rect(previewArea, "CharacterImage");
                Stretch(raw, new Vector2(18, 112), new Vector2(-18, -60));
                view.Preview = raw.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                view.Preview.raycastTarget = false; view.Preview.color = Color.white;
                view.Preview.enabled = false; // Connected to a private render target in C02.
                view.PreviewHint = Text(previewArea, "PreviewHint", "캐릭터 미리보기", 28, Muted);
                Anchors(view.PreviewHint.rectTransform, new Vector2(0, .35f), new Vector2(1, .65f), new Vector2(16, 0), new Vector2(-16, 0));
                view.PreviewHint.alignment = TextAlignmentOptions.Center;
                view.ResetPreview = Button(previewArea, "ResetPreview", "착용 중인 모습 보기", Teal);
                Anchors((RectTransform)view.ResetPreview.transform, Vector2.zero, new Vector2(1, 0), new Vector2(22, 24), new Vector2(-22, 90));

                var catalog = Rect(body, "Catalog");
                Anchors(catalog, new Vector2(.40f, 0), Vector2.one, new Vector2(12, 0), Vector2.zero);
                var tabs = Rect(catalog, "Tabs");
                Anchors(tabs, new Vector2(0, 1), Vector2.one, new Vector2(0, -66), Vector2.zero);
                var tabLayout = tabs.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                tabLayout.spacing = 8; tabLayout.childControlWidth = tabLayout.childControlHeight = true;
                tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = true;
                string[] labels = { "머리", "상의", "등", "하의", "꼬리" };
                view.Tabs = new UnityEngine.UI.Button[labels.Length];
                for (int i = 0; i < labels.Length; i++)
                {
                    view.Tabs[i] = Button(tabs, "Tab_" + (AbsoluteZero.Core.Cosmetic.CosmeticPart)i, labels[i], Teal);
                    var layout = view.Tabs[i].gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                    layout.minWidth = 70; layout.flexibleWidth = 1;
                }
                var scroll = Rect(catalog, "Scroll");
                Stretch(scroll, Vector2.zero, new Vector2(0, -84));
                view.Scroll = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
                view.Scroll.horizontal = false;
                view.Scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
                view.Scroll.scrollSensitivity = 36;
                var viewport = Panel(scroll, "Viewport", new Color(.90f, .90f, .85f));
                Stretch(viewport, Vector2.zero, new Vector2(-18, 0));
                viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
                var bar = Panel(scroll, "Scrollbar", new Color(.81f, .83f, .78f));
                Anchors(bar, new Vector2(1, 0), Vector2.one, new Vector2(-10, 0), Vector2.zero);
                var handle = Panel(bar, "Handle", Teal); Stretch(handle);
                var scrollbar = bar.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
                scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
                scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
                view.Scroll.verticalScrollbar = scrollbar;
                view.Scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
                view.Content = Rect(viewport, "Content");
                view.Content.anchorMin = new Vector2(0, 1); view.Content.anchorMax = Vector2.one;
                view.Content.pivot = new Vector2(.5f, 1); view.Content.anchoredPosition = Vector2.zero;
                view.Content.sizeDelta = Vector2.zero;
                var grid = view.Content.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
                grid.padding = new RectOffset(12, 12, 12, 12); grid.spacing = new Vector2(14, 14);
                grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
                grid.cellSize = new Vector2(220, 194);
                var fitter = view.Content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                view.Content.gameObject.AddComponent<ClosetResponsiveGrid>();
                view.Scroll.content = view.Content; view.Scroll.viewport = viewport;
                view.EmptyMessage = Text(viewport, "EmptyMessage", "이 부위에는 등록된 항목이 없어요", 28, Muted);
                Stretch(view.EmptyMessage.rectTransform, new Vector2(28, 20), new Vector2(-28, -20));
                view.EmptyMessage.alignment = TextAlignmentOptions.Center; view.EmptyMessage.gameObject.SetActive(false);
                view.ItemTemplate = Cell(root.transform); view.ItemTemplate.gameObject.SetActive(false);

                var footer = Rect(panel, "Footer");
                Anchors(footer, Vector2.zero, new Vector2(1, 0), new Vector2(30, 16), new Vector2(-30, 164));
                view.SelectedName = Text(footer, "SelectedName", "선택한 항목이 없습니다", 30, Ink);
                Anchors(view.SelectedName.rectTransform, new Vector2(0, .40f), new Vector2(.65f, 1), Vector2.zero, new Vector2(-18, 0));
                view.SelectedName.overflowMode = TextOverflowModes.Ellipsis;
                view.EquippedName = Text(footer, "EquippedName", "착용 중: 기본 모습", 24, Muted);
                Anchors(view.EquippedName.rectTransform, new Vector2(0, .18f), new Vector2(.65f, .46f), Vector2.zero, new Vector2(-18, 0));
                var actions = Rect(footer, "Actions");
                Anchors(actions, new Vector2(.66f, .38f), Vector2.one, Vector2.zero, Vector2.zero);
                var actionLayout = actions.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                actionLayout.spacing = 12; actionLayout.childControlWidth = actionLayout.childControlHeight = true;
                actionLayout.childForceExpandWidth = actionLayout.childForceExpandHeight = true;
                view.Equip = Button(actions, "Equip", "착용하기", Teal);
                view.Unequip = Button(actions, "Unequip", "해제하기", new Color(.46f, .38f, .28f));
                view.Equip.interactable = view.Unequip.interactable = false;
                view.SaveStatus = Text(footer, "SaveStatus", "닫으면 착용한 모습이 저장됩니다", 21, Muted);
                Anchors(view.SaveStatus.rectTransform, Vector2.zero, new Vector2(.60f, .2f), Vector2.zero, new Vector2(-8, 0));
                view.PublicationStatus = Text(footer, "PublicationStatus", "", 21, Muted);
                Anchors(view.PublicationStatus.rectTransform, new Vector2(.60f, 0), new Vector2(1, .2f), Vector2.zero, Vector2.zero);
                view.PublicationStatus.alignment = TextAlignmentOptions.MidlineRight;
                root.AddComponent<ClosetPanelNavigation>();
                if (!view.Validate(out var error)) throw new InvalidOperationException(error);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void BuildFixture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FixturePath));
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                var camera = new GameObject("Camera", typeof(Camera));
                camera.GetComponent<Camera>().backgroundColor = new Color(.13f, .19f, .18f);
                camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                var canvas = new GameObject("LayoutCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var probe = new GameObject("LayoutValidation").AddComponent<ClosetLayoutValidationProbe>();
                probe.View = panel.GetComponent<ClosetViewBindings>();
                probe.Registry = AssetDatabase.LoadAssetAtPath<AbsoluteZero.Core.Cosmetic.CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
                EditorSceneManager.SaveScene(scene, FixturePath);
            }
            finally
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static ClosetItemCellBindings Cell(Transform parent)
        {
            var button = Button(parent, "ItemTemplate", "", Color.white);
            var root = (RectTransform)button.transform;
            var cell = root.gameObject.AddComponent<ClosetItemCellBindings>(); cell.Button = button;
            var border = Panel(root, "Selection", new Color(.70f, .45f, .15f));
            Stretch(border); cell.SelectionBorder = border.GetComponent<UnityEngine.UI.Image>(); cell.SelectionBorder.raycastTarget = false;
            var inner = Panel(border, "Interior", Color.white); Stretch(inner, new Vector2(5, 5), new Vector2(-5, -5));
            inner.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            cell.SelectionBorder.gameObject.SetActive(false);
            var icon = Panel(root, "Icon", Color.white);
            Anchors(icon, new Vector2(.17f, .35f), new Vector2(.83f, .90f), Vector2.zero, Vector2.zero);
            cell.Icon = icon.GetComponent<UnityEngine.UI.Image>(); cell.Icon.preserveAspect = true; cell.Icon.raycastTarget = false;
            cell.Fallback = Text(root, "Fallback", "그림 준비 중", 24, Muted);
            Anchors(cell.Fallback.rectTransform, new Vector2(0, .38f), new Vector2(1, .80f), new Vector2(8, 0), new Vector2(-8, 0));
            cell.Fallback.alignment = TextAlignmentOptions.Center;
            cell.Name = Text(root, "ItemName", "항목 이름", 26, Ink);
            Anchors(cell.Name.rectTransform, Vector2.zero, new Vector2(1, .34f), new Vector2(12, 8), new Vector2(-12, 0));
            cell.Name.alignment = TextAlignmentOptions.Center; cell.Name.overflowMode = TextOverflowModes.Ellipsis;
            cell.EquippedBadge = Text(root, "EquippedBadge", "착용 중", 22, Teal);
            Anchors(cell.EquippedBadge.rectTransform, new Vector2(0, .80f), Vector2.one, new Vector2(10, 0), new Vector2(-10, -4));
            cell.EquippedBadge.alignment = TextAlignmentOptions.TopRight;
            return cell;
        }
        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        static RectTransform Panel(Transform parent, string name, Color color)
        {
            var rect = Rect(parent, name); rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = color; return rect;
        }
        static TMP_Text Text(Transform parent, string name, string value, float size, Color color)
        {
            var rect = Rect(parent, name); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font; text.text = value; text.fontSize = size; text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
            return text;
        }
        static UnityEngine.UI.Button Button(Transform parent, string name, string label, Color color)
        {
            var rect = Panel(parent, name, Color.white); var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = button.colors; colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, .18f); colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = color * .8f; colors.disabledColor = new Color(.67f, .70f, .68f);
            button.colors = colors;
            var text = Text(rect, "Label", label, 28, Color.white); text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform, new Vector2(6, 4), new Vector2(-6, -4)); return button;
        }
        static void Stretch(RectTransform rect, Vector2 min = default, Vector2 max = default)
            => Anchors(rect, Vector2.zero, Vector2.one, min, max);
        static void Anchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 min, Vector2 max)
        {
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = min; rect.offsetMax = max;
        }
    }
}
