using UnityEngine;

namespace Realms
{
    /// <summary>
    /// Where a studio model meets the ground, in its prefab (the largest side 1 long, the bottom
    /// centre at 0): the box round its parts in the lowest twentieth of its height, and its
    /// height. Setup measures it from the meshes; the map shades the ground round it
    /// (<see cref="ContactShade"/>), so the shade hugs the walls rather than the eaves or a
    /// windmill's sails.
    /// </summary>
    public sealed class ModelFooting : MonoBehaviour
    {
        public Vector2 centre;
        public Vector2 half;
        public float height;
    }
}
