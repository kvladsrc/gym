using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulslike
{
    /// <summary>Candidate skies to compare in the game: K shows the next one,
    /// with its fog, and names it for a moment.</summary>
    public class SkyChoice : MonoBehaviour
    {
        public string[] names;
        public Material[] skies;
        public Color[] fogs;

        int current;
        float shownUntil;
        GUIStyle style;

        void Update()
        {
            if (skies == null || skies.Length < 2 || Keyboard.current == null) return;
            if (!Keyboard.current.kKey.wasPressedThisFrame) return;
            current = (current + 1) % skies.Length;
            RenderSettings.skybox = skies[current];
            RenderSettings.fogColor = fogs[current];
            DynamicGI.UpdateEnvironment();
            shownUntil = Time.unscaledTime + 2.5f;
        }

        void OnGUI()
        {
            if (Time.unscaledTime > shownUntil) return;
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight };
            float scale = Screen.height / 720f;
            style.fontSize = Mathf.RoundToInt(18 * scale);
            GUI.Label(new Rect(Screen.width - 420 * scale, 24 * scale, 390 * scale, 30 * scale),
                $"Sky {current + 1}/{skies.Length}: {names[current]}", style);
        }
    }
}
