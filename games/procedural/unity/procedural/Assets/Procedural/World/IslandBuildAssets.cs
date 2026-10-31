using Procedural.Generation;
using UnityEngine;

namespace Procedural.World
{
    /// <summary>
    /// What a built level is drawn with: terrain layers in SurfaceLayer order
    /// and water materials. Placeholders now, Studio assets later.
    /// </summary>
    public class IslandBuildAssets : ScriptableObject
    {
        public TerrainLayer[] layers = new TerrainLayer[SurfaceLayer.Count];
        public Material terrain;
        public Material sea;
        public Material river;
        public Material lake;
        [Tooltip("Prefabs for the generator's species; without it the level has no plants.")]
        public SpeciesCatalog species;

        public bool IsComplete
        {
            get
            {
                if (layers == null || layers.Length != SurfaceLayer.Count) return false;
                foreach (var layer in layers)
                    if (layer == null) return false;
                return terrain != null && sea != null && river != null && lake != null;
            }
        }
    }
}
