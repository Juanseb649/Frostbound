using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Completa los trajes de clase generando piezas sobre la malla del pingüino (mismos huesos y pesos, así se deforman
// con el esqueleto):
//  · Mago: túnica hasta el cuello y mangas anchas con puño plateado; sin barba ni capa (la capa tapaba las mangas).
//  · Shinobi: mangas ceñidas que dejan la punta de las aletas al aire, capucha completa algo más holgada (la cabeza
//    ya no asoma al moverse) y la vaina de la katana en la espalda (la espada de verdad se guarda en ella).
// Se puede ejecutar varias veces.
public static class SetupMageOutfit
{
    private const string Dir = "Assets/Models/Gear/Generated";

    [MenuItem("Tools/Frostbound/Clases/Completar trajes (mago y shinobi)")]
    public static void Run() => Debug.Log("[Trajes] " + Build());

    public static string Build() => BuildMage() + " · " + BuildNinja();

    private class Penguin
    {
        public SkinnedMeshRenderer body, flipL, flipR;
        public int head;
        public HashSet<Vector3> headVerts = new HashSet<Vector3>();
        public float neck = 0.9f;
    }

    private static Penguin LoadPenguin()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var p = new Penguin();
        foreach (var smr in db.penguinPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name == "Penguin_Body") p.body = smr;
            if (smr.name == "Penguin_Flipper_L") p.flipL = smr;
            if (smr.name == "Penguin_Flipper_R") p.flipR = smr;
        }
        p.head = System.Array.FindIndex(p.body.bones, b => b != null && b.name == "Head");
        BoneWeight[] bw = p.body.sharedMesh.boneWeights;
        Vector3[] bv = p.body.sharedMesh.vertices;
        for (int i = 0; i < bv.Length; i++) if (BoneWeightOf(bw[i], p.head) > 0.3f) p.headVerts.Add(bv[i]);
        foreach (Vector3 hv in p.headVerts) p.neck = Mathf.Min(p.neck, hv.y);
        return p;
    }

    // ---------- Mago ----------

    private static string BuildMage()
    {
        Penguin p = LoadPenguin();
        var b = new MeshBuilder();
        b.Add(p.body.sharedMesh, (v, n) => v.y < 0.46f || v.y > p.neck || p.headVerts.Contains(v) ? -1f : v.y < 0.6f ? 0.03f : 0.016f, v => v.y > p.neck - 0.05f);
        foreach (var flip in new[] { p.flipL, p.flipR }) Sleeve(b, flip.sharedMesh, 0.72f, 0.6f, 0.016f, 0.034f);
        Mesh mesh = b.Build("Mage_Tunic", p.body.sharedMesh.bindposes, Dir + "/Mage_Tunic.asset");

        string prefabPath = Dir + "/Mage_Robe_Complete.prefab";
        GameObject gear = OpenGear("Assets/Models/Gear/Frostbound_Mage_Gear.fbx", new[] { "Beard", "Weapon_R", "Cloak" });
        SkinnedMeshRenderer robe = Find(gear, "Robe");
        Material silver = robe.sharedMaterials.FirstOrDefault(m => m != null && m.name.Contains("Silver"));
        AddSkinned(gear, robe, p.body, "Tunic", mesh, new[] { robe.sharedMaterials[0], silver != null ? silver : robe.sharedMaterials[0] });
        GameObject prefab = Save(gear, prefabPath);

        var torso = Piece("Pieza_Mage_Torso");
        torso.prefab = prefab;
        torso.onlyParts = new[] { "Robe", "Belt", "Tunic" };
        var hat = Piece("Pieza_Mage_Casco");
        hat.onlyParts = new[] { "Hat" };
        var cls = AssetDatabase.LoadAssetAtPath<CharacterClass>("Assets/Data/Classes/Mage_Mago.asset");
        cls.startingOutfit = new[] { hat, torso };
        EditorUtility.SetDirty(torso); EditorUtility.SetDirty(hat); EditorUtility.SetDirty(cls);
        AssetDatabase.SaveAssets();
        return "mago: túnica y mangas " + mesh.vertexCount + " vértices";
    }

    // ---------- Shinobi ----------

    private static string BuildNinja()
    {
        Penguin p = LoadPenguin();
        GameObject gear = OpenGear("Assets/Models/Gear/Frostbound_Ninja_Gear.fbx", new[] { "Weapon_R", "Weapon_L", "Wraps_L", "Wraps_R" });
        SkinnedMeshRenderer suit = Find(gear, "Suit"), hood = Find(gear, "Hood"), katana = Find(gear, "Katana_Back");

        // Mangas ceñidas hasta media aleta: la punta queda al aire.
        var sb = new MeshBuilder();
        foreach (var flip in new[] { p.flipL, p.flipR }) Sleeve(sb, flip.sharedMesh, 0.56f, 0.47f, 0.012f, 0.014f);
        Mesh sleeves = sb.Build("Ninja_Sleeves", p.body.sharedMesh.bindposes, Dir + "/Ninja_Sleeves.asset");
        Material charcoal = suit.sharedMaterials.FirstOrDefault(m => m != null && m.name.Contains("Charcoal"));
        AddSkinned(gear, suit, p.body, "Sleeves", sleeves, new[] { suit.sharedMaterials[0], charcoal != null ? charcoal : suit.sharedMaterials[0] });

        // Capucha completa: la misma malla un poco más holgada (antes rozaba la coronilla a 1 cm y asomaba al moverse).
        Mesh full = Inflate(hood.sharedMesh, 0.018f, "Ninja_HoodFull");
        Save(full, Dir + "/Ninja_HoodFull.asset");
        AddSkinned(gear, hood, null, "HoodFull", full, hood.sharedMaterials);

        // Vaina sola (sin empuñadura), reflejada para que el mango asome por el hombro derecho.
        Mesh saya = Scabbard(katana, out Vector3 mouth, out Vector3 tip);
        Save(saya, Dir + "/Ninja_Saya.asset");
        AddSkinned(gear, katana, null, "Saya", saya, new[] { katana.sharedMaterials[0], katana.sharedMaterials.Length > 1 ? katana.sharedMaterials[1] : katana.sharedMaterials[0] });
        GameObject prefab = Save(gear, Dir + "/Ninja_Gear_Complete.prefab");

        var info = AssetDatabase.LoadAssetAtPath<SheathInfo>("Assets/Resources/Sheath_Ninja.asset");
        if (info == null)
        {
            info = ScriptableObject.CreateInstance<SheathInfo>();
            AssetDatabase.CreateAsset(info, "Assets/Resources/Sheath_Ninja.asset");
        }
        info.mouth = mouth;
        info.tip = tip;
        EditorUtility.SetDirty(info);

        var torso = Piece("Pieza_Ninja_Torso");
        torso.prefab = prefab;
        torso.onlyParts = new[] { "Suit", "Sleeves", "Saya" };
        var head = Piece("Pieza_Ninja_Casco");
        head.prefab = prefab;
        head.onlyParts = new[] { "HoodFull" };
        EditorUtility.SetDirty(torso); EditorUtility.SetDirty(head);

        string katanaInfo = NinjaKatana();
        AssetDatabase.SaveAssets();
        return "shinobi: mangas " + sleeves.vertexCount + " vértices, vaina boca " + mouth.ToString("F2") + " punta " + tip.ToString("F2") + ", " + katanaInfo;
    }

    // Arma inicial del shinobi: una katana sencilla (los kunais pasan a ser su habilidad de maná).
    private static string NinjaKatana()
    {
        const string path = "Assets/Data/Items/ninja_katana.asset";
        var kunai = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/ninja_kunai.asset");
        var katana = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Weapons/katana.asset");
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            item = Object.Instantiate(kunai);
            AssetDatabase.CreateAsset(item, path);
        }
        item.name = "ninja_katana";
        item.id = "ninja_katana";
        item.displayName = "Katana de la sombra";
        item.description = "La espada del aprendiz shinobi. Se desenfunda en un parpadeo.";
        item.weaponType = WeaponType.Katana;
        item.weaponModel = katana.weaponModel;
        item.icon = katana.icon;
        item.damage = 6;
        item.attackSpeed = 1.5f;
        EditorUtility.SetDirty(item);

        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        if (!db.items.items.Contains(item)) { db.items.items.Add(item); EditorUtility.SetDirty(db.items); }

        var cls = AssetDatabase.LoadAssetAtPath<CharacterClass>("Assets/Data/Classes/Ninja.asset");
        var list = cls.startingItems.Where(i => i != null && i.id != "ninja_kunai" && i.id != "ninja_offhand" && i != item).ToList();
        list.Insert(0, item);
        cls.startingItems = list.ToArray();
        cls.startingGear = new[] { "Capucha y máscara", "Katana a la espalda", "Traje de sombra", "Abanico de kunais (maná)" };
        EditorUtility.SetDirty(cls);
        return "katana inicial lista";
    }

    // ---------- Piezas ----------

    // Manga: el tramo de la aleta desde el hombro hasta "reach" (0–1 del largo), más ancha cuanto más lejos.
    private static void Sleeve(MeshBuilder b, Mesh m, float reach, float cuffFrom, float offset0, float offsetGrow)
    {
        Vector3[] mv = m.vertices;
        float inner = mv.Min(v => Mathf.Abs(v.x));
        var top = mv.Where(v => Mathf.Abs(v.x) < inner + 0.03f).ToList();
        Vector3 shoulder = top.Aggregate(Vector3.zero, (a, c) => a + c) / Mathf.Max(1, top.Count);
        float length = mv.Max(v => (v - shoulder).magnitude);
        b.Add(m, (v, n) =>
        {
            float k = (v - shoulder).magnitude / length;
            return k > reach ? -1f : offset0 + offsetGrow * k;
        }, v => (v - shoulder).magnitude / length > cuffFrom);
    }

    private static Mesh Inflate(Mesh src, float amount, string name)
    {
        var m = Object.Instantiate(src);
        m.name = name;
        Vector3[] v = m.vertices, n = m.normals;
        // Malla de caras planas: los vértices repetidos se empujan con la normal media para que no se abran grietas.
        var sum = new Dictionary<Vector3Int, Vector3>();
        Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
        for (int i = 0; i < v.Length; i++) { var k = Key(v[i]); sum[k] = (sum.TryGetValue(k, out Vector3 a) ? a : Vector3.zero) + n[i]; }
        for (int i = 0; i < v.Length; i++) v[i] += sum[Key(v[i])].normalized * amount;
        m.vertices = v;
        m.RecalculateBounds();
        return m;
    }

    // La vaina es la parte negra de Katana_Back más allá de la guarda; se refleja en X (mango al hombro derecho).
    private static Mesh Scabbard(SkinnedMeshRenderer katana, out Vector3 mouth, out Vector3 tip)
    {
        Mesh src = katana.sharedMesh;
        Vector3[] v = src.vertices;
        int[] steel = src.subMeshCount > 1 ? src.GetTriangles(1) : new int[0];
        Vector3 guard = steel.Length > 0 ? steel.Select(i => v[i]).Aggregate(Vector3.zero, (a, c) => a + c) / steel.Length : v.OrderByDescending(p => p.y).First();
        int[] black = src.GetTriangles(0);
        Vector3 far = black.Select(i => v[i]).OrderByDescending(p => (p - guard).sqrMagnitude).First();
        Vector3 axis = (far - guard).normalized;
        var keep = new List<int>();
        for (int t = 0; t < black.Length; t += 3)
        {
            bool ok = true;
            for (int k = 0; k < 3; k++) if (Vector3.Dot(v[black[t + k]] - guard, axis) < 0.02f) ok = false;
            if (ok) keep.AddRange(new[] { black[t], black[t + 2], black[t + 1] });
        }
        var m = Object.Instantiate(src);
        m.name = "Ninja_Saya";
        Vector3[] mv = m.vertices, mn = m.normals;
        for (int i = 0; i < mv.Length; i++) { mv[i].x = -mv[i].x; mn[i].x = -mn[i].x; }
        m.vertices = mv;
        m.normals = mn;
        m.subMeshCount = 1;
        m.SetTriangles(keep, 0);
        m.RecalculateBounds();
        mouth = new Vector3(-guard.x, guard.y, guard.z) + new Vector3(-axis.x, axis.y, axis.z) * 0.02f;
        tip = new Vector3(-far.x, far.y, far.z);
        return m;
    }

    private static GameObject OpenGear(string path, string[] remove)
    {
        GameObject gear = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        PrefabUtility.UnpackPrefabInstance(gear, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var smr in gear.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (remove.Contains(smr.name)) Object.DestroyImmediate(smr.gameObject);
        return gear;
    }

    private static SkinnedMeshRenderer Find(GameObject gear, string name) =>
        gear.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == name);

    // Nueva parte con piel: con "bonesFrom" toma el orden de huesos del pingüino; sin él, el de la parte de referencia.
    private static void AddSkinned(GameObject gear, SkinnedMeshRenderer reference, SkinnedMeshRenderer bonesFrom, string name, Mesh mesh, Material[] mats)
    {
        Transform old = gear.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var byName = gear.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
        var go = new GameObject(name);
        go.transform.SetParent(reference.transform.parent, false);
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.bones = bonesFrom != null
            ? bonesFrom.bones.Select(b => b != null && byName.TryGetValue(b.name, out Transform t) ? t : null).ToArray()
            : reference.bones;
        smr.rootBone = reference.rootBone;
        smr.sharedMaterials = mats;
        smr.localBounds = new Bounds(new Vector3(0f, 0.6f, 0f), new Vector3(1.4f, 1.4f, 1.4f));
    }

    private static GameObject Save(GameObject gear, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(gear, path);
        Object.DestroyImmediate(gear);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void Save(Mesh mesh, string path)
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
    }

    private static OutfitItem Piece(string name) => AssetDatabase.LoadAssetAtPath<OutfitItem>("Assets/Data/Outfits/Pieces/" + name + ".asset");

    private static float BoneWeightOf(BoneWeight w, int bone)
    {
        float s = 0f;
        if (w.boneIndex0 == bone) s += w.weight0;
        if (w.boneIndex1 == bone) s += w.weight1;
        if (w.boneIndex2 == bone) s += w.weight2;
        if (w.boneIndex3 == bone) s += w.weight3;
        return s;
    }

    // Junta trozos de mallas del pingüino empujando cada vértice por su normal (offset < 0 = fuera).
    private class MeshBuilder
    {
        private readonly List<Vector3> _v = new List<Vector3>(), _n = new List<Vector3>();
        private readonly List<BoneWeight> _w = new List<BoneWeight>();
        private readonly List<int> _main = new List<int>(), _trim = new List<int>();

        public void Add(Mesh src, System.Func<Vector3, Vector3, float> offset, System.Func<Vector3, bool> isTrim)
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
                _v.Add(v[i] + n[i] * off[i]);
                _n.Add(n[i]);
                _w.Add(w[i]);
                map[i] = _v.Count - 1;
                return _v.Count - 1;
            }
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                if (off[a] < 0f || off[b] < 0f || off[c] < 0f) continue;
                List<int> target = isTrim(v[a]) && isTrim(v[b]) && isTrim(v[c]) ? _trim : _main;
                target.Add(Map(a)); target.Add(Map(b)); target.Add(Map(c));
            }
        }

        public Mesh Build(string name, Matrix4x4[] bindposes, string path)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_v);
            mesh.SetNormals(_n);
            mesh.boneWeights = _w.ToArray();
            mesh.bindposes = bindposes;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(_main, 0);
            mesh.SetTriangles(_trim, 1);
            mesh.RecalculateBounds();
            Save(mesh, path);
            return mesh;
        }
    }
}
