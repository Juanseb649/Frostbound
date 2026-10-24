using UnityEditor;
using UnityEngine;

// Escudos: después de generar los modelos con Blender (frostbound_weapons.py -- ... --solo-escudos),
// enlaza cada escudo con su modelo W_<id>.fbx y le renderiza el icono. Sin modelo, el escudo usa el del vikingo.
public static class SetupShields
{
    internal static readonly string[] Ids = { "shield_buckler", "shield_kite", "shield_tower", "shield_wolf" };

    [MenuItem("Tools/Frostbound/Armas/Configurar escudos")]
    public static void Build()
    {
        SetupWeapons.ConfigureModels();
        int linked = 0;
        foreach (string id in Ids)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/" + id + ".asset");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Weapons/W_" + id + ".fbx");
            if (item == null || model == null) continue;
            item.weaponModel = model;
            ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Data/Items/ItemDatabase.asset");
            if (db != null && !db.items.Contains(item)) { db.items.Add(item); EditorUtility.SetDirty(db); }
            Sprite icon = SetupWeapons.RenderIcon(model, id, WeaponType.None);
            if (icon != null) item.icon = icon;
            EditorUtility.SetDirty(item);
            linked++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Frostbound] Escudos configurados: " + linked + " de " + Ids.Length + " con modelo propio.");
    }
}
