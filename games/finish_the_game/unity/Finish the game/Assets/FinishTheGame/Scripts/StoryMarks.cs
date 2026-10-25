using System.Collections.Generic;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>The story's key moments on the ribbon in the sky, drawn flat,
    /// as 2D marks over the picture where the ribbon carries the place: far
    /// off, only a light; nearer, it takes its own colour; nearer still, its
    /// name; then, as the ribbon comes off the road, the place itself on it. Each is once for each time the route passes
    /// it (a mark at each s where the route comes nearest a place, within
    /// <see cref="reach"/>), and the story's end at the ribbon's tip, always
    /// in sight (<see cref="RibbonBend.StoryAt"/>). Only what lies past
    /// <see cref="from"/> metres of the ribbon is marked: the rings nearer
    /// show the place itself.</summary>
    [DefaultExecutionOrder(120)]
    public class StoryMarks : MonoBehaviour
    {
        public StoryWorld world;
        public RibbonBend ribbon;
        public ForwardCamera view;
        public float reach = 30f, from = 40f;
        public float size = 26f, smallest = 8f;
        /// <summary>A far mark's light, before it shows its own colour.</summary>
        public Color light = new Color(1f, 0.92f, 0.75f);
        /// <summary>The places' pictograms (the studio's, pixelated): village,
        /// cave, city, farm, end; a mark without one is a light.</summary>
        public Texture2D village, cave, city, farm, end;
        /// <summary>The way it marks (-1: the active one); marked from this s
        /// on (a branch: past where it parts); how bright.</summary>
        public int route = -1;
        public float fromS, alpha = 1f;
        StoryPath Route => route < 0 || route >= world.Routes.Count ? world.Path : world.Routes[route];

        struct Mark
        {
            public float s;
            public string name;
            public Color colour;
            public bool final;
            public Texture2D icon;
        }

        readonly List<Mark> marks = new List<Mark>();
        Camera eye;
        Texture2D spot;
        GUIStyle label;

        void Start()
        {
            eye = view.GetComponent<Camera>();
            spot = Spot(64);
            foreach (var (place, name, colour, icon) in new[]
            {
                (PlaneLayout.A, "A", new Color(1f, 0.45f, 0.35f), village),
                (PlaneLayout.Cave, "The cave", new Color(0.75f, 0.7f, 1f), cave),
                (PlaneLayout.City, "The city", new Color(0.45f, 0.65f, 1f), city),
                (PlaneLayout.Farm, "The farm", new Color(1f, 0.85f, 0.35f), farm),
                (PlaneLayout.Shrine, "The lake shrine", new Color(0.6f, 0.9f, 0.85f), (Texture2D)null),
                (PlaneLayout.Portal, "The old portal", new Color(0.85f, 0.6f, 1f), (Texture2D)null),
            })
                Passes(new Vector3(place.x, 0, place.y), name, colour, icon);
            if (!Route.Closed)
                marks.Add(new Mark { s = Route.Length, name = "The end", colour = new Color(1f, 0.95f, 0.8f), final = true, icon = end });
            marks.Sort((x, y) => x.s.CompareTo(y.s));
        }

        /// <summary>A mark at each s where the route comes nearest the place.</summary>
        void Passes(Vector3 place, string name, Color colour, Texture2D icon)
        {
            var path = Route;
            float step = 2f, previous = float.MaxValue, before = float.MaxValue;
            for (float s = 0; s <= path.Length + step; s += step)
            {
                float d = s <= path.Length ? Vector3.Distance(path.Position(s), place) : float.MaxValue;
                if (previous < before && previous <= d && previous < reach)
                    marks.Add(new Mark { s = s - step, name = name, colour = colour, icon = icon });
                before = previous;
                previous = d;
            }
        }

        static Texture2D Spot(int pixels)
        {
            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            for (int y = 0; y < pixels; y++)
                for (int x = 0; x < pixels; x++)
                {
                    float r = new Vector2(x - pixels / 2f + 0.5f, y - pixels / 2f + 0.5f).magnitude / (pixels / 2f);
                    float glow = Mathf.Clamp01(1f - r);
                    texture.SetPixel(x, y, new Color(1, 1, 1, glow * glow + (r < 0.3f ? 0.6f : 0f)));
                }
            texture.Apply();
            return texture;
        }

        void OnGUI()
        {
            if (Dialogue.Cinematic) return;
            if (Event.current.type != EventType.Repaint) return;
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter, normal = { textColor = Color.white } };
            float start = ribbon.Start;
            float breath = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            bool branch = alpha < 1f, drawn = false;
            foreach (var mark in marks)
            {
                if (mark.s < start || mark.s < fromS) continue;
                // A branch not taken shows only where it leads (its first place).
                if (branch && drawn) break;
                drawn = true;
                float a = ribbon.ArcOf(mark.s);
                if (a < from) continue;
                var at = Along(a);
                var screen = eye.WorldToScreenPoint(at);
                if (screen.z <= 0) continue;
                float near = 1f - Mathf.InverseLerp(from, ribbon.ArcAt(ribbon.Count - 1), a);
                float d = Mathf.Lerp(smallest, size, near) * (mark.final ? 1.6f : 1f) * breath;
                var middle = new Vector2(screen.x, Screen.height - screen.y);
                // A light; its own colour as it nears; its name nearer still.
                var colour = Color.Lerp(light, mark.colour, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.6f, near)));
                colour.a = Mathf.Clamp01((a - from) / 20f) * alpha;
                GUI.color = colour;
                GUI.DrawTexture(new Rect(middle.x - d, middle.y - d, 2 * d, 2 * d), spot);
                // Nearer, the light opens into the place's pictogram.
                float shown = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, near));
                if (mark.icon != null && shown > 0)
                {
                    float r = d * 1.3f;
                    GUI.color = new Color(1, 1, 1, colour.a * shown);
                    GUI.DrawTexture(new Rect(middle.x - r, middle.y - r, 2 * r, 2 * r), mark.icon);
                }
                GUI.color = new Color(1, 1, 1, colour.a * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.85f, near)));
                GUI.Label(new Rect(middle.x - 80, middle.y + d * 0.6f, 160, 22), mark.name, label);
            }
            GUI.color = Color.white;
        }

        /// <summary>The ribbon's curve at a metres along it.</summary>
        Vector3 Along(float a)
        {
            float f = Mathf.Clamp01(a / ribbon.ArcAt(ribbon.Count - 1)) * (ribbon.Count - 1);
            int i = Mathf.Min((int)f, ribbon.Count - 2);
            return Vector3.Lerp(ribbon.Point(i), ribbon.Point(i + 1), f - i);
        }
    }
}
