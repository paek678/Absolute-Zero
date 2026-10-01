using System;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.UI.LobbyUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.EditorTools
{
    public static class ClosetFeatureSetup
    {
        public const string VisualPath = "Assets/Prefabs/UI/Closet/ClosetPreviewVisual.prefab";
        const string LobbyPath = "Assets/Scenes/LobbyScene.unity";
        [MenuItem("Absolute Zero/Closet/Connect Candidate Preview")]
        public static void BuildPreview()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            var tag = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tag.FindProperty("layers");
            if (LayerMask.NameToLayer("ClosetPreview") < 0)
            {
                int free = -1;
                for (int i = 8; i < layers.arraySize; i++)
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { free = i; break; }
                if (free < 0) throw new InvalidOperationException("No free preview layer.");
                layers.GetArrayElementAtIndex(free).stringValue = "ClosetPreview";
                tag.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(tag.targetObject);
            }
            var registry = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CosmeticAtlasPreview.prefab");
            var temporaryScene = EditorSceneManager.NewPreviewScene();
            try
            {
                using var visual = new PrivateCosmeticPreview(source.transform, registry, LayerMask.NameToLayer("ClosetPreview"));
                SceneManager.MoveGameObjectToScene(visual.Root, temporaryScene);
                visual.Root.name = "ClosetPreviewVisual";
                visual.Root.transform.localPosition = Vector3.zero;
                // Retain only authored render hierarchy. Runtime screen owns the mapper.
                UnityEngine.Object.DestroyImmediate(visual.Root.GetComponent<CosmeticAtlasRenderer>());
                visual.Root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(visual.Root, VisualPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(temporaryScene); }
            var candidate = PrefabUtility.LoadPrefabContents(ClosetLayoutSetup.PrefabPath);
            try
            {
                var surface = candidate.GetComponent<ClosetPreviewSurface>() ?? candidate.AddComponent<ClosetPreviewSurface>();
                surface.VisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
                surface.Registry = registry;
                PrefabUtility.SaveAsPrefabAsset(candidate, ClosetLayoutSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(candidate); }
        }

        [MenuItem("Absolute Zero/Closet/Install Validated Candidate In Lobby")]
        public static void InstallIntoLobby()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(LobbyPath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Preserve dirty Lobby scene before replacement.");
            if (opened) scene = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Additive);
            try
            {
                var ui = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AZLobbyUI>(true)).Single();
                var owner = new SerializedObject(ui);
                var property = owner.FindProperty("_closetPanel");
                var old = property.objectReferenceValue as GameObject;
                if (old == null) throw new InvalidOperationException("Existing Closet panel is missing.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ClosetLayoutSetup.PrefabPath);
                if (!prefab.GetComponent<ClosetViewBindings>().Validate(out var error)) throw new InvalidOperationException(error);
                var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.transform.parent);
                replacement.name = "ClosetPanel";
                replacement.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                replacement.SetActive(false);
                property.objectReferenceValue = replacement;
                owner.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(old);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
