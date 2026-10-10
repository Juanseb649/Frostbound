using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Completa el traje del mago: túnica que cubre el pecho hasta el cuello y mangas anchas con puño plateado.
// Se generan a partir de la malla del pingüino (mismos huesos y pesos), así se deforman con el esqueleto.
// También quita la barba. Se puede ejecutar varias veces.
public static class SetupMageOutfit
{
    private const string Dir = "Assets/Models/Gear/Generated";
    private const string GearPath = "Assets/Models/Gear/Frostbound_Mage_Gear.fbx";
    private const string PrefabPath = Dir + "/Mage_Robe_Complete.prefab";
    private const string MeshPath = Dir + "/Mage_Tunic.asset";

    [MenuItem("Tools/Frostbound/Clases/Completar traje del mago")]
    public static void Run() => Debug.Log("[Mago] " + Build());

    public static string Build()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        GameObject penguin = db.penguinPrefab;
        SkinnedMeshRenderer body = null, flipL = null, flipR = null;
        foreach (var smr in penguin.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name == "Penguin_Body") body = smr;
            if (smr.name == "Penguin_Flipper_L") flipL = smr;
            if (smr.name == "Penguin_Flipper_R") flipR = smr;
        }
        if (body == null || flipL == null || flipR == null) return "faltan mallas del pingüino";

        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var weights = new List<BoneWeight>();
        var robe = new List<int>();
        var trim = new List<int>();

        // Túnica: el torso desde la cintura (se mete bajo la falda) hasta el cuello (bajo el sombrero).
        int head = System.Array.FindIndex(body.bones, b => b != null && b.name == "Head");
        BoneWeight[] bw = body.sharedMesh.boneWeights;
        Vector3[] bv = body.sharedMesh.vertices;
        var headVerts = new HashSet<Vector3>();
        for (int i = 0; i < bv.Length; i++) if (HeadWeight(bw[i], head) > 0.3f) headVerts.Add(bv[i]);
        float neck = 0.9f;
        foreach (Vector3 hv in headVerts) neck = Mathf.Min(neck, hv.y);
        AddPart(body.sharedMesh, verts, normals, weights, robe, trim, (p, n) =>
        {
            if (p.y < 0.46f || p.y > neck || headVerts.Contains(p)) return -1f;
            return p.y < 0.6f ? 0.03f : 0.016f;
        }, p => p.y > neck - 0.05f);

        // Mangas: cubren el 70 % de cada aleta desde el hombro, cada vez más anchas; el último tramo es el puño.
        foreach (var flip in new[] { flipL, flipR })
        {
            Mesh m = flip.sharedMesh;
            Vector3 shoulder = Vector3.zero;
            float side = m.bounds.center.x >= 0f ? 1f : -1f;
            Vector3[] mv = m.vertices;
            float inner = float.MaxValue;
            foreach (Vector3 v in mv) inner = Mathf.Min(inner, Mathf.Abs(v.x));
            Vector3 top = mv.Where(v => Mathf.Abs(v.x) < inner + 0.03f).Aggregate(Vector3.zero, (a, b) => a + b);
            int count = mv.Count(v => Mathf.Abs(v.x) < inner + 0.03f);
            shoulder = count > 0 ? top / count : new Vector3(side * 0.24f, 0.6f, 0f);
            float length = mv.Max(v => (v - shoulder).magnitude);
            AddPart(m, verts, normals, weights, robe, trim, (p, n) =>
            {
                float k = (p - shoulder).magnitude / length;
                if (k > 0.72f) return -1f;
                return 0.014f + 0.03f * k;
            }, p => (p - shoulder).magnitude / length > 0.6f);
        }

        var mesh = new Mesh { name = "Mage_Tunic" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.boneWeights = weights.ToArray();
        mesh.bindposes = body.sharedMesh.bindposes;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(robe, 0);
        mesh.SetTriangles(trim, 1);
        mesh.RecalculateBounds();
        AssetDatabase.DeleteAsset(MeshPath);
        AssetDatabase.CreateAsset(mesh, MeshPath);

        // Prefab: el equipo del mago (falda, capa, cinturón) más la túnica y las mangas, sin barba ni arma.
        GameObject gear = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GearPath));
        PrefabUtility.UnpackPrefabInstance(gear, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        SkinnedMeshRenderer robeSmr = null;
        foreach (var smr in gear.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name == "Robe") robeSmr = smr;
            if (smr.name == "Beard" || smr.name.StartsWith("Weapon")) Object.DestroyImmediate(smr.gameObject);
        }
        if (robeSmr == null) { Object.DestroyImmediate(gear); return "el equipo del mago no tiene Robe"; }
        var bonesByName = gear.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
        var tunic = new GameObject("Tunic");
        tunic.transform.SetParent(robeSmr.transform.parent, false);
        var tsmr = tunic.AddComponent<SkinnedMeshRenderer>();
        tsmr.sharedMesh = mesh;
        tsmr.bones = body.bones.Select(b => b != null && bonesByName.TryGetValue(b.name, out Transform t) ? t : null).ToArray();
        tsmr.rootBone = robeSmr.rootBone;
        Material silver = robeSmr.sharedMaterials.FirstOrDefault(m => m != null && m.name.Contains("Silver"));
        tsmr.sharedMaterials = new[] { robeSmr.sharedMaterials[0], silver != null ? silver : robeSmr.sharedMaterials[0] };
        tsmr.localBounds = new Bounds(new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
        PrefabUtility.SaveAsPrefabAsset(gear, PrefabPath);
        Object.DestroyImmediate(gear);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        var torso = AssetDatabase.LoadAssetAtPath<OutfitItem>("Assets/Data/Outfits/Pieces/Pieza_Mage_Torso.asset");
        torso.prefab = prefab;
        torso.onlyParts = new[] { "Robe", "Cloak", "Belt", "Tunic" };
        EditorUtility.SetDirty(torso);
        var hat = AssetDatabase.LoadAssetAtPath<OutfitItem>("Assets/Data/Outfits/Pieces/Pieza_Mage_Casco.asset");
        hat.onlyParts = new[] { "Hat" };
        EditorUtility.SetDirty(hat);
        var cls = AssetDatabase.LoadAssetAtPath<CharacterClass>("Assets/Data/Classes/Mage_Mago.asset");
        cls.startingOutfit = new[] { hat, torso };
        EditorUtility.SetDirty(cls);
        AssetDatabase.SaveAssets();
        return "cuello y=" + neck.ToString("F2") + " túnica y mangas: " + verts.Count + " vértices, " + (robe.Count + trim.Count) / 3 + " triángulos (puños " + trim.Count / 3 + ")";
    }

    private static float HeadWeight(BoneWeight w, int head)
    {
        float s = 0f;
        if (w.boneIndex0 == head) s += w.weight0;
        if (w.boneIndex1 == head) s += w.weight1;
        if (w.boneIndex2 == head) s += w.weight2;
        if (w.boneIndex3 == head) s += w.weight3;
        return s;
    }

    // Copia la parte de una malla del pingüino (offset < 0 = fuera) empujando cada vértice por su normal.
    private static void AddPart(Mesh src, List<Vector3> verts, List<Vector3> normals, List<BoneWeight> weights, List<int> main, List<int> trim,
        System.Func<Vector3, Vector3, float> offset, System.Func<Vector3, bool> isTrim)
    {
        Vector3[] v = src.vertices, n = src.normals;
        BoneWeight[] w = src.boneWeights;
        int[] tris = src.triangles;
        var map = new Dictionary<int, int>();
        var off = new float[v.Length];
        for (int i = 0; i < v.Length; i++) off[i] = offset(v[i], n[i]);
        int Map(int i)
        {
            if (map.TryGetValue(i, out int j)) return j;
            verts.Add(v[i] + n[i] * off[i]);
            normals.Add(n[i]);
            weights.Add(w[i]);
            map[i] = verts.Count - 1;
            return verts.Count - 1;
        }
        for (int t = 0; t < tris.Length; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
            if (off[a] < 0f || off[b] < 0f || off[c] < 0f) continue;
            bool cuff = isTrim(v[a]) && isTrim(v[b]) && isTrim(v[c]);
            List<int> target = cuff ? trim : main;
            target.Add(Map(a)); target.Add(Map(b)); target.Add(Map(c));
        }
    }
}
