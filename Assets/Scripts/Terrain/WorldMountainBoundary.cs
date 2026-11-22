using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Terrain
{
    /// <summary>Resident geometry and four solid barriers, independent of streamed chunks.</summary>
    public sealed class WorldMountainBoundary : MonoBehaviour
    {
        Mesh _mesh;
        Material _material;

        public static void Create(WorldTerrain terrain)
        {
            var root = new GameObject("PermanentMountainBoundary");
            root.transform.SetParent(terrain.transform, false);
            root.AddComponent<WorldMountainBoundary>().Build(terrain);
        }

        void Build(WorldTerrain terrain)
        {
            var map = terrain.Map;
            var settings = terrain.Settings;
            var layout = terrain.Traversal.Layout;
            float band = layout.MountainWidth, width = layout.Width, depth = layout.Depth;
            // These colliders exist even when every neighbouring terrain chunk is unloaded.
            // The vertical barrier is intentionally much higher than jump and step reach.
            AddBarrier(new Vector3(band * .5f, 20f, depth * .5f), new Vector3(band, 60f, depth));
            AddBarrier(new Vector3(width - band * .5f, 20f, depth * .5f), new Vector3(band, 60f, depth));
            AddBarrier(new Vector3(width * .5f, 20f, band * .5f), new Vector3(width - 2f * band, 60f, band));
            AddBarrier(new Vector3(width * .5f, 20f, depth - band * .5f), new Vector3(width - 2f * band, 60f, band));

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            // Four-unit facets reduce the resident border mesh to a few thousand triangles.
            int stride = Mathf.Max(1, Mathf.RoundToInt(4f / settings.blockSize));
            int bandCells = Mathf.Max(1, Mathf.RoundToInt(band / settings.blockSize));
            BuildStrip(0, 0, bandCells, map.Depth);
            BuildStrip(map.Width - bandCells, 0, map.Width, map.Depth);
            BuildStrip(bandCells, 0, map.Width - bandCells, bandCells);
            BuildStrip(bandCells, map.Depth - bandCells, map.Width - bandCells, map.Depth);

            _mesh = new Mesh { name = "ResidentMountainPeaks", indexFormat = IndexFormat.UInt32 };
            _mesh.SetVertices(vertices);
            _mesh.SetTriangles(triangles, 0);
            _mesh.SetColors(colors);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            _material = new Material(settings.terrainMaterial)
            {
                name = "MountainStone", color = new Color(.31f, .35f, .32f)
            };
            _material.mainTexture = null;
            if (_material.HasProperty("_Smoothness")) _material.SetFloat("_Smoothness", 0f);
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = _material;

            void BuildStrip(int minX, int minZ, int maxX, int maxZ)
            {
                for (int z = minZ; z < maxZ; z += stride)
                for (int x = minX; x < maxX; x += stride)
                {
                    int x1 = Mathf.Min(x + stride, maxX), z1 = Mathf.Min(z + stride, maxZ);
                    float y00 = Height(x, z), y10 = Height(x1, z);
                    float y11 = Height(x1, z1), y01 = Height(x, z1);
                    Vector3 a = new Vector3(x * settings.blockSize, y00, z * settings.blockSize);
                    Vector3 b = new Vector3(x1 * settings.blockSize, y10, z * settings.blockSize);
                    Vector3 c = new Vector3(x1 * settings.blockSize, y11, z1 * settings.blockSize);
                    Vector3 d = new Vector3(x * settings.blockSize, y01, z1 * settings.blockSize);
                    Quad(a, b, c, d);
                    if (x == minX) Wall(d, a);
                    if (x1 == maxX) Wall(b, c);
                    if (z == minZ) Wall(a, b);
                    if (z1 == maxZ) Wall(c, d);
                }
            }
            float Height(int x, int z)
            {
                // Sample a stable world-space profile at each vertex; adjacent strips agree.
                float noise = Mathf.PerlinNoise(x * .095f + settings.seed * .003f,
                    z * .095f + settings.seed * .007f);
                return Mathf.Lerp(8f, 14f, Mathf.Clamp01((noise - .25f) * 2f));
            }
            void Wall(Vector3 a, Vector3 b)
            {
                Quad(new Vector3(a.x, -settings.stepHeight, a.z),
                    new Vector3(b.x, -settings.stepHeight, b.z), b, a);
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int i = 0; i < 4; i++) colors.Add(Color.white);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }
        }

        void AddBarrier(Vector3 centre, Vector3 size)
        {
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.center = centre;
            collider.size = size;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
