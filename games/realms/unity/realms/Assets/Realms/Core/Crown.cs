using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>The crown's gems, one for each great feat; a gem once won stays.</summary>
    public enum Gem : byte
    {
        /// <summary>The capital has become a royal city (the fourth level).</summary>
        Capital,

        /// <summary>Four towns of the third level.</summary>
        Towns,

        /// <summary>Three lairs cleared.</summary>
        Hunter,

        /// <summary>A hero of the fifth level.</summary>
        Hero,

        /// <summary>The kingdom's land has grown by <see cref="Crown.LandsGoal"/> cells since the start.</summary>
        Lands,
    }

    /// <summary>
    /// The game's goal and end (the economy and fun plans of 2026-11-11): every kingdom has a crown
    /// with a socket for each gem, the gems are won in any order, and the first kingdom to set
    /// <see cref="Needed"/> of them has won. The players chose this short game, three gems of five
    /// in about 35-45 turns, as the main one; a longer crown waits until the game has more to do
    /// in its middle. The numbers are the plan's first ones, to be tuned by the measurer.
    /// </summary>
    public static class Crown
    {
        public static readonly Gem[] Order = { Gem.Capital, Gem.Towns, Gem.Hunter, Gem.Hero, Gem.Lands };

        /// <summary>The gems that win the game.</summary>
        public const int Needed = 3;

        public const int CapitalLevel = 4, TownsLevel = 3, TownsGoal = 4, HunterGoal = 3, HeroGoal = 5, LandsGoal = 60;

        /// <summary>How far the kingdom has come towards the gem and how far it must, as in «2 из 3».</summary>
        public static (int Have, int Need) Progress(GameState state, int country, Gem gem)
        {
            World world = state.World;
            switch (gem)
            {
                case Gem.Capital:
                {
                    int capital = world.Countries[country].Capital;
                    return (capital >= 0 ? Towns.Level(world.Cities[capital]) : 0, CapitalLevel);
                }
                case Gem.Towns:
                {
                    int towns = 0;
                    foreach (int index in world.Countries[country].Cities)
                    {
                        if (Towns.Level(world.Cities[index]) >= TownsLevel)
                            towns++;
                    }
                    return (towns, TownsGoal);
                }
                case Gem.Hunter:
                    return (state.Lairs[country], HunterGoal);
                case Gem.Hero:
                {
                    int level = 0;
                    foreach (Hero hero in state.Heroes)
                    {
                        if (hero.Country == country)
                            level = Math.Max(level, hero.Level);
                    }
                    return (level, HeroGoal);
                }
                case Gem.Lands:
                    return (Math.Max(0, Land(world, country) - state.StartLand[country]), LandsGoal);
                default:
                    return (0, 1);
            }
        }

        public static bool Reached(GameState state, int country, Gem gem)
        {
            (int have, int need) = Progress(state, country, gem);
            return have >= need;
        }

        /// <summary>The kingdom's land cells (the sea it holds does not count).</summary>
        public static int Land(World world, int country)
        {
            int land = 0;
            foreach (Cell c in world.Cells)
            {
                if (c.Owner == country && c.IsLand)
                    land++;
            }
            return land;
        }
    }

    /// <summary>A gem a kingdom has won, and the turn it came on.</summary>
    public readonly struct GemWon
    {
        public readonly Gem Gem;
        public readonly int Turn;

        public GemWon(Gem gem, int turn)
        {
            Gem = gem;
            Turn = turn;
        }
    }

    public sealed partial class GameState
    {
        /// <summary>Each kingdom's gems in the order they were won.</summary>
        public List<GemWon>[] Gems;

        /// <summary>Each kingdom's land at the start, for the gem «Земли».</summary>
        public int[] StartLand;

        /// <summary>Lairs each kingdom has cleared, for the gem «Охотник».</summary>
        public int[] Lairs;

        /// <summary>The turn on which each kingdom set its crown's last gem; 0 while it has not.</summary>
        public int[] Crowned;

        void StartCrown()
        {
            int count = World.Countries.Count;
            Gems = new List<GemWon>[count];
            StartLand = new int[count];
            Lairs = new int[count];
            Crowned = new int[count];
            for (int k = 0; k < count; k++)
            {
                Gems[k] = new List<GemWon>();
                StartLand[k] = Crown.Land(World, k);
            }
        }

        public bool HasGem(int country, Gem gem)
        {
            foreach (GemWon won in Gems[country])
            {
                if (won.Gem == gem)
                    return true;
            }
            return false;
        }

        /// <summary>The first kingdom (the players' if it was among them) to set its crown, or -1.</summary>
        public int Winner
        {
            get
            {
                int winner = -1;
                for (int k = 0; k < Crowned.Length; k++)
                {
                    if (Crowned[k] == 0)
                        continue;
                    if (winner < 0 || Crowned[k] < Crowned[winner] || (Crowned[k] == Crowned[winner] && k == World.Player))
                        winner = k;
                }
                return winner;
            }
        }

        /// <summary>The kingdom's place among the crowns: 1 for the first, 0 while it has none.</summary>
        public int CrownPlace(int country)
        {
            if (Crowned[country] == 0)
                return 0;
            int place = 1;
            for (int k = 0; k < Crowned.Length; k++)
            {
                if (k != country && Crowned[k] > 0 && Crowned[k] < Crowned[country])
                    place++;
            }
            return place;
        }

        /// <summary>Sets the gems the kingdoms have just won, the players' kingdom first; returns them.</summary>
        public List<(int Country, Gem Gem)> CheckCrown()
        {
            var won = new List<(int, Gem)>();
            int count = Gems.Length;
            for (int i = 0; i < count; i++)
            {
                int k = (World.Player + i) % count;
                foreach (Gem gem in Crown.Order)
                {
                    if (HasGem(k, gem) || !Crown.Reached(this, k, gem))
                        continue;
                    Gems[k].Add(new GemWon(gem, Turn));
                    won.Add((k, gem));
                    if (Gems[k].Count == Crown.Needed)
                        Crowned[k] = Turn;
                }
            }
            return won;
        }
    }
}
