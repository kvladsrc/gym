using Procedural.Generation;
using UnityEngine;

namespace Procedural.World
{
    /// <summary>A level: a seed and the parameters of DESIGN.md "Recipe".</summary>
    [CreateAssetMenu(menuName = "Procedural/Island Recipe", fileName = "IslandRecipe")]
    public class IslandRecipeAsset : ScriptableObject
    {
        public IslandRecipe recipe = new IslandRecipe();
    }
}
