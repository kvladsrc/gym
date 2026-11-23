using System;

namespace Realms.Core
{
    /// <summary>
    /// The Great Build (the economy plan's 3.4, 2026-11-11): a capital with a castle raises it in
    /// three stages, one after another. The first two bring pilgrims, <see cref="GoldPerStage"/>
    /// gold a turn each; the third finishes it. It gives the stone and wood that piled up a use to
    /// the game's end, and its income asks whether to start early or to save for something else.
    /// The crafts' cloth and iron do not exist yet, so the third stage takes stone in their place,
    /// as the plan says.
    /// </summary>
    public static class GreatBuild
    {
        public const int Stages = 3;

        /// <summary>The gold a turn each of the first two stages brings.</summary>
        public const int GoldPerStage = 2;

        /// <summary>The stages that bring gold: the first two.</summary>
        public const int PayingStages = 2;

        /// <summary>The stages' prices, gold / food / wood / stone.</summary>
        static readonly Stock[] Prices =
        {
            new Stock(0, 0, 20, 30),
            new Stock(30, 0, 30, 40),
            new Stock(60, 0, 0, 40),
        };

        /// <summary>The price of <paramref name="stage"/> (1 to <see cref="Stages"/>).</summary>
        public static Stock Cost(int stage)
        {
            if (stage < 1 || stage > Stages)
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "a stage of the Great Build is 1 to 3");
            return Prices[stage - 1];
        }

        /// <summary>The stage the town would raise next, or 0 when the build stands finished.</summary>
        public static int Next(City city) => city.BuildStage < Stages ? city.BuildStage + 1 : 0;

        /// <summary>
        /// Whether the town may raise its next stage: only a capital, only with a castle, the
        /// stages in order, and the first on a free place of the town, as a building takes one.
        /// </summary>
        public static Refusal Can(City city, Stock treasury)
        {
            if (!city.Capital)
                return Refusal.NotTheCapital;
            if (city.BuildStage >= Stages)
                return Refusal.AlreadyBuilt;
            if (!city.Buildings.Contains(TownBuilding.Castle))
                return Refusal.NoCastle;
            if (city.BuildStage == 0 && Towns.Free(city) == 0)
                return Refusal.NoSlot;
            return treasury.Covers(Cost(city.BuildStage + 1)) ? Refusal.None : Refusal.NotEnough;
        }

        /// <summary>What the town's build brings a turn.</summary>
        public static Stock Yield(City city) => new Stock(GoldPerStage * Math.Min(city.BuildStage, PayingStages), 0, 0, 0);

        /// <summary>What <paramref name="stage"/> adds to the income (nothing for the last).</summary>
        public static int GoldOf(int stage) => stage >= 1 && stage <= PayingStages ? GoldPerStage : 0;
    }
}
