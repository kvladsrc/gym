using UnityEngine;

namespace Procedural.World
{
    /// <summary>The scene's handle on a level: its recipe and the last build.</summary>
    public class IslandGenerator : MonoBehaviour
    {
        public IslandRecipeAsset recipe;
        public IslandBuildAssets assets;

        [Tooltip("The level built from the recipe; replaced by every bake.")]
        public GameObject level;
    }
}
