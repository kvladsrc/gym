using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The captures for reviewing the look without playing (<c>-realmsCapture DIR</c>): the map in
    /// its fog, the kingdom, a town, the hero's way to a monster group, the fight's dialog, the
    /// battlefield, a monster stack's card and the result, a group in the mountains, then a few
    /// turns played by a simple bot, a fishing boat close, the map after them, the exchange of
    /// units with a town, the town's and the hero's screens, the map turned, settlers founding a
    /// town, the capital growing onto quarters, the flag editor with the new flag on the map, the
    /// help, and the menu (in the battle too) with its question before quitting.
    /// </summary>
    public sealed partial class RealmsGame
    {
        IEnumerator Capture()
        {
            capturing = true;
            mapCamera.Scripted = true;
            Directory.CreateDirectory(captureDir);
            World world = state.World;
            yield return null;

            // The whole seen land, pulled back as far as the camera goes over it (a little south
            // of its middle, so the land sits between the top bar and the bottom panel).
            mapCamera.Focus(mapCamera.SeenMiddle - new Vector2(0, 2f), 85f, true);
            yield return Shot("01-overview");

            City capital = world.Cities[world.Countries[world.Player].Capital];
            var capitalAt = new Vector2(world.CenterX(capital.Cell), world.CenterZ(capital.Cell));
            mapCamera.Focus(capitalAt - new Vector2(0, 1f), 26f, true);
            yield return Shot("02-kingdom");
            // The same view a moment later: the wind's waves have run on over the grass.
            yield return new WaitForSeconds(0.6f);
            yield return Shot("02b-wind");
            // The badges of resources, places and monsters, which show while Alt is held.
            hud.BadgesShown = true;
            yield return Shot("02c-badges");
            hud.BadgesShown = false;
            // The crown's feats over the kingdoms, as a new game has them.
            hud.ToggleKingdoms();
            yield return Shot("02d-crown");
            hud.CloseLists();
            yield return GuideShots(capitalAt);

            ChooseHero(null, false);
            Select(capital.Cell);
            mapCamera.Focus(capitalAt - new Vector2(0, 0.6f), 9f, true);
            yield return Shot("03-capital");
            // The chosen capital's reach: a dot on each wild cell it can grow into.
            mapCamera.Focus(capitalAt - new Vector2(0, 1f), 20f, true);
            yield return Shot("03b-capital-reach");
            Select(-1);

            // The stake (the territory plan's П3): the card of a wild resource a few cells out,
            // then the stake on it with the dots of the way the nearest town takes to it.
            int stakeCell = -1, longest = 0;
            var way = new List<int>();
            foreach (Cell c in world.Cells)
            {
                if (c.Owner >= 0 || c.Resource == Resource.None || !state.Seen[c.Index] || !Influence.CanStake(world, world.Player, c.Index))
                    continue;
                Influence.StakeWay(world, world.Player, c.Index, way);
                if (way.Count > longest && way.Count <= 4)
                {
                    stakeCell = c.Index;
                    longest = way.Count;
                }
            }
            if (stakeCell >= 0)
            {
                // Framed on the way, not on the capital: the town that takes the way may be another one,
                // and the bottom panel covers the south of the view (shots47 had the stake under it).
                int walker = Influence.StakeWay(world, world.Player, stakeCell, way);
                int from = world.Cities[walker].Cell;
                var middle = new Vector2(world.CenterX(stakeCell) + world.CenterX(from), world.CenterZ(stakeCell) + world.CenterZ(from)) * 0.5f;
                mapCamera.Focus(middle - new Vector2(0, 0.6f), 13f, true);
                Select(stakeCell);
                yield return Shot("03c-stake-card");
                PlaceStake(stakeCell);
                Select(stakeCell);
                yield return Shot("03d-stake");
                Select(-1);
            }

            // The hero's way to the nearest monster group (shown to the camera if the fog hides it).
            Hero first = state.HeroesOf(world.Player)[0];
            int group = Nearest(first.Cell, c => c.Monster != Monster.None && !c.Lair);
            if (group >= 0)
            {
                if (!state.Seen[group])
                {
                    state.Reveal(group, 1);
                    view.UpdateFog();
                    hud.RebuildLabels();
                    hud.WorldChanged();
                }
                ChooseHero(first, false);
                Vector2 mid = new Vector2((world.CenterX(first.Cell) + world.CenterX(group)) / 2,
                    (world.CenterZ(first.Cell) + world.CenterZ(group)) / 2 - 1f);
                mapCamera.Focus(mid, 16f, true);
                // The order comes after the camera: it pans the map when the goal would sit under
                // the bottom panel, and the pan eases in.
                yield return null;
                Order(group);
                for (float t = 0; t < 1.2f; t += Time.deltaTime)
                    yield return null;
                yield return Shot("04-hero-way");

                // Walk there when it is this turn's way, else fight from where the hero stands.
                if (path != null && path.Turns[path.Turns.Count - 1] == 0)
                    StartCoroutine(Walk());
                else
                {
                    busy = true;
                    StartCoroutine(FightThenFree(first, group));
                }
                while (!hud.DialogOpen)
                    yield return null;
                yield return Shot("05-fight-dialog");
                hud.AnswerModal(0);
                for (float t = 0; t < 1.5f; t += Time.deltaTime)
                    yield return null;
                yield return Shot("06-battle");
                // A monster stack's card, with the line telling whom its kind goes for; set again
                // each frame, as the pointer's own card would replace it.
                Fighter monster = battleView.Fight?.Fighters.Find(x => x.Side == Side.Monsters && x.Alive);
                if (monster != null)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        hud.BattleInfo(monster, null);
                        yield return null;
                    }
                    hud.BattleInfo(monster, null);
                    ScreenCapture.CaptureScreenshot(Path.Combine(captureDir, "06b-battle-card.png"));
                    yield return null;
                    hud.BattleInfo(null, null);
                }
                // The menu in a battle, which waits while it is open.
                hud.ShowMenu(true);
                yield return Shot("06c-battle-menu");
                hud.ShowMenu(false);
                // The retreat's question, answered with fighting on.
                battleView.AskRetreat();
                while (!hud.DialogOpen)
                    yield return null;
                yield return Shot("06d-retreat-ask");
                hud.AnswerModal(1);
                yield return null;
                battleView.SetAuto(true);
                for (float t = 0; t < 2.5f && !hud.DialogOpen; t += Time.deltaTime)
                    yield return null;
                yield return Shot("07-battle-later");
                while (!hud.DialogOpen)
                    yield return null;
                // The monsters may offer to give up before the end: let them go, then the result.
                if (battleView.Fight != null && battleView.Fight.Offered)
                {
                    yield return Shot("07b-surrender");
                    hud.AnswerModal(0);
                    while (!hud.DialogOpen)
                        yield return null;
                }
                yield return Shot("08-result");
                while (busy)
                {
                    if (hud.DialogOpen)
                        hud.AnswerModal(0);
                    yield return null;
                }
                mapCamera.Focus(new Vector2(world.CenterX(first.Cell), world.CenterZ(first.Cell) - 1f), 18f, true);
                yield return Shot("09-after-fight");
            }

            // A group in the mountains, close: its figures must stand clear of the peaks.
            int high = Nearest(capital.Cell, c => c.Monster != Monster.None && c.Biome == Biome.Mountains);
            if (high >= 0)
            {
                if (!state.Seen[high])
                {
                    state.Reveal(high, 1);
                    view.UpdateFog();
                    hud.RebuildLabels();
                    hud.WorldChanged();
                }
                ChooseHero(null, false);
                Select(-1);
                mapCamera.Focus(new Vector2(world.CenterX(high), world.CenterZ(high) - 0.4f), 7f, true);
                yield return Shot("09b-mountain-group");
            }

            int coast = Nearest(capital.Cell, c => c.City >= 0 && !world.Cities[c.City].Capital && HasWaterNeighbor(c.Index) && state.Seen[c.Index]);
            if (coast >= 0)
            {
                ChooseHero(null, false);
                Select(-1);
                mapCamera.Focus(new Vector2(world.CenterX(coast), world.CenterZ(coast) - 0.8f), 15f, true);
                yield return Shot("10-coast");
            }

            // A few turns played by a simple bot: buildings, land, hired units, the hero exploring and fighting.
            TurnReport last = null;
            for (int turn = 0; turn < 14; turn++)
                last = AutoTurn();
            view.UpdateFog();
            heroes.Refresh();
            hud.RebuildLabels();
            hud.WorldChanged();
            int built = Nearest(capital.Cell, c => c.Owner == world.Player && c.Building != FieldBuilding.None && c.IsLand);
            if (built >= 0)
            {
                Select(built);
                mapCamera.Focus(new Vector2(world.CenterX(built), world.CenterZ(built) - 0.5f), 7f, true);
                yield return Shot("11-field-built");
            }
            // A fishing boat close, its sail in the owner's flag; nearer than the map lets the
            // players come.
            int fish = Nearest(capital.Cell, c => c.Resource == Resource.Fish && c.Owner >= 0 && state.Seen[c.Index]);
            if (fish >= 0)
            {
                Select(-1);
                float least = mapCamera.minDistance;
                mapCamera.minDistance = 2f;
                mapCamera.Focus(new Vector2(world.CenterX(fish), world.CenterZ(fish) - 0.2f), 2.6f, true);
                yield return Shot("11b-boat");
                mapCamera.minDistance = least;
            }
            // A farm of any kingdom close: its windmill stands clear of the farmhouse (the
            // players saw it half inside, 2026-11-11).
            int farm = Nearest(capital.Cell, c => c.Building == FieldBuilding.Farm && state.Seen[c.Index]);
            if (farm >= 0)
            {
                Select(-1);
                float least = mapCamera.minDistance;
                mapCamera.minDistance = 2f;
                mapCamera.Focus(new Vector2(world.CenterX(farm), world.CenterZ(farm) - 0.2f), 3.2f, true);
                yield return Shot("11c-farm");
                mapCamera.minDistance = least;
            }
            Select(-1);
            mapCamera.Focus(mapCamera.SeenMiddle - new Vector2(0, 2f), 85f, true);
            yield return Shot("12-overview-later");
            mapCamera.Focus(capitalAt - new Vector2(0, 1f), 22f, true);
            hud.ShowReport(last);
            // The cells the border took on the last turn flash as they do after «Следующий ход».
            var taken = new List<int>();
            foreach (TurnEvent e in last.Events)
            {
                if (e.Kind == TurnEventKind.Border)
                    taken.Add(e.Cell);
            }
            view.FlashTaken(taken);
            yield return new WaitForSeconds(0.25f);
            List<Hero> mine = state.HeroesOf(world.Player);
            ChooseHero(mine.Count > 0 ? mine[0] : null, false);
            yield return Shot("13-kingdom-later");
            hud.ToggleKingdoms();
            yield return Shot("13b-lists");
            hud.CloseLists();
            ChooseHero(null, false);
            Select(capital.Cell);
            mapCamera.Focus(capitalAt - new Vector2(0, 0.5f), 6.5f, true);
            yield return Shot("14-capital-later");
            Select(-1);

            // A band of a lair going for one of the players' towns (the fun plan's Ф3): the line over
            // the map and the band's card. The bot's turns may have sent one already; else the lair
            // nearest the players' towns sends one now.
            Raid band = null;
            foreach (Raid r in state.Bands)
            {
                if (world.Cities[r.Town].Country == world.Player)
                    band = r;
            }
            if (band == null)
            {
                foreach (int lairCell in LairsByDistance(world.Player))
                {
                    var sent = new TurnReport();
                    Raid r = Raids.Send(state, lairCell, sent);
                    if (r == null)
                        continue;
                    view.Refresh(sent.Changed);
                    if (world.Cities[r.Town].Country == world.Player)
                    {
                        band = r;
                        break;
                    }
                }
            }
            // No lair of this map raids the players' towns (shots48): one is staged by hand.
            if (band == null)
                band = StageBand();
            if (band != null)
            {
                if (!state.Seen[band.Cell])
                {
                    state.Reveal(band.Cell, 1);
                    view.UpdateFog();
                    hud.RebuildLabels();
                }
                hud.WorldChanged();
                hud.Refresh();
                ChooseHero(null, false);
                Select(band.Cell);
                int target = world.Cities[band.Town].Cell;
                var between = new Vector2(world.CenterX(band.Cell) + world.CenterX(target), world.CenterZ(band.Cell) + world.CenterZ(target)) * 0.5f;
                mapCamera.Focus(between - new Vector2(0, 0.6f), 12f, true);
                yield return Shot("14f-raid");
                Select(-1);
            }

            // A neighbour's troop at a lair (the economy plan's step 4): its flag, the line over the
            // map and the lair's card. The bot's turns may have sent one to a lair the players know;
            // else the nearest neighbour sends one now to the lair nearest the players' towns.
            Troop siege = null;
            foreach (Troop t in state.LairTroops)
            {
                if (state.Seen[t.Lair])
                    siege = t;
            }
            if (siege == null)
                siege = StageTroop(band != null ? band.Lair : -1);
            if (siege != null)
            {
                if (!state.Seen[siege.Lair])
                {
                    state.Reveal(siege.Lair, 1);
                    view.UpdateFog();
                }
                view.Refresh(new[] { siege.Lair });
                hud.RebuildLabels();
                hud.WorldChanged();
                hud.Refresh();
                ChooseHero(null, false);
                Select(siege.Lair);
                mapCamera.Focus(new Vector2(world.CenterX(siege.Lair), world.CenterZ(siege.Lair) - 0.6f), 9f, true);
                yield return Shot("14g-troop");
                Select(-1);
            }

            // The exchange with the capital, half the spearmen left there for another hero.
            if (mine.Count > 0 && mine[0].Army.Count > 0)
            {
                Hero h = mine[0];
                List<Core.Stack> garrison = capital.Garrison;
                Armies.Move(h.Army, garrison, h.Army[0].Unit, (h.Army[0].Count + 1) / 2);
                hud.Exchange(h.Label, h.Army, capital.Label, garrison, null);
                yield return Shot("14b-exchange");
                hud.AnswerModal(0);
                Armies.Merge(h.Army, garrison);
            }

            // The capital's and the hero's screens: the camera down beside the panel, the land blurred.
            EnterTown(capital.Index);
            for (float t = 0; t < 2f; t += Time.deltaTime)
                yield return null;
            yield return Shot("14c-town-screen");
            // Its army tab with a hero in the town: the units' names under their pictures and the
            // arrows between them (the names ran into the arrows, 2026-11-11).
            if (mine.Count > 0)
            {
                Hero h = mine[0];
                int was = h.Cell;
                var kept = capital.Garrison.ConvertAll(one => new Core.Stack(one.Unit, one.Count));
                h.Cell = capital.Cell;
                heroes.Refresh();
                Armies.Add(capital.Garrison, Unit.Spearmen, 6);
                Armies.Add(capital.Garrison, Unit.Archers, 4);
                Armies.Add(capital.Garrison, Unit.Riders, 2);
                hud.ChooseTownTab(2);
                for (float t = 0; t < 0.5f; t += Time.deltaTime)
                    yield return null;
                yield return Shot("14c2-town-army");
                capital.Garrison.Clear();
                capital.Garrison.AddRange(kept);
                h.Cell = was;
                heroes.Refresh();
                hud.ChooseTownTab(0);
            }
            if (mine.Count > 0)
            {
                EnterHero(mine[0]);
                for (float t = 0; t < 2f; t += Time.deltaTime)
                    yield return null;
                yield return Shot("14d-hero-screen");
            }
            LeaveScreen();

            // The map turned: the minimap shows the view, the compass points north.
            mapCamera.Face(-50f, true);
            mapCamera.Focus(mapCamera.Nearer(capitalAt, 1f), 22f, true);
            yield return Shot("14e-turned");
            mapCamera.Face(0f, true);

            // Settlers: a cart gathered in the capital waits there, a hero leads one to free land
            // and founds a town under the offered name; its screen, then the map around it.
            if (mine.Count > 0)
            {
                Hero h = mine[0];
                state.Treasury[world.Player] += Settling.Cost(world, world.Player);
                GatherSettlers(capital.Index);
                EnterTown(capital.Index);
                hud.ChooseTownTab(1);
                for (float t = 0; t < 1f; t += Time.deltaTime)
                    yield return null;
                hud.ScrollTownToEnd();
                for (float t = 0; t < 1f; t += Time.deltaTime)
                    yield return null;
                yield return Shot("16-town-settlers");
                LeaveScreen();
                int site = Nearest(capital.Cell, c => state.Seen[c.Index] && state.HeroAt(c.Index) == null
                    && Settling.CanFoundOn(world, world.Player, c.Index) == Refusal.None && Settling.NearestTown(world, c.Index) == Settling.Spacing);
                if (site >= 0)
                {
                    capital.Settlers = false;
                    h.Settlers = true;
                    h.Cell = site;
                    heroes.Refresh();
                    ChooseHero(h, false);
                    mapCamera.Focus(new Vector2(world.CenterX(site), world.CenterZ(site) - 0.5f), 7f, true);
                    yield return Shot("17-settlers-hero");
                    StartCoroutine(Found(h));
                    while (!hud.DialogOpen)
                        yield return null;
                    yield return Shot("18-found-dialog");
                    hud.AnswerModal(0);
                    for (float t = 0; t < 2.5f; t += Time.deltaTime)
                        yield return null;
                    LogGroundCheck("founding a town");
                    yield return Shot("19-new-town-screen");
                    LeaveScreen();
                    ChooseHero(null, false);
                    Select(-1);
                    mapCamera.Focus(new Vector2(world.CenterX(site), world.CenterZ(site) - 0.6f), 9f, true);
                    yield return Shot("20-new-town");
                }
            }

            // A full capital: the card of an empty cell beside it offers a quarter, which the
            // capital gets.
            {
                state.Treasury[world.Player] += new Stock(50, 0, 50, 0);
                capital.Houses = Math.Max(capital.Houses, Math.Min(Towns.MaxHouses, capital.Houses + Towns.Free(capital)));
                // The full capital's own screen offers the quarter too, the cell ringed as while
                // the pointer is over the button.
                List<int> grow = Towns.ExpansionCells(world, capital);
                if (Towns.Free(capital) == 0 && grow.Count > 0)
                {
                    EnterTown(capital.Index);
                    hud.ChooseTownTab(0);
                    view.SetHover(grow[0]);
                    for (float t = 0; t < 2f; t += Time.deltaTime)
                        yield return null;
                    yield return Shot("20a-expand-screen");
                    LeaveScreen();
                }
                int site = ExpandSite(capital);
                if (site >= 0)
                {
                    Select(site);
                    Vector2 at = new Vector2(world.CenterX(site), world.CenterZ(site));
                    mapCamera.Focus((at + capitalAt) / 2 - new Vector2(0, 0.5f), 6.5f, true);
                    yield return Shot("20b-expand-card");
                    ExpandTown(site);
                    for (float t = 0; t < 1.5f; t += Time.deltaTime)
                        yield return null;
                    LogGroundCheck("growing a town");
                    yield return Shot("20c-expanded");
                    Select(-1);
                }
            }

            // Walls and a castle: the capital grown to a royal city inside walls with a castle,
            // quarters round it as it fills them, a second town with walls only; the places and
            // fortifications on the capital's screen.
            {
                state.Treasury[world.Player] += new Stock(80, 0, 80, 80);
                capital.Houses = Math.Max(capital.Houses, 8);
                state.Build(world.Player, capital.Index, TownBuilding.Walls);
                state.Build(world.Player, capital.Index, TownBuilding.Castle);
                while (true)
                {
                    capital.Houses = Math.Min(Towns.MaxHouses, capital.Houses + Towns.Free(capital));
                    int next = capital.Houses < Towns.MaxHouses ? ExpandSite(capital) : -1;
                    if (next < 0)
                        break;
                    state.Treasury[world.Player] += Towns.DistrictCost;
                    state.Expand(world.Player, next);
                }
                City walled = null;
                foreach (int index in world.Countries[world.Player].Cities)
                {
                    City town = world.Cities[index];
                    if (!town.Capital && (walled == null || town.Houses > walled.Houses))
                        walled = town;
                }
                if (walled != null)
                {
                    walled.Houses = Math.Max(walled.Houses, 5);
                    state.Build(world.Player, walled.Index, TownBuilding.Walls);
                }
                RedrawMap();
                hud.RebuildLabels();
                hud.WorldChanged();
                // The capital chosen, so the panel shows it rather than a black strip with one
                // line (review of 2026-11-11).
                Select(capital.Cell);
                mapCamera.Focus(capitalAt - new Vector2(0, 0.5f), 6.5f, true);
                yield return Shot("21-castle");
                mapCamera.Focus(capitalAt - new Vector2(0, 0.35f), 3.6f, true);
                yield return Shot("21b-castle-close");
                // The same view a moment later: the wind has moved the trees, the grass and the smoke.
                yield return new WaitForSeconds(1.5f);
                yield return Shot("21c-wind");
                if (walled != null)
                {
                    Vector2 at = new Vector2(world.CenterX(walled.Cell), world.CenterZ(walled.Cell));
                    mapCamera.Focus(at - new Vector2(0, 0.4f), 5f, true);
                    yield return Shot("22-walls");
                }
                foreach (City town in walled != null ? new[] { capital, walled } : new[] { capital })
                {
                    EnterTown(town.Index);
                    hud.ChooseTownTab(0);
                    for (float t = 0; t < 1f; t += Time.deltaTime)
                        yield return null;
                    if (town == capital)
                        yield return Shot("23a-town-places");
                    hud.ScrollTownToEnd();
                    for (float t = 0; t < 1f; t += Time.deltaTime)
                        yield return null;
                    yield return Shot(town == capital ? "23-town-fortifications" : "23b-walled-town-screen");
                    LeaveScreen();
                }

                // A quarter across a river from its centre: the street to it goes over a bridge and,
                // from a walled centre, through a gate (the players asked for both, 2026-11-11).
                foreach (City town in walled != null ? new[] { walled, capital } : new[] { capital })
                {
                    int over = -1;
                    for (int d = 0; d < 6 && over < 0; d++)
                    {
                        int j = world.Neighbor(town.Cell, d);
                        if (j < 0 || (world.Cells[town.Cell].Rivers >> d & 1) == 0)
                            continue;
                        if (world.Cells[j].District == town.Index)
                            over = j;
                        else if (town.Houses < Towns.MaxHouses)
                        {
                            town.Houses = Math.Min(Towns.MaxHouses, town.Houses + Towns.Free(town));
                            state.Treasury[world.Player] += Towns.DistrictCost;
                            if (state.Expand(world.Player, j) == Refusal.None)
                                over = j;
                        }
                    }
                    if (over < 0)
                        continue;
                    RedrawMap();
                    hud.RebuildLabels();
                    hud.WorldChanged();
                    Select(-1);
                    Vector2 a = new Vector2(world.CenterX(town.Cell), world.CenterZ(town.Cell));
                    Vector2 b = new Vector2(world.CenterX(over), world.CenterZ(over));
                    mapCamera.Focus((a + b) / 2 - new Vector2(0, 0.35f), 3.6f, true);
                    for (float t = 0; t < 1f; t += Time.deltaTime)
                        yield return null;
                    yield return Shot("23c-quarter-bridge");
                    break;
                }
            }

            // The flag editor as a new game opens it, with the generated flag; then the players'
            // own on another kingdom's field, so the two kingdoms swap colours and names.
            Select(-1);
            StartCoroutine(EditFlag(true));
            for (int k = 0; k < 3; k++)
                yield return null;
            yield return Shot("24a-flag-editor");
            flagDraft.Set(new Flag(FlagPattern.Border, Flags.Blue, Flags.Yellow, Emblem.Dragon, Flags.Yellow));
            yield return null;
            yield return Shot("24b-flag-drawn");
            hud.AnswerModal(0);
            yield return null;
            hud.Toast("", 0f);
            Select(capital.Cell);
            mapCamera.Focus(capitalAt - new Vector2(0, 0.4f), 5f, true);
            yield return Shot("24c-flag-capital");
            mine = state.HeroesOf(world.Player);
            if (mine.Count > 0)
            {
                ChooseHero(mine[0], false);
                mapCamera.Focus(new Vector2(world.CenterX(mine[0].Cell), world.CenterZ(mine[0].Cell) - 0.3f), 5f, true);
                yield return Shot("24d-flag-hero");
            }
            mapCamera.Focus(capitalAt - new Vector2(0, 1f), 26f, true);
            hud.ToggleKingdoms();
            yield return null;
            yield return Shot("24e-flag-kingdoms");
            hud.ToggleKingdoms();

            mapCamera.Focus(capitalAt - new Vector2(0, 1f), 30f, true);
            hud.ToggleHelp(true);
            hud.ShowHelpPage(0);
            yield return Shot("15-help");
            hud.ShowHelpPage(1);
            yield return Shot("15h-help-keys");
            // The hints page as after the guide frames: the capture saw them all.
            seen = (Learned)2047;
            hud.SetSeenHints(SeenLines());
            hud.ToggleHelp(true);
            hud.ShowHelpPage(2);
            yield return Shot("15i-help-hints");
            hud.ShowHelpPage(0);
            // The menu beside the map, then its lists of saves (the capture's own, beside its
            // pictures): «Сохранить» after a save into the first slot, «Загрузить» after an autosave.
            hud.ToggleHelp(false);
            hud.ShowMenu(true);
            yield return Shot("15b-menu");
            SaveInto(1);
            ShowSaveList();
            yield return null;
            yield return Shot("15c-save-list");
            hud.CloseSaves();
            AutoSave();
            ShowLoadList();
            yield return null;
            yield return Shot("15f-load-list");
            yield return LoadCheck();
            // The loaded game's pending chest asks at once; the players answer it before any menu.
            if (hud.DialogOpen)
            {
                hud.AnswerModal(0);
                yield return null;
            }
            // The new game's screen beside the capture's own map, not made anew, then the main
            // menu over its picture; the capture plays on.
            hud.ShowNewGame(state.World.Seed, difficulty);
            yield return Shot("15e-new-game");
            hud.ShowTitle(Words.TitleContinueLine(state.World.Countries[state.World.Player].Label, state.Turn));
            yield return Shot("15j-title");
            // «Загрузить» from the main menu: the list in its own panel under the logo.
            ShowLoadList();
            yield return null;
            yield return Shot("15l-title-load");
            hud.CloseSaves();
            hud.ShowMenu(false);
            // The end of the game: the players' crown set with the three gems they are nearest to.
            yield return CrownShot();
            Application.Quit();
        }

        /// <summary>
        /// Opens the save in the first slot as the players would and logs whether the loaded game
        /// is the saved one, byte for byte, and how long its showing took; then the map as loaded.
        /// </summary>
        IEnumerator LoadCheck()
        {
            byte[] before = Save.ToBytes(new SaveGame { State = state });
            var watch = System.Diagnostics.Stopwatch.StartNew();
            LoadFrom(new SaveFile { Path = SlotPath(1), Slot = 1 });
            // The map is drawn under the loading screen in the next frames.
            yield return null;
            yield return null;
            yield return null;
            yield return Shot("15k-loading");
            while (loading)
                yield return null;
            long ms = watch.ElapsedMilliseconds;
            byte[] after = Save.ToBytes(new SaveGame { State = state });
            bool same = before.Length == after.Length;
            for (int i = 0; same && i < before.Length; i++)
                same = before[i] == after[i];
            Debug.Log($"Realms load check: the loaded game is {(same ? "the saved one" : "NOT the saved one")} " +
                $"({before.Length} bytes), shown in {ms} ms");
            yield return Shot("15g-loaded");
        }

        /// <summary>
        /// Logs how far the ground worked out again only round a new town or quarter
        /// (<see cref="MapView.Reshape"/>) is from the whole ground worked out anew: nothing but
        /// zero means the partial redraw left a seam.
        /// </summary>
        void LogGroundCheck(string after)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            float off = view.Field.Difference(new TerrainField(state.World));
            Debug.Log($"Realms ground check after {after}: off a new ground by {off:G4} ({watch.ElapsedMilliseconds} ms)");
        }

        /// <summary>A neighbour of the player's town it may grow onto now, behind the centre first; -1 if none.</summary>
        int ExpandSite(City town)
        {
            World world = state.World;
            foreach (int d in new[] { 1, 2, 0, 3, 5, 4 })
            {
                int j = world.Neighbor(town.Cell, d);
                if (j >= 0 && Towns.CanExpand(world, state.Treasury[world.Player], world.Player, j, out _) == Refusal.None)
                    return j;
            }
            return -1;
        }

        IEnumerator FightThenFree(Hero fighter, int cell)
        {
            yield return Fight(fighter, cell);
            AfterMove(true);
            busy = false;
        }

        /// <summary>
        /// One turn of a simple player, for the captures: grow full towns onto a neighbour, build
        /// what it can in its towns and then on its land, hire what its towns offer, send each hero
        /// on the bot's way (<see cref="Rival.HeroTurn"/>), end the turn. Without <paramref name="show"/>
        /// the map is not drawn again (the promo plays many such turns, then draws it once);
        /// without <paramref name="hire"/> the towns keep their recruits.
        /// </summary>
        TurnReport AutoTurn(bool show = true, bool hire = true)
        {
            World world = state.World;
            int me = world.Player;
            var changed = new List<int>();
            bool expanded = false;
            foreach (int index in world.Countries[me].Cities)
            {
                City town = world.Cities[index];
                if (Towns.Free(town) == 0 && town.Houses < Towns.MaxHouses)
                {
                    int site = ExpandSite(town);
                    expanded |= site >= 0 && state.Expand(me, site) == Refusal.None;
                }
                foreach (TownBuilding building in Economy.TownBuildings)
                {
                    if (state.Build(me, index, building) == Refusal.None)
                        changed.Add(world.Cities[index].Cell);
                }
                foreach (Unit unit in Units.Hired)
                {
                    if (!hire)
                        break;
                    int count = state.Affordable(me, index, unit);
                    if (count > 0)
                        state.Recruit(me, index, unit, count);
                }
            }
            foreach (Cell cell in world.Cells)
            {
                if (state.Build(me, cell.Index) == Refusal.None)
                    changed.Add(cell.Index);
            }
            foreach (Hero h in state.HeroesOf(me))
                Rival.HeroTurn(state, h, changed);
            state.CheckQuests();
            TurnReport report = state.EndTurn();
            changed.AddRange(report.Changed);
            if (!show)
                return report;
            if (expanded)
                RedrawMap();
            else
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                bool laid = view.Refresh(changed);
                if (laid)
                    heroes.Refresh();
                Debug.Log($"Capture turn {state.Turn - 1} shown in {watch.ElapsedMilliseconds} ms, the ground laid again: {laid}");
            }
            return report;
        }

        /// <summary>The lairs, nearest to one of the kingdom's towns first.</summary>
        /// <summary>
        /// A band staged by hand two steps from the players' town nearest to a lair, on the side
        /// toward the lair, for the raid's frame when no lair of the map raids the players' towns.
        /// </summary>
        Raid StageBand()
        {
            World world = state.World;
            List<int> lairs = LairsByDistance(world.Player);
            if (lairs.Count == 0)
                return null;
            Cell lair = world.Cells[lairs[0]];
            int town = -1, townDistance = int.MaxValue;
            foreach (int t in world.Countries[world.Player].Cities)
            {
                int d = Hex.Distance(lair.Hex, world.Cells[world.Cities[t].Cell].Hex);
                if (d < townDistance)
                {
                    town = t;
                    townDistance = d;
                }
            }
            if (town < 0)
                return null;
            Hex at = world.Cells[world.Cities[town].Cell].Hex;
            int cell = -1, best = int.MaxValue;
            foreach (Cell c in world.Cells)
            {
                if (c.IsWater || c.Biome == Biome.Mountains || c.Monster != Monster.None || c.City >= 0 || c.District >= 0
                    || c.Place != Core.Place.None || Hex.Distance(c.Hex, at) != 2)
                    continue;
                int d = Hex.Distance(c.Hex, lair.Hex);
                if (d < best)
                {
                    best = d;
                    cell = c.Index;
                }
            }
            if (cell < 0)
                return null;
            Cell spot = world.Cells[cell];
            spot.Monster = lair.Monster;
            spot.Count = spot.FirstCount = Raids.Size(lair.Count);
            spot.Band = true;
            var raid = new Raid { Lair = lair.Index, Town = town, Cell = cell, Steps = 2 };
            state.Bands.Add(raid);
            view.Refresh(new[] { cell });
            return raid;
        }

        /// <summary>
        /// A troop staged by hand: the neighbour nearest to the lair nearest the players' towns
        /// (not <paramref name="skip"/>, the band's) stands at it with two turns left.
        /// </summary>
        Troop StageTroop(int skip)
        {
            World world = state.World;
            foreach (int lair in LairsByDistance(world.Player))
            {
                if (lair == skip || Troops.At(state, lair) != null)
                    continue;
                int country = -1, best = int.MaxValue;
                for (int k = 0; k < world.Countries.Count; k++)
                {
                    if (k == world.Player || world.Countries[k].Cities.Count == 0 || Troops.Of(state, k) != null)
                        continue;
                    int d = Troops.Distance(world, k, lair);
                    if (d < best)
                    {
                        best = d;
                        country = k;
                    }
                }
                if (country < 0)
                    return null;
                Cell c = world.Cells[lair];
                var troop = new Troop
                {
                    Country = country, Lair = lair, Turns = Troops.StandTurns - 1, Men = Troops.Men(c.Monster, c.Count),
                    Paid = Troops.Price(c.Monster, c.Count),
                };
                state.LairTroops.Add(troop);
                return troop;
            }
            return null;
        }

        List<int> LairsByDistance(int country)
        {
            World world = state.World;
            var lairs = new List<int>();
            var near = new Dictionary<int, int>();
            foreach (Cell c in world.Cells)
            {
                if (!c.Lair || c.Monster == Monster.None)
                    continue;
                int best = int.MaxValue;
                foreach (int t in world.Countries[country].Cities)
                    best = Mathf.Min(best, Hex.Distance(c.Hex, world.Cells[world.Cities[t].Cell].Hex));
                lairs.Add(c.Index);
                near[c.Index] = best;
            }
            lairs.Sort((a, b) => near[a].CompareTo(near[b]));
            return lairs;
        }

        /// <summary>The end of the game, its crown set by hand, with the bar's gems lit behind it.</summary>
        IEnumerator CrownShot()
        {
            int player = state.World.Player;
            var near = new List<Gem>(Crown.Order);
            near.Sort((a, b) =>
            {
                (int ha, int na) = Crown.Progress(state, player, a);
                (int hb, int nb) = Crown.Progress(state, player, b);
                return (hb / (float)nb).CompareTo(ha / (float)na);
            });
            foreach (Gem gem in near)
            {
                if (state.Gems[player].Count < Crown.Needed && !state.HasGem(player, gem))
                    state.Gems[player].Add(new GemWon(gem, Mathf.Max(1, state.Turn - 2 + state.Gems[player].Count)));
            }
            state.Crowned[player] = state.Turn;
            hud.Refresh();
            ShowCrownEnd(true);
            while (!hud.DialogOpen)
                yield return null;
            yield return Shot("15d-crown-end");
            hud.AnswerModal(0);
        }

        /// <summary>
        /// The guiding lines (the interface plan's 3.3) as a newcomer sees them: the rings on the
        /// cells for the first task, the outlined button on a chosen cell, the fight once building
        /// is learned, the end-turn button calling, and a one-time hint. The capture keeps nothing
        /// learned and clears the guide after, so the later frames show the game without it.
        /// </summary>
        IEnumerator GuideShots(Vector2 capitalAt)
        {
            World world = state.World;
            mapCamera.Focus(capitalAt - new Vector2(0, 1f), 20f, true);
            GuideNow();
            yield return Shot("02e-guide-rings");
            List<int> spots = Guide.BuildSpots(state, world.Player, Guide.FieldFor(Quest.Farm), 1);
            if (spots.Count > 0)
            {
                Select(spots[0]);
                GuideNow();
                yield return Shot("02f-guide-button");
                Select(-1);
            }
            // Building learned: the fight is next, with the hero's group of a sure win ringed.
            learned |= Learned.BuildOnce | Learned.Build;
            if (hero != null)
                mapCamera.Focus(new Vector2(world.CenterX(hero.Cell), world.CenterZ(hero.Cell)) - new Vector2(0, 1f), 22f, true);
            GuideNow();
            yield return Shot("02g-guide-fight");
            // Everything learned and a newcomer waiting: the end-turn button calls.
            learned |= Learned.Order | Learned.Chest | Learned.Stake | Learned.TownBuild;
            lastDeed = Time.unscaledTime - CallIdle - 1f;
            GuideNow();
            yield return Shot("02h-guide-call");
            ShowHint(Words.HintCamera, Learned.HintCamera);
            lastDeed = Time.unscaledTime;
            GuideNow();
            yield return Shot("02i-guide-hint");
            learned = Learned.None;
            hint = null;
            hud.SetGuide(null, null, null, false);
            view.SetGuide(null);
        }

        IEnumerator Shot(string name)
        {
            for (int i = 0; i < 10; i++)
                yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(captureDir, name + ".png"));
            // The camera's distance and height, for judging zoom limits and fades from the log.
            Debug.Log($"Realms capture {name}: camera distance {mapCamera.Distance:F1}, height {mapCamera.transform.position.y:F1}");
            for (int i = 0; i < 3; i++)
                yield return null;
        }

        int Nearest(int from, Func<Cell, bool> match)
        {
            World world = state.World;
            Hex origin = world.Cells[from].Hex;
            int best = -1, bestDistance = int.MaxValue;
            foreach (Cell cell in world.Cells)
            {
                if (!match(cell))
                    continue;
                int d = Hex.Distance(origin, cell.Hex);
                if (d < bestDistance)
                {
                    best = cell.Index;
                    bestDistance = d;
                }
            }
            return best;
        }

        bool HasWaterNeighbor(int index)
        {
            for (int d = 0; d < 6; d++)
            {
                int n = state.World.Neighbor(index, d);
                if (n >= 0 && state.World.Cells[n].IsWater)
                    return true;
            }
            return false;
        }
    }
}
