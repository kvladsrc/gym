using Unity.MLAgents;
using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>
    /// One duel in the training arena: two fighters of one body and one
    /// network (FighterAgent, symmetric self-play). Each episode lays the
    /// arena out anew (its floor bent into hills up to 0.6 m, 0–12 trunks of
    /// random girth, 0–4 wall pieces, at random places), and draws each fighter's height (1.4–2.2 m) and weapon (sword,
    /// hammer or bare hands) apart; they start on clear ground, at least 5 m
    /// apart, facing anywhere.
    ///
    /// The fight's rewards come in the curriculum's last stage (FighterAgent.
    /// Stage 2; before it the agents learn to face and close in): damage dealt
    /// as a share of the other's health, the same taken off; the win +1, the
    /// loss nothing more (its lost health is -1 already), a time-out -2 to
    /// both: losing a fight, energy and all, is always better than running
    /// from it to the time-out. Not zero-sum: a decided fight is +1 to the
    /// pair, a time-out -4. A combo, adrenaline and evasions (they
    /// raise adrenaline) pay through
    /// the damage they add (Fighter.ComboFactor, Fighter.LevelFactor). No
    /// cost for passing time. Earlier
    /// stages end at a shorter time limit, with nothing for it.
    /// </summary>
    public class DuelEpisode : DuelGround
    {
        public FighterAgent first, second;
        /// <summary>Trunks and wall pieces, from which each episode shows some.</summary>
        public Transform[] trees = { }, walls = { };
        /// <summary>Half the floor's side inside the walls, m.</summary>
        public float halfSize = 7f;
        public Vector2 heights = new Vector2(1.4f, 2.2f);
        /// <summary>The floor bent anew each fight (<see cref="Shape"/>):
        /// the game's ground is not flat. Its mesh, and its collider.</summary>
        public MeshFilter ground;
        public MeshCollider groundCollider;
        /// <summary>The most a fight's floor rises or sinks from the middle,
        /// m (each fight from flat to this); and its hills' width, m.</summary>
        public float bumps = 0.6f;
        public Vector2 hillWidths = new Vector2(6f, 14f);
        /// <summary>The floor's cells a side.</summary>
        public const int GroundCells = 60;
        float bumpHeight, hillWidth;
        Vector2 noiseAt;
        /// <summary>The time limits, s: of the early stages and of the fight.</summary>
        public float earlyLimit = 30f, limit = 90f;
        /// <summary>The share of the episode's time limit gone, 0–1 (the agents see it).</summary>
        public override float Elapsed => Mathf.Clamp01(clock / (FighterAgent.Stage >= 2 ? limit : earlyLimit));

        /// <summary>A time-out's reward to both. With a loss at -1 (and its
        /// lost health another -1, and the energy spent), the side losing
        /// gained by running to the time-out, and they ran; now a loss adds
        /// nothing to its lost health.</summary>
        const float TimeOut = -2f;
        /// <summary>A loss's own reward: nothing to learn from, only its sign
        /// for the ELO.</summary>
        const float LossMark = -0.001f;

        float clock;
        bool over;
        int peak;
        /// <summary>The weapons' crossings this episode, and whether they cross now.</summary>
        int clashes;
        bool crossing;
        /// <summary>Of a sword against a hammer: the sword's hits on the hammer's holder.</summary>
        int swordHits;

        public override Vector3 Centre => transform.position;
        public override float HalfSize => halfSize;

        void Start()
        {
            var stats = Academy.Instance.StatsRecorder;
            var taken = Tags("Actions", first.Body);
            var landedTags = Tags("Actions landed", first.Body);
            foreach (var (agent, rival) in new[] { (first, second), (second, first) })
            {
                var me = agent;
                var them = rival;
                me.Body.Health.Damaged += amount =>
                {
                    if (FighterAgent.Stage < 2) return;
                    float share = amount / me.Body.Health.max;
                    them.AddReward(share);
                    me.AddReward(-share);
                };
                me.Body.Evaded += () =>
                {
                    stats.Add("Duel/Evaded", 1f, StatAggregationMethod.Sum);
                };
                me.Body.Struck += (landed, damage) =>
                {
                    stats.Add("Duel/Hit rate", landed ? 1f : 0f);
                    if (!landed) return;
                    stats.Add("Duel/Damage a hit", damage);
                    stats.Add("Duel/Combo", me.Body.Combo);
                };
                me.Body.LevelRose += level => peak = Mathf.Max(peak, level);
                // Which actions it takes with what in hand, and which of its
                // blows land (training/actions.py tables them).
                int doing = -1;
                bool told = false;
                me.Body.Acted += action =>
                {
                    doing = action;
                    told = false;
                    if (FighterAgent.Stage >= 2)
                        stats.Add(taken[(int)me.Body.Held, action], 1f, StatAggregationMethod.Sum);
                };
                me.Body.Struck += (landed, _) =>
                {
                    if (!landed || told || doing < 0 || FighterAgent.Stage < 2) return;
                    told = true;
                    stats.Add(landedTags[(int)me.Body.Held, doing], 1f, StatAggregationMethod.Sum);
                };
                // A hit of the sword itself (its blade open this frame; a kick has none).
                me.Body.Struck += (landed, _) =>
                {
                    if (landed && me.Body.Held == Weapon.Sword && them.Body.Held == Weapon.Hammer && me.Body.Blades > 0)
                        swordHits++;
                };
            }
            // Every action each weapon may take, once at 0: one never taken still shows.
            var body = first.Body;
            foreach (Weapon held in System.Enum.GetValues(typeof(Weapon)))
                for (int action = 0; action <= body.RollAction; action++)
                    if (body.Usable(action, held))
                        stats.Add(taken[(int)held, action], 0f, StatAggregationMethod.Sum);
            Begin();
        }

        /// <summary>Each action's stat by what is in hand, made once: "Actions/Sword/07 name".</summary>
        static string[,] Tags(string kind, Fighter body)
        {
            var weapons = (Weapon[])System.Enum.GetValues(typeof(Weapon));
            var tags = new string[weapons.Length, body.Actions];
            foreach (var held in weapons)
                for (int action = 0; action < body.Actions; action++)
                {
                    string name = action == body.RollAction ? "roll" : action < body.attackNames.Length ? body.attackNames[action] : "?";
                    tags[(int)held, action] = $"{kind}/{held}/{action:00} {name}";
                }
            return tags;
        }

        void Update()
        {
            if (over) return;
            clock += Time.deltaTime;
            int stage = FighterAgent.Stage;
            bool firstDead = first.Body.Health.Dead, secondDead = second.Body.Health.Dead;
            if (!firstDead && !secondDead && clock < (stage >= 2 ? limit : earlyLimit)) return;
            over = true;
            if (stage >= 2)
            {
                // Both dead in one frame cannot happen (blows land one at a time).
                // A win +1, a loss nothing more, a time-out -2. Added to the
                // last step's damage and energy (set, they were lost). The
                // self-play ELO reads the last step's sign (>0 a win, 0 a
                // draw, <0 a loss): a loss gets a mark of -0.001 so it is never
                // taken for a draw (with 0 its ELO only rose).
                bool decided = firstDead || secondDead;
                first.AddReward(secondDead ? 1f : decided ? LossMark : TimeOut);
                second.AddReward(firstDead ? 1f : decided ? LossMark : TimeOut);
            }
            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Duel/Stage", stage);
            stats.Add("Duel/Decided", firstDead || secondDead ? 1f : 0f);
            stats.Add("Duel/Length, s", clock);
            stats.Add("Duel/Peak adrenaline", peak);
            // The share of the decisions spent at adrenaline 0 (far apart).
            int decisions = first.Decisions + second.Decisions;
            if (decisions > 0) stats.Add("Duel/Apart share", (first.Apart + second.Apart) / (float)decisions);
            // Which balance: where it changed shows as a step (weapons.py, actions.py).
            stats.Add("Balance/version", Balance.Hash, StatAggregationMethod.MostRecent);
            stats.Add("Duel/Health lost", 1f - (first.Body.Health.Current / first.Body.Health.max
                + second.Body.Health.Current / second.Body.Health.max) / 2f);
            if (stage >= 2)
            {
                WeaponStats(stats, firstDead, secondDead);
                ClashStats(stats);
                GuardStats(stats);
            }
            first.EndEpisode();
            second.EndEpisode();
            Begin();
        }

        /// <summary>The weapons' record (training/weapons.py tables it): of a
        /// pair of different weapons, the share of its decided fights the first
        /// won (Weapons/Sword vs Hammer, the pair in the enum's order).</summary>
        void WeaponStats(StatsRecorder stats, bool firstDead, bool secondDead)
        {
            Weapon a = first.Body.Held, b = second.Body.Held;
            if (a == b || !(firstDead || secondDead)) return;
            bool firstWon = secondDead;
            // In the enum's order: a pair is one entry whichever side held what.
            if (a > b) (a, b, firstWon) = (b, a, !firstWon);
            stats.Add($"Weapons/{a} vs {b}", firstWon ? 1f : 0f);
        }

        /// <summary>Two weapons crossing, both in their blows' open phases (after
        /// both fighters' Update): counted once as they meet, however many frames
        /// they stay crossed. Nothing changes for it yet: only counted.</summary>
        void LateUpdate()
        {
            Fighter one = first.Body, two = second.Body;
            bool now = false;
            for (int i = 0; i < one.Blades && !now; i++)
                for (int j = 0; j < two.Blades && !now; j++)
                    now = Fighter.Cross(one.Blade[i], two.Blade[j]);
            if (now && !crossing) clashes++;
            crossing = now;
        }

        /// <summary>Of a fight between two weapons (bare hands have none to
        /// cross): their crossings (Clash/Sword vs Hammer, a fight), and each
        /// weapon's blows and the share of them a blow taken cut short.</summary>
        void ClashStats(StatsRecorder stats)
        {
            Fighter one = first.Body, two = second.Body;
            if (one.Held == Weapon.Hands || two.Held == Weapon.Hands) return;
            if (one.Held > two.Held) (one, two) = (two, one);
            stats.Add($"Clash/{one.Held} vs {two.Held}", clashes);
            foreach (var (f, rival) in new[] { (one, two), (two, one) })
            {
                string at = $"{f.Held} vs {rival.Held}";
                stats.Add($"Clash/{at}: blows", f.WeaponBlows);
                if (f.WeaponBlows > 0) stats.Add($"Clash/{at}: blows cut", f.WeaponBlowsCut / (float)f.WeaponBlows);
            }
        }

        /// <summary>Of a sword against a hammer: the share of the sword's blows
        /// the hammer blocked, and the sword's hits a fight (Guard/).</summary>
        void GuardStats(StatsRecorder stats)
        {
            Fighter sword = first.Body.Held == Weapon.Sword ? first.Body : second.Body;
            Fighter hammer = sword == first.Body ? second.Body : first.Body;
            if (sword.Held != Weapon.Sword || hammer.Held != Weapon.Hammer) return;
            if (sword.WeaponBlows > 0) stats.Add("Guard/Sword blows blocked", sword.WeaponBlowsBlocked / (float)sword.WeaponBlows);
            stats.Add("Guard/Sword hits", swordHits);
        }

        void Begin()
        {
            // A balance changed since takes effect from this fight (Fighter.ResetAt).
            Balance.Refresh();
            clock = 0f;
            over = false;
            peak = 0;
            clashes = 0;
            crossing = false;
            swordHits = 0;
            Shape();
            Layout();
            foreach (var agent in new[] { first, second })
                agent.Body.Equip((Weapon)Random.Range(0, 3), Random.Range(heights.x, heights.y));
            float edge = halfSize - 1.5f;
            Vector3 one, two;
            int tries = 0;
            do
            {
                one = Free(edge, 1.2f * first.Body.Size);
                two = Free(edge, 1.2f * second.Body.Size);
            } while (Vector3.Distance(one, two) < 5f && ++tries < 50);
            first.Body.ResetAt(one, Random.Range(0f, 360f));
            second.Body.ResetAt(two, Random.Range(0f, 360f));
            first.Restart();
            second.Restart();
        }

        /// <summary>0–12 trunks 0.4–1.6 m thick and 0–4 wall pieces 2–4 m long,
        /// anywhere on the floor, turned at random; the rest put away.</summary>
        void Layout()
        {
            float edge = halfSize - 1f;
            int trunks = Random.Range(0, trees.Length + 1);
            for (int i = 0; i < trees.Length; i++)
            {
                var tree = trees[i];
                tree.gameObject.SetActive(i < trunks);
                if (i >= trunks) continue;
                float girth = Random.Range(0.4f, 1.6f);
                tree.localScale = new Vector3(girth, tree.localScale.y, girth);
                // Sunk a little: on a slope the trunk's low side stood in the air.
                var at = new Vector3(Centre.x + Random.Range(-edge, edge), 0f, Centre.z + Random.Range(-edge, edge));
                at.y = Height(at) + tree.localScale.y - Sunk;
                tree.position = at;
            }
            int pieces = Random.Range(0, walls.Length + 1);
            for (int i = 0; i < walls.Length; i++)
            {
                var wall = walls[i];
                wall.gameObject.SetActive(i < pieces);
                if (i >= pieces) continue;
                wall.localScale = new Vector3(Random.Range(2f, 4f), wall.localScale.y, wall.localScale.z);
                var at = new Vector3(Centre.x + Random.Range(-edge, edge), 0f, Centre.z + Random.Range(-edge, edge));
                at.y = Height(at) + wall.localScale.y / 2f - Sunk;
                wall.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 180f), 0f));
            }
            // The new places, for the spawn checks below in this same frame.
            Physics.SyncTransforms();
        }

        /// <summary>A place with no obstacle within <paramref name="clearance"/> m.</summary>
        Vector3 Free(float edge, float clearance)
        {
            for (int i = 0; i < 60; i++)
            {
                var at = Centre + new Vector3(Random.Range(-edge, edge), 0f, Random.Range(-edge, edge));
                at.y = Height(at);
                bool clear = true;
                foreach (var hit in Physics.OverlapSphere(at + Vector3.up * 1.2f, clearance, 1 << 0, QueryTriggerInteraction.Ignore))
                    if (hit.CompareTag("Obstacle")) { clear = false; break; }
                if (clear) return at;
            }
            return Centre + Vector3.up * Height(Centre);
        }

        /// <summary>How deep trunks and wall pieces stand in the floor, m.</summary>
        const float Sunk = 0.5f;

        /// <summary>A flat floor <paramref name="halfSize"/> m from the middle
        /// each way, <see cref="GroundCells"/> cells a side (the builder's;
        /// <see cref="Shape"/> bends it).</summary>
        public static Mesh FlatGround(float halfSize)
        {
            int n = GroundCells + 1;
            var vertices = new Vector3[n * n];
            var uv = new Vector2[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    vertices[z * n + x] = new Vector3((x / (float)GroundCells * 2f - 1f) * halfSize, 0f, (z / (float)GroundCells * 2f - 1f) * halfSize);
                    // In metres from the middle: the viewer's 25 cm cells (LevelBuilder.ArenaFloor).
                    uv[z * n + x] = new Vector2(vertices[z * n + x].x, vertices[z * n + x].z);
                }
            var triangles = new int[GroundCells * GroundCells * 6];
            for (int z = 0, t = 0; z < GroundCells; z++)
                for (int x = 0; x < GroundCells; x++, t += 6)
                {
                    int v = z * n + x;
                    triangles[t] = v; triangles[t + 1] = v + n; triangles[t + 2] = v + 1;
                    triangles[t + 3] = v + 1; triangles[t + 4] = v + n; triangles[t + 5] = v + n + 1;
                }
            var mesh = new Mesh { name = "Arena floor", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The floor's height at a place (world y): hills of
        /// <see cref="hillWidth"/>, rising and sinking up to <see cref="bumpHeight"/>.</summary>
        public float Height(Vector3 at)
        {
            float x = (at.x - Centre.x) / hillWidth + noiseAt.x, z = (at.z - Centre.z) / hillWidth + noiseAt.y;
            float hills = (Mathf.PerlinNoise(x, z) - 0.5f) * 2f + 0.35f * (Mathf.PerlinNoise(2.1f * x + 7.3f, 2.1f * z + 1.7f) - 0.5f) * 2f;
            return Centre.y + bumpHeight * hills / 1.35f;
        }

        /// <summary>This fight's floor: from flat to <see cref="bumps"/> high,
        /// hills <see cref="hillWidths"/> wide, anywhere in the noise.</summary>
        void Shape()
        {
            bumpHeight = Random.Range(0f, bumps);
            hillWidth = Random.Range(hillWidths.x, hillWidths.y);
            noiseAt = new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f));
            if (ground == null)
            {
                bumpHeight = 0f;
                return;
            }
            var mesh = ground.sharedMesh;
            var vertices = mesh.vertices;
            var place = ground.transform;
            for (int i = 0; i < vertices.Length; i++)
            {
                var world = place.TransformPoint(vertices[i]);
                vertices[i].y = place.InverseTransformPoint(new Vector3(world.x, Height(world), world.z)).y;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // A collider takes a changed mesh only when given it again.
            groundCollider.sharedMesh = null;
            groundCollider.sharedMesh = mesh;
        }
    }
}
