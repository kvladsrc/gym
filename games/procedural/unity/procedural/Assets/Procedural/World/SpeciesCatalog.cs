using System;
using System.Collections.Generic;
using UnityEngine;

namespace Procedural.World
{
    /// <summary>How the instances of a species become Unity objects.</summary>
    public enum PlantBuild
    {
        /// <summary>Terrain tree instances (yaw and uniform scale only).</summary>
        TerrainTree,
        /// <summary>Terrain detail layer drawn from the prefab's mesh (instanced).</summary>
        TerrainDetail,
        /// <summary>A GameObject per instance (full rotation), e.g. rocks and logs.</summary>
        SceneObject,
        /// <summary>
        /// Drawn with GPU instancing from the prefab's mesh and materials: no GameObjects and no
        /// colliders. For small, numerous clutter such as stones and hummocks.
        /// </summary>
        Instanced
    }

    [Serializable]
    public class SpeciesVisual
    {
        [Tooltip("Species id used by the generator core, e.g. pine, grass, log.")]
        public string id;
        public PlantBuild build;
        [Tooltip("Pivot at the ground; height and footprint at scale 1 match the core's species table.")]
        public GameObject prefab;
    }

    /// <summary>
    /// Maps generator species to prefabs: primitives now, Studio assets later.
    /// Swapping a prefab here is all a new asset needs.
    /// </summary>
    [CreateAssetMenu(menuName = "Procedural/Species Catalog", fileName = "SpeciesCatalog")]
    public class SpeciesCatalog : ScriptableObject
    {
        public List<SpeciesVisual> species = new List<SpeciesVisual>();

        public SpeciesVisual Find(string id) => species.Find(s => s.id == id);
    }
}
