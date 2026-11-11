using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Realms
{
    /// <summary>
    /// How the picture changes on a town or hero screen, so the screen does not look like the map:
    /// a deeper vignette around the subject and the land beyond it out of focus, easing in and out.
    /// Both are post-processing, which the camera does not draw for now (Setup.BuildScene), so
    /// the screens currently look like the map.
    /// </summary>
    public sealed class ScreenLook : MonoBehaviour
    {
        Vignette vignette;
        DepthOfField depth;
        float mapVignette, amount, goal;
        Vector2 centre = new Vector2(0.5f, 0.5f), goalCentre = new Vector2(0.5f, 0.5f);
        MapCamera mapCamera;

        public void Bind(MapCamera camera)
        {
            mapCamera = camera;
            // The scene has one volume (Setup).
            Volume volume = FindAnyObjectByType<Volume>();
            if (volume == null)
                return;
            // The volume's own copy of the profile, so the asset stays as it is.
            VolumeProfile profile = volume.profile;
            if (profile.TryGet(out vignette))
                mapVignette = vignette.intensity.value;
            if (!profile.TryGet(out depth))
                depth = profile.Add<DepthOfField>(true);
            depth.mode.Override(DepthOfFieldMode.Off);
            depth.highQualitySampling.Override(true);
        }

        /// <summary>A screen opens: the subject stands at <paramref name="subject"/> (0..1 of the screen).</summary>
        public void Focus(Vector2 subject)
        {
            goal = 1f;
            goalCentre = subject;
        }

        /// <summary>Back to the map's look.</summary>
        public void Clear() => goal = 0f;

        void LateUpdate()
        {
            float dt = Mathf.Min(ViewClock.Delta, 0.05f);
            amount = Mathf.MoveTowards(amount, goal, dt * 1.6f);
            float k = Mathf.SmoothStep(0f, 1f, amount);
            centre = Vector2.Lerp(centre, goal > 0f ? goalCentre : new Vector2(0.5f, 0.5f), 1f - Mathf.Exp(-6f * dt));
            if (vignette != null)
            {
                // Deeper than the map's, not much: at 0.34 the sea at the top of a town's screen
                // was a fifth darker than on the map, almost ink.
                vignette.intensity.Override(Mathf.Lerp(mapVignette, 0.26f, k));
                vignette.center.Override(centre);
            }
            if (depth == null || mapCamera == null)
                return;
            if (k < 0.01f)
            {
                depth.mode.Override(DepthOfFieldMode.Off);
                return;
            }
            // Sharp up to a little behind the subject, then out of focus.
            float focus = mapCamera.Distance;
            depth.mode.Override(DepthOfFieldMode.Gaussian);
            depth.gaussianStart.Override(focus + 1f);
            depth.gaussianEnd.Override(focus + Mathf.Lerp(80f, 6f, k));
            depth.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, 1.5f, k));
        }
    }
}
