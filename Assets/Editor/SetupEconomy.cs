using System.IO;
using UnityEditor;
using UnityEngine;

// Objetos de la economía: el pergamino de regreso al poblado (se compra al mago y al mercader).
public static class SetupEconomy
{
    private const string IconDir = "Assets/Frostbound/UI/Icons";
    private const string ScrollPath = "Assets/Data/Items/town_scroll.asset";
    private const string DatabasePath = "Assets/Data/Items/ItemDatabase.asset";

    [MenuItem("Tools/Frostbound/Objetos/Crear pergamino de regreso")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        string iconPath = IconDir + "/Icon_scroll.png";
        Texture2D tex = FrostboundIconPainter.Render(FrostboundIconPainter.Catalog()["scroll"]);
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, iconPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(iconPath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(iconPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.spritePixelsPerUnit = 128f;
        imp.SaveAndReimport();
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ScrollPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, ScrollPath);
        }
        item.id = "town_scroll";
        item.displayName = "Pergamino de regreso";
        item.description = "Un viento antiguo te devuelve a la plaza del poblado. Se lee en un instante.";
        item.icon = icon;
        item.category = ItemCategory.Consumable;
        item.rarity = ItemRarity.Common;
        item.maxStack = 20;
        item.heal = 0f;
        item.restoreMana = 0f;
        item.townPortal = true;
        EditorUtility.SetDirty(item);

        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
        if (db != null && !db.items.Contains(item))
        {
            db.items.Add(item);
            EditorUtility.SetDirty(db);
        }
        AssetDatabase.SaveAssets();
        return "pergamino=" + (item != null) + " icono=" + (icon != null) + " en_db=" + (db != null && db.items.Contains(item));
    }
}
