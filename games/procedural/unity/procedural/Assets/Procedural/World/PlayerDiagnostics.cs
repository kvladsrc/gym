using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Profiling;

namespace Procedural.World
{
    /// <summary>
    /// Caps the frame rate (vsync, 60 FPS) and keeps the game running without
    /// focus; in players, logs frame rate, memory and focus changes to Player.log
    /// every 10 s. "-soak &lt;seconds&gt;" walks the island in a wide circle on its
    /// own and quits: a hands-off stability run.
    /// </summary>
    public class PlayerDiagnostics : MonoBehaviour
    {
        const float Interval = 10f;

        float next, soakEnd = -1f, worst;
        int frames;
        Walker walker;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
            // Paused without focus, the player stopped its loop while input kept
            // queueing: 13 GB in half a minute, and a locked cursor nobody could free.
            Application.runInBackground = true;
            if (Application.isEditor) return;
            var go = new GameObject("Player Diagnostics");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerDiagnostics>();
        }

        void Start()
        {
            next = Time.realtimeSinceStartup + Interval;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-soak");
            if (i < 0 || i + 1 >= args.Length || !float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
                return;
            soakEnd = Time.realtimeSinceStartup + seconds;
            walker = FindAnyObjectByType<Walker>();
            if (walker != null)
            {
                walker.useInput = false;
                walker.moveInput = new Vector2(0f, 1f);
                walker.turnInput = 6f;
            }
            Debug.Log($"[procedural] soak for {seconds} s, walker {(walker != null ? "found" : "missing")}");
        }

        void OnApplicationFocus(bool focused)
        {
            Debug.Log($"[procedural] focus {(focused ? "gained" : "lost")} at t={Time.realtimeSinceStartup:F0}s");
            if (!focused)
                CursorLock.Release();
        }

        void Update()
        {
            frames++;
            worst = Mathf.Max(worst, Time.unscaledDeltaTime);
            float now = Time.realtimeSinceStartup;
            if (now >= next)
            {
                var p = walker != null ? walker.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[procedural] t={0:F0}s fps={1:F1} worst={2:F0}ms allocated={3}MB reserved={4}MB mono={5}MB at=({6:F0},{7:F0},{8:F0})",
                    now, frames / Interval, worst * 1000f, Profiler.GetTotalAllocatedMemoryLong() >> 20,
                    Profiler.GetTotalReservedMemoryLong() >> 20, Profiler.GetMonoUsedSizeLong() >> 20, p.x, p.y, p.z));
                frames = 0;
                worst = 0f;
                next = now + Interval;
            }
            if (soakEnd > 0f && now >= soakEnd)
                Application.Quit();
        }
    }
}
