using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbsoluteZero.Core.Maps;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AbsoluteZero.EditorTools.Maps
{
    public static class MapPresetAuthoring
    {
        public static void ValidateDefinition(MapDefinitionSO map)
        {
            if (map == null || string.IsNullOrWhiteSpace(map.Id) || map.Lighting == null)
                throw new InvalidOperationException("Select a map definition with a stable ID and lighting settings.");
            var prefab = map.EnvironmentPrefab;
            if (prefab == null || !PrefabUtility.IsPartOfPrefabAsset(prefab) || prefab.transform.parent != null)
                throw new InvalidOperationException("The map must reference a prefab root asset.");
            if (!prefab.activeSelf || prefab.transform.localPosition != Vector3.zero ||
                prefab.transform.localRotation != Quaternion.identity || prefab.transform.localScale != Vector3.one)
                throw new InvalidOperationException("The map prefab root must be active with position/rotation zero and scale one.");
            if (prefab.GetComponentsInChildren<NetworkObject>(true).Length != 0 ||
                prefab.GetComponentsInChildren<NetworkBehaviour>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Camera>(true).Length != 0 ||
                prefab.GetComponentsInChildren<AudioListener>(true).Length != 0 ||
                prefab.GetComponentsInChildren<MapSceneBinding>(true).Length != 0)
                throw new InvalidOperationException("Map prefabs must not contain network objects, gameplay cameras/listeners or map bindings.");
            if (prefab.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0))
                throw new InvalidOperationException("Map prefab contains a missing script.");
            if (prefab.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Any(m => m == null)))
                throw new InvalidOperationException("Map prefab contains an unassigned material.");
            if (DirectionalLights(prefab).Length != 1)
                throw new InvalidOperationException("A map must have exactly one active directional light for the weather presenter.");
            var layout = prefab.GetComponent<MapCharacterLayout>();
            if (layout != null)
                foreach (var mode in new[] { MapLayoutMode.Duel, MapLayoutMode.Multi })
                    if (!layout.TryValidateMode(mode, out var error)) throw new InvalidOperationException(error);
            var light = map.Lighting;
            if (!Finite(light.AmbientIntensity) || light.AmbientIntensity < 0 ||
                !Finite(light.ReflectionIntensity) || light.ReflectionIntensity < 0 ||
                !Finite(light.FogDensity) || light.FogDensity < 0 ||
                !Finite(light.FogStart) || !Finite(light.FogEnd) || light.FogEnd < light.FogStart)
                throw new InvalidOperationException("Map lighting contains invalid intensity or fog values.");
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static Light[] DirectionalLights(GameObject root) => root.GetComponentsInChildren<Light>(true)
            .Where(l => l.type == LightType.Directional && l.enabled && ActiveWithin(l.transform, root.transform)).ToArray();

        static bool ActiveWithin(Transform node, Transform root)
        {
            for (var t = node; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t == root) return true;
            }
            return false;
        }

        public static MapSceneBinding GetBinding(Scene scene)
        {
            var bindings = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapSceneBinding>(true)).ToArray();
            if (bindings.Length != 1) throw new InvalidOperationException($"{scene.name}: expected one MapSceneBinding, found {bindings.Length}.");
            return bindings[0];
        }

        public static void ValidateBinding(MapSceneBinding binding)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply maps in Edit Mode, before starting a match.");
            if (binding == null || EditorUtility.IsPersistent(binding) || !binding.gameObject.scene.IsValid() || !binding.gameObject.scene.isLoaded)
                throw new InvalidOperationException("Select a map binding in a loaded scene.");
            if (!binding.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The map binding must be active in the scene.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Return to the scene before applying a map.");
            if (binding.transform.position != Vector3.zero || binding.transform.rotation != Quaternion.identity || binding.transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("Map binding must keep world position/rotation zero and scale one.");
            var old = binding.EnvironmentInstance;
            if (old != null && (old.transform.parent != binding.transform || old.scene != binding.gameObject.scene))
                throw new InvalidOperationException("The previous environment is not a direct child owned by this binding.");
            if (binding.transform.childCount != (old == null ? 0 : 1))
                throw new InvalidOperationException("Map binding contains extra children. Keep gameplay objects outside it.");
            if (binding.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>())
                .Any(l => l.enabled && l.type == LightType.Directional && (old == null || !l.transform.IsChildOf(old.transform))))
                throw new InvalidOperationException("Move the scene's directional light inside the map so weather uses exactly one light.");
            if (old == null) return;
            var owned = new HashSet<Object>();
            foreach (var t in old.GetComponentsInChildren<Transform>(true))
            {
                owned.Add(t.gameObject);
                foreach (var component in t.GetComponents<Component>()) if (component != null) owned.Add(component);
            }
            foreach (var root in binding.gameObject.scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component == binding || component == binding.transform || owned.Contains(component)) continue;
                using var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && owned.Contains(property.objectReferenceValue))
                        throw new InvalidOperationException($"{component.name}.{property.propertyPath} references the current map. Remove or rebind that external dependency first.");
            }
        }

        static Object RenderSettingsObject()
        {
            // Unity 6.3's serialized scene settings have no public Undo accessor. Editor-only, verified before mutation.
            var method = typeof(RenderSettings).GetMethod("GetRenderSettings", BindingFlags.Static | BindingFlags.NonPublic);
            var settings = method?.Invoke(null, null) as Object;
            if (settings == null) throw new InvalidOperationException("This Editor does not expose RenderSettings for Undo; map was not changed.");
            return settings;
        }

        public static void Apply(MapSceneBinding binding, MapDefinitionSO map)
        {
            ValidateDefinition(map);
            ValidateBinding(binding);
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(binding.gameObject.scene);
            try
            {
                var settings = RenderSettingsObject();
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Apply map: " + map.DisplayName);
                try
                {
                    var next = (GameObject)PrefabUtility.InstantiatePrefab(map.EnvironmentPrefab, binding.gameObject.scene);
                    Undo.RegisterCreatedObjectUndo(next, "Map instance");
                    Undo.SetTransformParent(next.transform, binding.transform, "Map owner");
                    next.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    next.transform.localScale = Vector3.one;
                    // Redo must recreate the target before restoring references to it.
                    Undo.RegisterCompleteObjectUndo(settings, "Map lighting");
                    Undo.RegisterCompleteObjectUndo(binding, "Map definition");
                    var old = binding.EnvironmentInstance;
                    var serialized = new SerializedObject(binding);
                    serialized.FindProperty("_appliedMap").objectReferenceValue = map;
                    serialized.FindProperty("_environmentInstance").objectReferenceValue = next;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (old != null) Undo.DestroyObjectImmediate(old);
                    ApplyLighting(map.Lighting, DirectionalLights(next).Single());
                    EditorUtility.SetDirty(settings);
                    EditorUtility.SetDirty(binding);
                    EditorSceneManager.MarkSceneDirty(binding.gameObject.scene);
                    Undo.FlushUndoRecordObjects();
                    Undo.CollapseUndoOperations(group);
                    Undo.IncrementCurrentGroup();
                }
                catch
                {
                    Undo.RevertAllDownToGroup(group);
                    throw;
                }
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }

        public static MapLightingSettings CaptureLighting()
        {
            return new MapLightingSettings {
                AmbientMode = RenderSettings.ambientMode, AmbientSkyColor = RenderSettings.ambientSkyColor,
                AmbientEquatorColor = RenderSettings.ambientEquatorColor, AmbientGroundColor = RenderSettings.ambientGroundColor,
                AmbientIntensity = RenderSettings.ambientIntensity, Skybox = RenderSettings.skybox,
                ReflectionIntensity = RenderSettings.reflectionIntensity, ReflectionMode = RenderSettings.defaultReflectionMode,
                CustomReflection = RenderSettings.customReflectionTexture as Cubemap,
                ReflectionResolution = RenderSettings.defaultReflectionResolution, ReflectionBounces = RenderSettings.reflectionBounces,
                Fog = RenderSettings.fog, FogColor = RenderSettings.fogColor, FogMode = RenderSettings.fogMode,
                FogDensity = RenderSettings.fogDensity, FogStart = RenderSettings.fogStartDistance, FogEnd = RenderSettings.fogEndDistance,
                SubtractiveShadowColor = RenderSettings.subtractiveShadowColor, UseMapDirectionalAsSun = RenderSettings.sun != null
            };
        }

        static void ApplyLighting(MapLightingSettings value, Light sun)
        {
            RenderSettings.ambientMode = value.AmbientMode; RenderSettings.ambientSkyColor = value.AmbientSkyColor;
            RenderSettings.ambientEquatorColor = value.AmbientEquatorColor; RenderSettings.ambientGroundColor = value.AmbientGroundColor;
            RenderSettings.ambientIntensity = value.AmbientIntensity; RenderSettings.skybox = value.Skybox;
            RenderSettings.defaultReflectionMode = value.ReflectionMode; RenderSettings.customReflectionTexture = value.CustomReflection;
            RenderSettings.defaultReflectionResolution = value.ReflectionResolution; RenderSettings.reflectionBounces = value.ReflectionBounces;
            RenderSettings.reflectionIntensity = value.ReflectionIntensity; RenderSettings.fog = value.Fog;
            RenderSettings.fogColor = value.FogColor; RenderSettings.fogMode = value.FogMode; RenderSettings.fogDensity = value.FogDensity;
            RenderSettings.fogStartDistance = value.FogStart; RenderSettings.fogEndDistance = value.FogEnd;
            RenderSettings.subtractiveShadowColor = value.SubtractiveShadowColor;
            RenderSettings.sun = value.UseMapDirectionalAsSun ? sun : null;
        }

        public static void ApplyToRegisteredScenes(MapLibraryAsset library, MapDefinitionSO map)
        {
            ValidateDefinition(map);
            ValidateLibrary(library);
            if (library == null || library.GameplayScenes == null || library.GameplayScenes.Length == 0)
                throw new InvalidOperationException("Register the target scenes in the map library.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("Save open scene changes before applying to the registered scenes.");
            var paths = library.GameplayScenes.Select(s => s == null ? "" : AssetDatabase.GetAssetPath(s)).ToArray();
            if (paths.Any(string.IsNullOrEmpty) || paths.Distinct().Count() != paths.Length)
                throw new InvalidOperationException("The map library has missing or duplicate scene entries.");
            var opened = new List<Scene>();
            var bindings = new List<MapSceneBinding>();
            try
            {
                // Validate every destination before any map instance is changed.
                foreach (var path in paths)
                {
                    var scene = SceneManager.GetSceneByPath(path);
                    if (!scene.IsValid() || !scene.isLoaded) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); opened.Add(scene); }
                    var binding = GetBinding(scene); ValidateBinding(binding); bindings.Add(binding);
                }
                foreach (var binding in bindings) Apply(binding, map);
                foreach (var binding in bindings)
                    if (!EditorSceneManager.SaveScene(binding.gameObject.scene))
                        throw new InvalidOperationException("Could not save " + binding.gameObject.scene.path);
            }
            finally
            {
                // Never discard an unsaved result on a failed batch. Leave it open for recovery.
                foreach (var scene in opened) if (scene.IsValid() && scene.isLoaded && !scene.isDirty) EditorSceneManager.CloseScene(scene, true);
                var active = setup.FirstOrDefault(s => s.isActive);
                if (active != null && !string.IsNullOrEmpty(active.path)) { var scene = SceneManager.GetSceneByPath(active.path); if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene); }
            }
        }

        public static void ValidateLibrary(MapLibraryAsset library)
        {
            if (library == null || library.Maps == null || library.Maps.Length == 0 || library.Maps.Any(m => m == null))
                throw new InvalidOperationException("Register valid map definitions in the library.");
            foreach (var map in library.Maps) ValidateDefinition(map);
            if (library.Maps.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != library.Maps.Length)
                throw new InvalidOperationException("Each map in the library must have a unique stable ID.");
        }
    }
}
