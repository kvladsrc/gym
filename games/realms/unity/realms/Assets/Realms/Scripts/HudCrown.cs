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
        readonly List<(Gem Gem, VisualElement Row, VisualElement Icon, Label Progress)> crownRows =
            new List<(Gem, VisualElement, VisualElement, Label)>();
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
                Text(body, "crown-row-feat", Words.GemFeat(gem), regular);
                crownRows.Add((gem, row, icon, progress));
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
            for (int k = 0; k < crownGems.Length; k++)
                SetGem(crownGems[k], Crown.Order[k], state.HasGem(player, Crown.Order[k]));
            int have = state.Gems[player].Count;
            kingdomsButton.tooltip = Words.CrownTip(have);
            foreach ((Gem gem, VisualElement row, VisualElement icon, Label progress) in crownRows)
            {
                bool won = state.HasGem(player, gem);
                SetGem(icon, gem, won);
                row.EnableInClassList("crown-row-won", won);
                if (won)
                    progress.text = Words.GemWon(GemTurn(player, gem));
                else
                {
                    (int now, int need) = Crown.Progress(state, player, gem);
                    progress.text = Words.GemProgress(gem, now, need);
                }
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
                Text(body, "crown-end-map", Words.MenuMap(world.Seed, state.Turn), bold);
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
