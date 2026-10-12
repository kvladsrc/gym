using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// Small detail on the ground, whose baked texture (one of 240 x 300 m,
    /// ~7 cm a pixel) is smooth up close: a detail map on the ground's material
    /// and decals scattered over it (the studio's images, made fit for use by
    /// tools/ground_textures.py into Art/Textures/Ground and Art/Textures/Decals).
    /// </summary>
    public static partial class LevelBuilder
    {
        const string GroundTextures = Art + "/Textures/Ground/";
        const string DecalTextures = Art + "/Textures/Decals/";
        /// <summary>The detail texture's size on the ground, m.</summary>
        const float DetailTile = 3.2f;

        /// <summary>The ground's baked material, copied (an FBX's own material
        /// cannot be changed), with the detail map: grey around 0.5 and
        /// multiplied ×2, so far off, where its mipmaps average to grey, it
        /// leaves the baked colour as it is.</summary>
        static void GroundDetail(Renderer ground)
        {
            var albedo = Texture(GroundTextures + "GroundDetail.png", normal: false);
            var normal = Texture(GroundTextures + "GroundDetailNormal.png", normal: true);
            if (albedo == null || ground == null) return;
            var source = ground.sharedMaterial;
            // The object's name as a file name ("D / Landscape · …" has a slash).
            var file = System.Text.RegularExpressions.Regex.Replace(ground.name, "[^A-Za-z0-9]+", "_").Trim('_');
            var path = $"{Root}/Materials/Ground {file}.mat";
            AssetDatabase.DeleteAsset(path);
            var material = new Material(source) { name = "Ground " + ground.name };
            AssetDatabase.CreateAsset(material, path);
            material.SetTexture("_DetailAlbedoMap", albedo);
            material.SetFloat("_DetailAlbedoMapScale", 0.55f);
            if (normal != null)
            {
                material.SetTexture("_DetailNormalMap", normal);
                material.SetFloat("_DetailNormalMapScale", 0.4f);
            }
            var size = ground.bounds.size;
            var tiling = new Vector2(size.x / DetailTile, size.z / DetailTile);
            material.SetTextureScale("_DetailAlbedoMap", tiling);
            material.EnableKeyword("_DETAIL_SCALED");
            EditorUtility.SetDirty(material);
            ground.sharedMaterial = material;
        }

        const string GroundLayerTextures = Art + "/Textures/GroundLayers/";

        /// <summary>A ground painted from tiled images by masks (Soulslike/
        /// GroundLayers): the studio's six tileable images (tools/ground_textures.py
        /// --layer) at their own scale, its masks as the export baked them, its
        /// baked colour far off. Without the masks, the detail map instead.</summary>
        static void GroundLayers(Renderer ground, string maskA, string maskB)
        {
            var a = Mask(maskA);
            var b = Mask(maskB);
            if (ground == null || a == null || b == null)
            {
                missing.Add("ground masks");
                GroundDetail(ground);
                return;
            }
            var source = ground.sharedMaterial;
            var file = System.Text.RegularExpressions.Regex.Replace(ground.name, "[^A-Za-z0-9]+", "_").Trim('_');
            var material = MaterialAsset("Ground " + file, Shader.Find("Soulslike/GroundLayers"), null);
            material.SetTexture("_Atlas", source.GetTexture("_BaseMap"));
            material.SetTexture("_MaskA", a);
            material.SetTexture("_MaskB", b);
            foreach (var layer in new[] { "Litter", "Needles", "Leaves", "Damp", "Gravel1", "Gravel2" })
            {
                var texture = Texture(GroundLayerTextures + layer + ".png", normal: false);
                if (texture == null) missing.Add("ground layer " + layer);
                material.SetTexture("_" + layer, texture);
            }
            EditorUtility.SetDirty(material);
            ground.sharedMaterial = material;
        }

        /// <summary>Grass on a ground painted by <see cref="GroundLayers"/>
        /// (GrassField, Soulslike/Grass): its masks and baked colour, and the
        /// world X/Z → atlas UV line fitted from the ground's mesh (the export
        /// bakes into a planar UV; ~0.1 m off on average).</summary>
        static void Grass(Renderer ground)
        {
            var source = ground.sharedMaterial;
            var collider = ground.GetComponent<MeshCollider>();
            var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            if (source.shader.name != "Soulslike/GroundLayers" || collider == null || mesh == null)
            {
                missing.Add("grass (no layered ground)");
                return;
            }
            var vertices = mesh.vertices;
            var uv = mesh.uv;
            int n = vertices.Length;
            double sx = 0, sz = 0, su = 0, sv = 0, sxx = 0, szz = 0, sxu = 0, szv = 0;
            for (int i = 0; i < n; i++)
            {
                var w = ground.transform.TransformPoint(vertices[i]);
                sx += w.x; sz += w.z; su += uv[i].x; sv += uv[i].y;
                sxx += w.x * w.x; szz += w.z * w.z; sxu += w.x * uv[i].x; szv += w.z * uv[i].y;
            }
            double au = (n * sxu - sx * su) / (n * sxx - sx * sx), bu = (su - au * sx) / n;
            double av = (n * szv - sz * sv) / (n * szz - sz * sz), bv = (sv - av * sz) / n;

            var material = MaterialAsset("Grass", Shader.Find("Soulslike/Grass"), null);
            material.SetTexture("_Atlas", source.GetTexture("_Atlas"));
            material.SetTexture("_MaskA", source.GetTexture("_MaskA"));
            material.SetTexture("_MaskB", source.GetTexture("_MaskB"));
            material.SetVector("_GroundUV", new Vector4((float)au, (float)bu, (float)av, (float)bv));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            var field = new GameObject("Grass").AddComponent<GrassField>();
            field.ground = collider;
            field.material = material;
        }

        /// <summary>A mask: data, not colour (no sRGB), clamped.</summary>
        static Texture2D Mask(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;
            if (importer.sRGBTexture || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Decals over the floor, in clusters (where a slow noise is
        /// high), none on steep ground; each lies along the ground's slope, turned
        /// at random, faded out past 40 m. The same scatter on every build.</summary>
        static void Decals(Rect area, int count)
        {
            EnsureDecalFeature();
            var materials = new List<Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { DecalTextures.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var texture = Texture(path, normal: false);
                var material = MaterialAsset("Decal " + System.IO.Path.GetFileNameWithoutExtension(path),
                    Shader.Find("Shader Graphs/Decal"), null);
                material.SetTexture("Base_Map", texture);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                materials.Add(material);
            }
            if (materials.Count == 0)
            {
                missing.Add("decals");
                return;
            }
            var parent = new GameObject("Decals").transform;
            var random = new System.Random(7);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            int placed = 0;
            for (int tries = 0; placed < count && tries < count * 12; tries++)
            {
                var at = new Vector3(R(area.xMin, area.xMax), 0f, R(area.yMin, area.yMax));
                if (Mathf.PerlinNoise(at.x * 0.04f + 13f, at.z * 0.04f + 7f) < 0.45f) continue;
                if (FloorHit(at) is not { } hit || Vector3.Angle(hit.normal, Vector3.up) > 35f) continue;
                var decal = new GameObject("Decal").AddComponent<DecalProjector>();
                decal.transform.SetParent(parent, false);
                decal.material = materials[random.Next(materials.Count)];
                float size = R(1.2f, 3.5f);
                decal.size = new Vector3(size, size, 1.2f);
                decal.pivot = Vector3.zero;
                decal.drawDistance = 40f;
                decal.fadeScale = 0.85f;
                decal.transform.position = hit.point;
                // The projector looks along its forward: down into the slope.
                decal.transform.rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, R(0f, 360f));
                placed++;
            }
        }

        /// <summary>The highest floor surface under a point, with its normal.</summary>
        static RaycastHit? FloorHit(Vector3 point)
        {
            RaycastHit? best = null;
            var ray = new Ray(new Vector3(point.x, 200f, point.z), Vector3.down);
            foreach (var collider in floor)
                if (collider != null && collider.Raycast(ray, out var hit, 400f) && (best == null || hit.point.y > best.Value.point.y))
                    best = hit;
            return best;
        }

        /// <summary>The URP renderers draw decals only with the Decal feature:
        /// added to each renderer once, with its entry in the feature map.</summary>
        static void EnsureDecalFeature()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null || data.rendererFeatures.Any(f => f is DecalRendererFeature)) continue;
                var feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
                feature.name = "Decal";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id);
                var serialized = new SerializedObject(data);
                var features = serialized.FindProperty("m_RendererFeatures");
                var map = serialized.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>A texture for tiling (repeat, mipmaps) or, for a normal map, imported as one.</summary>
        static Texture2D Texture(string path, bool normal)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool decal = path.StartsWith(DecalTextures);
            var wrap = decal ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (importer.textureType != type || importer.wrapMode != wrap || !importer.mipmapEnabled
                || importer.alphaIsTransparency != decal)
            {
                importer.textureType = type;
                importer.wrapMode = wrap;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = decal;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
