using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>
    /// What the guiding lines point at (the interface plan's 3.3, 2026-11-11): the conditions on
    /// the game's state, here so the tests can check them without the interface. The Unity side
    /// (Scripts/RealmsGuide.cs) picks the step, draws the rings and outlines the button. Six tasks
    /// have steps; after them the players know the gestures.
    /// </summary>
    public static class Guide
    {
        /// <summary>The tasks whose gestures the lines teach.</summary>
        public static readonly Quest[] Taught = { Quest.Farm, Quest.LumberCamp, Quest.Fight, Quest.Chest, Quest.Stake, Quest.Barracks };

        /// <summary>The field building a task asks for, or None.</summary>
        public static FieldBuilding FieldFor(Quest quest)
        {
            switch (quest)
            {
                case Quest.Farm: return FieldBuilding.Farm;
                case Quest.LumberCamp: return FieldBuilding.LumberCamp;
                case Quest.Quarry: return FieldBuilding.Quarry;
                case Quest.GoldMine: return FieldBuilding.GoldMine;
                default: return FieldBuilding.None;
            }
        }

        /// <summary>The town building a task asks for, or null.</summary>
        public static TownBuilding? TownFor(Quest quest)
        {
            switch (quest)
            {
                case Quest.Granary: return TownBuilding.Granary;
                case Quest.Market: return TownBuilding.Market;
                case Quest.Tavern: return TownBuilding.Tavern;
                case Quest.Barracks: return TownBuilding.Barracks;
                case Quest.ArcheryRange: return TownBuilding.ArcheryRange;
                case Quest.Stables: return TownBuilding.Stables;
                case Quest.Walls: return TownBuilding.Walls;
                default: return null;
            }
        }

        /// <summary>
        /// The kingdom's cells that can take the field building: its resource, nothing built yet,
        /// no group on it; nearest the capital first, at most <paramref name="count"/>. Whether the
        /// treasury pays is left to the cell's card, which says so.
        /// </summary>
        public static List<int> BuildSpots(GameState state, int country, FieldBuilding building, int count = 3)
        {
            World world = state.World;
            var spots = new List<int>();
            foreach (Cell c in world.Cells)
            {
                if (c.Owner == country && c.City < 0 && c.Building == FieldBuilding.None && c.Monster == Monster.None
                    && c.Resource != Resource.None && Economy.BuildingFor(c.Resource) == building)
                    spots.Add(c.Index);
            }
            Hex home = Home(world, country);
            spots.Sort((a, b) =>
            {
                int da = Hex.Distance(home, world.Cells[a].Hex), db = Hex.Distance(home, world.Cells[b].Hex);
                return da != db ? da.CompareTo(db) : a.CompareTo(b);
            });
            if (spots.Count > count)
                spots.RemoveRange(count, spots.Count - count);
            return spots;
        }

        /// <summary>The kingdom's capital's hexagon (its first town's when it has no capital).</summary>
        static Hex Home(World world, int country)
        {
            List<int> towns = world.Countries[country].Cities;
            foreach (int index in towns)
            {
                if (world.Cities[index].Capital)
                    return world.Cells[world.Cities[index].Cell].Hex;
            }
            return towns.Count > 0 ? world.Cells[world.Cities[towns[0]].Cell].Hex : default;
        }

        /// <summary>
        /// The nearest group the players have seen that the hero surely beats, as the measurer's bot
        /// judges it (<see cref="Rival.Stronger"/>, <see cref="Rival.SureWin"/>), and can reach; -1
        /// when there is none. Odds are worked out for the nearest few only, as they cost.
        /// </summary>
        public static int EasyGroup(GameState state, Hero hero, int tries = 4)
        {
            World world = state.World;
            float army = Armies.Worth(hero.Army);
            if (army <= 0)
                return -1;
            Hex at = world.Cells[hero.Cell].Hex;
            var groups = new List<int>();
            foreach (Cell c in world.Cells)
            {
                if (c.Monster != Monster.None && state.Seen[c.Index]
                    && army >= Rival.Stronger * c.Count * Units.Worth(Units.Of(c.Monster)))
                    groups.Add(c.Index);
            }
            groups.Sort((a, b) =>
            {
                int da = Hex.Distance(at, world.Cells[a].Hex), db = Hex.Distance(at, world.Cells[b].Hex);
                return da != db ? da.CompareTo(db) : a.CompareTo(b);
            });
            foreach (int cell in groups)
            {
                if (tries-- <= 0)
                    break;
                if (state.OddsOf(hero, cell, Rival.OddsRuns).Win >= Rival.SureWin && state.Path(hero, cell) != null)
                    return cell;
            }
            return -1;
        }

        /// <summary>The chests the players have seen, nearest the hexagon first.</summary>
        public static List<int> SeenChests(GameState state, Hex from)
        {
            World world = state.World;
            var chests = new List<int>();
            foreach (Cell c in world.Cells)
            {
                if (c.Place == Place.Chest && state.Seen[c.Index])
                    chests.Add(c.Index);
            }
            chests.Sort((a, b) =>
            {
                int da = Hex.Distance(from, world.Cells[a].Hex), db = Hex.Distance(from, world.Cells[b].Hex);
                return da != db ? da.CompareTo(db) : a.CompareTo(b);
            });
            return chests;
        }

        /// <summary>Where the players' stake serves best: the nearest wild resource their towns reach that may take one; -1 when none.</summary>
        public static int StakeSpot(GameState state, int country)
        {
            foreach (int cell in Influence.StakeChoices(state.World, country))
            {
                if (state.Seen[cell] && Influence.CanStake(state.World, country, cell))
                    return cell;
            }
            return -1;
        }

        /// <summary>A town of the kingdom where the building could go now or once paid for (not built, the level and a free place there); -1 when none.</summary>
        public static int TownToBuild(GameState state, int country, TownBuilding building)
        {
            World world = state.World;
            var rich = new Stock(int.MaxValue / 4, int.MaxValue / 4, int.MaxValue / 4, int.MaxValue / 4);
            int found = -1;
            foreach (int index in world.Countries[country].Cities)
            {
                City city = world.Cities[index];
                if (Economy.CanBuild(city, rich, building) != Refusal.None)
                    continue;
                if (city.Capital)
                    return index;
                if (found < 0)
                    found = index;
            }
            return found;
        }

        /// <summary>Whether one of the kingdom's heroes has the moves for a step somewhere.</summary>
        public static bool HeroCanStep(GameState state, int country)
        {
            World world = state.World;
            foreach (Hero hero in state.HeroesOf(country))
            {
                for (int dir = 0; dir < 6; dir++)
                {
                    int to = world.Neighbor(hero.Cell, dir);
                    if (to >= 0 && HeroRules.StepCost(world, hero.Cell, dir) <= hero.Moves && state.CanEnter(hero, to, true))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Whether something the players can do this turn moves the task on.</summary>
        public static bool CanAdvance(GameState state, Quest quest)
        {
            World world = state.World;
            int player = world.Player;
            Stock treasury = state.Treasury[player];
            FieldBuilding field = FieldFor(quest);
            if (field != FieldBuilding.None)
            {
                foreach (int cell in BuildSpots(state, player, field, int.MaxValue))
                {
                    if (Economy.CanBuild(world, treasury, player, cell) == Refusal.None)
                        return true;
                }
                return false;
            }
            TownBuilding? town = TownFor(quest);
            if (town != null)
            {
                foreach (int index in world.Countries[player].Cities)
                {
                    if (Economy.CanBuild(world.Cities[index], treasury, town.Value) == Refusal.None)
                        return true;
                }
                return false;
            }
            switch (quest)
            {
                case Quest.Fight:
                case Quest.Lair:
                case Quest.Chest:
                case Quest.HeroLevel:
                    return HeroCanStep(state, player);
                case Quest.Stake:
                    return world.Countries[player].Stake < 0 && StakeSpot(state, player) >= 0;
                case Quest.Settle:
                    foreach (Hero hero in state.HeroesOf(player))
                    {
                        if (hero.Settlers && hero.Moves > 0)
                            return true;
                    }
                    foreach (int index in world.Countries[player].Cities)
                    {
                        if (state.CanGatherSettlers(player, index) == Refusal.None)
                            return true;
                    }
                    return false;
                default:
                    // The towns grow with the turns.
                    return false;
            }
        }

        /// <summary>None of the tasks on offer can be moved on this turn: the end-turn button calls.</summary>
        public static bool NothingToDo(GameState state)
        {
            foreach (Quest quest in state.Offered())
            {
                if (CanAdvance(state, quest))
                    return false;
            }
            return true;
        }
    }
}
