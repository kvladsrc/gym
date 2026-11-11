using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>
    /// The rules of new towns: where one may stand, what the settlers cost and the new town's
    /// name. A town gathers a settlers' cart, a hero leads it, and on wild or own land at least
    /// <see cref="Spacing"/> steps from every town it becomes a village that takes the cells around
    /// it (see <see cref="GameState.Found"/>).
    /// </summary>
    public static class Settling
    {
        /// <summary>The fewest steps between two towns.</summary>
        public const int Spacing = 3;

        /// <summary>Houses of a new town: a village.</summary>
        public const int Houses = 2;

        /// <summary>The longest name the players may give a town.</summary>
        public const int NameLength = 22;

        /// <summary>The cart's price: 5 gold and 8 food for each town of the kingdom, so every new town makes the next dearer.</summary>
        public static Stock Cost(World world, int country)
        {
            int towns = world.Countries[country].Cities.Count;
            return new Stock(5 * towns, 8 * towns, 0, 0);
        }

        /// <summary>
        /// Whether a new town of the kingdom may stand on the cell: land that is not mountains, wild
        /// or its own, with no resource, place or monster group on it, far enough from every town.
        /// </summary>
        public static Refusal CanFoundOn(World world, int country, int cell)
        {
            Cell c = world.Cells[cell];
            if (c.IsWater || c.Biome == Biome.Mountains)
                return Refusal.BadGround;
            if (c.Owner >= 0 && c.Owner != country)
                return Refusal.NotYours;
            if (c.Resource != Resource.None || c.Place != Place.None || c.Monster != Monster.None)
                return Refusal.Taken;
            if (NearestTown(world, cell) < Spacing)
                return Refusal.TooClose;
            return Refusal.None;
        }

        /// <summary>Steps from the cell to the nearest town (a large number without towns).</summary>
        public static int NearestTown(World world, int cell)
        {
            int nearest = int.MaxValue;
            Hex at = world.Cells[cell].Hex;
            foreach (City city in world.Cities)
                nearest = Math.Min(nearest, Hex.Distance(at, world.Cells[city.Cell].Hex));
            return nearest;
        }

        /// <summary>A name for the next town: one of <see cref="Names.Cities"/> no town has, in an order of the map's own.</summary>
        public static string NewName(World world)
        {
            var used = new HashSet<string>();
            foreach (City city in world.Cities)
                used.Add(city.Name);
            int count = Names.Cities.Length;
            int start = ((world.Seed * 7 + world.Cities.Count * 13) & int.MaxValue) % count;
            for (int k = 0; k < count; k++)
            {
                string name = Names.Cities[(start + k) % count];
                if (!used.Contains(name))
                    return name;
            }
            return Names.Numbered(world.Cities.Count + 1);
        }

        /// <summary>The players' name for a town, trimmed and cut short; the suggestion when it is empty.</summary>
        public static string CleanName(string name, string suggestion)
        {
            name = (name ?? "").Trim();
            if (name.Length > NameLength)
                name = name.Substring(0, NameLength).TrimEnd();
            return name.Length == 0 ? suggestion : name;
        }
    }

    /// <summary>Settlers' carts and the towns they found.</summary>
    public sealed partial class GameState
    {
        /// <summary>Towns the players have founded (for the tasks).</summary>
        public int TownsFounded;

        /// <summary>Whether the town can gather a settlers' cart now.</summary>
        public Refusal CanGatherSettlers(int country, int city)
        {
            City town = World.Cities[city];
            if (town.Country != country)
                return Refusal.NotYours;
            if (town.Settlers)
                return Refusal.SettlersWaiting;
            return Treasury[country].Covers(Settling.Cost(World, country)) ? Refusal.None : Refusal.NotEnough;
        }

        /// <summary>
        /// The town gathers a settlers' cart and the kingdom pays for it: it joins the kingdom's hero
        /// in the town when he leads none, else it waits there for the next hero who comes in.
        /// </summary>
        public Refusal GatherSettlers(int country, int city)
        {
            Refusal refusal = CanGatherSettlers(country, city);
            if (refusal != Refusal.None)
                return refusal;
            City town = World.Cities[city];
            Treasury[country] -= Settling.Cost(World, country);
            town.Settlers = true;
            Hero hero = HeroAt(town.Cell);
            if (hero != null)
                TakeSettlers(hero);
            return Refusal.None;
        }

        /// <summary>The hero takes the cart waiting in the kingdom's town he stands in, if he leads none.</summary>
        bool TakeSettlers(Hero hero)
        {
            City town = TownOf(hero);
            if (town == null || !town.Settlers || hero.Settlers)
                return false;
            town.Settlers = false;
            hero.Settlers = true;
            return true;
        }

        /// <summary>Whether the hero's settlers can found a town where he stands.</summary>
        public Refusal CanFound(Hero hero) =>
            hero.Settlers ? Settling.CanFoundOn(World, hero.Country, hero.Cell) : Refusal.NoSettlers;

        /// <summary>
        /// The hero's settlers found a village on his cell under the players' name (the suggestion
        /// when they give none); it takes its cell and the wild cells around it, the open sea and
        /// monster groups' cells aside.
        /// </summary>
        public Refusal Found(Hero hero, string name)
        {
            Refusal refusal = CanFound(hero);
            if (refusal != Refusal.None)
                return refusal;
            World.AddCity(hero.Country, hero.Cell, false, Settling.Houses, Settling.CleanName(name, Settling.NewName(World)));
            hero.Settlers = false;
            World.Cells[hero.Cell].Owner = hero.Country;
            for (int d = 0; d < 6; d++)
            {
                int j = World.Neighbor(hero.Cell, d);
                if (j < 0)
                    continue;
                Cell c = World.Cells[j];
                if (c.Owner < 0 && c.Biome != Biome.Ocean && c.Monster == Monster.None)
                    c.Owner = hero.Country;
            }
            if (hero.Country == World.Player)
            {
                TownsFounded++;
                Reveal(hero.Cell, 2);
            }
            return Refusal.None;
        }
    }
}
