using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Procedural.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Procedural.Tests
{
    /// <summary>PlayMode checks that the first-person walker collides and lands.</summary>
    public class WalkerCollisionTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        readonly List<Object> assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < objects.Count; i++)
                if (objects[i] != null)
                    Object.Destroy(objects[i]);
            objects.Clear();

            for (int i = 0; i < assets.Count; i++)
                if (assets[i] != null)
                    Object.Destroy(assets[i]);
            assets.Clear();
        }

        [UnityTest]
        public IEnumerator WalkerStopsAtATrunk()
        {
            CreateFlatTerrain();
            CreateTrunk();

            var walker = CreateWalker(new Vector3(32f, 0.1f, 30f));
            walker.useInput = false;
            walker.moveInput = new Vector2(0f, 1f);
            float startZ = walker.transform.position.z;

            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline)
                yield return null;

            float z = walker.transform.position.z;
            Assert.Less(z, 40f - 0.3f - 0.35f + 0.1f, "walker entered the trunk");
            Assert.GreaterOrEqual(z - startZ, 5f, "walker did not move forward");
        }

        [UnityTest]
        public IEnumerator WalkerStandsOnTerrain()
        {
            CreateFlatTerrain();

            var walker = CreateWalker(new Vector3(32f, 3f, 30f));
            walker.useInput = false;
            walker.moveInput = Vector2.zero;

            float deadline = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(Mathf.Abs(walker.transform.position.y), Is.LessThanOrEqualTo(0.15f),
                "walker did not settle on the terrain");
            Assert.IsTrue(walker.isGrounded, "walker is not grounded");
        }

        Terrain CreateFlatTerrain()
        {
            var data = new TerrainData
            {
                heightmapResolution = 33,
                size = new Vector3(64f, 10f, 64f)
            };
            data.SetHeights(0, 0, new float[33, 33]);
            assets.Add(data);

            var terrainObject = Terrain.CreateTerrainGameObject(data);
            objects.Add(terrainObject);
            Physics.SyncTransforms();
            return terrainObject.GetComponent<Terrain>();
        }

        void CreateTrunk()
        {
            var trunk = new GameObject("Trunk");
            trunk.transform.position = new Vector3(32f, 2f, 40f);
            var capsule = trunk.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = 4f;
            objects.Add(trunk);
        }

        Walker CreateWalker(Vector3 position)
        {
            var walkerObject = new GameObject("Walker");
            walkerObject.transform.position = position;
            walkerObject.transform.rotation = Quaternion.identity;
            var walker = walkerObject.AddComponent<Walker>();
            objects.Add(walkerObject);
            return walker;
        }
    }
}
