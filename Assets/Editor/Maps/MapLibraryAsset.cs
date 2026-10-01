using AbsoluteZero.Core.Maps;
using UnityEditor;
using UnityEngine;

namespace AbsoluteZero.EditorTools.Maps
{
    [CreateAssetMenu(menuName = "Absolute Zero/Maps/Authoring Library", fileName = "MapLibrary")]
    public sealed class MapLibraryAsset : ScriptableObject
    {
        [Tooltip("Renderer-only editor preview. Actual participants keep their existing network prefab and cosmetics.")]
        public GameObject CharacterPreviewPrefab;
        public MapDefinitionSO[] Maps = System.Array.Empty<MapDefinitionSO>();
        public SceneAsset[] GameplayScenes = System.Array.Empty<SceneAsset>();
    }
}
