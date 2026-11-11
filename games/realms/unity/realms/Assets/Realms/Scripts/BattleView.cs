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
            ["boar"] = -90f, ["rider"] = -90f,
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

        readonly Dictionary<string, ModelBatch.Source> sources = new Dictionary<string, ModelBatch.Source>();
        Vector4 overlay;
        Battle battle;
        Transform root, arrow, ring;
        Mesh tiles, ringMesh;
        Color[] tileColours;
        int ringOf = -1;
        Color ringColour;
        float mapFieldOfView, panelTop;
        readonly Dictionary<int, Transform> figures = new Dictionary<int, Transform>();
        readonly Dictionary<int, UnityEngine.UIElements.Label> counts = new Dictionary<int, UnityEngine.UIElements.Label>();
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
        public IEnumerator Play(Battle fight, string title)
        {
            battle = fight;
            played = rounds = 0;
            hover = -1;
            waiting = acted = auto = false;
            shown = new int[battle.Fighters.Count];
            for (int i = 0; i < shown.Length; i++)
                shown[i] = battle.Fighters[i].StartCount;
            // The camera first: the woods around the field are placed where the camera sees them.
            Aim();
            Build();
            hud.ShowBattle(title, portraits, Defend, ToggleAuto, AskRetreat);
            yield return Run();
        }

        public void Close()
        {
            StopAllCoroutines();
            hud.HideBattle();
            counts.Clear();
            figures.Clear();
            if (root != null)
            {
                Figures.Destroy(root.gameObject);
                Shader.SetGlobalVector(OverlayId, overlay);
                Shader.SetGlobalFloat(FieldBiomeId, 0f);
            }
            root = arrow = ring = null;
            tiles = ringMesh = null;
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

            // The ground far around, in the map's textures.
            var land = new MeshKit();
            Vector3 c = Centre;
            const float reach = 70f;
            land.Quad(c + new Vector3(-reach, 0f, -reach), c + new Vector3(-reach, 0f, reach), c + new Vector3(reach, 0f, reach),
                c + new Vector3(reach, 0f, -reach), ground, Vector3.up);
            Ground(Figures.Built(land, "Figure ground", view.terrainMaterial, root), sharesA, sharesB);

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
                float room = Room(at);
                float across = cam.WorldToViewportPoint(Origin + at).x;
                if (room <= 0f || across < -0.08f || across > 1.08f)
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
            if (decor.VertexCount > 0)
                Figures.Built(decor, "Figure decor", material, root);
            batch.Build(root, "battle");

            // The cells: one flat hexagon each in the ground's textures, recoloured to show where
            // the stack can go, with a lighter rim; the rims of neighbouring cells make the grid.
            var kit = new MeshKit();
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
                }
            }
            GameObject cells = Figures.Built(kit, "Figure tiles", view.terrainMaterial, root);
            Ground(cells, sharesA, sharesB);
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

            foreach (Fighter f in battle.Fighters)
                Spawn(f);

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
            bool woods = (g == Biome.Forest ? r < 0.85f : g == Biome.Hills ? r < 0.35f : g == Biome.Mountains ? r < 0.15f : r < 0.45f)
                && room >= LeastTree;
            bool rock = !woods && (obstacle || g == Biome.Hills || g == Biome.Mountains || g == Biome.Beach || r < 0.7f);
            if (woods ? Tree(batch, at, rng, obstacle, room) : rock ? Stone(batch, at, rng, obstacle) : Small(batch, at, rng))
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
            ModelBatch.Source model = (!obstacle && rng.Chance(green ? 0.3f : 0.1f) ? Model("boulder_mossy") : null) ?? Model("boulder");
            if (model == null)
                return false;
            Lay(batch, model, at, rng, obstacle ? rng.Range(1.05f, 1.2f) : rng.Range(0.45f, 1.1f));
            return true;
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
            GameObject figure = Figures.Unit(f.Unit, art, material, holder, FigureSize(f.Unit));
            string model = PropBuilder.ModelOf(f.Unit);
            if (Facings.TryGetValue(model, out float facing) && art != null && art.ModelOf(model) != null)
                figure.transform.localRotation = Quaternion.Euler(0f, facing, 0f);
            Light(figure, f.Unit);
            // Idle keeps the turn it starts with.
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
            Vector3 target = Origin + Centre;

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
            int perCell = tileColours.Length / battle.Blocked.Length;
            for (int i = 0; i < battle.Blocked.Length; i++)
            {
                Hex h = Battle.HexOf(i);
                Color c = Color.Lerp(ground, Color.white, (h.Col + h.Row) % 2 == 0 ? 0.075f : 0.035f);
                if (battle.Blocked[i])
                    c = ground * 0.92f;
                else if (reach.Contains(i))
                    c = Color.Lerp(c, Color.white, 0.42f);
                Fighter here = battle.At(i);
                if (mine && here != null && here.Side != f.Side)
                    c = Color.Lerp(c, Red, 0.3f);
                if (i == hover && mine)
                {
                    if (enemy)
                        c = Color.Lerp(c, Red, shoot || from >= 0 ? 0.75f : 0.35f);
                    else if (reach.Contains(i))
                        c = Color.Lerp(c, Color.white, 0.75f);
                }
                if (from >= 0 && i == from && from != f.Cell)
                    c = Color.Lerp(c, Color.white, 0.8f);
                Color inside = c.linear, rim = Color.Lerp(c, Color.white, LineLight).linear;
                inside.a = rim.a = 1f;
                for (int k = 0; k < perCell; k++)
                    tileColours[i * perCell + k] = k < FillVertices ? inside : rim;
            }
            tiles.colors = tileColours;
            Mark(battle.Outcome == Outcome.Running ? f : null);
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
                hud.BattleTurn(Words.Count(f.Unit, f.Count), battle.CanShoot(f));
                Paint();
                yield return new WaitUntil(() => acted || auto || battle.Outcome != Outcome.Running);
                waiting = false;
                hover = -1;
                hud.BattleInfo(null, null);
                hud.BattleTurn(null, false);
                Paint();
            }
            yield return PlayEvents();
            if (battle.Surrendered)
                yield return Flee();
            Paint();
            yield return Pause(0.6f);
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
                    hud.BattleQueue(battle.Queue(), battle.NextRound(), shown, rounds);
                    break;
                case BattleEventKind.Walk:
                {
                    Transform figure = figures[e.Actor];
                    foreach (int cell in e.Path)
                        yield return Step(figure, CellAt(cell));
                    figure.localRotation = Rest(battle.Fighters[e.Actor]);
                    break;
                }
                case BattleEventKind.Strike:
                case BattleEventKind.StrikeBack:
                    yield return Lunge(e);
                    break;
                case BattleEventKind.Shoot:
                    yield return Shoot(e);
                    break;
                case BattleEventKind.Defend:
                    hud.Float(T("Защита!", "Defend!"), "float-defend", Head(e.Actor));
                    yield return Pause(0.4f);
                    break;
                case BattleEventKind.Heal:
                    hud.Float($"+{e.Damage}", "float-heal", Head(e.Actor));
                    yield return Pause(0.35f);
                    break;
                case BattleEventKind.Web:
                    hud.Float(T("В паутине!", "Caught in a web!"), "float-web", Head(e.Target));
                    yield return Pause(0.3f);
                    break;
                case BattleEventKind.Surrender:
                    foreach (Fighter m in battle.Fighters)
                    {
                        if (m.Side == Side.Monsters && m.Alive)
                            hud.Float(Words.Yields(m.Unit, m.Count), "float-yield", Head(m.Id));
                    }
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

        /// <summary>The striker jumps at the target and back; the blow lands at the top.</summary>
        IEnumerator Lunge(BattleEvent e)
        {
            Transform a = figures[e.Actor], t = figures[e.Target];
            Vector3 home = a.localPosition, reach = Vector3.Lerp(home, t.localPosition, 0.42f);
            Face(a, t.localPosition);
            for (float k = 0f; k < 1f; k += Dt / 0.13f)
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

        /// <summary>An arrow flies in an arc from the shooter to the target.</summary>
        IEnumerator Shoot(BattleEvent e)
        {
            Transform a = figures[e.Actor], t = figures[e.Target];
            Face(a, t.localPosition);
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
            hud.Float($"−{e.Damage}", "float-hit", Head(e.Target));
            StartCoroutine(shown[e.Target] == 0 ? Fall(e.Target) : Shake(figures[e.Target]));
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

        /// <summary>A beaten stack topples over and sinks away.</summary>
        IEnumerator Fall(int id)
        {
            Transform figure = figures[id];
            if (counts.TryGetValue(id, out var label))
            {
                hud.RemoveTag(label);
                counts.Remove(id);
            }
            Vector3 home = figure.localPosition;
            Quaternion start = figure.localRotation;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.6f)
            {
                figure.localRotation = start * Quaternion.Euler(0f, 0f, 85f * Mathf.Min(1f, k * 2f));
                figure.localPosition = home - Vector3.up * (0.9f * Mathf.Max(0f, k - 0.45f));
                yield return null;
            }
            figure.gameObject.SetActive(false);
        }

        /// <summary>Monsters that were let go turn round and run off the far edge of the field.</summary>
        IEnumerator Flee()
        {
            var runners = new List<Transform>();
            var from = new List<Vector3>();
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
                Face(figure, figure.localPosition + Vector3.right);
            }
            Vector3 away = Vector3.right * (6f * HexMetrics.InnerRadius);
            for (float k = 0f; k < 1f; k += Time.deltaTime / 1.1f)
            {
                for (int i = 0; i < runners.Count; i++)
                {
                    runners[i].localPosition = from[i] + away * k
                        + Vector3.up * (0.14f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 7f)) - 0.9f * Mathf.Max(0f, k - 0.6f) / 0.4f);
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
                    else
                        hud.ShowMenu(!hud.MenuOpen);
                }
                else if (keyboard.f1Key.wasPressedThisFrame || keyboard.hKey.wasPressedThisFrame)
                    hud.ToggleHelp(!hud.HelpVisible);
                else if (!hud.Paused)
                {
                    if (keyboard.aKey.wasPressedThisFrame)
                        ToggleAuto();
                    if (keyboard.dKey.wasPressedThisFrame)
                        Defend();
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

        void ShowHover(Fighter target)
        {
            Fighter f = battle.Current;
            string line = null;
            if (target != null && f != null && target.Side != f.Side)
            {
                bool shot = battle.CanShoot(f);
                if (shot || battle.StrikeCell(target, pointer.x, pointer.z) >= 0)
                {
                    float damage = battle.Expected(f, target, shot);
                    line = T($"{(shot ? "Выстрел" : "Удар")}: около {Mathf.Max(1, Mathf.RoundToInt(damage))} урона, ", $"{(shot ? "Shot" : "Strike")}: about {Mathf.Max(1, Mathf.RoundToInt(damage))} damage, ") +
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
            battle.Defend();
            acted = true;
        }

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
                        battle.FightOn();
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
                    acted = true;
                });
        }
    }
}
