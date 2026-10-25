using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FinishTheGame
{
    /// <summary>The map: the world from above, as it is (north up)
    /// (<see cref="StoryWorld.MapImage"/>). Shows the story's curve, the hero
    /// and his facing, the part of the curve shown where it is (white) and
    /// the part pressed into what lies ahead (blue, fading). M makes it big;
    /// Tab goes to the other world (the next scene in the build).</summary>
    public class StoryMap : MonoBehaviour
    {
        public StoryWorld world;
        public ForwardCamera view;
        public float small = 260f, big = 620f;
        /// <summary>The map shown (else only the hints).</summary>
        public bool shown = true;

        Texture2D arrow, dot;
        bool large;
        GUIStyle label;

        Rect Frame
        {
            get
            {
                float size = large ? big : small;
                return new Rect(Screen.width - size - 16, 16, size, size);
            }
        }

        public bool Covers(Vector2 screen) => shown && Frame.Contains(new Vector2(screen.x, Screen.height - screen.y));

        void Start()
        {
            arrow = Arrow(32);
            dot = new Texture2D(1, 1);
            dot.SetPixel(0, 0, Color.white);
            dot.Apply();
        }

        bool hinted = true;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.mKey.wasPressedThisFrame) large = !large;
            // The hint at the foot of the picture off and on (a clean picture for a take).
            if (keyboard.f1Key.wasPressedThisFrame) hinted = !hinted;
            if (keyboard.tabKey.wasPressedThisFrame && SceneManager.sceneCountInBuildSettings > 1)
                SceneManager.LoadScene((SceneManager.GetActiveScene().buildIndex + 1) % SceneManager.sceneCountInBuildSettings);
        }

        static Texture2D Arrow(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // A triangle pointing up (texture y up = screen up).
                    float v = (float)y / (size - 1), u = Mathf.Abs((float)x / (size - 1) - 0.5f) * 2f;
                    bool inside = u <= 1f - v;
                    bool rim = inside && u > 1f - v - 0.12f;
                    texture.SetPixel(x, y, inside ? (rim ? Color.black : new Color(1f, 0.9f, 0.3f)) : Color.clear);
                }
            texture.Apply();
            return texture;
        }

        void OnGUI()
        {
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            if (Dialogue.Cinematic) return;
            if (!shown || dot == null || world.MapImage == null)
            {
                if (hinted)
                    GUI.Label(new Rect(16, Screen.height - 32, 900, 24), "WASD: walk · mouse: look (a click holds it, Esc lets go) · wheel: nearer, further · "
                    + (world.Forks ? "Q, E: choose the way at a fork · " : "") + "Tab: the other world", label);
                return;
            }
            var frame = Frame;
            var area = world.MapArea;
            GUI.DrawTexture(frame, world.MapImage);
            Vector2 ToScreen(Vector3 w) => new Vector2(
                frame.x + (w.x - area.xMin) / area.width * frame.width,
                frame.yMax - (w.z - area.yMin) / area.height * frame.height);
            var path = world.Path;

            // Shown where it is, then pressed into what lies ahead, fading.
            float start = view.Focus + world.Ahead;
            for (float s = view.Focus - world.Behind; s < start + 4f * world.Horizon; s += 6f)
            {
                if (!path.Closed && (s < 0 || s > path.Length)) continue;
                float fade = s <= start ? 1f : Mathf.Exp(-(s - start) / world.Horizon);
                GUI.color = s <= start ? new Color(1f, 1f, 1f, 0.9f) : new Color(0.6f, 0.8f, 1f, 0.9f * fade);
                var p = ToScreen(path.Position(s));
                float size = Mathf.Max(2f, 4f * fade);
                GUI.DrawTexture(new Rect(p.x - size / 2, p.y - size / 2, size, size), dot);
            }
            GUI.color = Color.white;

            // GUI angles turn the other way than the world's headings.
            var hero = world.hero.transform;
            var at = ToScreen(world.ToLevel(hero.position));
            var facing = world.ToLevelWay(hero.forward);
            float degrees = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-degrees, at);
            GUI.DrawTexture(new Rect(at.x - 10, at.y - 10, 20, 20), arrow);
            GUI.matrix = matrix;

            label ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            float story = world.hero.S;
            string where = path.Closed
                ? $"story {story:0} m · lap {Mathf.FloorToInt(story / path.Length) + 1} · loop {path.Length:0} m"
                : $"story {story:0} of {path.Length:0} m";
            GUI.Label(new Rect(frame.x, frame.yMax + 4, frame.width, 24), where, label);
            GUI.Label(new Rect(16, Screen.height - 32, 600, 24), "WASD or click: walk · M: the map · Tab: the other world", label);
        }
    }
}
