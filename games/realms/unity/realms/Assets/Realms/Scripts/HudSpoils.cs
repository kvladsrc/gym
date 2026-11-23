using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>
    /// The spoils of a won fight flying into the victory dialog (the visual plan's step 6 and its
    /// proposal 7 of 2026-11-11): stars of experience and chips of the reward fly from where the
    /// monsters stood into the dialog's lines, whose numbers run up as they land, in about a
    /// second. The figures stay as they are, as the players keep creatures and battles still. A
    /// quick battle has no field, so its numbers only run up. Any key or click, or the dialog
    /// closing, ends it at once.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>A spoil's flight, the pause between two, and a number's run once its first spoil lands, in seconds.</summary>
        const float SpoilSeconds = 0.5f, SpoilGap = 0.07f, SpoilRun = 0.35f;

        /// <summary>The most places the spoils fly from, and the most spoils in all.</summary>
        const int MostSpoilPlaces = 4, MostSpoils = 10;

        sealed class Spoil
        {
            public VisualElement Anchor, Icon;
            public Vector3 From;
            public SpoilLine Line;
            public float Start;
        }

        sealed class SpoilLine
        {
            public Label Number;
            public VisualElement Target;
            public string Icon;
            public int Value;
            public float Landed = -1f;
        }

        readonly List<Spoil> spoils = new List<Spoil>();
        readonly List<SpoilLine> spoilLines = new List<SpoilLine>();
        VisualElement spoilLayer;
        float spoilsBase = -1f;
        int spoilsFrame;
        bool spoiling;

        /// <summary>
        /// Starts the spoils: from the world points <paramref name="from"/> (none after a quick
        /// battle) into <paramref name="experience"/>, the experience line's number, and the chips
        /// of <paramref name="rewards"/> (<see cref="RewardChips"/>), which show <paramref name="reward"/>.
        /// </summary>
        public void Spoils(IReadOnlyList<Vector3> from, Label experience, int experienceValue, VisualElement rewards, Stock reward)
        {
            FinishSpoils();
            if (root?.panel == null)
                return;
            if (experience != null && experienceValue > 0)
                spoilLines.Add(new SpoilLine { Number = experience, Target = experience, Icon = "icon_experience", Value = experienceValue });
            if (rewards != null)
            {
                // The chips stand in the goods' order, one for each good the fight brought.
                int[] values = { reward.Gold, reward.Food, reward.Wood, reward.Stone };
                List<VisualElement> chips = rewards.Query<VisualElement>(className: "reward").ToList();
                int chip = 0;
                for (int g = 0; g < 4 && chip < chips.Count; g++)
                {
                    if (values[g] <= 0)
                        continue;
                    Label number = chips[chip].Q<Label>(className: "reward-text");
                    VisualElement icon = chips[chip].Q(className: "chip-icon") ?? chips[chip];
                    chip++;
                    if (number != null)
                        spoilLines.Add(new SpoilLine { Number = number, Target = icon, Icon = GoodsIcons[g], Value = values[g] });
                }
            }
            if (spoilLines.Count == 0)
                return;
            foreach (SpoilLine line in spoilLines)
                line.Number.text = "+0";
            spoiling = true;
            spoilsBase = -1f;
            spoilsFrame = Time.frameCount;

            // From up to four of the places, spread over them, each sending one of every line.
            var places = new List<Vector3>();
            if (from != null && cam != null)
            {
                int count = Mathf.Min(MostSpoilPlaces, from.Count);
                for (int k = 0; k < count; k++)
                    places.Add(from[k * from.Count / count]);
            }
            if (places.Count == 0)
            {
                foreach (SpoilLine line in spoilLines)
                    line.Landed = 0.15f;
                return;
            }
            if (spoilLayer == null)
            {
                spoilLayer = Child(root, "turn-chips");
                spoilLayer.pickingMode = PickingMode.Ignore;
            }
            // Over the dialog.
            spoilLayer.BringToFront();
            foreach (Vector3 place in places)
            {
                foreach (SpoilLine line in spoilLines)
                {
                    if (spoils.Count == MostSpoils)
                        break;
                    VisualElement anchor = Child(spoilLayer, "anchor");
                    VisualElement icon = Child(anchor, "turn-chip");
                    anchor.pickingMode = icon.pickingMode = PickingMode.Ignore;
                    SetIcon(icon, line.Icon);
                    anchor.style.opacity = 0f;
                    spoils.Add(new Spoil { Anchor = anchor, Icon = icon, From = place, Line = line, Start = 0.1f + spoils.Count * SpoilGap });
                }
            }
        }

        /// <summary>The spoils at once to their end: every number shows its whole value.</summary>
        public void FinishSpoils()
        {
            foreach (Spoil spoil in spoils)
                spoil.Anchor.RemoveFromHierarchy();
            spoils.Clear();
            foreach (SpoilLine line in spoilLines)
            {
                line.Number.text = "+" + line.Value;
                line.Target.style.scale = new StyleScale(StyleKeyword.Null);
            }
            spoilLines.Clear();
            spoiling = false;
        }

        void UpdateSpoils()
        {
            if (!spoiling)
                return;
            if (!DialogOpen)
            {
                FinishSpoils();
                return;
            }
            if (Time.frameCount != spoilsFrame && PressedAnything())
            {
                FinishSpoils();
                return;
            }
            if (spoilsBase < 0f)
            {
                if (Time.frameCount == spoilsFrame)
                    return;
                spoilsBase = ViewClock.Now;
            }
            float now = ViewClock.Now - spoilsBase;
            for (int i = spoils.Count - 1; i >= 0; i--)
            {
                Spoil spoil = spoils[i];
                float k = (now - spoil.Start) / SpoilSeconds;
                if (k < 0f)
                    continue;
                Vector2 to = spoil.Line.Target.worldBound.center;
                if (k >= 1f)
                {
                    spoil.Anchor.RemoveFromHierarchy();
                    spoils.RemoveAt(i);
                    if (spoil.Line.Landed < 0f)
                        spoil.Line.Landed = now;
                    continue;
                }
                Vector3 screen = cam.WorldToScreenPoint(spoil.From);
                Vector2 from = screen.z > 0f ? RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, spoil.From, cam) : to;
                float e = k * k * (3f - 2f * k);
                // Up in an arc, as the turn's chips fly into the treasury.
                Vector2 p = Vector2.Lerp(from, to, e) + new Vector2(0f, -70f * Mathf.Sin(Mathf.PI * k));
                spoil.Anchor.style.translate = new Translate(p.x, p.y);
                spoil.Anchor.style.opacity = Mathf.Clamp01(k / 0.12f);
                float s = Mathf.Lerp(1f, 0.7f, e);
                spoil.Icon.style.scale = new Scale(new Vector3(s, s, 1f));
            }
            bool running = spoils.Count > 0;
            foreach (SpoilLine line in spoilLines)
            {
                if (line.Landed < 0f)
                {
                    running = true;
                    continue;
                }
                float r = Mathf.Clamp01((now - line.Landed) / SpoilRun);
                line.Number.text = "+" + Mathf.RoundToInt(line.Value * (1f - (1f - r) * (1f - r)));
                // The line's mark swells a little as the first spoil lands in it.
                float swell = Mathf.Clamp01((now - line.Landed) / 0.22f);
                float size = swell < 1f ? 1f + 0.18f * (1f - swell) : 1f;
                line.Target.style.scale = new Scale(new Vector3(size, size, 1f));
                if (r < 1f)
                    running = true;
            }
            if (!running)
                FinishSpoils();
        }

        /// <summary>Whether a key or a mouse button went down this frame.</summary>
        static bool PressedAnything()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame
                || mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
        }
    }
}
