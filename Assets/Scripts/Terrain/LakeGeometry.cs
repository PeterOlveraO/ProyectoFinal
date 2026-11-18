using System.Collections.Generic;
using UnityEngine;

namespace Game.Terrain
{
    /// <summary>
    /// Una fila del disco = un quad de agua y una caja sólida. Sin MeshColliders
    /// convexos, triggers ni objetos por celda; 23 cajas para un lago de radio 11.
    /// La huella es exactamente LakeInfo.ContainsCell, incluidas las esquinas.
    /// </summary>
    public static class LakeGeometry
    {
        public static Mesh BuildWaterMesh(LakeInfo lake, float blockSize, float waterY)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var normals = new List<Vector3>();
            for (int z = lake.Center.y - lake.Radius; z <= lake.Center.y + lake.Radius; z++)
            {
                lake.GetRowBounds(z, out int minX, out int maxX);
                float x0 = minX * blockSize;
                float x1 = (maxX + 1) * blockSize;
                float z0 = z * blockSize;
                float z1 = (z + 1) * blockSize;
                int start = vertices.Count;
                vertices.Add(new Vector3(x0, waterY, z0));
                vertices.Add(new Vector3(x1, waterY, z0));
                vertices.Add(new Vector3(x1, waterY, z1));
                vertices.Add(new Vector3(x0, waterY, z1));
                for (int n = 0; n < 4; n++) normals.Add(Vector3.up);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start);
                triangles.Add(start + 3);
                triangles.Add(start + 2);
            }
            var mesh = new Mesh { name = "Lake_Water" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Obstáculos estáticos desde el fondo hasta encima de la altura máxima del
        /// mundo. CharacterController se detiene en la orilla antes de poder caer.
        /// El root comparte el espacio local del terreno (traslación, escala 1).
        /// </summary>
        public static void AddBlockers(GameObject root, LakeInfo lake, float blockSize,
                                       float bottomY, float topY)
        {
            for (int z = lake.Center.y - lake.Radius; z <= lake.Center.y + lake.Radius; z++)
            {
                lake.GetRowBounds(z, out int minX, out int maxX);
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3((minX + maxX + 1) * 0.5f * blockSize,
                                         (bottomY + topY) * 0.5f, (z + 0.5f) * blockSize);
                box.size = new Vector3((maxX - minX + 1) * blockSize,
                                       Mathf.Max(0.1f, topY - bottomY), blockSize);
            }
        }
    }
}
