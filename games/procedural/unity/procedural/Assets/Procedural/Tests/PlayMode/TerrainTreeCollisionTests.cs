using System.Collections;
using NUnit.Framework;
using Procedural.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Procedural.Tests
{
    /// <summary>The walker against a Terrain tree built from the placeholder pine prefab, as baked levels have them.</summary>
    public class TerrainTreeCollisionTests
    {
        const string PinePrefab = "Assets/Procedural/Placeholders/Plants/pine.prefab";
        GameObject terrain, walker;

        [TearDown]
        public void TearDown()
        {
            if (terrain != null) Object.Destroy(terrain);
            if (walker != null) Object.Destroy(walker);
        }

        [UnityTest]
        public IEnumerator WalkerStopsAtATerrainTree()
        {
#if UNITY_EDITOR
            var pine = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PinePrefab);
#else
            GameObject pine = null;
#endif
            if (pine == null)
                Assert.Ignore("Placeholder pine prefab is missing; run Procedural/Rebuild Plant Placeholders.");

            var data = new TerrainData { heightmapResolution = 33 };
            data.size = new Vector3(64f, 10f, 64f);
            data.treePrototypes = new[] { new TreePrototype { prefab = pine } };
            data.SetTreeInstances(new[]
            {
                new TreeInstance
                {
                    position = new Vector3(32f / 64f, 0f, 40f / 64f),
                    widthScale = 1.3f,
                    heightScale = 1.3f,
                    color = Color.white,
                    lightmapColor = Color.white,
                    prototypeIndex = 0
                }
            }, true);
            terrain = Terrain.CreateTerrainGameObject(data);

            walker = new GameObject("Walker", typeof(CharacterController), typeof(Walker));
            walker.transform.position = new Vector3(32f, 0.1f, 30f);
            var walk = walker.GetComponent<Walker>();
            walk.useInput = false;
            walk.moveInput = new Vector2(0f, 1f);
            walk.view = walker.transform;

            float start = Time.time;
            while (Time.time - start < 3f)
                yield return null;

            // Trunk radius 0.25 × scale 1.3, walker radius 0.35.
            float limit = 40f - 0.25f * 1.3f - 0.35f + 0.1f;
            Assert.Less(walker.transform.position.z, limit, "the walker passed through the tree");
            Assert.Greater(walker.transform.position.z, 35f, "the walker did not move");
        }
    }
}
