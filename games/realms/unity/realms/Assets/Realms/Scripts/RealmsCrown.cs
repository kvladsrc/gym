using System;
using System.Collections;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Realms
{
    /// <summary>
    /// The crown in the game (the economy and fun plans of 2026-11-11): the gems the players'
    /// deeds and the turns win, and the end of the game once a crown is set. The game's first
    /// crown is shown once, whoever set it; the players may play on, and their own crown, if it
    /// comes later, is shown once more with its place. The players' own crown is celebrated
    /// before its window (the visual plan's 12, 2026-11-12).
    /// </summary>
    public sealed partial class RealmsGame
    {
        bool firstCrownShown, ownCrownShown;

        /// <summary>The players' crown is being celebrated: the camera flies over their towns.</summary>
        bool parading;

        /// <summary>How the parade flies: seconds per town, of which the flight; towns at most; distances.</summary>
        const float ParadeStay = 2f, ParadeFly = 1f, ParadeTurn = 5f, ParadeNear = 11f, ParadeBack = 18f;
        const int ParadeTowns = 4;

        /// <summary>
        /// After the players' deed (a build, a purchase, a town founded or grown, a hero's way
        /// with its fights): the gems it won, each flying into the bar's crown, a line for the
        /// players' newest, and the end.
        /// </summary>
        void CrownAfterDeed()
        {
            int player = state.World.Player;
            var mine = new List<Gem>();
            bool any = false;
            foreach ((int country, Gem gem) in state.CheckCrown())
            {
                any = true;
                if (country == player)
                    mine.Add(gem);
            }
            if (!any)
                return;
            hud.Refresh();
            foreach (Gem gem in mine)
                hud.FlyGem(gem);
            if (mine.Count > 0)
            {
                GemSound(state.Gems[player].Count);
                hud.News(Words.GemToast(mine[mine.Count - 1], state.Gems[player].Count), 6f);
            }
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
                EndOfGame(winner);
            }
            if (!ownCrownShown && state.Crowned[player] > 0)
            {
                ownCrownShown = true;
                EndOfGame(player);
            }
        }

        /// <summary>A crown's end: a neighbour's window at once, the players' own after its parade.</summary>
        void EndOfGame(int country)
        {
            if (country == state.World.Player)
            {
                StartCoroutine(ParadeThenEnd(country));
                return;
            }
            CrownSound(country);
            hud.CrownEnd(country, state.CrownPlace(country), CrownChosen);
        }

        /// <summary>
        /// The players' crown: once the deed's walk, the turn's show, the gems' flight and any
        /// window before it are done, the fanfare, the parade over their towns, and the end's window.
        /// </summary>
        IEnumerator ParadeThenEnd(int country)
        {
            while (busy || hud.DialogOpen || hud.MenuOpen || battleView.Active || hud.TurnShowing || hud.GemsFlying)
                yield return null;
            LeaveScreen();
            CrownSound(country);
            yield return Parade(country);
            // A key that cut the parade may have opened the menu: the window waits for it.
            while (hud.MenuOpen || hud.DialogOpen)
                yield return null;
            hud.CrownEnd(country, state.CrownPlace(country), CrownChosen);
        }

        /// <summary>
        /// The players' towns from above (the visual plan's 12, the fun plan's В12): the capital
        /// first and then the largest, two seconds each and four at most, the camera turning
        /// slowly and rising between far towns; then back over the capital, ten seconds in all.
        /// The flags fly harder and the smoke is thicker until the end's window is answered. A
        /// click or a key ends it at once; nothing else is done meanwhile.
        /// </summary>
        IEnumerator Parade(int country)
        {
            World world = state.World;
            var towns = new List<int>(world.Countries[country].Cities);
            if (towns.Count == 0)
                yield break;
            towns.Sort((a, b) =>
            {
                City ca = world.Cities[a], cb = world.Cities[b];
                if (ca.Capital != cb.Capital)
                    return ca.Capital ? -1 : 1;
                return cb.Houses != ca.Houses ? cb.Houses.CompareTo(ca.Houses) : a.CompareTo(b);
            });
            if (towns.Count > ParadeTowns)
                towns.RemoveRange(ParadeTowns, towns.Count - ParadeTowns);

            parading = busy = true;
            mapCamera.Scripted = true;
            view.Feast(true);
            // A flock rises from the capital (the visual plan's 12).
            if (view.Birds != null)
                view.Birds.Rise(TownAt(towns[0]));
            hud.Parade(true);
            mapCamera.MapPose(out Vector2 from, out float fromDistance, out float startFacing);
            float facing = startFacing;
            Vector2 capital = TownAt(towns[0]);
            bool cut = false;
            // The click or key that ended the turn's show or did the deed is not a cut.
            int firstFrame = Time.frameCount;
            // A leg for each town, then the way back over the capital, facing as before.
            for (int leg = 0; leg <= towns.Count && !cut; leg++)
            {
                bool back = leg == towns.Count;
                Vector2 to = back ? capital : TownAt(towns[leg]);
                float toDistance = back ? ParadeBack : ParadeNear, stay = back ? ParadeFly : ParadeStay;
                // Far towns are flown to from higher up.
                float hop = Mathf.Min(Vector2.Distance(from, to) * 0.5f, 14f);
                float start = ViewClock.Now;
                while (true)
                {
                    float t = ViewClock.Now - start;
                    if (t >= stay)
                        break;
                    if (Time.frameCount > firstFrame && ParadeCut())
                    {
                        cut = true;
                        break;
                    }
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / ParadeFly));
                    mapCamera.Focus(Vector2.Lerp(from, to, e), Mathf.Lerp(fromDistance, toDistance, e) + hop * Mathf.Sin(Mathf.PI * e));
                    mapCamera.Face(back ? Mathf.LerpAngle(facing, startFacing, e) : facing + t * ParadeTurn);
                    yield return null;
                }
                if (!back)
                    facing += stay * ParadeTurn;
                from = to;
                fromDistance = toDistance;
            }
            if (cut)
            {
                mapCamera.Focus(capital, ParadeBack);
                mapCamera.Face(startFacing);
            }
            hud.Parade(false);
            mapCamera.Scripted = false;
            parading = busy = false;
        }

        Vector2 TownAt(int city)
        {
            World world = state.World;
            int cell = world.Cities[city].Cell;
            return new Vector2(world.CenterX(cell), world.CenterZ(cell));
        }

        /// <summary>A click or a key ends the parade.</summary>
        static bool ParadeCut()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            return (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
                || (keyboard != null && keyboard.anyKey.wasPressedThisFrame);
        }

        /// <summary>
        /// The end's answer: 0 plays on, 1 opens the new game's window once the hero's way is done
        /// («Назад» there plays on). The feast ends either way.
        /// </summary>
        void CrownChosen(int choice)
        {
            view.Feast(false);
            if (choice == 1)
                StartCoroutine(NewGameWhenFree());
        }

        IEnumerator NewGameWhenFree()
        {
            while (busy)
                yield return null;
            OpenNewGame(MenuMode.Pause);
        }
    }
}
