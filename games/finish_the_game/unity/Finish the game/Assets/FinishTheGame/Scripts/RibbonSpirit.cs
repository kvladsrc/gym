using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>A spirit as it first comes: a light, a will-o'-the-wisp (a
    /// white-gold core in a warm halo, flickering, shedding sparks, a trail of
    /// light behind it), always turned to the camera and about as big to the
    /// eye far off as near (<see cref="apparent"/>, within <see cref="least"/>
    /// and <see cref="most"/>). It comes out of
    /// a place's mark on the ribbon with a flash and rides down along the
    /// ribbon's upper edge, by fits and starts: each stretch at a pace of its
    /// own, speeding up and slowing to a stop; once or twice (a surprise) it
    /// gathers itself and leaps down to the turn below (the nearest place of
    /// an earlier turn); down the ribbon's way up to the road, and along the
    /// road to the hero (<see cref="Plan"/>, <see cref="Ride"/>); and back the
    /// same way. Its places on the ribbon are kept as places along it
    /// (sample indices), so that it rides the ribbon as the ribbon moves.</summary>
    public class RibbonSpirit : MonoBehaviour
    {
        public RibbonBend ribbon;
        public Camera eye;
        public Material glowLook, trailLook;
        public AudioClip hop, flash, vanish;
        /// <summary>Its height a metre off the camera, m; its least and most, m.</summary>
        public float apparent = 0.085f, least = 1.1f, most = 60f;
        /// <summary>A stretch's length, in its own sizes (longer low down,
        /// where it is a long way); its pace, sizes a second at its quickest.</summary>
        public Vector2 stretch = new Vector2(12f, 24f), pace = new Vector2(10f, 17f);
        /// <summary>The leaps down to the turn below, at most; gathering itself, s; the leap's time, s.</summary>
        public int leaps = 2;
        public float gather = 0.6f, leapTime = 0.9f;
        /// <summary>Its takes are the same each time (its stretches and leaps drawn from this).</summary>
        public int seed = 7;

        /// <summary>A place it goes to: on the ribbon (a sample index), or somewhere of the world's (followed as it moves).</summary>
        public struct Spot
        {
            public float along;
            public Func<Vector3> world;
        }

        public bool Shown { get; private set; }
        /// <summary>Where it stands (its feet), and its height.</summary>
        public Vector3 Place { get; private set; }
        public float Size { get; private set; }

        Transform core, halo;
        Material coreMaterial, haloMaterial;
        TrailRenderer trail;
        ParticleSystem sparks;
        AudioSource sounds;
        float flashing, alpha, squash = 1f;

        void Awake()
        {
            (core, coreMaterial) = Glow("Core", 4f);
            (halo, haloMaterial) = Glow("Halo", 2.2f);
            sparks = Sparks();
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailLook;
            trail.time = 0.55f;
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(1f, 0.85f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.3f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) },
            };
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;
            sounds = gameObject.AddComponent<AudioSource>();
            sounds.playOnAwake = false;
            Show(false);
        }

        (Transform, Material) Glow(string name, float falloff)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(transform, false);
            var material = new Material(glowLook);
            material.SetFloat("_Falloff", falloff);
            var drawn = quad.GetComponent<MeshRenderer>();
            drawn.sharedMaterial = material;
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return (quad.transform, material);
        }

        /// <summary>Sparks shed as it goes, drifting up and fading, in the world (left behind).</summary>
        ParticleSystem Sparks()
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.7f), new Color(1f, 0.65f, 0.3f));
            main.gravityModifier = -0.05f;
            main.maxParticles = 400;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = system.emission;
            emission.rateOverTime = 26f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) },
            };
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var drawn = go.GetComponent<ParticleSystemRenderer>();
            var spark = new Material(glowLook);
            spark.SetFloat("_Falloff", 3f);
            spark.SetColor("_Tint", new Color(3f, 2.4f, 1.5f, 1f));
            drawn.sharedMaterial = spark;
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return system;
        }

        public void Hide() => Show(false);

        void Show(bool on)
        {
            Shown = on;
            core.gameObject.SetActive(on);
            halo.gameObject.SetActive(on);
            trail.emitting = on;
            if (on) sparks.Play();
            else
            {
                trail.Clear();
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>A place on the ribbon at sample index <paramref name="along"/>.</summary>
        public static Spot On(float along) => new Spot { along = along };
        public static Spot At(Func<Vector3> world) => new Spot { world = world };

        /// <summary>Where it is at a sample index: in the ribbon, its light in
        /// the ribbon's middle where the ribbon stands on edge (in the sky);
        /// on it, rolling, where it lies (its way down to the road).</summary>
        public Vector3 Edge(float along)
        {
            int n = ribbon.Count;
            along = Mathf.Clamp(along, 0, n - 1.001f);
            int i = (int)along;
            float k = along - i;
            var centre = Vector3.Lerp(ribbon.Point(i), ribbon.Point(i + 1), k);
            var side = Vector3.Lerp(ribbon.SideAt(i), ribbon.SideAt(i + 1), k).normalized;
            return centre - Vector3.up * (0.5f * SizeAt(centre) * Mathf.Abs(side.y));
        }

        Vector3 Where(Spot spot) => spot.world != null ? spot.world() : Edge(spot.along);

        float SizeAt(Vector3 at) => Mathf.Clamp(Vector3.Distance(eye.transform.position, at) * apparent, least, most);

        /// <summary>Out of a place on the ribbon, with a flash.</summary>
        public IEnumerator Appear(Spot spot, bool loud = true)
        {
            Place = Where(spot);
            Size = SizeAt(Place);
            // Its light trails from here, not from where it was last.
            transform.position = Place + Vector3.up * (Size * 0.5f);
            Show(true);
            trail.Clear();
            if (loud && flash != null) sounds.PlayOneShot(flash, 0.9f);
            for (float t = 0; t < 0.9f; t += Time.deltaTime)
            {
                Place = Where(spot);
                flashing = 1f - t / 0.9f;
                alpha = Mathf.Clamp01(t / 0.35f);
                yield return null;
            }
            flashing = 0f;
            alpha = 1f;
        }

        /// <summary>Gone into a place, with a flash.</summary>
        public IEnumerator Vanish(Spot spot, bool loud = true)
        {
            if (loud && vanish != null) sounds.PlayOneShot(vanish, 0.8f);
            for (float t = 0; t < 0.7f; t += Time.deltaTime)
            {
                Place = Where(spot);
                flashing = t / 0.7f;
                alpha = 1f - Mathf.Clamp01((t - 0.35f) / 0.35f);
                yield return null;
            }
            flashing = 0f;
            Show(false);
        }

        /// <summary>A leg of its way: along the ribbon (sample indices) at a pace,
        /// a leap from one turn to the one below, or the road to the hero.</summary>
        public class Leg
        {
            public bool leap, road;
            public float from, to, pace, rest;
        }

        float[] runs;
        /// <summary>The ribbon's upper edge's length to each sample, m, as it is now.</summary>
        void Measure()
        {
            int n = ribbon.Count;
            if (runs == null || runs.Length != n) runs = new float[n];
            var last = Edge(0);
            for (int i = 1; i < n; i++)
            {
                var at = Edge(i);
                runs[i] = runs[i - 1] + Vector3.Distance(last, at);
                last = at;
            }
        }

        float Run(float along)
        {
            int i = Mathf.Clamp((int)along, 0, runs.Length - 2);
            return Mathf.Lerp(runs[i], runs[i + 1], along - i);
        }

        float AlongAt(float run)
        {
            int lo = 0, hi = runs.Length - 1;
            if (run <= 0) return 0;
            if (run >= runs[hi]) return hi;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (runs[mid] < run) lo = mid; else hi = mid;
            }
            return lo + Mathf.InverseLerp(runs[lo], runs[hi], run);
        }

        /// <summary>Its way down from a place on the ribbon to the hero.</summary>
        public List<Leg> Plan(float from)
        {
            Measure();
            var random = new System.Random(seed);
            float Range(Vector2 r) => Mathf.Lerp(r.x, r.y, (float)random.NextDouble());
            var legs = new List<Leg>();
            float along = from;
            int leapt = 0;
            while (along > 0.5f)
            {
                var at = Edge(along);
                float length = Range(stretch) * SizeAt(at) * (at.y < 45f ? 2f : 1f);
                float next = AlongAt(Run(along) - length);
                if (next < 1f) next = 0f;
                legs.Add(new Leg { from = along, to = next, pace = Range(pace), rest = Mathf.Lerp(0.15f, 0.45f, (float)random.NextDouble()) });
                along = next;
                if (leapt < leaps && Below(along, out float below))
                {
                    legs.Add(new Leg { leap = true, from = along, to = below });
                    along = below;
                    leapt++;
                }
            }
            legs.Add(new Leg { road = true, pace = Range(pace) });
            return legs;
        }

        /// <summary>The nearest place of an earlier turn below a high place:
        /// far along the ribbon, near in the air.</summary>
        bool Below(float along, out float below)
        {
            below = 0;
            var at = Edge(along);
            if (at.y < 45f) return false;
            float nearest = 450f, here = Run(along);
            for (int j = 0; j < (int)along; j++)
            {
                var there = Edge(j);
                float d = Vector3.Distance(there, at);
                if (d < nearest && here - runs[j] > Mathf.Max(400f, 3f * d) && there.y < at.y - 3f)
                {
                    nearest = d;
                    below = j;
                }
            }
            return below > 0;
        }

        /// <summary>Rides its legs, down to the hero (<paramref name="before"/>:
        /// the hero's front), or back up from him (faster, no rests).</summary>
        public IEnumerator Ride(List<Leg> legs, Func<Vector3> before, bool back = false)
        {
            Measure();
            for (int k = 0; k < legs.Count; k++)
            {
                var leg = legs[back ? legs.Count - 1 - k : k];
                if (leg.road)
                    yield return Glide(back ? before : () => Edge(0), back ? () => Edge(0) : before, leg.pace * (back ? 1.5f : 1f));
                else if (leg.leap)
                    yield return Leap(back ? leg.to : leg.from, back ? leg.from : leg.to, back);
                else
                {
                    float from = back ? leg.to : leg.from, to = back ? leg.from : leg.to;
                    yield return Glide(() => Edge(from), () => Edge(to), leg.pace * (back ? 1.6f : 1f), from, to);
                    // A stop, now and then, before going on (not before a leap: it gathers itself instead).
                    var next = k + 1 < legs.Count ? legs[back ? legs.Count - 2 - k : k + 1] : null;
                    if (!back && next != null && !next.leap)
                        for (float t = 0; t < leg.rest; t += Time.deltaTime)
                        {
                            Place = Edge(to);
                            yield return null;
                        }
                }
            }
        }

        /// <summary>Along the ribbon's edge between two sample indices (or
        /// straight from a place to a place, without them), speeding up and
        /// slowing to a stop.</summary>
        IEnumerator Glide(Func<Vector3> a, Func<Vector3> b, float quickest, float from = -1, float to = -1)
        {
            bool onRibbon = from >= 0;
            float start = onRibbon ? Run(from) : 0f, length = onRibbon ? Mathf.Abs(Run(to) - start) : Vector3.Distance(a(), b());
            float way = onRibbon ? Mathf.Sign(Run(to) - start) : 1f, done = 0;
            while (done < length)
            {
                float k = done / Mathf.Max(1e-3f, length);
                // Slow out of its stop, quick in the middle, slow into the next.
                float speed = quickest * SizeAt(Place) * (0.15f + 0.85f * Mathf.Sin(Mathf.PI * Mathf.Clamp(k, 0.03f, 0.97f)));
                done = Mathf.Min(length, done + speed * Time.deltaTime);
                Place = onRibbon ? Edge(AlongAt(start + way * done)) : Vector3.Lerp(a(), b(), done / Mathf.Max(1e-3f, length));
                yield return null;
            }
        }

        /// <summary>Gathers itself (squashed, brightening) and leaps from one
        /// turn to another, landing squashed, sparks bursting.</summary>
        IEnumerator Leap(float from, float to, bool quick)
        {
            float gathering = quick ? gather * 0.4f : gather;
            for (float t = 0; t < gathering; t += Time.deltaTime)
            {
                float k = t / gathering;
                Place = Edge(from);
                squash = 1f - 0.4f * Mathf.SmoothStep(0f, 1f, k);
                flashing = 0.5f * k * (0.75f + 0.25f * Mathf.Sin(t * 40f));
                yield return null;
            }
            if (hop != null) sounds.PlayOneShot(hop, 0.6f);
            sparks.Emit(40);
            float far = Vector3.Distance(Edge(from), Edge(to)), time = leapTime + far / 500f;
            for (float t = 0; t < time; t += Time.deltaTime)
            {
                float k = t / time;
                var a = Edge(from);
                var b = Edge(to);
                Place = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * ((0.3f * far + SizeAt(a)) * 4f * k * (1f - k));
                squash = 1f + 0.35f * Mathf.Sin(Mathf.PI * k);
                flashing = 0.5f * (1f - k);
                yield return null;
            }
            Place = Edge(to);
            sparks.Emit(30);
            for (float t = 0; t < 0.3f; t += Time.deltaTime)
            {
                squash = 1f - 0.3f * Mathf.Sin(Mathf.PI * t / 0.3f);
                yield return null;
            }
            squash = 1f;
            flashing = 0f;
        }

        void LateUpdate()
        {
            if (!Shown) return;
            Size = SizeAt(Place);
            var middle = Place + Vector3.up * (Size * 0.5f * squash);
            transform.position = middle;
            var toward = middle - eye.transform.position;
            var turned = Quaternion.LookRotation(toward, eye.transform.up);
            // A flame's flicker: two slow waves and a quick one.
            float t = Time.time;
            float flicker = 1f + 0.12f * Mathf.Sin(t * 7.3f) + 0.08f * Mathf.Sin(t * 13.1f + 1.7f) + 0.05f * Mathf.Sin(t * 29.0f);
            core.SetPositionAndRotation(middle, turned);
            core.localScale = new Vector3(Size * 0.55f / Mathf.Sqrt(squash), Size * 0.55f * squash, 1f) * flicker;
            halo.SetPositionAndRotation(middle + toward.normalized * (Size * 0.05f), turned);
            halo.localScale = Vector3.one * (Size * (2.2f + 12f * flashing) * (0.95f + 0.05f * flicker));
            coreMaterial.SetColor("_Tint", new Color(4f, 3.6f, 2.8f, 1f) * (alpha * (1f + 2f * flashing)));
            haloMaterial.SetColor("_Tint", new Color(1.6f, 1.1f, 0.55f, 0.6f * flicker + 2.5f * flashing) * alpha);
            // Its sparks as big and as quick for its size, near or far.
            sparks.transform.localScale = Vector3.one * Size;
            var main = sparks.main;
            main.startSpeedMultiplier = Size;
            trail.widthMultiplier = Size * 0.3f;
            trail.minVertexDistance = Size * 0.1f;
        }
    }
}
