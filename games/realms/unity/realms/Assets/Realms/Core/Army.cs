using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>A kind of fighter: the three a kingdom hires, then the monsters.</summary>
    public enum Unit : byte
    {
        Spearmen,
        Archers,
        Riders,
        Goblins,
        Boars,
        Wolves,
        Bandits,
        Skeletons,
        Spiders,
        Troll,
    }

    [Flags]
    public enum Ability : byte
    {
        None = 0,

        /// <summary>Shoots any stack on the field unless an enemy stands next to it.</summary>
        Shoots = 1,

        /// <summary>Nobody strikes back at it.</summary>
        NoStrikeBack = 2,

        /// <summary>Arrows do half damage to it.</summary>
        ArrowProof = 4,

        /// <summary>A stack it hits cannot walk in its next turn.</summary>
        Web = 8,

        /// <summary>Its top unit heals a third of its health at the start of every round.</summary>
        Regenerates = 16,
    }

    public readonly struct UnitStats
    {
        public readonly int Health, Attack, Defence, MinDamage, MaxDamage, Speed;
        public readonly Ability Abilities;

        public UnitStats(int health, int attack, int defence, int minDamage, int maxDamage, int speed,
            Ability abilities = Ability.None)
        {
            Health = health;
            Attack = attack;
            Defence = defence;
            MinDamage = minDamage;
            MaxDamage = maxDamage;
            Speed = speed;
            Abilities = abilities;
        }

        public bool Has(Ability ability) => (Abilities & ability) != 0;
    }

    /// <summary>
    /// Whom a monster kind goes for in a fight. Each kind has its own way, so every group sets
    /// the players a different problem, and the way is simple enough to plan against (the
    /// battle plan of 2026-11-10: the players found every fight the same, the monsters always
    /// running at the archers).
    /// </summary>
    public enum Temper : byte
    {
        /// <summary>The hired units: the auto battle's own choice.</summary>
        None,

        /// <summary>Goblins: the nearest, the next ones piling onto a stack goblins struck this round.</summary>
        Mob,

        /// <summary>Boars: the stack in the row nearest their own, straight ahead.</summary>
        Charge,

        /// <summary>Wolves: the stack with the least health.</summary>
        Weakest,

        /// <summary>Bandits: the nearest.</summary>
        Nearest,

        /// <summary>Skeletons: the nearest stack that does not shoot.</summary>
        Wall,

        /// <summary>Spiders: the fastest stack not caught in a web yet.</summary>
        Fastest,

        /// <summary>The troll: the stack with the most health.</summary>
        Biggest,
    }

    /// <summary>One kind of unit with a count: "12 spearmen".</summary>
    public sealed class Stack
    {
        public Unit Unit;
        public int Count;

        public Stack(Unit unit, int count)
        {
            Unit = unit;
            Count = count;
        }
    }

    public static class Units
    {
        /// <summary>The units towns hire, in the order of their buildings.</summary>
        public static readonly Unit[] Hired = { Unit.Spearmen, Unit.Archers, Unit.Riders };

        /// <summary>A regenerating unit heals this share (one in so many) of its health a round.</summary>
        public const int HealShare = 3;

        static readonly UnitStats[] Table =
        {
            new UnitStats(10, 4, 5, 1, 3, 4), // spearmen
            new UnitStats(8, 5, 3, 2, 3, 4, Ability.Shoots), // archers
            new UnitStats(22, 7, 6, 3, 6, 7), // riders
            new UnitStats(5, 3, 1, 1, 2, 5), // goblins
            new UnitStats(8, 4, 2, 1, 3, 6), // boars
            new UnitStats(12, 5, 3, 2, 4, 8, Ability.NoStrikeBack), // wolves
            new UnitStats(10, 5, 4, 2, 3, 4, Ability.Shoots), // bandits
            new UnitStats(9, 5, 5, 1, 3, 4, Ability.ArrowProof), // skeletons
            new UnitStats(16, 6, 4, 2, 5, 6, Ability.Web), // spiders
            new UnitStats(60, 10, 8, 8, 14, 4, Ability.Regenerates), // troll
        };

        public static UnitStats Of(Unit unit) => Table[(int)unit];

        /// <summary>Whom the unit goes for in a fight; <see cref="Temper.None"/> for the hired ones.</summary>
        public static Temper TemperOf(Unit unit)
        {
            switch (unit)
            {
                case Unit.Goblins: return Temper.Mob;
                case Unit.Boars: return Temper.Charge;
                case Unit.Wolves: return Temper.Weakest;
                case Unit.Bandits: return Temper.Nearest;
                case Unit.Skeletons: return Temper.Wall;
                case Unit.Spiders: return Temper.Fastest;
                case Unit.Troll: return Temper.Biggest;
                default: return Temper.None;
            }
        }

        /// <summary>
        /// The chance that a beaten monster group of this kind offers to give up (see
        /// <see cref="Battle.Offered"/>). The players asked that not every fight end so, but by the
        /// monster's kind and by chance (2026-11-10): goblins give up in about half of the fights
        /// they lose, bandits and beasts less often, the troll seldom, and the dead never.
        /// </summary>
        public static float YieldChance(Unit unit)
        {
            switch (unit)
            {
                case Unit.Goblins: return 0.5f;
                case Unit.Bandits: return 0.4f;
                case Unit.Wolves: return 0.3f;
                case Unit.Boars: return 0.25f;
                case Unit.Spiders: return 0.2f;
                case Unit.Troll: return 0.15f;
                default: return 0f;
            }
        }

        /// <summary>The unit a monster group is made of.</summary>
        public static Unit Of(Monster monster)
        {
            switch (monster)
            {
                case Monster.Troll: return Unit.Troll;
                case Monster.Wolves: return Unit.Wolves;
                case Monster.Boar: return Unit.Boars;
                case Monster.Goblins: return Unit.Goblins;
                case Monster.Bandits: return Unit.Bandits;
                case Monster.Skeletons: return Unit.Skeletons;
                case Monster.Spiders: return Unit.Spiders;
                default: throw new ArgumentOutOfRangeException(nameof(monster), monster, null);
            }
        }

        /// <summary>The town building that hires the unit.</summary>
        public static TownBuilding Building(Unit unit)
        {
            switch (unit)
            {
                case Unit.Spearmen: return TownBuilding.Barracks;
                case Unit.Archers: return TownBuilding.ArcheryRange;
                case Unit.Riders: return TownBuilding.Stables;
                default: throw new ArgumentOutOfRangeException(nameof(unit), unit, "monsters are not hired");
            }
        }

        /// <summary>What one unit costs to hire.</summary>
        public static Stock Cost(Unit unit)
        {
            switch (unit)
            {
                case Unit.Spearmen: return new Stock(4, 1, 0, 0);
                case Unit.Archers: return new Stock(6, 0, 1, 0);
                case Unit.Riders: return new Stock(14, 2, 0, 0);
                default: return default;
            }
        }

        /// <summary>Recruits the unit's building adds to its town's pool a turn.</summary>
        public static int PerTurn(Unit unit) => unit == Unit.Spearmen ? 4 : unit == Unit.Archers ? 3 : unit == Unit.Riders ? 2 : 0;

        /// <summary>The most recruits a town keeps waiting: three turns' worth.</summary>
        public static int PoolLimit(Unit unit) => 3 * PerTurn(unit);

        /// <summary>
        /// A rough worth of one unit for sizing monster groups: the square root of health times the
        /// mean damage, with a little for attack and defence. Armies fight stack against stack,
        /// so their strength grows with the square of the count (Lanchester) and the worth of n
        /// units is n times this.
        /// </summary>
        public static float Worth(Unit unit)
        {
            UnitStats s = Of(unit);
            float worth = MathF.Sqrt(s.Health * 0.5f * (s.MinDamage + s.MaxDamage) * (1f + 0.05f * (s.Attack + s.Defence)));
            if (s.Has(Ability.Shoots))
                worth *= 1.25f;
            if (s.Has(Ability.Regenerates))
                worth *= 1.5f;
            return worth;
        }
    }

    public static class Armies
    {
        /// <summary>The most stacks a hero's army or a town's garrison holds.</summary>
        public const int MaxStacks = 5;

        public static bool CanAdd(List<Stack> army, Unit unit)
        {
            if (army.Count < MaxStacks)
                return true;
            foreach (Stack s in army)
            {
                if (s.Unit == unit)
                    return true;
            }
            return false;
        }

        /// <summary>Adds units to the stack of their kind, or as a new stack; false when there is no room.</summary>
        public static bool Add(List<Stack> army, Unit unit, int count)
        {
            if (count <= 0)
                return true;
            foreach (Stack s in army)
            {
                if (s.Unit == unit)
                {
                    s.Count += count;
                    return true;
                }
            }
            if (army.Count >= MaxStacks)
                return false;
            army.Add(new Stack(unit, count));
            return true;
        }

        /// <summary>Moves every stack of <paramref name="from"/> that fits into <paramref name="to"/>.</summary>
        public static void Merge(List<Stack> to, List<Stack> from)
        {
            for (int i = 0; i < from.Count; i++)
            {
                if (Add(to, from[i].Unit, from[i].Count))
                {
                    from.RemoveAt(i);
                    i--;
                }
            }
        }

        /// <summary>
        /// Moves up to <paramref name="count"/> units of the kind from one army to another (heroes
        /// meeting, or a hero and his town's garrison); an emptied stack goes. Returns how many moved:
        /// none when the other army has no room for a new stack.
        /// </summary>
        public static int Move(List<Stack> from, List<Stack> to, Unit unit, int count)
        {
            int k = from.FindIndex(s => s.Unit == unit);
            if (k < 0 || count <= 0)
                return 0;
            Stack stack = from[k];
            count = Math.Min(count, stack.Count);
            if (!Add(to, unit, count))
                return 0;
            stack.Count -= count;
            if (stack.Count == 0)
                from.RemoveAt(k);
            return count;
        }

        /// <summary>How many units of the kind the army has.</summary>
        public static int Count(List<Stack> army, Unit unit)
        {
            foreach (Stack s in army)
            {
                if (s.Unit == unit)
                    return s.Count;
            }
            return 0;
        }

        public static int Count(List<Stack> army)
        {
            int count = 0;
            foreach (Stack s in army)
                count += s.Count;
            return count;
        }

        /// <summary>The army's worth (see <see cref="Units.Worth"/>).</summary>
        public static float Worth(List<Stack> army)
        {
            float worth = 0;
            foreach (Stack s in army)
                worth += s.Count * Units.Worth(s.Unit);
            return worth;
        }
    }
}
