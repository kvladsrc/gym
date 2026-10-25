using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FinishTheGame
{
    /// <summary>One who speaks in a talk: a name over his lines (none: the
    /// hero), his body (at his feet), his eyes' height over them, his voice.</summary>
    public class Speaker
    {
        public string name;
        public Transform body;
        public float eyes = 1.6f;
        public AudioSource voice;
        /// <summary>A spirit's shoulder is not looked over: the camera stands beside it.</summary>
        public bool beside;
        public Vector3 Eyes => body.position + Vector3.up * eyes;
    }

    /// <summary>Talks as in Gothic II: the camera taken from the player and
    /// set over one's shoulder at whoever speaks (a cut each line, always on
    /// one side of the two); the line voiced, and written at the top of the
    /// picture under the speaker's name (the hero has none), skipped with a
    /// click, space or Enter; the hero's answers listed at the bottom,
    /// numbered, chosen with W, S, the arrows or the wheel and Enter, E,
    /// space or a click, or by their number; the hero then says the chosen
    /// one. And what goes with talks: the log's and experience's notes in the
    /// middle of the picture, a caption at its foot, the picture fading out
    /// and in. While a scene has the picture (<see cref="Cinematic"/>) the
    /// game's own marks and hints step aside.</summary>
    public class Dialogue : MonoBehaviour
    {
        public Font font;
        public ForwardCamera view;
        public Hero hero;
        /// <summary>The log's jingle.</summary>
        public AudioClip entry;
        /// <summary>The talk's camera: its field of view, degrees; how far behind
        /// the listener's eyes, m, and to the side.</summary>
        public float fieldOfView = 36f, behind = 1.3f, shoulder = 0.75f, above = 0.22f;

        public static Dialogue Current { get; private set; }
        public static bool Cinematic => Current != null && Current.cinematic;

        Camera eye;
        AudioSource sounds;
        /// <summary>A soft warm light from the camera in talks: faces against the low sun are read.</summary>
        Light fill;
        bool cinematic;
        float wideView;
        Vector3 side;
        string speaker, line;
        string[] options;
        int chosen;
        string noteTitle, noteLine, caption;
        float noteSince = -100f, captionSince = -100f, captionFor;
        float fade;
        Color fadeColour = Color.black;
        Texture2D shade, blank;
        GUIStyle nameStyle, lineStyle, optionStyle, chosenStyle, noteTitleStyle, noteStyle, captionStyle;
        int styledFor;

        void Awake()
        {
            Current = this;
            eye = view.GetComponent<Camera>();
            sounds = gameObject.AddComponent<AudioSource>();
            sounds.playOnAwake = false;
            sounds.spatialBlend = 0f;
            shade = Shade();
            blank = Texture2D.whiteTexture;
            fill = new GameObject("Talk Light").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 9f;
            fill.intensity = 2.2f;
            fill.color = new Color(1f, 0.86f, 0.7f);
            fill.shadows = LightShadows.None;
            fill.transform.SetParent(eye.transform, false);
            fill.transform.localPosition = new Vector3(0.6f, 0.5f, 0.3f);
            fill.enabled = false;
        }

        /// <summary>The scene takes the picture and the hero: the camera is the scene's, he stands.</summary>
        public void Take()
        {
            if (cinematic) return;
            cinematic = true;
            hero.held = true;
            view.enabled = false;
            wideView = eye.fieldOfView;
            fill.enabled = true;
        }

        /// <summary>The player has them back.</summary>
        public void Release()
        {
            if (!cinematic) return;
            cinematic = false;
            hero.held = false;
            hero.facing = null;
            eye.fieldOfView = wideView;
            fill.enabled = false;
            view.enabled = true;
            speaker = line = null;
            options = null;
        }

        /// <summary>A talk between the hero and another: they turn to each
        /// other, the camera keeps to one side of them.</summary>
        public void Begin(Speaker other)
        {
            Take();
            hero.facing = other.body.position;
            var between = other.body.position - hero.transform.position;
            between.y = 0;
            side = Vector3.Cross(Vector3.up, between.normalized);
            eye.fieldOfView = fieldOfView;
        }

        /// <summary>Over the listener's shoulder at the speaker.</summary>
        public void Over(Speaker listener, Speaker speaking)
        {
            var at = speaking.Eyes - Vector3.up * 0.06f;
            var from = listener.Eyes + Vector3.up * above;
            var way = at - from;
            way.y = 0;
            way.Normalize();
            eye.fieldOfView = fieldOfView;
            eye.transform.position = listener.beside ? from - way * 0.3f + side * 1.6f : from - way * behind + side * shoulder;
            eye.transform.LookAt(at);
        }

        /// <summary>A shot of the scene's own.</summary>
        public void Shot(Vector3 from, Vector3 at, float fieldOfView = 50f)
        {
            eye.fieldOfView = fieldOfView;
            eye.transform.position = from;
            eye.transform.LookAt(at);
        }

        /// <summary>A line spoken (its voice, or as long as it takes to read), the camera on the speaker.</summary>
        public IEnumerator Say(Speaker who, Speaker to, string text, AudioClip voice)
        {
            if (to != null) Over(to, who);
            speaker = who.name;
            line = text;
            float length = voice != null ? voice.length + 0.35f : 1.2f + 0.055f * text.Length;
            if (voice != null && who.voice != null)
            {
                who.voice.clip = voice;
                who.voice.Play();
            }
            float since = 0f;
            yield return null;
            while (since < length)
            {
                if (to != null) Over(to, who);
                since += Time.deltaTime;
                if (since > 0.25f && Skipped()) break;
                yield return null;
            }
            if (who.voice != null) who.voice.Stop();
            speaker = line = null;
            yield return null;
        }

        /// <summary>The hero's answers, numbered; the chosen one's index.</summary>
        public IEnumerator Choose(Speaker other, string[] answers, Action<int> result)
        {
            var me = new Speaker { body = hero.transform, eyes = 1.62f };
            options = answers;
            chosen = 0;
            yield return null;
            while (true)
            {
                Over(me, other);
                var keyboard = Keyboard.current;
                var mouse = Mouse.current;
                int step = 0;
                if (keyboard != null)
                {
                    if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) step = -1;
                    if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) step = 1;
                    for (int k = 0; k < answers.Length && k < 9; k++)
                        if (keyboard[Key.Digit1 + k].wasPressedThisFrame)
                        {
                            chosen = k;
                            step = 0;
                            options = null;
                            result(k);
                            yield break;
                        }
                }
                if (mouse != null)
                {
                    float wheel = mouse.scroll.ReadValue().y;
                    if (wheel != 0) step = wheel > 0 ? -1 : 1;
                }
                chosen = (chosen + step + answers.Length) % answers.Length;
                if (Confirmed())
                {
                    options = null;
                    result(chosen);
                    yield break;
                }
                yield return null;
            }
        }

        static bool Skipped()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame));
        }

        static bool Confirmed()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
                    || keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame));
        }

        /// <summary>A note in the middle of the picture (the log's, the experience's), with the log's jingle.</summary>
        public void Note(string title, string text, bool jingle = true)
        {
            noteTitle = title;
            noteLine = text;
            noteSince = Time.time;
            if (jingle && entry != null) sounds.PlayOneShot(entry, 0.8f);
        }

        /// <summary>A caption at the foot of the picture, for a while.</summary>
        public void Caption(string text, float seconds)
        {
            caption = text;
            captionSince = Time.time;
            captionFor = seconds;
        }

        /// <summary>The picture fading to a colour (1) or back (0).</summary>
        public IEnumerator Fade(float to, float seconds, Color colour)
        {
            fadeColour = colour;
            float from = fade;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                fade = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            fade = to;
        }

        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null) sounds.PlayOneShot(clip, volume);
        }

        void Styles()
        {
            if (nameStyle != null && styledFor == Screen.height) return;
            styledFor = Screen.height;
            int size = Mathf.RoundToInt(Screen.height / 30f);
            GUIStyle Style(int px, Color colour, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperCenter) =>
                new GUIStyle(GUI.skin.label) { font = font, fontSize = px, fontStyle = style, alignment = anchor, wordWrap = true, richText = false, normal = { textColor = colour } };
            nameStyle = Style(Mathf.RoundToInt(size * 0.8f), new Color(0.93f, 0.78f, 0.47f));
            lineStyle = Style(size, new Color(0.96f, 0.94f, 0.9f));
            optionStyle = Style(Mathf.RoundToInt(size * 0.92f), new Color(0.78f, 0.75f, 0.69f), FontStyle.Normal, TextAnchor.UpperLeft);
            chosenStyle = Style(Mathf.RoundToInt(size * 0.92f), new Color(1f, 0.87f, 0.55f), FontStyle.Normal, TextAnchor.UpperLeft);
            noteTitleStyle = Style(Mathf.RoundToInt(size * 1.15f), new Color(0.93f, 0.78f, 0.47f));
            noteStyle = Style(size, new Color(0.96f, 0.94f, 0.9f));
            captionStyle = Style(Mathf.RoundToInt(size * 0.95f), new Color(0.96f, 0.94f, 0.9f), FontStyle.Italic);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            Styles();
            float w = Screen.width, h = Screen.height;
            if (line != null)
            {
                float width = w * 0.62f, x = (w - width) / 2, y = h * 0.045f;
                float nameHeight = string.IsNullOrEmpty(speaker) ? 0 : nameStyle.CalcHeight(new GUIContent(speaker), width);
                float lineHeight = lineStyle.CalcHeight(new GUIContent(line), width);
                Box(new Rect(x - h * 0.03f, y - h * 0.015f, width + h * 0.06f, nameHeight + lineHeight + h * 0.03f), 1f);
                if (nameHeight > 0) Text(new Rect(x, y, width, nameHeight), speaker, nameStyle, 1f);
                Text(new Rect(x, y + nameHeight, width, lineHeight), line, lineStyle, 1f);
            }
            if (options != null)
            {
                float width = w * 0.5f, x = (w - width) / 2, lineHeight = optionStyle.lineHeight * 1.25f;
                float height = lineHeight * options.Length, y = h * 0.95f - height;
                Box(new Rect(x - h * 0.03f, y - h * 0.02f, width + h * 0.06f, height + h * 0.04f), 1f);
                for (int i = 0; i < options.Length; i++)
                    Text(new Rect(x, y + i * lineHeight, width, lineHeight), $"{i + 1}.  {options[i]}", i == chosen ? chosenStyle : optionStyle, 1f);
            }
            float noted = Time.time - noteSince;
            if (noted < 4.2f && noteTitle != null)
            {
                float a = Mathf.Clamp01(noted / 0.3f) * Mathf.Clamp01((4.2f - noted) / 0.8f);
                float width = w * 0.5f, x = (w - width) / 2, y = h * 0.36f;
                float titleHeight = noteTitleStyle.CalcHeight(new GUIContent(noteTitle), width);
                Text(new Rect(x, y, width, titleHeight), noteTitle, noteTitleStyle, a);
                if (!string.IsNullOrEmpty(noteLine)) Text(new Rect(x, y + titleHeight, width, h * 0.1f), noteLine, noteStyle, a);
            }
            float captioned = Time.time - captionSince;
            if (caption != null && captioned < captionFor)
            {
                float a = Mathf.Clamp01(captioned / 0.6f) * Mathf.Clamp01((captionFor - captioned) / 0.8f);
                Text(new Rect(w * 0.15f, h * 0.84f, w * 0.7f, h * 0.08f), caption, captionStyle, a);
            }
            if (fade > 0f)
            {
                GUI.color = new Color(fadeColour.r, fadeColour.g, fadeColour.b, fade);
                GUI.DrawTexture(new Rect(0, 0, w, h), blank);
                GUI.color = Color.white;
            }
        }

        void Box(Rect rect, float alpha)
        {
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(rect, shade);
            GUI.color = Color.white;
        }

        /// <summary>Text with a soft dark edge, to be read over anything.</summary>
        static void Text(Rect rect, string text, GUIStyle style, float alpha)
        {
            var colour = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.75f * alpha);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, style);
            style.normal.textColor = new Color(colour.r, colour.g, colour.b, alpha);
            GUI.Label(rect, text, style);
            style.normal.textColor = colour;
        }

        /// <summary>A dark band, its ends and edges soft.</summary>
        static Texture2D Shade()
        {
            const int W = 128, H = 32;
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float ends = Mathf.SmoothStep(0f, 1f, Mathf.Min(x, W - 1 - x) / (W * 0.12f));
                    float edges = Mathf.SmoothStep(0f, 1f, Mathf.Min(y, H - 1 - y) / (H * 0.25f));
                    texture.SetPixel(x, y, new Color(0.04f, 0.03f, 0.03f, 0.62f * ends * edges));
                }
            texture.Apply();
            return texture;
        }
    }
}
