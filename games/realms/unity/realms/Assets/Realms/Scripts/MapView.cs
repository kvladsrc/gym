using Realms.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realms
{
    /// <summary>
    /// Shows a world: the terrain mesh, the rivers, the sea, the props, the clouds of the fog of
    /// war, and the cell textures the shaders read: the cells for territories, borders, the grid,
    /// fields, roads and the hover and selection rings, and what the players have seen, which
    /// fades in cell by cell as it opens, with the height of the clouds over each cell. A town,
    /// resource, monster group or place in an unseen cell is hidden until the cell is seen, so
    /// nothing tall stands out of the clouds. The wind (_RealmsWind) sways the plants, runs over
    /// the grass and the wheat in waves, carries the chimneys' smoke and drives small clouds over
    /// the map, whose shadows drift over the land with them; the clouds fade out as the camera
    /// comes down to the land.
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        public Material terrainMaterial, waterMaterial, riverMaterial, propMaterial, fogMaterial, flagMaterial, smokeMaterial, cloudMaterial;

        /// <summary>How hard the wind blows: 1 bends the plants as far as they were made to bend.</summary>
        public float wind = 1f;

        /// <summary>Where the wind blows to on the map (x, z): the clouds drift this way.</summary>
        static readonly Vector2 WindToward = new Vector2(-0.011f, -0.005f).normalized;

        /// <summary>The drifting clouds' height over the map, in world units.</summary>
        public float cloudHeight = 4.5f;

        /// <summary>The share of the clouds' grid squares (6 by 5 world units) that hold a cloud.</summary>
        public float cloudCover = 0.4f;

        /// <summary>The camera's height over the map below which the clouds start to fade, and where they are gone.
        /// MapCamera keeps the map's view about 12 high at the start (the seen land fills it), a hero's or town's close look under 7.</summary>
        public float cloudsFrom = 13f, cloudsGone = 9f;

        /// <summary>How fast a cell's clouds clear, in shares a second.</summary>
        public float fogFade = 1.6f;

        /// <summary>The fog texture's green channel: the clouds' height in units of this (REALMS_FOG_HEIGHT_SCALE in RealmsCommon.hlsl).</summary>
        public const float CloudHeightScale = 2.5f;

        public World World { get; private set; }
        public TerrainField Field { get; private set; }

        /// <summary>Darkening under the drifting clouds' shadows.</summary>
        public float cloudShadows = 0.2f;

        /// <summary>
        /// Flags in the blue channel of the cell texture; the alpha holds the road mask, with
        /// <see cref="RoadShape.HubBit"/> where the roads lead to the cell's middle.
        /// <see cref="FlagNoClouds"/> marks a town or a road and the cells next to them, where the
        /// drifting clouds thin away (RealmsCloudRoom in RealmsCommon.hlsl).
        /// </summary>
        public const int FlagWheat = 1, FlagCity = 2, FlagLair = 4, FlagWater = 8, FlagCentre = 16, FlagNoClouds = 128;

        /// <summary>A town cell's flags hold its town's level less one from this bit on (two bits).</summary>
        public const int LevelShift = 5;

        static readonly int CellsId = Shader.PropertyToID("_RealmsCells");
        static readonly int MapSizeId = Shader.PropertyToID("_RealmsMapSize");
        static readonly int ColorsId = Shader.PropertyToID("_RealmsCountryColors");
        static readonly int HoverId = Shader.PropertyToID("_RealmsHover");
        static readonly int SelectId = Shader.PropertyToID("_RealmsSelect");
        static readonly int OverlayId = Shader.PropertyToID("_RealmsOverlay");
        static readonly int CloudsId = Shader.PropertyToID("_RealmsClouds");
        static readonly int CloudHeightId = Shader.PropertyToID("_RealmsCloudHeight");
        static readonly int CloudCoverId = Shader.PropertyToID("_RealmsCloudCover");
        static readonly int CloudShowId = Shader.PropertyToID("_RealmsCloudShow");
        static readonly int FogId = Shader.PropertyToID("_RealmsFog");
        static readonly int WindId = Shader.PropertyToID("_RealmsWind");

        Texture2D cells, fog;
        Transform content;
        MeshFilter terrainFilter, riverFilter;
        PropBuilder props;
        ContactShade contact;
        BorderMarks marks;
        bool[] seen;
        float[] clear;
        byte[] fogBytes;
        bool fogMoving;

        public PropBuilder Props => props;

        /// <summary>The neighbours' troops at the lairs, whose flags the map shows (set with the game's state before <see cref="Show"/>).</summary>
        public System.Collections.Generic.IReadOnlyList<Troop> LairTroops { get; set; }

        /// <summary>The map's objects: hidden while a battle is on.</summary>
        public Transform Content => content;

        public void Show(World world, ArtLibrary art)
        {
            foreach (float _ in ShowSteps(world, art))
            {
            }
        }

        /// <summary>The map is being drawn in steps (<see cref="ShowSteps"/>): its parts are half made.</summary>
        bool drawing;

        /// <summary>
        /// Draws the map as <see cref="Show"/> does, telling after each part, each cell of its own
        /// and each block of props how far it has come, 0 to 1, so that a loading screen can be
        /// drawn between them: the players found the game hanging at its start while the last
        /// game loaded behind the main menu (2026-11-11).
        /// </summary>
        public System.Collections.Generic.IEnumerable<float> ShowSteps(World world, ArtLibrary art)
        {
            drawing = true;
            // Each part of the drawing is timed into one log line: the players found the game
            // hanging when a town is founded or grows, which draws the whole map again (2026-11-11).
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var laps = new System.Text.StringBuilder("Map drawn, ms:");
            void Lap(string part)
            {
                laps.Append(' ').Append(part).Append(' ').Append(watch.ElapsedMilliseconds);
                watch.Restart();
            }

            Clear();
            World = world;
            Field = new TerrainField(world);
            Lap("field");
            yield return 0.3f;
            content = new GameObject("Map").transform;
            content.SetParent(transform, false);
            marks = new BorderMarks(world, Field, propMaterial, content, LairTroops, Visible);

            UploadCells();
            Lap("cells");

            var terrain = new GameObject("Terrain");
            terrain.transform.SetParent(content, false);
            terrainFilter = terrain.AddComponent<MeshFilter>();
            terrainFilter.sharedMesh = Field.BuildMesh();
            var terrainRenderer = terrain.AddComponent<MeshRenderer>();
            terrainRenderer.sharedMaterial = terrainMaterial;
            terrainRenderer.shadowCastingMode = ShadowCastingMode.On;
            Lap("terrain");
            yield return 0.36f;

            var rivers = new GameObject("Rivers");
            rivers.transform.SetParent(content, false);
            riverFilter = rivers.AddComponent<MeshFilter>();
            riverFilter.sharedMesh = RiverCourse.BuildMesh(Field.Rivers);
            var riverRenderer = rivers.AddComponent<MeshRenderer>();
            riverRenderer.sharedMaterial = riverMaterial;
            riverRenderer.shadowCastingMode = ShadowCastingMode.Off;
            Lap("rivers");

            var water = new GameObject("Sea");
            water.transform.SetParent(content, false);
            water.AddComponent<MeshFilter>().sharedMesh = SeaMesh(world);
            var waterRenderer = water.AddComponent<MeshRenderer>();
            waterRenderer.sharedMaterial = waterMaterial;
            waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
            Lap("sea");

            var props = new GameObject("Props");
            props.transform.SetParent(content, false);
            SmokePlumes.Material = smokeMaterial;
            contact = new ContactShade(world);
            this.props = new PropBuilder(world, Field, art, propMaterial, props.transform, contact);
            Lap("contact");
            yield return 0.4f;
            foreach (float part in this.props.BuildSteps())
                yield return 0.4f + 0.58f * part;
            Lap("props");

            if (fogMaterial != null)
            {
                var clouds = new GameObject("Fog");
                clouds.transform.SetParent(content, false);
                Mesh plane = SeaMesh(world);
                plane.name = "Fog";
                clouds.AddComponent<MeshFilter>().sharedMesh = plane;
                var cloudRenderer = clouds.AddComponent<MeshRenderer>();
                cloudRenderer.sharedMaterial = fogMaterial;
                cloudRenderer.shadowCastingMode = ShadowCastingMode.Off;
                cloudRenderer.receiveShadows = false;
            }

            if (cloudMaterial != null)
            {
                // One plane at the clouds' height over the map and some sea round it; the shader
                // draws the clouds where they drift over it.
                var drifting = new GameObject("Clouds");
                drifting.transform.SetParent(content, false);
                drifting.transform.localPosition = new Vector3(0f, cloudHeight, 0f);
                Mesh plane = SeaMesh(world, 12f);
                plane.name = "Clouds";
                drifting.AddComponent<MeshFilter>().sharedMesh = plane;
                var driftingRenderer = drifting.AddComponent<MeshRenderer>();
                driftingRenderer.sharedMaterial = cloudMaterial;
                driftingRenderer.shadowCastingMode = ShadowCastingMode.Off;
                driftingRenderer.receiveShadows = false;
            }

            SetHover(-1);
            SetSelect(-1);
            Lap("fog");
            Debug.Log(laps.ToString());
            drawing = false;
            yield return 1f;
        }

        /// <summary>
        /// Shows what changed in the world: owners (the borders) and the cells whose town or
        /// resource changed. When the ground under something changed too (a group came or was
        /// beaten, a neighbour's town grew) and <paramref name="ground"/> allows, the ground is
        /// laid again now (<see cref="Reshape"/>): left for later, all of it came with the next
        /// town founded, which took 1.7 s instead of 0.2 (capture of 2026-11-11). True when it
        /// was, so the heroes are set on the ground again.
        /// </summary>
        public bool Refresh(System.Collections.Generic.IEnumerable<int> changed, bool ground = true)
        {
            if (ground && Field != null && Field.Stale())
            {
                Reshape(changed);
                return true;
            }
            UploadCells();
            foreach (int cell in changed)
            {
                props.RebuildCell(cell);
                props.ShowCell(cell, Visible(cell));
            }
            contact?.Flush();
            return false;
        }

        /// <summary>
        /// Shows a town founded or grown on <paramref name="changed"/> without drawing the whole
        /// map again: the ground is worked out again round what changed since it was drawn
        /// (<see cref="TerrainField.Refresh"/>) and its mesh and the rivers' made anew; the props
        /// are dressed again on the changed cells and wherever the ground moved. The players
        /// found the game hanging for seconds when a town was founded or grew, as the whole map
        /// was drawn again (2026-11-11).
        /// </summary>
        public void Reshape(System.Collections.Generic.IEnumerable<int> changed)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var laps = new System.Text.StringBuilder("Map reshaped, ms:");
            void Lap(string part)
            {
                laps.Append(' ').Append(part).Append(' ').Append(watch.ElapsedMilliseconds);
                watch.Restart();
            }

            System.Collections.Generic.HashSet<int> cells = Field.Refresh();
            int moved = cells.Count;
            Lap("field");
            Destroy(terrainFilter.sharedMesh);
            terrainFilter.sharedMesh = Field.BuildMesh();
            Lap("terrain");
            Destroy(riverFilter.sharedMesh);
            riverFilter.sharedMesh = RiverCourse.BuildMesh(Field.Rivers);
            Lap("rivers");
            UploadCells();
            Lap("cells");
            cells.UnionWith(changed);
            // The blocks off the screen are dressed in the next frames, one a frame (Update).
            int blocks = props.Redress(cells, InView);
            foreach (int index in cells)
                props.ShowCell(index, Visible(index));
            Lap("props");
            contact?.Flush();
            Lap("contact");
            laps.Append($" (cells {cells.Count}, the ground moved on {moved}, blocks {blocks}, waiting {props.Pending})");
            Debug.Log(laps.ToString());
        }

        /// <summary>Whether a cell is on the screen or near its edge (a block's half beyond it).</summary>
        bool InView(int cell)
        {
            Camera view = Camera.main;
            if (view == null)
                return true;
            float x = World.CenterX(cell), z = World.CenterZ(cell);
            Vector3 p = view.WorldToViewportPoint(new Vector3(x, Mathf.Max(Field.Height(x, z), 0f), z));
            return p.z > 0f && p.x > -0.4f && p.x < 1.4f && p.y > -0.4f && p.y < 1.4f;
        }

        void Clear()
        {
            // The flags' meshes go with them before the map's objects go.
            marks?.Dispose();
            marks = null;
            if (content != null)
            {
                foreach (MeshFilter filter in content.GetComponentsInChildren<MeshFilter>())
                {
                    // Generated meshes only: the studio models share their prefabs' meshes.
                    Mesh mesh = filter.sharedMesh;
                    if (mesh != null && (mesh.name == "Terrain" || mesh.name == "Sea" || mesh.name == "Fog" || mesh.name == "Clouds" || mesh.name == "Rivers" || mesh.name.StartsWith("Props ")))
                        Destroy(mesh);
                }
                Destroy(content.gameObject);
            }
            content = null;
            contact?.Dispose();
            contact = null;
            props = null;
            fogMoving = false;
        }

        void UploadCells()
        {
            if (cells == null || cells.width != World.Width || cells.height != World.Height)
            {
                if (cells != null)
                    Destroy(cells);
                cells = new Texture2D(World.Width, World.Height, TextureFormat.RGBA32, false, true)
                {
                    name = "Realms cells", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                };
            }
            var pixels = new Color32[World.Cells.Length];
            // Clouds keep a cell away from towns and roads: one the size of a town lay over the
            // road to a town and over the capital and read as a blot (review of 2026-11-10).
            var busy = new bool[World.Cells.Length];
            foreach (Cell cell in World.Cells)
            {
                if (cell.Town < 0 && cell.Roads == 0)
                    continue;
                busy[cell.Index] = true;
                for (int d = 0; d < 6; d++)
                {
                    int n = World.Neighbor(cell.Index, d);
                    if (n >= 0)
                        busy[n] = true;
                }
            }
            foreach (Cell cell in World.Cells)
            {
                int flags = busy[cell.Index] ? FlagNoClouds : 0;
                if (cell.Resource == Resource.Wheat)
                    flags |= FlagWheat;
                if (cell.Town >= 0)
                {
                    flags |= FlagCity | (Towns.Level(World.Cities[cell.Town]) - 1) << LevelShift;
                    if (cell.City >= 0)
                        flags |= FlagCentre;
                }
                if (cell.Monster != Monster.None)
                    flags |= FlagLair;
                if (cell.IsWater)
                    flags |= FlagWater;
                int roads = cell.Roads | (RoadShape.Hub(cell) ? RoadShape.HubBit : 0);
                pixels[cell.Index] = new Color32((byte)(cell.Owner + 1), (byte)cell.Biome, (byte)flags, (byte)roads);
            }
            cells.SetPixels32(pixels);
            cells.Apply(false);

            Shader.SetGlobalTexture(CellsId, cells);
            Shader.SetGlobalVector(MapSizeId, new Vector4(World.Width, World.Height, 1f / World.Width, 1f / World.Height));
            var colors = new Vector4[8];
            for (int k = 0; k < colors.Length; k++)
                colors[k] = k < Palette.Countries.Length ? (Vector4)Palette.Countries[k].linear : Vector4.one;
            Shader.SetGlobalVectorArray(ColorsId, colors);
            Shader.SetGlobalFloat(CloudsId, cloudShadows);
            Shader.SetGlobalFloat(CloudHeightId, cloudHeight);
            Shader.SetGlobalFloat(CloudCoverId, cloudCover);
            Shader.SetGlobalVector(WindId, new Vector4(WindToward.x, WindToward.y, wind, 0f));
            // The towns' next cells move as the land, the towns and the lairs change.
            marks?.Update();
        }

        /// <summary>
        /// The cells the players have seen; newly seen ones clear over a moment unless
        /// <paramref name="instant"/>. The array is kept and read again by <see cref="UpdateFog"/>.
        /// </summary>
        public void SetSeen(bool[] seenCells, bool instant)
        {
            int n = World.Cells.Length;
            seen = seenCells;
            if (clear == null || clear.Length != n || instant)
            {
                clear = new float[n];
                fogBytes = new byte[n * 2];
                for (int i = 0; i < n; i++)
                {
                    clear[i] = seen[i] ? 1f : 0f;
                    float height = CloudHeight(World.Cells[i].Biome) / CloudHeightScale;
                    fogBytes[i * 2 + 1] = (byte)Mathf.RoundToInt(Mathf.Clamp01(height) * 255f);
                }
            }
            for (int i = 0; i < n; i++)
                props.ShowCell(i, seen[i]);
            marks?.UpdateTroops();
            if (fog == null || fog.width != World.Width || fog.height != World.Height)
            {
                if (fog != null)
                    Destroy(fog);
                fog = new Texture2D(World.Width, World.Height, TextureFormat.RG16, false, true)
                {
                    name = "Realms fog", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                };
            }
            fogMoving = true;
            UploadFog();
            Shader.SetGlobalTexture(FogId, fog);
        }

        /// <summary>True when the cell has been seen and its clouds are mostly gone.</summary>
        public bool Visible(int cell) => seen == null || (cell >= 0 && seen[cell]);

        /// <summary>
        /// How high the clouds stand over a cell of the biome: above its ground and trees (the
        /// peaks of unseen mountains are hidden, see <see cref="PropBuilder.ShowCell"/>).
        /// </summary>
        public static float CloudHeight(Biome biome)
        {
            switch (biome)
            {
                case Biome.Mountains:
                case Biome.Hills: return 1.15f;
                case Biome.Ocean:
                case Biome.Shallows: return 0.85f;
                default: return 1f;
            }
        }

        void UploadFog()
        {
            for (int i = 0; i < clear.Length; i++)
                fogBytes[i * 2] = (byte)Mathf.RoundToInt(Mathf.Clamp01(clear[i]) * 255f);
            fog.SetPixelData(fogBytes, 0);
            fog.Apply(false);
        }

        void Update()
        {
            if (drawing)
                return;
            // The clouds fade out as the camera comes down (a town's or hero's screen holds it low).
            Camera view = Camera.main;
            float height = view != null ? view.transform.position.y : cloudsFrom;
            Shader.SetGlobalFloat(CloudShowId, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(cloudsGone, cloudsFrom, height)));
            if (props != null && props.DressPending())
                contact?.Flush();
            if (!fogMoving || seen == null || clear == null)
                return;
            float step = fogFade * Time.deltaTime;
            bool moving = false;
            for (int i = 0; i < clear.Length; i++)
            {
                float goal = seen[i] ? 1f : 0f;
                if (clear[i] == goal)
                    continue;
                if (clear[i] == 0f)
                    props.ShowCell(i, true);
                clear[i] = Mathf.MoveTowards(clear[i], goal, step);
                if (clear[i] == 0f)
                    props.ShowCell(i, false);
                moving = true;
            }
            if (moving)
                UploadFog();
            fogMoving = moving;
        }

        /// <summary>Starts clearing the cells the players have just seen.</summary>
        public void UpdateFog() => fogMoving = true;

        static Mesh SeaMesh(World world, float margin = 140f)
        {
            float x0 = world.MinX - margin, x1 = world.MaxX + margin, z0 = world.MinZ - margin, z1 = world.MaxZ + margin;
            var mesh = new Mesh { name = "Sea" };
            mesh.SetVertices(new[] { new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0) });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetIndices(new[] { 0, 1, 2, 0, 2, 3 }, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector4 Ring(World world, int cell)
        {
            if (world == null || cell < 0)
                return new Vector4(0, 0, 0, 0);
            Hex hex = world.Cells[cell].Hex;
            return new Vector4(hex.Q, hex.R, 1, 0);
        }

        public void SetHover(int cell) => Shader.SetGlobalVector(HoverId, Ring(World, cell));

        public void SetSelect(int cell)
        {
            Shader.SetGlobalVector(SelectId, Ring(World, cell));
            marks?.Choose(cell);
        }

        /// <summary>The other kingdoms' next cells show while Alt is held, with the badges.</summary>
        public void ShowOtherMarks(bool show) => marks?.ShowOthers(show);

        /// <summary>The marks again after the players moved their stake (<see cref="BorderMarks"/>).</summary>
        public void UpdateMarks() => marks?.Update();

        /// <summary>The cells the players' border has just taken flash and settle (<see cref="BorderMarks.Flash"/>).</summary>
        public void FlashTaken(System.Collections.Generic.IEnumerable<int> cells) => marks?.Flash(cells);

        /// <summary>Gold rings on the cells the guiding lines point at (the interface plan's 3.3); null: none.</summary>
        public void SetGuide(System.Collections.Generic.IEnumerable<int> cells) => marks?.SetGuide(cells);

        /// <summary>
        /// Line widths grow as the camera pulls back so borders stay readable; close up they stop
        /// thinning at half their width at 22 away (at 0.7 a border filled 14 pixels at the town's
        /// close view and read as a thick bright stripe, review of 2026-11-10).
        /// </summary>
        public void SetZoom(float distance)
        {
            float scale = Mathf.Clamp(distance / 22f, 0.5f, 2.6f);
            float grid = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 45f, distance));
            Shader.SetGlobalVector(OverlayId, new Vector4(scale, grid, 0, 0));
        }

        /// <summary>The cell under a world point, or -1 off the map.</summary>
        public int CellAt(Vector3 point)
        {
            if (World == null)
                return -1;
            return World.IndexOf(HexMetrics.FromWorld(point.x, point.z));
        }

        void OnDestroy()
        {
            Clear();
            if (cells != null)
                Destroy(cells);
            if (fog != null)
                Destroy(fog);
        }
    }
}
