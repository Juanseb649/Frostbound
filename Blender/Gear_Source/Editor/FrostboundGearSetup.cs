using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FrostboundGearSetup
{
    const string Root = "Assets/Frostbound/Gear";
    const string ModelsDir = Root + "/Models";
    const string MaterialsDir = Root + "/Materials";
    const string PrefabsDir = Root + "/Prefabs";
    const string ShaderName = "Frostbound/Toon";

    [MenuItem("Tools/Frostbound/Equipamiento/1. Preparar prefabs de equipo")]
    public static void BuildGearPrefabs()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            EditorUtility.DisplayDialog("Frostbound", $"No se encontró el shader '{ShaderName}'.", "OK");
            return;
        }

        EnsureFolder(MaterialsDir);
        EnsureFolder(PrefabsDir);
        var penguinScale = FindPenguinImporter();

        foreach (var objPath in Directory.GetFiles(ModelsDir, "*.obj", SearchOption.AllDirectories))
        {
            var assetPath = objPath.Replace('\\', '/');
            var importer = (ModelImporter)AssetImporter.GetAtPath(assetPath);
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalSmoothingAngle = 50f;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            if (penguinScale != null)
            {
                importer.globalScale = penguinScale.globalScale;
                importer.useFileScale = penguinScale.useFileScale;
            }
            importer.SaveAndReimport();

            var palette = ReadMtl(Path.ChangeExtension(assetPath, ".mtl"));
            var textures = ReadMtlTextures(Path.ChangeExtension(assetPath, ".mtl"));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var name = mats[i] != null ? mats[i].name : "Default";
                    mats[i] = GetToonMaterial(shader, name, palette);
                    if (textures.TryGetValue(name, out var texPath))
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                        if (tex != null) { mats[i].SetTexture("_BaseMap", tex); EditorUtility.SetDirty(mats[i]); }
                    }
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            var prefabPath = $"{PrefabsDir}/{Path.GetFileNameWithoutExtension(assetPath)}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Frostbound", "Prefabs de equipo creados en " + PrefabsDir, "OK");
    }

    [MenuItem("Tools/Frostbound/Equipamiento/2. Añadir equipador al objeto seleccionado")]
    public static void AddEquipper()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Frostbound", "Selecciona el jugador (el objeto raíz del pingüino).", "OK");
            return;
        }

        var eq = go.GetComponent<HeroGearEquipper>();
        if (eq == null) eq = Undo.AddComponent<HeroGearEquipper>(go);
        Undo.RecordObject(eq, "Configurar equipador");
        eq.penguinModel = FindPenguinMesh(go.transform) ?? go.transform;
        eq.gear.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.name.Contains("_NPC_")) continue;
            var cls = prefab.name.Replace("Frostbound_", "").Replace("_Gear", "");
            eq.gear.Add(new HeroGearEquipper.ClassGear { className = cls, gearPrefab = prefab });
        }
        EditorUtility.SetDirty(eq);
    }

    [MenuItem("Tools/Frostbound/Equipamiento/3. Aplicar toon al pingüino seleccionado")]
    public static void ApplyToonToPenguin()
    {
        var go = Selection.activeGameObject;
        var shader = Shader.Find(ShaderName);
        if (go == null || shader == null) return;
        EnsureFolder(MaterialsDir);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (IsGear(r.transform)) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (src == null || src.shader == shader) continue;
                var path = $"{MaterialsDir}/Penguin_{src.name}_Toon.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader);
                    AssetDatabase.CreateAsset(m, path);
                }
                var tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
                var col = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", col);
                m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
                EditorUtility.SetDirty(m);
                mats[i] = m;
            }
            Undo.RecordObject(r, "Toon pingüino");
            r.sharedMaterials = mats;
        }
        AssetDatabase.SaveAssets();
    }

    static bool IsGear(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            if (p.name.StartsWith("Frostbound_")) return true;
        return false;
    }

    static Material GetToonMaterial(Shader shader, string name, Dictionary<string, (Color kd, Color ke)> palette)
    {
        var path = $"{MaterialsDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        if (palette.TryGetValue(name, out var c))
        {
            m.SetColor("_BaseColor", c.kd);
            m.SetColor("_EmissionColor", c.ke * 0.6f);
        }
        m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Dictionary<string, (Color, Color)> ReadMtl(string path)
    {
        var result = new Dictionary<string, (Color, Color)>();
        if (!File.Exists(path)) return result;
        string current = null;
        Color kd = Color.white, ke = Color.black;
        void Flush() { if (current != null) result[current] = (kd, ke); }
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            var parts = line.Split(' ');
            if (parts.Length == 0) continue;
            if (parts[0] == "newmtl") { Flush(); current = parts[1]; kd = Color.white; ke = Color.black; }
            else if (parts[0] == "Kd" && parts.Length >= 4) kd = ParseColor(parts);
            else if (parts[0] == "Ke" && parts.Length >= 4) ke = ParseColor(parts);
        }
        Flush();
        return result;
    }

    static Dictionary<string, string> ReadMtlTextures(string path)
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(path)) return result;
        string current = null;
        var dir = Path.GetDirectoryName(path).Replace('\\', '/');
        foreach (var raw in File.ReadAllLines(path))
        {
            var parts = raw.Trim().Split(' ');
            if (parts[0] == "newmtl") current = parts[1];
            else if (parts[0] == "map_Kd" && current != null && parts.Length > 1) result[current] = dir + "/" + parts[parts.Length - 1];
        }
        return result;
    }

    static Color ParseColor(string[] p)
    {
        float f(int i) => float.Parse(p[i], CultureInfo.InvariantCulture);
        return new Color(f(1), f(2), f(3), 1f);
    }

    static ModelImporter FindPenguinImporter()
    {
        foreach (var guid in AssetDatabase.FindAssets("Penguin t:Model"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(Root)) continue;
            if (AssetImporter.GetAtPath(path) is ModelImporter mi) return mi;
        }
        return null;
    }

    static Transform FindPenguinMesh(Transform root)
    {
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh != null && mf.sharedMesh.name.ToLower().Contains("geometry_0"))
                return mf.transform.parent != null && mf.transform.parent != root ? mf.transform.parent : mf.transform;
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
