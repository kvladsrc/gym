using System.Collections;
using System.IO;
using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The promo's recording (<c>-realmsPromo DIR</c>; the family asked for a video in English of
    /// the game's main moments, 2026-11-11): the land from above, the kingdom with its badges
    /// under Alt, a hero's way to a monster group and the battle, and a town's screen with its
    /// turning camera. Time runs at a steady 30 frames a second whatever the laptop manages
    /// (<see cref="Time.captureFramerate"/>), and each clip's frames go to a folder of DIR of its
    /// own, for cutting them together with fades.
    /// </summary>
    public sealed partial class RealmsGame
    {
        const int PromoRate = 30;

        string promoDir, clipDir;
        int clipFrame;

        IEnumerator Promo()
        {
            capturing = true;
            mapCamera.Scripted = true;
            Time.captureFramerate = PromoRate;
            Directory.CreateDirectory(promoDir);
            StartCoroutine(RecordFrames());
            World world = state.World;
            int me = world.Player;
            City capital = world.Cities[world.Countries[me].Capital];
            Vector2 capitalAt = At(capital.Cell);
            // Settle: the ground, the plants and the fog drawn, the first frames' stalls gone.
            yield return Wait(1f);
            Debug.Log($"Promo time: delta {Time.deltaTime:F4}, unscaled {Time.unscaledDeltaTime:F4}");

            // 1. The land from above, no interface: a slow glide down towards the capital, turning to north.
            hud.Hidden = true;
            Clip("01-land");
            yield return Glide(capitalAt + new Vector2(-3f, -5f), 34f, -28f, capitalAt - new Vector2(0f, 1.2f), 19f, 0f, 7f);
            Cut();

            // 2. The kingdom as the players see it, the badges of the whole map while Alt is held.
            hud.Hidden = false;
            Clip("02-kingdom");
            Vector2 from = capitalAt - new Vector2(1.5f, 1f), to = capitalAt + new Vector2(1.5f, -0.2f);
            for (float t = 0f; t < 7f; t += Time.deltaTime)
            {
                hud.BadgesShown = t > 3f;
                mapCamera.Focus(Vector2.Lerp(from, to, Smooth(t / 7f)), Mathf.Lerp(24f, 22f, t / 7f), true);
                yield return null;
            }
            hud.BadgesShown = false;
            Cut();

            // 3. A hero's way to a monster group, and the walk there, the camera following.
            Hero first = state.HeroesOf(me).Count > 0 ? state.HeroesOf(me)[0] : null;
            int group = first != null ? PromoGroup(first) : -1;
            if (group >= 0)
            {
                // A fuller army than a new game's, so the battle has its archers and riders.
                Armies.Add(first.Army, Unit.Archers, 6);
                Armies.Add(first.Army, Unit.Riders, 3);
                Armies.Add(first.Army, Unit.Spearmen, 4);
                ChooseHero(first, false);
                Vector2 start = At(first.Cell), goal = At(group);
                Vector2 follow = Vector2.Lerp(start, goal, 0.35f) - new Vector2(0f, 1f);
                mapCamera.Focus(follow, 14f, true);
                Clip("03-way");
                yield return Wait(0.8f);
                Order(group);
                yield return Wait(1.6f);
                StartCoroutine(Walk());
                // Follow the figure gently until the fight's question comes up.
                while (!hud.DialogOpen)
                {
                    if (heroes.FigureAt(first) is Vector3 figure)
                    {
                        Vector2 wanted = new Vector2(figure.x, figure.z) - new Vector2(0f, 1f);
                        follow = Vector2.Lerp(follow, wanted, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
                        mapCamera.Focus(follow, 14f, true);
                    }
                    yield return null;
                }
                yield return Wait(1.6f);
                Cut();
                hud.AnswerModal(0);

                // 4. The battle, fought by the auto battle, its first seconds and its end.
                yield return Wait(0.4f);
                Clip("04-battle");
                yield return Wait(1.2f);
                battleView.SetAuto(true);
                for (float t = 0f; t < 14f && !hud.DialogOpen; t += Time.deltaTime)
                    yield return null;
                Cut();
                while (!hud.DialogOpen)
                    yield return null;
                // Monsters that give up are let go: the result follows.
                if (battleView.Fight != null && battleView.Fight.Offered)
                {
                    hud.AnswerModal(0);
                    yield return null;
                    while (!hud.DialogOpen)
                        yield return null;
                }
                Clip("05-result");
                yield return Wait(2.2f);
                Cut();
                while (busy)
                {
                    if (hud.DialogOpen)
                        hud.AnswerModal(0);
                    yield return null;
                }
                ChooseHero(null, false);
                Select(-1);
            }

            // 5. The capital's screen: the camera comes down, a building goes up, the camera turns round the town.
            mapCamera.Focus(capitalAt - new Vector2(0f, 1f), 20f, true);
            yield return Wait(0.5f);
            state.Treasury[me] += new Stock(400, 200, 200, 200);
            hud.Refresh();
            Clip("06-town");
            EnterTown(capital.Index);
            yield return Wait(2.5f);
            foreach (TownBuilding building in Economy.TownBuildings)
            {
                if (Economy.TakesSlot(building) && Done(state.Build(me, capital.Index, building), capital.Cell))
                {
                    Debug.Log($"Promo built {building}");
                    break;
                }
            }
            for (float t = 0f; t < 9f; t += Time.deltaTime)
            {
                // About 12 degrees a second, easing in: the players' own Q and E.
                mapCamera.Turn(12f * Mathf.Clamp01(t / 1.5f) * Time.deltaTime);
                if (t > 3f && t - Time.deltaTime <= 3f)
                    hud.ChooseTownTab(1);
                yield return null;
            }
            Cut();
            LeaveScreen();
            Debug.Log("Promo done");
            yield return Wait(0.2f);
            Application.Quit();
        }

        /// <summary>A monster group the hero can walk to this turn, two to five steps away, of a kind the start army beats; the hero is moved near one if none is.</summary>
        int PromoGroup(Hero h)
        {
            World world = state.World;
            int best = -1, bestLength = int.MaxValue;
            foreach (Cell c in world.Cells)
            {
                if (c.Lair || !(c.Monster == Monster.Boar || c.Monster == Monster.Goblins || c.Monster == Monster.Bandits))
                    continue;
                HeroPath way = state.Path(h, c.Index);
                if (way == null || way.Turns[way.Turns.Count - 1] != 0 || way.Cells.Count < 2 || way.Cells.Count > 5)
                    continue;
                if (way.Cells.Count < bestLength)
                {
                    best = c.Index;
                    bestLength = way.Cells.Count;
                }
            }
            if (best >= 0)
                return Reveal(best);
            // None near: the hero is set down three cells from the nearest such group.
            int group = Nearest(h.Cell, c => !c.Lair && (c.Monster == Monster.Boar || c.Monster == Monster.Goblins));
            if (group < 0)
                return -1;
            Hex hex = world.Cells[group].Hex;
            int was = h.Cell;
            foreach (Cell c in world.Cells)
            {
                if (Hex.Distance(c.Hex, hex) != 3 || c.IsWater || c.Monster != Monster.None || c.City >= 0 || state.HeroAt(c.Index) != null)
                    continue;
                h.Cell = c.Index;
                HeroPath way = state.Path(h, group);
                if (way != null && way.Turns[way.Turns.Count - 1] == 0)
                {
                    state.Reveal(c.Index, 2);
                    heroes.Refresh();
                    return Reveal(group);
                }
            }
            h.Cell = was;
            return -1;
        }

        int Reveal(int cell)
        {
            if (!state.Seen[cell])
                state.Reveal(cell, 1);
            view.UpdateFog();
            hud.RebuildLabels();
            hud.WorldChanged();
            return cell;
        }

        /// <summary>The camera from one pose to another, easing in and out, over <paramref name="seconds"/>.</summary>
        IEnumerator Glide(Vector2 from, float fromDistance, float fromYaw, Vector2 to, float toDistance, float toYaw, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Smooth(t / seconds);
                mapCamera.Face(Mathf.LerpAngle(fromYaw, toYaw, k), true);
                mapCamera.Focus(Vector2.Lerp(from, to, k), Mathf.Lerp(fromDistance, toDistance, k), true);
                yield return null;
            }
        }

        static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        Vector2 At(int cell) => new Vector2(state.World.CenterX(cell), state.World.CenterZ(cell));

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
                yield return null;
        }

        void Clip(string name)
        {
            clipDir = Path.Combine(promoDir, name);
            Directory.CreateDirectory(clipDir);
            clipFrame = 0;
        }

        void Cut()
        {
            if (clipDir != null)
                Debug.Log($"Promo clip {Path.GetFileName(clipDir)}: {clipFrame} frames");
            clipDir = null;
        }

        /// <summary>Each drawn frame of a clip, as a JPEG of the whole screen with the interface.</summary>
        IEnumerator RecordFrames()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                if (clipDir == null)
                    continue;
                Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(clipDir, $"{clipFrame++:D5}.jpg"), frame.EncodeToJPG(95));
                Destroy(frame);
            }
        }
    }
}
