using System.Collections;
using System.Collections.Generic;
using System.IO;
using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The promo's recording (<c>-realmsPromo DIR</c>; the family asked for a video in English of
    /// the game's main moments, 2026-11-11). The players' bot first plays some turns, founding
    /// towns on the way, so the kingdom has towns, roads and buildings to show. Then: the land from
    /// high above, coming down to the capital; the kingdom with the badges under Alt; a hero's way
    /// over open land to a monster group and the battle, turn by turn, to its result; settlers
    /// founding a town; and the capital's screen, a building rising in the middle of the frame and
    /// troops hired as the camera turns round the town. This second take follows the review of the
    /// first one's montage: nearer cameras, stills round each action, no auto battle's line, the
    /// camera's turn away from the fog. Time runs at a steady 30 frames a second whatever the
    /// laptop manages (<see cref="Time.captureFramerate"/>), and each clip's frames go to a
    /// folder of DIR of its own, for cutting them together with fades.
    /// </summary>
    public sealed partial class RealmsGame
    {
        const int PromoRate = 30, PromoTurns = 20;

        string promoDir, clipDir;
        int clipFrame;

        IEnumerator Promo()
        {
            capturing = true;
            mapCamera.Scripted = true;
            Time.captureFramerate = PromoRate;
            Directory.CreateDirectory(promoDir);
            World world = state.World;
            int me = world.Player;
            yield return Older(PromoTurns);
            City capital = world.Cities[world.Countries[me].Capital];
            Vector2 capitalAt = At(capital.Cell);
            Vector2 kingdomAt = KingdomMiddle(me, capitalAt, out Vector2 landAt);
            StartCoroutine(RecordFrames());
            ChooseHero(null, false);
            Select(-1);
            mapCamera.Face(-24f, true);
            mapCamera.Focus(landAt, 40f, true);
            // Settle: the ground, the plants and the fog drawn, the first frames' stalls gone.
            yield return Wait(1f);
            Debug.Log($"Promo time: delta {Time.deltaTime:F4}, view {ViewClock.Delta:F4}");

            // 1. The land from high above, no interface, coming down to the capital.
            hud.Hidden = true;
            Clip("01-land");
            Vector2 close = capitalAt - new Vector2(0f, 0.6f);
            yield return Glide(landAt, 40f, -24f, close, 13f, 0f, 7f);
            yield return Wait(0.5f);
            Cut();

            // 2. The kingdom: the interface back over the same view, the camera rising over the
            // kingdom's land, then the badges of the whole map while Alt is held.
            hud.Hidden = false;
            Clip("02-kingdom");
            yield return Wait(0.5f);
            yield return Glide(close, 13f, 0f, kingdomAt, 25f, 0f, 1.8f);
            Vector2 drift = kingdomAt + new Vector2(0.8f, 0.3f);
            hud.BadgesShown = true;
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                mapCamera.Focus(Vector2.Lerp(kingdomAt, drift, Smooth(t / 5f)), 25f, true);
                yield return null;
            }
            hud.BadgesShown = false;
            yield return Wait(0.5f);
            Cut();

            // 3. A hero's way over open land to a monster group, the camera near and following.
            List<Hero> heroesOfMine = state.HeroesOf(me);
            Hero first = heroesOfMine.Count > 0 ? heroesOfMine[0] : null;
            int group = first != null ? PromoGroup(first) : -1;
            if (group >= 0)
            {
                ChooseHero(first, false);
                Vector2 follow = At(first.Cell) - new Vector2(0f, 0.5f);
                mapCamera.Focus(follow, 8f, true);
                yield return Wait(0.3f);
                Clip("03-way");
                yield return Wait(1f);
                Order(group);
                yield return Wait(1.2f);
                StartCoroutine(Walk());
                while (!hud.DialogOpen)
                {
                    follow = Follow(first, follow, 8f);
                    yield return null;
                }
                yield return Wait(2.5f);
                Cut();
                hud.AnswerModal(0);

                // 4. The battle, each of the players' turns taken as a player would (the reach lit,
                // a short pause) rather than by the auto battle, to the result's window.
                battleView.Hands = 0.6f;
                yield return Wait(0.3f);
                Clip("04-battle");
                yield return Wait(1f);
                bool nearer = false;
                for (float t = 0f; t < 45f && !hud.DialogOpen; t += Time.deltaTime)
                {
                    if (!nearer && t > 2.5f)
                    {
                        nearer = true;
                        StartCoroutine(battleView.Nearer(0.22f, 1.6f));
                    }
                    yield return null;
                }
                // Monsters that give up are let go: the result follows.
                if (battleView.Fight != null && battleView.Fight.Offered)
                {
                    yield return Wait(1.2f);
                    hud.AnswerModal(0);
                    yield return null;
                    while (!hud.DialogOpen)
                        yield return null;
                }
                yield return Wait(3f);
                Cut();
                battleView.Hands = 0f;
                while (busy)
                {
                    if (hud.DialogOpen)
                        hud.AnswerModal(0);
                    yield return null;
                }
                ChooseHero(null, false);
                Select(-1);
            }

            // 5. Settlers found a town: the hero takes the wagon a few steps on, names the town and it stands.
            heroesOfMine = state.HeroesOf(me);
            Hero settler = heroesOfMine.Count > 0 ? heroesOfMine[0] : null;
            if (settler != null && PromoSite(settler, out int site))
            {
                hud.ClearToast();
                settler.Settlers = true;
                heroes.Refresh();
                ChooseHero(settler, false);
                Vector2 follow = Vector2.Lerp(At(settler.Cell), At(site), 0.3f) - new Vector2(0f, 0.5f);
                mapCamera.Focus(follow, 8f, true);
                yield return Wait(0.3f);
                Clip("05-found");
                yield return Wait(0.8f);
                Order(site);
                yield return Wait(0.8f);
                StartCoroutine(Walk());
                yield return null;
                while (busy)
                {
                    follow = Follow(settler, follow, 8f);
                    yield return null;
                }
                yield return Wait(0.5f);
                StartCoroutine(Found(settler));
                while (!hud.DialogOpen)
                    yield return null;
                yield return Wait(1.5f);
                hud.AnswerModal(0);
                yield return Wait(3f);
                Cut();
                LeaveScreen();
                ChooseHero(null, false);
                Select(-1);
            }

            // 6. The capital's screen: a building rises in the middle of the frame, troops are
            // hired, and the camera turns round the town to the side away from the fog.
            mapCamera.Focus(capitalAt - new Vector2(0f, 1f), 20f, true);
            TownBuilding? next = ReadyTown(capital);
            yield return Wait(0.8f);
            hud.ClearToast();
            Clip("06-town");
            EnterTown(capital.Index);
            mapCamera.Turn(0f);
            yield return Wait(2f);
            if (next is TownBuilding building)
            {
                Rise.Delay = 1.2f;
                Rise.Seconds = 1.1f;
                bool built = Done(state.Build(me, capital.Index, building), capital.Cell, building);
                Rise.Delay = 0f;
                Rise.Seconds = 0.45f;
                hud.ClearToast();
                Debug.Log($"Promo built {building}: {built}");
                if (built && Rise.Last != null)
                {
                    // The camera turns so that the building's place lies on the screen's middle line, the nearer way.
                    Vector3 p = Rise.Last.transform.position;
                    Vector2 away = new Vector2(p.x, p.z) - capitalAt;
                    if (away.sqrMagnitude > 0.01f)
                    {
                        float behind = Mathf.Atan2(away.x, away.y) * Mathf.Rad2Deg, before = behind + 180f;
                        float turn = Mathf.DeltaAngle(mapCamera.Yaw, before);
                        if (Mathf.Abs(Mathf.DeltaAngle(mapCamera.Yaw, behind)) < Mathf.Abs(turn))
                            turn = Mathf.DeltaAngle(mapCamera.Yaw, behind);
                        mapCamera.Turn(turn);
                    }
                }
                yield return Wait(1.2f + 1.1f + 1f);
            }
            hud.ChooseTownTab(1);
            yield return Wait(1.2f);
            foreach (Unit unit in Units.Hired)
            {
                int count = state.Affordable(me, capital.Index, unit);
                if (count > 0)
                {
                    Recruit(capital.Index, unit, count);
                    Debug.Log($"Promo hired {count} {unit}");
                    break;
                }
            }
            yield return Wait(1.5f);
            float side = ClearSide(capitalAt, mapCamera.Yaw, 50f);
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                // About 8.5 degrees a second, easing in: the players' own Q and E.
                mapCamera.Turn(side * 8.5f * Mathf.Clamp01(t / 1.2f) * Time.deltaTime);
                yield return null;
            }
            yield return Wait(0.5f);
            Cut();
            LeaveScreen();
            Debug.Log("Promo done");
            yield return Wait(0.2f);
            Application.Quit();
        }

        /// <summary>
        /// An older game than a new map's: the players' bot plays <paramref name="turns"/> turns
        /// (<see cref="AutoTurn"/>, the map drawn once at the end), a town founded every six turns,
        /// and the towns keep the last turns' recruits for the hiring on camera.
        /// </summary>
        IEnumerator Older(int turns)
        {
            World world = state.World;
            int me = world.Player;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int turn = 0; turn < turns; turn++)
            {
                if (turn % 6 == 2)
                    FoundNear(me);
                AutoTurn(false, turn < turns - 3);
                if (turn % 4 == 3)
                    yield return null;
            }
            RedrawMap();
            view.UpdateFog();
            heroes.Refresh();
            hud.RebuildLabels();
            hud.WorldChanged();
            hud.Refresh();
            hud.RefreshInfo();
            Debug.Log($"Promo: the players' bot played {turns} turns in {watch.ElapsedMilliseconds} ms: turn {state.Turn}, " +
                $"{world.Countries[me].Cities.Count} towns, treasury {state.Treasury[me]}");
        }

        /// <summary>A town founded on the free site nearest the capital, the settlers paid for: the older game's towns.</summary>
        void FoundNear(int me)
        {
            World world = state.World;
            List<Hero> mine = state.HeroesOf(me);
            Stock cost = Settling.Cost(world, me);
            if (mine.Count == 0 || !state.Treasury[me].Covers(cost))
                return;
            Hero h = mine[0];
            City capital = world.Cities[world.Countries[me].Capital];
            int site = Nearest(capital.Cell, c => (c.Owner == me || c.Owner < 0) && state.HeroAt(c.Index) == null
                && Settling.CanFoundOn(world, me, c.Index) == Refusal.None);
            if (site < 0)
                return;
            int was = h.Cell;
            h.Cell = site;
            h.Settlers = true;
            Refusal refusal = state.Found(h, Settling.NewName(world));
            h.Cell = was;
            h.Settlers = false;
            if (refusal == Refusal.None)
                state.Treasury[me] -= cost;
        }

        /// <summary>The middle of the kingdom's land, and of all the land seen (<paramref name="land"/>).</summary>
        Vector2 KingdomMiddle(int me, Vector2 fallback, out Vector2 land)
        {
            Vector2 mine = Vector2.zero, seen = Vector2.zero;
            int m = 0, s = 0;
            foreach (Cell c in state.World.Cells)
            {
                if (c.IsWater || !state.Seen[c.Index])
                    continue;
                Vector2 at = At(c.Index);
                seen += at;
                s++;
                if (c.Owner == me)
                {
                    mine += at;
                    m++;
                }
            }
            land = s > 0 ? seen / s : fallback;
            return m > 0 ? mine / m : fallback;
        }

        /// <summary>The camera eased after the hero's figure, the figure a little above the screen's middle.</summary>
        Vector2 Follow(Hero h, Vector2 follow, float distance)
        {
            if (heroes.FigureAt(h) is Vector3 figure)
            {
                Vector2 wanted = new Vector2(figure.x, figure.z) - new Vector2(0f, 0.5f);
                follow = Vector2.Lerp(follow, wanted, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
                mapCamera.Focus(follow, distance, true);
            }
            return follow;
        }

        static bool PromoKind(Cell c) => !c.Lair && (c.Monster == Monster.Boar || c.Monster == Monster.Bandits || c.Monster == Monster.Goblins);

        /// <summary>
        /// A monster group for the hero's way: five to nine steps this turn, over open seen land,
        /// of a kind that stands out on the grass (boars and bandits before goblins), one the hero
        /// beats easily but not at once. Without one the hero is set down six cells from one; an
        /// army too weak for it is topped up.
        /// </summary>
        int PromoGroup(Hero h)
        {
            World world = state.World;
            h.Moves = HeroRules.MovesPerTurn;
            int best = -1;
            float bestScore = float.MaxValue;
            foreach (Cell c in world.Cells)
            {
                if (!PromoKind(c) || !state.Seen[c.Index])
                    continue;
                HeroPath way = state.Path(h, c.Index);
                if (way == null || way.Turns[way.Turns.Count - 1] != 0 || way.Cells.Count < 5 || way.Cells.Count > 9)
                    continue;
                float score = WayScore(h, way, c);
                if (score < bestScore)
                {
                    best = c.Index;
                    bestScore = score;
                }
            }
            if (best < 0)
                best = SetDownNear(h);
            if (best < 0)
                return -1;
            if (Armies.Count(h.Army, Unit.Archers) == 0)
                Armies.Add(h.Army, Unit.Archers, 6);
            for (int k = 0; k < 4 && state.OddsOf(h, best, 60).Win < Odds.Easy; k++)
            {
                Armies.Add(h.Army, Unit.Spearmen, 6);
                Armies.Add(h.Army, Unit.Riders, 3);
            }
            Debug.Log($"Promo group {world.Cells[best].Monster} x{world.Cells[best].Count} on {world.Cells[best].Biome}, " +
                $"win {state.OddsOf(h, best, 60).Win:F2}, way {state.Path(h, best)?.Cells.Count}");
            return Reveal(best);
        }

        /// <summary>Lower for a better way: about seven steps, no wood, no fog, a group that stands out, a fight neither lost nor trivial.</summary>
        float WayScore(Hero h, HeroPath way, Cell group)
        {
            float score = Mathf.Abs(way.Cells.Count - 7) * 0.5f;
            foreach (int cell in way.Cells)
            {
                Cell c = state.World.Cells[cell];
                if (c.Biome == Biome.Forest)
                    score += 2f;
                if (!state.Seen[cell])
                    score += 3f;
            }
            if (group.Biome == Biome.Forest)
                score += 3f;
            if (group.Monster == Monster.Goblins)
                score += 3f;
            float win = state.OddsOf(h, group.Index, 40).Win;
            return score + (win < Odds.Easy ? 20f : (win - Odds.Easy) * 6f);
        }

        /// <summary>The hero set down on open land six cells from a group near the capital, when the way there is one turn's.</summary>
        int SetDownNear(Hero h)
        {
            World world = state.World;
            City capital = world.Cities[world.Countries[world.Player].Capital];
            int group = Nearest(capital.Cell, c => PromoKind(c) && c.Biome != Biome.Forest);
            if (group < 0)
                return -1;
            Hex hex = world.Cells[group].Hex;
            int was = h.Cell;
            foreach (Cell c in world.Cells)
            {
                if (Hex.Distance(c.Hex, hex) != 6 || c.IsWater || c.Biome == Biome.Forest || c.Biome == Biome.Mountains
                    || c.Monster != Monster.None || c.City >= 0 || state.HeroAt(c.Index) != null)
                    continue;
                h.Cell = c.Index;
                HeroPath way = state.Path(h, group);
                if (way != null && way.Turns[way.Turns.Count - 1] == 0)
                {
                    state.Reveal(c.Index, 2);
                    heroes.Refresh();
                    view.UpdateFog();
                    return group;
                }
            }
            h.Cell = was;
            return -1;
        }

        /// <summary>
        /// Free land three to five steps from the hero, on open ground, where the settlers may found
        /// a town; from the capital when none is near the hero.
        /// </summary>
        bool PromoSite(Hero h, out int site)
        {
            World world = state.World;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                h.Moves = HeroRules.MovesPerTurn;
                site = -1;
                float bestScore = float.MaxValue;
                foreach (Cell c in world.Cells)
                {
                    if (!state.Seen[c.Index] || c.Biome == Biome.Forest || state.HeroAt(c.Index) != null
                        || Settling.CanFoundOn(world, h.Country, c.Index) != Refusal.None)
                        continue;
                    HeroPath way = state.Path(h, c.Index);
                    if (way == null || way.Turns[way.Turns.Count - 1] != 0 || way.Cells.Count < 3 || way.Cells.Count > 5)
                        continue;
                    float score = Mathf.Abs(way.Cells.Count - 4);
                    foreach (int cell in way.Cells)
                        score += world.Cells[cell].Biome == Biome.Forest ? 1f : 0f;
                    if (score < bestScore)
                    {
                        site = c.Index;
                        bestScore = score;
                    }
                }
                if (site >= 0)
                {
                    Debug.Log($"Promo settlers' site {site} on {world.Cells[site].Biome}, way {state.Path(h, site).Cells.Count}");
                    return true;
                }
                City capital = world.Cities[world.Countries[h.Country].Capital];
                if (state.HeroAt(capital.Cell) != null && state.HeroAt(capital.Cell) != h)
                    break;
                h.Cell = capital.Cell;
                heroes.Refresh();
            }
            site = -1;
            return false;
        }

        /// <summary>
        /// The capital made ready for its screen, off camera: a free place (the town grown onto a
        /// neighbour when full), barracks for the hiring, and the money for one more building,
        /// which is returned (none when every one stands).
        /// </summary>
        TownBuilding? ReadyTown(City capital)
        {
            int me = state.World.Player;
            state.Treasury[me] += new Stock(300, 150, 150, 150);
            if (Towns.Free(capital) == 0)
            {
                int site = ExpandSite(capital);
                if (site >= 0 && state.Expand(me, site) == Refusal.None)
                    RedrawMap();
            }
            if (!capital.Buildings.Contains(TownBuilding.Barracks))
                Done(state.Build(me, capital.Index, TownBuilding.Barracks), capital.Cell, TownBuilding.Barracks);
            bool any = false;
            foreach (Unit unit in Units.Hired)
                any |= state.Affordable(me, capital.Index, unit) > 0;
            if (!any)
                capital.Recruits[(int)Unit.Spearmen] += 6;
            hud.ClearToast();
            foreach (TownBuilding building in Economy.TownBuildings)
            {
                if (Economy.TakesSlot(building) && !capital.Buildings.Contains(building))
                    return building;
            }
            return null;
        }

        /// <summary>
        /// Which way the camera turns round the town, +1 or -1: the side whose next
        /// <paramref name="arc"/> degrees show less unseen land behind the town.
        /// </summary>
        float ClearSide(Vector2 centre, float yaw, float arc)
        {
            float Fog(float facing)
            {
                var forward = new Vector2(Mathf.Sin(facing * Mathf.Deg2Rad), Mathf.Cos(facing * Mathf.Deg2Rad));
                int count = 0;
                foreach (Cell c in state.World.Cells)
                {
                    if (state.Seen[c.Index])
                        continue;
                    Vector2 off = At(c.Index) - centre;
                    float d = off.magnitude;
                    if (d > 1.5f && d < 7f && Vector2.Dot(off / d, forward) > 0.82f)
                        count++;
                }
                return count;
            }
            float left = 0f, right = 0f;
            for (float a = 10f; a <= arc; a += 10f)
            {
                right += Fog(yaw + a);
                left += Fog(yaw - a);
            }
            Debug.Log($"Promo turn: unseen cells {right} turning right, {left} turning left");
            return right <= left ? 1f : -1f;
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
