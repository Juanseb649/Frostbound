using System.Collections.Generic;
using UnityEngine;

// Mallas de la montaña generadas por código: el terreno del valle, rocas facetadas, cumbres y mojones.
public static class MountainMeshes
{
    public const float Cell = 1.5f;

    // Terreno con tres submallas: sendero (pisado), nieve y roca (pendientes fuertes).
    public static Mesh Terrain(MountainLayout.Result r)
    {
        float x0 = -MountainLayout.HalfWidth, x1 = MountainLayout.HalfWidth;
        float z0 = -MountainLayout.Margin, z1 = MountainLayout.Length + MountainLayout.Margin;
        int nx = Mathf.CeilToInt((x1 - x0) / Cell) + 1, nz = Mathf.CeilToInt((z1 - z0) / Cell) + 1;
        var verts = new Vector3[nx * nz];
        var uv = new Vector2[nx * nz];
        var inside = new bool[nx * nz];
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = x0 + i * Cell, z = z0 + j * Cell;
                int k = j * nx + i;
                verts[k] = new Vector3(x, r.Height(x, z), z);
                uv[k] = new Vector2(x * 0.1f, z * 0.1f);
                inside[k] = r.Edge(x, z) < 0.2f;
            }
        var path = new List<int>();
        var snow = new List<int>();
        var rock = new List<int>();
        for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                Add(verts, inside, a, c, b, path, snow, rock);
                Add(verts, inside, b, c, d, path, snow, rock);
            }
        var mesh = new Mesh { name = "Terreno_Faldas", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.subMeshCount = 3;
        mesh.SetTriangles(path, 0);
        mesh.SetTriangles(snow, 1);
        mesh.SetTriangles(rock, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void Add(Vector3[] v, bool[] inside, int a, int b, int c, List<int> path, List<int> snow, List<int> rock)
    {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]).normalized;
        List<int> target = inside[a] && inside[b] && inside[c] ? path : n.y < 0.74f ? rock : snow;
        target.Add(a); target.Add(b); target.Add(c);
    }

    // Roca facetada: icosaedro subdividido con los vértices desplazados (cada semilla da una forma distinta).
    public static Mesh Rock(int seed, float squash = 0.7f)
    {
        var rng = new DeterministicRng(seed);
        Mesh ico = Icosphere(1);
        Vector3[] src = ico.vertices;
        int[] tris = ico.triangles;
        var shared = new Dictionary<Vector3, Vector3>();
        var verts = new List<Vector3>();
        var idx = new List<int>();
        foreach (int t in tris)
        {
            Vector3 p = src[t];
            if (!shared.TryGetValue(p, out Vector3 q))
            {
                q = p * rng.Range(0.78f, 1.12f);
                q.y *= squash;
                shared[p] = q;
            }
            verts.Add(q);
            idx.Add(verts.Count - 1);
        }
        Orient(verts, idx, false);
        var mesh = new Mesh { name = "Roca" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Cumbre: cono irregular facetado con la base ancha.
    public static Mesh Peak(int seed)
    {
        var rng = new DeterministicRng(seed);
        int sides = 9, rings = 4;
        var ringPts = new List<Vector3[]>();
        for (int k = 0; k <= rings; k++)
        {
            float t = k / (float)rings;
            float radius = Mathf.Lerp(0.5f, 0.06f, Mathf.Pow(t, 0.8f));
            var pts = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (i + rng.Range(-0.25f, 0.25f)) / sides * Mathf.PI * 2f;
                float rr = radius * rng.Range(0.8f, 1.15f);
                pts[i] = new Vector3(Mathf.Sin(a) * rr, t * rng.Range(0.92f, 1.05f), Mathf.Cos(a) * rr);
            }
            ringPts.Add(pts);
        }
        Vector3 tip = new Vector3(rng.Range(-0.04f, 0.04f), 1.08f, rng.Range(-0.04f, 0.04f));
        var verts = new List<Vector3>();
        var idx = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c) { verts.Add(a); verts.Add(b); verts.Add(c); idx.Add(verts.Count - 3); idx.Add(verts.Count - 2); idx.Add(verts.Count - 1); }
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = ringPts[k][i], b = ringPts[k][(i + 1) % sides], c = ringPts[k + 1][i], d = ringPts[k + 1][(i + 1) % sides];
                Tri(a, c, b);
                Tri(b, c, d);
            }
        for (int i = 0; i < sides; i++) Tri(ringPts[rings][i], tip, ringPts[rings][(i + 1) % sides]);
        Orient(verts, idx, true);
        var mesh = new Mesh { name = "Cumbre" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(idx, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Gira los triángulos que miran hacia dentro (las caras de Unity se ven en sentido horario).
    private static void Orient(List<Vector3> v, List<int> idx, bool axial)
    {
        for (int i = 0; i < idx.Count; i += 3)
        {
            Vector3 a = v[idx[i]], b = v[idx[i + 1]], c = v[idx[i + 2]];
            Vector3 centroid = (a + b + c) / 3f;
            Vector3 outward = axial ? new Vector3(centroid.x, 0f, centroid.z) + Vector3.up * 0.15f : centroid;
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            {
                int t = idx[i + 1];
                idx[i + 1] = idx[i + 2];
                idx[i + 2] = t;
            }
        }
    }

    private static Mesh _ico;

    public static Mesh Icosphere(int subdivisions)
    {
        if (_ico != null) return _ico;
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized * 0.5f;
        var f = new List<int>
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };
        for (int s = 0; s < subdivisions; s++)
        {
            var cache = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int m)) return m;
                v.Add(((v[a] + v[b]) * 0.5f).normalized * 0.5f);
                cache[key] = v.Count - 1;
                return v.Count - 1;
            }
            var nf = new List<int>();
            for (int i = 0; i < f.Count; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            f = nf;
        }
        _ico = new Mesh { name = "Icosfera" };
        _ico.SetVertices(v);
        _ico.SetTriangles(f, 0);
        _ico.RecalculateNormals();
        return _ico;
    }
}
