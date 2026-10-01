using System;
using System.Linq;
using AbsoluteZero.UI.LobbyUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AbsoluteZero.EditorTools
{
    public static class SoloSelectionSetup
    {
        [MenuItem("Absolute Zero/Solo/Connect Selection Screen")]
        public static void Connect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            const string path = "Assets/Scenes/LobbyScene.unity";
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Preserve dirty Lobby scene first.");
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var owner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AZLobbyUI>(true)).Single();
                using var data = new SerializedObject(owner);
                var main = ((GameObject)data.FindProperty("_mainPanel").objectReferenceValue).transform;
                var canvas = main.parent;
                var existing = canvas.Find("SoloSelectionPanel");
                if (existing != null)
                {
                    var found = existing.GetComponent<SoloSelectionBindings>();
                    if (found == null || !found.IsComplete) throw new InvalidOperationException("Existing selection panel is incomplete; inspect it before repairing.");
                    data.FindProperty("_soloSelection").objectReferenceValue = found;
                }
                else
                {
                    var font = main.GetComponentsInChildren<TextMeshProUGUI>(true).First(t => t.font != null).font;
                    var panel = Rect(canvas, "SoloSelectionPanel", Vector2.zero, Vector2.one);
                    var dim = panel.gameObject.AddComponent<UnityEngine.UI.Image>(); dim.color = new Color(.03f,.06f,.09f,.92f);
                    var body = Rect(panel, "Body", new Vector2(.13f,.1f), new Vector2(.87f,.9f));
                    body.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.10f,.15f,.19f);
                    Text(body, "Title", "봇 대전", font, 46, new Vector2(.05f,.88f), new Vector2(.95f,.97f));
                    Text(body, "Subtitle", "상대를 선택하고 1대1 대전을 시작하세요", font, 25, new Vector2(.05f,.81f), new Vector2(.95f,.88f));
                    var bindings = panel.gameObject.AddComponent<SoloSelectionBindings>();
                    bindings.Content = Scroll(body, "Profiles", new Vector2(.05f,.27f), new Vector2(.40f,.78f));
                    var layout = bindings.Content.gameObject.AddComponent<VerticalLayoutGroup>();
                    layout.spacing = 12; layout.childControlWidth = true; layout.childControlHeight = true;
                    layout.childForceExpandHeight = false;
                    bindings.OptionTemplate = Button(bindings.Content, "ProfileTemplate", "기본 봇", font, Vector2.zero, Vector2.one);
                    var element = bindings.OptionTemplate.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 84;
                    bindings.OptionTemplate.gameObject.SetActive(false);
                    var detailContent = Scroll(body, "DetailsScroll", new Vector2(.45f,.27f), new Vector2(.95f,.78f));
                    bindings.Details = detailContent.gameObject.AddComponent<TextMeshProUGUI>();
                    Style(bindings.Details, font, 28); bindings.Details.text = "";
                    bindings.Status = Text(body, "Status", "", font, 25, new Vector2(.05f,.14f), new Vector2(.95f,.25f));
                    bindings.Status.enableAutoSizing = true; bindings.Status.fontSizeMin = 16; bindings.Status.fontSizeMax = 25;
                    bindings.BackButton = Button(body, "BackBtn", "뒤로", font, new Vector2(.05f,.035f), new Vector2(.28f,.125f));
                    bindings.StartButton = Button(body, "StartBtn", "대전 시작", font, new Vector2(.63f,.035f), new Vector2(.95f,.125f));
                    data.FindProperty("_soloSelection").objectReferenceValue = bindings;
                    panel.gameObject.SetActive(false);
                }
                var profiles = data.FindProperty("_soloProfiles");
                if (profiles.arraySize == 0)
                {
                    profiles.arraySize = 1;
                    var entry = profiles.GetArrayElementAtIndex(0);
                    entry.FindPropertyRelative("Title").stringValue = "기본 봇";
                    entry.FindPropertyRelative("Description").stringValue = "기존 기본 설정으로 진행하는 봇 대전입니다.";
                    entry.FindPropertyRelative("Encounter").objectReferenceValue = data.FindProperty("_soloEncounter").objectReferenceValue;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { SceneManager.SetActiveScene(previous); if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        static RectTransform Scroll(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var root = Rect(parent, name, min, max);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect(root, "Viewport", Vector2.zero, Vector2.one);
            var image = viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.12f,.18f,.22f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var content = Rect(viewport, "Content", new Vector2(0,1), Vector2.one);
            content.pivot = new Vector2(.5f,1);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            return content;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        static void Style(TMP_Text text, TMP_FontAsset font, float size)
        { text.font = font; text.fontSize = size; text.color = new Color(.94f,.96f,.94f); text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; }
        static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, float size, Vector2 min, Vector2 max)
        { var text = Rect(parent, name, min, max).gameObject.AddComponent<TextMeshProUGUI>(); Style(text, font, size); text.text = value; return text; }
        static UnityEngine.UI.Button Button(Transform parent, string name, string title, TMP_FontAsset font, Vector2 min, Vector2 max)
        {
            var rect = Rect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.25f,.48f,.45f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var label = Text(rect, "Label", title, font, 32, new Vector2(.04f,.08f), new Vector2(.96f,.92f));
            label.alignment = TextAlignmentOptions.Center; label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = 32;
            return button;
        }
    }
}
