using UnityEngine;

namespace FinishTheGame
{
    /// <summary>The land's gentle relief: low rolling hills (two octaves of
    /// noise, up to a few metres), flat along the road (fading in from
    /// <see cref="RoadFlat"/> to <see cref="RoadFull"/> metres off it) and on
    /// the places (the village, the city, the farm, the cave, the lake), so
    /// that the road, the squares and the hero's way stay level.</summary>
    public static class Land
    {
        public const float RoadFlat = 6f, RoadFull = 30f;
        const float Cell = 5f, Reach = 60f;

        static float[] distance;
        static int cells;
        static float half;
        static (Vector2 at, float radius)[] flats = { };

        /// <summary>The distance to the road, on a grid over the plane (the road
        /// stamped onto it sample by sample), and the flat places.</summary>
        public static void Build(System.Collections.Generic.IEnumerable<StoryPath> routes, float halfSide, params (Vector2 at, float radius)[] places)
        {
            half = halfSide;
            cells = Mathf.CeilToInt(2 * half / Cell) + 1;
            distance = new float[cells * cells];
            for (int i = 0; i < distance.Length; i++) distance[i] = Reach;
            int r = Mathf.CeilToInt(Reach / Cell);
            foreach (var route in routes)
            for (float s = 0; s <= route.Length; s += 2f)
            {
                var p = route.Position(s);
                int cx = Mathf.RoundToInt((p.x + half) / Cell), cz = Mathf.RoundToInt((p.z + half) / Cell);
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= cells || z >= cells) continue;
                        float d = new Vector2(x * Cell - half - p.x, z * Cell - half - p.z).magnitude;
                        int k = z * cells + x;
                        if (d < distance[k]) distance[k] = d;
                    }
            }
            flats = places;
        }

        static float RoadDistance(float x, float z)
        {
            if (distance == null) return Reach;
            float fx = Mathf.Clamp((x + half) / Cell, 0, cells - 1.001f), fz = Mathf.Clamp((z + half) / Cell, 0, cells - 1.001f);
            int ix = (int)fx, iz = (int)fz;
            float tx = fx - ix, tz = fz - iz;
            float a = Mathf.Lerp(distance[iz * cells + ix], distance[iz * cells + ix + 1], tx);
            float b = Mathf.Lerp(distance[(iz + 1) * cells + ix], distance[(iz + 1) * cells + ix + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        /// <summary>The ground's height at (x, z), m.</summary>
        public static float Height(float x, float z)
        {
            if (distance == null) return 0f;
            float hills = 6.5f * (Mathf.PerlinNoise(x / 130f + 31.7f, z / 130f + 12.3f) - 0.42f) * 2f
                        + 1.8f * (Mathf.PerlinNoise(x / 38f + 5.1f, z / 38f + 77.9f) - 0.5f) * 2f;
            float keep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoadFlat, RoadFull, RoadDistance(x, z)));
            foreach (var (at, radius) in flats)
                keep *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius, radius * 1.6f, Vector2.Distance(new Vector2(x, z), at)));
            return hills * keep;
        }
    }
}
