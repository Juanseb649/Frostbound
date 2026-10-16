using UnityEditor;
using UnityEngine;

// Crea el material de nieve con relieve (Resources/Frostbound/NieveRelieve): grano redondeado de ~5 cm (mapa base
// y normal, mosaico de 3 m) y ondas de viento suaves (detalle, mosaico de 12 m).
public static class SnowMaterialBuilder
{
    private const string Dir = "Assets/Resources/Frostbound/";

    public static string Build()
    {
        Normal(Dir + "NieveRelieve_Normal.png");
        Normal(Dir + "NieveOndas_Normal.png");
        Color(Dir + "NieveRelieve_Albedo.png", true);
        Color(Dir + "NieveOndas_Albedo.png", false);

        Material src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ground.mat");
        string path = Dir + "NieveRelieve.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(src);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "NieveRelieve_Albedo.png"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "NieveRelieve_Normal.png"));
        m.SetFloat("_BumpScale", 1f);
        m.SetTexture("_DetailAlbedoMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "NieveOndas_Albedo.png"));
        m.SetTexture("_DetailNormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "NieveOndas_Normal.png"));
        m.SetFloat("_DetailAlbedoMapScale", 1f);
        m.SetFloat("_DetailNormalMapScale", 1f);
        m.SetTexture("_DetailMask", Texture2D.whiteTexture);
        m.SetFloat("_Smoothness", 0.22f);
        m.SetFloat("_EnvironmentReflections", 0f);
        m.SetColor("_BaseColor", new UnityEngine.Color(0.86f, 0.89f, 0.94f));
        m.SetTextureScale("_BaseMap", Vector2.one / 3f);
        m.SetTextureScale("_DetailAlbedoMap", Vector2.one / 12f);
        m.EnableKeyword("_NORMALMAP");
        m.EnableKeyword("_DETAIL_MULX2");
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return "material " + path + " · keywords " + string.Join(",", m.shaderKeywords);
    }

    private static void Normal(string p)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(p);
        if (imp == null) return;
        imp.textureType = TextureImporterType.NormalMap;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 8;
        imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
    }

    private static void Color(string p, bool srgb)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(p);
        if (imp == null) return;
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = srgb;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.anisoLevel = 8;
        imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
    }
}
