using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>A kingdom's piece on the map: a rider who leads an army of up to five stacks.</summary>
    public sealed class Hero
    {
        public int Index;
        public string Name;

        /// <summary>The name as the players read it, in the game's language (<see cref="Names.Shown"/>).</summary>
        public string Label => Names.Shown(Name);
        public int Country;
        public int Cell;
        public int Attack = 1, Defence = 1;
        public int Level = 1, Experience;

        /// <summary>Moves left this turn.</summary>
        public int Moves = HeroRules.MovesPerTurn;

        /// <summary>Level-ups whose card (attack or defence) the players have not chosen yet.</summary>
        public int LevelUps;

        public readonly List<Stack> Army = new List<Stack>();

        /// <summary>Cells of the shrines this hero has visited.</summary>
        public readonly List<int> Shrines = new List<int>();

        /// <summary>A settlers' cart travels with the hero, to found a town (see <see cref="Settling"/>).</summary>
        public bool Settlers;
    }

    /// <summary>A way for a hero to a cell: the cells after the start and the turn each step ends in.</summary>
    public sealed class HeroPath
    {
        public readonly List<int> Cells = new List<int>();

        /// <summary>The turn each step is made in: 0 this turn, 1 the next and so on.</summary>
        public readonly List<int> Turns = new List<int>();

        public int Target => Cells.Count == 0 ? -1 : Cells[Cells.Count - 1];

        /// <summary>
        /// The way runs to a cell the players have not seen: on through the fog as if it were
        /// meadow, so it is laid again after every step as the fog lifts (see <see cref="GameState.Path"/>).
        /// </summary>
        public bool Fog;

        /// <summary>Steps made this turn.</summary>
        public int Now
        {
            get
            {
                int n = 0;
                while (n < Turns.Count && Turns[n] == 0)
                    n++;
                return n;
            }
        }
    }

    public static class HeroRules
    {
        public const int MovesPerTurn = 16;

        /// <summary>The steps of a turn after the army went hungry: half (<see cref="Upkeep"/>).</summary>
        public const int HungryMoves = MovesPerTurn / 2;
        public const int MaxHeroes = 3;
        public const int HireGold = 15;
        public const int HireSpearmen = 5;
        public const int StartSpearmen = 10, StartArchers = 4;

        /// <summary>
        /// The spearmen a hero beaten to the last unit finds in the town he goes back to, so that
        /// he can try again soon (the fun plan of 2026-11-11, from the economy plan's militia).
        /// </summary>
        public const int Militia = 5;

        /// <summary>What a step into the fog is reckoned to cost on a way there: a meadow's, as the land is not known.</summary>
        public const int FogStep = 2;

        /// <summary>A hero opens the fog this far around him, one more from hills and mountains.</summary>
        public const int Sight = 3;

        /// <summary>The heroes' names: short and of no one people, so they read the same in both languages (2026-11-12).</summary>
        public static readonly string[] Names =
        {
            "Арен", "Лина", "Тео", "Мира", "Кай", "Нора", "Феликс", "Ида", "Лео", "Элла", "Финн", "Ирис",
        };

        /// <summary>Experience for the next level: 100 for level 2, 300 for 3, 600 for 4, 1000 for 5…</summary>
        public static int NextLevel(int level) => 50 * level * (level + 1);

        /// <summary>Moves to enter a cell of the biome without a road.</summary>
        public static int Cost(Biome biome)
        {
            switch (biome)
            {
                case Biome.Forest:
                case Biome.Hills:
                    return 3;
                case Biome.Mountains:
                    return 4;
                default:
                    return 2;
            }
        }

        /// <summary>
        /// Moves to step from a cell across edge <paramref name="direction"/>: 1 along a road, else
        /// the biome's cost and 2 more to cross a river without a bridge.
        /// </summary>
        public static int StepCost(World world, int from, int direction)
        {
            Cell a = world.Cells[from];
            int to = world.Neighbor(from, direction);
            if (((a.Roads >> direction) & 1) != 0)
                return 1;
            int cost = Cost(world.Cells[to].Biome);
            if (((a.Rivers >> direction) & 1) != 0)
                cost += 2;
            return cost;
        }

        public static int SightOf(Cell cell) =>
            Sight + (cell.Biome == Biome.Hills || cell.Biome == Biome.Mountains ? 1 : 0);
    }
}
