using UnityEngine;

namespace Realms
{
    /// <summary>
    /// A building just built rises out of the ground, up a little past its height and back (the
    /// visual plan of 2026-11-11, item 2): the players see what they built appear where it stands.
    /// The model's pivot is its bottom centre, so it grows from the ground, its shadow with it. The
    /// component goes once the building stands.
    /// </summary>
    public sealed class Rise : MonoBehaviour
    {
        /// <summary>How long the rise takes (the plan's 0.45 s).</summary>
        const float Seconds = 0.45f;

        /// <summary>The overshoot's height, as a share of the building's, and when in the rise it comes.</summary>
        const float Over = 1.08f, Peak = 0.7f, Flat = 0.01f;

        Vector3 full;
        float clock;

        void Awake()
        {
            full = transform.localScale;
            Apply(0f);
        }

        void Update()
        {
            clock += Time.deltaTime;
            float k = Mathf.Clamp01(clock / Seconds);
            if (k >= 1f)
            {
                transform.localScale = full;
                Destroy(this);
                return;
            }
            Apply(k);
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
