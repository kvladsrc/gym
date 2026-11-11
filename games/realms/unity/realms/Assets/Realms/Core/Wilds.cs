using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>Monster groups and places on the wild land.</summary>
    public static class Wilds
    {
        /// <summary>A watchtower opens the fog this far around it.</summary>
        public const int TowerSight = 7;

        /// <summary>The groups grow every this many turns.</summary>
        public const int GrowthTurns = 4;

        /// <summary>The groups nearest the players' kingdom, placed first, small and never lairs.</summary>
        public const int FirstGroups = 3;

        /// <summary>The most trolls in one group.</summary>
        public const int MaxTrolls = 8;

        /// <summary>The worth of the army a kingdom's first hero starts with (see <see cref="Units.Worth"/>).</summary>
        public static float StartWorth =>
            HeroRules.StartSpearmen * Units.Worth(Unit.Spearmen) + HeroRules.StartArchers * Units.Worth(Unit.Archers);

        /// <summary>
        /// A group's worth as a share of the first hero's army, by how deep in the wild it stands
        /// (<see cref="WorldGen.WildDepth"/>): under half at the edge, even at six steps, three
        /// times at twenty.
        /// </summary>
        public static float Strength(int distance) => 0.3f + 0.12f * distance;

        /// <summary>How many monsters a group of the kind has at the distance; <paramref name="luck"/> scales it.</summary>
        public static int Size(Monster monster, int distance, float luck)
        {
            int count = (int)MathF.Round(StartWorth * Strength(distance) * luck / Units.Worth(Units.Of(monster)));
            count = Math.Max(1, count);
            return monster == Monster.Troll ? Math.Min(count, MaxTrolls) : count;
        }

        /// <summary>
        /// The kind of a group: goblins and boars near the players, wolves, bandits and skeletons
        /// in the middle, spiders in far forests and trolls in far hills and mountains.
        /// </summary>
        public static Monster Kind(Rng rng, int distance, Biome biome)
        {
            float r = rng.Value();
            bool forest = biome == Biome.Forest, high = biome == Biome.Hills || biome == Biome.Mountains;
            if (distance < 5)
                return r < 0.6f ? Monster.Goblins : Monster.Boar;
            if (distance < 12)
            {
                if (forest)
                    return r < 0.45f ? Monster.Wolves : r < 0.7f ? Monster.Bandits : r < 0.85f ? Monster.Goblins : Monster.Spiders;
                if (high)
                    return r < 0.45f ? Monster.Skeletons : r < 0.8f ? Monster.Bandits : Monster.Wolves;
                return r < 0.35f ? Monster.Bandits : r < 0.6f ? Monster.Wolves : r < 0.8f ? Monster.Skeletons : Monster.Boar;
            }
            if (forest)
                return r < 0.6f ? Monster.Spiders : r < 0.8f ? Monster.Wolves : Monster.Bandits;
            if (high)
                return r < 0.35f ? Monster.Troll : r < 0.7f ? Monster.Skeletons : Monster.Bandits;
            return r < 0.35f ? Monster.Bandits : r < 0.65f ? Monster.Skeletons : r < 0.85f ? Monster.Wolves : Monster.Spiders;
        }

        /// <summary>Boars' lairs near the players, wolves' in the middle, trolls' far away.</summary>
        public static Monster LairKind(int distance) =>
            distance < 6 ? Monster.Boar : distance < 13 ? Monster.Wolves : Monster.Troll;
    }

    public static partial class WorldGen
    {
        /// <summary>
        /// How deep in the wild every cell lies for the players: steps over land from their
        /// kingdom, counted so the nearest unowned land is 1 even when other kingdoms stand
        /// between; 0 on their land, int.MaxValue where no way over land leads.
        /// </summary>
        public static int[] WildDepth(World world)
        {
            int[] steps = FromPlayers(world);
            int nearest = int.MaxValue;
            foreach (Cell c in world.Cells)
            {
                if (c.IsLand && c.Owner < 0 && c.City < 0)
                    nearest = Math.Min(nearest, steps[c.Index]);
            }
            if (nearest == int.MaxValue)
                return steps;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] != int.MaxValue)
                    steps[i] = Math.Max(Math.Min(steps[i], 1), steps[i] - (nearest - 1));
            }
            return steps;
        }

        static int[] FromPlayers(World world)
        {
            var steps = new int[world.Cells.Length];
            var queue = new Queue<int>();
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i] = int.MaxValue;
                if (world.Cells[i].Owner == world.Player)
                {
                    steps[i] = 0;
                    queue.Enqueue(i);
                }
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    int j = world.Neighbor(i, d);
                    if (j < 0 || steps[j] != int.MaxValue || !world.Cells[j].IsLand)
                        continue;
                    steps[j] = steps[i] + 1;
                    queue.Enqueue(j);
                }
            }
            return steps;
        }

        static void SetGroup(Cell c, Monster monster, int count, bool lair)
        {
            c.Monster = monster;
            c.Count = c.FirstCount = count;
            c.Lair = lair;
        }

        /// <summary>
        /// Monster groups on the wild land the players can walk to, at least three steps from
        /// every city and never side by side: first a few small ones nearest the players, then
        /// lairs, four or more steps apart, then the rest. A group's kind and size depend on how
        /// deep in the wild it stands (see <see cref="WildDepth"/>).
        /// </summary>
        static void Monsters(World world, Rng rng)
        {
            int[] depth = WildDepth(world);
            var wild = new List<int>();
            foreach (Cell c in world.Cells)
            {
                if (c.IsLand && c.Owner < 0 && c.City < 0 && c.Biome != Biome.Beach && depth[c.Index] != int.MaxValue &&
                    NearestCity(world, c.Hex) >= 3)
                    wild.Add(c.Index);
            }
            rng.Shuffle(wild);
            int lairs = Math.Clamp(wild.Count / 45, 3, 9), groups = wild.Count / 15;
            // The nearest cells first (in the shuffled order among equals), then all in that order.
            var nearest = new List<int>(wild);
            var rank = new Dictionary<int, int>();
            for (int k = 0; k < wild.Count; k++)
                rank[wild[k]] = k;
            nearest.Sort((a, b) => depth[a] != depth[b] ? depth[a].CompareTo(depth[b]) : rank[a].CompareTo(rank[b]));
            var placed = new List<Hex>();
            var dens = new List<Hex>();
            foreach (int i in nearest)
            {
                if (placed.Count >= Wilds.FirstGroups)
                    break;
                TryGroup(world, rng, i, depth[i], false, placed, dens);
            }
            foreach (int i in wild)
            {
                if (placed.Count >= Wilds.FirstGroups + lairs + groups)
                    break;
                TryGroup(world, rng, i, depth[i], dens.Count < lairs, placed, dens);
            }
        }

        /// <summary>A group on the cell unless one stands next to it; a lair if asked and no other is near.</summary>
        static void TryGroup(World world, Rng rng, int cell, int depth, bool lair, List<Hex> placed, List<Hex> dens)
        {
            Cell c = world.Cells[cell];
            foreach (Hex other in placed)
            {
                if (Hex.Distance(other, c.Hex) < 2)
                    return;
            }
            foreach (Hex den in dens)
                lair &= Hex.Distance(den, c.Hex) >= 4;
            Monster monster = lair ? Wilds.LairKind(depth) : Wilds.Kind(rng, depth, c.Biome);
            float luck = rng.Range(0.8f, 1.25f) * (lair ? 1.3f : 1f);
            SetGroup(c, monster, Wilds.Size(monster, depth, luck), lair);
            placed.Add(c.Hex);
            if (lair)
                dens.Add(c.Hex);
        }

        /// <summary>
        /// Chests, piles of goods, shrines and watchtowers on free wild land the players can walk
        /// to, two or more steps apart and from the cities; beyond three steps from the players'
        /// kingdom most of them have a monster group on them to beat first.
        /// </summary>
        static void Places(World world, Rng rng)
        {
            int[] from = WildDepth(world);
            var free = new List<int>();
            int wildCount = 0;
            foreach (Cell c in world.Cells)
            {
                if (!c.IsLand || c.Owner >= 0 || c.City >= 0)
                    continue;
                wildCount++;
                if (c.Resource == Resource.None && c.Monster == Monster.None && NearestCity(world, c.Hex) >= 2 &&
                    from[c.Index] != int.MaxValue)
                    free.Add(c.Index);
            }
            rng.Shuffle(free);
            var wanted = new List<Place> { Place.ShrineAttack, Place.ShrineAttack, Place.ShrineDefence, Place.ShrineDefence };
            for (int k = Math.Max(2, wildCount / 300); k > 0; k--)
                wanted.Add(Place.Watchtower);
            for (int k = wildCount / 40; k > 0; k--)
                wanted.Add(Place.Chest);
            for (int k = wildCount / 35; k > 0; k--)
            {
                float r = rng.Value();
                wanted.Add(r < 0.4f ? Place.WoodPile : r < 0.7f ? Place.StonePile : Place.GoldPile);
            }
            var placed = new List<Hex>();
            int next = 0;
            foreach (Place place in wanted)
            {
                for (; next < free.Count; next++)
                {
                    Cell c = world.Cells[free[next]];
                    int distance = from[free[next]];
                    bool crowded = false;
                    foreach (Hex other in placed)
                        crowded |= Hex.Distance(other, c.Hex) < 2;
                    if (crowded)
                        continue;
                    c.Place = place;
                    placed.Add(c.Hex);
                    if (distance > 3 && rng.Chance(place == Place.Watchtower ? 0.4f : 0.7f))
                    {
                        Monster monster = Wilds.Kind(rng, distance, c.Biome);
                        SetGroup(c, monster, Wilds.Size(monster, distance, rng.Range(0.8f, 1.25f)), false);
                    }
                    next++;
                    break;
                }
            }
        }
    }
}
