using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Mejora visual del mundo jugable (no el menú): pasa los materiales de piedra, madera, tejados y empedrado del
// poblado y del castillo al sombreador Frostbound/WorldTriplanarLit con las texturas PBR CC0 de Poly Haven que ya
// están en Assets/Frostbound/Textures/Manor. Se conserva el color de cada material (solo cambian sombreador y
// texturas); hielo, metal, telas, cristales y pingüinos no se tocan. Se puede ejecutar varias veces.
public static class SetupWorldMaterials
{
    private const string Tex = "Assets/Frostbound/Textures/Manor/";

    private struct Spec
    {
        public string path, texture;
        public float tile, detail, snow, texColor, normal, variation;
    }

    private static Spec S(string path, string texture, float tile, float detail, float snow, float texColor = 0f, float normal = 1f, float variation = 0.12f) =>
        new Spec { path = path, texture = texture, tile = tile, detail = detail, snow = snow, texColor = texColor, normal = normal, variation = variation };

    private static readonly Spec[] Specs =
    {
        // Poblado.
        S("Assets/Materials/Village/WallStone.mat", "castle_brick_07", 2.4f, 0.55f, 0.9f),
        S("Assets/Materials/Village/Stone.mat", "castle_wall_varriation", 2f, 0.6f, 0.7f),
        S("Assets/Materials/Village/StoneDark.mat", "castle_wall_varriation", 2f, 0.65f, 0.6f),
        S("Assets/Materials/Village/Wood.mat", "dark_wooden_planks", 1.5f, 0.6f, 0.35f),
        S("Assets/Materials/Village/WoodDark.mat", "dark_wooden_planks", 1.5f, 0.6f, 0.35f),
        S("Assets/Materials/Village/Roof_Slate.mat", "roof_slates_02", 1.6f, 0.6f, 0.35f),
        S("Assets/Materials/Village/Roof_Red.mat", "roof_slates_02", 1.6f, 0.55f, 0.3f),
        S("Assets/Materials/Village/Roof_Green.mat", "roof_slates_02", 1.6f, 0.55f, 0.3f),
        S("Assets/Materials/Village/Paving.mat", "cobblestone_floor_001", 1.4f, 0.55f, 0.15f, 0f, 0.8f, 0.08f),
        S("Assets/Materials/Village/PavingRoad.mat", "cobblestone_floor_001", 1.4f, 0.6f, 0.15f, 0f, 0.8f, 0.08f),
        S("Assets/Materials/Village/Wall_Cream.mat", "castle_wall_varriation", 2f, 0.3f, 0f, 0f, 0.6f, 0.06f),
        S("Assets/Materials/Village/Wall_Rose.mat", "castle_wall_varriation", 2f, 0.3f, 0f, 0f, 0.6f, 0.06f),
        S("Assets/Materials/Village/Wall_Sage.mat", "castle_wall_varriation", 2f, 0.3f, 0f, 0f, 0.6f, 0.06f),
        S("Assets/Materials/Village/Wall_Terracotta.mat", "castle_wall_varriation", 2f, 0.3f, 0f, 0f, 0.6f, 0.06f),
        S("Assets/Materials/Village/Wall_Ice.mat", "castle_wall_varriation", 2f, 0.25f, 0f, 0f, 0.5f, 0.06f),
        // Castillo y su poblado en ruinas.
        S("Assets/Materials/Castle/Gothic_Stone.mat", "castle_brick_07", 2.6f, 0.75f, 0.6f),
        S("Assets/Materials/Castle/Gothic_Trim.mat", "castle_wall_varriation", 2f, 0.65f, 0.6f),
        S("Assets/Materials/Castle/Gothic_Roof.mat", "roof_slates_02", 1.8f, 0.6f, 0.5f),
        S("Assets/Materials/Castle/Gothic_Rock.mat", "castle_wall_varriation", 3f, 0.7f, 0.75f),
    };

    [MenuItem("Tools/Frostbound/Mundo/Materiales del mundo (triplanar)")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        Shader sh = Shader.Find("Frostbound/WorldTriplanarLit");
        if (sh == null) return "falta el sombreador Frostbound/WorldTriplanarLit";
        Texture2D snowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "snow_02/snow_02_albedo_suavidad.png");
        Texture2D snowNor = NormalMap(Tex + "snow_02/snow_02_nor.jpg");
        var means = new Dictionary<string, float>();
        int done = 0;
        var missing = new List<string>();
        foreach (Spec s in Specs)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(s.path);
            if (m == null) { missing.Add(Path.GetFileNameWithoutExtension(s.path)); continue; }
            string albedoPath = Tex + s.texture + "/" + s.texture + "_albedo_suavidad.png";
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            Texture2D normal = NormalMap(Tex + s.texture + "/" + s.texture + "_nor.jpg");
            if (albedo == null) { missing.Add(s.texture); continue; }
            if (!means.TryGetValue(albedoPath, out float mean)) means[albedoPath] = mean = MeanLuminance(albedoPath);

            // Lo que hay que conservar del material de antes.
            Color color = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            float smooth = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.3f;
            bool emissive = m.IsKeywordEnabled("_EMISSION");
            Color emission = emissive && m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;

            m.shader = sh;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", Mathf.Min(smooth, 0.6f));
            m.SetColor("_EmissionColor", emission);
            m.SetTexture("_MainTex", albedo);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_Tile", s.tile);
            m.SetFloat("_Detail", s.detail);
            m.SetFloat("_TexColor", s.texColor);
            m.SetFloat("_TexMean", mean);
            m.SetFloat("_NormalScale", s.normal);
            m.SetFloat("_Variation", s.variation);
            m.SetFloat("_Snow", s.snow);
            m.SetTexture("_SnowTex", snowTex);
            m.SetTexture("_SnowBump", snowNor);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            done++;
        }
        AssetDatabase.SaveAssets();
        return "materiales del mundo: " + done + " convertidos" + (missing.Count > 0 ? " · faltan: " + string.Join(", ", missing) : "");
    }

    // Luminancia media de la textura (para que la textura module el color sin oscurecerlo ni aclararlo en conjunto).
    private static float MeanLuminance(string path)
    {
        var t = new Texture2D(2, 2);
        t.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path)));
        Color[] px = t.GetPixels();
        double sum = 0;
        int step = Mathf.Max(1, px.Length / 4096);
        int n = 0;
        for (int i = 0; i < px.Length; i += step)
        {
            // En lineal: el sombreador lee la textura sRGB ya linealizada.
            sum += Mathf.GammaToLinearSpace(px[i].r) * 0.299 + Mathf.GammaToLinearSpace(px[i].g) * 0.587 + Mathf.GammaToLinearSpace(px[i].b) * 0.114;
            n++;
        }
        Object.DestroyImmediate(t);
        return Mathf.Max(0.05f, (float)(sum / Mathf.Max(1, n)));
    }

    // Asegura que el relieve se importa como normal map (lineal) y con filtrado anisotrópico.
    private static Texture2D NormalMap(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && (imp.textureType != TextureImporterType.NormalMap || imp.anisoLevel < 4))
        {
            imp.textureType = TextureImporterType.NormalMap;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
