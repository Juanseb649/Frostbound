using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Nieve con relieve: material con grano redondeado y ondas de viento (Resources/Frostbound/NieveRelieve) y,
// donde el suelo es plano, dunas de verdad: una malla por trozos con lomas suaves de hasta ~30 cm encima del suelo.
// La colisión sigue siendo el suelo plano: los pies se hunden un poco en la nieve.
public static class SnowSurface
{
    public const float GrainTile = 3f, RippleTile = 12f, DuneHeight = 0.4f;
    // Las normales se calculan con las lomas más altas de lo que son: sombreado de dunas de ~1,5 m con la geometría baja.
    public const float ShadeExaggeration = 3.5f;

    private static Material _relief;
    public static Material Relief
    {
        get
        {
            if (_relief == null) _relief = Resources.Load<Material>("Frostbound/NieveRelieve");
            return _relief;
        }
    }

    // Material de nieve con relieve para una malla cuyas UV miden metersPerUv metros por unidad.
    public static Material For(float metersPerUv, string name = "Nieve con relieve")
    {
        if (Relief == null) return null;
        var m = new Material(Relief) { name = name };
        m.SetTextureScale("_BaseMap", Vector2.one * (metersPerUv / GrainTile));
        m.SetTextureScale("_DetailAlbedoMap", Vector2.one * (metersPerUv / RippleTile));
        return m;
    }

    // Altura de las dunas (0..1) en un punto del mundo: lomas alargadas por el viento, redondeadas arriba.
    public static float Dune(float x, float z, int seed)
    {
        var r = new System.Random(seed);
        float o1 = (float)r.NextDouble() * 500f, o2 = (float)r.NextDouble() * 500f, o3 = (float)r.NextDouble() * 500f;
        return DuneWith(x, z, o1, o2, o3);
    }

    private static float DuneWith(float x, float z, float o1, float o2, float o3)
    {
        float wx = x + (Mathf.PerlinNoise(x * 0.017f + o1, z * 0.017f + o2) - 0.5f) * 10f;
        float wz = z + (Mathf.PerlinNoise(x * 0.017f + o3, z * 0.017f + o1) - 0.5f) * 10f;
        float a = Mathf.PerlinNoise(wx * 0.11f + o2, wz * 0.06f + o3);
        float b = Mathf.PerlinNoise(wx * 0.24f + o3, wz * 0.17f + o1);
        float c = Mathf.PerlinNoise(wx * 0.045f + o1, wz * 0.045f + o2);
        float h = 0.5f * a + 0.22f * b + 0.28f * c;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.78f, h));
    }

    // Dunas sobre un cuadrado de lado 2·half centrado en el origen. mask (0..1) aplana cerca de caminos y edificios;
    // donde vale 0 en los tres vértices no hay malla (se ve el suelo plano de debajo, con el mismo material).
    public static Transform BuildDunes(Transform parent, float half, Func<float, float, float> mask, int seed, Material mat)
    {
        var root = new GameObject("Dunas_Nieve").transform;
        root.SetParent(parent, false);
        if (mat == null) return root;
        var r = new System.Random(seed);
        float o1 = (float)r.NextDouble() * 500f, o2 = (float)r.NextDouble() * 500f, o3 = (float)r.NextDouble() * 500f;
        const float chunk = 48f, step = 1f, lift = 0.02f;
        int n = Mathf.RoundToInt(chunk / step);
        int chunks = Mathf.CeilToInt(half * 2f / chunk);
        float start = -chunks * chunk * 0.5f;

        float Y(float x, float z)
        {
            float m = mask(x, z);
            return m <= 0f ? 0f : lift + DuneHeight * m * DuneWith(x, z, o1, o2, o3);
        }

        for (int cx = 0; cx < chunks; cx++)
            for (int cz = 0; cz < chunks; cz++)
            {
                float x0 = start + cx * chunk, z0 = start + cz * chunk;
                var verts = new Vector3[(n + 1) * (n + 1)];
                var normals = new Vector3[verts.Length];
                var uvs = new Vector2[verts.Length];
                var masks = new float[verts.Length];
                for (int j = 0; j <= n; j++)
                    for (int i = 0; i <= n; i++)
                    {
                        float x = x0 + i * step, z = z0 + j * step;
                        int k = j * (n + 1) + i;
                        masks[k] = mask(x, z);
                        float y = masks[k] <= 0f ? lift : Y(x, z);
                        verts[k] = new Vector3(x, y, z);
                        uvs[k] = new Vector2(x, z);
                        const float e = 0.5f;
                        float dx = Y(x + e, z) - Y(x - e, z), dz = Y(x, z + e) - Y(x, z - e);
                        normals[k] = new Vector3(-dx / (2f * e) * ShadeExaggeration, 1f, -dz / (2f * e) * ShadeExaggeration).normalized;
                    }
                var tris = new List<int>(n * n * 6);
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                        if (masks[a] + masks[b] + masks[c] + masks[d] < 0.02f) continue;
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(b); tris.Add(c); tris.Add(d);
                    }
                if (tris.Count == 0) continue;
                var mesh = new Mesh { name = "Dunas_" + cx + "_" + cz };
                mesh.vertices = verts;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
                go.isStatic = true;
            }
        return root;
    }
}
