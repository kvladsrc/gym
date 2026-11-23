using System;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The crown (the economy and fun plans of 2026-11-11): the top bar's crown button with a
    /// socket for each gem, lit as the gems come; its list, opened by the crown or K, shows each
    /// gem's feat and how far the kingdom has come, over the kingdoms with their own crowns; and
    /// the end of the game, once a crown is set.
    /// </summary>
    public sealed partial class Hud
    {
        readonly VisualElement[] crownGems = new VisualElement[Crown.Order.Length];
        readonly List<(Gem Gem, VisualElement Row, VisualElement Icon, Label Progress, Label Feat)> crownRows =
            new List<(Gem, VisualElement, VisualElement, Label, Label)>();
        readonly List<(int Country, Label Gems)> rowGems = new List<(int, Label)>();
        readonly List<(int Country, VisualElement Gem)> rowAims = new List<(int, VisualElement)>();

        /// <summary>The bar's crown with the gems' sockets beside it; it opens the crown's list over the kingdoms.</summary>
        Button CrownButton(VisualElement bar)
        {
            Button button = PressButton(ToggleKingdoms);
            button.AddToClassList("button");
            button.AddToClassList("crown-button");
            button.focusable = false;
            bar.Add(button);
            crownIcon = Child(button, "button-icon");
            crownIcon.pickingMode = PickingMode.Ignore;
            VisualElement gems = Child(button, "crown-gems");
            gems.pickingMode = PickingMode.Ignore;
            for (int k = 0; k < crownGems.Length; k++)
            {
                crownGems[k] = Child(gems, "crown-gem");
                crownGems[k].pickingMode = PickingMode.Ignore;
            }
            return button;
        }

        /// <summary>The crown's part of the list: its rule and a row for each gem's feat.</summary>
        void BuildCrownList(VisualElement parent)
        {
            VisualElement head = Child(parent, "kingdoms-head");
            Text(head, "kingdoms-title", Words.CrownTitle, black);
            Text(head, "key-hint-dark", "K", bold);
            Text(parent, "kingdoms-note", Words.CrownGoal, regular);
            crownRows.Clear();
            foreach (Gem gem in Crown.Order)
            {
                VisualElement row = Child(parent, "crown-row");
                VisualElement icon = Child(row, "crown-row-gem");
                VisualElement body = Child(row, "crown-row-body");
                VisualElement top = Child(body, "crown-row-top");
                Text(top, "crown-row-name", Words.Gem(gem), black);
                Label progress = Text(top, "crown-row-progress", "", bold);
                // The feat's words wait for a game: the Lands gem's goal follows the map's size.
                Label feat = Text(body, "crown-row-feat", "", regular);
                crownRows.Add((gem, row, icon, progress, feat));
            }
            Child(parent, "crown-divider");
        }

        /// <summary>The kingdom's gems in its row of the kingdoms' list: a small crown and «1 из 3».</summary>
        void KingdomCrown(VisualElement stats, int country)
        {
            Text(stats, "kingdom-row-dot", "·", bold);
            SetIcon(Child(stats, "kingdom-row-coin"), "crown");
            rowGems.Add((country, Text(stats, "kingdom-row-stat", "", bold)));
            // A neighbour's row shows the gem it goes after now (its temper, the economy plan's step 2).
            if (country != state.World.Player)
                rowAims.Add((country, Child(stats, "kingdom-row-aim")));
        }

        /// <summary>The bar's sockets, the feats' progress and the kingdoms' gems as they are now.</summary>
        void RefreshCrown()
        {
            if (state?.Gems == null)
                return;
            int player = state.World.Player;
            // A socket whose gem is on its way stays empty until the gem lands.
            for (int k = 0; k < crownGems.Length; k++)
                SetGem(crownGems[k], Crown.Order[k], state.HasGem(player, Crown.Order[k]) && !GemComing(k));
            int have = state.Gems[player].Count;
            kingdomsButton.tooltip = Words.CrownTip(have);
            foreach ((Gem gem, VisualElement row, VisualElement icon, Label progress, Label feat) in crownRows)
            {
                bool won = state.HasGem(player, gem);
                SetGem(icon, gem, won);
                row.EnableInClassList("crown-row-won", won);
                (int now, int need) = Crown.Progress(state, player, gem);
                feat.text = Words.GemFeat(gem, need);
                progress.text = won ? Words.GemWon(GemTurn(player, gem)) : Words.GemProgress(gem, now, need);
            }
            foreach ((int country, Label label) in rowGems)
                label.text = Words.CrownCount(state.Gems[country].Count);
            foreach ((int country, VisualElement element) in rowAims)
            {
                Gem? aim = state.Aim(country);
                element.style.display = aim.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
                if (!aim.HasValue)
                    continue;
                SetGem(element, aim.Value, true);
                element.tooltip = Words.RivalAim(aim.Value);
            }
        }

        /// <summary>The players' crown is celebrated over their towns: the panels step aside, the top bar stays.</summary>
        public void Parade(bool on) => root?.EnableInClassList("on-parade", on);

        /// <summary>A gem on its way into the bar's crown, from the capital into its socket.</summary>
        sealed class GemFlight
        {
            public VisualElement Icon;
            public Vector2 From;
            public int Socket;
            public float Start;
        }

        readonly List<GemFlight> gemFlights = new List<GemFlight>();

        /// <summary>When each socket took its gem, for its swell.</summary>
        readonly float[] socketLanded = { -10f, -10f, -10f, -10f, -10f, -10f, -10f, -10f };

        /// <summary>How long a gem flies into the crown and how long its socket swells, in seconds.</summary>
        const float GemFlightSeconds = 1f, GemSwellSeconds = 0.35f;

        /// <summary>A gem is flying into the crown.</summary>
        public bool GemsFlying => gemFlights.Count > 0 || showGems.Count > 0;

        bool GemComing(int socket)
        {
            foreach (GemFlight flight in gemFlights)
            {
                if (flight.Socket == socket)
                    return true;
            }
            foreach (Gem gem in showGems)
            {
                if (Array.IndexOf(Crown.Order, gem) == socket)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// A gem the players won rises from their capital and flies into its socket in the bar's
        /// crown in a second; the socket stays empty until it lands and then swells (the visual
        /// plan's 12: each part of the crown flies into its place). From the screen's upper middle
        /// when the capital is off the screen.
        /// </summary>
        public void FlyGem(Gem gem)
        {
            int socket = Array.IndexOf(Crown.Order, gem);
            if (socket < 0 || socket >= crownGems.Length || crownGems[socket] == null || root?.panel == null || state == null)
                return;
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("gem-flight");
            SetGem(icon, gem, true);
            root.Add(icon);
            gemFlights.Add(new GemFlight { Icon = icon, From = CapitalOnPanel(), Socket = socket, Start = ViewClock.Now });
            SetGem(crownGems[socket], gem, false);
            UpdateGemFlights();
        }

        /// <summary>The players' capital on the panel, or the panel's upper middle when it is not on the screen.</summary>
        Vector2 CapitalOnPanel()
        {
            World world = state.World;
            int capital = world.Countries[world.Player].Capital;
            if (capital >= 0 && cam != null)
            {
                Vector3 screen = cam.WorldToScreenPoint(TownPoint(capital));
                if (screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height)
                    return PanelPoint(screen);
            }
            Rect panel = root.worldBound;
            return new Vector2(panel.center.x, panel.yMin + panel.height * 0.35f);
        }

        /// <summary>Each frame: the gems on their way, along an arc that rises first, and the sockets that took one.</summary>
        void UpdateGemFlights()
        {
            float now = ViewClock.Now;
            for (int i = gemFlights.Count - 1; i >= 0; i--)
            {
                GemFlight flight = gemFlights[i];
                float t = Mathf.Clamp01((now - flight.Start) / GemFlightSeconds);
                if (t >= 1f)
                {
                    flight.Icon.RemoveFromHierarchy();
                    gemFlights.RemoveAt(i);
                    socketLanded[flight.Socket] = now;
                    RefreshCrown();
                    continue;
                }
                Rect to = crownGems[flight.Socket].worldBound;
                float e = t * t * (3f - 2f * t);
                Vector2 at = Vector2.Lerp(flight.From, to.center, e);
                at.y -= Mathf.Sin(Mathf.PI * e) * 90f;
                float size = Mathf.Lerp(64f, Mathf.Max(14f, to.width), e);
                flight.Icon.style.left = at.x - size * 0.5f;
                flight.Icon.style.top = at.y - size * 0.5f;
                flight.Icon.style.width = size;
                flight.Icon.style.height = size;
                flight.Icon.style.opacity = Mathf.Clamp01(t / 0.12f);
            }
            for (int k = 0; k < crownGems.Length; k++)
            {
                if (socketLanded[k] < 0f || crownGems[k] == null)
                    continue;
                float since = now - socketLanded[k];
                if (since >= GemSwellSeconds)
                {
                    crownGems[k].style.scale = new StyleScale(StyleKeyword.Null);
                    socketLanded[k] = -10f;
                    continue;
                }
                float swell = 1f + 0.7f * Mathf.Sin(Mathf.PI * since / GemSwellSeconds);
                crownGems[k].style.scale = new Scale(new Vector3(swell, swell, 1f));
            }
        }

        int GemTurn(int country, Gem gem)
        {
            foreach (GemWon won in state.Gems[country])
            {
                if (won.Gem == gem)
                    return won.Turn;
            }
            return 0;
        }

        /// <summary>
        /// A gem, or its empty socket, in an element: the painted gems of the interface's art, and
        /// until they are in, a stone of the stylesheet (a coloured square turned on its corner).
        /// </summary>
        void SetGem(VisualElement element, Gem gem, bool won)
        {
            Texture2D texture = IconTexture(won ? Words.GemIcon(gem) : "gem_socket");
            element.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
            bool plain = texture == null;
            for (int g = 0; g < Crown.Order.Length; g++)
                element.EnableInClassList("gem-plain-" + g, plain && won && (int)gem == g);
            element.EnableInClassList("gem-plain-socket", plain && !won);
        }

        /// <summary>
        /// The end of the game: the kingdom whose crown is set, its gems by turn, the game's counts
        /// and the way on. The players' own crown says its place; a neighbour's, set first, lets
        /// them play on for theirs. Enter and Esc both play on: a new game is a click.
        /// </summary>
        public void CrownEnd(int country, int place, Action<int> chosen)
        {
            World world = state.World;
            bool own = country == world.Player;
            int turn = state.Crowned[country];
            string title = own ? Words.CrownWonTitle : Words.CrownLostTitle;
            string text = own ? Words.CrownWonText(turn, place) : Words.CrownLostText(world.Countries[country].Label, turn);
            Ask(title, own ? "crown" : FlagIcon(country), text, own ? Words.CrownWonAnswers : Words.CrownLostAnswers, chosen, body =>
            {
                Text(body, "crown-end-map", Words.MenuMap(world, state.Turn), bold);
                VisualElement gems = Child(body, "crown-end-gems");
                foreach (GemWon won in state.Gems[country])
                {
                    VisualElement item = Child(gems, "crown-end-gem");
                    SetGem(Child(item, "crown-end-stone"), won.Gem, true);
                    Text(item, "crown-end-name", Words.Gem(won.Gem), black);
                    Text(item, "crown-end-turn", T($"ход {won.Turn}", $"turn {won.Turn}"), bold);
                }
                if (!own)
                    return;
                BodyLine(body, Words.StatVictories, state.Victories.ToString());
                BodyLine(body, Words.StatLairs, state.LairsCleared.ToString());
                BodyLine(body, Words.StatChests, state.ChestsOpened.ToString());
                BodyLine(body, Words.StatTowns, state.TownsFounded.ToString());
            }, 0);
        }
    }
}
