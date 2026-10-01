using System;
using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using UnityEditor;
using UnityEngine;

namespace AbsoluteZero.EditorTools
{
    public static class RemainingHatRegistration
    {
        const string Folder = "Assets/Data/Cosmetics/AtlasBindings/";

        [MenuItem("Absolute Zero/Cosmetics/Register Remaining Hats")]
        public static void Register()
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/gameGem/MainFolder/Sprite/Customizing/hat.png")
                .OfType<Sprite>().ToDictionary(s => s.name);
            var registry = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            if (registry == null) throw new InvalidOperationException("Cosmetic registry missing.");
            // Preserve hat_01..04 and their previously approved fit. Coordinates are relative to body/head.
            var entries = new[]
            {
                (id: "hat_05", sprite: "hat_0", name: "산타 모자", x: 0f, y: .052f, scale: 1.18f),
                (id: "hat_06", sprite: "hat_1", name: "방울 산타 모자", x: 0f, y: .066f, scale: 1.18f),
                (id: "hat_07", sprite: "hat_3", name: "별 고깔", x: 0f, y: .682f, scale: 1.1f),
                (id: "hat_08", sprite: "hat_7", name: "고양이 귀", x: 0f, y: .48f, scale: .9f),
                (id: "hat_09", sprite: "hat_9", name: "두건", x: .06f, y: .54f, scale: 1.05f),
                (id: "hat_10", sprite: "hat_10", name: "천사 링", x: 0f, y: .88f, scale: 1f),
            };
            foreach (var entry in entries)
                if (!sprites.ContainsKey(entry.sprite)) throw new InvalidOperationException("Missing slice " + entry.sprite);
            var serialized = new SerializedObject(registry);
            var items = serialized.FindProperty("_allItems");
            foreach (var entry in entries)
            {
                var atlas = AssetDatabase.LoadAssetAtPath<CosmeticAtlasSO>(Folder + entry.id + "_atlas.asset");
                if (atlas == null)
                {
                    atlas = ScriptableObject.CreateInstance<CosmeticAtlasSO>();
                    AssetDatabase.CreateAsset(atlas, Folder + entry.id + "_atlas.asset");
                    atlas.Bindings = new List<CosmeticAtlasBinding> { new CosmeticAtlasBinding {
                        Note = "Authored hat atlas slice; adjust fitting in this asset.",
                        RendererPath = "body/head", Overlay = true, Replacement = sprites[entry.sprite],
                        LocalPosition = new Vector3(entry.x, entry.y, 0), LocalScale = Vector3.one * entry.scale,
                        SortOffset = 4,
                    }};
                    EditorUtility.SetDirty(atlas);
                    AssetDatabase.SaveAssetIfDirty(atlas);
                }
                var item = AssetDatabase.LoadAssetAtPath<CosmeticItemSO>(Folder + entry.id + ".asset");
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<CosmeticItemSO>();
                    AssetDatabase.CreateAsset(item, Folder + entry.id + ".asset");
                    var data = new SerializedObject(item);
                    data.FindProperty("_id").stringValue = entry.id;
                    data.FindProperty("_part").enumValueIndex = (int)CosmeticPart.Head;
                    data.FindProperty("_type").enumValueIndex = (int)CosmeticType.Overlay;
                    data.FindProperty("_displayName").stringValue = entry.name;
                    data.FindProperty("_sprite").objectReferenceValue = sprites[entry.sprite];
                    data.FindProperty("_atlas").objectReferenceValue = atlas;
                    data.ApplyModifiedProperties();
                    AssetDatabase.SaveAssetIfDirty(item);
                }
                if (!Enumerable.Range(0, items.arraySize).Any(i => items.GetArrayElementAtIndex(i).objectReferenceValue == item))
                    items.GetArrayElementAtIndex(items.arraySize++).objectReferenceValue = item;
            }
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(registry);
            registry.RefreshEditorLookup();
        }
    }
}
