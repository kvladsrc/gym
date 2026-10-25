using System.Collections.Generic;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>A world with a story's curve through it, for the camera
    /// (<see cref="ForwardCamera"/>), the hero (<see cref="Hero"/>) and the
    /// map (<see cref="StoryMap"/>): the loop that bends onto the ribbon
    /// (<see cref="LoopWorld"/>), or the plane seen through a band along the
    /// route, with the way ahead on the horizon (<see cref="PlaneWorld"/>).
    /// Around the camera's focus, from <see cref="Behind"/> metres back to
    /// <see cref="Ahead"/> on along the curve, the world is shown where it is;
    /// beyond, it is pressed into what lies ahead, at 1/e of its size each
    /// <see cref="Horizon"/> metres on. (Each world builds itself in Awake,
    /// before the others: DefaultExecutionOrder -100.)</summary>
    public abstract class StoryWorld : MonoBehaviour
    {
        public Material material;
        public Hero hero;
        public ForwardCamera view;

        public StoryPath Path { get; protected set; }

        /// <summary>Every way through the story (<see cref="Path"/> is the
        /// active one), which branches each takes, and which are still open.</summary>
        public List<StoryPath> Routes { get; } = new List<StoryPath>();
        public List<bool[]> Taken { get; } = new List<bool[]>();
        public List<bool> Open { get; } = new List<bool>();
        public int Active { get; private set; }
        /// <summary>The ways' forks are offered (chosen with Q, E); else the active way alone.</summary>
        public virtual bool Forks => true;
        float[,] divergence;

        /// <summary>Where two ways part, s (the same on both: they run the same
        /// road till there); far past the end for one way.</summary>
        public float Divergence(int a, int b)
        {
            if (a == b) return float.MaxValue;
            if (divergence == null || divergence.GetLength(0) != Routes.Count)
            {
                divergence = new float[Routes.Count, Routes.Count];
                for (int i = 0; i < Routes.Count; i++)
                    for (int j = i + 1; j < Routes.Count; j++)
                    {
                        float s = 0, end = Mathf.Min(Routes[i].Length, Routes[j].Length);
                        while (s < end && (Routes[i].Position(s) - Routes[j].Position(s)).sqrMagnitude < 0.25f) s += 1f;
                        divergence[i, j] = divergence[j, i] = s;
                    }
            }
            return divergence[a, b];
        }

        /// <summary>The branch that tells two ways apart (the first they differ in).</summary>
        public int Deciding(int a, int b)
        {
            for (int k = 0; k < Taken[a].Length; k++) if (Taken[a][k] != Taken[b][k]) return k;
            return -1;
        }

        /// <summary>Makes a way the active one; only one still open and not yet
        /// parted from the hero's (he is on their shared road).</summary>
        public bool Choose(int route)
        {
            if (route < 0 || route >= Routes.Count || !Open[route]) return false;
            if (route != Active && Divergence(route, Active) < hero.S + 2f) return false;
            Active = route;
            Path = Routes[route];
            return true;
        }
        public abstract float Ahead { get; }
        public abstract float Behind { get; }
        public abstract float Horizon { get; }
        /// <summary>The map's picture of the world from above, and the part of the ground (x, z) it shows.</summary>
        public abstract Texture2D MapImage { get; }
        public abstract Rect MapArea { get; }

        /// <summary>The level may be moved as a whole (the crystal turns under
        /// the hero): a point, or a way, from where it is shown to the level's
        /// own frame, where the story's curve is; and back. Its turn, radians.</summary>
        public Vector3 ToLevel(Vector3 p) => transform.InverseTransformPoint(p);
        public Vector3 FromLevel(Vector3 p) => transform.TransformPoint(p);
        public Vector3 ToLevelWay(Vector3 v) => transform.InverseTransformDirection(v);
        public Vector3 FromLevelWay(Vector3 v) => transform.TransformDirection(v);
        public float Turned => transform.eulerAngles.y * Mathf.Deg2Rad;

        /// <summary>The hero's step, as the world lets him take it from where he stands.</summary>
        public virtual Vector3 Steer(Vector3 at, Vector3 step) => step;

        /// <summary>How far a point stands out of where the hero may go, m
        /// (0: in), by his place near <paramref name="near"/> on the curve.</summary>
        public virtual float Outside(Vector3 at, float near) => 0f;

        /// <summary>The ribbon draws a thing far from where it is: never cull it by its place.</summary>
        public static void Bendable(Renderer renderer)
        {
            renderer.bounds = new Bounds(renderer.transform.position, Vector3.one * 4000f);
        }
    }
}
