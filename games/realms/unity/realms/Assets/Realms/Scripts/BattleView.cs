using System.Collections;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The battle mode: a field of 11 × 7 hexes on the fight's ground, one figure for each stack
    /// with its count, the players' commands (click a cell to walk, an enemy to strike or shoot,
    /// D to defend, A for the auto battle) and the fight's events played as short animations.
    /// The map is hidden meanwhile. It only shows a <see cref="Battle"/> and passes the players'
    /// choices to it, so another kind of battle can replace it. The ground is the map's (its
    /// shader, its textures and small things for the fight's biome), the woods and stones around
    /// the field are the map's studio models at the figures' scale, and the figures carry a light
    /// along their outlines so they stand out from the ground.
    /// </summary>
    public sealed class BattleView : MonoBehaviour
    {
        public Material material;
        public ArtLibrary art;
        public Hud hud;
        public MapView view;
        public MapCamera mapCamera;
        public Portraits portraits;

        /// <summary>
        /// Figures stand this many times bigger than on the map (a field cell is as big as a map
        /// cell), the big ones at most <see cref="MaxFigure"/>. At 4.7 and 2.3 neighbours in close
        /// combat ran into each other and over each other's count plates (review of 2026-11-10).
        /// </summary>
        public float figureScale = 3.9f;

        /// <summary>The tallest or longest a figure stands on the field, so a big one keeps to its cell.</summary>
        const float MaxFigure = 1.9f;

        /// <summary>
        /// Where a stack's count plate hangs, from its cell's middle: at the cell's near corner, in
        /// the gap between the two figures of the row in front, so it lies on no body.
        /// </summary>
        static readonly Vector3 PlateAt = new Vector3(0f, 0.02f, -0.84f);

        /// <summary>
        /// Far from the map, which is hidden anyway, and above the sea's level, where the terrain
        /// shader draws land and its small things.
        /// </summary>
        static readonly Vector3 Origin = new Vector3(-600f, 0.2f, -600f);

        static readonly Color Red = Palette.Hex("#D05D5F");

        /// <summary>
        /// The battle camera: steeper and narrower than the map's, so the field fills the screen's
        /// width between the panels and the figures stand bigger (review of 2026-11-10).
        /// </summary>
        const float Pitch = 48f, FieldOfView = 30f;

        /// <summary>How near the framed field comes to the screen's sides (a share of half the width) and to the panels (pixels).</summary>
        const float SideMargin = 0.02f, PanelMargin = 6f;

        /// <summary>
        /// The least a tree around the field stands: where less shows under the top panel, a stone
        /// or something small stands instead, so no crown is cut off by the panel.
        /// </summary>
        const float LeastTree = 2.4f;

        /// <summary>
        /// The cells' outlines: the outer part of each hexagon (from this share of its radius) is
        /// drawn lighter, so the grid shows as thin light lines; <see cref="FillVertices"/> of each
        /// cell's vertices are its inside.
        /// </summary>
        const float LineFrom = 0.965f, LineLight = 0.22f;

        /// <summary>How far the rims of the cells the spears cover go towards the kingdom's colour.</summary>
        const float CoverRim = 0.75f;

        /// <summary>How far towards the target a strike with a run-up lunges (a plain strike 0.42).</summary>
        const float RunUpReach = 0.62f;

        /// <summary>
        /// A creature with its own moves (<see cref="Moves"/>) runs a cell in this many seconds
        /// (the others hop one in 0.14), its run clip sped up at most <see cref="RunPlayback"/>
        /// times to keep up: beyond that its paws slide a little.
        /// </summary>
        const float RunCell = 0.3f, RunPlayback = 1.6f;


        /// <summary>
        /// How far the outline round the cells a monster under the pointer could strike goes
        /// towards red. The cells themselves were tinted at first (0.4: at a quarter the meadow's
        /// yellow green turned only khaki, shots63), but a wolf, eight cells a turn on a field
        /// eleven wide, reaches nearly all of it, and the whole field went red (2026-11-19).
        /// </summary>
        const float DangerLine = 0.85f;

        /// <summary>
        /// A troll's arrow: how far from each figure's middle it starts and ends, the length and
        /// half width of its head, the half width of its shaft, its dark edge, and its height.
        /// </summary>
        const float AimGap = 0.45f, AimHead = 0.3f, AimHeadWidth = 0.2f, AimWidth = 0.065f, AimEdge = 0.035f, AimLift = 0.016f;

        /// <summary>
        /// Draws an arrow on the ground from each troll to the stack it told (the combat plan's
        /// 3.1: the target is shown in advance, so the players can lead it away), between the
        /// figures as they stand now; the mesh is made again only when one has moved.
        /// </summary>
        void UpdateAims()
        {
            if (aims == null || battle == null)
                return;
            var ends = new List<Vector3>();
            if (battle.Outcome == Outcome.Running)
            {
                foreach (Fighter f in battle.Fighters)
                {
                    if (!f.Alive || f.Aim < 0 || !battle.Fighters[f.Aim].Alive || shown[f.Id] <= 0 || shown[f.Aim] <= 0 ||
                        !figures.TryGetValue(f.Id, out Transform troll) || !figures.TryGetValue(f.Aim, out Transform aim))
                        continue;
                    ends.Add(troll.localPosition);
                    ends.Add(aim.localPosition);
                }
            }
            bool same = ends.Count == aimEnds.Count;
            for (int k = 0; same && k < ends.Count; k++)
                same = (ends[k] - aimEnds[k]).sqrMagnitude < 1e-4f;
            if (same)
                return;
            aimEnds.Clear();
            aimEnds.AddRange(ends);
            var kit = new MeshKit();
            for (int k = 0; k + 1 < ends.Count; k += 2)
            {
                Arrow(kit, ends[k], ends[k + 1], AimEdge, AimLift - 0.003f, RingDark);
                Arrow(kit, ends[k], ends[k + 1], 0f, AimLift, Red);
            }
            Mesh old = aims.sharedMesh;
            aims.sharedMesh = kit.ToMesh("Figure aims");
            if (old != null)
                Destroy(old);
        }

        /// <summary>A flat arrow on the ground between two places, widened by <paramref name="grow"/> on every side.</summary>
        static void Arrow(MeshKit kit, Vector3 from, Vector3 to, float grow, float lift, Color colour)
        {
            Vector3 way = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float length = way.magnitude;
            if (length < 2f * AimGap + AimHead)
                return;
            Vector3 dir = way / length, side = new Vector3(-dir.z, 0f, dir.x), up = Vector3.up * lift;
            Vector3 start = new Vector3(from.x, 0f, from.z) + dir * (AimGap - grow) + up;
            Vector3 tip = new Vector3(to.x, 0f, to.z) - dir * (AimGap - grow * 1.6f) + up;
            Vector3 neck = tip - dir * (AimHead + grow * 2.6f);
            float shaft = AimWidth + grow, head = AimHeadWidth + grow * 1.6f;
            kit.Quad(start - side * shaft, start + side * shaft, neck + side * shaft, neck - side * shaft, colour, Vector3.up);
            kit.Triangle(neck - side * head, neck + side * head, tip, colour, Vector3.up);
        }
        const int FillVertices = 18;

        /// <summary>
        /// The ring round the acting stack, inside its cell: in the player's kingdom colour or, for
        /// the monsters, in the bone their plates wear, between two dark lines (RingEdge wide), so
        /// it shows on light ground as well (the bone alone read pale there, review of 2026-11-10).
        /// </summary>
        const float RingInner = 0.86f, RingOuter = 0.95f, RingEdge = 0.035f, RingLift = 0.012f;
        static readonly Color Bone = Palette.Hex("#CDBF9F"), RingDark = Palette.Hex("#2B2620");

        /// <summary>
        /// The unit models whose heads point along the model's -x instead of the -z the figures
        /// face (seen in the models' silhouettes, 2026-11-10): the turn that brings them round.
        /// </summary>
        static readonly Dictionary<string, float> Facings = new Dictionary<string, float>
        {
            ["boar"] = -90f, ["rider"] = -90f, ["wolf"] = -90f, ["wolf_2"] = -90f,
        };

        static readonly int OverlayId = Shader.PropertyToID("_RealmsOverlay"), FieldBiomeId = Shader.PropertyToID("_RealmsFieldBiome");
        static readonly int RimLightId = Shader.PropertyToID("_RimLight"), BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>The light along the figures' outlines, linear.</summary>
        static readonly Vector4 RimLight = new Vector4(0.12f, 0.115f, 0.1f, 0f);

        /// <summary>
        /// The unit models whose paint is darker than <see cref="LeastLightness"/>: their Oklab
        /// lightness, area-weighted over the texture (measured 2026-11-10; the others measure
        /// 0.39-0.48, the boar remade on 2026-11-11 0.50). On the field they are lifted to it, so a
        /// dark figure does not sink into the ground.
        /// </summary>
        static readonly Dictionary<string, float> DarkModels = new Dictionary<string, float>
        {
            ["bandit"] = 0.29f, ["troll"] = 0.297f,
        };

        const float LeastLightness = 0.32f;

        /// <summary>How far a tree's top around the field moves in the wind, as a share of its height.</summary>
        const float TreeSway = 0.025f;

        /// <summary>How far a bush's top and a tuft of grass around the field move in the wind, for every unit of their height.</summary>
        const float BushSway = 0.04f, GrassSway = 0.12f;

        /// <summary>The map's bushes, set round the field where a tree's crown would not show.</summary>
        static readonly string[] BushKinds = { "bush_round", "bush_wide" };

        /// <summary>
        /// How far from the field's middle the ground's patches (<see cref="Patch"/>) reach across and
        /// along: beyond it the ground is the biome's own, as the far plain under it.
        /// </summary>
        const float PatchReachX = 24f, PatchReachZ = 15f;

        Vector2 patchSeed;
        Vector3 patchCentre;

        /// <summary>The ground's colour under each vertex of the cells, with its patches; the cells are recoloured from it.</summary>
        Color[] tileGround;

        readonly Dictionary<string, ModelBatch.Source> sources = new Dictionary<string, ModelBatch.Source>();
        Vector4 overlay;
        Battle battle;

        /// <summary>The map cell of the group fought, whose look its stacks keep (-1: the kinds' own).</summary>
        int site = -1;
        Transform root, arrow, ring;
        Mesh tiles, ringMesh;

        /// <summary>The arrows on the ground from each troll to the stack it told (the combat plan's 3.1), rebuilt as they move.</summary>
        MeshFilter aims;

        /// <summary>The figures' places the arrows were last drawn between, two for each.</summary>
        readonly List<Vector3> aimEnds = new List<Vector3>();
        Color[] tileColours;

        /// <summary>The cell across each side of each cell, six a cell in the order of their rims (-1 off the field).</summary>
        int[] sides;
        int ringOf = -1;
        Color ringColour;
        float mapFieldOfView, panelTop;
        readonly Dictionary<int, Transform> figures = new Dictionary<int, Transform>();
        readonly Dictionary<int, UnityEngine.UIElements.Label> counts = new Dictionary<int, UnityEngine.UIElements.Label>();
        readonly Dictionary<int, Transform> whiteFlags = new Dictionary<int, Transform>();

        /// <summary>The rim of the plates after a lost fight (Hud.uss .battle-count-lost).</summary>
        static readonly Color LostRim = new Color32(0x6E, 0x68, 0x5F, 0xFF);

        /// <summary>How many times the spears' cover has been shown in this battle (the capture waits for it).</summary>
        public int CoversShown { get; private set; }

        /// <summary>How many strikes with a run-up have been shown in this battle (the capture waits for one).</summary>
        public int RunUpsShown { get; private set; }

        /// <summary>The battle waits for the players to move a stack.</summary>
        public bool Waiting => waiting;

        /// <summary>The hero's orders shown in this battle, for the captures.</summary>
        public int OrdersShown { get; private set; }

        /// <summary>Whether the chest on this field has been shown taken, for the captures.</summary>
        public int ChestsShown { get; private set; }

        /// <summary>The kingdom of the hero who fights, whose figure stands at the field's left edge to give the order; -1 for none.</summary>
        int heroCountry = -1;

        /// <summary>The hero's figure at the left edge (the combat plan's 3.4), null when the battle has no orders.</summary>
        Transform heroFigure;

        /// <summary>The chest of a «Добыча» field (the combat plan's 3.5) until a players' stack takes it, else null.</summary>
        Transform chestFigure;

        /// <summary>How wide the chest on the field is: half a cell, so that it reads beside the figures.</summary>
        const float ChestWidth = 0.62f;

        /// <summary>
        /// How far left of the first column, in columns, and on which row the hero stands: just
        /// clear of the field. A column and a third put him off the screen's edge (shots64).
        /// </summary>
        const float HeroOut = 0.85f;
        const int HeroRow = 3;

        /// <summary>How far from the hero's spot nothing grows or lies, so no crown or stone hides him.</summary>
        const float HeroClear = 3.2f;

        /// <summary>How far the hero's body and the order's words reach left of his spot, for the framing.</summary>
        const float HeroHalf = 0.75f;

        /// <summary>How far left the camera aims while the hero stands at the edge, so the field shrinks less to show him.</summary>
        const float HeroShift = 0.6f;

        /// <summary>Whether white rags are up over monsters that offer to give up.</summary>
        public bool WhiteFlagsUp => whiteFlags.Count > 0;
        int[] shown;
        int played, rounds, hover = -1;
        bool waiting, acted, auto;
        Vector3 pointer;
        Camera cam;
        Vector3 cameraPosition;
        Quaternion cameraRotation;

        public bool Active => battle != null;

        /// <summary>The fight on the field, null when none is shown.</summary>
        public Battle Fight => battle;

        /// <summary>Opens the field and plays the fight until it ends; the field stays until <see cref="Close"/>.</summary>
        public IEnumerator Play(Battle fight, string title, int cell = -1, int country = -1)
        {
            battle = fight;
            site = cell;
            heroCountry = country;
            played = rounds = 0;
            CoversShown = RunUpsShown = OrdersShown = ChestsShown = 0;
            hover = -1;
            waiting = acted = auto = false;
            shown = new int[battle.Fighters.Count];
            for (int i = 0; i < shown.Length; i++)
                shown[i] = battle.Fighters[i].StartCount;
            // The camera first: the woods around the field are placed where the camera sees them.
            Aim();
            Build();
            hud.ShowBattle(title, portraits, Defend, ToggleAuto, AskRetreat, ToggleOrders, Order);
            Ambience.Ground = battle.Ground;
            Sounds.Play(Sfx.BattleStart);
            Music.Battle(false);
            yield return Run();
        }

        public void Close()
        {
            StopAllCoroutines();
            hud.HideBattle();
            counts.Clear();
            whiteFlags.Clear();
            figures.Clear();
            if (root != null)
            {
                Figures.Destroy(root.gameObject);
                Shader.SetGlobalVector(OverlayId, overlay);
                Shader.SetGlobalFloat(FieldBiomeId, 0f);
            }
            root = arrow = ring = null;
            tiles = ringMesh = null;
            aims = null;
            aimEnds.Clear();
            heroFigure = null;
            chestFigure = null;
            tileGround = null;
            sides = null;
            ringOf = -1;
            battle = null;
            waiting = false;
            view.Content.gameObject.SetActive(true);
            // The map camera sets its clip planes and the shadows' reach again each frame; the
            // field of view is set once, so it is put back here.
            if (cam != null)
            {
                cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                cam.fieldOfView = mapFieldOfView;
            }
            mapCamera.enabled = true;
        }

        // ----- The field -----

        static Vector3 CellAt(int cell)
        {
            Hex h = Battle.HexOf(cell);
            return new Vector3(HexMetrics.CenterX(h), 0f, HexMetrics.CenterZ(h));
        }

        static Vector3 Centre
        {
            get
            {
                Vector3 a = CellAt(0), b = CellAt(Battle.Width * Battle.Height - 1);
                return new Vector3((a.x + b.x + HexMetrics.InnerRadius) * 0.5f, 0f, (a.z + b.z) * 0.5f);
            }
        }

        Color GroundColour =>
            battle.Ground == Biome.Mountains ? Color.Lerp(Palette.Hills, Palette.Rock, 0.45f) : Palette.Ground(battle.Ground);

        void Build()
        {
            view.Content.gameObject.SetActive(false);
            root = new GameObject("Battle").transform;
            root.position = Origin;
            var rng = new Rng(battle.Fighters.Count * 131 + (int)battle.Ground * 17 + battle.Fighters[0].StartCount);
            Color ground = GroundColour;
            GroundShares(out Vector4 sharesA, out Vector4 sharesB);

            // The map's grid would run on past the field: it is off while the fight lasts. The
            // ground's small things follow the fight's biome.
            overlay = Shader.GetGlobalVector(OverlayId);
            Shader.SetGlobalVector(OverlayId, new Vector4(overlay.x, 0f, overlay.z, overlay.w));
            Shader.SetGlobalFloat(FieldBiomeId, (int)battle.Ground);

            // The ground far around, in the map's textures, a little below the field's patches
            // (Patch), which lie on a finer grid where the camera sees them and fade into it.
            Vector3 c = Centre;
            patchCentre = c;
            patchSeed = new Vector2(rng.Range(0f, 500f), rng.Range(0f, 500f));
            var land = new MeshKit();
            const float reach = 70f, below = -0.01f;
            land.Quad(c + new Vector3(-reach, below, -reach), c + new Vector3(-reach, below, reach), c + new Vector3(reach, below, reach),
                c + new Vector3(reach, below, -reach), ground, Vector3.up);
            Ground(Figures.Built(land, "Figure ground", view.terrainMaterial, root), sharesA, sharesB);
            var near = new MeshKit();
            const float grid = 0.8f;
            int across = Mathf.CeilToInt(2f * PatchReachX / grid), along = Mathf.CeilToInt(2f * PatchReachZ / grid);
            for (int i = 0; i < across; i++)
            for (int j = 0; j < along; j++)
            {
                float x0 = c.x - PatchReachX + i * grid, z0 = c.z - PatchReachZ + j * grid;
                near.Quad(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z0 + grid), new Vector3(x0 + grid, 0f, z0 + grid),
                    new Vector3(x0 + grid, 0f, z0), ground, Vector3.up);
            }
            Patched(Figures.Built(near, "Figure ground patches", view.terrainMaterial, root), sharesA, sharesB);

            // The woods, stones and smaller things beyond the field, a little apart, where the
            // camera sees them, and a tree or a big stone on each blocked cell.
            var decor = new MeshKit();
            var batch = new ModelBatch();
            var taken = new List<Vector3>();
            for (int k = 0, placed = 0; k < 500 && placed < 70; k++)
            {
                var at = new Vector3(rng.Range(-10f, 29f), 0f, rng.Range(-2f, 18f));
                bool field = at.x > -2.6f && at.x < 20.8f && at.z > -2.6f && at.z < 10.9f;
                if (field || taken.Exists(other => (other - at).sqrMagnitude < 1.4f * 1.4f))
                    continue;
                // The hero's spot beyond the left edge stays clear.
                if (ShowsHero && (at - HeroSpot).sqrMagnitude < HeroClear * HeroClear)
                    continue;
                float room = Room(at);
                float sideways = cam.WorldToViewportPoint(Origin + at).x;
                if (room <= 0f || sideways < -0.08f || sideways > 1.08f)
                    continue;
                Decor(decor, batch, at, rng, false, room);
                taken.Add(at);
                placed++;
            }
            for (int i = 0; i < battle.Blocked.Length; i++)
            {
                if (battle.Blocked[i])
                    Decor(decor, batch, CellAt(i), rng, true, Room(CellAt(i)));
            }
            Tufts(decor, rng);
            if (decor.VertexCount > 0)
                Figures.Built(decor, "Figure decor", material, root);
            batch.Build(root, "battle");

            // The cells: one flat hexagon each in the ground's textures, recoloured to show where
            // the stack can go, with a lighter rim; the rims of neighbouring cells make the grid.
            var kit = new MeshKit();
            sides = new int[battle.Blocked.Length * 6];
            for (int i = 0; i < battle.Blocked.Length; i++)
            {
                Vector3 at = CellAt(i) + new Vector3(0, 0.005f, 0);
                for (int k = 0; k < 6; k++)
                {
                    int n = (k + 1) % 6;
                    Vector3 a = at + LineFrom * new Vector3(HexMetrics.CornerX[k], 0, HexMetrics.CornerZ[k]);
                    Vector3 b = at + LineFrom * new Vector3(HexMetrics.CornerX[n], 0, HexMetrics.CornerZ[n]);
                    kit.Triangle(at, a, b, Color.white, Vector3.up);
                }
                for (int k = 0; k < 6; k++)
                {
                    int n = (k + 1) % 6;
                    Vector3 a = new Vector3(HexMetrics.CornerX[k], 0, HexMetrics.CornerZ[k]);
                    Vector3 b = new Vector3(HexMetrics.CornerX[n], 0, HexMetrics.CornerZ[n]);
                    kit.Quad(at + LineFrom * a, at + a, at + b, at + LineFrom * b, Color.white, Vector3.up);
                    sides[i * 6 + k] = Across(i, a + b);
                }
            }
            GameObject cells = Figures.Built(kit, "Figure tiles", view.terrainMaterial, root);
            tileGround = Patched(cells, sharesA, sharesB);
            tiles = cells.GetComponent<MeshFilter>().sharedMesh;
            tileColours = new Color[tiles.vertexCount];

            // The ring round the acting stack, coloured and moved in Paint, and its dark lines.
            var band = new MeshKit();
            var lines = new MeshKit();
            for (int k = 0; k < 6; k++)
            {
                int n = (k + 1) % 6;
                Vector3 a = new Vector3(HexMetrics.CornerX[k], 0, HexMetrics.CornerZ[k]);
                Vector3 b = new Vector3(HexMetrics.CornerX[n], 0, HexMetrics.CornerZ[n]);
                band.Quad(RingInner * a, RingOuter * a, RingOuter * b, RingInner * b, Color.white, Vector3.up);
                float inner = RingInner - RingEdge, outer = RingOuter + RingEdge;
                lines.Quad(inner * a, RingInner * a, RingInner * b, inner * b, RingDark, Vector3.up);
                lines.Quad(RingOuter * a, outer * a, outer * b, RingOuter * b, RingDark, Vector3.up);
            }
            GameObject marker = Figures.Built(band, "Figure ring", material, root);
            marker.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Figures.Built(lines, "Figure ring lines", material, marker.transform)
                .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            ring = marker.transform;
            ringMesh = marker.GetComponent<MeshFilter>().sharedMesh;
            ringOf = -1;
            ringColour = Color.clear;
            ring.gameObject.SetActive(false);

            // The trolls' arrows to the stacks they told, drawn in LateUpdate.
            var told = new GameObject("Figure aims");
            told.transform.SetParent(root, false);
            aims = told.AddComponent<MeshFilter>();
            var toldRenderer = told.AddComponent<MeshRenderer>();
            toldRenderer.sharedMaterial = material;
            toldRenderer.shadowCastingMode = ShadowCastingMode.Off;
            aimEnds.Clear();

            foreach (Fighter f in battle.Fighters)
                Spawn(f);
            chestFigure = battle.Chest >= 0 ? FieldChest(CellAt(battle.Chest)) : null;

            // The hero beyond the left edge, turned to the field, when the battle has orders.
            if (ShowsHero)
            {
                heroFigure = new GameObject("Hero").transform;
                heroFigure.SetParent(root, false);
                heroFigure.localPosition = HeroSpot;
                heroFigure.localRotation = HeroRest;
                GameObject hero = Figures.Hero(art, material, heroFigure, HeroSize, heroCountry);
                hero.transform.localRotation = Quaternion.Euler(0f, Figures.HeroFacing(art), 0f);
            }

            var shaft = new MeshKit();
            shaft.Cylinder(new Vector3(0, 0, -0.35f), new Vector3(0, 0, 0.3f), 0.02f, 5, Palette.Wood, Palette.Wood);
            shaft.Lathe(new Vector3(0, 0, 0.3f), new[] { 0f, 0.12f }, new[] { 0.05f, 0f }, new[] { Palette.Rock }, 5, rng);
            arrow = Figures.Built(shaft, "Figure arrow", material, root).transform;
            arrow.gameObject.SetActive(false);
            Paint();
        }

        /// <summary>
        /// The ground textures' shares on the field, as the map gives them to the fight's biome;
        /// mountains fight on their grassy foothills, as <see cref="GroundColour"/> has it.
        /// </summary>
        void GroundShares(out Vector4 a, out Vector4 b)
        {
            var shares = new float[GroundLayer.Count];
            TerrainField.BiomeLayers(battle.Ground, shares, 0);
            if (battle.Ground == Biome.Mountains)
            {
                var hills = new float[GroundLayer.Count];
                TerrainField.BiomeLayers(Biome.Hills, hills, 0);
                for (int k = 0; k < shares.Length; k++)
                    shares[k] = Mathf.Lerp(hills[k], shares[k], 0.45f);
            }
            a = new Vector4(shares[0], shares[1], shares[2], shares[3]);
            b = new Vector4(shares[4], shares[5], shares[6], shares[7]);
        }

        /// <summary>Gives a mesh drawn with the terrain material the ground textures' shares; it casts no shadow.</summary>
        static void Ground(GameObject go, Vector4 a, Vector4 b)
        {
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var sharesA = new List<Vector4>(mesh.vertexCount);
            var sharesB = new List<Vector4>(mesh.vertexCount);
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                sharesA.Add(a);
                sharesB.Add(b);
            }
            mesh.SetUVs(1, sharesA);
            mesh.SetUVs(2, sharesB);
        }

        /// <summary>A studio model to set around the field, or null when the art lacks it.</summary>
        ModelBatch.Source Model(string name)
        {
            if (!sources.TryGetValue(name, out ModelBatch.Source source))
                sources[name] = source = art != null ? ModelBatch.Source.Of(art.ModelOf(name)) : null;
            return source;
        }

        /// <summary>
        /// A tree, a stone or something small around the field, or a tree or a big stone on a
        /// blocked cell: the map's studio models, or low-poly stand-ins while the art lacks them.
        /// A tree stands only where <paramref name="room"/> (see <see cref="Room"/>) shows at least
        /// <see cref="LeastTree"/> of it.
        /// </summary>
        void Decor(MeshKit kit, ModelBatch batch, Vector3 at, Rng rng, bool obstacle, float room)
        {
            Biome g = battle.Ground;
            float r = rng.Value();
            // A pattern's obstacles keep their kind, so that its shape reads: a grove is trees
            // (a stone where a crown would not show), a gorge a wall of stones.
            bool patterned = obstacle && Battle.Patterns && battle.Shape != FieldShape.Open;
            bool woods = (patterned ? battle.Shape == FieldShape.Grove
                    : g == Biome.Forest ? r < 0.85f : g == Biome.Hills ? r < 0.35f : g == Biome.Mountains ? r < 0.15f : r < 0.45f)
                && room >= LeastTree;
            // Where a tree's crown would be cut by the top panel, mostly a bush, and now and then
            // elsewhere: the stones that stood there instead made a row of one boulder along the
            // top edge (the visual plan of 2026-11-11, problem 1).
            bool bush = !woods && !obstacle && g != Biome.Mountains && g != Biome.Beach && room >= 0.9f
                && rng.Chance(room < LeastTree ? 0.75f : 0.25f);
            bool rock = !woods && !bush && (obstacle || g == Biome.Hills || g == Biome.Mountains || g == Biome.Beach || r < 0.7f);
            if (woods ? Tree(batch, at, rng, obstacle, room) : bush ? Bush(batch, at, rng, room) : rock ? Stone(batch, at, rng, obstacle) : Small(batch, at, rng))
                return;
            StandIn(kit, at, rng, obstacle, woods, rock);
        }

        /// <summary>
        /// One of the map's trees for the biome, one and a half to two figures tall; on a cell of
        /// the field a little lower and about as wide as the cell, so it hides little behind it.
        /// It is no taller than the <paramref name="room"/> under the top panel.
        /// </summary>
        bool Tree(ModelBatch batch, Vector3 at, Rng rng, bool obstacle, float room)
        {
            Biome g = battle.Ground;
            string[] kinds = g == Biome.Forest ? (rng.Chance(0.08f) ? PropBuilder.DeadTrees : rng.Chance(0.3f) ? PropBuilder.Conifers : PropBuilder.WoodTrees)
                : g == Biome.Mountains ? PropBuilder.Conifers
                : g == Biome.Hills ? (rng.Chance(0.25f) ? PropBuilder.Conifers : PropBuilder.HillTrees)
                : PropBuilder.OpenTrees;
            string name = kinds[rng.Range(0, kinds.Length)];
            ModelBatch.Source model = Model(name);
            if (model == null)
                return false;
            float height = PropBuilder.TreeHeight(name) / 0.42f * (obstacle ? rng.Range(2.5f, 2.8f) : rng.Range(3f, 4.3f));
            height = Mathf.Min(height, room);
            float width = obstacle ? 1.9f : height;
            Vector3 size = model.Bounds.size;
            float k = Mathf.Min(height / Mathf.Max(size.y, 1e-3f), width / Mathf.Max(Mathf.Max(size.x, size.z), 1e-3f));
            Color tint = Color.Lerp(Color.white, new Color(1.05f, 1f, 0.84f), rng.Value() * rng.Value()) * rng.Range(0.88f, 1.05f);
            float sway = kinds == PropBuilder.DeadTrees ? TreeSway * 0.4f : TreeSway;
            batch.Add(model, at - new Vector3(0f, 0.03f, 0f), rng.Range(0f, 360f), Vector3.one * k, tint, sway);
            return true;
        }

        /// <summary>
        /// A studio boulder, sometimes mossy where the land is green; on a cell of the field a plain
        /// one that fills most of it. No bigger than a boar: a larger mossy one, its pale dome under
        /// a green cap in the field's light, read as a turnip (review of 2026-11-10).
        /// </summary>
        bool Stone(ModelBatch batch, Vector3 at, Rng rng, bool obstacle)
        {
            Biome g = battle.Ground;
            bool green = g == Biome.Forest || g == Biome.Meadow || g == Biome.Hills;
            ModelBatch.Source model = (!obstacle && rng.Chance(green ? 0.15f : 0.05f) ? Model("boulder_mossy") : null) ?? Model("boulder");
            if (model == null)
                return false;
            Lay(batch, model, at, rng, obstacle ? rng.Range(1.05f, 1.2f) : rng.Range(0.45f, 1.1f));
            return true;
        }

        /// <summary>
        /// One of the map's bushes, a cell or so across and no taller than the <paramref name="room"/>
        /// under the top panel, straw-tinted on the steppe as on the map.
        /// </summary>
        bool Bush(ModelBatch batch, Vector3 at, Rng rng, float room)
        {
            int first = rng.Range(0, BushKinds.Length);
            ModelBatch.Source model = Model(BushKinds[first]) ?? Model(BushKinds[(first + 1) % BushKinds.Length]);
            if (model == null)
                return false;
            Vector3 size = model.Bounds.size;
            float k = rng.Range(1.1f, 1.7f) / Mathf.Max(Mathf.Max(size.x, size.z), 1e-3f);
            k = Mathf.Min(k, 0.95f * room / Mathf.Max(size.y, 1e-3f));
            bool parched = battle.Ground == Biome.Plains && rng.Chance(0.6f);
            Color tint = (parched ? new Color(1.12f, 1.02f, 0.64f) : Color.white) * rng.Range(0.9f, 1.04f);
            batch.Add(model, at - new Vector3(0f, 0.03f, 0f), rng.Range(0f, 360f), Vector3.one * k, tint, BushSway);
            return true;
        }

        /// <summary>
        /// Tufts of grass on the field and round it, as the map has them (PropBuilder.Tufts) at the
        /// field's size: green ones the ground's shade on meadows, hills and the forest floor, straw on
        /// the steppe and the beach, none in the mountains. On the field they keep to the cells' rims,
        /// clear of the stacks standing in the middle.
        /// </summary>
        void Tufts(MeshKit kit, Rng rng)
        {
            Biome g = battle.Ground;
            if (g == Biome.Mountains)
                return;
            bool dry = g == Biome.Plains || g == Biome.Beach;
            int wanted = g == Biome.Beach ? 14 : g == Biome.Forest ? 22 : 40;
            for (int k = 0, placed = 0; k < wanted * 8 && placed < wanted; k++)
            {
                var at = new Vector3(rng.Range(-4f, 23f), 0f, rng.Range(-3f, 12.5f));
                float nearest = float.MaxValue;
                for (int i = 0; i < battle.Blocked.Length; i++)
                    nearest = Mathf.Min(nearest, (CellAt(i) - at).sqrMagnitude);
                if (nearest < 0.62f * 0.62f)
                    continue;
                placed++;
                Color shade = Patch(at, out _, out _);
                Color low = dry ? Palette.Hex("#8F7E2A") : shade * 0.62f;
                Color high = dry ? Palette.Hex("#E3CB72") : Color.Lerp(shade, Palette.LeavesLight, 0.45f);
                float s = rng.Range(3.6f, 5.4f) * (dry ? 1.3f : 1f);
                int blades = rng.Range(7, 11);
                float turn = rng.Range(0f, Mathf.PI * 2f);
                kit.Sway(at.y, GrassSway, MeshKit.PhaseAt(at));
                for (int b = 0; b < blades; b++)
                {
                    float a = turn + b * Mathf.PI * 2f / blades + rng.Range(-0.3f, 0.3f);
                    var lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 side = new Vector3(-lean.z, 0f, lean.x) * (0.009f * s);
                    Vector3 rootAt = at + lean * (0.008f * s) - new Vector3(0f, 0.01f, 0f);
                    Vector3 tip = at + lean * (rng.Range(0.022f, 0.04f) * s) + new Vector3(0f, rng.Range(0.04f, 0.07f) * s, 0f);
                    kit.TwoSided(rootAt - side, rootAt + side, tip, Color.Lerp(low, high, rng.Range(0.3f, 1f)));
                }
                kit.Still();
            }
        }

        /// <summary>
        /// The ground's colour at a point of the field, with its patches a few cells across: lusher
        /// and drier stretches in the map's own shades of the biome (<see cref="Shade"/>) and a few
        /// spots of bare earth, fading out toward <see cref="PatchReachX"/>. The map's own drifts
        /// (Terrain.shader, Variety) are about a map cell across and read as a fine mottle this
        /// close, so the field was one flat carpet (the visual plan of 2026-11-11, problem 1).
        /// <paramref name="v"/> is the patch's shade (-1 dry … 1 lush), <paramref name="earth"/>
        /// the bare earth's share.
        /// </summary>
        Color Patch(Vector3 at, out float v, out float earth)
        {
            float away = Mathf.Max(Mathf.Abs(at.x - patchCentre.x) / PatchReachX, Mathf.Abs(at.z - patchCentre.z) / PatchReachZ);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, away));
            float x = at.x + patchSeed.x, z = at.z + patchSeed.y;
            v = (Mathf.PerlinNoise(x * 0.12f, z * 0.12f) - 0.5f) * 2.4f + (Mathf.PerlinNoise(x * 0.3f + 31.7f, z * 0.3f + 7.3f) - 0.5f) * 0.9f;
            v = Mathf.Clamp(v, -0.85f, 0.85f) * fade;
            Biome g = battle.Ground;
            bool soil = g == Biome.Meadow || g == Biome.Hills || g == Biome.Plains || g == Biome.Forest;
            earth = soil ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 0.82f, Mathf.PerlinNoise(x * 0.45f + 63.1f, z * 0.45f + 12.9f))) * fade : 0f;
            return Color.Lerp(Shade(v), Palette.Earth * 0.94f, earth * 0.5f);
        }

        /// <summary>The fight's ground in a shade <paramref name="v"/>: lusher and deeper above 0, drier, lighter or stonier below.</summary>
        Color Shade(float v)
        {
            Color g = GroundColour;
            switch (battle.Ground)
            {
                case Biome.Meadow:
                case Biome.Plains:
                    return Palette.Ground(battle.Ground, v);
                case Biome.Hills:
                    return v > 0f ? Color.Lerp(g, Palette.Meadow, v * 0.5f) : Color.Lerp(g, Palette.Plains, -v * 0.5f);
                case Biome.Forest:
                    return v > 0f ? g * (1f - 0.12f * v) : Color.Lerp(g, Palette.Hills, -v * 0.45f);
                case Biome.Mountains:
                    return v > 0f ? Color.Lerp(g, Palette.Hills, v * 0.5f) : Color.Lerp(g, Palette.Rock, -v * 0.5f);
                default:
                    return v > 0f ? Color.Lerp(g, Palette.Plains, v * 0.45f) : g;
            }
        }

        /// <summary>
        /// Gives a mesh drawn with the terrain material the field's patches (<see cref="Patch"/>) by
        /// each vertex's place, in its colours and the ground textures' shares, and returns the
        /// colours as screen colours; it casts no shadow.
        /// </summary>
        Color[] Patched(GameObject go, Vector4 a, Vector4 b)
        {
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            var colours = new Color[vertices.Length];
            var linear = new Color[vertices.Length];
            var sharesA = new List<Vector4>(vertices.Length);
            var sharesB = new List<Vector4>(vertices.Length);
            var shares = new float[GroundLayer.Count];
            for (int i = 0; i < vertices.Length; i++)
            {
                colours[i] = Patch(vertices[i], out float v, out float earth);
                linear[i] = colours[i].linear;
                linear[i].a = 1f;
                PatchShares(a, b, v, earth, shares);
                sharesA.Add(new Vector4(shares[0], shares[1], shares[2], shares[3]));
                sharesB.Add(new Vector4(shares[4], shares[5], shares[6], shares[7]));
            }
            mesh.colors = linear;
            mesh.SetUVs(1, sharesA);
            mesh.SetUVs(2, sharesB);
            return colours;
        }

        /// <summary>
        /// The ground textures' shares in a patch: the biome's (<paramref name="a"/>, <paramref name="b"/>)
        /// moved toward the drier or greener layer with the patch's shade <paramref name="v"/>, and
        /// toward bare dirt with <paramref name="earth"/>.
        /// </summary>
        void PatchShares(Vector4 a, Vector4 b, float v, float earth, float[] shares)
        {
            shares[0] = a.x;
            shares[1] = a.y;
            shares[2] = a.z;
            shares[3] = a.w;
            shares[4] = b.x;
            shares[5] = b.y;
            shares[6] = b.z;
            shares[7] = b.w;
            switch (battle.Ground)
            {
                case Biome.Meadow:
                case Biome.Hills:
                    if (v < 0f)
                        Toward(shares, GroundLayer.DryGrass, -v * 0.6f);
                    break;
                case Biome.Plains:
                    if (v > 0f)
                        Toward(shares, GroundLayer.Grass, v * 0.5f);
                    break;
                case Biome.Forest:
                    if (v < 0f)
                        Toward(shares, GroundLayer.Grass, -v * 0.5f);
                    break;
                case Biome.Mountains:
                    if (v < 0f)
                        Toward(shares, GroundLayer.Scree, -v * 0.5f);
                    break;
                default:
                    if (v > 0f)
                        Toward(shares, GroundLayer.DryGrass, v * 0.5f);
                    break;
            }
            Toward(shares, GroundLayer.Dirt, earth * 0.7f);
        }

        /// <summary>Moves a share <paramref name="t"/> of every layer over to <paramref name="layer"/>.</summary>
        static void Toward(float[] shares, int layer, float t)
        {
            for (int k = 0; k < GroundLayer.Count; k++)
                shares[k] *= 1f - t;
            shares[layer] += t;
        }

        /// <summary>Something small on the ground: a fallen log or mushrooms in the woods, mushrooms or pebbles elsewhere.</summary>
        bool Small(ModelBatch batch, Vector3 at, Rng rng)
        {
            Biome g = battle.Ground;
            float r = rng.Value();
            string name = g == Biome.Forest ? (r < 0.5f ? "log" : "mushrooms")
                : (g == Biome.Meadow || g == Biome.Hills) && r < 0.3f ? "mushrooms" : "pebbles";
            ModelBatch.Source model = Model(name);
            if (model == null)
                return false;
            Lay(batch, model, at, rng, name == "log" ? rng.Range(1.3f, 1.9f) : name == "mushrooms" ? rng.Range(0.5f, 0.8f) : rng.Range(0.8f, 1.3f));
            return true;
        }

        /// <summary>A model lying on the ground, <paramref name="width"/> across, sunk a little, turned any way.</summary>
        static void Lay(ModelBatch batch, ModelBatch.Source model, Vector3 at, Rng rng, float width)
        {
            Vector3 size = model.Bounds.size;
            float k = width / Mathf.Max(Mathf.Max(size.x, size.z), 1e-3f);
            batch.Add(model, at - new Vector3(0f, 0.04f * width, 0f), rng.Range(0f, 360f), Vector3.one * k,
                Color.white * rng.Range(0.9f, 1.04f));
        }

        /// <summary>
        /// The map's chest on a cell of the field (the combat plan's 3.5: «Сундук — существующая
        /// модель»), turned a little toward the camera; a low wooden box while the art lacks it.
        /// </summary>
        Transform FieldChest(Vector3 at)
        {
            var holder = new GameObject("Field chest").transform;
            holder.SetParent(root, false);
            holder.localPosition = at;
            holder.localRotation = Quaternion.Euler(0f, -20f, 0f);
            GameObject prefab = art != null ? art.ModelOf("chest") : null;
            if (prefab != null)
            {
                GameObject chest = Instantiate(prefab, holder, false);
                chest.transform.localPosition = Vector3.zero;
                chest.transform.localScale = Vector3.one * ChestWidth;
                foreach (MeshRenderer r in chest.GetComponentsInChildren<MeshRenderer>())
                    r.shadowCastingMode = ShadowCastingMode.On;
            }
            else
            {
                var kit = new MeshKit();
                kit.Box(Vector3.zero, new Vector3(0.5f, 0.28f, 0.33f), 0f, Palette.Wood);
                kit.Box(new Vector3(0f, 0.28f, 0f), new Vector3(0.52f, 0.09f, 0.35f), 0f, Palette.Gold);
                Figures.Built(kit, "Figure chest", material, holder);
            }
            return holder;
        }

        /// <summary>The low-poly stand-ins: a tree, a rock or a bush.</summary>
        void StandIn(MeshKit kit, Vector3 at, Rng rng, bool obstacle, bool woods, bool rock)
        {
            Biome g = battle.Ground;
            float s = obstacle ? rng.Range(0.95f, 1.1f) : rng.Range(0.8f, 1.35f);
            if (woods)
            {
                float trunk = 0.65f * s;
                kit.Lathe(at - new Vector3(0, 0.05f, 0), new[] { 0f, trunk }, new[] { 0.13f * s, 0.1f * s }, new[] { Palette.Trunk }, 6, rng);
                if (g == Biome.Forest && rng.Chance(0.6f) || g == Biome.Mountains)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float y = (0.35f + 0.38f * k) * s, radius = (0.78f - 0.18f * k) * s, h = (0.95f - 0.1f * k) * s;
                        Color green = Color.Lerp(Palette.Pine, Palette.PineLight, k / 2f);
                        kit.Lathe(at + new Vector3(0, y, 0), new[] { 0f, h }, new[] { radius, 0f }, new[] { green }, 8, rng, 0.08f, 0.1f);
                    }
                }
                else
                {
                    kit.Blob(at + new Vector3(0, 1.15f * s, 0), new Vector3(0.75f, 0.65f, 0.75f) * s, 8, 4,
                        Palette.Leaves * 0.86f, Palette.LeavesLight, rng, 0.12f);
                }
            }
            else if (rock)
            {
                float radius = (obstacle ? 0.6f : rng.Range(0.3f, 0.8f)) * s;
                kit.Blob(at + new Vector3(0, radius * 0.3f, 0), new Vector3(radius, radius * 0.75f, radius * 0.9f), 7, 3,
                    Palette.RockDark, Palette.Boulder, rng, 0.2f, stone: true);
            }
            else
            {
                float radius = (obstacle ? 0.55f : rng.Range(0.3f, 0.5f)) * s;
                kit.Blob(at + new Vector3(0, radius * 0.45f, 0), new Vector3(radius, radius * 0.7f, radius), 7, 3,
                    Palette.Leaves * 0.8f, Palette.LeavesLight, rng, 0.15f);
            }
        }

        void Spawn(Fighter f)
        {
            var holder = new GameObject("Stack " + f.Id).transform;
            holder.SetParent(root, false);
            holder.localPosition = CellAt(f.Cell);
            holder.localRotation = Rest(f);
            // The monsters keep the look their group has on the map; the players' units have one.
            int look = f.Side == Side.Players ? -1 : site;
            GameObject figure = Figures.Unit(f.Unit, art, material, holder, FigureSize(f.Unit), look);
            string model = PropBuilder.ModelOf(f.Unit, look, art);
            if (Facings.TryGetValue(model, out float facing) && art != null && art.ModelOf(model) != null)
                figure.transform.localRotation = Quaternion.Euler(0f, facing, 0f);
            Light(figure, f.Unit);
            // A creature with its own moves stands in its captured loop; the others breathe as
            // drawn, Idle keeping the turn they start with.
            if (figure.TryGetComponent(out Moves moves) && moves.Has(Moves.Stand))
                moves.hold = Moves.Stand;
            else
                figure.AddComponent<Idle>().look = 6f;
            figures[f.Id] = holder;
            counts[f.Id] = hud.AddTag(f.Side == Side.Players ? "battle-count" : "battle-count-enemy", f.Count.ToString(),
                () => holder.position + PlateAt);
            if (f.Side == Side.Players)
                Hud.Rim(counts[f.Id], hud.Mine);
        }

        float FigureSize(Unit unit) => Mathf.Min(PropBuilder.SizeOf(unit) * figureScale, MaxFigure);

        /// <summary>
        /// Gives the figure the light along its outline and lifts a dark model's paint to
        /// <see cref="LeastLightness"/> (Oklab lightness goes with the cube root of luminance),
        /// for this figure only.
        /// </summary>
        static void Light(GameObject figure, Unit unit)
        {
            float lift = DarkModels.TryGetValue(PropBuilder.ModelOf(unit), out float lightness) ? Mathf.Pow(LeastLightness / lightness, 3f) : 1f;
            var block = new MaterialPropertyBlock();
            foreach (Renderer renderer in figure.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                block.SetVector(RimLightId, RimLight);
                Material paint = renderer.sharedMaterial;
                if (lift > 1f && paint != null && paint.HasProperty(BaseColorId))
                {
                    Color c = paint.GetColor(BaseColorId).linear * lift;
                    c.a = 1f;
                    block.SetVector(BaseColorId, c);
                }
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>The players' stacks look right and the monsters left, both a little towards the camera.</summary>
        static Quaternion Rest(Fighter f) => Quaternion.Euler(0f, f.Side == Side.Players ? -60f : 60f, 0f);

        /// <summary>
        /// Frames the field between the top panel and the bottom bar: the camera comes as near as
        /// the outer cells, the near row's count plates and the tallest figure's head in the far
        /// row allow, and the field is centred between the panels.
        /// </summary>
        void Aim()
        {
            cam = mapCamera.GetComponent<Camera>();
            mapCamera.enabled = false;
            cameraPosition = cam.transform.position;
            cameraRotation = cam.transform.rotation;
            mapFieldOfView = cam.fieldOfView;
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 200f;

            float pixels = Mathf.Max(Screen.height, 1);
            hud.BattlePanels(out float topPanel, out float bottomBar, out float plateHalf);
            panelTop = 1f - 2f * topPanel / pixels;
            float top = 1f - 2f * (topPanel + PanelMargin) / pixels, bottom = -1f + 2f * (bottomBar + PanelMargin) / pixels;
            float plate = 2f * plateHalf / pixels;

            Quaternion rotation = Quaternion.Euler(Pitch, 0f, 0f);
            Vector3 forward = rotation * Vector3.forward, up = rotation * Vector3.up, right = rotation * Vector3.right;
            float tanV = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad), tanH = tanV * cam.aspect;
            Vector3 target = Origin + Centre + (ShowsHero ? Vector3.left * HeroShift : Vector3.zero);

            // What must show, seen from the target: each point's depth, height and sideways offset
            // in the camera's axes, and how far below it on the screen its thing reaches.
            float tallest = 0f;
            foreach (Fighter f in battle.Fighters)
                tallest = Mathf.Max(tallest, FigureSize(f.Unit));
            var points = new List<Vector4>();
            void Keep(Vector3 p, float below)
            {
                Vector3 v = p - target;
                points.Add(new Vector4(Vector3.Dot(v, forward), Vector3.Dot(v, up), Vector3.Dot(v, right), below));
            }
            for (int i = 0; i < battle.Blocked.Length; i++)
            {
                Hex h = Battle.HexOf(i);
                bool near = h.Row == 0, far = h.Row == Battle.Height - 1;
                if (!near && !far && h.Col != 0 && h.Col != Battle.Width - 1)
                    continue;
                Vector3 c = Origin + CellAt(i);
                for (int k = 0; k < 6; k++)
                    Keep(c + new Vector3(HexMetrics.CornerX[k], 0f, HexMetrics.CornerZ[k]), 0f);
                if (near)
                    Keep(c + PlateAt, plate);
                if (far)
                    Keep(c + new Vector3(0f, tallest, 0f), 0f);
            }
            if (ShowsHero)
            {
                Keep(Origin + HeroSpot + Vector3.left * HeroHalf, 0f);
                Keep(Origin + HeroSpot + new Vector3(-HeroHalf, HeroSize + 0.6f, 0f), 0f);
            }

            // Lifting the camera along its up moves every point down the screen and changes neither
            // depth nor sideways place: the lift that leaves as much room above as below.
            float Spare(float distance, float lift, out float above, out float below)
            {
                above = below = float.PositiveInfinity;
                foreach (Vector4 p in points)
                {
                    float depth = p.x + distance, y = (p.y - lift) / (depth * tanV);
                    above = Mathf.Min(above, top - y);
                    below = Mathf.Min(below, y - p.w - bottom);
                }
                return above - below;
            }
            float Lift(float distance)
            {
                float low = -30f, high = 30f;
                for (int step = 0; step < 32; step++)
                {
                    float middle = 0.5f * (low + high);
                    if (Spare(distance, middle, out _, out _) < 0f)
                        low = middle;
                    else
                        high = middle;
                }
                return 0.5f * (low + high);
            }
            bool Fits(float distance)
            {
                foreach (Vector4 p in points)
                {
                    float depth = p.x + distance;
                    if (depth < 1f || Mathf.Abs(p.z / (depth * tanH)) > 1f - SideMargin)
                        return false;
                }
                Spare(distance, Lift(distance), out float above, out _);
                return above >= 0f;
            }

            float closest = 4f, farthest = 120f;
            for (int step = 0; step < 32; step++)
            {
                float middle = 0.5f * (closest + farthest);
                if (Fits(middle))
                    farthest = middle;
                else
                    closest = middle;
            }
            float shift = Lift(farthest), deepest = 0f;
            foreach (Vector4 p in points)
                deepest = Mathf.Max(deepest, p.x + farthest);
            cam.transform.SetPositionAndRotation(target - forward * farthest + up * shift, rotation);
            // The field's far side and the ground beyond it keep their shadows.
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
                pipeline.shadowDistance = deepest + 8f;
        }

        /// <summary>
        /// How tall something standing on the field's ground at the point can be with its top still
        /// below the top panel; 0 or less where its foot is under the panel already.
        /// </summary>
        float Room(Vector3 at)
        {
            Transform eye = cam.transform;
            Vector3 v = Origin + at - eye.position, forward = eye.forward, up = eye.up;
            float depth = Vector3.Dot(v, forward), y = Vector3.Dot(v, up);
            if (depth <= 0f)
                return 0f;
            float edge = panelTop * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return (edge * depth - y) / Mathf.Max(up.y - edge * forward.y, 1e-3f);
        }

        /// <summary>Colours the cells: where the current stack can go, the target under the pointer and the cell it strikes from.</summary>
        void Paint()
        {
            if (battle == null || tiles == null)
                return;
            Color ground = GroundColour;
            Fighter f = battle.Current;
            bool mine = waiting && f != null && f.Side == Side.Players;
            var reach = mine ? new HashSet<int>(battle.Reachable()) : new HashSet<int>();
            Fighter target = hover >= 0 ? battle.At(hover) : null;
            bool enemy = mine && target != null && target.Side != f.Side;
            bool shoot = enemy && battle.CanShoot(f);
            int from = enemy && !shoot ? battle.StrikeCell(target, pointer.x, pointer.z) : -1;
            HashSet<int> zone = CoverZone();
            // The cells a monster under the pointer could strike next turn (the combat plan's 3.1), outlined.
            HashSet<int> danger = target != null && target.Side == Side.Monsters && battle.Outcome == Outcome.Running
                ? battle.StrikeZone(target)
                : null;
            int perCell = tileColours.Length / battle.Blocked.Length, perSide = (perCell - FillVertices) / 6;
            for (int i = 0; i < battle.Blocked.Length; i++)
            {
                Hex h = Battle.HexOf(i);
                float checker = (h.Col + h.Row) % 2 == 0 ? 0.075f : 0.035f;
                bool blocked = battle.Blocked[i], inReach = reach.Contains(i);
                Fighter here = battle.At(i);
                bool foe = mine && here != null && here.Side != f.Side;
                bool pointed = i == hover && mine, strikeFrom = from >= 0 && i == from && from != f.Cell;

                // The cell's colour over the ground's own under each vertex, with its patches.
                Color Over(Color g)
                {
                    Color c = Color.Lerp(g, Color.white, checker);
                    if (blocked)
                        c = g * 0.92f;
                    else if (inReach)
                        c = Color.Lerp(c, Color.white, 0.42f);
                    if (foe)
                        c = Color.Lerp(c, Red, 0.3f);
                    if (pointed)
                    {
                        if (enemy)
                            c = Color.Lerp(c, Red, shoot || from >= 0 ? 0.75f : 0.35f);
                        else if (inReach)
                            c = Color.Lerp(c, Color.white, 0.75f);
                    }
                    if (strikeFrom)
                        c = Color.Lerp(c, Color.white, 0.8f);
                    return c;
                }

                for (int k = 0; k < perCell; k++)
                {
                    int v = i * perCell + k;
                    Color c = Over(tileGround != null && v < tileGround.Length ? tileGround[v] : ground);
                    if (k >= FillVertices && danger != null && Outline(danger, target.Cell, i, (k - FillVertices) / perSide))
                        c = Color.Lerp(c, Red, DangerLine);
                    else if (k >= FillVertices)
                        c = zone.Contains(i) ? Color.Lerp(c, hud.Mine, CoverRim) : Color.Lerp(c, Color.white, LineLight);
                    Color linear = c.linear;
                    linear.a = 1f;
                    tileColours[v] = linear;
                }
            }
            tiles.colors = tileColours;
            Mark(battle.Outcome == Outcome.Running ? f : null);
            MarkCovered();
        }

        /// <summary>
        /// Whether the outline of a monster's reach (<paramref name="danger"/>, and its own cell
        /// <paramref name="home"/>) runs along this side of cell <paramref name="i"/>: between a
        /// cell in it and one out of it, or the field's edge. Rocks break the line rather than ring it.
        /// </summary>
        bool Outline(HashSet<int> danger, int home, int i, int side)
        {
            int n = sides[i * 6 + side];
            if (battle.Blocked[i] || n >= 0 && battle.Blocked[n])
                return false;
            bool inside = i == home || danger.Contains(i);
            return inside != (n >= 0 && (n == home || danger.Contains(n)));
        }

        /// <summary>The cell whose centre lies <paramref name="way"/> from cell <paramref name="i"/>'s (twice a side's middle), or -1 off the field.</summary>
        static int Across(int i, Vector3 way)
        {
            Vector3 at = CellAt(i) + way;
            foreach (int n in Battle.NeighboursOf(i))
            {
                if ((CellAt(n) - at).sqrMagnitude < 0.1f)
                    return n;
            }
            return -1;
        }

        /// <summary>
        /// The cells round the players' spearmen whose cover is unspent this round (the combat
        /// plan's 3.3): their rims take the kingdom's colour. An enemy that strikes from one of
        /// them at the spearmen or at a stack beside them is struck first.
        /// </summary>
        HashSet<int> CoverZone()
        {
            var zone = new HashSet<int>();
            if (battle.Outcome != Outcome.Running)
                return zone;
            foreach (Fighter g in battle.Fighters)
            {
                if (!g.Alive || g.Side != Side.Players || g.Covered || !g.Stats.Has(Ability.Cover))
                    continue;
                foreach (int n in Battle.NeighboursOf(g.Cell))
                {
                    if (!battle.Blocked[n] && battle.At(n) != g)
                        zone.Add(n);
                }
            }
            return zone;
        }

        /// <summary>A small shield on the plate of each players' stack that stands beside spearmen whose cover is unspent.</summary>
        void MarkCovered()
        {
            foreach (KeyValuePair<int, UnityEngine.UIElements.Label> plate in counts)
            {
                Fighter f = battle.Fighters[plate.Key];
                bool covered = false;
                if (battle.Outcome == Outcome.Running && f.Alive && f.Side == Side.Players)
                {
                    foreach (int n in Battle.NeighboursOf(f.Cell))
                    {
                        Fighter g = battle.At(n);
                        if (g != null && g.Side == Side.Players && !g.Covered && g.Stats.Has(Ability.Cover))
                            covered = true;
                    }
                }
                hud.CoverMark(plate.Value, covered);
            }
        }

        /// <summary>
        /// Lays the ring round the acting stack, in the player's kingdom colour or the monsters'
        /// bone, or takes it away; it follows the stack while it walks (<see cref="LateUpdate"/>).
        /// </summary>
        void Mark(Fighter f)
        {
            if (ring == null)
                return;
            ringOf = f != null ? f.Id : -1;
            ring.gameObject.SetActive(f != null);
            if (f == null)
                return;
            Color colour = f.Side == Side.Players ? hud.Mine : Bone;
            if (colour != ringColour)
            {
                ringColour = colour;
                Color linear = colour.linear;
                linear.a = 1f;
                var colours = new Color[ringMesh.vertexCount];
                for (int k = 0; k < colours.Length; k++)
                    colours[k] = linear;
                ringMesh.colors = colours;
            }
            LateUpdate();
        }

        void LateUpdate()
        {
            UpdateAims();
            if (ring == null || ringOf < 0 || !ring.gameObject.activeSelf)
                return;
            if (!figures.TryGetValue(ringOf, out Transform holder) || shown[ringOf] <= 0)
            {
                ring.gameObject.SetActive(false);
                return;
            }
            Vector3 at = holder.localPosition;
            ring.localPosition = new Vector3(at.x, RingLift, at.z);
        }

        // ----- Turns -----

        IEnumerator Run()
        {
            while (true)
            {
                yield return PlayEvents();
                hud.BattleQueue(battle.Queue(), battle.NextRound(), shown, rounds);
                yield return new WaitWhile(() => hud.Paused);
                if (battle.Outcome != Outcome.Running || battle.Current == null)
                    break;
                if (battle.Offered)
                {
                    yield return AskSurrender();
                    continue;
                }
                Fighter f = battle.Current;
                if (f.Side == Side.Monsters || auto)
                {
                    Paint();
                    yield return Pause(f.Side == Side.Monsters ? 0.35f : 0.12f);
                    yield return new WaitWhile(() => hud.Paused);
                    if (battle.Outcome == Outcome.Running && battle.Current == f)
                        battle.Auto();
                    continue;
                }
                waiting = true;
                acted = false;
                string who = Words.Count(f.Unit, f.Count);
                if (battle.ActsAgain(f))
                    who += T(" — ещё раз, вполсилы", " — once more, at half strength");
                hud.BattleTurn(who, battle.CanShoot(f));
                hud.BattleOrders(Battle.OrdersPerBattle > 0, battle.CanOrder);
                Paint();
                float handsAt = Time.time + Hands;
                yield return new WaitUntil(() => acted || auto || battle.Outcome != Outcome.Running || (Hands > 0f && Time.time >= handsAt));
                if (!acted && !auto && Hands > 0f && battle.Outcome == Outcome.Running && battle.Current == f)
                    battle.Auto();
                waiting = false;
                hover = -1;
                hud.BattleInfo(null, null);
                // After an order the same stack's turn goes on: its line stays while the order shows.
                if (battle.Outcome != Outcome.Running || battle.Current != f)
                    hud.BattleTurn(null, false);
                hud.BattleOrders(Battle.OrdersPerBattle > 0, false);
                Paint();
            }
            yield return PlayEvents();
            if (battle.Surrendered)
                yield return Flee();
            // The last beaten creature finishes its fall before the battle ends.
            while (Time.time < fallsEnd - 0.6f)
                yield return null;
            Paint();
            // Lost or left: the plates go grey, quietly; a loss only slows the game down (the
            // visual plan's proposal 7). The figures stay as they are: no creature is animated.
            if (battle.Outcome != Outcome.Won)
            {
                foreach (UnityEngine.UIElements.Label plate in counts.Values)
                {
                    plate.AddToClassList("battle-count-lost");
                    Hud.Rim(plate, LostRim);
                }
            }
            yield return Pause(0.6f);
        }

        /// <summary>
        /// A white rag on a stick raised beside each stack of people that offers to give up: undyed
        /// cloth (<see cref="FlagArt.Undyed"/>) that waves like the banners, going up with the stack
        /// as it leaves. Beasts back off without one. The visual plan's proposal 7 (2026-11-11), the
        /// rag rather than the figures moving, as the players keep creatures still.
        /// </summary>
        void RaiseWhiteFlags()
        {
            if (FlagArt.ClothMaterial == null)
                return;
            foreach (Fighter m in battle.Fighters)
            {
                if (m.Side != Side.Monsters || !m.Alive || Words.Beasts(m.Unit) || whiteFlags.ContainsKey(m.Id)
                    || !figures.TryGetValue(m.Id, out Transform holder))
                    continue;
                var flag = new GameObject("Figure white flag").transform;
                flag.SetParent(holder, false);
                flag.localPosition = new Vector3(0.34f, 0f, 0.24f);
                var top = new Vector3(0f, FigureSize(m.Unit) + 0.75f, 0f);
                var pole = new MeshKit();
                pole.Cylinder(Vector3.zero, top, 0.022f, 6, Palette.Wood, Palette.Wood);
                Figures.Built(pole, "Figure white flag pole", material, flag);
                var cloth = new FlagCloth();
                cloth.Add(top - new Vector3(0f, 0.04f, 0f), new Vector3(-1f, 0f, 0.15f), 0.55f, 0.36f, FlagArt.Undyed);
                var go = new GameObject("Figure white flag cloth");
                go.transform.SetParent(flag, false);
                go.AddComponent<MeshFilter>().sharedMesh = cloth.ToMesh("Figure white flag");
                go.AddComponent<MeshRenderer>().sharedMaterial = FlagArt.ClothMaterial;
                whiteFlags[m.Id] = flag;
                StartCoroutine(Raise(flag));
            }
        }

        /// <summary>The rags go down: the players chose to fight on.</summary>
        void LowerWhiteFlags()
        {
            foreach (Transform flag in whiteFlags.Values)
            {
                if (flag != null)
                    Figures.Destroy(flag.gameObject);
            }
            whiteFlags.Clear();
        }

        /// <summary>A rag goes up from the ground in under half a second, a little past its height and back.</summary>
        static IEnumerator Raise(Transform flag)
        {
            for (float t = 0f; t < 0.45f && flag != null; t += Time.deltaTime)
            {
                float k = t / 0.45f;
                float height = k < 0.7f ? 1.06f * (1f - Mathf.Pow(1f - k / 0.7f, 3f)) : Mathf.Lerp(1.06f, 1f, (k - 0.7f) / 0.3f);
                flag.localScale = new Vector3(1f, Mathf.Max(height, 0.01f), 1f);
                yield return null;
            }
            if (flag != null)
                flag.localScale = Vector3.one;
        }

        /// <summary>Where the monsters stood when the fight ended, the beaten ones too, a little above the ground: the spoils fly from there.</summary>
        public List<Vector3> FoePoints()
        {
            var points = new List<Vector3>();
            if (battle == null || root == null)
                return points;
            foreach (Fighter f in battle.Fighters)
            {
                if (f.Side == Side.Monsters)
                    points.Add(root.TransformPoint(CellAt(f.Cell) + Vector3.up * 0.6f));
            }
            return points;
        }

        IEnumerator PlayEvents()
        {
            while (played < battle.Events.Count)
                yield return Animate(battle.Events[played++]);
        }

        IEnumerator Animate(BattleEvent e)
        {
            switch (e.Kind)
            {
                case BattleEventKind.Round:
                    rounds++;
                    if (rounds > 1)
                        Sounds.Play(Sfx.Round, 0.8f);
                    hud.BattleQueue(battle.Queue(), battle.NextRound(), shown, rounds);
                    break;
                case BattleEventKind.Walk:
                {
                    Transform figure = figures[e.Actor];
                    Unit unit = battle.Fighters[e.Actor].Unit;
                    Sfx step = unit == Unit.Riders && Sounds.Has(Sfx.StepHoof) ? Sfx.StepHoof : Sfx.StepGrass;
                    Moves moves = Runner(figure);
                    foreach (int cell in e.Path)
                    {
                        yield return moves != null ? Run(figure, moves, CellAt(cell), unit) : Step(figure, CellAt(cell));
                        Sounds.PlayAt(step, figure.position, 0.5f);
                    }
                    if (moves != null)
                        moves.Loop(Moves.Stand, 1f, 0.3f);
                    figure.localRotation = Rest(battle.Fighters[e.Actor]);
                    break;
                }
                case BattleEventKind.Strike:
                case BattleEventKind.StrikeBack:
                    yield return Lunge(e);
                    break;
                case BattleEventKind.Order:
                    yield return Ordered(e);
                    break;
                case BattleEventKind.Cover:
                    // The spears' cover (the combat plan's 3.3): the spearmen lunge first, with a shout.
                    CoversShown++;
                    hud.Float(T("Копья!", "Spears!"), "float-cover", Head(e.Actor));
                    yield return Lunge(e);
                    break;
                case BattleEventKind.Shoot:
                    yield return Shoot(e);
                    break;
                case BattleEventKind.Defend:
                    Sounds.PlayAt(Sfx.Defend, figures[e.Actor].position);
                    hud.Float(T("Защита!", "Defend!"), "float-defend", Head(e.Actor));
                    yield return Pause(0.4f);
                    break;
                case BattleEventKind.Chest:
                    // «Добыча» (the combat plan's 3.5): the stack stands on the chest, which opens
                    // and goes, its gold shown over the stack.
                    ChestsShown++;
                    Sounds.PlayAt(Sfx.ChestOpen, figures[e.Actor].position);
                    hud.Float(Words.ChestTaken(e.Damage), "float-chest", Head(e.Actor));
                    if (chestFigure != null)
                        chestFigure.gameObject.SetActive(false);
                    yield return Pause(0.5f);
                    break;
                case BattleEventKind.Heal:
                    Sounds.PlayAt(Sfx.Heal, figures[e.Actor].position);
                    hud.Float($"+{e.Damage}", "float-heal", Head(e.Actor));
                    yield return Pause(0.35f);
                    break;
                case BattleEventKind.Web:
                    Sounds.PlayAt(Sfx.Web, figures[e.Target].position);
                    hud.Float(T("В паутине!", "Caught in a web!"), "float-web", Head(e.Target));
                    yield return Pause(0.3f);
                    break;
                case BattleEventKind.Surrender:
                    Sounds.Play(Sfx.Surrender);
                    foreach (Fighter m in battle.Fighters)
                    {
                        if (m.Side == Side.Monsters && m.Alive)
                            hud.Float(Words.Yields(m.Unit, m.Count), "float-yield", Head(m.Id));
                    }
                    RaiseWhiteFlags();
                    yield return Pause(0.7f);
                    break;
            }
        }

        float Dt => Time.deltaTime * (auto ? 1.8f : 1f);

        IEnumerator Pause(float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt)
                yield return null;
        }

        Vector3 Head(int id) => figures[id].position + Vector3.up * (FigureSize(battle.Fighters[id].Unit) + 0.15f);

        static void Face(Transform figure, Vector3 towards)
        {
            Vector3 way = towards - figure.localPosition;
            if (way.sqrMagnitude > 1e-4f)
                figure.localRotation = Quaternion.Euler(0f, Mathf.Atan2(-way.x, -way.z) * Mathf.Rad2Deg, 0f);
        }

        IEnumerator Step(Transform figure, Vector3 to)
        {
            Vector3 from = figure.localPosition;
            Face(figure, to);
            for (float t = 0f; t < 1f; t += Dt / 0.14f)
            {
                figure.localPosition = Vector3.Lerp(from, to, t) + Vector3.up * (0.18f * Mathf.Sin(Mathf.PI * t));
                yield return null;
            }
            figure.localPosition = to;
        }

        /// <summary>The moves of a creature on the field that has its own run, or null.</summary>
        static Moves Runner(Transform figure)
        {
            Moves moves = figure.GetComponentInChildren<Moves>();
            return moves != null && moves.Has(Moves.Run) && moves.runSpeed > 0f ? moves : null;
        }

        /// <summary>
        /// A creature with its own run crosses a cell in <see cref="RunCell"/> seconds, its feet
        /// on the ground, the run clip sped up towards that pace.
        /// </summary>
        IEnumerator Run(Transform figure, Moves moves, Vector3 to, Unit unit)
        {
            Vector3 from = figure.localPosition;
            Face(figure, to);
            float pace = Vector3.Distance(from, to) / RunCell, own = moves.runSpeed * FigureSize(unit);
            moves.Loop(Moves.Run, Mathf.Clamp(pace / own, 0.7f, RunPlayback) * (auto ? 1.8f : 1f), 0.12f);
            for (float t = 0f; t < 1f; t += Dt / RunCell)
            {
                figure.localPosition = Vector3.Lerp(from, to, t);
                yield return null;
            }
            figure.localPosition = to;
        }

        /// <summary>
        /// The striker jumps at the target and back; the blow lands at the top. With a run-up
        /// (the combat plan's 3.3) the jump goes further and lands with a bigger number. A
        /// creature with its own bite bites instead (<see cref="Bite"/>).
        /// </summary>
        IEnumerator Lunge(BattleEvent e)
        {
            Transform a = figures[e.Actor], t = figures[e.Target];
            Moves moves = a.GetComponentInChildren<Moves>();
            if (moves != null && moves.Has(Moves.Bite) && moves.strike > 0f)
            {
                yield return Bite(e, moves);
                yield break;
            }
            Vector3 home = a.localPosition, reach = Vector3.Lerp(home, t.localPosition, e.RunUp ? RunUpReach : 0.42f);
            Face(a, t.localPosition);
            Sounds.PlayAt(Sfx.Swing, a.position, e.RunUp ? 1f : 0.8f);
            for (float k = 0f; k < 1f; k += Dt / (e.RunUp ? 0.16f : 0.13f))
            {
                a.localPosition = Vector3.Lerp(home, reach, k * k) + Vector3.up * (0.12f * Mathf.Sin(Mathf.PI * k));
                yield return null;
            }
            Hit(e);
            for (float k = 0f; k < 1f; k += Dt / 0.18f)
            {
                a.localPosition = Vector3.Lerp(reach, home, k);
                yield return null;
            }
            a.localPosition = home;
            a.localRotation = Rest(battle.Fighters[e.Actor]);
            yield return Pause(0.22f);
        }

        /// <summary>
        /// A creature's own bite, at the pace it was packed with (the pack takes the video's slow
        /// motion out): it plays from a little before the head draws back, the figure closing in
        /// on the target for the part of the way the head does not go by itself, and the blow
        /// lands when the head is furthest forward; then the clip steps back to where it started,
        /// the figure with it, and it stands.
        /// </summary>
        IEnumerator Bite(BattleEvent e, Moves moves)
        {
            Transform a = figures[e.Actor], t = figures[e.Target];
            Vector3 home = a.localPosition, way = t.localPosition - home;
            way.y = 0f;
            float reach = way.magnitude * (e.RunUp ? RunUpReach : 0.42f);
            float rest = Mathf.Max(0f, reach - moves.biteReach * FigureSize(battle.Fighters[e.Actor].Unit));
            Vector3 close = home + (way.sqrMagnitude > 1e-6f ? way.normalized * rest : Vector3.zero);
            Face(a, t.localPosition);
            Sounds.PlayAt(Sfx.Swing, a.position, e.RunUp ? 1f : 0.8f);
            float from = Mathf.Max(0f, moves.windup - Moves.Lead);
            moves.Once(Moves.Bite, from, auto ? 1.8f : 1f, 0.1f);
            float strike = Mathf.Max(0.05f, moves.strike - from);
            for (float k = 0f; k < 1f; k += Dt / strike)
            {
                a.localPosition = Vector3.Lerp(home, close, k * k);
                yield return null;
            }
            a.localPosition = close;
            Hit(e);
            // The rest of the clip: the tug and the steps back to where it stood, the figure
            // going home with it.
            float after = Mathf.Max(0.2f, moves.Length(Moves.Bite) - moves.strike);
            for (float k = 0f; k < 1f; k += Dt / after)
            {
                a.localPosition = Vector3.Lerp(close, home, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            a.localPosition = home;
            moves.Loop(Moves.Stand, 1f, 0.25f);
            a.localRotation = Rest(battle.Fighters[e.Actor]);
            yield return Pause(0.22f);
        }

        /// <summary>An arrow flies in an arc from the shooter to the target.</summary>
        IEnumerator Shoot(BattleEvent e)
        {
            Transform a = figures[e.Actor], t = figures[e.Target];
            Face(a, t.localPosition);
            Sounds.PlayAt(Sfx.BowShot, a.position);
            yield return Pause(0.1f);
            Vector3 from = a.localPosition + Vector3.up * 0.7f, to = t.localPosition + Vector3.up * 0.55f;
            float seconds = 0.2f + 0.025f * Vector3.Distance(from, to), lift = 0.12f * Vector3.Distance(from, to);
            arrow.gameObject.SetActive(true);
            Vector3 last = from;
            for (float k = 0f; k < 1f; k += Dt / seconds)
            {
                Vector3 p = Vector3.Lerp(from, to, k) + Vector3.up * (lift * 4f * k * (1f - k));
                arrow.localPosition = p;
                if ((p - last).sqrMagnitude > 1e-6f)
                    arrow.localRotation = Quaternion.LookRotation(p - last);
                last = p;
                yield return null;
            }
            arrow.gameObject.SetActive(false);
            Hit(e);
            a.localRotation = Rest(battle.Fighters[e.Actor]);
            yield return Pause(0.25f);
        }

        void Hit(BattleEvent e)
        {
            shown[e.Target] = Mathf.Max(0, shown[e.Target] - e.Killed);
            if (counts.TryGetValue(e.Target, out var label))
                label.text = shown[e.Target].ToString();
            hud.Float($"−{e.Damage}", e.RunUp ? "float-hit float-runup" : "float-hit", Head(e.Target));
            if (e.RunUp)
                RunUpsShown++;
            // An arrow thunks; blades knock on shields; the beasts' blows are thick thuds.
            Unit striker = battle.Fighters[e.Actor].Unit;
            Sfx blow = e.Kind == BattleEventKind.Shoot ? Sfx.ArrowHit
                : striker is Unit.Boars or Unit.Wolves or Unit.Spiders or Unit.Troll ? Sfx.HitBeast : Sfx.HitBlade;
            Sounds.PlayAt(blow, figures[e.Target].position);
            if (shown[e.Target] == 0)
                StartCoroutine(Fall(e.Target));
            else
                Flinch(figures[e.Target]);
        }

        /// <summary>
        /// A creature with its own flinch (<see cref="Moves.Hurt"/>) recoils from the blow and
        /// stands again; the other figures shake.
        /// </summary>
        void Flinch(Transform figure)
        {
            Moves moves = figure.GetComponentInChildren<Moves>();
            if (moves == null || !moves.Once(Moves.Hurt, 0f, auto ? 1.8f : 1f, 0.08f, Moves.Stand))
                StartCoroutine(Shake(figure));
        }

        IEnumerator Shake(Transform figure)
        {
            Vector3 home = figure.localPosition;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.25f)
            {
                figure.localPosition = home + new Vector3(0.1f * Mathf.Sin(k * 40f) * (1f - k), 0f, 0f);
                yield return null;
            }
            figure.localPosition = home;
        }

        /// <summary>
        /// A beaten stack topples over and sinks away. A creature with its own death
        /// (<see cref="Moves.Die"/>) collapses onto its belly as it was filmed instead, lies
        /// still for a moment and sinks into the ground.
        /// </summary>
        IEnumerator Fall(int id)
        {
            Transform figure = figures[id];
            Sounds.PlayAt(Sfx.Fall, figure.position);
            if (counts.TryGetValue(id, out var label))
            {
                hud.RemoveTag(label);
                counts.Remove(id);
            }
            Vector3 home = figure.localPosition;
            Quaternion start = figure.localRotation;
            Moves moves = figure.GetComponentInChildren<Moves>();
            float pace = auto ? 1.8f : 1f;
            if (moves != null && moves.Once(Moves.Die, 0f, pace, 0.08f))
            {
                float lie = moves.Length(Moves.Die) / pace + DeadStill;
                fallsEnd = Mathf.Max(fallsEnd, Time.time + lie + Sink);
                for (float t = 0f; t < lie; t += Time.deltaTime)
                    yield return null;
                for (float k = 0f; k < 1f; k += Time.deltaTime / Sink)
                {
                    figure.localPosition = home - Vector3.up * (0.5f * k * k);
                    yield return null;
                }
                figure.gameObject.SetActive(false);
                yield break;
            }
            // A creature without its own death goes still as it falls.
            if (moves != null)
                moves.Speed(0f);
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.6f)
            {
                figure.localRotation = start * Quaternion.Euler(0f, 0f, 85f * Mathf.Min(1f, k * 2f));
                figure.localPosition = home - Vector3.up * (0.9f * Mathf.Max(0f, k - 0.45f));
                yield return null;
            }
            figure.gameObject.SetActive(false);
        }

        /// <summary>
        /// How long a creature killed with its own death lies still after it, and how long it
        /// then takes to sink away, in seconds.
        /// </summary>
        const float DeadStill = 0.5f, Sink = 0.6f;

        /// <summary>When the last of those deaths has sunk away (Time.time): the battle's end waits for it.</summary>
        float fallsEnd;

        /// <summary>Monsters that were let go turn round and run off the far edge of the field.</summary>
        IEnumerator Flee()
        {
            var runners = new List<Transform>();
            var from = new List<Vector3>();
            var units = new List<Unit>();
            foreach (Fighter m in battle.Fighters)
            {
                if (m.Side != Side.Monsters || !m.Alive)
                    continue;
                if (counts.TryGetValue(m.Id, out var label))
                {
                    hud.RemoveTag(label);
                    counts.Remove(m.Id);
                }
                Transform figure = figures[m.Id];
                runners.Add(figure);
                from.Add(figure.localPosition);
                units.Add(m.Unit);
                Face(figure, figure.localPosition + Vector3.right);
            }
            const float Seconds = 1.1f;
            Vector3 away = Vector3.right * (6f * HexMetrics.InnerRadius);
            // A creature with its own run runs off on its feet; the others bound away.
            var bounds = new bool[runners.Count];
            for (int i = 0; i < runners.Count; i++)
            {
                Moves moves = Runner(runners[i]);
                bounds[i] = moves == null;
                if (moves != null)
                {
                    float own = moves.runSpeed * FigureSize(units[i]);
                    moves.Loop(Moves.Run, Mathf.Clamp(away.magnitude / Seconds / own, 0.7f, RunPlayback), 0.15f);
                }
            }
            if (runners.Count > 0)
                Sounds.Play(Sfx.Flee);
            for (float k = 0f; k < 1f; k += Time.deltaTime / Seconds)
            {
                for (int i = 0; i < runners.Count; i++)
                {
                    float bound = bounds[i] ? 0.14f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 7f)) : 0f;
                    runners[i].localPosition = from[i] + away * k + Vector3.up * (bound - 0.9f * Mathf.Max(0f, k - 0.6f) / 0.4f);
                }
                yield return null;
            }
            foreach (Transform figure in runners)
                figure.gameObject.SetActive(false);
        }

        // ----- The players' commands -----

        void Update()
        {
            if (battle == null)
                return;
            var keyboard = Keyboard.current;
            if (keyboard != null && !hud.Modal)
            {
                // Esc closes the help or opens and closes the menu; the battle waits meanwhile.
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (hud.HelpVisible)
                        hud.ToggleHelp(false);
                    else if (hud.OrdersOpen)
                        hud.ShowOrders(false);
                    else
                        hud.ShowMenu(!hud.MenuOpen);
                }
                else if (keyboard.f1Key.wasPressedThisFrame || keyboard.hKey.wasPressedThisFrame)
                    hud.ToggleHelp(!hud.HelpVisible, Hud.HelpBattlePage);
                else if (!hud.Paused)
                {
                    if (keyboard.aKey.wasPressedThisFrame)
                        ToggleAuto();
                    if (keyboard.dKey.wasPressedThisFrame)
                        Defend();
                    if (keyboard.oKey.wasPressedThisFrame)
                        ToggleOrders();
                    if (hud.OrdersOpen && (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame))
                        Order(HeroOrder.Attack);
                    else if (hud.OrdersOpen && (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame))
                        Order(HeroOrder.Shields);
                }
            }
            // A pointer left over the field while the captures or the promo run is no one's.
            if (!waiting || hud.Modal || hud.Paused || mapCamera.Scripted)
                return;
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            Vector2 screen = mouse.position.ReadValue();
            int under = -1;
            if (!hud.PointerOverUi(screen))
            {
                Ray ray = cam.ScreenPointToRay(screen);
                if (new Plane(Vector3.up, Origin).Raycast(ray, out float distance))
                {
                    pointer = ray.GetPoint(distance) - Origin;
                    under = Battle.IndexOf(HexMetrics.FromWorld(pointer.x, pointer.z));
                }
            }
            Fighter target = under >= 0 ? battle.At(under) : null;
            if (under != hover || target != null)
            {
                hover = under;
                Paint();
                ShowHover(target);
            }
            if (under >= 0 && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                Act(under, target);
        }

        /// <summary>
        /// For the captures: the field and the card as with the pointer on this cell, -1 for
        /// none; the captures' pointer is no one's (<see cref="Update"/>), so it stays.
        /// </summary>
        public void Point(int cell)
        {
            if (battle == null)
                return;
            hover = cell;
            if (cell >= 0)
                pointer = CellAt(cell);
            Paint();
            ShowHover(cell >= 0 ? battle.At(cell) : null);
        }

        void ShowHover(Fighter target)
        {
            Fighter f = battle.Current;
            string line = null;
            if (target != null && f != null && target.Side != f.Side)
            {
                bool shot = battle.CanShoot(f);
                int from = shot ? -1 : battle.StrikeCell(target, pointer.x, pointer.z);
                if (shot || from >= 0)
                {
                    // A run-up (the combat plan's 3.3): from three tiles or more, the line says so.
                    bool runUp = !shot && battle.RunUpAt(target, from);
                    float damage = shot ? battle.Expected(f, target, true) : battle.StrikeExpected(f, target, from);
                    string what = shot ? T("Выстрел", "Shot") : runUp ? T("Удар с разбега", "Charge") : T("Удар", "Strike");
                    line = T($"{what}: около {Mathf.Max(1, Mathf.RoundToInt(damage))} урона, ", $"{what}: about {Mathf.Max(1, Mathf.RoundToInt(damage))} damage, ") +
                        T($"падёт {Kills(target, damage)}", $"{Kills(target, damage)} will fall");
                }
                else
                    line = T("Не дотянуться в этот ход", "Out of reach this turn");
            }
            hud.BattleInfo(target, line);
        }

        /// <summary>How many of the target a blow of the damage kills, as shown before the blow.</summary>
        static int Kills(Fighter target, float damage)
        {
            if (damage >= target.TotalHealth)
                return target.Count;
            if (damage < target.Top)
                return 0;
            return 1 + (int)((damage - target.Top) / target.Stats.Health);
        }

        void Act(int cell, Fighter target)
        {
            Fighter f = battle.Current;
            if (f == null || f.Side != Side.Players)
                return;
            bool done;
            if (target != null && target.Side != f.Side)
                done = battle.CanShoot(f) ? battle.Shoot(target) : battle.Strike(target, pointer.x, pointer.z);
            else if (target == null)
                done = battle.Walk(cell);
            else
                done = false;
            if (done)
                acted = true;
        }

        void Defend()
        {
            if (!waiting || battle.Current == null || battle.Current.Side != Side.Players)
                return;
            hud.ShowOrders(false);
            battle.Defend();
            acted = true;
        }

        /// <summary>Opens or closes the hero's two orders on the bar, while the players may give one.</summary>
        public void ToggleOrders()
        {
            hud.ShowOrders(waiting && battle != null && battle.CanOrder && !hud.OrdersOpen);
        }

        /// <summary>
        /// The hero's order (the combat plan's 3.4). It spends no action: the turn goes round the
        /// loop once to show the order, then waits for the same stack again.
        /// </summary>
        public void Order(HeroOrder order)
        {
            hud.ShowOrders(false);
            if (!waiting || battle == null || !battle.GiveOrder(order))
                return;
            acted = true;
        }

        /// <summary>Whether the hero stands at the edge: the battle has orders and a hero fights it.</summary>
        bool ShowsHero => Battle.OrdersPerBattle > 0 && heroCountry >= 0;

        float HeroSize => FigureSize(Unit.Riders);

        /// <summary>Beyond the first column, on the middle row: as far left of it as a column and a third.</summary>
        static Vector3 HeroSpot
        {
            get
            {
                Vector3 first = CellAt(Battle.IndexOf(Hex.FromOffset(0, HeroRow))), second = CellAt(Battle.IndexOf(Hex.FromOffset(1, HeroRow)));
                return first - HeroOut * (second - first);
            }
        }

        /// <summary>The hero at rest: towards the field and a little to the camera, like the players' stacks.</summary>
        static Quaternion HeroRest => Quaternion.Euler(0f, -60f, 0f);

        /// <summary>
        /// The order shown: the hero turns to the stack and hops twice under the order's words;
        /// «Щиты!» puts «Защита!» over each of the players' stacks.
        /// </summary>
        IEnumerator Ordered(BattleEvent e)
        {
            bool attack = e.Order == HeroOrder.Attack;
            OrdersShown++;
            string words = attack ? T("В атаку!", "Attack!") : T("Щиты!", "Shields!");
            Sounds.Play(attack ? Sfx.BattleStart : Sfx.Defend);
            if (heroFigure != null)
            {
                // The words a little towards the field, so they never run off the screen's edge.
                hud.Float(words, "float-order", heroFigure.position + new Vector3(0.5f, HeroSize + 0.25f, 0f), 1.6f);
                if (figures.TryGetValue(e.Actor, out Transform stack))
                    Face(heroFigure, stack.localPosition);
                Vector3 home = heroFigure.localPosition;
                for (int hop = 0; hop < 2; hop++)
                {
                    for (float k = 0f; k < 1f; k += Dt / 0.2f)
                    {
                        heroFigure.localPosition = home + Vector3.up * (0.2f * Mathf.Sin(Mathf.PI * k));
                        yield return null;
                    }
                }
                heroFigure.localPosition = home;
            }
            else if (figures.ContainsKey(e.Actor))
                hud.Float(words, "float-order", Head(e.Actor), 1.6f);
            if (!attack)
            {
                foreach (Fighter f in battle.Fighters)
                {
                    if (f.Side == Side.Players && f.Alive && figures.ContainsKey(f.Id))
                        hud.Float(T("Защита!", "Defend!"), "float-defend", Head(f.Id));
                }
            }
            yield return Pause(0.5f);
            if (heroFigure != null)
                heroFigure.localRotation = HeroRest;
        }

        /// <summary>
        /// When above zero (the promo), each of the players' stacks takes the auto battle's choice
        /// after that many seconds of its turn, the turn shown as the players' own (whose move it
        /// is, its reach lit) rather than with the auto battle's line.
        /// </summary>
        public float Hands;

        /// <summary>Turns the auto battle on or off (as the A key does).</summary>
        public void SetAuto(bool on)
        {
            if (auto != on)
                ToggleAuto();
        }

        void ToggleAuto()
        {
            auto = !auto;
            hud.BattleAuto(auto);
        }

        /// <summary>
        /// The beaten monsters offer to give up (<see cref="Battle.Offered"/>): letting them go wins
        /// the fight now with no more losses, finishing them off gives the hero more experience.
        /// Esc and Enter pick as in every question: Enter lets them go, Esc fights on.
        /// </summary>
        IEnumerator AskSurrender()
        {
            Fighter any = battle.Fighters.Find(f => f.Side == Side.Monsters && f.Alive);
            bool answered = false;
            hud.Ask(Words.YieldTitle(any != null ? any.Unit : Unit.Goblins), "swords", Words.YieldOffer, Words.YieldAnswers, choice =>
                {
                    if (choice == 0)
                        battle.Accept();
                    else
                    {
                        battle.FightOn();
                        LowerWhiteFlags();
                    }
                    answered = true;
                });
            yield return new WaitUntil(() => answered || battle.Outcome != Outcome.Running);
        }

        /// <summary>Asks whether to retreat (the bar's button; the captures show the question too).</summary>
        internal void AskRetreat()
        {
            if (battle == null || battle.Outcome != Outcome.Running)
                return;
            hud.Ask(Words.RetreatTitle, "swords", Words.RetreatAsk, Words.RetreatAnswers, choice =>
                {
                    // Esc picks the last answer: fighting on.
                    if (choice != 0 || battle == null || battle.Outcome != Outcome.Running)
                        return;
                    battle.Retreat();
                    Sounds.Play(Sfx.Retreat);
                    acted = true;
                });
        }
    }
}
