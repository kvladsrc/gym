using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The time the view's motion and messages run on: the real frame's time while the game is
    /// played, and the steady step of a recording (the promo sets <see cref="Time.captureFramerate"/>
    /// and then draws frames slower than real time). On the wall clock the camera's easing came out
    /// too quick in the promo's first take, its descent to a town under half a second (2026-11-11).
    /// </summary>
    public static class ViewClock
    {
        static bool Recording => Time.captureDeltaTime > 0f;

        /// <summary>This frame's time.</summary>
        public static float Delta => Recording ? Time.deltaTime : Time.unscaledDeltaTime;

        /// <summary>The time now, for how long a message stays.</summary>
        public static float Now => Recording ? Time.time : Time.unscaledTime;
    }
}
