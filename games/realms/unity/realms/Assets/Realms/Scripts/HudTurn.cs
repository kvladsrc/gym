using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The end of a turn shown (the visual plan's step 4 and the interface plan's 3.4 of
    /// 2026-11-11; the players asked for an animation of the turn's change on 2026-11-10). The
    /// turn's rules are done at once; then, for about a second and a half, the players see what it
    /// brought: chips of the income fly from their towns in view to the treasury, whose numbers run
    /// up; the season's new pip pops; the towns' new houses rise one after another
    /// (PropBuilder.RisingHouses), each with «+1 дом» over its town; last the day's events come up
    /// along the right edge. Nothing waits for it: any key or click ends it at once
    /// (RealmsGame.Update), and the next turn can be asked at any moment.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>A chip's flight, the pause between two chips, and the treasury numbers' run, in seconds.</summary>
        const float ChipSeconds = 0.45f, ChipGap = 0.08f, RunSeconds = 0.5f;

        /// <summary>When the first new house rises after the turn's end, and the pause between two houses.</summary>
        public const float FirstHouse = 0.45f, HouseGap = 0.3f;

        /// <summary>The most chips in the air, the most towns they come from, and the most from one town.</summary>
        const int MostChips = 8, MostTowns = 4, MostFromTown = 3;

        sealed class Chip
        {
            public VisualElement Anchor, Icon;
            public Vector3 From;
            public int Goods;
            public float Start;
        }

        readonly List<Chip> chips = new List<Chip>();
        readonly List<(float At, int City, string Text)> houseFloats = new List<(float, int, string)>();
        readonly VisualElement[] goodsIcon = new VisualElement[4];
        readonly float[] goodsHit = { -10f, -10f, -10f, -10f };
        readonly int[] runFrom = new int[4], runTo = new int[4];
        VisualElement chipLayer, poppedPip;

        /// <summary>
        /// The show's times count from its first frame after the turn's end (<see cref="showBase"/>):
        /// the frame that ends a turn redraws the towns and may take a few tenths of a second, which
        /// would otherwise swallow the show's start.
        /// </summary>
        float showBase = -1f, runStart, eventsAt, showEnd;
        bool showing;
        int showFrame;

        /// <summary>True while the end of a turn is being shown.</summary>
        public bool TurnShowing => showing;

        /// <summary>The gems the players set this turn, which fly into the crown when the day's events come.</summary>
        readonly List<Gem> showGems = new List<Gem>();

        /// <summary>
        /// The gems the players' kingdom set this turn: they fly into the bar's crown as the day's
        /// events come (the visual plan's 12), at once when no show runs.
        /// </summary>
        public void GemsThisTurn(IEnumerable<Gem> gems)
        {
            foreach (Gem gem in gems)
            {
                if (showing)
                    showGems.Add(gem);
                else
                    FlyGem(gem);
            }
            RefreshCrown();
        }

        void FlyShowGems()
        {
            if (showGems.Count == 0)
                return;
            var gems = new List<Gem>(showGems);
            showGems.Clear();
            foreach (Gem gem in gems)
                FlyGem(gem);
        }

        /// <summary>The frame the show began on: the key that ended the turn does not end its show.</summary>
        public int TurnShowFrame => showFrame;

        void BuildTurnShow()
        {
            chips.Clear();
            houseFloats.Clear();
            showing = false;
            poppedPip = null;
            // Over the top bar, which the chips fly into.
            chipLayer = Child(root, "turn-chips");
            chipLayer.pickingMode = PickingMode.Ignore;
        }

        /// <summary>
        /// Shows the turn just ended: <paramref name="before"/> is the treasury before it,
        /// <paramref name="income"/> what the land brought, and <paramref name="grown"/> the players'
        /// towns that got a house, in the order their houses rise (see <see cref="FirstHouse"/>),
        /// with whether the town reached a new level.
        /// </summary>
        public void ShowTurn(Stock before, Stock income, IReadOnlyList<(int City, bool Level)> grown)
        {
            FinishTurnShow();
            if (state == null || chipLayer == null || cam == null || root?.panel == null)
                return;
            World world = state.World;
            Stock after = state.Treasury[world.Player];
            const float now = 0f;
            showing = true;
            showBase = -1f;
            showFrame = Time.frameCount;
            Fill(runFrom, before);
            Fill(runTo, after);
            for (int g = 0; g < 4; g++)
                amounts[g].text = runFrom[g].ToString();

            // The goods the land brought, gold first, then the largest.
            int[] brought = { income.Gold, income.Food, income.Wood, income.Stone };
            var goods = new List<int>();
            for (int g = 1; g < 4; g++)
            {
                if (brought[g] > 0)
                    goods.Add(g);
            }
            goods.Sort((a, b) => brought[b].CompareTo(brought[a]));
            if (brought[0] > 0)
                goods.Insert(0, 0);
            // From the players' towns in view, nearest the middle of the screen first.
            var towns = new List<(int City, float Away)>();
            foreach (int index in world.Countries[world.Player].Cities)
            {
                Vector3 v = cam.WorldToViewportPoint(TownPoint(index));
                if (v.z > 0f && v.x > 0.04f && v.x < 0.96f && v.y > 0.2f && v.y < 0.86f)
                    towns.Add((index, (new Vector2(v.x, v.y) - new Vector2(0.5f, 0.5f)).sqrMagnitude));
            }
            towns.Sort((a, b) => a.Away.CompareTo(b.Away));
            if (towns.Count > MostTowns)
                towns.RemoveRange(MostTowns, towns.Count - MostTowns);
            for (int round = 0; round < Mathf.Min(MostFromTown, goods.Count); round++)
            {
                foreach (var town in towns)
                {
                    if (chips.Count == MostChips)
                        break;
                    VisualElement anchor = Child(chipLayer, "anchor");
                    VisualElement icon = Child(anchor, "turn-chip");
                    // A layer that ignores the pointer still lets its children take it.
                    anchor.pickingMode = icon.pickingMode = PickingMode.Ignore;
                    SetIcon(icon, GoodsIcons[goods[round]]);
                    anchor.style.opacity = 0f;
                    chips.Add(new Chip { Anchor = anchor, Icon = icon, From = TownPoint(town.City), Goods = goods[round], Start = chips.Count * ChipGap });
                }
            }
            float landed = chips.Count > 0 ? chips[chips.Count - 1].Start + ChipSeconds : now;
            runStart = chips.Count > 0 ? ChipSeconds : 0.1f;

            // The season's new pip.
            int season = (state.Turn - 1) % 4;
            poppedPip = pips[season];

            // A word over each town as its new house rises.
            float lastHouse = now;
            for (int i = 0; i < grown.Count; i++)
            {
                (int city, bool level) = grown[i];
                float at = now + FirstHouse + i * HouseGap;
                lastHouse = at + 0.45f;
                string text = level ? Words.Level(Towns.Level(world.Cities[city])) + "!" : T("+1 дом", "+1 house");
                houseFloats.Add((at, city, text));
            }
            // The day's events come last, when the money and the houses are in.
            eventsAt = Mathf.Min(now + 1.6f, Mathf.Max(now + 0.6f, Mathf.Max(landed, lastHouse)));
            eventRows.style.opacity = 0f;
            showEnd = Mathf.Max(runStart + RunSeconds, Mathf.Max(eventsAt + 0.3f, lastHouse));
        }

        static void Fill(int[] into, Stock stock)
        {
            into[0] = stock.Gold;
            into[1] = stock.Food;
            into[2] = stock.Wood;
            into[3] = stock.Stone;
        }

        /// <summary>A point a little above the middle of a town, for the chips and the words over it.</summary>
        Vector3 TownPoint(int city)
        {
            World world = state.World;
            int cell = world.Cities[city].Cell;
            float x = world.CenterX(cell), z = world.CenterZ(cell);
            return new Vector3(x, view.Field.Height(x, z) + 0.3f, z);
        }

        /// <summary>The show at once to its end (a key, a click, a dialog), the words over the towns gone with it.</summary>
        public void FinishTurnShow() => EndShow(true);

        void EndShow(bool cut)
        {
            if (!showing)
                return;
            showing = false;
            foreach (Chip chip in chips)
                chip.Anchor.RemoveFromHierarchy();
            chips.Clear();
            houseFloats.Clear();
            if (cut)
            {
                for (int i = floats.Count - 1; i >= 0; i--)
                {
                    if (!floats[i].Label.ClassListContains("float-house"))
                        continue;
                    floats[i].Anchor.RemoveFromHierarchy();
                    floats.RemoveAt(i);
                }
            }
            // The treasury as it is now: a click that ended the show may have spent from it already.
            Fill(runTo, state.Treasury[state.World.Player]);
            for (int g = 0; g < 4; g++)
            {
                amounts[g].text = runTo[g].ToString();
                goodsHit[g] = -10f;
                if (goodsIcon[g] != null)
                    goodsIcon[g].style.scale = new StyleScale(StyleKeyword.Null);
            }
            if (poppedPip != null)
                poppedPip.style.scale = new StyleScale(StyleKeyword.Null);
            poppedPip = null;
            if (eventRows != null)
                eventRows.style.opacity = 1f;
            FlyShowGems();
        }

        void UpdateTurnShow()
        {
            if (!showing)
                return;
            // A dialog (the crown's end of the game) takes the screen: the show is over.
            if (DialogOpen)
            {
                EndShow(true);
                return;
            }
            if (showBase < 0f)
            {
                if (Time.frameCount == showFrame)
                    return;
                showBase = ViewClock.Now;
            }
            float now = ViewClock.Now - showBase;
            for (int i = chips.Count - 1; i >= 0; i--)
            {
                Chip chip = chips[i];
                float k = (now - chip.Start) / ChipSeconds;
                if (k < 0f)
                    continue;
                if (k >= 1f)
                {
                    chip.Anchor.RemoveFromHierarchy();
                    chips.RemoveAt(i);
                    goodsHit[chip.Goods] = now;
                    continue;
                }
                Vector2 to = goodsIcon[chip.Goods].worldBound.center;
                Vector3 screen = cam.WorldToScreenPoint(chip.From);
                Vector2 from = screen.z > 0f ? RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, chip.From, cam) : to;
                float e = k * k * (3f - 2f * k);
                // Up in an arc, as if tossed into the treasury.
                Vector2 p = Vector2.Lerp(from, to, e) + new Vector2(0f, -60f * Mathf.Sin(Mathf.PI * k));
                chip.Anchor.style.translate = new Translate(p.x, p.y);
                chip.Anchor.style.opacity = Mathf.Clamp01(k / 0.12f);
                float s = Mathf.Lerp(1f, 0.7f, e);
                chip.Icon.style.scale = new Scale(new Vector3(s, s, 1f));
            }
            // A treasury's icon swells a little as each chip lands in it.
            for (int g = 0; g < 4; g++)
            {
                float k = (now - goodsHit[g]) / 0.22f;
                float s = k >= 0f && k < 1f ? 1f + 0.18f * (1f - k) : 1f;
                goodsIcon[g].style.scale = new Scale(new Vector3(s, s, 1f));
            }
            // The numbers run from the old treasury to the new.
            float r = Mathf.Clamp01((now - runStart) / RunSeconds);
            r = 1f - (1f - r) * (1f - r);
            for (int g = 0; g < 4; g++)
                amounts[g].text = Mathf.RoundToInt(Mathf.Lerp(runFrom[g], runTo[g], r)).ToString();
            if (poppedPip != null)
            {
                float k = Mathf.Clamp01(now / 0.35f);
                float s = Mathf.Lerp(1.9f, 1f, 1f - (1f - k) * (1f - k));
                poppedPip.style.scale = new Scale(new Vector3(s, s, 1f));
            }
            for (int i = houseFloats.Count - 1; i >= 0; i--)
            {
                if (now < houseFloats[i].At)
                    continue;
                var house = houseFloats[i];
                houseFloats.RemoveAt(i);
                Vector3 point = TownPoint(house.City);
                Float(house.Text, "float-house", point, 1.4f, AboveName(point, state.World.Cities[house.City].Cell));
            }
            eventRows.style.opacity = Mathf.Clamp01((now - eventsAt) / 0.3f);
            if (now >= eventsAt)
                FlyShowGems();
            if (now >= showEnd && chips.Count == 0 && houseFloats.Count == 0)
                EndShow(false);
        }
    }
}
