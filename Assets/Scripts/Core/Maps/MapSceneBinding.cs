using UnityEngine;

namespace AbsoluteZero.Core.Maps
{
    // Metadata for a baked scene map. No runtime spawning or second environment-state owner.
    [DisallowMultipleComponent]
    public sealed class MapSceneBinding : MonoBehaviour
    {
        [SerializeField] MapDefinitionSO _appliedMap;
        [SerializeField] GameObject _environmentInstance;

        public MapDefinitionSO AppliedMap => _appliedMap;
        public GameObject EnvironmentInstance => _environmentInstance;
    }
}
