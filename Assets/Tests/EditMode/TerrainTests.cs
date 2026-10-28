using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Terrain;

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
            settings.maxLevel = 4;
            settings.flatRadius = 12f;
            settings.flatBlend = 8f;
            settings.flatLevel = 1;
            settings.seed = seed;
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
        // Chunk mesh building
        // ---------------------------------------------------------------

        [Test]
        public void ChunkMesh_FlatInteriorChunk_Has1024Vertices()
        {
            const int chunkSize = 16;
            const int maxLevel = 4;

            var map = new HeightMap(48, 48);
            for (int z = 0; z < map.Depth; z++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    map.Set(x, z, 2);
                }
            }

            var data = new ChunkMeshData();
            ChunkMeshBuilder.Build(map, 1, 1, chunkSize, 1f, 0.5f, maxLevel, data);

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
            ChunkMeshBuilder.Build(map, 0, 0, 3, 1f, 0.5f, 4, data);

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
                    settings.blockSize, settings.stepHeight, settings.maxLevel, data);

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
                settings.blockSize, settings.stepHeight, settings.maxLevel, data);

            Assert.AreEqual(data.Vertices.Count, data.Normals.Count);
            Assert.AreEqual(data.Vertices.Count, data.UVs.Count);
        }
    }
}
