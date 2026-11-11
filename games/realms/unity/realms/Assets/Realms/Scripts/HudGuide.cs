using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>
    /// The guiding lines' part of the interface (the interface plan's 3.3, 2026-11-11): a thin
    /// «как» line under the task it helps with, or under all the tasks for a one-time hint and the
    /// end-turn call; a gold outline on the button to press next; and the «Следующий ход» button
    /// calling with a gold outline when no task can move on this turn. The rings on the map's cells
    /// are MapView's (BorderMarks); RealmsGuide.cs decides what shows.
    /// </summary>
    public sealed partial class Hud
    {
        Button nextTurnButton;
        Quest? guideQuest;
        string guideLine, guideButton;
        bool guideCall;

        /// <summary>The outlines shown now (the buttons' rings), pulsing in LateUpdate.</summary>
        readonly List<VisualElement> guideRings = new List<VisualElement>();

        /// <summary>
        /// What the guide shows: the line and the task it stands under (null: under all the tasks),
        /// the name of the button to outline (null: none), and whether the end-turn button calls.
        /// </summary>
        public void SetGuide(Quest? under, string line, string button, bool call)
        {
            bool rows = line != guideLine || (line != null && under != guideQuest);
            guideQuest = under;
            guideLine = line;
            if (button != guideButton || call != guideCall)
            {
                guideButton = button;
                guideCall = call;
                OutlineGuided();
            }
            if (rows && state != null)
                BuildQuests();
        }

        /// <summary>
        /// A button the guide may point at, known by its <paramref name="name"/>: it gets a gold
        /// outline inside its edge, shown while it is the one to press.
        /// </summary>
        void Guided(VisualElement button, string name)
        {
            button.userData = name;
            button.AddToClassList("guideable");
            VisualElement ring = Child(button, "guide-ring");
            ring.pickingMode = PickingMode.Ignore;
            if (name != null && name == guideButton)
            {
                button.AddToClassList("guide-target");
                guideRings.Add(ring);
            }
        }

        /// <summary>The outline moves to the button now wanted, and to «Следующий ход» while it calls.</summary>
        void OutlineGuided()
        {
            guideRings.Clear();
            root?.Query<VisualElement>(className: "guideable").ForEach(button =>
            {
                string name = button.userData as string;
                bool on = button == nextTurnButton ? guideCall : guideButton != null && name == guideButton;
                button.EnableInClassList("guide-target", on);
                VisualElement ring = button.Q(className: "guide-ring");
                if (on && ring != null)
                    guideRings.Add(ring);
            });
        }

        /// <summary>The guide's line as a row of the task lines, after its task's row or after them all.</summary>
        void GuideRow()
        {
            VisualElement row = Child(questRows, "quest-guide-row");
            row.pickingMode = PickingMode.Ignore;
            Halo(row, "quest-guide", guideLine, bold);
        }

        /// <summary>The outlines breathe slowly, so they catch the eye without blinking.</summary>
        void PulseGuide()
        {
            if (guideRings.Count == 0)
                return;
            float glow = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.2f);
            for (int k = guideRings.Count - 1; k >= 0; k--)
            {
                // A card drawn again leaves its old buttons off the panel.
                if (guideRings[k].panel == null)
                    guideRings.RemoveAt(k);
                else
                    guideRings[k].style.opacity = glow;
            }
        }

        /// <summary>The town screen's town, or -1 when it is not open.</summary>
        public int TownScreenCity => screenCity;

        /// <summary>Whether the town screen shows its buildings' tab.</summary>
        public bool TownBuildingsTab => townTab == TownTab.Buildings;
    }
}
