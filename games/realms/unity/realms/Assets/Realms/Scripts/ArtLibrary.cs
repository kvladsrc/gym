using System;
using System.Collections.Generic;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The art fetched from Assets Studio (tools/fetch_art.py) as the editor's Setup prepared it:
    /// model prefabs (pivot at the bottom centre, footprint 1) and icons. Lives in
    /// Art/Resources/RealmsArt.asset, which is not in Git; without it the map uses its own simple
    /// houses and text instead of icons.
    /// </summary>
    public sealed class ArtLibrary : ScriptableObject
    {
        [Serializable]
        public struct Model
        {
            public string name;
            public GameObject prefab;
        }

        [Serializable]
        public struct Icon
        {
            public string name;
            public Texture2D texture;
        }

        public List<Model> models = new List<Model>();
        public List<Icon> icons = new List<Icon>();

        public static ArtLibrary Load() => Resources.Load<ArtLibrary>("RealmsArt");

        public GameObject ModelOf(string name)
        {
            foreach (Model model in models)
            {
                if (model.name == name)
                    return model.prefab;
            }
            return null;
        }

        public Texture2D IconOf(string name)
        {
            foreach (Icon icon in icons)
            {
                if (icon.name == name)
                    return icon.texture;
            }
            return null;
        }
    }
}
