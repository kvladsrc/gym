using System;
using System.Collections;
using Realms.Core;

namespace Realms
{
    /// <summary>
    /// The crown in the game (the economy and fun plans of 2026-11-11): the gems the players'
    /// deeds and the turns win, and the end of the game once a crown is set. The game's first
    /// crown is shown once, whoever set it; the players may play on, and their own crown, if it
    /// comes later, is shown once more with its place.
    /// </summary>
    public sealed partial class RealmsGame
    {
        bool firstCrownShown, ownCrownShown;

        /// <summary>
        /// After the players' deed (a build, a purchase, a town founded or grown, a hero's way
        /// with its fights): the gems it won, a line for the players' newest, and the end.
        /// </summary>
        void CrownAfterDeed()
        {
            int player = state.World.Player;
            Gem? newest = null;
            bool any = false;
            foreach ((int country, Gem gem) in state.CheckCrown())
            {
                any = true;
                if (country == player)
                    newest = gem;
            }
            if (!any)
                return;
            hud.Refresh();
            if (newest != null)
                hud.Toast(Words.GemToast(newest.Value, state.Gems[player].Count), 6f);
            ShowCrownEnd();
        }

        /// <summary>
        /// The end of the game, once: the first crown set (the players' own or a neighbour's), and
        /// the players' own when it comes after a neighbour's. The capture shows it only when asked.
        /// </summary>
        void ShowCrownEnd(bool force = false)
        {
            if (capturing && !force)
                return;
            int player = state.World.Player;
            int winner = state.Winner;
            if (winner < 0)
                return;
            if (!firstCrownShown)
            {
                firstCrownShown = true;
                if (winner == player)
                    ownCrownShown = true;
                hud.CrownEnd(winner, state.CrownPlace(winner), CrownChosen);
            }
            if (!ownCrownShown && state.Crowned[player] > 0)
            {
                ownCrownShown = true;
                hud.CrownEnd(player, state.CrownPlace(player), CrownChosen);
            }
        }

        /// <summary>The end's answer: 0 plays on, 1 starts a new game once the hero's way is done.</summary>
        void CrownChosen(int choice)
        {
            if (choice == 1)
                StartCoroutine(NewWorldWhenFree());
        }

        IEnumerator NewWorldWhenFree()
        {
            while (busy)
                yield return null;
            NewWorld(Environment.TickCount & 0xFFFFF);
        }
    }
}
