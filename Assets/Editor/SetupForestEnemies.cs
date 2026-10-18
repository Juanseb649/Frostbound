using UnityEditor;
using UnityEngine;

// Corruptos del Bosque (capítulo II): leñador (del vikingo), escupesavia (del mago) y acechador (del ninja);
// y de la Cumbre (capítulo III): cristalizado (del caballero) e hijo del Frost (del vikingo).
// Crea o actualiza sus EnemyDefinition en Assets/Data/Enemies y los añade al GameDatabase. Se puede ejecutar varias veces.
public static class SetupForestEnemies
{
    private const string Folder = "Assets/Data/Enemies/";
    private const string SapMaterialPath = "Assets/Materials/Gear/Corrupt_SapShot.mat";

    [MenuItem("Tools/Frostbound/Bosque/Crear enemigos del bosque")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        Material sap = SapMaterial();

        EnemyDefinition wood = Make(ForestCreature.WoodcutterId, "Corrupt_Viking", "Leñador corrupto", def =>
        {
            def.maxHealth = 110f; def.damage = new Vector2(7f, 10f); def.moveSpeed = 2.7f; def.experience = 24;
            def.ranged = false; def.attackRange = 2.2f; def.areaRadius = 2.6f; def.windup = 1.1f; def.cooldown = 2.6f;
            def.hitsPerAttack = 1; def.sightRange = 10f; def.lootPicks = 1;
        });
        EnemyDefinition spit = Make(ForestCreature.SapspitterId, "Corrupt_Mage", "Escupesavia", def =>
        {
            def.maxHealth = 55f; def.damage = new Vector2(4f, 6f); def.moveSpeed = 3.1f; def.experience = 20;
            def.ranged = true; def.attackRange = 10f; def.windup = 0.8f; def.cooldown = 2.4f; def.projectileSpeed = 9f;
            def.preferredDistance = new Vector2(7f, 10f); def.sightRange = 12f; def.lootPicks = 1;
            if (sap != null) def.projectileMaterial = sap;
        });
        EnemyDefinition stalk = Make(ForestCreature.StalkerId, "Corrupt_Ninja", "Acechador del bosque", def =>
        {
            def.maxHealth = 50f; def.damage = new Vector2(4f, 6f); def.moveSpeed = 5.2f; def.experience = 22;
            def.ranged = false; def.attackRange = 1.7f; def.areaRadius = 0f; def.windup = 0.3f; def.cooldown = 1.6f;
            def.hitsPerAttack = 1; def.sightRange = 12f; def.lootPicks = 1;
        });

        // Cumbre (capítulo III).
        EnemyDefinition crys = Make(SummitCreature.CrystallizedId, "Corrupt_Knight", "Cristalizado", def =>
        {
            def.maxHealth = 130f; def.damage = new Vector2(7f, 10f); def.moveSpeed = 2.9f; def.experience = 30;
            def.ranged = false; def.attackRange = 1.8f; def.areaRadius = 0f; def.windup = 0.6f; def.cooldown = 1.9f;
            def.hitsPerAttack = 1; def.sightRange = 11f; def.lootPicks = 1;
        });
        EnemyDefinition spawn = Make(SummitCreature.FrostSpawnId, "Corrupt_Viking", "Hijo del Frost", def =>
        {
            def.elite = true;
            def.maxHealth = 170f; def.damage = new Vector2(9f, 13f); def.moveSpeed = 3.3f; def.experience = 48;
            def.ranged = false; def.attackRange = 2.2f; def.areaRadius = 2.4f; def.windup = 0.8f; def.cooldown = 2.4f;
            def.hitsPerAttack = 1; def.sightRange = 12f; def.lootPicks = 2;
        });

        foreach (EnemyDefinition d in new[] { wood, spit, stalk, crys, spawn })
            if (d != null && !db.enemies.Contains(d)) db.enemies.Add(d);
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        return "enemigos del bosque: " + (wood != null) + "/" + (spit != null) + "/" + (stalk != null)
            + " · cumbre: " + (crys != null) + "/" + (spawn != null) + " · base de datos=" + db.enemies.Count;
    }

    private static EnemyDefinition Make(string id, string fromId, string displayName, System.Action<EnemyDefinition> tune)
    {
        string path = Folder + id + ".asset";
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        var from = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Folder + fromId + ".asset");
        if (from == null) return null;
        if (def == null)
        {
            def = Object.Instantiate(from);
            AssetDatabase.CreateAsset(def, path);
        }
        // Siempre del modelo y equipo de su base; el resto, los números del bosque.
        def.name = id;
        def.id = id;
        def.displayName = displayName;
        def.prefab = from.prefab;
        def.gear = from.gear;
        def.elite = false;   // el hijo del Frost lo vuelve a marcar en su ajuste
        def.projectileMaterial = from.projectileMaterial;
        tune(def);
        EditorUtility.SetDirty(def);
        return def;
    }

    private static Material SapMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(SapMaterialPath);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Corrupt_SapShot" };
        m.SetColor("_BaseColor", new Color(0.22f, 0.5f, 0.12f));
        m.SetFloat("_Smoothness", 0.85f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.1f, 0.35f, 0.05f));
        AssetDatabase.CreateAsset(m, SapMaterialPath);
        return m;
    }
}
