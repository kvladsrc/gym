using UnityEngine;
using UnityEngine.Rendering;

namespace Realms
{
    /// <summary>
    /// Dust round the foot of a building that has just risen out of the ground (the visual plan
    /// of 2026-11-11, item 2): a ring of soft puffs that roll outward and fade in under a second.
    /// One mesh holds a pool of puffs; a ring takes the oldest ones and writes where and when they
    /// start, and the Realms/Puff shader moves them by the time, so nothing else changes per frame.
    /// </summary>
    public sealed class Puffs : MonoBehaviour
    {
        /// <summary>The material (MapView sets it); without it no dust.</summary>
        public static Material Material;

        /// <summary>Puffs in the air at once, over every ring.</summary>
        const int Pool = 96;

        /// <summary>How long a puff lives, in seconds.</summary>
        const float Life = 0.8f;

        static readonly int NowId = Shader.PropertyToID("_RealmsPuffNow");
        static readonly Vector2[] Corners = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
        static Puffs instance;

        Mesh mesh;
        readonly Vector3[] positions = new Vector3[Pool * 4];
        readonly Vector4[] puffs = new Vector4[Pool * 4];
        readonly Vector2[] paths = new Vector2[Pool * 4];
        readonly Color[] colours = new Color[Pool * 4];
        int next;
        uint seed = 0x9E3779B9u;

        /// <summary>
        /// A ring of dust round <paramref name="foot"/> (on the ground, in world space), reaching
        /// about <paramref name="radius"/> out.
        /// </summary>
        public static void Ring(Vector3 foot, float radius, int count = 10) => Ring(foot, radius, count, Color.clear);

        /// <summary>
        /// A ring of puffs of <paramref name="colour"/> (a splash in the shallows); a clear colour
        /// draws dust.
        /// </summary>
        public static void Ring(Vector3 foot, float radius, int count, Color colour)
        {
            if (Material == null || radius <= 0f)
                return;
            if (instance == null)
                instance = new GameObject("Dust").AddComponent<Puffs>();
            instance.Emit(foot, radius, count, colour);
        }

        void Awake()
        {
            var corners = new Vector2[Pool * 4];
            var indices = new int[Pool * 6];
            for (int p = 0; p < Pool; p++)
            {
                for (int k = 0; k < 4; k++)
                {
                    corners[p * 4 + k] = Corners[k];
                    // Born long ago: folded into a point until a ring takes it.
                    puffs[p * 4 + k] = new Vector4(-1000f, 0f, 0f, 0f);
                    paths[p * 4 + k] = new Vector2(0f, Life);
                }
                int first = p * 4;
                indices[p * 6] = first;
                indices[p * 6 + 1] = first + 2;
                indices[p * 6 + 2] = first + 1;
                indices[p * 6 + 3] = first;
                indices[p * 6 + 4] = first + 3;
                indices[p * 6 + 5] = first + 2;
            }
            mesh = new Mesh { name = "Dust", indexFormat = IndexFormat.UInt16 };
            mesh.MarkDynamic();
            mesh.vertices = positions;
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, puffs);
            mesh.SetUVs(2, paths);
            // Clear: the dust's own colour (Puff.shader).
            mesh.colors = colours;
            mesh.SetTriangles(indices, 0);
            // The puffs may be anywhere on the map: the bounds never cull them.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Shader.SetGlobalFloat(NowId, Time.time);
        }

        void Update() => Shader.SetGlobalFloat(NowId, Time.time);

        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }

        void Emit(Vector3 foot, float radius, int count, Color colour)
        {
            float now = Time.time;
            Shader.SetGlobalFloat(NowId, now);
            float turn = Next() * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                int p = next;
                next = (next + 1) % Pool;
                float angle = turn + (i + 0.35f * (Next() - 0.5f)) * Mathf.PI * 2f / count;
                // A few start a moment later, so the ring is not one even circle.
                var puff = new Vector4(now + 0.06f * Next(), Next(), radius * (0.32f + 0.16f * Next()), angle);
                var path = new Vector2(radius * (0.85f + 0.3f * Next()), Life * (0.8f + 0.4f * Next()));
                for (int k = 0; k < 4; k++)
                {
                    positions[p * 4 + k] = foot;
                    puffs[p * 4 + k] = puff;
                    paths[p * 4 + k] = path;
                    colours[p * 4 + k] = colour;
                }
            }
            mesh.vertices = positions;
            mesh.SetUVs(1, puffs);
            mesh.SetUVs(2, paths);
            mesh.colors = colours;
        }

        /// <summary>The next of a few random numbers in 0..1 (an integer hash, as the shaders' are).</summary>
        float Next()
        {
            seed = seed * 747796405u + 2891336453u;
            uint word = ((seed >> (int)((seed >> 28) + 4u)) ^ seed) * 277803737u;
            return ((word >> 22) ^ word) / 4294967296f;
        }
    }
}
