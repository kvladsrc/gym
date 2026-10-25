using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// Full screen at the screen's own resolution, whatever the player saved:
    /// a tiling compositor shrank the window to half the screen, the player
    /// kept that size, and every launch after drew a full-screen window at
    /// it. A window keeps the size it was left at.
    /// </summary>
    static class ScreenFit
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Fit()
        {
            if (Application.isEditor || Screen.fullScreenMode == FullScreenMode.Windowed) return;
            int width = Display.main.systemWidth, height = Display.main.systemHeight;
            if (Screen.width != width || Screen.height != height)
                Screen.SetResolution(width, height, Screen.fullScreenMode);
        }
    }
}
