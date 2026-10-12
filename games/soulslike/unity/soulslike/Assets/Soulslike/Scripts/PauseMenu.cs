using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulslike
{
    /// <summary>The player's volume settings, kept between runs: the whole
    /// (the listener's), the music's and the effects' (every other sound:
    /// blows, steps, voices, fires).</summary>
    public static class AudioVolume
    {
        public static float Master { get; private set; } = 1f;
        public static float Music { get; private set; } = 1f;
        public static float Effects { get; private set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            Master = PlayerPrefs.GetFloat("volume.master", 1f);
            Music = PlayerPrefs.GetFloat("volume.music", 1f);
            Effects = PlayerPrefs.GetFloat("volume.effects", 1f);
            AudioListener.volume = Master;
        }

        public static void Set(float master, float music, float effects)
        {
            if (master == Master && music == Music && effects == Effects) return;
            Master = master;
            Music = music;
            Effects = effects;
            AudioListener.volume = Master;
            PlayerPrefs.SetFloat("volume.master", Master);
            PlayerPrefs.SetFloat("volume.music", Music);
            PlayerPrefs.SetFloat("volume.effects", Effects);
        }
    }

    /// <summary>The pause menu (Esc): the game stops, the cursor is free;
    /// resume, the three volumes, quit (IMGUI, as the HUD).</summary>
    public class PauseMenu : MonoBehaviour
    {
        public static bool Open { get; private set; }

        GUIStyle title, text, button;
        Texture2D white;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Show(!Open);
        }

        void OnDisable()
        {
            if (Open) Show(false);
        }

        void Show(bool open)
        {
            Open = open;
            Time.timeScale = open ? 0f : 1f;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        void OnGUI()
        {
            if (!Open) return;
            float scale = Screen.height / 720f;
            if (white == null)
            {
                white = Texture2D.whiteTexture;
                title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
                button = new GUIStyle(GUI.skin.button);
            }
            title.fontSize = Mathf.RoundToInt(40 * scale);
            title.normal.textColor = new Color(0.85f, 0.7f, 0.3f);
            text.fontSize = Mathf.RoundToInt(18 * scale);
            button.fontSize = Mathf.RoundToInt(20 * scale);

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            GUI.color = Color.white;

            float width = 420 * scale, row = 44 * scale;
            float x = (Screen.width - width) / 2f, y = Screen.height * 0.22f;
            GUI.Label(new Rect(x, y, width, 60 * scale), "PAUSED", title);
            y += 90 * scale;
            if (GUI.Button(new Rect(x, y, width, row), "Resume", button)) Show(false);
            y += row + 12 * scale;
            if (DayNight.Instance != null
                && GUI.Button(new Rect(x, y, width, row), DayNight.Day ? "Time: day (switch to night)" : "Time: night (switch to day)", button))
                DayNight.Instance.Toggle();
            y += row + 30 * scale;

            float master = Slider("Volume", AudioVolume.Master, x, ref y, width, row, scale);
            float music = Slider("Music", AudioVolume.Music, x, ref y, width, row, scale);
            float effects = Slider("Effects", AudioVolume.Effects, x, ref y, width, row, scale);
            AudioVolume.Set(master, music, effects);

            y += 30 * scale;
            if (GUI.Button(new Rect(x, y, width, row), "Quit game", button))
            {
                PlayerPrefs.Save();
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }

        float Slider(string name, float value, float x, ref float y, float width, float row, float scale)
        {
            GUI.Label(new Rect(x, y, 130 * scale, row), name, text);
            GUI.Label(new Rect(x + width - 60 * scale, y, 60 * scale, row), $"{Mathf.RoundToInt(value * 100)}%", text);
            value = GUI.HorizontalSlider(new Rect(x + 130 * scale, y + row * 0.4f, width - 200 * scale, row * 0.5f), value, 0f, 1f);
            y += row;
            return value;
        }
    }
}
