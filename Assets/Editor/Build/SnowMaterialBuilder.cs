using UnityEditor;
using UnityEngine;

// Crea el material de nieve con detalle (Resources/Frostbound/NieveDetallada): variación grande + nieve fina con relieve.
public static class SnowMaterialBuilder
{
    private const string Dir = "Assets/Resources/Frostbound/";

    public static string Build()
    {
        Normal(Dir + "NieveDetalle_Normal.png");
        Normal(Dir + "NieveMacro_Normal.png");
        Color(Dir + "NieveDetalle_Albedo.png", false);
        Color(Dir + "NieveMacro_Albedo.png", true);

        Material src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ground.mat");
        string path = Dir + "NieveDetallada.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(src);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "NieveMacro_Albedo.png"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "NieveMacro_Normal.png"));
        m.SetFloat("_BumpScale", 0.6f);
        m.SetTexture("_DetailAlbedoMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "NieveDetalle_Albedo.png"));
        m.SetTexture("_DetailNormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "NieveDetalle_Normal.png"));
        m.SetFloat("_DetailAlbedoMapScale", 1f);
        m.SetFloat("_DetailNormalMapScale", 0.9f);
        m.SetTexture("_DetailMask", Texture2D.whiteTexture);
        m.SetFloat("_Smoothness", 0.32f);
        m.SetColor("_BaseColor", UnityEngine.Color.white);
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
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
    }

    private static void Color(string p, bool srgb)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(p);
        if (imp == null) return;
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = srgb;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
    }
}
