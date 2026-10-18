using UnityEditor;
using UnityEngine;

// Hielo de cuarzo cristalino (solo los cristales de hielo del mundo; ni el menú ni las paredes de hielo).
// - Crea Resources/Materials/Cuarzo.mat (lo usan los cristales que generan las zonas en ejecución).
// - Pasa el hielo del castillo (Materials/Castle/Frost_Corrupt.mat: cristales del castillo y la ciudadela, sello de la
//   puerta, interior, cristales del camino y sello de la ciudadela) al mismo sombreador, algo más gris y denso.
// Se puede ejecutar varias veces.
public static class SetupQuartzIce
{
    private const string QuartzPath = "Assets/Resources/Materials/Cuarzo.mat";
    private const string CastleIcePath = "Assets/Materials/Castle/Frost_Corrupt.mat";

    [MenuItem("Tools/Frostbound/Mundo/Hielo de cuarzo")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        Shader sh = Shader.Find("Frostbound/QuartzCrystal");
        if (sh == null) return "falta el sombreador Frostbound/QuartzCrystal";

        var quartz = AssetDatabase.LoadAssetAtPath<Material>(QuartzPath);
        if (quartz == null)
        {
            quartz = new Material(sh) { name = "Cuarzo" };
            AssetDatabase.CreateAsset(quartz, QuartzPath);
        }
        Apply(quartz, sh, new Color(0.86f, 0.87f, 0.88f), new Color(0.62f, 0.64f, 0.68f), 0.55f, 0.06f);

        var castle = AssetDatabase.LoadAssetAtPath<Material>(CastleIcePath);
        if (castle != null)
        {
            // El del castillo, un punto más gris perla y denso (está junto a piedra oscura y se vería blanco puro).
            Apply(castle, sh, new Color(0.8f, 0.81f, 0.83f), new Color(0.55f, 0.57f, 0.61f), 0.6f, 0.05f);
            castle.DisableKeyword("_EMISSION");
        }
        AssetDatabase.SaveAssets();
        return "hielo de cuarzo: " + QuartzPath + (castle != null ? " + " + CastleIcePath : " (sin el del castillo)");
    }

    private static void Apply(Material m, Shader sh, Color body, Color depth, float veins, float glow)
    {
        m.shader = sh;
        m.SetColor("_BaseColor", body);
        m.SetColor("_DepthColor", depth);
        m.SetColor("_VeinColor", new Color(0.97f, 0.97f, 0.96f));
        m.SetFloat("_Veins", veins);
        m.SetFloat("_VeinScale", 3.2f);
        m.SetFloat("_Smoothness", 0.88f);
        m.SetFloat("_Fresnel", 0.9f);
        m.SetFloat("_Depth", 0.35f);
        m.SetFloat("_Bump", 0.25f);
        m.SetFloat("_Glow", glow);
        m.SetColor("_GlowColor", new Color(0.92f, 0.95f, 1f));
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
    }
}
