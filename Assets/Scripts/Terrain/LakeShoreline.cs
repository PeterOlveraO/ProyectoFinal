using System.Collections.Generic;
using UnityEngine;

namespace Game.Terrain
{
    /// <summary>Two quiet color bands inside the exact lake footprint. Visuals only.</summary>
    public sealed class LakeShoreline : MonoBehaviour
    {
        Mesh _mesh;
        Material[] _materials;

        public static void Create(WorldTerrain terrain)
        {
            if (terrain.Map.Lakes.Count == 0) return;
            var go = new GameObject("LakeShoreline");
            go.transform.SetParent(terrain.transform, false);
            go.AddComponent<LakeShoreline>().Build(terrain);
        }

        void Build(WorldTerrain terrain)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>() };
            float block = terrain.BlockSize;
            foreach (var lake in terrain.Map.Lakes)
            {
                float y = (lake.RimLevel - .25f) * terrain.StepHeight + .012f;
                for (int z = lake.Center.y - lake.Radius; z <= lake.Center.y + lake.Radius; z++)
                {
                    lake.GetRowBounds(z, out int minX, out int maxX);
                    for (int x = minX; x <= maxX; x++)
                    {
                        float x0 = x * block, z0 = z * block;
                        for (int band = 1; band >= 0; band--)
                        {
                            float w = block * (band == 0 ? .08f : .22f);
                            float height = y + (band == 0 ? .004f : 0);
                            if (!lake.ContainsCell(x - 1, z))
                                Quad(vertices, normals, triangles[band], x0, z0, x0 + w, z0 + block, height);
                            if (!lake.ContainsCell(x + 1, z))
                                Quad(vertices, normals, triangles[band], x0 + block - w, z0, x0 + block, z0 + block, height);
                            if (!lake.ContainsCell(x, z - 1))
                                Quad(vertices, normals, triangles[band], x0, z0, x0 + block, z0 + w, height);
                            if (!lake.ContainsCell(x, z + 1))
                                Quad(vertices, normals, triangles[band], x0, z0 + block - w, x0 + block, z0 + block, height);
                        }
                    }
                }
            }
            _mesh = new Mesh { name = "LakeShorelineBands", subMeshCount = 2 };
            _mesh.SetVertices(vertices);
            _mesh.SetNormals(normals);
            for (int i = 0; i < 2; i++) _mesh.SetTriangles(triangles[i], i);
            _mesh.RecalculateBounds();
            _materials = new[]
            {
                new Material(terrain.Settings.terrainMaterial) { name = "ShoreFoam", color = new Color32(172, 212, 181, 255) },
                new Material(terrain.Settings.terrainMaterial) { name = "ShoreShallows", color = new Color32(103, 178, 181, 255) }
            };
            foreach (var material in _materials)
            {
                material.mainTexture = null;
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0);
            }
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = _materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void Quad(List<Vector3> v, List<Vector3> n, List<int> t,
            float x0, float z0, float x1, float z1, float y)
        {
            int start = v.Count;
            v.Add(new Vector3(x0, y, z0)); v.Add(new Vector3(x1, y, z0));
            v.Add(new Vector3(x1, y, z1)); v.Add(new Vector3(x0, y, z1));
            for (int i = 0; i < 4; i++) n.Add(Vector3.up);
            t.Add(start); t.Add(start + 2); t.Add(start + 1);
            t.Add(start); t.Add(start + 3); t.Add(start + 2);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_materials != null)
                foreach (var material in _materials)
                    if (material != null) Destroy(material);
        }
    }
}
