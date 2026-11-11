using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realms
{
    /// <summary>
    /// Figures for the map and the battlefield: a unit or a hero as its studio model, or a simple
    /// stand-in while the model is missing. Each stands with its pivot at the feet and faces -z,
    /// like the models. Meshes made here are named "Figure …" so <see cref="Destroy"/> can free them.
    /// </summary>
    public static class Figures
    {
        /// <summary>A unit's figure, <paramref name="size"/> tall and wide at most.</summary>
        public static GameObject Unit(Unit unit, ArtLibrary art, Material material, Transform parent, float size)
        {
            GameObject prefab = art != null ? art.ModelOf(PropBuilder.ModelOf(unit)) : null;
            if (prefab != null)
                return Model(prefab, parent, size);
            var kit = new MeshKit();
            var rng = new Rng((int)unit + 7);
            Color body = PropBuilder.ColourOf(unit);
            kit.Blob(new Vector3(0, 0.36f, 0), new Vector3(0.3f, 0.36f, 0.32f), 8, 4, body * 0.78f, body, rng, 0.06f);
            // Two eyes, so the stand-in shows which way it looks.
            for (int side = -1; side <= 1; side += 2)
                kit.Blob(new Vector3(0.1f * side, 0.5f, -0.27f), new Vector3(0.05f, 0.06f, 0.03f), 6, 3, Color.white, Color.white, rng, 0f);
            return Built(kit, "Figure " + unit, material, parent, size);
        }

        /// <summary>
        /// A hero: the rider model or a stand-in, with the kingdom's flag on a pole beside it.
        /// </summary>
        /// <summary>
        /// The turn that brings the hero's model head first along the way its figure faces: the
        /// model's head points along its -x instead of the -z the figures face (seen in the
        /// models' silhouettes, 2026-11-10, like the battle's boar and rider in BattleView), so
        /// a hero went sideways from cell to cell. None for the figure made of shapes.
        /// </summary>
        public static float HeroFacing(ArtLibrary art) => art != null && art.ModelOf("hero") != null ? -90f : 0f;

        public static GameObject Hero(ArtLibrary art, Material material, Transform parent, float size, int country)
        {
            var root = new GameObject("Hero");
            root.transform.SetParent(parent, false);
            GameObject prefab = art != null ? art.ModelOf("hero") : null;
            GameObject body;
            if (prefab != null)
                body = Model(prefab, root.transform, size);
            else
            {
                var kit = new MeshKit();
                var rng = new Rng(11);
                Color horse = Palette.Hex("#8B6246"), cloak = Palette.Hex("#486AA7"), steel = Palette.Hex("#D9DEE8");
                kit.Blob(new Vector3(0, 0.36f, 0), new Vector3(0.18f, 0.16f, 0.36f), 8, 4, horse * 0.8f, horse, rng, 0.04f);
                kit.Blob(new Vector3(0, 0.5f, -0.36f), new Vector3(0.08f, 0.1f, 0.12f), 6, 3, horse * 0.85f, horse, rng, 0.04f);
                for (int k = 0; k < 4; k++)
                {
                    float x = k % 2 == 0 ? -0.1f : 0.1f, z = k < 2 ? -0.22f : 0.22f;
                    kit.Cylinder(new Vector3(x, 0f, z), new Vector3(x, 0.26f, z), 0.035f, 5, horse * 0.7f, horse * 0.7f);
                }
                kit.Blob(new Vector3(0, 0.62f, 0.02f), new Vector3(0.12f, 0.16f, 0.1f), 7, 3, cloak * 0.8f, cloak, rng, 0.04f);
                kit.Blob(new Vector3(0, 0.84f, 0.0f), new Vector3(0.08f, 0.08f, 0.08f), 7, 3, steel * 0.85f, steel, rng, 0f);
                kit.Cylinder(new Vector3(0.14f, 0.45f, -0.05f), new Vector3(0.14f, 1.05f, -0.12f), 0.015f, 5, Palette.Wood, Palette.Wood);
                body = Built(kit, "Figure hero", material, root.transform, size);
            }
            body.AddComponent<Idle>().look = 8f;
            // The banner behind the hero's left shoulder.
            Banner(material, root.transform, new Vector3(-0.22f, 0f, 0.14f), size, country);
            return root;
        }

        /// <summary>
        /// A pole <see cref="BannerHeight"/> × <paramref name="size"/> tall from <paramref name="foot"/>
        /// (in the same units) with the kingdom's flag of cloth flying towards −x, or without its
        /// material a two-tailed flag of the kingdom's colour: the hero's banner, and a small one on
        /// the settlers' cart waiting in a town (HeroView).
        /// </summary>
        public static void Banner(Material material, Transform parent, Vector3 foot, float size, int country)
        {
            var flag = new MeshKit();
            var frng = new Rng(3);
            Vector3 top = foot + new Vector3(0, BannerHeight, 0);
            flag.Cylinder(foot, top, 0.018f, 6, Palette.Wood, Palette.Wood);
            flag.Blob(top + new Vector3(0, 0.03f, 0), new Vector3(0.035f, 0.035f, 0.035f), 6, 3, Palette.HayDark, Palette.Gold, frng, 0f);
            if (FlagArt.ClothMaterial != null)
            {
                var cloth = new FlagCloth();
                cloth.Add(top - new Vector3(0, 0.03f, 0), new Vector3(-1f, 0f, 0.1f), 0.5f, 0.33f, country);
                var go = new GameObject("Figure flag");
                go.transform.SetParent(parent, false);
                go.transform.localScale = Vector3.one * size;
                go.AddComponent<MeshFilter>().sharedMesh = cloth.ToMesh("Figure flag");
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = FlagArt.ClothMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                Built(flag, "Figure banner", material, parent, size);
                return;
            }
            Color banner = Palette.Countries[country % Palette.Countries.Length];
            Vector3 a = top - new Vector3(0, 0.03f, 0), b = top - new Vector3(0, 0.38f, 0);
            Vector3 mid = (a + b) * 0.5f + new Vector3(-0.2f, 0f, 0.02f);
            Vector3 tipA = a + new Vector3(-0.42f, -0.06f, 0.04f), tipB = b + new Vector3(-0.42f, 0.04f, 0.04f);
            flag.TwoSided(a, tipA, mid, banner * 1.06f);
            flag.TwoSided(a, mid, b, banner);
            flag.TwoSided(mid, tipB, b, banner * 0.9f);
            Built(flag, "Figure banner", material, parent, size);
        }

        /// <summary>How tall a <see cref="Banner"/>'s pole is at size 1.</summary>
        public const float BannerHeight = 1.15f;

        /// <summary>
        /// A settlers' cart, its pole towards −z, <paramref name="length"/> long along the ground:
        /// the studio model, or a covered wagon of wood and canvas on four wheels. The studio cart
        /// scaled as the hero stood twice as long as his horse and taller than a town's hall
        /// (review of 2026-11-11), as its largest side is not its length.
        /// </summary>
        public static GameObject Cart(ArtLibrary art, Material material, Transform parent, float length)
        {
            GameObject prefab = art != null ? art.ModelOf("cart") : null;
            if (prefab != null)
            {
                var root = new GameObject("Cart");
                root.transform.SetParent(parent, false);
                Model(prefab, root.transform, length / GroundSide(prefab));
                return root;
            }
            var kit = new MeshKit();
            var rng = new Rng(17);
            Color wood = Palette.Wood, dark = Palette.Wood * 0.62f, canvas = Palette.Hex("#D3C6A6");
            kit.Box(new Vector3(0, 0.15f, 0), new Vector3(0.3f, 0.09f, 0.5f), 0f, wood);
            for (int k = 0; k < 4; k++)
            {
                float x = k % 2 == 0 ? -0.17f : 0.17f, z = k < 2 ? -0.16f : 0.16f, side = Mathf.Sign(x);
                kit.Cylinder(new Vector3(x - side * 0.02f, 0.1f, z), new Vector3(x + side * 0.02f, 0.1f, z), 0.1f, 10, dark, wood);
            }
            kit.Blob(new Vector3(0, 0.25f, 0.02f), new Vector3(0.16f, 0.16f, 0.23f), 9, 4, canvas * 0.82f, canvas, rng, 0.015f);
            kit.Cylinder(new Vector3(0, 0.16f, -0.25f), new Vector3(0, 0.1f, -0.52f), 0.018f, 5, wood, wood);
            // From the pole's end to the wagon's back.
            const float KitLength = 0.77f;
            return Built(kit, "Figure cart", material, parent, length / KitLength);
        }

        static readonly Dictionary<GameObject, float> groundSides = new Dictionary<GameObject, float>();

        /// <summary>
        /// The longer side along the ground of the box round a prefab's meshes, in its own units:
        /// Setup makes a model's largest side 1 long, but that may be its height.
        /// </summary>
        public static float GroundSide(GameObject prefab)
        {
            if (groundSides.TryGetValue(prefab, out float side))
                return side;
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
            var box = new Bounds();
            bool any = false;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                Bounds b = filter.sharedMesh.bounds;
                Matrix4x4 toPrefab = toRoot * filter.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var sign = new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f);
                    Vector3 corner = toPrefab.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, sign));
                    if (any)
                        box.Encapsulate(corner);
                    else
                    {
                        box = new Bounds(corner, Vector3.zero);
                        any = true;
                    }
                }
            }
            side = any ? Mathf.Max(box.size.x, box.size.z, 1e-3f) : 1f;
            groundSides[prefab] = side;
            return side;
        }

        /// <summary>A studio model under the parent, scaled to the size, at the parent's origin.</summary>
        static GameObject Model(GameObject prefab, Transform parent, float size)
        {
            GameObject go = Object.Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * size;
            return go;
        }

        /// <summary>A mesh from the kit as a child of the parent, casting shadows.</summary>
        public static GameObject Built(MeshKit kit, string name, Material material, Transform parent, float size = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = kit.ToMesh(name);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        /// <summary>
        /// Destroys the object and the meshes made for it here or merged under it by a
        /// <see cref="ModelBatch"/> ("Props …"); the models' own meshes stay.
        /// </summary>
        public static void Destroy(GameObject go)
        {
            if (go == null)
                return;
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh != null && (mesh.name.StartsWith("Figure ") || mesh.name.StartsWith("Props ")))
                    Object.Destroy(mesh);
            }
            Object.Destroy(go);
        }
    }
}
