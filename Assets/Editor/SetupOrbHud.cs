using UnityEditor;
using UnityEngine;

// Tools > Frostbound > UI > Configurar HUD de orbes: importa los dibujos de las estatuas, los orbes y la barra
// de pociones como sprites y los asigna al UISkin del juego.
public static class SetupOrbHud
{
    private const string Folder = "Assets/Frostbound/UI/HUD/";
    private const string SkinPath = "Assets/Data/UI/UISkin.asset";

    [MenuItem("Tools/Frostbound/UI/Configurar HUD de orbes")]
    public static void Setup()
    {
        string[] names = { "HUD_Statue_Life", "HUD_Statue_Mana", "HUD_OrbBack", "HUD_OrbFill_Life", "HUD_OrbFill_Mana", "HUD_OrbFrame_Life", "HUD_OrbFrame_Mana", "HUD_Bar" };
        foreach (string n in names)
        {
            var imp = AssetImporter.GetAtPath(Folder + n + ".png") as TextureImporter;
            if (imp == null) { Debug.LogWarning("[HUD] Falta " + n); continue; }
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
        UISkin skin = AssetDatabase.LoadAssetAtPath<UISkin>(SkinPath);
        if (skin == null) { Debug.LogError("[HUD] No se encontró " + SkinPath); return; }
        Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + n + ".png");
        skin.hudStatueLife = S("HUD_Statue_Life");
        skin.hudStatueMana = S("HUD_Statue_Mana");
        skin.hudOrbBack = S("HUD_OrbBack");
        skin.hudOrbFillLife = S("HUD_OrbFill_Life");
        skin.hudOrbFillMana = S("HUD_OrbFill_Mana");
        skin.hudOrbFrameLife = S("HUD_OrbFrame_Life");
        skin.hudOrbFrameMana = S("HUD_OrbFrame_Mana");
        skin.hudBar = S("HUD_Bar");
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        Debug.Log("[HUD] HUD de orbes configurado: " + skin.HasOrbHud);
    }
}
