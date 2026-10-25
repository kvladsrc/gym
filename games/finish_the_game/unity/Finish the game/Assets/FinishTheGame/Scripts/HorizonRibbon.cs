using UnityEngine;

namespace FinishTheGame
{
    /// <summary>The way ahead on the horizon: a ribbon laid along
    /// <see cref="RibbonBend"/>'s curve (it starts on the ground where the
    /// band ends, rises, turns to face the camera, rolls up), showing the
    /// route unrolled into a picture (<see cref="picture"/>: its length along
    /// u, across along v), pressed as the curve presses it: the place u metres
    /// past the band's end is drawn at a = arc (1 − e^(−u/arc)) along the
    /// curve, e^(−u/arc) of its width (and of the curve's own share,
    /// <see cref="RibbonBend.WidthAt"/>: in the sky, a thin ribbon). It starts
    /// <see cref="startArc"/> metres along the curve and fades in over
    /// <see cref="fadeIn"/> metres, where the world near fades out, and away at its tip.</summary>
    [DefaultExecutionOrder(110)]
    public class HorizonRibbon : MonoBehaviour
    {
        public StoryWorld world;
        public RibbonBend ribbon;
        public Material material;
        public Texture2D picture;
        /// <summary>Half the ribbon's width where it starts, m (as the picture's).</summary>
        public float halfWidth = 30f;
        public float startArc, fadeIn = 10f, lift = 0.05f;
        /// <summary>In the sky, the width left at the tip, as a share of the start's.</summary>
        public float tipWidth = 0.3f;
        /// <summary>In the sky, how blurred the picture is at the tip (its mip level): far off, no detail.</summary>
        public float tipBlur = 6f;

        Mesh mesh;
        Vector3[] vertices;
        Vector2[] uvs, blurs;
        Color[] colours;
        MeshRenderer view;

        void Start()
        {
            int n = ribbon.Count;
            vertices = new Vector3[2 * n];
            uvs = new Vector2[2 * n];
            blurs = new Vector2[2 * n];
            colours = new Color[2 * n];
            var triangles = new int[6 * (n - 1)];
            for (int i = 0; i < n - 1; i++)
            {
                int a = 2 * i, b = a + 2;
                triangles[6 * i] = a; triangles[6 * i + 1] = b; triangles[6 * i + 2] = a + 1;
                triangles[6 * i + 3] = a + 1; triangles[6 * i + 4] = b; triangles[6 * i + 5] = b + 1;
            }
            mesh = new Mesh { name = "Horizon" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            view = gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = material;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            StoryWorld.Bendable(view);
        }

        void LateUpdate()
        {
            if (picture == null) return;
            material.mainTexture = picture;
            var path = world.Path;
            float start = ribbon.Start;
            for (int i = 0; i < ribbon.Count; i++)
            {
                float a = ribbon.ArcAt(i);
                float s = ribbon.StoryAt(a);
                // As wide as the strip's start; in the sky, narrowing on to its tip.
                float scale = Mathf.Exp(-Mathf.Min(s - start, ribbon.arc * 3f) / ribbon.arc);
                if (ribbon.sky)
                    scale = Mathf.Lerp(1f, tipWidth, Mathf.InverseLerp(0f, ribbon.ArcAt(ribbon.Count - 1), a));
                Vector3 centre = ribbon.Point(i), side = ribbon.SideAt(i) * (halfWidth * scale * ribbon.WidthAt(i) * ribbon.RunAt(i));
                centre.y += lift;
                vertices[2 * i] = centre - side;
                vertices[2 * i + 1] = centre + side;
                float u = s / path.Length;
                uvs[2 * i] = new Vector2(u, 0);
                uvs[2 * i + 1] = new Vector2(u, 1);
                float far = ribbon.sky ? Mathf.InverseLerp(0f, ribbon.ArcAt(ribbon.Count - 1), a) : 0f;
                blurs[2 * i] = blurs[2 * i + 1] = new Vector2(tipBlur * far, 0);
                // Faded away at the tip, but in the sky: there, the story's end.
                float alpha = Mathf.SmoothStep(0, 1, (a - startArc) / fadeIn)
                    * (ribbon.sky ? 1f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f * ribbon.arc, ribbon.arc, a)))
                    * (path.Closed || s <= path.Length ? 1f : 0f);
                colours[2 * i] = colours[2 * i + 1] = new Color(1, 1, 1, alpha);
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.uv2 = blurs;
            mesh.colors = colours;
            mesh.RecalculateBounds();
        }
    }
}
