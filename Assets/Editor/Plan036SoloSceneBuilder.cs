using System;
using System.IO;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.UI.LobbyUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class Plan036SoloSceneBuilder
{
    public const string SoloScene = "Assets/Scenes/GameScene_Solo.unity";
    public const string Encounter = "Assets/Data/Solo/Encounters/DevelopmentSlice.asset";
    public static string Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var previous = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(previous.path) || previous.isDirty) throw new InvalidOperationException("Preserve/save the current scene first.");
        if (!File.Exists(SoloScene) && !AssetDatabase.CopyAsset("Assets/Scenes/GameScene.unity", SoloScene))
            throw new IOException("Could not derive the dedicated Solo scene from 1v1.");
        var scene = EditorSceneManager.OpenScene(SoloScene, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var behaviours = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            if (behaviours.Any(x => x is Unity.Netcode.NetworkManager)) throw new InvalidOperationException("Game scene must use the lobby's persistent NetworkManager.");
            foreach (var preview in behaviours.Where(x => x != null && x.GetType().Name == "AnimationTestRunner")) UnityEngine.Object.DestroyImmediate(preview);
            if (!behaviours.OfType<SoloDevelopmentDriver>().Any()) new GameObject("Solo Development Driver").AddComponent<SoloDevelopmentDriver>();
            EditorSceneManager.SaveScene(scene);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
        var encounter = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Encounter);
        if (encounter == null)
        {
            encounter = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Plan036ConfigurationBuilder.FixturePath));
            var fields = new SerializedObject(encounter);
            fields.FindProperty("configId").stringValue = "test.solo.scene.slice";
            fields.FindProperty("gameplayScenePath").stringValue = SoloScene;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(encounter, Encounter);
        }
        var lobby = EditorSceneManager.OpenScene("Assets/Scenes/LobbyScene.unity", OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(lobby);
            var network = lobby.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Unity.Netcode.NetworkManager>(true)).Single();
            if (network.GetComponent<AbsoluteZero.Core.Session.PersistentNetworkRoot>() == null)
                network.gameObject.AddComponent<AbsoluteZero.Core.Session.PersistentNetworkRoot>();
            var ui = lobby.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AZLobbyUI>(true)).Single();
            var fields = new SerializedObject(ui);
            fields.FindProperty("_soloEncounter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Plan036ConfigurationBuilder.DraftPath);
            fields.FindProperty("_soloValidationEncounter").objectReferenceValue = encounter;
            fields.FindProperty("_soloCosmetics").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            var catalog = Plan036ConfigurationBuilder.ReadCurrentDuelCatalog();
            var list = fields.FindProperty("_soloCatalog"); list.arraySize = catalog.Length;
            for (int i = 0; i < catalog.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = catalog[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
            var panel = (GameObject)fields.FindProperty("_mainPanel").objectReferenceValue;
            if (panel == null) throw new InvalidOperationException("MainPanel reference is missing.");
            if (panel.transform.Find("SoloBtn") == null)
            {
                var go = new GameObject("SoloBtn", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(panel.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f);
                rt.anchoredPosition = new Vector2(30, 92); rt.sizeDelta = new Vector2(200, 72);
                go.GetComponent<Image>().color = new Color(0.15f, 0.29f, 0.33f, 0.98f);
                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(go.transform, false);
                var text = label.GetComponent<TextMeshProUGUI>();
                var reference = panel.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(x => x != text && x.font != null);
                if (reference != null) text.font = reference.font;
                text.text = "1대 봇 대전"; text.fontSize = 28; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
                var tr = (RectTransform)label.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(8, 4); tr.offsetMax = new Vector2(-8, -4);
            }
            EditorSceneManager.SaveScene(lobby);
        }
        finally { EditorSceneManager.CloseScene(lobby, true); SceneManager.SetActiveScene(previous); }
        if (!EditorBuildSettings.scenes.Any(x => x.path == SoloScene))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(SoloScene, true) }).ToArray();
        AssetDatabase.SaveAssets();
        return "Dedicated Solo scene, additive lobby button and persistent network root saved. Development slice requires --solo-scripted-slice.";
    }
}
