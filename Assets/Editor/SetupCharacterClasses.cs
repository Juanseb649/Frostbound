using UnityEngine;
using UnityEditor;

public static class SetupCharacterClasses
{
    private const string Folder = "Assets/Data/Classes";

    [MenuItem("Tools/Penguin/Character Classes/Create 4 Default Classes")]
    public static void CreateClasses()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Data", "Classes");

        CreateClass("Knight_Caballero", "Caballero", new Color(0.72f, 0.78f, 0.90f),
            strength: 5, mana: 5, agility: 5, health: 8, speed: 5.5f, damage: 10f);

        CreateClass("Viking_Vikingo", "Vikingo", new Color(0.80f, 0.55f, 0.35f),
            strength: 9, mana: 3, agility: 4, health: 6, speed: 5f, damage: 14f);

        CreateClass("Ninja", "Ninja", new Color(0.35f, 0.40f, 0.52f),
            strength: 4, mana: 4, agility: 9, health: 4, speed: 8f, damage: 9f);

        CreateClass("Mage_Mago", "Mago", new Color(0.65f, 0.45f, 0.80f),
            strength: 3, mana: 9, agility: 5, health: 3, speed: 6f, damage: 8f);

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Clases",
            "Se crearon las 4 clases en " + Folder + ":\n" +
            "Caballero (estable), Vikingo (fuerza), Ninja (agilidad), Mago (mana).\n\n" +
            "Ahora ejecuta Tools/Penguin/Setup Penguin as Player.", "OK");
    }

    private static void CreateClass(string assetName, string displayName, Color color,
        int strength, int mana, int agility, int health,
        float speed, float damage)
    {
        string path = Folder + "/" + assetName + ".asset";
        CharacterClass cls = AssetDatabase.LoadAssetAtPath<CharacterClass>(path);
        if (cls == null)
        {
            cls = ScriptableObject.CreateInstance<CharacterClass>();
            AssetDatabase.CreateAsset(cls, path);
        }

        cls.className = displayName;
        cls.classColor = color;
        cls.baseStrength = strength;
        cls.baseMana = mana;
        cls.baseAgility = agility;
        cls.baseHealth = health;
        cls.baseMoveSpeed = speed;
        cls.baseDamage = damage;
        EditorUtility.SetDirty(cls);
    }
}
