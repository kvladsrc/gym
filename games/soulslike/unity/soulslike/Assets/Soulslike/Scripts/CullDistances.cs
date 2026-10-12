using UnityEngine;

namespace Soulslike
{
    /// <summary>Per-layer draw distances for the camera (not saved with a
    /// scene, so set at start): the undergrowth is not drawn far off, the
    /// trees not beyond the fog. A level of thousands of plants drew 7 M
    /// triangles a frame without it.</summary>
    [RequireComponent(typeof(Camera))]
    public class CullDistances : MonoBehaviour
    {
        public const int Undergrowth = 30, Trees = 29;
        public float undergrowth = 45f, trees = 140f;

        void Awake()
        {
            var camera = GetComponent<Camera>();
            var distances = new float[32];
            distances[Undergrowth] = undergrowth;
            distances[Trees] = trees;
            camera.layerCullDistances = distances;
            camera.layerCullSpherical = true;
        }
    }
}
