using UnityEngine;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>
    /// The players' settings beside the volumes (the interface plan's 3.5, 2026-11-12), kept
    /// between runs in PlayerPrefs like the flag and the language: how Alt shows the map's badges, the newcomer's hints, and the camera's speeds and edge. The screen's
    /// mode and window size are the player's own, which Unity keeps; the volumes are
    /// <see cref="SoundSettings"/>. A capture and the promo run with the defaults and leave the
    /// players' own as they are.
    /// </summary>
    public static class GameSettings
    {
        const string AltKey = "Realms.Interface.AltToggles",
            HintsKey = "Realms.Guide.Hints", MoveKey = "Realms.Camera.Move", TurnKey = "Realms.Camera.Turn",
            EdgeKey = "Realms.Camera.Edge", WindowKey = "Realms.Screen.Window";

        // The interface has one size, the 1920 × 1080 it was drawn for, scaled with the screen: at
        // 115 % the bottom panel's place card got too narrow and its lines broke letter by letter,
        // so the players dropped the size setting (2026-11-13). The panel's reference resolution
        // is in RealmsPanel.asset.

        /// <summary>The window's sizes, of which those smaller than the display are offered.</summary>
        static readonly Vector2Int[] WindowSizes =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440),
        };

        public const int DefaultSpeed = 5;

        static bool read, kept = true;
        static int move, turn;
        static bool altToggles, hints, edge;

        static void Read()
        {
            if (read)
                return;
            read = true;
            altToggles = PlayerPrefs.GetInt(AltKey, 0) != 0;
            hints = PlayerPrefs.GetInt(HintsKey, 1) != 0;
            move = Mathf.Clamp(PlayerPrefs.GetInt(MoveKey, DefaultSpeed), 1, 10);
            turn = Mathf.Clamp(PlayerPrefs.GetInt(TurnKey, DefaultSpeed), 1, 10);
            edge = PlayerPrefs.GetInt(EdgeKey, 0) != 0;
        }

        static void Keep(string key, int value)
        {
            if (!kept)
                return;
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        /// <summary>Alt turns the map's badges on and off instead of showing them while held.</summary>
        public static bool AltToggles
        {
            get { Read(); return altToggles; }
            set { Read(); altToggles = value; Keep(AltKey, value ? 1 : 0); }
        }

        /// <summary>The newcomer's guiding lines, rings, calls and one-time hints are shown.</summary>
        public static bool Hints
        {
            get { Read(); return hints; }
            set { Read(); hints = value; Keep(HintsKey, value ? 1 : 0); }
        }

        /// <summary>How fast the keys (and the screen's edge) move the map, 1 to 10; 5 as before.</summary>
        public static int CameraMove
        {
            get { Read(); return move; }
            set { Read(); move = Mathf.Clamp(value, 1, 10); Keep(MoveKey, move); }
        }

        /// <summary>How fast Q and E turn the map, 1 to 10; 5 as before.</summary>
        public static int CameraTurn
        {
            get { Read(); return turn; }
            set { Read(); turn = Mathf.Clamp(value, 1, 10); Keep(TurnKey, turn); }
        }

        /// <summary>The pointer at the screen's edge moves the map.</summary>
        public static bool EdgePan
        {
            get { Read(); return edge; }
            set { Read(); edge = value; Keep(EdgeKey, value ? 1 : 0); }
        }

        /// <summary>A speed's factor: 5 is as before, 1 half as fast, 10 about 2.4 times.</summary>
        public static float SpeedFactor(int level) => Mathf.Pow(2f, (level - DefaultSpeed) / 4f);

        public static float MoveFactor => SpeedFactor(CameraMove);

        public static float TurnFactor => SpeedFactor(CameraTurn);

        /// <summary>
        /// The defaults for this run only, the players' own staying saved: the captures and the
        /// promo show the game as it comes. A capture may still set them for its frames.
        /// </summary>
        public static void UseDefaultsForThisRun()
        {
            read = true;
            kept = false;
            altToggles = false;
            hints = true;
            move = turn = DefaultSpeed;
            edge = false;
        }

        /// <summary>«Вернуть как было»: every setting and volume at its default, the screen fullscreen.</summary>
        public static void Reset()
        {
            AltToggles = false;
            Hints = true;
            CameraMove = CameraTurn = DefaultSpeed;
            EdgePan = false;
            SoundSettings.Reset();
        }

        // ----- The screen -----

        /// <summary>The game fills the screen (not a window).</summary>
        public static bool Fullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;

        /// <summary>
        /// The desktop's size as the game sees it: the size it fills the screen at by itself (on
        /// a scaled display the scaled one, not the panel's own pixels).
        /// </summary>
        static Vector2Int Desktop()
        {
            Resolution current = Screen.currentResolution;
            if (current.width > 0 && current.height > 0)
                return new Vector2Int(current.width, current.height);
            return new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);
        }

        /// <summary>The window's sizes smaller than the display, smallest first; at least the smallest.</summary>
        public static Vector2Int[] Windows()
        {
            Vector2Int desktop = Desktop();
            var fit = new System.Collections.Generic.List<Vector2Int>();
            foreach (Vector2Int w in WindowSizes)
            {
                if (desktop.x <= 0 || desktop.y <= 0 || (w.x < desktop.x && w.y < desktop.y))
                    fit.Add(w);
            }
            if (fit.Count == 0)
                fit.Add(WindowSizes[0]);
            return fit.ToArray();
        }

        /// <summary>The window's size chosen last, or the largest that fits.</summary>
        public static Vector2Int Window
        {
            get
            {
                Vector2Int[] windows = Windows();
                int chosen = PlayerPrefs.GetInt(WindowKey, -1);
                foreach (Vector2Int w in windows)
                {
                    if (w.x == chosen)
                        return w;
                }
                return windows[windows.Length - 1];
            }
            set
            {
                if (kept)
                    PlayerPrefs.SetInt(WindowKey, value.x);
            }
        }

        /// <summary>Fills the screen at the display's own size, or opens a window of <paramref name="window"/>.</summary>
        public static void SetScreen(bool fullscreen, Vector2Int window)
        {
            if (fullscreen)
            {
                Vector2Int desktop = Desktop();
                Screen.SetResolution(desktop.x, desktop.y, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Window = window;
                Screen.SetResolution(window.x, window.y, FullScreenMode.Windowed);
            }
        }
    }
}
