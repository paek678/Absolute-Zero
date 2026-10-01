using System;
using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace AbsoluteZero.EditorTools
{
    // Adapt the art team's authored pose libraries to the existing networked cosmetic IDs.
    public static class IncomingTopRegistration
    {
        public static readonly string[] Names = { "apron", "maid", "shirt", "swimsuit", "tanktop1", "tanktop2", "training" };
        static readonly string[] DisplayNames = { "앞치마", "메이드복", "셔츠", "수영복", "민소매 1", "민소매 2", "트레이닝복" };
        const string Folder = "Assets/Data/Cosmetics/AtlasBindings/";
        const string SourcePath = "Assets/Art/testProject/LastPrefab2/PlayerSpriteLib.spriteLib";
        public static string LibraryPath(string name) => "Assets/Art/gameGem/MainFolder/Sprite/Customizing/Player_Body_" + name + ".spriteLib";

        [MenuItem("Absolute Zero/Cosmetics/Register Incoming Tops")]
        public static void Register()
        {
            var source = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(SourcePath);
            var registry = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            if (source == null || registry == null) throw new InvalidOperationException("Source library or registry missing.");
            var serialized = new SerializedObject(registry);
            var items = serialized.FindProperty("_allItems");
            for (int i = 0; i < Names.Length; i++)
            {
                string assetName = "top_" + Names[i];
                string id = "top_" + (i + 1).ToString("D2"); // Keep the existing eight-character wire contract.
                var library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(LibraryPath(Names[i]));
                if (library == null) throw new InvalidOperationException("Missing top library: " + Names[i]);
                var bindings = new List<CosmeticAtlasBinding>();
                foreach (var pair in new[] { ("body", "body"), ("body/arm1", "arm_Left"), ("body/arm2", "arm_Right") })
                {
                    var unique = new Dictionary<Sprite, Sprite>();
                    foreach (var label in source.GetCategoryLabelNames(pair.Item2))
                    {
                        var original = source.GetSprite(pair.Item2, label);
                        var replacement = library.GetSprite(pair.Item2, label);
                        if (original == null || replacement == null) throw new InvalidOperationException(id + ": missing " + label);
                        if (unique.TryGetValue(original, out var previous))
                        {
                            if (previous != replacement) throw new InvalidOperationException(id + ": ambiguous source pose " + label);
                            continue;
                        }
                        unique.Add(original, replacement);
                        bindings.Add(new CosmeticAtlasBinding { RendererPath = pair.Item1, Source = original,
                            Replacement = replacement, Note = pair.Item2 + "/" + label + (original == replacement ? " (authored original fallback)" : "") });
                    }
                }
                var atlas = AssetDatabase.LoadAssetAtPath<CosmeticAtlasSO>(Folder + assetName + "_atlas.asset");
                if (atlas == null) { atlas = ScriptableObject.CreateInstance<CosmeticAtlasSO>(); AssetDatabase.CreateAsset(atlas, Folder + assetName + "_atlas.asset"); }
                Undo.RecordObject(atlas, "Register incoming top poses");
                atlas.Bindings = bindings;
                EditorUtility.SetDirty(atlas);
                AssetDatabase.SaveAssetIfDirty(atlas);
                var item = AssetDatabase.LoadAssetAtPath<CosmeticItemSO>(Folder + assetName + ".asset");
                if (item == null) { item = ScriptableObject.CreateInstance<CosmeticItemSO>(); AssetDatabase.CreateAsset(item, Folder + assetName + ".asset"); }
                var data = new SerializedObject(item);
                data.FindProperty("_id").stringValue = id;
                data.FindProperty("_part").enumValueIndex = (int)CosmeticPart.Top;
                data.FindProperty("_type").enumValueIndex = (int)CosmeticType.Swap;
                data.FindProperty("_displayName").stringValue = DisplayNames[i];
                data.FindProperty("_sprite").objectReferenceValue = library.GetSprite("body", "default");
                data.FindProperty("_atlas").objectReferenceValue = atlas;
                data.ApplyModifiedProperties();
                AssetDatabase.SaveAssetIfDirty(item);
                if (!Enumerable.Range(0, items.arraySize).Any(n => items.GetArrayElementAtIndex(n).objectReferenceValue == item))
                    items.GetArrayElementAtIndex(items.arraySize++).objectReferenceValue = item;
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
            registry.RefreshEditorLookup();
        }
    }
}
