using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>
    /// The army's soft upkeep (the economy plan's 3.4; the family said "Окей, давай попробуем." on
    /// 2026-11-12): the kingdom's first <see cref="Free"/> warriors eat nothing, each further
    /// <see cref="PerFood"/> eat one food a turn, and a rider counts as two. The army eats at the
    /// turn's end, after the income and before the towns take food for their growth. When the
    /// stores run short, nobody dies or leaves: the heroes walk half the next turn
    /// (<see cref="HeroRules.HungryMoves"/>). A constant sink for the food that piled up, tied to
    /// the heroes by one rule; the free first warriors keep a beaten kingdom from being locked in.
    /// </summary>
    public static class Upkeep
    {
        /// <summary>The warriors who eat nothing.</summary>
        public const int Free = 15;

        /// <summary>Each this many more eat one food a turn.</summary>
        public const int PerFood = 10;

        /// <summary>How many warriors a stack is for the upkeep: a rider counts as two.</summary>
        public static int Heads(Unit unit, int count) => unit == Unit.Riders ? 2 * count : count;

        /// <summary>The kingdom's warriors: its heroes' armies and its towns' garrisons, a rider counting as two.</summary>
        public static int Warriors(GameState state, int country)
        {
            int warriors = 0;
            foreach (Hero hero in state.Heroes)
            {
                if (hero.Country == country)
                    warriors += Heads(hero.Army);
            }
            foreach (int index in state.World.Countries[country].Cities)
                warriors += Heads(state.World.Cities[index].Garrison);
            return warriors;
        }

        static int Heads(List<Stack> stacks)
        {
            int heads = 0;
            foreach (Stack stack in stacks)
                heads += Heads(stack.Unit, stack.Count);
            return heads;
        }

        /// <summary>The food so many warriors eat a turn: 0 up to 15, 1 for 16-25, 2 for 26-35 and so on.</summary>
        public static int Food(int warriors) => warriors <= Free ? 0 : (warriors - Free + PerFood - 1) / PerFood;

        /// <summary>The food the kingdom's army eats a turn.</summary>
        public static int Of(GameState state, int country) => Food(Warriors(state, country));

        /// <summary>How much more food a turn the army would eat with <paramref name="count"/> more of <paramref name="unit"/>.</summary>
        public static int More(GameState state, int country, Unit unit, int count)
        {
            int warriors = Warriors(state, country);
            return Food(warriors + Heads(unit, count)) - Food(warriors);
        }

        /// <summary>
        /// The army eats from the treasury, as much as it has; returns what it ate and sets
        /// <paramref name="hungry"/> when that was less than the army wants.
        /// </summary>
        public static int Eat(GameState state, int country, out bool hungry)
        {
            int want = Of(state, country);
            int ate = Math.Min(want, Math.Max(0, state.Treasury[country].Food));
            state.Treasury[country].Food -= ate;
            hungry = ate < want;
            return ate;
        }
    }
}
