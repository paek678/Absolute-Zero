using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbsoluteZero.Core.Maps
{
    [CreateAssetMenu(menuName = "Absolute Zero/Maps/Map Definition", fileName = "MapDefinition")]
    public sealed class MapDefinitionSO : ScriptableObject
    {
        [SerializeField] string _id;
        [SerializeField] string _displayName;
        [SerializeField] GameObject _environmentPrefab;
        [SerializeField] MapLightingSettings _lighting = new();

        public string Id => _id;
        public string DisplayName => _displayName;
        public GameObject EnvironmentPrefab => _environmentPrefab;
        public MapLightingSettings Lighting => _lighting;
    }

    // Source configuration only. Authoring copies these values into the scene's RenderSettings.
    [Serializable]
    public sealed class MapLightingSettings
    {
        public AmbientMode AmbientMode = AmbientMode.Trilight;
        public Color AmbientSkyColor = new(.14f, .20f, .31f);
        public Color AmbientEquatorColor = new(.10f, .14f, .22f);
        public Color AmbientGroundColor = new(.05f, .065f, .10f);
        [Min(0)] public float AmbientIntensity = 1;
        public Material Skybox;
        [Range(0, 1)] public float ReflectionIntensity = .2f;
        public DefaultReflectionMode ReflectionMode = DefaultReflectionMode.Skybox;
        public Cubemap CustomReflection;
        public int ReflectionResolution = 128;
        public int ReflectionBounces = 1;
        public bool Fog;
        public Color FogColor = Color.gray;
        public FogMode FogMode = FogMode.ExponentialSquared;
        [Min(0)] public float FogDensity = .01f;
        public float FogStart;
        public float FogEnd = 300;
        public Color SubtractiveShadowColor = new(.42f, .478f, .627f);
        public bool UseMapDirectionalAsSun = true;
    }
}
