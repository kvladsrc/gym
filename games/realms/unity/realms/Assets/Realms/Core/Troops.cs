using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>A neighbour's troop standing at a lair under its flag (<see cref="Troops"/>).</summary>
    public sealed class Troop
    {
        /// <summary>The kingdom that sent it.</summary>
        public int Country;

        /// <summary>The lair's cell.</summary>
        public int Lair;

        /// <summary>Turns until it clears the lair, as its flag shows them.</summary>
        public int Turns;

        /// <summary>The spearmen in it.</summary>
        public int Men;

        /// <summary>What the kingdom paid for it.</summary>
        public Stock Paid;
    }

    /// <summary>
    /// The neighbours' troops to the lairs, the economy plan's step 4 (2026-11-11). A neighbour has
    /// no hero; for a lair it buys a troop of spearmen worth <see cref="Margin"/> times the lair's
    /// group at the spearmen's price. The troop stands <see cref="StandTurns"/> turns at the lair
    /// under the kingdom's flag and then clears it in the kingdom's favour as a hero's win would:
    /// the lair's reward, the count for the gem «Охотник» and the cell into its land when it may
    /// take it. A lair under a troop sends no bands. When the players' hero clears the lair first,
    /// the troop goes home and half its price comes back. A kingdom has one troop out at a time and
    /// sends it to the nearest lair within <see cref="Reach"/> steps of its towns that it can pay for.
    /// </summary>
    public static class Troops
    {
        /// <summary>A troop is worth this many times the lair's group (the plan's 1.2).</summary>
        public const float Margin = 1.2f;

        /// <summary>Turns a troop stands at the lair before it is cleared.</summary>
        public const int StandTurns = 3;

        /// <summary>The farthest lair a kingdom sends a troop to, in steps from its towns: as far as a lair sends its bands.</summary>
        public const int Reach = Raids.Reach;

        /// <summary>
        /// A kingdom saves for a troop only when its income pays for it within this many turns. Without
        /// it a neighbour after the hunter's gem saved for a troll lair it could never pay for and
        /// built nothing else (the measurer, 2026-11-11).
        /// </summary>
        public const int SaveTurns = 6;

        /// <summary>The spearmen a troop for the group needs: <see cref="Margin"/> times its worth.</summary>
        public static int Men(Monster monster, int count) =>
            Math.Max(1, (int)MathF.Ceiling(Margin * count * Units.Worth(Units.Of(monster)) / Units.Worth(Unit.Spearmen) - 1e-3f));

        /// <summary>What a troop for the group costs: its spearmen at the spearmen's price.</summary>
        public static Stock Price(Monster monster, int count) => Units.Cost(Unit.Spearmen) * Men(monster, count);

        /// <summary>The troop at the lair, or null.</summary>
        public static Troop At(GameState state, int lair)
        {
            foreach (Troop troop in state.LairTroops)
            {
                if (troop.Lair == lair)
                    return troop;
            }
            return null;
        }

        /// <summary>The kingdom's troop out, or null.</summary>
        public static Troop Of(GameState state, int country)
        {
            foreach (Troop troop in state.LairTroops)
            {
                if (troop.Country == country)
                    return troop;
            }
            return null;
        }

        /// <summary>Steps from the kingdom's nearest town (its centre or a quarter) to the cell.</summary>
        public static int Distance(World world, int country, int cell)
        {
            Hex at = world.Cells[cell].Hex;
            int best = int.MaxValue;
            foreach (int index in world.Countries[country].Cities)
            {
                City city = world.Cities[index];
                best = Math.Min(best, Hex.Distance(at, world.Cells[city.Cell].Hex));
                foreach (int district in city.Districts)
                    best = Math.Min(best, Hex.Distance(at, world.Cells[district].Hex));
            }
            return best;
        }

        /// <summary>
        /// Whether the kingdom's troops have a lair to go for: its troop is out, or a lair with a
        /// group and no troop lies within <see cref="Reach"/> and its price within <see cref="CanSave"/>.
        /// </summary>
        public static bool Hunting(GameState state, int country)
        {
            if (Of(state, country) != null)
                return true;
            foreach (Cell c in state.World.Cells)
            {
                if (c.Lair && c.Monster != Monster.None && At(state, c.Index) == null && Distance(state.World, country, c.Index) <= Reach
                    && CanSave(state, country, Price(c.Monster, c.Count)))
                    return true;
            }
            return false;
        }

        /// <summary>Whether the kingdom has the price or gathers it within <see cref="SaveTurns"/> turns of its income (the food its towns do not eat).</summary>
        public static bool CanSave(GameState state, int country, Stock price)
        {
            Stock has = state.Treasury[country], income = Economy.Income(state.World, country);
            int food = Math.Max(0, income.Food - Towns.Demand(state.World, country));
            return price.Gold <= has.Gold + SaveTurns * income.Gold && price.Food <= has.Food + SaveTurns * food;
        }

        /// <summary>
        /// The lair the kingdom would send its troop to: the nearest within <see cref="Reach"/> with
        /// a group and no troop that it can pay for (the lower cell on a tie), or -1;
        /// <paramref name="tooDear"/> tells that a lair was within reach and its price within
        /// <see cref="CanSave"/>, but not in the treasury yet.
        /// </summary>
        public static int Choose(GameState state, int country, out bool tooDear)
        {
            World world = state.World;
            tooDear = false;
            int best = -1, bestDistance = int.MaxValue;
            foreach (Cell c in world.Cells)
            {
                if (!c.Lair || c.Monster == Monster.None || At(state, c.Index) != null)
                    continue;
                int distance = Distance(world, country, c.Index);
                if (distance > Reach || distance >= bestDistance)
                    continue;
                Stock price = Price(c.Monster, c.Count);
                if (!state.Treasury[country].Covers(price))
                {
                    tooDear |= CanSave(state, country, price);
                    continue;
                }
                best = c.Index;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>
        /// Sends the kingdom's troop to the lair <see cref="Choose"/> picks and pays for it; false
        /// when the kingdom has one out already or no lair to send it to. The players send none:
        /// they have heroes.
        /// </summary>
        public static bool Send(GameState state, int country, TurnReport report = null) => Send(state, country, out _, report);

        /// <inheritdoc cref="Send(GameState, int, TurnReport)"/>
        public static bool Send(GameState state, int country, out bool tooDear, TurnReport report = null)
        {
            tooDear = false;
            World world = state.World;
            if (country == world.Player || Of(state, country) != null)
                return false;
            int lair = Choose(state, country, out tooDear);
            if (lair < 0)
                return false;
            Cell c = world.Cells[lair];
            var troop = new Troop
            {
                Country = country, Lair = lair, Turns = StandTurns, Men = Men(c.Monster, c.Count), Paid = Price(c.Monster, c.Count),
            };
            state.Treasury[country] -= troop.Paid;
            state.LairTroops.Add(troop);
            state.TroopsSent[country]++;
            if (report != null)
            {
                report.Changed.Add(lair);
                if (state.Seen[lair])
                    report.Events.Add(new TurnEvent(TurnEventKind.TroopSent, -1, country: country, cell: lair, monster: c.Monster, amount: StandTurns));
            }
            return true;
        }

        /// <summary>
        /// The troops' part of the turn's end, before the neighbours buy: a troop whose lair someone
        /// else cleared goes home with half its price; the others count down and clear their lairs.
        /// </summary>
        public static void Play(GameState state, TurnReport report)
        {
            World world = state.World;
            for (int t = state.LairTroops.Count - 1; t >= 0; t--)
            {
                Troop troop = state.LairTroops[t];
                Cell c = world.Cells[troop.Lair];
                report.Changed.Add(troop.Lair);
                if (!c.Lair || c.Monster == Monster.None)
                {
                    Stock back = Half(troop.Paid);
                    state.Treasury[troop.Country] += back;
                    state.TroopsRecalled[troop.Country]++;
                    state.LairTroops.RemoveAt(t);
                    if (state.Seen[troop.Lair])
                        report.Events.Add(new TurnEvent(TurnEventKind.TroopRecalled, -1, country: troop.Country, cell: troop.Lair, amount: back.Gold));
                    continue;
                }
                if (--troop.Turns > 0)
                    continue;
                state.LairTroops.RemoveAt(t);
                Clear(state, troop, report);
            }
        }

        /// <summary>Clears the troop's lair for its kingdom as a hero's win would.</summary>
        static void Clear(GameState state, Troop troop, TurnReport report)
        {
            World world = state.World;
            Cell c = world.Cells[troop.Lair];
            Monster monster = c.Monster;
            int k = troop.Country;
            state.Treasury[k] += GameState.Reward(monster, c.Count, true);
            state.Lairs[k]++;
            state.TroopsWon[k]++;
            c.Monster = Monster.None;
            c.Count = c.FirstCount = 0;
            c.Lair = false;
            if (Influence.TakesCleared(world, k, troop.Lair))
                Influence.Take(state, k, troop.Lair, report);
            if (state.Seen[troop.Lair])
                report.Events.Add(new TurnEvent(TurnEventKind.TroopCleared, -1, country: k, cell: troop.Lair, monster: monster));
        }

        static Stock Half(Stock s) => new Stock(s.Gold / 2, s.Food / 2, s.Wood / 2, s.Stone / 2);
    }

    public sealed partial class GameState
    {
        /// <summary>The neighbours' troops standing at lairs (<see cref="Core.Troops"/>).</summary>
        public readonly List<Troop> LairTroops = new List<Troop>();

        /// <summary>Per kingdom: troops sent, lairs they cleared, and troops that went home because someone was first.</summary>
        public int[] TroopsSent, TroopsWon, TroopsRecalled;

        void InitTroops()
        {
            int n = World.Countries.Count;
            TroopsSent = new int[n];
            TroopsWon = new int[n];
            TroopsRecalled = new int[n];
        }
    }
}
