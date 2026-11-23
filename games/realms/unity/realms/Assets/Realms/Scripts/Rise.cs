using System.Collections.Generic;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// A building just built rises out of the ground, up a little past its height and back (the
    /// visual plan of 2026-11-11, item 2): the players see what they built appear where it stands,
    /// with a ring of dust round its foot (<see cref="Puffs"/>). The model's pivot is its bottom
    /// centre, so it grows from the ground, its shadow with it. At the end of a turn the towns' new
    /// houses wait their turn flat (<see cref="Delay"/>) and rise one after another. Any key or click
    /// ends every rise at once (<see cref="FinishAll"/>). The component goes once the building stands.
    /// </summary>
    public sealed class Rise : MonoBehaviour
    {
        /// <summary>How long the rise takes: the plan's 0.45 s for a house or a building, 0.8 s for a castle.</summary>
        public float Seconds = 0.45f;

        /// <summary>The overshoot's height, as a share of the building's, and when in the rise it comes.</summary>
        const float Over = 1.08f, Peak = 0.7f, Flat = 0.01f;

        static readonly List<Rise> rising = new List<Rise>();

        /// <summary>Seconds the building waits flat before it rises.</summary>
        public float Delay;

        Vector3 full;
        float clock;
        bool counting, dusted;

        /// <summary>True while a building is rising or waiting to.</summary>
        public static bool Any => rising.Count > 0;

        /// <summary>Every rising building stands at once, as the turn's show ends.</summary>
        public static void FinishAll()
        {
            for (int i = rising.Count - 1; i >= 0; i--)
                rising[i].Finish();
        }

        void Awake()
        {
            full = transform.localScale;
            Apply(0f);
        }

        void OnEnable() => rising.Add(this);

        void OnDisable() => rising.Remove(this);

        void Update()
        {
            // The frame it was made in drew the map again and may have run long: the rise counts
            // from the frame after, as the turn's show does (Hud.ShowTurn).
            if (!counting)
            {
                counting = true;
                return;
            }
            clock += Time.deltaTime;
            if (clock < Delay)
                return;
            if (!dusted)
            {
                dusted = true;
                // Round the foot, out to a little past the walls: the model's largest side is 1
                // before its scale.
                Puffs.Ring(transform.position, 0.55f * Mathf.Max(full.x, full.z));
            }
            float k = (clock - Delay) / Seconds;
            if (k >= 1f)
                Finish();
            else
                Apply(k);
        }

        void Finish()
        {
            transform.localScale = full;
            rising.Remove(this);
            // Destroy waits for the frame's end: disabled, it does not shrink the building again before.
            enabled = false;
            Destroy(this);
        }

        void Apply(float k)
        {
            float height = k < Peak
                ? Over * (1f - Mathf.Pow(1f - k / Peak, 3f))
                : Mathf.Lerp(Over, 1f, Mathf.SmoothStep(0f, 1f, (k - Peak) / (1f - Peak)));
            transform.localScale = new Vector3(full.x, full.y * Mathf.Max(height, Flat), full.z);
        }
    }
}
