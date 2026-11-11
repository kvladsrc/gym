using System.Collections;
using System.Collections.Generic;
using System.IO;
using Realms.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realms
{
    /// <summary>
    /// The promo's recording (<c>-realmsPromo DIR</c>; the family asked for a video in English of
    /// the game's main moments, 2026-11-11, showing only gameplay: no directed shots or camera
    /// angles). The players' bot first plays some turns off camera, founding towns on the way, so
    /// the kingdom has towns, roads and buildings; a hero is set near a monster group and a second
    /// one, with settlers, on a free site. Then it plays as the players do, with their controls
    /// only: the wheel and Alt over the kingdom; F, the wheel and two right clicks for a hero's
    /// way; the battle's turns taken one by one; settlers founding a town; Space and the capital's
    /// screen, a building built and troops hired. Time runs at a steady 30 frames a second
    /// (<see cref="Time.captureFramerate"/>), and each clip's frames go to a folder of DIR of its
    /// own, for cutting them together.
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
            List<Hero> mine = PromoHeroes(capital);
            Hero walker = mine.Count > 0 ? mine[0] : null;
            int group = walker != null ? PromoGroup(walker) : -1;
            Hero founder = mine.Count > 1 ? mine[1] : walker;
            TownBuilding? next = ReadyTown(capital);

            // As a saved game opens: over the capital, facing north, nothing chosen, the camera
            // where the players left it, nearer than a new game's (the map's own view of the
            // kingdom is as far as the camera goes, so the wheel would not move it).
            ChooseHero(null, false);
            Select(-1);
            hud.ClearToast();
            mapCamera.Face(0f, true);
            FocusCapital(14f, true);
            StartCoroutine(RecordFrames());
            yield return Wait(1f);
            Debug.Log($"Promo time: delta {Time.deltaTime:F4}, view {ViewClock.Delta:F4}");

            // 1. The kingdom: the wheel back over the capital, the keys moving the kingdom's land
            // to the middle, then Alt held.
            Clip("01-kingdom");
            yield return Wait(1.2f);
            for (int notch = 0; notch < 4; notch++)
            {
                mapCamera.Wheel(-1f, ScreenOf(capital.Cell));
                yield return Wait(0.3f);
            }
            yield return Wait(0.5f);
            yield return KeysTo(LandMiddle(me), 3f);
            yield return Wait(0.8f);
            hud.BadgesShown = true;
            yield return Wait(4f);
            hud.BadgesShown = false;
            yield return Wait(1f);
            Cut();

            // 2. A hero's way: F, the wheel nearer while the hero and the group both stay in view,
            // a right click on the group, a second one to walk; the fight's question.
            if (group >= 0)
            {
                Clip("02-way");
                yield return Wait(0.8f);
                NextHero();
                yield return Wait(1.1f);
                for (int notch = 0; notch < 3 && NearerKeeps(walker.Cell, group); notch++)
                {
                    mapCamera.Wheel(1f, (ScreenOf(walker.Cell) + ScreenOf(group)) * 0.5f);
                    yield return Wait(0.25f);
                }
                yield return Wait(0.6f);
                Order(group);
                yield return Wait(1.2f);
                Order(group);
                while (!hud.DialogOpen)
                    yield return null;
                yield return Wait(2.5f);
                Cut();

                // 3. The battle from «Fight!», each of the players' stacks moving after a short
                // look at its reach, to the result's window and back on the map.
                Clip("03-battle");
                hud.AnswerModal(0);
                yield return null;
                for (float t = 0f; t < 60f; t += Time.deltaTime)
                {
                    battleView.Hands = Random.Range(0.6f, 1f);
                    if (hud.DialogOpen)
                    {
                        if (battleView.Fight == null || !battleView.Fight.Offered)
                            break;
                        // Monsters that give up are let go.
                        yield return Wait(2f);
                        hud.AnswerModal(0);
                    }
                    yield return null;
                }
                battleView.Hands = 0f;
                yield return Wait(3f);
                hud.AnswerModal(0);
                yield return null;
                while (busy)
                {
                    if (hud.DialogOpen)
                    {
                        yield return Wait(1.5f);
                        hud.AnswerModal(0);
                    }
                    yield return null;
                }
                yield return Wait(1.5f);
                Cut();
            }

            // 4. Settlers found a town: F to the hero with the cart, «Found a town», a name typed.
            if (founder != null && founder.Settlers)
            {
                Clip("04-found");
                yield return Wait(0.5f);
                NextHero();
                if (hero != founder)
                {
                    yield return Wait(0.4f);
                    NextHero();
                }
                yield return Wait(1f);
                FoundPicked();
                while (!hud.DialogOpen)
                    yield return null;
                yield return Wait(0.5f);
                TextField field = hud.DialogField;
                if (field != null)
                {
                    string name = TownName();
                    field.value = "";
                    foreach (char letter in name)
                    {
                        yield return Wait(Random.Range(0.12f, 0.26f));
                        field.value += letter;
                    }
                }
                yield return Wait(0.4f);
                hud.AnswerModal(0);
                while (!OnScreen)
                    yield return null;
                yield return Wait(3f);
                LeaveScreen();
                yield return Wait(1.5f);
                Cut();
            }

            // 5. The capital: Space, its screen, a building built, troops hired and seen in the army.
            Clip("05-town");
            yield return Wait(0.7f);
            FocusCapital(Mathf.Min(mapCamera.Distance, 26f));
            yield return Wait(0.8f);
            OpenScreenAt(capital.Cell);
            yield return Wait(2.5f);
            if (next is TownBuilding building)
            {
                bool built = Done(state.Build(me, capital.Index, building), capital.Cell, building);
                Debug.Log($"Promo built {building}: {built}");
                yield return Wait(1.8f);
            }
            hud.ChooseTownTab(1);
            yield return Wait(1.2f);
            foreach (Unit unit in Units.Hired)
            {
                int count = state.Affordable(me, capital.Index, unit);
                if (count > 0 && capital.Buildings.Contains(Units.Building(unit)))
                {
                    Recruit(capital.Index, unit, count);
                    Debug.Log($"Promo hired {count} {unit}");
                    break;
                }
            }
            yield return Wait(1.5f);
            hud.ChooseTownTab(2);
            yield return Wait(4.5f);
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
            int site = FreeSite(capital.Cell, me);
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

        int FreeSite(int from, int me) => Nearest(from, c => (c.Owner == me || c.Owner < 0) && c.Biome != Biome.Forest
            && state.HeroAt(c.Index) == null && Settling.CanFoundOn(state.World, me, c.Index) == Refusal.None);

        /// <summary>
        /// The players' heroes for the clips, off camera: a second one hired in the capital (its
        /// tavern built when it has none), standing with settlers on a free site near the capital.
        /// </summary>
        List<Hero> PromoHeroes(City capital)
        {
            int me = state.World.Player;
            List<Hero> mine = state.HeroesOf(me);
            if (mine.Count < 2)
            {
                state.Treasury[me] += new Stock(400, 150, 150, 150);
                if (!capital.Buildings.Contains(TownBuilding.Tavern))
                    Done(state.Build(me, capital.Index, TownBuilding.Tavern), capital.Cell, TownBuilding.Tavern);
                Refusal refusal = state.HireHero(me, capital.Index);
                Debug.Log($"Promo hires a second hero: {refusal}");
                mine = state.HeroesOf(me);
            }
            if (mine.Count > 1)
            {
                Hero second = mine[1];
                int site = FreeSite(capital.Cell, me);
                if (site >= 0)
                {
                    second.Cell = site;
                    second.Settlers = true;
                    second.Moves = HeroRules.MovesPerTurn;
                    Reveal(site);
                    Debug.Log($"Promo settlers on {state.World.Cells[site].Biome} at {site}");
                }
            }
            heroes.Refresh();
            hud.ClearToast();
            return mine;
        }

        /// <summary>The middle of the kingdom's land, where the camera looks to have it on the screen's middle.</summary>
        Vector2 LandMiddle(int me)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (Cell c in state.World.Cells)
            {
                if (c.Owner == me && !c.IsWater)
                {
                    sum += new Vector2(state.World.CenterX(c.Index), state.World.CenterZ(c.Index));
                    count++;
                }
            }
            return count > 0 ? mapCamera.Nearer(sum / count, 1f) : mapCamera.Target;
        }

        /// <summary>
        /// The arrow keys held as a player holds them, each on or off, until the camera looks at
        /// <paramref name="point"/> or <paramref name="most"/> seconds pass (the camera faces north).
        /// </summary>
        IEnumerator KeysTo(Vector2 point, float most)
        {
            for (float t = 0f; t < most; t += Time.deltaTime)
            {
                Vector2 off = point - mapCamera.Target;
                float slack = mapCamera.Distance * 0.06f;
                var keys = new Vector2(Mathf.Abs(off.x) > slack ? Mathf.Sign(off.x) : 0f, Mathf.Abs(off.y) > slack ? Mathf.Sign(off.y) : 0f);
                if (keys == Vector2.zero)
                    yield break;
                mapCamera.Pan(keys, Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>
        /// One more notch of the wheel, towards the middle of the two cells, keeps them both on
        /// the screen above the bottom panel: each lies 1/0.82 farther from the wheel's point.
        /// </summary>
        bool NearerKeeps(int a, int b)
        {
            Vector2 pa = ScreenOf(a), pb = ScreenOf(b), middle = (pa + pb) * 0.5f;
            return Inside(middle + (pa - middle) / 0.82f, 0.15f) && Inside(middle + (pb - middle) / 0.82f, 0.15f);
        }

        /// <summary>A screen point inside the view less <paramref name="margin"/> of it, above the bottom panel.</summary>
        bool Inside(Vector2 p, float margin)
        {
            float bottom = Mathf.Max(hud.DockTop, 0f) + margin * Screen.height;
            return p.x > margin * Screen.width && p.x < (1f - margin) * Screen.width && p.y > bottom && p.y < (1f - margin) * Screen.height;
        }

        Vector2 ScreenOf(int cell)
        {
            float x = state.World.CenterX(cell), z = state.World.CenterZ(cell);
            Vector3 p = Camera.main.WorldToScreenPoint(new Vector3(x, Mathf.Max(view.Field.Height(x, z), 0f), z));
            return new Vector2(p.x, p.y);
        }

        /// <summary>A name for the new town the players type, one no town has yet.</summary>
        string TownName()
        {
            foreach (string name in new[] { "Oakfield", "Willowby", "Brookhill", "Elmstead" })
            {
                bool taken = false;
                foreach (City c in state.World.Cities)
                    taken |= c.Label == name;
                if (!taken)
                    return name;
            }
            return Settling.NewName(state.World);
        }

        /// <summary>
        /// How well a monster group suits the battle's clip, lower first: boars, as the family
        /// asked (2026-11-11), then others that come at the players across the field; -1 for the
        /// bandits, who shoot from afar, and for lairs.
        /// </summary>
        static int KindRank(Cell c) => c.Lair || c.Band ? -1 : c.Monster switch
        {
            Monster.Boar => 0,
            Monster.Wolves => 1,
            Monster.Goblins => 2,
            Monster.Skeletons or Monster.Spiders => 3,
            Monster.Troll => 4,
            _ => -1,
        };

        /// <summary>A small group makes a short fight: up to about 30 counts as small.</summary>
        static int SizeRank(Cell c) => Mathf.Max(0, c.Count - 30) / 15;

        static int Rank(Cell c) => KindRank(c) * 4 + Mathf.Min(SizeRank(c), 3);

        static bool Open(Cell c) => c.Biome == Biome.Meadow || c.Biome == Biome.Plains;

        /// <summary>
        /// A monster group for the hero's way: six to ten steps this turn, over open seen land,
        /// of a kind that comes across the field (<see cref="KindRank"/>). Without one the hero is
        /// set down on open land a few cells from one. The army is made strong enough to win in a
        /// round or two, archers among it.
        /// </summary>
        int PromoGroup(Hero h)
        {
            World world = state.World;
            h.Moves = HeroRules.MovesPerTurn;
            int best = -1;
            float bestScore = float.MaxValue;
            foreach (Cell c in world.Cells)
            {
                if (KindRank(c) < 0 || !state.Seen[c.Index])
                    continue;
                HeroPath way = state.Path(h, c.Index);
                if (way == null || way.Turns[way.Turns.Count - 1] != 0 || way.Cells.Count < 6 || way.Cells.Count > 10)
                    continue;
                float score = WayScore(way, c);
                if (score < bestScore)
                {
                    best = c.Index;
                    bestScore = score;
                }
            }
            // A better kind or a smaller group elsewhere: the hero is set down beside it.
            if (best < 0 || bestScore >= 10f || KindRank(world.Cells[best]) > 0 || SizeRank(world.Cells[best]) > 0)
            {
                int near = SetDownNear(h);
                if (near >= 0 && (best < 0 || bestScore >= 10f || Rank(world.Cells[near]) < Rank(world.Cells[best])))
                    best = near;
            }
            if (best < 0)
            {
                Debug.Log("Promo: no monster group for the hero's way");
                return -1;
            }
            // The family asked for boars (2026-11-11). Boars live by the kingdoms' borders, and the
            // bot's hero has beaten them all by turn 20, so the chosen group is made a small boar
            // group, as the save of another game could hold it.
            Cell chosen = world.Cells[best];
            if (chosen.Monster != Monster.Boar || chosen.Count > 30)
            {
                Debug.Log($"Promo made {chosen.Monster} x{chosen.Count} a boar group");
                chosen.Monster = Monster.Boar;
                chosen.Count = Mathf.Min(chosen.Count, 24);
                chosen.FirstCount = chosen.Count;
                RedrawMap(new List<int> { best });
            }
            if (Armies.Count(h.Army, Unit.Archers) == 0)
                Armies.Add(h.Army, Unit.Archers, 8);
            for (int k = 0; k < 8 && state.OddsOf(h, best, 60).Win < 0.995f; k++)
            {
                Armies.Add(h.Army, Unit.Spearmen, 10);
                Armies.Add(h.Army, Unit.Riders, 6);
                Armies.Add(h.Army, Unit.Archers, 4);
            }
            Debug.Log($"Promo group {world.Cells[best].Monster} x{world.Cells[best].Count} on {world.Cells[best].Biome}, " +
                $"win {state.OddsOf(h, best, 60).Win:F2}, way {state.Path(h, best)?.Cells.Count}");
            return Reveal(best);
        }

        /// <summary>Lower for a better way: about eight steps, no wood, no fog, a group that comes across the field, on open ground.</summary>
        float WayScore(HeroPath way, Cell group)
        {
            float score = Mathf.Abs(way.Cells.Count - 8) * 0.5f + Rank(group) * 3f;
            foreach (int cell in way.Cells)
            {
                Cell c = state.World.Cells[cell];
                if (c.Biome == Biome.Forest)
                    score += 2f;
                if (!state.Seen[cell])
                    score += 3f;
            }
            if (!Open(group))
                score += 10f;
            return score;
        }

        /// <summary>
        /// The hero set down on open land five to seven cells from a group on open land, the
        /// nearest to the capital of the best kinds, when the way there is one turn's; the land
        /// round both is seen.
        /// </summary>
        int SetDownNear(Hero h)
        {
            World world = state.World;
            City capital = world.Cities[world.Countries[world.Player].Capital];
            Hex home = world.Cells[capital.Cell].Hex;
            var groups = new List<Cell>();
            foreach (Cell c in world.Cells)
            {
                if (KindRank(c) >= 0 && Open(c))
                    groups.Add(c);
            }
            groups.Sort((a, b) => (Rank(a) * 1000 + Hex.Distance(a.Hex, home)).CompareTo(Rank(b) * 1000 + Hex.Distance(b.Hex, home)));
            int was = h.Cell;
            for (int g = 0; g < groups.Count && g < 40; g++)
            {
                Cell group = groups[g];
                foreach (int wanted in new[] { 6, 5, 4 })
                {
                    foreach (Cell c in world.Cells)
                    {
                        if (Hex.Distance(c.Hex, group.Hex) != wanted || !Open(c) || c.Monster != Monster.None || c.City >= 0
                            || c.District >= 0 || state.HeroAt(c.Index) != null)
                            continue;
                        // A way leads only over seen land: the land round both is seen for
                        // the try, and the fog comes back when the way is not one turn's.
                        h.Cell = c.Index;
                        bool[] seen = (bool[])state.Seen.Clone();
                        state.Reveal(c.Index, 3);
                        state.Reveal(group.Index, 2);
                        HeroPath way = state.Path(h, group.Index);
                        if (way != null && way.Turns[way.Turns.Count - 1] == 0 && way.Cells.Count >= 4)
                        {
                            heroes.Refresh();
                            view.UpdateFog();
                            Debug.Log($"Promo set the hero down {wanted} cells from the group, way {way.Cells.Count}");
                            return group.Index;
                        }
                        System.Array.Copy(seen, state.Seen, seen.Length);
                    }
                }
            }
            h.Cell = was;
            return -1;
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
            // A capital that holds every building it can gets its walls, then its castle.
            foreach (TownBuilding building in Economy.TownBuildings)
            {
                if (Economy.TakesSlot(building) && !capital.Buildings.Contains(building))
                    return Afford(building);
            }
            foreach (TownBuilding building in Economy.Fortifications)
            {
                if (!capital.Buildings.Contains(building))
                    return Afford(building);
            }
            return null;

            TownBuilding Afford(TownBuilding building)
            {
                Stock missing = state.Treasury[me].Missing(Economy.Cost(building));
                state.Treasury[me] += missing;
                return building;
            }
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
