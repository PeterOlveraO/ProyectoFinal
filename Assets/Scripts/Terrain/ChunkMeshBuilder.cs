using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Terrain
{
    /// <summary>Geometría de un chunk antes de pasarla a un Mesh (permite probarla sin GPU).</summary>
    public sealed class ChunkMeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(4096);
        public readonly List<Vector3> Normals = new List<Vector3>(4096);
        public readonly List<Vector2> UVs = new List<Vector2>(4096);

        /// <summary>Submeshes: 0..2 tapas de zona 1..3, 3..5 costados de zona 1..3.</summary>
        public List<int>[] Triangles = new List<int>[0];

        public int SubMeshCount => Triangles.Length;
        public int ZoneCount => 3;
        public int TopSubMesh(int zone) => Mathf.Clamp(zone, 1, ZoneCount) - 1;
        public int SideSubMesh(int zone) => ZoneCount + Mathf.Clamp(zone, 1, ZoneCount) - 1;

        public void Reset(int subMeshCount)
        {
            Vertices.Clear();
            Normals.Clear();
            UVs.Clear();

            if (Triangles.Length != subMeshCount)
            {
                Triangles = new List<int>[subMeshCount];
                for (int i = 0; i < subMeshCount; i++) Triangles[i] = new List<int>(1024);
            }
            else
            {
                for (int i = 0; i < subMeshCount; i++) Triangles[i].Clear();
            }
        }
    }

    /// <summary>
    /// Malla escalonada de un chunk con solo las caras visibles:
    ///   - Tapa superior de cada bloque, a level * stepHeight, con material de su zona.
    ///   - Costados únicamente donde el vecino es más bajo, una cara por nivel de desnivel.
    ///     Los vecinos se leen del mapa GLOBAL, así que en las uniones entre chunks no se
    ///     generan caras ocultas.
    ///   - En el borde del mundo los costados bajan hasta el nivel -1 (losa de cierre).
    /// Cada cara usa un tile del atlas de pasto, con tono determinista por celda global.
    /// La malla queda en espacio local del chunk (origen en su esquina mínima).
    /// </summary>
    public static class ChunkMeshBuilder
    {
        /// <summary>Nivel que se usa como fondo fuera del mundo.</summary>
        public const int BorderBottomLevel = -1;

        /// <summary>Submeshes totales: 3 tapas + 3 costados (uno por zona).</summary>
        public const int SubMeshCount = 6;

        public static void Build(HeightMap map, int chunkX, int chunkZ, int chunkSize,
                                 float blockSize, float stepHeight, ChunkMeshData data)
        {
            data.Reset(SubMeshCount);

            int originX = chunkX * chunkSize;
            int originZ = chunkZ * chunkSize;

            for (int lz = 0; lz < chunkSize; lz++)
            {
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    int gx = originX + lx;
                    int gz = originZ + lz;
                    if (!map.Contains(gx, gz)) continue;

                    int level = map.Get(gx, gz);
                    int zone = map.GetZone(gx, gz);
                    float y = level * stepHeight;
                    float x0 = lx * blockSize;
                    float x1 = x0 + blockSize;
                    float z0 = lz * blockSize;
                    float z1 = z0 + blockSize;

                    // Tapa (material de la zona).
                    AddQuad(data, data.Triangles[data.TopSubMesh(zone)],
                            new Vector3(x0, y, z0), new Vector3(x1, y, z0),
                            new Vector3(x1, y, z1), new Vector3(x0, y, z1),
                            Vector3.up, false);
                    SetGrassUVs(data, gx, gz);

                    // Costados hacia vecinos más bajos (o hacia el borde del mundo).
                    var sides = data.Triangles[data.SideSubMesh(zone)];

                    for (int k = NeighborLevel(map, gx - 1, gz); k < level; k++)
                        AddQuad(data, sides,
                                new Vector3(x0, k * stepHeight, z0), new Vector3(x0, (k + 1) * stepHeight, z0),
                                new Vector3(x0, (k + 1) * stepHeight, z1), new Vector3(x0, k * stepHeight, z1),
                                Vector3.left, true);

                    for (int k = NeighborLevel(map, gx + 1, gz); k < level; k++)
                        AddQuad(data, sides,
                                new Vector3(x1, k * stepHeight, z1), new Vector3(x1, (k + 1) * stepHeight, z1),
                                new Vector3(x1, (k + 1) * stepHeight, z0), new Vector3(x1, k * stepHeight, z0),
                                Vector3.right, true);

                    for (int k = NeighborLevel(map, gx, gz - 1); k < level; k++)
                        AddQuad(data, sides,
                                new Vector3(x1, k * stepHeight, z0), new Vector3(x1, (k + 1) * stepHeight, z0),
                                new Vector3(x0, (k + 1) * stepHeight, z0), new Vector3(x0, k * stepHeight, z0),
                                Vector3.back, true);

                    for (int k = NeighborLevel(map, gx, gz + 1); k < level; k++)
                        AddQuad(data, sides,
                                new Vector3(x0, k * stepHeight, z1), new Vector3(x0, (k + 1) * stepHeight, z1),
                                new Vector3(x1, (k + 1) * stepHeight, z1), new Vector3(x1, k * stepHeight, z1),
                                Vector3.forward, true);
                }
            }
        }

        /// <summary>Vuelca <paramref name="data"/> en <paramref name="mesh"/> (un submesh por material).</summary>
        public static void Apply(ChunkMeshData data, Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = data.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(data.Vertices);
            mesh.SetNormals(data.Normals);
            mesh.SetUVs(0, data.UVs);
            mesh.subMeshCount = data.SubMeshCount;
            for (int i = 0; i < data.SubMeshCount; i++)
                mesh.SetTriangles(data.Triangles[i], i, false);
            mesh.RecalculateBounds();
        }

        static int NeighborLevel(HeightMap map, int x, int z)
        {
            return map.Contains(x, z) ? map.Get(x, z) : BorderBottomLevel;
        }

        static void SetGrassUVs(ChunkMeshData data, int x, int z)
        {
            // Coordenadas globales: mismo tono aunque cambie el tamaño/orden del chunk.
            uint hash = unchecked((uint)(x * 73856093) ^ (uint)(z * 19349663));
            hash ^= hash >> 13;
            const int tiles = TerrainMaterials.GrassTilesPerAxis;
            int tile = (int)(hash % (tiles * tiles));
            float scale = 1f / tiles;
            float u = tile % tiles * scale;
            float v = tile / tiles * scale;
            int start = data.UVs.Count - 4;
            data.UVs[start] = new Vector2(u, v);
            data.UVs[start + 1] = new Vector2(u + scale, v);
            data.UVs[start + 2] = new Vector2(u + scale, v + scale);
            data.UVs[start + 3] = new Vector2(u, v + scale);
        }

        /// <summary>
        /// Quad p0..p3. Para tapas: p0=(x0,z0) p1=(x1,z0) p2=(x1,z1) p3=(x0,z1).
        /// Para costados: p0 abajo, p1 arriba, p2 arriba, p3 abajo.
        /// Triángulos (p0,p2,p1) y (p0,p3,p2): su normal geométrica coincide con
        /// <paramref name="normal"/> en el winding de Unity.
        /// </summary>
        static void AddQuad(ChunkMeshData data, List<int> triangles,
                            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, bool side)
        {
            int b = data.Vertices.Count;

            data.Vertices.Add(p0);
            data.Vertices.Add(p1);
            data.Vertices.Add(p2);
            data.Vertices.Add(p3);

            data.Normals.Add(normal);
            data.Normals.Add(normal);
            data.Normals.Add(normal);
            data.Normals.Add(normal);

            if (side)
            {
                data.UVs.Add(new Vector2(0f, 0f));
                data.UVs.Add(new Vector2(0f, 1f / TerrainMaterials.GrassTilesPerAxis));
                data.UVs.Add(new Vector2(1f / TerrainMaterials.GrassTilesPerAxis, 1f / TerrainMaterials.GrassTilesPerAxis));
                data.UVs.Add(new Vector2(1f / TerrainMaterials.GrassTilesPerAxis, 0f));
            }
            else
            {
                data.UVs.Add(new Vector2(0f, 0f));
                data.UVs.Add(new Vector2(1f, 0f));
                data.UVs.Add(new Vector2(1f, 1f));
                data.UVs.Add(new Vector2(0f, 1f));
            }

            triangles.Add(b);
            triangles.Add(b + 2);
            triangles.Add(b + 1);
            triangles.Add(b);
            triangles.Add(b + 3);
            triangles.Add(b + 2);
        }
    }
}
