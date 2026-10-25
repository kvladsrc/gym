using System.Collections.Generic;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>A box of the loop, placed by the story's curve: s along it, t
    /// to its right, standing on <see cref="bottom"/>, turned with the road
    /// (size: x across, y up, z along) and then by <see cref="turn"/> degrees.
    /// A solid one has a collider (and cuts the navmesh); a fixed one is not
    /// of the story: it stays where it is, never on the ribbon (the mountain).</summary>
    public struct Block
    {
        public string name;
        public float s, t, bottom, turn;
        public Vector3 size;
        public Color colour;
        public bool solid, fixedPlace;

        public Block(string name, float s, float t, Vector3 size, Color colour, bool solid = true, float bottom = 0f, float turn = 0f, bool fixedPlace = false)
        {
            this.fixedPlace = fixedPlace;
            this.name = name;
            this.s = s;
            this.t = t;
            this.size = size;
            this.colour = colour;
            this.solid = solid;
            this.bottom = bottom;
            this.turn = turn;
        }
    }

    /// <summary>One loop of the world round the mountain, along the story's
    /// curve. The ground runs from <see cref="Inner"/> to <see cref="Outer"/>
    /// across the road; the mountain's foot is a line of cliffs on the inner
    /// side, and the mountain itself is <see cref="Mountain"/>: both fixed,
    /// the world's axis, never on the ribbon. A, the village with the red tower; B, the blue arch and the camp;
    /// C, the ring of green stones; D, the grey ruins; between them woods,
    /// fields and a stream. Placeholder boxes only.</summary>
    public static class LoopLayout
    {
        public const float PointA = 20f;
        /// <summary>The ground's edges across the road, m (the inner one, towards the mountain, negative).</summary>
        public const float Inner = -36f, Outer = 34f;

        public static readonly Color Grass = new Color(0.33f, 0.42f, 0.24f);
        public static readonly Color Road = new Color(0.55f, 0.48f, 0.36f);
        static readonly Color Stone = new Color(0.52f, 0.52f, 0.5f);
        static readonly Color Rock = new Color(0.38f, 0.36f, 0.35f);
        static readonly Color Wall = new Color(0.45f, 0.43f, 0.4f);
        static readonly Color House = new Color(0.62f, 0.5f, 0.38f);
        static readonly Color Roof = new Color(0.45f, 0.22f, 0.18f);
        static readonly Color Tower = new Color(0.75f, 0.18f, 0.15f);
        static readonly Color Tree = new Color(0.16f, 0.3f, 0.16f);
        static readonly Color Arch = new Color(0.2f, 0.35f, 0.75f);
        static readonly Color Tent = new Color(0.85f, 0.5f, 0.15f);
        static readonly Color Hay = new Color(0.85f, 0.75f, 0.3f);
        static readonly Color Fence = new Color(0.4f, 0.3f, 0.2f);
        static readonly Color Menhir = new Color(0.25f, 0.65f, 0.35f);
        static readonly Color Water = new Color(0.2f, 0.4f, 0.6f);
        static readonly Color Ruin = new Color(0.6f, 0.6f, 0.62f);

        /// <summary>The mountain in the ring's middle: stacked rocks, each (x, z,
        /// bottom, size, yaw) in the world, round the origin.</summary>
        public static IEnumerable<(Vector3 at, Vector3 size, float yaw)> Mountain()
        {
            // Inside the ground's inner edge (at least 32 m from the middle; more in the three lobes).
            yield return (new Vector3(0, 0, 0), new Vector3(44, 20, 40), 10);
            yield return (new Vector3(-3, 20, 2), new Vector3(34, 24, 30), 35);
            yield return (new Vector3(2, 44, -2), new Vector3(24, 26, 20), 60);
            yield return (new Vector3(0, 70, 0), new Vector3(12, 22, 10), 20);
            foreach (float degrees in new[] { 30f, 150f, 270f })
            {
                float angle = degrees * Mathf.Deg2Rad;
                yield return (new Vector3(Mathf.Cos(angle) * 42, 0, Mathf.Sin(angle) * 42), new Vector3(30, 26, 28), degrees);
            }
        }

        public static readonly Color MountainRock = new Color(0.34f, 0.32f, 0.31f);

        public static List<Block> Make(float length)
        {
            var blocks = new List<Block>();
            var random = new System.Random(7);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

            // The mountain's foot inside, a low wall outside.
            for (float s = 0; s < length - 5; s += 8)
            {
                blocks.Add(new Block("Cliff", s + 4, Inner + 3, new Vector3(5, Range(10, 24), 8.5f), Rock, fixedPlace: true));
                blocks.Add(new Block("Wall", s + 4, Outer - 1, new Vector3(1.5f, Range(1.2f, 2.5f), 8.2f), Wall));
            }

            // A: the village.
            blocks.Add(new Block("A Square", PointA, 0, new Vector3(16, 0.06f, 16), Stone, solid: false));
            blocks.Add(new Block("A Tower", PointA, -7, new Vector3(3, 14, 3), Tower));
            blocks.Add(new Block("A Well", PointA + 2, 5, new Vector3(2, 1, 2), Stone));
            foreach (var (s, t) in new[] { (6f, -11f), (34f, -11f), (8f, 11f), (32f, 11f), (20f, 18f) })
                HouseAt(s, t);

            Trees(55, 140, 90);

            // B: the arch over the road, and the camp.
            float b = 180;
            blocks.Add(new Block("B Pillar", b, -3.5f, new Vector3(1.2f, 6, 1.2f), Arch));
            blocks.Add(new Block("B Pillar", b, 3.5f, new Vector3(1.2f, 6, 1.2f), Arch));
            blocks.Add(new Block("B Lintel", b, 0, new Vector3(8.2f, 1.2f, 1.2f), Arch, bottom: 6f));
            for (int i = 0; i < 5; i++)
                blocks.Add(new Block("Tent", b - 10 + i * 5, 9 + (i % 2) * 5, new Vector3(3, 2, 3), Tent, turn: Range(-30, 30)));

            // The fields.
            for (float s = 210; s < 290; s += 6)
            {
                blocks.Add(new Block("Fence", s + 2.5f, -6, new Vector3(0.2f, 1, 5), Fence));
                blocks.Add(new Block("Fence", s + 2.5f, 6, new Vector3(0.2f, 1, 5), Fence));
            }
            for (int i = 0; i < 24; i++)
                blocks.Add(new Block("Hay", Range(212, 288), (i % 2 == 0 ? -1 : 1) * Range(9, 26), Vector3.one * 1.5f, Hay, turn: Range(0, 90)));

            // C: the ring of stones, the road through it.
            float c = 330;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4 + Mathf.PI / 8;
                blocks.Add(new Block("C Stone", c + Mathf.Sin(angle) * 7, Mathf.Cos(angle) * 7, new Vector3(1, 5, 1), Menhir));
            }

            // The stream across, and the bridge's rails.
            blocks.Add(new Block("Stream", 370, (Inner + Outer) / 2, new Vector3(Outer - Inner, 0.05f, 4), Water, solid: false));
            blocks.Add(new Block("Rail", 370, -2.8f, new Vector3(0.3f, 1, 6), Fence));
            blocks.Add(new Block("Rail", 370, 2.8f, new Vector3(0.3f, 1, 6), Fence));

            Trees(390, 470, 80);

            // D: the ruins.
            float d = 510;
            for (int i = 0; i < 9; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                blocks.Add(new Block("D Ruin", d - 16 + i * 4, side * Range(6, 16), new Vector3(Range(2, 6), Range(1, 5), 0.8f), Ruin, turn: Range(-20, 20)));
            }
            blocks.Add(new Block("D Gate", d, -4, new Vector3(1.5f, 7, 1.5f), Ruin));
            blocks.Add(new Block("D Gate", d, 4, new Vector3(1.5f, 4, 1.5f), Ruin));

            Trees(560, 640, 70);
            Trees(140, 170, 12);
            Trees(290, 320, 12);
            Trees(470, 490, 8);
            return blocks;

            void HouseAt(float s, float t)
            {
                blocks.Add(new Block("House", s, t, new Vector3(6, 5, 7), House));
                blocks.Add(new Block("Roof", s, t, new Vector3(6.6f, 1.2f, 7.6f), Roof, bottom: 5f));
            }

            void Trees(float from, float to, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float t = random.Next(2) == 0 ? -Range(4, -Inner - 7) : Range(4, Outer - 3);
                    blocks.Add(new Block("Tree", Range(from, to), t, new Vector3(0.8f, Range(4, 8), 0.8f), Tree));
                }
            }
        }
    }
}
