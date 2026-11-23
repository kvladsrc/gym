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

        /// <summary>Its spears' cover (<see cref="Ability.Cover"/>) is spent this round.</summary>
        public bool Covered;

        /// <summary>
        /// The stack a troll told it goes for (the combat plan of 2026-11-11, 3.1): kept until
        /// that stack falls, shown by an arrow on the field; -1 for the others.
        /// </summary>
        public int Aim = -1;

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

        /// <summary>
        /// Spearmen (the actor) struck the stack about to strike them or their neighbour (the
        /// target) first: their cover (<see cref="Ability.Cover"/>). The strike follows unless
        /// the target fell.
        /// </summary>
        Cover,

        /// <summary>
        /// The hero gave an order (<see cref="BattleEvent.Order"/>, <see cref="Battle.GiveOrder"/>);
        /// the actor is the players' stack whose turn it is.
        /// </summary>
        Order,

        /// <summary>
        /// The players' stack (the actor) stood on the chest's cell and took it
        /// (<see cref="FieldEvent.Loot"/>); <see cref="BattleEvent.Damage"/> is its gold.
        /// </summary>
        Chest,
    }

    /// <summary>
    /// What the hero may order once a battle, in the turn of any of the players' stacks (the
    /// combat plan of 2026-11-11, 3.4; the players chose one order a battle to begin with).
    /// </summary>
    public enum HeroOrder : byte
    {
        /// <summary>«В атаку!»: the stack whose turn it is acts once more after its action.</summary>
        Attack,

        /// <summary>«Щиты!»: every stack of the players defends until its next turn; the turn is not spent.</summary>
        Shields,
    }

    /// <summary>
    /// The field's pattern by the ground of the fight (the combat plan of 2026-11-11, 3.5, cut
    /// to three by the fun plan): the same trees and stones in a shape the players learn to read.
    /// </summary>
    public enum FieldShape : byte
    {
        /// <summary>«Чистое поле»: meadows, plains and beaches; up to two stones.</summary>
        Open,

        /// <summary>«Роща»: forests; two clumps of two or three trees with three ways past them.</summary>
        Grove,

        /// <summary>«Теснина»: hills and mountains; a wall of stones across the middle with one or two gaps.</summary>
        Gorge,
    }

    /// <summary>
    /// What happens on the field of about one fight in four, told before it (the combat plan's
    /// 3.5; the players found events before a fight interesting, 2026-11-11).
    /// </summary>
    public enum FieldEvent : byte
    {
        None,

        /// <summary>«Засада»: the monsters start in columns 6-8, not at the far edge.</summary>
        Ambush,

        /// <summary>«Добыча»: a chest lies on the field; the players' stack that stands on it takes its gold.</summary>
        Loot,
    }

    public sealed class BattleEvent
    {
        public BattleEventKind Kind;
        public int Actor = -1, Target = -1, Damage, Killed;
        public List<int> Path;

        /// <summary>A strike with a run-up (<see cref="Ability.RunUp"/>), half as strong again.</summary>
        public bool RunUp;

        /// <summary>The order of a <see cref="BattleEventKind.Order"/>.</summary>
        public HeroOrder Order;
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

        /// <summary>
        /// A strike from a cell at least this many cells (as the crow flies, not along the way)
        /// from where the striker began its turn is a run-up (<see cref="Ability.RunUp"/>).
        /// </summary>
        public const int RunUpCells = 3;

        /// <summary>How much stronger a strike with a run-up is.</summary>
        public const float RunUpFactor = 1.5f;

        /// <summary>
        /// Wolves circle out of reach of the players' foot stacks while they cannot get at their
        /// mark (the combat plan's 3.1) in the first this many rounds; then they go straight in,
        /// so that a fight cannot stall.
        /// </summary>
        public const int CircleRounds = 3;

        public readonly List<Fighter> Fighters = new List<Fighter>();
        public readonly bool[] Blocked = new bool[Width * Height];
        public readonly int Attack, Defence;
        public readonly Biome Ground;
        public readonly List<BattleEvent> Events = new List<BattleEvent>();

        /// <summary>
        /// The fields are laid by their patterns (<see cref="Shape"/>), on since 2026-11-12: their
        /// odds came within 12 points of the scattered obstacles' on the same ground, inside the
        /// combat plan's 15. Off, the obstacles are scattered as before, which the measurer
        /// (<c>FieldReportTable</c>) compares against.
        /// </summary>
        public static bool Patterns = true;

        /// <summary>
        /// The shares of fights with an ambush and with a chest (<see cref="GameState.FieldEventOf"/>):
        /// one fight in four has one of them, as the combat plan's 3.5 asks. The odds shown before
        /// a fight count its event, so an ambush that makes a fight harder pays more.
        /// </summary>
        public static readonly float AmbushShare = 0.125f, LootShare = 0.125f;

        /// <summary>
        /// «За укрытием» (the combat plan's 3.5): a shot over an obstacle does
        /// <see cref="ShelterFactor"/> of its damage. The plan brings it in last of the field's
        /// rules, and takes it out if the boy cannot see why a shot was weaker.
        /// </summary>
        public static bool ShelterRule = false;

        public const float ShelterFactor = 0.5f;

        /// <summary>The field's pattern, by the ground (<see cref="ShapeOf"/>).</summary>
        public readonly FieldShape Shape;

        /// <summary>What happens on this field, told before the fight.</summary>
        public readonly FieldEvent FieldEvent;

        /// <summary>The cell the chest lies on (<see cref="FieldEvent.Loot"/>) until the players take it; -1 otherwise.</summary>
        public int Chest { get; private set; } = -1;

        /// <summary>The chest's gold, set by the game when the fight starts; paid when the fight ends if the players took it.</summary>
        public int ChestGold;

        /// <summary>A players' stack took the chest.</summary>
        public bool ChestTaken { get; private set; }

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

        /// <summary>
        /// How many orders the hero gives in a battle (the players chose one to begin with,
        /// 2026-11-11). It came a build after the run-up and the habits: the plan brings in one
        /// new rule for the players a build.
        /// </summary>
        public const int OrdersPerBattle = 1;

        /// <summary>The orders the hero has left in this battle.</summary>
        public int OrdersLeft = OrdersPerBattle;

        /// <summary>The round the hero's last order was given in, 0 for none yet.</summary>
        public int OrderRound { get; private set; }

        /// <summary>The stack told «В атаку!»: it acts once more after this action.</summary>
        Fighter again;

        /// <summary>
        /// How strong a stack strikes or shoots in the action «В атаку!» gives it. A whole second
        /// action moved the measured auto battles' odds by 15-23 points in even fights; the
        /// combat plan's 3.4 wants ten at most.
        /// </summary>
        public const float AgainFactor = 0.5f;

        /// <summary>The stack acting now in the action «В атаку!» gave it, or null.</summary>
        Fighter extra;

        /// <summary>Whether the stack acts now in the action «В атаку!» gave it, at <see cref="AgainFactor"/> of its strength.</summary>
        public bool ActsAgain(Fighter f) => f != null && f == extra;

        float AgainOf(Fighter a) => a == extra ? AgainFactor : 1f;

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
        /// <param name="fieldEvent">What happens on the field, told before the fight.</param>
        public Battle(IReadOnlyList<Stack> army, int attack, int defence, Monster monster, int count, Biome ground, int seed,
            FieldEvent fieldEvent = FieldEvent.None)
        {
            Attack = attack;
            Defence = defence;
            Ground = ground;
            Shape = ShapeOf(ground);
            FieldEvent = fieldEvent;
            rng = new Rng(seed);
            yieldRng = new Rng(seed ^ 0x5A17B3C9);
            // The field has its own sequence, so that its pattern does not change the fight's rolls.
            var layout = new Rng(seed ^ 0x3C6EF372);
            PlaceObstacles(layout);
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
                int cell = fieldEvent == FieldEvent.Ambush ? AmbushCell(rows[k], layout) : rows[k] * Width + Width - 1;
                Add(unit, share, Side.Monsters, cell, -1);
            }
            if (fieldEvent == FieldEvent.Loot)
                Chest = ChestCell(layout);
            monsterStart = Health(Side.Monsters);
            DeclareAims();
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

        /// <summary>The field's pattern for the ground: a grove in forests, a gorge on hills and mountains, an open field elsewhere.</summary>
        public static FieldShape ShapeOf(Biome ground) =>
            ground == Biome.Forest ? FieldShape.Grove
            : ground == Biome.Hills || ground == Biome.Mountains ? FieldShape.Gorge
            : FieldShape.Open;

        /// <summary>
        /// The obstacles by the field's pattern (the combat plan of 2026-11-11, 3.5): only in
        /// columns 2-8, so that both sides start free, and always with a way from the left edge to
        /// the right; the grove and the gorge are turned upside down on half the fields. Without
        /// the patterns, two to four in a forest or on hills and mountains and up to two elsewhere,
        /// scattered over the middle.
        /// </summary>
        void PlaceObstacles(Rng layout)
        {
            if (Patterns && Shape == FieldShape.Grove)
            {
                Clump(layout, 1, 2, 3, 6);
                Clump(layout, 4, 5, 4, 7);
            }
            else if (Patterns && Shape == FieldShape.Gorge)
                Wall(layout);
            else
            {
                int count = Patterns || Shape == FieldShape.Open ? layout.Range(0, 3) : layout.Range(2, 5);
                for (int placed = 0, tries = 0; placed < count && tries < 50; tries++)
                {
                    int cell = layout.Range(1, Height - 1) * Width + layout.Range(3, Width - 3);
                    if (Blocked[cell])
                        continue;
                    Blocked[cell] = true;
                    placed++;
                }
            }
            if (Patterns && Shape != FieldShape.Open && layout.Chance(0.5f))
                FlipRows();
        }

        /// <summary>
        /// A clump of two trees side by side within rows <paramref name="top"/>-<paramref name="bottom"/>,
        /// begun in columns <paramref name="from"/>-<paramref name="to"/>. The grove's two clumps
        /// keep to rows 1-2 and 4-5, so rows 0, 3 and 6 stay free: the grove's three ways past.
        /// </summary>
        void Clump(Rng layout, int top, int bottom, int from, int to)
        {
            int cell = layout.Range(top, bottom + 1) * Width + layout.Range(from, to + 1);
            Blocked[cell] = true;
            var around = new List<int>();
            foreach (int n in Neighbours[cell])
            {
                int row = n / Width, col = n % Width;
                if (row >= top && row <= bottom && col >= 2 && col <= 8)
                    around.Add(n);
            }
            if (around.Count > 0)
                Blocked[around[layout.Range(0, around.Count)]] = true;
        }

        /// <summary>
        /// A wall of stones across the field in columns 4-6, each stone touching the one in the
        /// next row so that nothing slips between them, with one or two gaps apart from each other.
        /// </summary>
        void Wall(Rng layout)
        {
            var cols = new int[Height];
            cols[1] = layout.Range(4, 7);
            for (int r = 2; r < Height - 1; r++)
            {
                // The two cells of this row touching the stone above: odd rows lie half a cell to the right.
                int a = (r - 1) % 2 == 0 ? cols[r - 1] - 1 : cols[r - 1], b = a + 1;
                bool aFits = a >= 4 && a <= 6, bFits = b >= 4 && b <= 6;
                cols[r] = aFits && bFits ? (layout.Chance(0.5f) ? a : b) : aFits ? a : b;
            }
            // Two gaps apart from each other in rows 1-5, the edges open: four ways through.
            int gap = layout.Range(1, Height - 1), other = gap <= 2 ? layout.Range(gap + 2, Height - 1) : layout.Range(1, gap - 1);
            for (int r = 1; r < Height - 1; r++)
            {
                if (r != gap && r != other)
                    Blocked[r * Width + cols[r]] = true;
            }
        }

        /// <summary>The field upside down: the middle row stays, and rows keep their half-cell shift, so stones that touched still touch.</summary>
        void FlipRows()
        {
            for (int r = 0; r < Height / 2; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    int a = r * Width + c, b = (Height - 1 - r) * Width + c;
                    (Blocked[a], Blocked[b]) = (Blocked[b], Blocked[a]);
                }
            }
        }

        /// <summary>A free cell of the row in columns 6-8 for a monster stack of an ambush, else its usual cell at the far edge.</summary>
        int AmbushCell(int row, Rng layout)
        {
            int start = layout.Range(0, 3);
            for (int k = 0; k < 3; k++)
            {
                int cell = row * Width + 6 + (start + k) % 3;
                if (Free(cell))
                    return cell;
            }
            return row * Width + Width - 1;
        }

        /// <summary>A free cell in columns 4-6, rows 1-5, for the chest; -1 when there is none.</summary>
        int ChestCell(Rng layout)
        {
            var free = new List<int>();
            for (int r = 1; r < Height - 1; r++)
            {
                for (int c = 4; c <= 6; c++)
                {
                    if (Free(r * Width + c))
                        free.Add(r * Width + c);
                }
            }
            return free.Count == 0 ? -1 : free[layout.Range(0, free.Count)];
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

        public bool EnemyNextTo(Fighter f) => EnemyNextTo(f.Cell, f.Side);

        /// <summary>Whether a stack of <paramref name="side"/> on the cell would have an enemy beside it.</summary>
        public bool EnemyNextTo(int cell, Side side)
        {
            foreach (int n in Neighbours[cell])
            {
                Fighter other = At(n);
                if (other != null && other.Side != side)
                    return true;
            }
            return false;
        }

        /// <summary>How far the nearest living enemy of <paramref name="side"/> stands from the cell.</summary>
        int NearestEnemy(int cell, Side side)
        {
            int near = int.MaxValue;
            foreach (Fighter g in Fighters)
            {
                if (g.Alive && g.Side != side)
                    near = Math.Min(near, Distance(cell, g.Cell));
            }
            return near;
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
            if (shot && ShelterRule && Sheltered(a.Cell, t.Cell))
                damage *= ShelterFactor;
            return damage;
        }

        /// <summary>
        /// Whether a shot between the two cells goes over an obstacle: the line between their
        /// middles, followed in small steps, crosses a blocked cell. It is followed from the lower
        /// cell to the higher whichever way the shot goes, so that it is the same both ways.
        /// </summary>
        public bool Sheltered(int from, int to)
        {
            int lo = Math.Min(from, to), hi = Math.Max(from, to);
            Hex a = Hexes[lo], b = Hexes[hi];
            int n = 4 * Hex.Distance(a, b);
            for (int k = 1; k < n; k++)
            {
                float t = (float)k / n;
                int cell = IndexOf(Hex.Round(a.Q + (b.Q - a.Q) * t, a.R + (b.R - a.R) * t));
                if (cell >= 0 && cell != lo && cell != hi && Blocked[cell])
                    return true;
            }
            return false;
        }

        /// <summary>The mean damage, at most what the target can take.</summary>
        public float Expected(Fighter a, Fighter t, bool shot) =>
            MathF.Min(t.TotalHealth, AgainOf(a) * Damage(a, t, shot, 0.5f * (a.Stats.MinDamage + a.Stats.MaxDamage)));

        /// <summary>
        /// The mean damage of <paramref name="a"/> walking from where it stands to
        /// <paramref name="from"/> and striking <paramref name="t"/> there: with a run-up
        /// (<see cref="Ability.RunUp"/>) half as strong again, unless the spears' cover meets it
        /// there. At most what the target can take.
        /// </summary>
        public float StrikeExpected(Fighter a, Fighter t, int from)
        {
            float damage = AgainOf(a) * Damage(a, t, false, 0.5f * (a.Stats.MinDamage + a.Stats.MaxDamage));
            if (RunsUp(a, a.Cell, from) && Guard(a, t, from) == null)
                damage *= RunUpFactor;
            return MathF.Min(t.TotalHealth, damage);
        }

        /// <summary>
        /// A strike of the stack from <paramref name="from"/> is a run-up when the stack has one
        /// (<see cref="Ability.RunUp"/>) and began its turn at <paramref name="start"/>, at least
        /// <see cref="RunUpCells"/> cells away: going round in a circle does not count.
        /// </summary>
        public static bool RunsUp(Fighter f, int start, int from) =>
            f.Stats.Has(Ability.RunUp) && Distance(start, from) >= RunUpCells;

        /// <summary>Whether the current stack's strike at the target from the cell would be a run-up, the spears' cover not meeting it there.</summary>
        public bool RunUpAt(Fighter target, int from)
        {
            Fighter f = Current;
            return f != null && target != null && from >= 0 && RunsUp(f, f.Cell, from) && Guard(f, target, from) == null;
        }

        BattleEvent Hit(BattleEventKind kind, Fighter a, Fighter t, bool shot, float factor = 1f)
        {
            float roll = rng.Range(a.Stats.MinDamage, a.Stats.MaxDamage + 1);
            int damage = Math.Max(1, (int)MathF.Round(factor * AgainOf(a) * Damage(a, t, shot, roll)));
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
            // The chest goes to the players' stack that stands on it, once; monsters leave it.
            if (cell == Chest && f.Side == Side.Players)
            {
                Chest = -1;
                ChestTaken = true;
                Events.Add(new BattleEvent { Kind = BattleEventKind.Chest, Actor = f.Id, Damage = ChestGold });
            }
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
            int start = f.Cell;
            WalkTo(f, from);
            // The spears' cover (the combat plan's 3.3): spearmen beside the striker, struck or
            // beside the stack it strikes, strike it first, and it strikes with those it has left.
            // When they are struck themselves, striking first takes the place of striking back.
            Fighter guard = Guard(f, target, from);
            if (guard != null)
            {
                guard.Covered = true;
                Hit(BattleEventKind.Cover, guard, f, false);
                if (!f.Alive)
                {
                    EndAction();
                    return true;
                }
            }
            // A run-up (the same plan's «Разбег»): from three cells away or more, half as strong
            // again, unless the spears met the striker.
            bool runUp = guard == null && RunsUp(f, start, from);
            Hit(BattleEventKind.Strike, f, target, false, runUp ? RunUpFactor : 1f).RunUp = runUp;
            if (target.Alive && guard != target && !target.StruckBack && !f.Stats.Has(Ability.NoStrikeBack))
            {
                target.StruckBack = true;
                Hit(BattleEventKind.StrikeBack, target, f, false);
            }
            EndAction();
            return true;
        }

        /// <summary>
        /// The spearmen that would strike <paramref name="striker"/> first if it struck
        /// <paramref name="target"/> from <paramref name="from"/> (<see cref="Ability.Cover"/>):
        /// a stack of the target's side with its cover unspent this round, standing beside
        /// <paramref name="from"/>, that is the target or stands beside it. The target itself
        /// first, else the one that would hurt the striker most; null when nobody covers it there.
        /// Shots go through: only strikes ask.
        /// </summary>
        public Fighter Guard(Fighter striker, Fighter target, int from)
        {
            if (target.Alive && target.Stats.Has(Ability.Cover) && !target.Covered && Distance(from, target.Cell) == 1)
                return target;
            Fighter best = null;
            float most = -1f;
            foreach (int n in Neighbours[from])
            {
                Fighter g = At(n);
                if (g == null || g.Side != target.Side || g.Covered || !g.Stats.Has(Ability.Cover) ||
                    Distance(g.Cell, target.Cell) != 1)
                    continue;
                float harm = Expected(g, striker, false);
                if (harm > most)
                {
                    most = harm;
                    best = g;
                }
            }
            return best;
        }

        /// <summary>The share of the striker's health the spears' cover would take if it struck the target from the cell; 0 when nobody covers it there.</summary>
        public float CoverLoss(Fighter striker, Fighter target, int from)
        {
            Fighter guard = Guard(striker, target, from);
            return guard == null || striker.TotalHealth <= 0 ? 0f : Expected(guard, striker, false) / striker.TotalHealth;
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
        /// and hurt most, shooters first (from a cell that gives a run-up, when the stack has one),
        /// or walk towards the nearest enemy.
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
                    if (StepUp(f))
                        return;
                    OrderBeforeBlow(f);
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
                    float s = StrikeExpected(f, t, n) * Priority(t) - 0.01f * (n == f.Cell ? 0 : steps[n]);
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
                OrderBeforeBlow(f);
                Strike(best, from);
                return;
            }
            // With nothing to strike within reach, a players' stack takes the chest when it can get
            // there this turn, no enemy stands beside it and it lies about on the way: at most one
            // step farther from the enemies than where the stack would walk anyway. Any detour cost
            // the start army a fifth of its fights with bandits, whose shots punish every lost step;
            // strictly on the way, the auto took the chest in one fight in six, one step off in
            // two in five, at no more than four points of the odds.
            int towards = Approach(f, steps, reach);
            if (f.Side == Side.Players && Chest >= 0 && steps[Chest] <= reach && !EnemyNextTo(Chest, f.Side)
                && (towards < 0 || NearestEnemy(Chest, f.Side) <= NearestEnemy(towards, f.Side) + 1))
            {
                Walk(Chest);
                return;
            }
            if (towards >= 0 && towards != f.Cell)
                Walk(towards);
            else
                Defend();
        }

        static float Priority(Fighter t) => t.Stats.Has(Ability.Shoots) ? 1.5f : 1f;

        /// <summary>
        /// A shooter of the players' auto battle with every enemy beyond <see cref="ShortRange"/>
        /// walks to a cell within it rather than shoot at half. It steps up only to a cell that no
        /// enemy striking in melee could reach next turn and that has no enemy beside it, and stays
        /// further from the enemy than its own stacks that strike in melee, so these are still the
        /// bandits' nearest mark; of such cells, the one furthest from the enemy, then the nearest
        /// to walk to. False when there is none.
        /// The archers had stood at the field's edge all fight, so a fight of the first army with
        /// bandits at six steps ran 10.7 rounds on average, the last five of them the archers and
        /// the bandits left trading half-strength shots across the field; the fun plan wants at
        /// most 5 % of fights longer than eight rounds. Measured over the auto battles on
        /// 2026-11-12, that fight now runs 9.2 rounds and is won 60 % of the time against 52 %, and
        /// the share of fights over eight rounds fell from 7 % to 5 % for the first army and from
        /// 5 % to 3 % for the middle one; the odds against the other kinds did not move.
        /// </summary>
        bool StepUp(Fighter f)
        {
            if (Reach(f) == 0 || NearestEnemy(f.Cell, f.Side) <= ShortRange)
                return false;
            var danger = new HashSet<int>();
            int front = int.MaxValue;
            foreach (Fighter g in Fighters)
            {
                if (!g.Alive || g.Stats.Has(Ability.Shoots))
                    continue;
                if (g.Side != f.Side)
                    danger.UnionWith(StrikeZone(g));
                else
                    front = Math.Min(front, NearestEnemy(g.Cell, f.Side));
            }
            int[] steps = Steps(f);
            int reach = Reach(f), best = -1, bestKey = int.MinValue;
            for (int c = 0; c < steps.Length; c++)
            {
                if (steps[c] > reach || c == f.Cell || danger.Contains(c) || EnemyNextTo(c, f.Side))
                    continue;
                int near = NearestEnemy(c, f.Side);
                if (near > ShortRange || front != int.MaxValue && near <= front)
                    continue;
                int key = near * 100 - steps[c];
                if (key > bestKey)
                {
                    bestKey = key;
                    best = c;
                }
            }
            return best >= 0 && Walk(best);
        }

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
            // A troll goes for the stack it told (the combat plan's 3.1), past whoever strikes it
            // on the way, while it can get to it at all.
            if (temper == Temper.Biggest && f.Aim >= 0 && Fighters[f.Aim].Alive &&
                TowardsAim(f, Fighters[f.Aim], steps, previous, reach))
                return;
            Fighter target = Pick(f, temper, steps, reach, false, out int from);
            if (target != null && Strike(target, from))
                return;
            target = Pick(f, temper, steps, int.MaxValue - 1, false, out int goal);
            if (target != null && reach > 0)
            {
                int cell = goal;
                while (steps[cell] > reach)
                    cell = previous[cell];
                // Wolves circle out of the foot stacks' reach; skeletons keep their line.
                if (temper == Temper.Weakest && Round <= CircleRounds)
                    cell = Circle(f, steps, reach, goal, cell);
                else if (temper == Temper.Wall)
                {
                    int limit = LineLimit(f);
                    while (cell != f.Cell && Hexes[cell].Col < limit)
                        cell = previous[cell];
                }
                if (Walk(cell))
                    return;
            }
            Defend();
        }

        /// <summary>
        /// A troll's turn towards the stack it told (<see cref="Fighter.Aim"/>): it strikes it from
        /// the nearest cell beside it within reach, else walks towards that cell; false when no
        /// cell beside it can be got to at all, and the troll strikes or walks as its temper says.
        /// </summary>
        bool TowardsAim(Fighter f, Fighter aim, int[] steps, int[] previous, int reach)
        {
            int goal = -1, near = int.MaxValue;
            foreach (int n in Neighbours[aim.Cell])
            {
                int s = n == f.Cell ? 0 : Free(n) ? steps[n] : int.MaxValue;
                if (s < near)
                {
                    near = s;
                    goal = n;
                }
            }
            if (goal < 0)
                return false;
            if (near <= reach)
                return Strike(aim, goal);
            if (reach == 0)
                return false;
            int cell = goal;
            while (steps[cell] > reach)
                cell = previous[cell];
            return Walk(cell);
        }

        /// <summary>
        /// Every troll keeps the stack it told it goes for until that stack falls, then tells the
        /// biggest one left (the combat plan's 3.1: the troll's target is shown in advance, so the
        /// players can lead it away and strike the troll together before it heals).
        /// </summary>
        void DeclareAims()
        {
            foreach (Fighter f in Fighters)
            {
                if (!f.Alive || f.Side != Side.Monsters || Units.TemperOf(f.Unit) != Temper.Biggest ||
                    f.Aim >= 0 && Fighters[f.Aim].Alive)
                    continue;
                f.Aim = -1;
                int most = 0;
                foreach (Fighter t in Fighters)
                {
                    if (t.Alive && t.Side != f.Side && t.TotalHealth > most)
                    {
                        most = t.TotalHealth;
                        f.Aim = t.Id;
                    }
                }
            }
        }

        /// <summary>
        /// Where circling wolves stop when they cannot get at their mark this turn (the combat
        /// plan's 3.1): among the cells they can walk to, those no foot stack of the players could
        /// strike next turn, the nearest to <paramref name="goal"/>, the cell beside the mark they
        /// make for; <paramref name="otherwise"/> when there is no such cell.
        /// </summary>
        int Circle(Fighter f, int[] steps, int reach, int goal, int otherwise)
        {
            var danger = new HashSet<int>();
            foreach (Fighter g in Fighters)
            {
                if (g.Alive && g.Side != f.Side && g.Unit != Unit.Riders)
                    danger.UnionWith(StrikeZone(g));
            }
            int best = -1, near = int.MaxValue;
            for (int c = 0; c < steps.Length; c++)
            {
                if (steps[c] > reach || c != f.Cell && !Free(c) || danger.Contains(c))
                    continue;
                int d = Distance(c, goal);
                if (d < near || d == near && steps[c] < steps[best])
                {
                    near = d;
                    best = c;
                }
            }
            return best >= 0 ? best : otherwise;
        }

        /// <summary>
        /// The column a stack of skeletons may walk to at the furthest (the combat plan's 3.1:
        /// skeletons hold their line, none more than a cell ahead of the rearmost): a column before
        /// the rearmost other stack of its kind, counting the walk that stack still has this round;
        /// 0 when it walks alone. The monsters come from the right, so ahead is to the left.
        /// </summary>
        int LineLimit(Fighter f)
        {
            int limit = 0;
            foreach (Fighter g in Fighters)
            {
                if (g == f || !g.Alive || g.Side != f.Side || g.Unit != f.Unit)
                    continue;
                int to = order.IndexOf(g);
                bool acted = to >= 0 && to < next - 1;
                limit = Math.Max(limit, Hexes[g.Cell].Col - (acted || g.Webbed ? 0 : g.Stats.Speed) - 1);
            }
            return limit;
        }

        /// <summary>
        /// The cells the stack could strike in its next turn as the field stands now: those beside
        /// a cell it can walk to, its own included (the combat plan's 3.1: outlined in red when the
        /// pointer is on a monster). Its own cell is not in it.
        /// </summary>
        public HashSet<int> StrikeZone(Fighter f)
        {
            var zone = new HashSet<int>();
            if (f == null || !f.Alive)
                return zone;
            int[] steps = Steps(f);
            int reach = f.Webbed || f.Stuck ? 0 : f.Stats.Speed;
            for (int c = 0; c < steps.Length; c++)
            {
                if (steps[c] > reach)
                    continue;
                foreach (int n in Neighbours[c])
                {
                    if (!Blocked[n])
                        zone.Add(n);
                }
            }
            zone.Remove(f.Cell);
            return zone;
        }

        /// <summary>
        /// The enemy the temper picks among those the fighter can shoot or, within
        /// <paramref name="limit"/> steps, get next to; <paramref name="cell"/> is the free cell
        /// beside it fewest steps away (the fighter's own when it already stands beside it), -1
        /// for a shot. Null when there is none. A kind that minds the spears
        /// (<see cref="Units.CoverCare"/>) strikes from the cell beside the target where the cover
        /// costs it least, and weighs that cost against its temper's want, the want put on 0..1
        /// among the targets: wolves leave a covered weak stack for an uncovered one. The cost
        /// counts in steps (<see cref="CareStep"/>), so that a small one changes nothing and a big
        /// pack still goes for the nearest; with no cost the pick is the temper's alone.
        /// </summary>
        Fighter Pick(Fighter f, Temper temper, int[] steps, int limit, bool shot, out int cell)
        {
            cell = -1;
            HashSet<int> mobbed = temper == Temper.Mob ? Mobbed(f) : null;
            float care = shot ? 0f : Units.CoverCare(f.Unit);
            var choices = new List<(Fighter Target, float Want, int Cell, int Away, float Loss, float Harm)>();
            float low = float.MaxValue, high = float.MinValue;
            foreach (Fighter t in Fighters)
            {
                if (!t.Alive || t.Side == f.Side)
                    continue;
                int beside = -1, away = int.MaxValue;
                float loss = 0f;
                if (shot)
                    away = Distance(f.Cell, t.Cell);
                else
                {
                    if (Distance(f.Cell, t.Cell) == 1)
                    {
                        beside = f.Cell;
                        away = 0;
                        loss = care > 0f ? CoverLoss(f, t, f.Cell) : 0f;
                    }
                    if (beside < 0 || Penalty(care, loss) > 0f)
                    {
                        foreach (int n in Neighbours[t.Cell])
                        {
                            if (!Free(n) || steps[n] > limit)
                                continue;
                            float l = care > 0f ? CoverLoss(f, t, n) : 0f;
                            float now = Penalty(care, l), then = Penalty(care, loss);
                            if (beside < 0 || now < then || (now == then && steps[n] < away))
                            {
                                beside = n;
                                away = steps[n];
                                loss = l;
                            }
                        }
                    }
                    if (beside < 0)
                        continue;
                }
                float want = Want(f, t, temper, mobbed);
                low = MathF.Min(low, want);
                high = MathF.Max(high, want);
                choices.Add((t, want, beside, away, loss, Expected(f, t, shot)));
            }
            Fighter best = null;
            (float Want, int Near, float Harm) bestKey = default;
            foreach (var c in choices)
            {
                float want = care > 0f ? (high > low ? (c.Want - low) / (high - low) : 1f) - Penalty(care, c.Loss) : c.Want;
                var key = (want, -c.Away, c.Harm);
                if (best == null || key.CompareTo(bestKey) > 0)
                {
                    best = c.Target;
                    bestKey = key;
                    cell = c.Cell;
                }
            }
            return best;
        }

        /// <summary>The spears' cost counts in steps of this much of the want's range, rounded: under half a step it is nothing.</summary>
        const float CareStep = 0.25f;

        /// <summary>What the spears' cover costs a monster that minds them this much, against its want (0 when it does not mind them).</summary>
        static float Penalty(float care, float loss) => care <= 0f ? 0f : CareStep * MathF.Floor(care * loss / CareStep + 0.5f);

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
                f.Covered = false;
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
            bool stuck = Current != null && Current.Stuck;
            if (Current != null)
                Current.Stuck = false;
            DeclareAims();
            if (!SideAlive(Side.Monsters))
                Finish(Outcome.Won);
            else if (!SideAlive(Side.Players))
                Finish(Outcome.Lost);
            if (Outcome != Outcome.Running)
                return;
            // «В атаку!»: the same stack acts once more, still caught in a web if it was.
            Fighter ordered = again;
            again = extra = null;
            if (ordered != null && ordered == Current && ordered.Alive)
            {
                ordered.Stuck = stuck;
                extra = ordered;
                MaybeOffer();
                return;
            }
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
        /// <summary>
        /// Whether the hero may give an order now: in a turn of the players' stacks, not while
        /// the monsters' offer to give up waits for an answer, with an order left.
        /// </summary>
        public bool CanOrder =>
            Outcome == Outcome.Running && !Offered && OrdersLeft > 0 && Current != null && Current.Side == Side.Players
            && again == null;

        /// <summary>
        /// The hero's order (the combat plan of 2026-11-11, 3.4): «В атаку!» lets the stack whose
        /// turn it is act once more after its action; «Щиты!» puts every stack of the players into
        /// defence until its next turn, and the stack whose turn it is still acts.
        /// </summary>
        public bool GiveOrder(HeroOrder order)
        {
            if (!CanOrder)
                return false;
            OrdersLeft--;
            OrderRound = Round;
            Events.Add(new BattleEvent { Kind = BattleEventKind.Order, Actor = Current.Id, Order = order });
            if (order == HeroOrder.Attack)
                again = Current;
            else
            {
                foreach (Fighter f in Fighters)
                {
                    if (f.Side == Side.Players && f.Alive)
                        f.Defending = true;
                }
            }
            return true;
        }

        /// <summary>
        /// The stack the players' auto battle gives «В атаку!» to: the strongest by worth, at its
        /// first strike or shot (the combat plan's 3.4).
        /// </summary>
        Fighter Strongest()
        {
            Fighter best = null;
            float worth = 0f;
            foreach (Fighter f in Fighters)
            {
                if (f.Side != Side.Players || !f.Alive)
                    continue;
                float w = f.Count * Units.Worth(f.Unit);
                if (w > worth)
                {
                    worth = w;
                    best = f;
                }
            }
            return best;
        }

        /// <summary>The players' auto battle orders «В атаку!» before the strongest stack's strike or shot.</summary>
        void OrderBeforeBlow(Fighter f)
        {
            if (CanOrder && f == Strongest())
                GiveOrder(HeroOrder.Attack);
        }

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
            Biome ground, int seed, int runs = 200, FieldEvent fieldEvent = FieldEvent.None)
        {
            var rng = new Rng(seed);
            int won = 0;
            var losses = new float[army.Count];
            for (int run = 0; run < runs; run++)
            {
                var battle = new Battle(army, attack, defence, monster, count, ground, rng.NextInt(), fieldEvent);
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
