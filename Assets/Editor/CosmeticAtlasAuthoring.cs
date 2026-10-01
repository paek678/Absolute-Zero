using System;
using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace AbsoluteZero.EditorTools
{
    public static class CosmeticAtlasAuthoring
    {
        const string Folder = "Assets/Data/Cosmetics/AtlasBindings";
        const string Prefab = "Assets/MainFolder/Sprite/player.prefab";

        [MenuItem("Absolute Zero/Cosmetics/Create Missing Reference Templates")]
        public static void CreateTemplates()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data/Cosmetics", "AtlasBindings");
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (reference == null) throw new InvalidOperationException("Reference player prefab missing");
            var items = new List<CosmeticItemSO>();
            foreach (CosmeticPart part in Enum.GetValues(typeof(CosmeticPart)))
            {
                string id = part switch { CosmeticPart.Head => "head_ref", CosmeticPart.Top => "top_ref", CosmeticPart.Back => "back_ref", CosmeticPart.Bottom => "low_ref", _ => "tail_ref" };
                string path = Folder + "/" + id + "_atlas.asset";
                var atlas = AssetDatabase.LoadAssetAtPath<CosmeticAtlasSO>(path);
                if (atlas == null)
                {
                    atlas = ScriptableObject.CreateInstance<CosmeticAtlasSO>();
                    foreach (var sr in reference.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        string rendererPath = AnimationUtility.CalculateTransformPath(sr.transform, reference.transform);
                        bool include = part switch {
                            CosmeticPart.Head => rendererPath.StartsWith("body/head", StringComparison.Ordinal),
                            CosmeticPart.Top => rendererPath == "body" || rendererPath == "body/arm1" || rendererPath == "body/arm2",
                            CosmeticPart.Bottom => rendererPath == "lowerbody",
                            _ => false };
                        if (!include) continue;
                        var sprites = new HashSet<Sprite>();
                        AddSource(atlas, rendererPath, sr.sprite, "Static/default", sprites);
                        var resolver = sr.GetComponent<SpriteResolver>();
                        var library = sr.GetComponent<SpriteLibrary>()?.spriteLibraryAsset;
                        if (resolver != null && library != null)
                        {
                            string category = resolver.GetCategory();
                            foreach (string label in library.GetCategoryLabelNames(category))
                                AddSource(atlas, rendererPath, library.GetSprite(category, label), category + "/" + label, sprites);
                        }
                        foreach (var clip in reference.GetComponent<Animator>().runtimeAnimatorController.animationClips.Distinct())
                            foreach (var curve in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                                if (curve.path == rendererPath && curve.type == typeof(SpriteRenderer) && curve.propertyName == "m_Sprite")
                                    foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, curve))
                                        AddSource(atlas, rendererPath, key.value as Sprite, clip.name, sprites);
                    }
                    if (part == CosmeticPart.Top) AddFirstPerson(atlas);
                    if (part == CosmeticPart.Head || part == CosmeticPart.Back || part == CosmeticPart.Tail)
                        atlas.Bindings.Add(new CosmeticAtlasBinding {
                            Note = "Optional overlay; assign Replacement and fit position/scale",
                            RendererPath = part == CosmeticPart.Head ? "body/head" : part == CosmeticPart.Back ? "body" : "lowerbody",
                            Overlay = true, SortOffset = part == CosmeticPart.Back ? -1 : 10 });
                    AssetDatabase.CreateAsset(atlas, path);
                }
                string itemPath = Folder + "/" + id + ".asset";
                var item = AssetDatabase.LoadAssetAtPath<CosmeticItemSO>(itemPath);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<CosmeticItemSO>();
                    var so = new SerializedObject(item);
                    so.FindProperty("_id").stringValue = id;
                    so.FindProperty("_part").enumValueIndex = (int)part;
                    so.FindProperty("_displayName").stringValue = part + " Atlas Template";
                    so.FindProperty("_atlas").objectReferenceValue = atlas;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(item, itemPath);
                }
                items.Add(item);
            }
            var registry = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            Undo.RecordObject(registry, "Register cosmetic atlas templates");
            var data = new SerializedObject(registry);
            var list = data.FindProperty("_allItems");
            foreach (var item in items)
            {
                bool present = Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == item);
                if (!present) { int i = list.arraySize++; list.GetArrayElementAtIndex(i).objectReferenceValue = item; }
            }
            data.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("Cosmetic atlas templates ready; existing templates were preserved.");
        }

        static void AddSource(CosmeticAtlasSO atlas, string path, Sprite sprite, string note, HashSet<Sprite> seen,
            CosmeticView view = CosmeticView.Character)
        {
            if (sprite == null || !seen.Add(sprite)) return;
            atlas.Bindings.Add(new CosmeticAtlasBinding { RendererPath = path, Source = sprite,
                Note = note + " | " + AssetDatabase.GetAssetPath(sprite), View = view });
        }
        static void AddFirstPerson(CosmeticAtlasSO atlas)
        {
            var seen = new Dictionary<string, HashSet<Sprite>>();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Resources/FPS" }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite" || binding.path == "item") continue;
                    if (!seen.TryGetValue(binding.path, out var set)) seen[binding.path] = set = new HashSet<Sprite>();
                    foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                        AddSource(atlas, binding.path, key.value as Sprite, clip.name, set, CosmeticView.FirstPerson);
                }
            }
        }
    }

    [CustomEditor(typeof(CosmeticAtlasSO))]
    public sealed class CosmeticAtlasInspector : UnityEditor.Editor
    {
        Texture2D _sourceAtlas, _replacementAtlas;
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Empty Replacement keeps the original. Source is the ORIGINAL pose, not your new sprite. Overlay offsets affect only the extra layer.", MessageType.Info);
            _sourceAtlas = (Texture2D)EditorGUILayout.ObjectField("Original atlas", _sourceAtlas, typeof(Texture2D), false);
            _replacementAtlas = (Texture2D)EditorGUILayout.ObjectField("Replacement atlas", _replacementAtlas, typeof(Texture2D), false);
            using (new EditorGUI.DisabledScope(_sourceAtlas == null || _replacementAtlas == null))
                if (GUILayout.Button("Fill empty replacements by exact sprite name"))
                {
                    var atlas = (CosmeticAtlasSO)target;
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(_replacementAtlas)).OfType<Sprite>().ToList();
                    Undo.RecordObject(atlas, "Fill cosmetic atlas bindings");
                    int count = 0;
                    foreach (var binding in atlas.Bindings)
                    {
                        if (binding.Source == null || binding.Source.texture != _sourceAtlas || binding.Replacement != null) continue;
                        var matches = sprites.Where(s => s.name == binding.Source.name).ToList();
                        if (matches.Count == 1) { binding.Replacement = matches[0]; count++; }
                    }
                    EditorUtility.SetDirty(atlas);
                    Debug.Log($"Filled {count} bindings; unmatched/ambiguous names preserved.");
                }
            DrawDefaultInspector();
        }
    }
}
