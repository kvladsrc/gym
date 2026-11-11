using System;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Realms
{
    /// <summary>
    /// Pictures of the units and the hero for the interface: each figure is rendered once, three
    /// quarters from the front, onto a light grey-beige ground in a small texture. People are
    /// framed from the waist up, so that a spearman reads by his helmet and spear at a slot's size;
    /// the animals and monsters stay whole (the family's rule: never a troll's, wolf's or boar's
    /// head on its own). The figures are lifted about a third lighter than on the map: on the dark
    /// panels the small pictures were dark brown on dark grey (review of 2026-11-10).
    /// </summary>
    public sealed class Portraits : IDisposable
    {
        /// <summary>A layer nothing else uses, so the portrait camera sees the figure alone.</summary>
        const int Layer = 31;

        const int Size = 192;

        /// <summary>
        /// Each picture is rendered at this many times its size and averaged down, which smooths
        /// the figure's edges: a multisampled target read back for the CPU came out as noise.
        /// </summary>
        const int Supersample = 2;
        static readonly Vector3 Stage = new Vector3(5000f, 0f, 5000f);

        /// <summary>How much of the picture the framed part of a figure fills.</summary>
        const float Fill = 0.9f;

        /// <summary>The share of a person's height, from the top, that the picture shows: the head down to the waist.</summary>
        const float Waist = 0.62f;

        /// <summary>The ground behind a figure, lighter in the middle than at the edges.</summary>
        static readonly Color GroundMiddle = Palette.Hex("#A89E8B"), GroundEdge = Palette.Hex("#7F7666");

        readonly Dictionary<Unit, Texture2D> units = new Dictionary<Unit, Texture2D>();
        Texture2D hero;

        public Texture2D Of(Unit unit) => units.TryGetValue(unit, out Texture2D t) ? t : null;

        public Texture2D Hero => hero;

        /// <summary>The units shown from the waist up, the people; the animals and monsters are shown whole.</summary>
        static bool Person(Unit unit) => unit == Unit.Spearmen || unit == Unit.Archers || unit == Unit.Riders || unit == Unit.Bandits;

        public static Portraits Render(ArtLibrary art, Material material, int country)
        {
            var portraits = new Portraits();
            var stage = new GameObject("Portrait stage");
            stage.transform.position = Stage;
            var cameraObject = new GameObject("Portrait camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.cullingMask = 1 << Layer;
            camera.fieldOfView = 24f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            const int side = Size * Supersample;
            var target = new RenderTexture(side, side, 24, RenderTextureFormat.ARGB32) { name = "Portrait" };
            target.Create();
            var readback = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Portrait readback" };
            camera.targetTexture = target;
            try
            {
                foreach (Unit unit in Enum.GetValues(typeof(Unit)))
                {
                    GameObject figure = Figures.Unit(unit, art, material, stage.transform, 1f);
                    portraits.units[unit] = Shoot(camera, target, readback, figure, Person(unit) ? Waist : 1f);
                    Remove(figure);
                }
                GameObject knight = Figures.Hero(art, material, stage.transform, 1f, country);
                portraits.hero = Shoot(camera, target, readback, knight, Waist);
                Remove(knight);
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(readback);
                UnityEngine.Object.Destroy(cameraObject);
                UnityEngine.Object.Destroy(stage);
            }
            return portraits;
        }

        /// <summary>
        /// Takes a figure off the stage. Destroy waits for the end of the frame and all the
        /// pictures are shot in one, so each figure stood on behind the next ones: the hero's
        /// picture came out with every unit round him, the troll among them (2026-11-10).
        /// </summary>
        static void Remove(GameObject figure)
        {
            figure.SetActive(false);
            Figures.Destroy(figure);
        }

        /// <summary>
        /// Frames the top <paramref name="share"/> of the figure's height from the front, a little
        /// from above and the side, renders it and lays it on the picture's ground.
        /// </summary>
        static Texture2D Shoot(Camera camera, RenderTexture target, Texture2D readback, GameObject figure, float share)
        {
            foreach (Transform t in figure.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = Layer;
            foreach (Idle idle in figure.GetComponentsInChildren<Idle>(true))
                idle.enabled = false;
            figure.transform.localRotation = Quaternion.Euler(0f, -28f, 0f);
            var bounds = new Bounds(figure.transform.position + Vector3.up * 0.5f, Vector3.one * 0.1f);
            bool any = false;
            foreach (Renderer r in figure.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                    bounds = r.bounds;
                else
                    bounds.Encapsulate(r.bounds);
                any = true;
            }
            Vector3 min = bounds.min, max = bounds.max;
            min.y = Mathf.Max(min.y, max.y - share * bounds.size.y);
            var box = new Bounds();
            box.SetMinMax(min, max);

            // The camera stands back until every corner of the box is inside the picture.
            Quaternion turn = Quaternion.Euler(14f, 0f, 0f);
            Vector3 look = turn * Vector3.forward, right = turn * Vector3.right, up = turn * Vector3.up;
            float tan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * Fill, distance = 0.1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z) - box.center;
                float across = Mathf.Max(Mathf.Abs(Vector3.Dot(c, right)), Mathf.Abs(Vector3.Dot(c, up)));
                distance = Mathf.Max(distance, across / tan - Vector3.Dot(c, look));
            }
            camera.transform.SetPositionAndRotation(box.center - look * distance, turn);
            camera.Render();

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            RenderTexture.active = active;
            var picture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "Portrait " + figure.name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
            };
            picture.SetPixels32(Compose(readback.GetPixels32()));
            picture.Apply(true, true);
            return picture;
        }

        /// <summary>
        /// The picture from the rendered one (<see cref="Supersample"/> times larger): the figure,
        /// lifted lighter (most in its darks, little in its lights, so nothing burns out), on the
        /// ground, lighter a little above the middle and darker to the edges. The camera cleared
        /// to transparent and the figure is opaque, so in each block of rendered pixels the share
        /// that is figure is how much of the picture's pixel it covers.
        /// </summary>
        static Color32[] Compose(Color32[] rendered)
        {
            const int side = Size * Supersample, samples = Supersample * Supersample;
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f) / Size - 0.5f, dy = (y + 0.5f) / Size - 0.56f;
                    float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 0.72f);
                    Color colour = Color.Lerp(GroundMiddle, GroundEdge, t * t);
                    float r = 0f, g = 0f, b = 0f, cover = 0f;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            Color32 p = rendered[(y * Supersample + sy) * side + x * Supersample + sx];
                            float a = p.a / 255f;
                            r += p.r / 255f * a;
                            g += p.g / 255f * a;
                            b += p.b / 255f * a;
                            cover += a;
                        }
                    }
                    if (cover > 0f)
                    {
                        var figure = new Color(Lift(r / cover), Lift(g / cover), Lift(b / cover));
                        colour = Color.Lerp(colour, figure, cover / samples);
                    }
                    colour.a = 1f;
                    pixels[y * Size + x] = colour;
                }
            }
            return pixels;
        }

        /// <summary>About a third lighter in the darks, an eighth in the lights.</summary>
        static float Lift(float v) => 1f - Mathf.Pow(1f - Mathf.Clamp01(v), 1.45f);

        public void Dispose()
        {
            foreach (Texture2D t in units.Values)
                UnityEngine.Object.Destroy(t);
            units.Clear();
            if (hero != null)
                UnityEngine.Object.Destroy(hero);
            hero = null;
        }
    }
}
