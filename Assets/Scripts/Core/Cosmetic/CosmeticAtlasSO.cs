using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Cosmetic
{
    public enum CosmeticView { Character, FirstPerson }

    [Serializable]
    public class CosmeticAtlasBinding
    {
        public string Note;
        public CosmeticView View;
        [Tooltip("Path relative to the character root, e.g. body/arm1")]
        public string RendererPath;
        public bool Overlay;
        [Tooltip("Original pose sprite. Null is allowed only for an always-visible overlay.")]
        public Sprite Source;
        [Tooltip("Drop the matching slice from your new atlas here. Empty keeps the original.")]
        public Sprite Replacement;
        public Vector3 LocalPosition;
        public Vector3 LocalScale = Vector3.one;
        public float Rotation;
        public int SortOffset;
    }

    [CreateAssetMenu(menuName = "AbsoluteZero/Cosmetic Atlas", fileName = "CosmeticAtlas")]
    public sealed class CosmeticAtlasSO : ScriptableObject
    {
        public List<CosmeticAtlasBinding> Bindings = new();
    }
}
