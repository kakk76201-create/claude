using System.Collections.Generic;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Меш головы. Главное архитектурное решение из GDD: лицо — не UV-развёртка сложного меша.
    /// UV передней половины запекается фронтальной проекцией: uv = (−localPos.x, localPos.y) / faceSize + 0.5.
    /// Ось x отражена: тогда лицо на голове выглядит так же, как картинка референса (как фото),
    /// и «левая щека» на картинке — это левая щека Модели.
    /// Задняя половина берёт один пиксель кожи из угла текстуры.
    /// </summary>
    public static class HeadMeshFactory
    {
        static readonly Vector2 BackUv = new Vector2(0.015f, 0.015f);

        public static Mesh CreateVisual(float radius, float faceSize, int lon = 48, int stacks = 32)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int half = stacks / 2;
            AddBlock(verts, uvs, tris, radius, faceSize, lon, stacks, 0, half, true);
            AddBlock(verts, uvs, tris, radius, faceSize, lon, stacks, half, stacks, false);

            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = verts[i].normalized;

            var m = new Mesh { name = "HeadVisual" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetNormals(normals);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Грубая сфера для выпуклого коллайдера (меньше 255 треугольников).</summary>
        public static Mesh CreateCollider(float radius, int lon = 12, int stacks = 8)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            AddBlock(verts, uvs, tris, radius, 1f, lon, stacks, 0, stacks, false);
            var m = new Mesh { name = "HeadCollider" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // Ось полюсов сферы — z: кольцо 0 это «кончик носа» (+z), последнее — затылок (−z).
        static void AddBlock(List<Vector3> verts, List<Vector2> uvs, List<int> tris, float radius, float faceSize,
            int lon, int stacks, int fromStack, int toStack, bool projectUv)
        {
            int start = verts.Count, rows = toStack - fromStack + 1, cols = lon + 1;
            for (int i = fromStack; i <= toStack; i++)
            {
                float theta = Mathf.PI * i / stacks;
                float z = radius * Mathf.Cos(theta), ring = radius * Mathf.Sin(theta);
                for (int j = 0; j <= lon; j++)
                {
                    float phi = 2f * Mathf.PI * j / lon;
                    var p = new Vector3(ring * Mathf.Cos(phi), ring * Mathf.Sin(phi), z);
                    verts.Add(p);
                    uvs.Add(projectUv ? new Vector2(-p.x / faceSize + 0.5f, p.y / faceSize + 0.5f) : BackUv);
                }
            }
            for (int r = 0; r < rows - 1; r++)
            {
                for (int j = 0; j < lon; j++)
                {
                    int a = start + r * cols + j, b = a + 1, c = a + cols, d = c + 1;
                    AddTriangle(verts, tris, a, c, b);
                    AddTriangle(verts, tris, b, c, d);
                }
            }
        }

        // Добавляет треугольник лицом наружу (в Unity лицевая сторона — по часовой стрелке).
        static void AddTriangle(List<Vector3> verts, List<int> tris, int a, int b, int c)
        {
            Vector3 pa = verts[a], pb = verts[b], pc = verts[c];
            Vector3 n = Vector3.Cross(pb - pa, pc - pa);
            if (n.sqrMagnitude < 1e-12f) return; // вырожденный треугольник у полюса
            Vector3 centroid = (pa + pb + pc) / 3f;
            if (Vector3.Dot(n, centroid) < 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
            else { tris.Add(a); tris.Add(b); tris.Add(c); }
        }
    }
}
