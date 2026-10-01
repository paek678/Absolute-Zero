using System;
using System.Linq;
using AbsoluteZero.UI.LobbyUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.EditorTools
{
    public static class SettingsFeatureSetup
    {
        [MenuItem("Absolute Zero/Settings/Connect Settings Controls")]
        public static void Connect()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            const string path = "Assets/Scenes/LobbyScene.unity";
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Preserve dirty Lobby scene first.");
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var owner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AZLobbyUI>(true)).Single();
                var panel = (GameObject)new SerializedObject(owner).FindProperty("_settingsPanel").objectReferenceValue;
                var bg = (RectTransform)panel.transform.Find("PanelBG");
                if (bg == null || bg.Find("BGMSlider/Slider") == null || bg.Find("SFXSlider/Slider") == null)
                    throw new InvalidOperationException("Existing Settings hierarchy is missing.");
                var bindings = panel.GetComponent<SettingsPanelBindings>() ?? panel.AddComponent<SettingsPanelBindings>();
                bg.sizeDelta = new Vector2(800, 760);
                var title = bg.Find("Title").GetComponent<TextMeshProUGUI>();
                Rect(title.rectTransform, new Vector2(0, 315), new Vector2(660, 55)); title.fontSize = 40;
                var master = bg.Find("MasterSlider");
                if (master == null) { master = UnityEngine.Object.Instantiate(bg.Find("BGMSlider"), bg); master.name = "MasterSlider"; }
                bindings.Master = Volume(master, 190, "전체 음량", out bindings.MasterValue);
                bindings.Bgm = Volume(bg.Find("BGMSlider"), 100, "배경음악", out bindings.BgmValue);
                bindings.Sfx = Volume(bg.Find("SFXSlider"), 10, "효과음", out bindings.SfxValue);
                bindings.Fullscreen = Toggle(bg, "Fullscreen", -95, "전체 화면", title.font);
                bindings.Shake = Toggle(bg, "Shake", -170, "화면 흔들림", title.font);
                bindings.Status = Text(bg, "SettingsStatus", title.font, "", 25, new Vector2(0, -255), new Vector2(660, 65));
                bindings.Status.alignment = TextAlignmentOptions.Center;
                bindings.Close = bg.Find("CloseBtn").GetComponent<UnityEngine.UI.Button>();
                Rect((RectTransform)bindings.Close.transform, new Vector2(190, -325), new Vector2(230, 55));
                var retry = bg.Find("RetrySave");
                if (retry == null) { retry = UnityEngine.Object.Instantiate(bindings.Close.transform, bg); retry.name = "RetrySave"; }
                Rect((RectTransform)retry, new Vector2(-150, -325), new Vector2(280, 55));
                retry.GetComponentInChildren<TextMeshProUGUI>().text = "다시 저장";
                bindings.Retry = retry.GetComponent<UnityEngine.UI.Button>(); retry.gameObject.SetActive(false);
                bindings.Dim = panel.transform.Find("Dim").GetComponent<UnityEngine.UI.Button>();
                if (!bindings.IsComplete) throw new InvalidOperationException("Settings bindings invalid.");
                EditorSceneManager.SaveScene(scene);
            }
            finally { SceneManager.SetActiveScene(previous); if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        static UnityEngine.UI.Slider Volume(Transform root, float y, string label, out TMP_Text value)
        {
            Rect((RectTransform)root, new Vector2(0, y), new Vector2(680, 60));
            var text = root.Find("Label").GetComponent<TextMeshProUGUI>(); text.text = label; text.fontSize = 28;
            Rect(text.rectTransform, new Vector2(-230, 0), new Vector2(210, 45));
            var slider = root.Find("Slider").GetComponent<UnityEngine.UI.Slider>();
            Rect((RectTransform)slider.transform, new Vector2(55, 0), new Vector2(300, 28));
            value = Text(root, "Value", text.font, "100%", 27, new Vector2(280, 0), new Vector2(95, 45));
            value.alignment = TextAlignmentOptions.Right;
            return slider;
        }
        static UnityEngine.UI.Toggle Toggle(Transform root, string name, float y, string label, TMP_FontAsset font)
        {
            var child = root.Find(name);
            if (child == null) child = new GameObject(name, typeof(RectTransform)).transform;
            child.SetParent(root, false); Rect((RectTransform)child, new Vector2(0, y), new Vector2(680, 55));
            var toggle = child.GetComponent<UnityEngine.UI.Toggle>() ?? child.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            var background = child.Find("Background");
            if (background == null) background = new GameObject("Background", typeof(RectTransform)).transform;
            background.SetParent(child, false); Rect((RectTransform)background, new Vector2(270, 0), new Vector2(46, 46));
            var image = background.GetComponent<UnityEngine.UI.Image>() ?? background.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.28f, .42f, .42f);
            var oldText = background.Find("Check");
            if (oldText != null) UnityEngine.Object.DestroyImmediate(oldText.gameObject);
            var markObject = new GameObject("Check", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            markObject.transform.SetParent(background, false);
            Rect((RectTransform)markObject.transform, Vector2.zero, new Vector2(24, 24));
            var mark = markObject.GetComponent<UnityEngine.UI.Image>(); mark.color = Color.white; mark.raycastTarget = false;
            toggle.targetGraphic = image; toggle.graphic = mark; toggle.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            Text(child, "Label", font, label, 28, new Vector2(-140, 0), new Vector2(380, 50));
            return toggle;
        }
        static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, string value, float size, Vector2 position, Vector2 dimensions)
        {
            var child = parent.Find(name);
            if (child == null) child = new GameObject(name, typeof(RectTransform)).transform;
            child.SetParent(parent, false); Rect((RectTransform)child, position, dimensions);
            var text = child.GetComponent<TextMeshProUGUI>() ?? child.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = size; text.text = value; text.color = new Color(.17f, .12f, .09f);
            text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
            return text;
        }
        static void Rect(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; }
    }
}
