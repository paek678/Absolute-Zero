#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Player;
using AbsoluteZero.UI.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class Plan037PresentationMigration
{
    public const string CatalogPath = "Assets/Data/Presentation/ItemPresentationCatalog.asset";
    static readonly string[] SharedPaths =
    {
        "freeze1", "freeze2", "freeze3", "Fan/fan_body", "Fan/fan_blades", "Fan/fan_grille",
        "Cat/cat_sleep", "Cat/cat_wakeup", "Cat/cat_jump", "Cat/cat_jump2", "Cat/cat_rummage", "banned_tape"
    };
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();
    static void Reference(SerializedObject so, string name, Object value)
        => so.FindProperty(name).objectReferenceValue = value;
    static void References(SerializedObject so, string name, IEnumerable<Object> values)
    {
        var data = values.ToArray(); var property = so.FindProperty(name); property.arraySize = data.Length;
        for (int i = 0; i < data.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = data[i];
    }
    static Dictionary<string, string> LegacyMap(Type type, string field)
        => (Dictionary<string, string>)type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    static T Required<T>(T value, string description) where T : Object
        => value != null ? value : throw new InvalidOperationException("Missing " + description);

    public static string PopulateCatalog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
        try
        {
            var items = All<ItemManager>(preview).Single().GetAllItems();
            if (items.Length != 21 || items.Distinct().Count() != 21) throw new InvalidOperationException("Unexpected item registry.");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Presentation")) AssetDatabase.CreateFolder("Assets/Data", "Presentation");
            var catalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemPresentationCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var spriteMap = LegacyMap(typeof(GameSprites), "ItemSpriteMap");
            var specialMap = LegacyMap(typeof(GameAudioManager), "ItemNameToSfx");
            var triggerMap = LegacyMap(typeof(GameAudioManager), "TriggerToSfx");
            var so = new SerializedObject(catalog); var entries = so.FindProperty("entries"); entries.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i]; var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("item").objectReferenceValue = item;
                entry.FindPropertyRelative("sprite").objectReferenceValue = Required(Resources.Load<Sprite>("ItemSprite/" + spriteMap[item.ItemName]), item.name + " sprite");
                entry.FindPropertyRelative("audioOverride").objectReferenceValue = specialMap.TryGetValue(item.ItemName, out var sound)
                    ? Required(Resources.Load<AudioClip>("Audio/" + sound), sound) : null;
                entry.FindPropertyRelative("choreography").enumValueIndex = (int)ItemPresentation.LegacyChoreography(item.ItemName);
            }
            var audio = so.FindProperty("triggerAudio"); audio.arraySize = triggerMap.Count;
            int index = 0;
            foreach (var pair in triggerMap)
            {
                var entry = audio.GetArrayElementAtIndex(index++);
                entry.FindPropertyRelative("trigger").stringValue = pair.Key;
                entry.FindPropertyRelative("clip").objectReferenceValue = Required(Resources.Load<AudioClip>("Audio/" + pair.Value), pair.Value);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var errors = new List<string>(); if (!catalog.Validate(items, errors)) throw new InvalidOperationException(string.Join("; ", errors));
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            return $"Catalog validated and saved: {items.Length} identities.";
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    public static string MigrateScene(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        if (path != "Assets/Scenes/GameScene.unity" && path != "Assets/Scenes/GameScene_Multi.unity"
            && path != "Assets/Scenes/GameScene_Solo.unity" && path != "Assets/Scenes/LobbyScene.unity")
            throw new ArgumentException("Only the inventoried scenes may be migrated.", nameof(path));
        var alreadyOpen = SceneManager.GetSceneByPath(path);
        if (alreadyOpen.IsValid() && alreadyOpen.isLoaded) throw new InvalidOperationException("Close the target scene first; never overwrite open work.");
        var catalog = Required(AssetDatabase.LoadAssetAtPath<ItemPresentationCatalogSO>(CatalogPath), "populated catalog");
        var active = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            // Verify every existing transform/component identity before allowing a save.
            var before = All<Transform>(scene).ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale, t.parent));
            var components = before.Keys.ToDictionary(t => t, t => t.GetComponents<Component>().Select(c => c != null ? c.GetType().FullName : "missing").ToArray());
            var transforms = before.Keys.ToArray();
            Transform Named(string name) => transforms.Single(t => t.name == name);
            if (path != "Assets/Scenes/LobbyScene.unity")
            {
                var root = All<MatchCompositionRoot>(scene).Single();
                var existing = All<MatchViewBindings>(scene);
                if (existing.Length > 1) throw new InvalidOperationException("Duplicate scene view bindings.");
                MatchViewBindings views;
                if (existing.Length == 1) views = existing[0];
                else
                {
                    var go = new GameObject("MatchViewBindings"); SceneManager.MoveGameObjectToScene(go, scene);
                    views = go.AddComponent<MatchViewBindings>();
                }
                var so = new SerializedObject(views);
                Reference(so, "catalog", catalog);
                Reference(so, "gameplayCamera", All<Camera>(scene).Single(c => c.CompareTag("MainCamera")));
                Reference(so, "fpsSpawn", Named("FPSAnimSpawn")); Reference(so, "iceboxSpawn", Named("BoxSpawnPoint"));
                Reference(so, "localStayItem", Named("PlayerStayItem")); Reference(so, "opponentStayItem", Named("EnemyStayItem"));
                References(so, "localItems", Enumerable.Range(1, 12).Select(i => Named("PlayerItem" + i)));
                References(so, "opponentItems", Enumerable.Range(1, 12).Select(i => Named("EnemyItem" + i)));
                References(so, "remoteVisuals", path.Contains("_Multi") ? Enumerable.Range(0, 3).Select(i => Named("EnemyPlayer_" + i)) : new[] { Named("EnemyPlayer") });
                Reference(so, "map", All<MapSceneBinding>(scene).Single());
                Reference(so, "fpsController", Required(Resources.Load<RuntimeAnimatorController>("FPS/FPSA"), "FPS controller"));
                Reference(so, "authoredFps", All<FPSVisualController>(scene).SingleOrDefault());
                var sprites = so.FindProperty("sprites"); sprites.arraySize = SharedPaths.Length;
                for (int i = 0; i < SharedPaths.Length; i++)
                {
                    var entry = sprites.GetArrayElementAtIndex(i); entry.FindPropertyRelative("role").enumValueIndex = i;
                    entry.FindPropertyRelative("sprite").objectReferenceValue = Required(Resources.Load<Sprite>(SharedPaths[i]), SharedPaths[i]);
                }
                var fpsMap = new Dictionary<string, Sprite>();
                foreach (var name in new[] { "gun", "tape", "fan", "mask", "card", "eat", "hug" })
                    fpsMap[name] = Required(Resources.LoadAll<Sprite>("FPS/FPS_" + name).FirstOrDefault(), "FPS " + name);
                fpsMap["swing"] = fpsMap["fan"]; fpsMap["defence"] = fpsMap["mask"]; fpsMap["use"] = fpsMap["gun"]; fpsMap["feed"] = fpsMap["eat"];
                var fps = so.FindProperty("fpsSprites"); fps.arraySize = fpsMap.Count; int n = 0;
                foreach (var pair in fpsMap)
                {
                    var entry = fps.GetArrayElementAtIndex(n++); entry.FindPropertyRelative("trigger").stringValue = pair.Key;
                    entry.FindPropertyRelative("sprite").objectReferenceValue = pair.Value;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                var errors = new List<string>(); if (!views.Validate(All<ItemManager>(scene).Single().GetAllItems(), errors)) throw new InvalidOperationException(string.Join("; ", errors));
                var rootSo = new SerializedObject(root); Reference(rootSo, "viewBindings", views);
                rootSo.FindProperty("requireViewBindings").boolValue = true; rootSo.ApplyModifiedPropertiesWithoutUndo();
            }
            // Loading exists before a match root and therefore receives explicit asset references.
            foreach (var loading in All<LoadingScreenManager>(scene))
            {
                var so = new SerializedObject(loading); Reference(so, "presentationCatalog", catalog);
                var tips = (Array)typeof(LoadingScreenManager).GetField("_tips", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var items = AssetDatabase.FindAssets("t:ItemDataSO", new[] { "Assets/Data/Items" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<ItemDataSO>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
                References(so, "tipItems", tips.Cast<object>().Select(t => items.Single(i => i.ItemName == (string)t.GetType().GetField("ItemName").GetValue(t))));
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var pair in before)
            {
                var t = pair.Key;
                if ((t.localPosition, t.localRotation, t.localScale, t.parent) != pair.Value
                    || !t.GetComponents<Component>().Select(c => c != null ? c.GetType().FullName : "missing").SequenceEqual(components[t]))
                    throw new InvalidOperationException("Existing transform/component changed: " + t.name);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            return $"Migrated {path}; {before.Count} transforms and component orders preserved.";
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }
}
#endif
