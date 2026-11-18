using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Terrain;
using UnityEditor;

namespace Game.Terrain.Tests
{
    public sealed class TerrainTests
    {
        static readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var so in _created)
            {
                if (so != null) UnityEngine.Object.DestroyImmediate(so);
            }
            _created.Clear();
        }

        static TerrainSettings CreateSettings(int seed = 12345)
        {
            var settings = ScriptableObject.CreateInstance<TerrainSettings>();
            _created.Add(settings);

            settings.chunkSize = 16;
            settings.chunksX = 10;
            settings.chunksZ = 10;
            settings.blockSize = 1f;
            settings.stepHeight = 0.5f;
            settings.maxLevel = 8;
            settings.flatRadius = 12f;
            settings.flatBlend = 8f;
            settings.flatLevel = 1;
            settings.seed = seed;

            settings.zone1Radius = 36f;
            settings.zone2Radius = 58f;
            settings.zone1ReliefLevels = 4;
            settings.zone2BaseLevel = 6;
            settings.zone3BaseLevel = 0;
            settings.ringReliefLevels = 2;
            settings.rampWidth = 10f;
            settings.rampHalfAngle = 9f;

            settings.lakeCount = 3;
            settings.lakeRadius = 4;
            settings.lakeDepth = 2;
            return settings;
        }

        // ---------------------------------------------------------------
        // HeightMap generation
        // ---------------------------------------------------------------

        [Test]
        public void HeightMap_HasWorldDimensions()
        {
            var settings = CreateSettings();
            HeightMap map = HeightMapGenerator.Generate(settings);

            Assert.AreEqual(160, map.Width);
            Assert.AreEqual(160, map.Depth);
        }

        [Test]
        public void HeightMap_LevelsWithinRange()
        {
            var settings = CreateSettings();
            HeightMap map = HeightMapGenerator.Generate(settings);

            for (int z = 0; z < map.Depth; z++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    int level = map.Get(x, z);
                    if (level < 0 || level > settings.maxLevel)
                    {
                        Assert.Fail($"Level {level} at ({x},{z}) is outside [0, {settings.maxLevel}].");
                    }
                }
            }
        }

        [Test]
        public void HeightMap_SameSeedSameResult()
        {
            var settingsA = CreateSettings(777);
            var settingsB = CreateSettings(777);
            var settingsC = CreateSettings(778);

            HeightMap a = HeightMapGenerator.Generate(settingsA);
            HeightMap b = HeightMapGenerator.Generate(settingsB);
            HeightMap c = HeightMapGenerator.Generate(settingsC);

            Assert.AreEqual(a.Width, b.Width);
            Assert.AreEqual(a.Depth, b.Depth);

            for (int z = 0; z < a.Depth; z++)
            {
                for (int x = 0; x < a.Width; x++)
                {
                    Assert.AreEqual(a.Get(x, z), b.Get(x, z),
                        $"Same seed produced different levels at ({x},{z}).");
                }
            }

            bool anyDifference = false;
            for (int z = 0; z < a.Depth && !anyDifference; z++)
            {
                for (int x = 0; x < a.Width && !anyDifference; x++)
                {
                    if (a.Get(x, z) != c.Get(x, z)) anyDifference = true;
                }
            }

            Assert.IsTrue(anyDifference, "Seeds 777 and 778 produced identical maps.");
        }

        [Test]
        public void HeightMap_CenterIsFlat()
        {
            var settings = CreateSettings();
            HeightMap map = HeightMapGenerator.Generate(settings);

            float centerX = map.Width * 0.5f;
            float centerZ = map.Depth * 0.5f;

            for (int z = 0; z < map.Depth; z++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    float dx = (x + 0.5f) - centerX;
                    float dz = (z + 0.5f) - centerZ;
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);

                    if (distance <= settings.flatRadius)
                    {
                        Assert.AreEqual(settings.flatLevel, map.Get(x, z),
                            $"Cell ({x},{z}) at distance {distance} from center should be flat.");
                    }
                }
            }
        }

        // ---------------------------------------------------------------
        // Zonas y lagos
        // ---------------------------------------------------------------

        [Test]
        public void HeightMap_ZonesAreConcentricAndDifferInHeight()
        {
            var settings = CreateSettings();
            HeightMap map = HeightMapGenerator.Generate(settings);

            int cx = map.Width / 2;
            int cz = map.Depth / 2;

            Assert.AreEqual(1, map.GetZone(cx, cz), "El centro debe ser zona 1.");
            Assert.AreEqual(2, map.GetZone(cx + 45, cz), "A 45 bloques del centro debe empezar la zona 2.");
            Assert.AreEqual(3, map.GetZone(cx + 68, cz), "A 68 bloques del centro debe empezar la zona 3.");

            int zone1Level = map.Get(cx, cz);
            int zone2Level = map.Get(cx + 45, cz);
            int zone3Level = map.Get(cx + 68, cz);

            Assert.Greater(zone2Level, zone1Level + 2, "La zona 2 debe ser claramente más alta que la 1.");
            Assert.Less(zone3Level, zone2Level - 2, "La zona 3 debe ser claramente más baja que la 2.");
        }

        [Test]
        public void HeightMap_HasRampsBetweenZones()
        {
            var settings = CreateSettings();
            HeightMap map = HeightMapGenerator.Generate(settings);

            int cz = map.Depth / 2;
            int previous = map.Get(26, cz);

            // Hacia +X hay una rampa: el nivel nunca sube más de un bloque a la vez.
            for (int x = 27; x <= 68; x++)
            {
                int level = map.Get(x, cz);
                Assert.LessOrEqual(Mathf.Abs(level - previous), 1,
                    $"Escalón de {Mathf.Abs(level - previous)} bloques en x={x} (rampa +X).");
                previous = level;
            }
        }

        [Test]
        public void HeightMap_LakesAreTwoBlocksDeep()
        {
            var settings = CreateSettings();
            HeightMap map = HeightMapGenerator.Generate(settings);

            Assert.Greater(map.Lakes.Count, 0, "Debería generarse al menos un lago.");

            foreach (var lake in map.Lakes)
            {
                Assert.AreEqual(settings.lakeDepth, lake.RimLevel - lake.BottomLevel,
                    "El lago debe tener exactamente la profundidad configurada.");
                Assert.Less(lake.BottomLevel, lake.RimLevel);
                Assert.AreEqual(lake.BottomLevel, map.Get(lake.Center.x, lake.Center.y));
            }
        }

        [TestCase(12345, 10, 36f)]
        [TestCase(12345, 11, 42f)]
        [TestCase(777, 11, 42f)]
        [TestCase(778, 12, 42f)]
        public void LargeLake_ExistsAndPreservesSpawnAndCardinalAccess(int seed, int radius, float zoneRadius)
        {
            var settings = CreateSettings(seed);
            settings.zone1Radius = zoneRadius;
            settings.lakeRadius = radius;
            settings.lakeCount = 1;
            var map = HeightMapGenerator.Generate(settings);
            var repeated = HeightMapGenerator.Generate(settings);
            settings.lakeCount = 0;
            var dry = HeightMapGenerator.Generate(settings);

            Assert.AreEqual(1, map.Lakes.Count, "Una configuración factible no puede quedarse sin lago.");
            Assert.AreEqual(radius, map.Lakes[0].Radius);
            Assert.AreEqual(map.Lakes[0].Center, repeated.Lakes[0].Center);
            Assert.AreEqual(map.Lakes[0].RimLevel, repeated.Lakes[0].RimLevel);
            for (int z = 0; z < map.Depth; z++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Assert.AreEqual(map.Get(x, z), repeated.Get(x, z));
                    float dx = x + 0.5f - map.Width * 0.5f;
                    float dz = z + 0.5f - map.Depth * 0.5f;
                    float angle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
                    bool corridor = Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Round(angle / 90f) * 90f))
                                    <= settings.rampHalfAngle;
                    if (dx * dx + dz * dz <= settings.flatRadius * settings.flatRadius || corridor)
                    {
                        Assert.IsFalse(map.IsLakeCell(x, z), $"Acceso ocupado en {x},{z}");
                        Assert.AreEqual(dry.Get(x, z), map.Get(x, z), $"Acceso alterado en {x},{z}");
                    }
                    if (map.IsLakeCell(x, z))
                        Assert.AreEqual(map.Lakes[0].BottomLevel, map.Get(x, z));
                }
            }
        }

        [Test]
        public void LargeLake_InfeasibleSettingsDoNotDamageSpawnOrShrinkRequestedLake()
        {
            var settings = CreateSettings();
            settings.lakeRadius = 12;
            settings.zone1Radius = 24f;
            var map = HeightMapGenerator.Generate(settings);
            Assert.AreEqual(0, map.Lakes.Count);
            Assert.AreEqual(settings.flatLevel, map.Get(map.Width / 2, map.Depth / 2));
        }

        [Test]
        public void LakeWaterRows_ExactlyMatchExcavationMask()
        {
            var lake = new LakeInfo { Center = new Vector2Int(30, 40), Radius = 11 };
            Mesh mesh = LakeGeometry.BuildWaterMesh(lake, 1f, 2f);
            try
            {
                Assert.AreEqual(23 * 4, mesh.vertexCount);
                var vertices = mesh.vertices;
                for (int z = lake.Center.y - 12; z <= lake.Center.y + 12; z++)
                {
                    for (int x = lake.Center.x - 12; x <= lake.Center.x + 12; x++)
                    {
                        bool overWater = false;
                        for (int i = 0; i < vertices.Length; i += 4)
                            overWater |= x + 0.5f >= vertices[i].x && x + 0.5f < vertices[i + 1].x &&
                                         z + 0.5f >= vertices[i].z && z + 0.5f < vertices[i + 2].z;
                        Assert.AreEqual(lake.ContainsCell(x, z), overWater, $"Máscara en {x},{z}");
                    }
                }
                var indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                    Assert.Greater(Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]],
                        vertices[indices[i + 2]] - vertices[indices[i]]).y, 0f);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void GrassAtlas_TonesVaryWithoutExtraVerticesOrSubmeshes()
        {
            var map = new HeightMap(16, 16);
            var a = new ChunkMeshData();
            var b = new ChunkMeshData();
            ChunkMeshBuilder.Build(map, 0, 0, 16, 1f, 0.5f, a);
            ChunkMeshBuilder.Build(map, 0, 0, 16, 1f, 0.5f, b);
            CollectionAssert.AreEqual(a.UVs, b.UVs);
            var tiles = new HashSet<Vector2>();
            for (int i = 0; i < a.Triangles[0].Count; i += 6) tiles.Add(a.UVs[a.Triangles[0][i]]);
            Assert.Greater(tiles.Count, 8, "El pasto no debe repetir un único tono por bloque.");
            Assert.AreEqual(6, a.SubMeshCount);
            Assert.AreEqual(16 * 16 * 4 + 16 * 4 * 4, a.Vertices.Count);
            Assert.AreEqual(a.Vertices.Count, a.UVs.Count);
        }

        [Test]
        public void GrassAtlas_GlobalCellKeepsToneAcrossChunkSizes()
        {
            var map = new HeightMap(32, 32);
            var whole = new ChunkMeshData();
            var right = new ChunkMeshData();
            ChunkMeshBuilder.Build(map, 0, 0, 32, 1f, 0.5f, whole);
            ChunkMeshBuilder.Build(map, 1, 0, 16, 1f, 0.5f, right);
            int wholeTop = whole.Triangles[0][(5 * 32 + 16) * 6];
            int rightTop = right.Triangles[0][5 * 16 * 6];
            for (int corner = 0; corner < 4; corner++)
                Assert.AreEqual(whole.UVs[wholeTop + corner], right.UVs[rightTop + corner]);
        }

        // ---------------------------------------------------------------
        // Chunk mesh building
        // ---------------------------------------------------------------

        [Test]
        public void IOS_TerrainAssetReferencesURPMaterialAndBuildScene()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TerrainSettings>("Assets/Settings/TerrainSettings.asset");
            Assert.IsNotNull(settings);
            Assert.IsNotNull(settings.terrainMaterial, "iOS necesita el material como asset referenciado.");
            Assert.IsNotNull(settings.terrainMaterial.shader);
            Assert.IsTrue(settings.terrainMaterial.shader.name.Contains("Universal Render Pipeline"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/World.unity"));

            Material[] materials = TerrainMaterials.Create(settings);
            try
            {
                Assert.AreEqual(ChunkMeshBuilder.SubMeshCount, materials.Length);
                foreach (var material in materials)
                {
                    Assert.IsNotNull(material.shader);
                    Assert.AreEqual(settings.terrainMaterial.shader, material.shader);
                }
            }
            finally
            {
                var texture = materials[0] != null ? materials[0].mainTexture : null;
                foreach (var material in materials)
                    if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ChunkMesh_FlatInteriorChunk_Has1024Vertices()
        {
            const int chunkSize = 16;

            var map = new HeightMap(48, 48);
            for (int z = 0; z < map.Depth; z++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    map.Set(x, z, 2);
                }
            }

            var data = new ChunkMeshData();
            ChunkMeshBuilder.Build(map, 1, 1, chunkSize, 1f, 0.5f, data);

            // Interior chunk: every neighbor is at the same level, so only the 256 tops are emitted.
            Assert.AreEqual(chunkSize * chunkSize * 4, data.Vertices.Count);

            int indexCount = 0;
            foreach (List<int> tris in data.Triangles)
            {
                indexCount += tris.Count;
            }
            Assert.AreEqual(chunkSize * chunkSize * 6, indexCount);
        }

        [Test]
        public void ChunkMesh_RaisedBlockIn3x3_Has116Vertices()
        {
            var map = new HeightMap(3, 3);
            for (int z = 0; z < map.Depth; z++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    map.Set(x, z, 0);
                }
            }
            map.Set(1, 1, 2);

            var data = new ChunkMeshData();
            ChunkMeshBuilder.Build(map, 0, 0, 3, 1f, 0.5f, data);

            // Expected breakdown (4 vertices per quad):
            //   9 tops                                   -> 9 * 4        = 36
            //   Center block walls: 4 sides * 2 levels   -> 4 * 2 * 4    = 32
            //     (levels 0 and 1, since neighbors are at level 0)
            //   World border: 12 edge faces * 1 level    -> 12 * 1 * 4   = 48
            //     (each edge cell drops to the -1 floor slab)
            // Total: 36 + 32 + 48 = 116
            Assert.AreEqual(116, data.Vertices.Count);
        }

        [Test]
        public void ChunkMesh_NoInvertedTriangles()
        {
            var settings = CreateSettings();
            HeightMap map = HeightMapGenerator.Generate(settings);

            var chunks = new (int x, int z)[]
            {
                (0, 0), (4, 4), (9, 9), (0, 9), (5, 2)
            };

            foreach (var chunk in chunks)
            {
                var data = new ChunkMeshData();
                ChunkMeshBuilder.Build(map, chunk.x, chunk.z, settings.chunkSize,
                    settings.blockSize, settings.stepHeight, data);

                List<Vector3> v = data.Vertices;
                List<Vector3> n = data.Normals;

                for (int s = 0; s < data.SubMeshCount; s++)
                {
                    List<int> tris = data.Triangles[s];
                    for (int t = 0; t + 2 < tris.Count; t += 3)
                    {
                        int a = tris[t];
                        int b = tris[t + 1];
                        int c = tris[t + 2];

                        Vector3 geometric = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                        if (!(Vector3.Dot(geometric, n[a]) > 0f))
                        {
                            Assert.Fail(
                                $"Chunk ({chunk.x},{chunk.z}), submesh {s}, triangle {t / 3}: " +
                                $"geometric normal {geometric} disagrees with stored normal {n[a]}.");
                        }
                    }
                }
            }
        }

        [Test]
        public void ChunkMesh_NormalsAndUVsMatchVertexCount()
        {
            var settings = CreateSettings();
            HeightMap map = HeightMapGenerator.Generate(settings);

            var data = new ChunkMeshData();
            ChunkMeshBuilder.Build(map, 4, 4, settings.chunkSize,
                settings.blockSize, settings.stepHeight, data);

            Assert.AreEqual(data.Vertices.Count, data.Normals.Count);
            Assert.AreEqual(data.Vertices.Count, data.UVs.Count);
        }
    }
}
