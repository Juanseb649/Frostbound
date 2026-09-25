using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Al importar modelos de Assets/Models:
//  • Tangente = normal suavizada (el contorno del shader toon la usa y no se rompe en bultos o pliegues).
//  • UV2 del cuerpo = posición de reposo normalizada (lateral, altura, frente, normal frontal) en las
//    mismas unidades que el script de Blender, para dibujar ojos y barriga como formas exactas.
public class PenguinMeshPostprocessor : AssetPostprocessor
{
    private const float BodyMinHeight = 0.08f;
    private const float BodyHeight = 0.92f;

    public override uint GetVersion() => 3;

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/Models/")) return;
        var importer = (ModelImporter)assetImporter;
        importer.importTangents = ModelImporterTangents.None;
    }

    void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.StartsWith("Assets/Models/")) return;

        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Process(smr.sharedMesh, smr.name.Contains("Body"));
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            Process(mf.sharedMesh, mf.name.Contains("Body"));
    }

    private static void Process(Mesh mesh, bool isBody)
    {
        if (mesh == null) return;
        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        if (verts.Length == 0 || normals.Length != verts.Length) return;

        Vector3[] smooth = SmoothNormals(mesh, verts, normals, 8);
        var tangents = new Vector4[verts.Length];
        for (int i = 0; i < verts.Length; i++) tangents[i] = new Vector4(smooth[i].x, smooth[i].y, smooth[i].z, 1f);
        mesh.tangents = tangents;

        var rest = new List<Vector4>(verts.Length);
        if (isBody && TryBodyFrame(mesh, verts, out Frame f))
        {
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i] - f.center;
                rest.Add(new Vector4(
                    Vector3.Dot(v, f.lateral) / f.scale,
                    Vector3.Dot(verts[i] - f.bottom, f.up) / f.scale + BodyMinHeight,
                    Vector3.Dot(v, f.forward) / f.scale,
                    Vector3.Dot(smooth[i], f.forward)));
            }
        }
        else
        {
            for (int i = 0; i < verts.Length; i++) rest.Add(new Vector4(0f, -10f, 0f, 0f));
        }
        mesh.SetUVs(2, rest);
    }

    private struct Frame
    {
        public Vector3 center, bottom, up, forward, lateral;
        public float scale;
    }

    // Deduce ejes y escala del cuerpo: el eje más largo es "arriba" y el pico (canal G del color) marca el frente.
    private static bool TryBodyFrame(Mesh mesh, Vector3[] verts, out Frame f)
    {
        f = default;
        Bounds b = mesh.bounds;
        Vector3 size = b.size;
        int upAxis = size.y >= size.x && size.y >= size.z ? 1 : (size.x >= size.z ? 0 : 2);

        Color[] colors = mesh.colors;
        Vector3 beak = Vector3.zero;
        int count = 0;
        if (colors != null && colors.Length == verts.Length)
            for (int i = 0; i < verts.Length; i++)
                if (colors[i].g > 0.5f) { beak += verts[i]; count++; }
        if (count == 0) return false;
        beak /= count;

        Vector3 up = Vector3.zero;
        up[upAxis] = 1f;
        if (Vector3.Dot(beak - b.center, up) < 0f) up = -up;

        Vector3 toBeak = beak - b.center;
        int h1 = (upAxis + 1) % 3, h2 = (upAxis + 2) % 3;
        int fa = Mathf.Abs(toBeak[h1]) >= Mathf.Abs(toBeak[h2]) ? h1 : h2;
        Vector3 forward = Vector3.zero;
        forward[fa] = Mathf.Sign(toBeak[fa]);

        float height = Mathf.Abs(Vector3.Dot(size, up));
        f = new Frame
        {
            center = b.center,
            bottom = b.center - up * height * 0.5f,
            up = up,
            forward = forward,
            lateral = Vector3.Cross(up, forward),
            scale = height / BodyHeight
        };
        return true;
    }

    private static Vector3[] SmoothNormals(Mesh mesh, Vector3[] verts, Vector3[] normals, int iterations)
    {
        int n = verts.Length;
        var weld = new int[n];
        var map = new Dictionary<Vector3Int, int>();
        var groups = new List<int>();
        for (int i = 0; i < n; i++)
        {
            Vector3Int key = Vector3Int.RoundToInt(verts[i] * 10000f);
            if (!map.TryGetValue(key, out int g))
            {
                g = groups.Count;
                map[key] = g;
                groups.Add(i);
            }
            weld[i] = g;
        }

        int gCount = groups.Count;
        var acc = new Vector3[gCount];
        for (int i = 0; i < n; i++) acc[weld[i]] += normals[i];

        var neighbors = new HashSet<int>[gCount];
        for (int g = 0; g < gCount; g++) neighbors[g] = new HashSet<int>();
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            int[] tris = mesh.GetTriangles(s);
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = weld[tris[t]], b = weld[tris[t + 1]], c = weld[tris[t + 2]];
                neighbors[a].Add(b); neighbors[a].Add(c);
                neighbors[b].Add(a); neighbors[b].Add(c);
                neighbors[c].Add(a); neighbors[c].Add(b);
            }
        }

        for (int g = 0; g < gCount; g++) acc[g] = acc[g].normalized;
        var next = new Vector3[gCount];
        for (int it = 0; it < iterations; it++)
        {
            for (int g = 0; g < gCount; g++)
            {
                Vector3 sum = acc[g];
                foreach (int k in neighbors[g]) sum += acc[k];
                next[g] = sum.normalized;
            }
            var tmp = acc; acc = next; next = tmp;
        }

        var result = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 v = acc[weld[i]];
            result[i] = v.sqrMagnitude > 0.5f ? v : normals[i];
        }
        return result;
    }
}
