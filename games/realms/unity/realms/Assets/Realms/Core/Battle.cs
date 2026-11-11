using System;
using System.Collections.Generic;

namespace Realms.Core
{
    public enum Side : byte
    {
        Players,
        Monsters,
    }

    public enum Outcome : byte
    {
        Running,
        Won,
        Lost,
    }

    /// <summary>A stack on the battlefield.</summary>
    public sealed class Fighter
    {
        public int Id;
        public Unit Unit;
        public UnitStats Stats;
        public Side Side;
        public int Count, StartCount;

        /// <summary>Health of the top unit; the others are whole.</summary>
        public int Top;

        /// <summary>Index of the battlefield cell.</summary>
        public int Cell;

        /// <summary>Index of the hero's stack it came from, or -1 for monsters.</summary>
        public int Source = -1;

        public bool StruckBack, Defending;

        /// <summary>Caught in a web: it cannot walk in its next turn.</summary>
        public bool Webbed;

        /// <summary>Acting now while caught in a web.</summary>
        public bool Stuck;

        public bool Alive => Count > 0;
        public int TotalHealth => Count <= 0 ? 0 : (Count - 1) * Stats.Health + Top;
    }

    public enum BattleEventKind : byte
    {
        /// <summary>A new round began.</summary>
        Round,

        /// <summary>The actor walked along <see cref="BattleEvent.Path"/>.</summary>
        Walk,

        Strike,
        StrikeBack,
        Shoot,
        Defend,

        /// <summary>The troll healed <see cref="BattleEvent.Damage"/> health of his wounds.</summary>
        Heal,

        /// <summary>The target is caught in a web.</summary>
        Web,

        /// <summary>The monsters offer to give up (<see cref="Battle.Offered"/>); the actor is their biggest stack.</summary>
        Surrender,
    }

    public sealed class BattleEvent
    {
        public BattleEventKind Kind;
        public int Actor = -1, Target = -1, Damage, Killed;
        public List<int> Path;
    }

    /// <summary>
    /// A fight between a hero's army and a monster group on a field of 11 × 7 hexes (odd rows
    /// shifted, as on the map). Every round each stack acts once, the fastest first: it walks and
    /// strikes, shoots or defends. The input is the two armies and the ground, the output the
    /// survivors, so another kind of battle could take its place.
    /// </summary>
    public sealed class Battle
    {
        public const int Width = 11, Height = 7;

        /// <summary>Shots beyond this many cells do half damage.</summary>
        public const int ShortRange = 6;

        /// <summary>A fight still going after this many rounds is lost.</summary>
        public const int MaxRounds = 50;

        public readonly List<Fighter> Fighters = new List<Fighter>();
        public readonly bool[] Blocked = new bool[Width * Height];
        public readonly int Attack, Defence;
        public readonly Biome Ground;
        public readonly List<BattleEvent> Events = new List<BattleEvent>();

        public int Round { get; private set; }
        public Outcome Outcome { get; private set; }
        public Fighter Current { get; private set; }

        /// <summary>Total health of the monsters killed: the hero's experience.</summary>
        public int Experience { get; private set; }

        /// <summary>
        /// The beaten monsters offer to give up and wait for the players' answer: <see cref="Accept"/>
        /// or <see cref="FightOn"/>. Any action of the players' stacks also fights on.
        /// </summary>
        public bool Offered { get; private set; }

        /// <summary>The monsters gave up and were let go: the fight is won with some of them alive.</summary>
        public bool Surrendered { get; private set; }

        /// <summary>
        /// The players gave up the field (<see cref="Retreat"/>): a loss, but the hero led the
        /// living away, each stack less a quarter.
        /// </summary>
        public bool Retreated { get; private set; }

        /// <summary>
        /// How many times its reward the fight pays when it is won: by how it looked before it
        /// (<see cref="Odds.RewardFactor"/>); the game sets it when the fight starts.
        /// </summary>
        public int RewardFactor = 1;

        /// <summary>The monsters' share of their starting health at or below which they may give up.</summary>
        public const float YieldBelow = 1f / 3f;

        /// <summary>How many times the monsters' health the players' stacks must have for them to give up.</summary>
        public const float YieldOdds = 2f;

        readonly Rng rng;

        /// <summary>Its own sequence for the one roll of the surrender, so that the fight's own rolls stay as they were.</summary>
        readonly Rng yieldRng;
        readonly int monsterStart;
        bool yieldRolled;
        readonly List<Fighter> order = new List<Fighter>();
        int next;

        static readonly int[][] Neighbours = new int[Width * Height][];
        static readonly Hex[] Hexes = new Hex[Width * Height];

        static Battle()
        {
            for (int i = 0; i < Width * Height; i++)
                Hexes[i] = Hex.FromOffset(i % Width, i / Width);
            for (int i = 0; i < Width * Height; i++)
            {
                var list = new List<int>(6);
                for (int d = 0; d < 6; d++)
                {
                    int j = IndexOf(Hexes[i].Neighbor(d));
                    if (j >= 0)
                        list.Add(j);
                }
                Neighbours[i] = list.ToArray();
            }
        }

        public static int IndexOf(Hex hex)
        {
            int col = hex.Col, row = hex.Row;
            return col < 0 || row < 0 || col >= Width || row >= Height ? -1 : row * Width + col;
        }

        public static Hex HexOf(int cell) => Hexes[cell];

        public static int[] NeighboursOf(int cell) => Neighbours[cell];

        public static int Distance(int a, int b) => Hex.Distance(Hexes[a], Hexes[b]);

        /// <param name="army">The hero's stacks, in order; their survivors are written back by <see cref="Survivors"/>.</param>
        /// <param name="attack">The hero's attack, added to every stack of the army.</param>
        /// <param name="defence">The hero's defence.</param>
        public Battle(IReadOnlyList<Stack> army, int attack, int defence, Monster monster, int count, Biome ground, int seed)
        {
            Attack = attack;
            Defence = defence;
            Ground = ground;
            rng = new Rng(seed);
            yieldRng = new Rng(seed ^ 0x5A17B3C9);
            PlaceObstacles();
            var mine = new List<int>();
            for (int i = 0; i < army.Count; i++)
            {
                if (army[i].Count > 0)
                    mine.Add(i);
            }
            int[] rows = Rows(mine.Count);
            for (int k = 0; k < mine.Count; k++)
                Add(army[mine[k]].Unit, army[mine[k]].Count, Side.Players, rows[k] * Width, mine[k]);
            Unit unit = Units.Of(monster);
            int stacks = Math.Min(4, count);
            rows = Rows(stacks);
            for (int k = 0; k < stacks; k++)
            {
                int share = count / stacks + (k < count % stacks ? 1 : 0);
                Add(unit, share, Side.Monsters, rows[k] * Width + Width - 1, -1);
            }
            monsterStart = Health(Side.Monsters);
            StartRound();
        }

        static int[] Rows(int stacks)
        {
            switch (stacks)
            {
                case 0: return new int[0];
                case 1: return new[] { 3 };
                case 2: return new[] { 2, 4 };
                case 3: return new[] { 1, 3, 5 };
                case 4: return new[] { 0, 2, 4, 6 };
                default: return new[] { 1, 2, 3, 4, 5 };
            }
        }

        void Add(Unit unit, int count, Side side, int cell, int source)
        {
            UnitStats stats = Units.Of(unit);
            Fighters.Add(new Fighter
            {
                Id = Fighters.Count, Unit = unit, Stats = stats, Side = side, Count = count, StartCount = count,
                Top = stats.Health, Cell = cell, Source = source,
            });
        }

        /// <summary>Trees in a forest, rocks on hills and mountains, a few bushes or stones elsewhere.</summary>
        void PlaceObstacles()
        {
            int count = Ground == Biome.Forest || Ground == Biome.Hills || Ground == Biome.Mountains
                ? rng.Range(2, 5)
                : rng.Range(0, 3);
            for (int placed = 0, tries = 0; placed < count && tries < 50; tries++)
            {
                int cell = rng.Range(1, Height - 1) * Width + rng.Range(3, Width - 3);
                if (Blocked[cell])
                    continue;
                Blocked[cell] = true;
                placed++;
            }
        }

        public Fighter At(int cell)
        {
            foreach (Fighter f in Fighters)
            {
                if (f.Alive && f.Cell == cell)
                    return f;
            }
            return null;
        }

        public bool Free(int cell) => !Blocked[cell] && At(cell) == null;

        public bool EnemyNextTo(Fighter f)
        {
            foreach (int n in Neighbours[f.Cell])
            {
                Fighter other = At(n);
                if (other != null && other.Side != f.Side)
                    return true;
            }
            return false;
        }

        public bool CanShoot(Fighter f) => f.Stats.Has(Ability.Shoots) && !EnemyNextTo(f);

        /// <summary>Steps from the fighter's cell to every cell over free cells; int.MaxValue where it cannot go.</summary>
        public int[] Steps(Fighter f, int[] previous = null)
        {
            var steps = new int[Width * Height];
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i] = int.MaxValue;
                if (previous != null)
                    previous[i] = -1;
            }
            var queue = new Queue<int>();
            steps[f.Cell] = 0;
            queue.Enqueue(f.Cell);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                foreach (int j in Neighbours[i])
                {
                    if (steps[j] != int.MaxValue || !Free(j))
                        continue;
                    steps[j] = steps[i] + 1;
                    if (previous != null)
                        previous[j] = i;
                    queue.Enqueue(j);
                }
            }
            return steps;
        }

        /// <summary>How far the fighter may walk this turn: its speed, nothing when caught in a web.</summary>
        public int Reach(Fighter f) => f.Stuck ? 0 : f.Stats.Speed;

        /// <summary>The cells the current stack can walk to this turn (its own cell included).</summary>
        public List<int> Reachable()
        {
            var cells = new List<int>();
            if (Current == null)
                return cells;
            int[] steps = Steps(Current);
            int reach = Reach(Current);
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] <= reach)
                    cells.Add(i);
            }
            return cells;
        }

        int AttackOf(Fighter f) => f.Stats.Attack + (f.Side == Side.Players ? Attack : 0);

        int DefenceOf(Fighter f)
        {
            int defence = f.Stats.Defence + (f.Side == Side.Players ? Defence : 0);
            return f.Defending ? (defence * 3 + 1) / 2 : defence;
        }

        /// <summary>Damage of <paramref name="a"/> on <paramref name="t"/> for a damage roll per unit.</summary>
        public float Damage(Fighter a, Fighter t, bool shot, float roll)
        {
            int attack = AttackOf(a), defence = DefenceOf(t);
            float factor = attack >= defence
                ? MathF.Min(3f, 1f + 0.1f * (attack - defence))
                : 1f / (1f + 0.05f * (defence - attack));
            float damage = a.Count * roll * factor;
            if (shot && Distance(a.Cell, t.Cell) > ShortRange)
                damage *= 0.5f;
            if (!shot && a.Stats.Has(Ability.Shoots))
                damage *= 0.5f;
            if (shot && t.Stats.Has(Ability.ArrowProof))
                damage *= 0.5f;
            return damage;
        }

        /// <summary>The mean damage, at most what the target can take.</summary>
        public float Expected(Fighter a, Fighter t, bool shot) =>
            MathF.Min(t.TotalHealth, Damage(a, t, shot, 0.5f * (a.Stats.MinDamage + a.Stats.MaxDamage)));

        BattleEvent Hit(BattleEventKind kind, Fighter a, Fighter t, bool shot)
        {
            float roll = rng.Range(a.Stats.MinDamage, a.Stats.MaxDamage + 1);
            int damage = Math.Max(1, (int)MathF.Round(Damage(a, t, shot, roll)));
            int before = t.Count;
            int left = t.TotalHealth - damage;
            if (left <= 0)
            {
                t.Count = 0;
                t.Top = 0;
            }
            else
            {
                int h = t.Stats.Health;
                t.Count = (left + h - 1) / h;
                t.Top = left - (t.Count - 1) * h;
            }
            int killed = before - t.Count;
            if (t.Side == Side.Monsters)
                Experience += killed * t.Stats.Health;
            var e = new BattleEvent { Kind = kind, Actor = a.Id, Target = t.Id, Damage = damage, Killed = killed };
            Events.Add(e);
            if (t.Alive && a.Stats.Has(Ability.Web) && !shot)
            {
                t.Webbed = true;
                Events.Add(new BattleEvent { Kind = BattleEventKind.Web, Actor = a.Id, Target = t.Id });
            }
            return e;
        }

        void WalkTo(Fighter f, int cell)
        {
            if (cell == f.Cell)
                return;
            var previous = new int[Width * Height];
            Steps(f, previous);
            var path = new List<int>();
            for (int i = cell; i != f.Cell; i = previous[i])
                path.Add(i);
            path.Reverse();
            f.Cell = cell;
            Events.Add(new BattleEvent { Kind = BattleEventKind.Walk, Actor = f.Id, Path = path });
        }

        bool CanReach(Fighter f, int cell)
        {
            if (cell == f.Cell)
                return true;
            if (!Free(cell))
                return false;
            return Steps(f)[cell] <= Reach(f);
        }

        /// <summary>The current stack walks to a free cell within its reach.</summary>
        public bool Walk(int cell)
        {
            Fighter f = Current;
            if (f == null || cell == f.Cell || !CanReach(f, cell))
                return false;
            WalkTo(f, cell);
            EndAction();
            return true;
        }

        /// <summary>The current stack walks to <paramref name="from"/> and strikes an enemy next to it.</summary>
        public bool Strike(Fighter target, int from)
        {
            Fighter f = Current;
            if (f == null || target == null || !target.Alive || target.Side == f.Side || !CanReach(f, from) ||
                Distance(from, target.Cell) != 1)
                return false;
            WalkTo(f, from);
            Hit(BattleEventKind.Strike, f, target, false);
            if (target.Alive && !target.StruckBack && !f.Stats.Has(Ability.NoStrikeBack))
            {
                target.StruckBack = true;
                Hit(BattleEventKind.StrikeBack, target, f, false);
            }
            EndAction();
            return true;
        }

        /// <summary>The current stack strikes the target from the reachable cell next to it nearest to <paramref name="near"/>.</summary>
        public bool Strike(Fighter target, float nearX = float.NaN, float nearZ = float.NaN)
        {
            int from = StrikeCell(target, nearX, nearZ);
            return from >= 0 && Strike(target, from);
        }

        /// <summary>
        /// The cell next to the target the current stack can strike from (its own when it already
        /// stands there), the nearest to a point in battlefield units, or the closest walk; -1.
        /// </summary>
        public int StrikeCell(Fighter target, float nearX = float.NaN, float nearZ = float.NaN)
        {
            Fighter f = Current;
            if (f == null || target == null || target.Side == f.Side)
                return -1;
            if (Distance(f.Cell, target.Cell) == 1)
                return f.Cell;
            int[] steps = Steps(f);
            int best = -1;
            float score = float.MaxValue;
            foreach (int n in Neighbours[target.Cell])
            {
                if (!Free(n) || steps[n] > Reach(f))
                    continue;
                float s = float.IsNaN(nearX)
                    ? steps[n]
                    : Sq(HexMetrics.CenterX(Hexes[n]) - nearX) + Sq(HexMetrics.CenterZ(Hexes[n]) - nearZ);
                if (s < score)
                {
                    score = s;
                    best = n;
                }
            }
            return best;
        }

        static float Sq(float x) => x * x;

        public bool Shoot(Fighter target)
        {
            Fighter f = Current;
            if (f == null || target == null || !target.Alive || target.Side == f.Side || !CanShoot(f))
                return false;
            Hit(BattleEventKind.Shoot, f, target, true);
            EndAction();
            return true;
        }

        public void Defend()
        {
            if (Current == null)
                return;
            Current.Defending = true;
            Events.Add(new BattleEvent { Kind = BattleEventKind.Defend, Actor = Current.Id });
            EndAction();
        }

        /// <summary>
        /// The players give up the field: a loss, but not of everything. The hero leads the living
        /// away and each stack leaves a quarter of them behind (the fun plan of 2026-11-11: a
        /// loss should slow the players down, not take the whole army, so that a dangerous fight
        /// is worth trying).
        /// </summary>
        public void Retreat()
        {
            foreach (Fighter f in Fighters)
            {
                if (f.Side == Side.Players && f.Alive)
                    f.Count -= RetreatLoss(f.Count);
            }
            Retreated = true;
            Outcome = Outcome.Lost;
            Current = null;
        }

        /// <summary>The units a stack of this many leaves behind in a retreat: a quarter, rounded.</summary>
        public static int RetreatLoss(int count) => (count + 2) / 4;

        /// <summary>
        /// The auto battle. A monster goes for the stack its kind's temper picks (see
        /// <see cref="MonsterTurn"/>). The players' stacks, when the players let the auto battle
        /// fight for them, shoot the stack they would hurt most, strike the stack they can reach
        /// and hurt most, shooters first, or walk towards the nearest enemy.
        /// </summary>
        public void Auto()
        {
            Fighter f = Current;
            if (f == null)
                return;
            Temper temper = f.Side == Side.Monsters ? Units.TemperOf(f.Unit) : Temper.None;
            if (temper != Temper.None)
            {
                MonsterTurn(f, temper);
                return;
            }
            Fighter best = null;
            float score = -1;
            if (CanShoot(f))
            {
                foreach (Fighter t in Fighters)
                {
                    if (!t.Alive || t.Side == f.Side)
                        continue;
                    float s = Expected(f, t, true) * Priority(t);
                    if (s > score)
                    {
                        score = s;
                        best = t;
                    }
                }
                if (best != null)
                {
                    Shoot(best);
                    return;
                }
            }
            int[] steps = Steps(f);
            int reach = Reach(f), from = -1;
            foreach (Fighter t in Fighters)
            {
                if (!t.Alive || t.Side == f.Side)
                    continue;
                foreach (int n in Neighbours[t.Cell])
                {
                    if (n != f.Cell && (!Free(n) || steps[n] > reach))
                        continue;
                    float s = Expected(f, t, false) * Priority(t) - 0.01f * (n == f.Cell ? 0 : steps[n]);
                    if (s > score)
                    {
                        score = s;
                        best = t;
                        from = n;
                    }
                }
            }
            if (best != null)
            {
                Strike(best, from);
                return;
            }
            int towards = Approach(f, steps, reach);
            if (towards >= 0 && towards != f.Cell)
                Walk(towards);
            else
                Defend();
        }

        static float Priority(Fighter t) => t.Stats.Has(Ability.Shoots) ? 1.5f : 1f;

        /// <summary>
        /// A monster's turn by its kind's temper (the battle plan of 2026-11-10: the players found
        /// every fight the same, the monsters always running at the archers). A shooter with no
        /// enemy beside it shoots the stack its temper picks among all; the others strike the one
        /// it picks among those they can reach this turn, or else walk towards the one it picks
        /// among those they can get to at all, or else defend. Between stacks the temper likes
        /// as much, the nearer goes first, then the one it would hurt more.
        /// </summary>
        void MonsterTurn(Fighter f, Temper temper)
        {
            if (CanShoot(f))
            {
                Fighter mark = Pick(f, temper, null, 0, true, out _);
                if (mark != null && Shoot(mark))
                    return;
            }
            var previous = new int[Width * Height];
            int[] steps = Steps(f, previous);
            int reach = Reach(f);
            Fighter target = Pick(f, temper, steps, reach, false, out int from);
            if (target != null && Strike(target, from))
                return;
            target = Pick(f, temper, steps, int.MaxValue - 1, false, out int goal);
            if (target != null && reach > 0)
            {
                int cell = goal;
                while (steps[cell] > reach)
                    cell = previous[cell];
                if (Walk(cell))
                    return;
            }
            Defend();
        }

        /// <summary>
        /// The enemy the temper picks among those the fighter can shoot or, within
        /// <paramref name="limit"/> steps, get next to; <paramref name="cell"/> is the free cell
        /// beside it fewest steps away (the fighter's own when it already stands beside it), -1
        /// for a shot. Null when there is none.
        /// </summary>
        Fighter Pick(Fighter f, Temper temper, int[] steps, int limit, bool shot, out int cell)
        {
            cell = -1;
            Fighter best = null;
            (float Want, int Near, float Harm) bestKey = default;
            HashSet<int> mobbed = temper == Temper.Mob ? Mobbed(f) : null;
            foreach (Fighter t in Fighters)
            {
                if (!t.Alive || t.Side == f.Side)
                    continue;
                int beside = -1, away = int.MaxValue;
                if (shot)
                    away = Distance(f.Cell, t.Cell);
                else if (Distance(f.Cell, t.Cell) == 1)
                {
                    beside = f.Cell;
                    away = 0;
                }
                else
                {
                    foreach (int n in Neighbours[t.Cell])
                    {
                        if (Free(n) && steps[n] <= limit && steps[n] < away)
                        {
                            beside = n;
                            away = steps[n];
                        }
                    }
                    if (beside < 0)
                        continue;
                }
                var key = (Want(f, t, temper, mobbed), -away, Expected(f, t, shot));
                if (best == null || key.CompareTo(bestKey) > 0)
                {
                    best = t;
                    bestKey = key;
                    cell = beside;
                }
            }
            return best;
        }

        /// <summary>How much the temper wants the target, nearness aside: the more, the sooner.</summary>
        static float Want(Fighter f, Fighter t, Temper temper, HashSet<int> mobbed)
        {
            switch (temper)
            {
                case Temper.Mob:
                    return mobbed.Contains(t.Id) ? 1 : 0;
                case Temper.Charge:
                    return -Math.Abs(Hexes[t.Cell].Row - Hexes[f.Cell].Row);
                case Temper.Weakest:
                    return -t.TotalHealth;
                case Temper.Wall:
                    return t.Stats.Has(Ability.Shoots) ? 0 : 1;
                case Temper.Fastest:
                    return (t.Webbed ? 0 : 100) + t.Stats.Speed;
                case Temper.Biggest:
                    return t.TotalHealth;
                default:
                    return 0; // Nearest: nearness alone.
            }
        }

        /// <summary>The enemies other stacks of the fighter's kind have struck this round.</summary>
        HashSet<int> Mobbed(Fighter f)
        {
            var mobbed = new HashSet<int>();
            for (int i = Events.Count - 1; i >= 0 && Events[i].Kind != BattleEventKind.Round; i--)
            {
                BattleEvent e = Events[i];
                if (e.Kind == BattleEventKind.Strike && e.Actor != f.Id && Fighters[e.Actor].Unit == f.Unit)
                    mobbed.Add(e.Target);
            }
            return mobbed;
        }

        /// <summary>The reachable cell on the way to the nearest free cell beside an enemy, or -1.</summary>
        int Approach(Fighter f, int[] steps, int reach)
        {
            int goal = -1, nearest = int.MaxValue;
            foreach (Fighter t in Fighters)
            {
                if (!t.Alive || t.Side == f.Side)
                    continue;
                foreach (int n in Neighbours[t.Cell])
                {
                    if (Free(n) && steps[n] < nearest)
                    {
                        nearest = steps[n];
                        goal = n;
                    }
                }
            }
            if (goal < 0 || reach == 0)
                return -1;
            var previous = new int[Width * Height];
            Steps(f, previous);
            int cell = goal;
            while (cell >= 0 && steps[cell] > reach)
                cell = previous[cell];
            return cell;
        }

        void StartRound()
        {
            Round++;
            Events.Add(new BattleEvent { Kind = BattleEventKind.Round });
            order.Clear();
            foreach (Fighter f in Fighters)
            {
                if (!f.Alive)
                    continue;
                f.StruckBack = false;
                if (f.Stats.Has(Ability.Regenerates) && f.Top < f.Stats.Health)
                {
                    int healed = Math.Min(f.Stats.Health - f.Top, f.Stats.Health / Units.HealShare);
                    f.Top += healed;
                    Events.Add(new BattleEvent { Kind = BattleEventKind.Heal, Actor = f.Id, Damage = healed });
                }
                order.Add(f);
            }
            order.Sort(Faster);
            next = 0;
            Advance();
        }

        /// <summary>The fastest first, the players' stacks first at equal speed, then in their order.</summary>
        static int Faster(Fighter a, Fighter b) =>
            a.Stats.Speed != b.Stats.Speed ? b.Stats.Speed.CompareTo(a.Stats.Speed)
            : a.Side != b.Side ? a.Side.CompareTo(b.Side) : a.Id.CompareTo(b.Id);

        /// <summary>The order the stacks alive now would act in next round.</summary>
        public List<Fighter> NextRound()
        {
            var queue = new List<Fighter>();
            foreach (Fighter f in Fighters)
            {
                if (f.Alive)
                    queue.Add(f);
            }
            queue.Sort(Faster);
            return queue;
        }

        /// <summary>The stacks still to act this round, the current one first.</summary>
        public List<Fighter> Queue()
        {
            var queue = new List<Fighter>();
            for (int i = Math.Max(0, next - 1); i < order.Count; i++)
            {
                if (order[i].Alive)
                    queue.Add(order[i]);
            }
            return queue;
        }

        void Advance()
        {
            while (next < order.Count && !order[next].Alive)
                next++;
            if (next >= order.Count)
            {
                Current = null;
                return;
            }
            Current = order[next++];
            Current.Defending = false;
            Current.Stuck = Current.Webbed;
            Current.Webbed = false;
        }

        void EndAction()
        {
            // Acting on is turning an offer down.
            Offered = false;
            if (Current != null)
                Current.Stuck = false;
            if (!SideAlive(Side.Monsters))
                Finish(Outcome.Won);
            else if (!SideAlive(Side.Players))
                Finish(Outcome.Lost);
            if (Outcome != Outcome.Running)
                return;
            Advance();
            if (Current == null)
            {
                if (Round >= MaxRounds)
                    Finish(Outcome.Lost);
                else
                    StartRound();
            }
            if (Outcome == Outcome.Running)
                MaybeOffer();
        }

        /// <summary>
        /// The first time the monsters are down to a third of their health and the players' stacks
        /// have twice theirs, the monsters' kind rolls once (<see cref="Units.YieldChance"/>) to
        /// offer to give up; the fight waits for the players' answer.
        /// </summary>
        void MaybeOffer()
        {
            if (yieldRolled)
                return;
            int theirs = Health(Side.Monsters);
            if (theirs > monsterStart * YieldBelow || Health(Side.Players) < theirs * YieldOdds)
                return;
            yieldRolled = true;
            Fighter biggest = null;
            foreach (Fighter f in Fighters)
            {
                if (f.Side == Side.Monsters && f.Alive && (biggest == null || f.Count > biggest.Count))
                    biggest = f;
            }
            if (biggest == null || !yieldRng.Chance(Units.YieldChance(biggest.Unit)))
                return;
            Offered = true;
            Events.Add(new BattleEvent { Kind = BattleEventKind.Surrender, Actor = biggest.Id });
        }

        /// <summary>The players let the monsters go: the fight is won, and the monsters left run away.</summary>
        public void Accept()
        {
            if (!Offered || Outcome != Outcome.Running)
                return;
            Offered = false;
            Surrendered = true;
            Finish(Outcome.Won);
        }

        /// <summary>The players turn the offer down and fight on.</summary>
        public void FightOn() => Offered = false;

        /// <summary>Total health of a side's living stacks.</summary>
        int Health(Side side)
        {
            int health = 0;
            foreach (Fighter f in Fighters)
            {
                if (f.Side == side)
                    health += f.TotalHealth;
            }
            return health;
        }

        void Finish(Outcome outcome)
        {
            Outcome = outcome;
            Current = null;
        }

        bool SideAlive(Side side)
        {
            foreach (Fighter f in Fighters)
            {
                if (f.Alive && f.Side == side)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Plays the rest of the fight with the auto battle on both sides; an offer to give up is
        /// taken when <paramref name="accept"/> (the quick battle and the odds), else turned down.
        /// </summary>
        public void AutoToEnd(bool accept = true)
        {
            while (Outcome == Outcome.Running)
            {
                if (Offered && accept)
                    Accept();
                else
                    Auto();
            }
        }

        /// <summary>Writes the surviving counts back into the hero's stacks and drops the empty ones; a lost fight, not a retreat, leaves none.</summary>
        public void Survivors(List<Stack> army)
        {
            foreach (Fighter f in Fighters)
            {
                if (f.Side == Side.Players && f.Source >= 0 && f.Source < army.Count)
                    army[f.Source].Count = f.Count;
            }
            if (Outcome == Outcome.Lost && !Retreated)
                army.Clear();
            army.RemoveAll(s => s.Count <= 0);
        }

        /// <summary>Monsters left alive.</summary>
        public int MonstersLeft()
        {
            int left = 0;
            foreach (Fighter f in Fighters)
            {
                if (f.Side == Side.Monsters)
                    left += f.Count;
            }
            return left;
        }
    }

    /// <summary>The chance to win a fight and the expected losses, from many auto battles.</summary>
    public readonly struct Odds
    {
        public readonly float Win;

        /// <summary>Mean units lost per stack of the hero's army.</summary>
        public readonly float[] Losses;

        public Odds(float win, float[] losses)
        {
            Win = win;
            Losses = losses;
        }

        public const float Easy = 0.8f, Even = 0.4f;

        /// <summary>
        /// How many times its reward a won fight pays, by its odds before it: an easy fight once,
        /// an even one twice, a dangerous one three times (the fun plan of 2026-11-11: the reward
        /// was smaller than the losses, so only easy fights were worth taking).
        /// </summary>
        public static int RewardFactor(float win) => win >= Easy ? 1 : win >= Even ? 2 : 3;

        public static Odds Of(IReadOnlyList<Stack> army, int attack, int defence, Monster monster, int count,
            Biome ground, int seed, int runs = 200)
        {
            var rng = new Rng(seed);
            int won = 0;
            var losses = new float[army.Count];
            for (int run = 0; run < runs; run++)
            {
                var battle = new Battle(army, attack, defence, monster, count, ground, rng.NextInt());
                battle.AutoToEnd();
                if (battle.Outcome == Outcome.Won)
                    won++;
                foreach (Fighter f in battle.Fighters)
                {
                    if (f.Side == Side.Players && f.Source >= 0)
                        losses[f.Source] += f.StartCount - f.Count;
                }
            }
            for (int i = 0; i < losses.Length; i++)
                losses[i] /= runs;
            return new Odds((float)won / runs, losses);
        }
    }
}
