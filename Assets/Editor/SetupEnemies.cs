using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Pingüinos corruptos por el Frost (spec-enemigos-corruptos-y-loot): equipo, datos, prefabs y hordas en el poblado.
// Las hordas quedan fuera de la empalizada y lejos del claro de Steve; ambos lugares son zonas seguras.
public static class SetupEnemies
{
    public const string DataDir = "Assets/Data/Enemies";
    public const string PrefabDir = "Assets/Prefabs/Enemies";
    public const string RootName = "Enemigos";
    private const string ItemDbPath = "Assets/Data/Items/ItemDatabase.asset";
    private const string ProjectileMat = "Assets/Materials/Gear/Corrupt_IceWeapon.mat";

    public static readonly Vector3 SteveClearing = new Vector3(-11f, 0f, -47f);
    public const float CampSafeRadius = 38f;
    public const float SteveSafeRadius = 20f;

    private struct Spec
    {
        public string id, name, fbx;
        public bool elite, ranged;
        public float hp, dmgMin, dmgMax, speed, range, windup, cooldown, sight, area, projSpeed;
        public int xp, hits, picks;
        public Vector2 preferred;
    }

    private static readonly Spec[] Specs =
    {
        new Spec { id = "Corrupt_Melee", name = "Pingüino corrupto por el Frost", fbx = "Frostbound_Corrupt_Melee_Gear", hp = 45, dmgMin = 3, dmgMax = 5, speed = 3.6f, range = 1.6f, windup = 0.4f, cooldown = 1.4f, sight = 10, xp = 9, hits = 1, picks = 1 },
        new Spec { id = "Corrupt_Ranged", name = "Pingüino corrupto por el Frost (arquero)", fbx = "Frostbound_Corrupt_Ranged_Gear", ranged = true, hp = 32, dmgMin = 2.5f, dmgMax = 4, speed = 3.2f, range = 9, windup = 0.6f, cooldown = 2f, sight = 12, xp = 10, hits = 1, picks = 1, projSpeed = 14, preferred = new Vector2(6, 9) },
        new Spec { id = "Corrupt_Ninja", name = "Ninja corrupto por el Frost", fbx = "Frostbound_Corrupt_Ninja_Gear", elite = true, hp = 100, dmgMin = 5, dmgMax = 7, speed = 5, range = 1.8f, windup = 0.35f, cooldown = 2.2f, sight = 12, xp = 32, hits = 2, picks = 2 },
        new Spec { id = "Corrupt_Viking", name = "Vikingo corrupto por el Frost", fbx = "Frostbound_Corrupt_Viking_Gear", elite = true, hp = 180, dmgMin = 8, dmgMax = 11, speed = 3.4f, range = 2.2f, area = 2.2f, windup = 0.8f, cooldown = 3f, sight = 10, xp = 40, hits = 1, picks = 2 },
        new Spec { id = "Corrupt_Mage", name = "Mago corrupto por el Frost", fbx = "Frostbound_Corrupt_Mage_Gear", elite = true, ranged = true, hp = 90, dmgMin = 5.5f, dmgMax = 8, speed = 3f, range = 10, windup = 0.9f, cooldown = 2.6f, sight = 13, xp = 36, hits = 1, picks = 2, projSpeed = 10, preferred = new Vector2(7, 10) },
        new Spec { id = "Corrupt_Knight", name = "Caballero corrupto por el Frost", fbx = "Frostbound_Corrupt_Knight_Gear", elite = true, hp = 225, dmgMin = 9, dmgMax = 12, speed = 2.8f, range = 2f, windup = 1f, cooldown = 3.2f, sight = 10, xp = 54, hits = 1, picks = 3 },
    };

    // (id, posición, nivel mín., nivel máx., tamaño mín., tamaño máx., prob. de élite)
    private static readonly (string id, Vector3 pos, int lvMin, int lvMax, int sizeMin, int sizeMax, float elite)[] Camps =
    {
        ("Campamento_Noroeste", new Vector3(-16f, 0f, 50f), 2, 3, 4, 6, 0.25f),
        ("Campamento_Norte", new Vector3(18f, 0f, 56f), 2, 3, 5, 7, 0.3f),
        ("Campamento_Noreste", new Vector3(40f, 0f, 42f), 2, 3, 4, 6, 0.25f),
        ("Campamento_Paso_Oeste", new Vector3(-42f, 0f, 44f), 2, 3, 4, 6, 0.25f),
        ("Campamento_Este", new Vector3(52f, 0f, 12f), 1, 2, 4, 6, 0.15f),
        ("Campamento_Oeste", new Vector3(-54f, 0f, 10f), 1, 2, 4, 6, 0.15f),
        ("Campamento_Sureste", new Vector3(45f, 0f, -40f), 1, 2, 4, 5, 0.1f),
    };

    [MenuItem("Tools/Frostbound/Enemigos/Preparar enemigos en el poblado")]
    public static void SetupAll()
    {
        if (!FrostboundBridge.ConfirmSave()) return;
        Dictionary<string, EnemyDefinition> defs = CreateAssets();
        string report = PlaceInVillage(defs);
        FrostboundBridge.Dialog("Frostbound", report, "OK");
    }

    public static string SetupFromBridge()
    {
        Dictionary<string, EnemyDefinition> defs = CreateAssets();
        return PlaceInVillage(defs);
    }

    // ---------- Equipo, datos y prefabs ----------

    public static Dictionary<string, EnemyDefinition> CreateAssets()
    {
        FrostboundGearSetup.Setup();
        FrostboundGearSetup.EnsureFolder(DataDir);
        FrostboundGearSetup.EnsureFolder(PrefabDir);
        GameObject penguin = AssetDatabase.LoadAssetAtPath<GameObject>(SetupPenguinWardrobe.PrefabPath);
        Material projMat = AssetDatabase.LoadAssetAtPath<Material>(ProjectileMat);

        var result = new Dictionary<string, EnemyDefinition>();
        foreach (Spec s in Specs)
        {
            OutfitItem gear = FrostboundGearSetup.GearItem("Gear_" + s.id, s.fbx);
            string path = DataDir + "/" + s.id + ".asset";
            EnemyDefinition def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.id = s.id;
            def.displayName = s.name;
            def.elite = s.elite;
            def.gear = gear;
            def.maxHealth = s.hp;
            def.damage = new Vector2(s.dmgMin, s.dmgMax);
            def.moveSpeed = s.speed;
            def.experience = s.xp;
            def.ranged = s.ranged;
            def.attackRange = s.range;
            def.windup = s.windup;
            def.cooldown = s.cooldown;
            def.hitsPerAttack = s.hits;
            def.areaRadius = s.area;
            def.sightRange = s.sight;
            def.lootPicks = s.picks;
            def.projectileSpeed = s.projSpeed > 0 ? s.projSpeed : 14f;
            def.projectileMaterial = projMat;
            def.preferredDistance = s.preferred.sqrMagnitude > 0 ? s.preferred : new Vector2(6f, 9f);
            def.prefab = BuildPrefab(s, penguin);
            EditorUtility.SetDirty(def);
            result[s.id] = def;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static GameObject BuildPrefab(Spec s, GameObject penguin)
    {
        var root = new GameObject("Enemy_" + s.id);
        // Visual: lo sacude Damageable al recibir golpes; dentro, Penguin lo balancea PenguinBodySway (como el héroe y los NPC).
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        var body = (GameObject)PrefabUtility.InstantiatePrefab(penguin, visual.transform);
        body.name = "Penguin";
        // Polimorfismo: del animador del héroe solo se hereda la locomoción (PenguinLocomotionAnimator).
        PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        PenguinRigAnimator heroAnim = body.GetComponent<PenguinRigAnimator>();
        if (heroAnim != null && !(heroAnim is PenguinLocomotionAnimator))
        {
            string json = JsonUtility.ToJson(heroAnim);
            Object.DestroyImmediate(heroAnim);
            PenguinLocomotionAnimator loco = body.AddComponent<PenguinLocomotionAnimator>();
            JsonUtility.FromJsonOverwrite(json, loco);
            loco.enableIdleActions = false;
            loco.referenceSpeed = s.speed;
        }
        PenguinOutfit outfit = body.GetComponent<PenguinOutfit>();
        if (outfit != null)
        {
            outfit.useClassOutfit = false;
            outfit.startingItems = new List<OutfitItem>();
        }
        HeroGearEquipper eq = body.GetComponent<HeroGearEquipper>();
        if (eq != null) eq.startingClass = "";

        float height = 1f;
        Bounds? b = PenguinBounds(body);
        if (b.HasValue)
        {
            body.transform.localPosition = new Vector3(0f, -b.Value.min.y, 0f);
            height = b.Value.size.y;
        }

        var col = root.AddComponent<CapsuleCollider>();
        col.height = height;
        col.radius = height * 0.3f;
        col.center = new Vector3(0f, height * 0.5f, 0f);

        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f;
        agent.height = height;
        agent.baseOffset = 0f;
        agent.speed = s.speed;
        agent.angularSpeed = 720f;
        agent.acceleration = 24f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;

        var dmg = root.AddComponent<Damageable>();
        dmg.displayName = s.name;
        dmg.maxHealth = s.hp;
        dmg.visual = visual.transform;
        dmg.popupHeight = height + 0.3f;

        // Misma locomoción que el héroe: balanceo, rebote e inclinación al correr (valores del Player en 01_Village).
        var sway = root.AddComponent<PenguinBodySway>();
        sway.model = body.transform;
        sway.Configure(14f, 0.12f, 6f, 8f, false, 1.2f, 0.025f);
        sway.referenceSpeed = s.speed;

        root.AddComponent<PenguinRagdoll>();

        var brain = root.AddComponent<EnemyBrain>();
        brain.model = body.transform;

        string path = PrefabDir + "/Enemy_" + s.id + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Bounds? PenguinBounds(GameObject go)
    {
        Bounds? b = null;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.StartsWith("Penguin_")) continue;
            if (b == null) b = r.bounds;
            else { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; }
        }
        return b;
    }

    // ---------- Escena ----------

    public static string PlaceInVillage(Dictionary<string, EnemyDefinition> defs)
    {
        Scene scene = EditorSceneManager.OpenScene(SceneIds.VillagePath, OpenSceneMode.Single);
        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;

        Zone(root, "Zona_Segura_Campamento", Vector3.zero, CampSafeRadius);
        Transform steve = FindSteve();
        Zone(root, "Zona_Segura_Steve", steve != null ? new Vector3(steve.position.x, 0f, steve.position.z) : SteveClearing, SteveSafeRadius);

        var loot = new GameObject("Botin").AddComponent<LootDirector>();
        loot.transform.SetParent(root, false);
        loot.database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDbPath);

        PlumagePalette palette = AssetDatabase.LoadAssetAtPath<PlumagePalette>(FrostboundGearSetup.PalettePath);
        EnemyDefinition melee = defs["Corrupt_Melee"], ranged = defs["Corrupt_Ranged"];
        var elites = new[] { defs["Corrupt_Ninja"], defs["Corrupt_Viking"], defs["Corrupt_Mage"], defs["Corrupt_Knight"] };
        foreach (var c in Camps)
        {
            var go = new GameObject(c.id);
            go.transform.SetParent(root, false);
            go.transform.position = c.pos;
            EnemyCamp camp = go.AddComponent<EnemyCamp>();
            camp.campId = c.id;
            camp.melee = melee;
            camp.ranged = ranged;
            camp.elites = elites;
            camp.palette = palette;
            camp.monsterLevel = new Vector2Int(c.lvMin, c.lvMax);
            camp.groupSize = new Vector2Int(c.sizeMin, c.sizeMax);
            camp.eliteChance = c.elite;
            camp.rangedFraction = 0.3f;
            camp.radius = 5f;
        }

        GameObject player = GameObject.Find("Player");
        if (player != null && player.GetComponent<PlayerRespawn>() == null) player.AddComponent<PlayerRespawn>();

        string nav = BakeNavMesh(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Enemigos listos: " + Specs.Length + " tipos, " + Camps.Length + " campamentos fuera del poblado. " + nav + " " + Validate();
    }

    private static void Zone(Transform root, string name, Vector3 pos, float radius)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = pos;
        SafeZone z = go.AddComponent<SafeZone>();
        z.zoneName = name;
        z.radius = radius;
    }

    private static Transform FindSteve()
    {
        foreach (NameTag t in Object.FindObjectsByType<NameTag>(FindObjectsInactive.Include))
            if (t.displayName == "Steve") return t.transform;
        return null;
    }

    private static string BakeNavMesh(Transform root)
    {
        var go = new GameObject("NavMesh_Enemigos");
        go.transform.SetParent(root, false);
        var surface = go.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Volume;
        surface.center = new Vector3(0f, 4f, 0f);
        surface.size = new Vector3(200f, 16f, 200f);
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        if (surface.navMeshData == null) return "NavMesh: NO se pudo hornear.";

        string dir = "Assets/Scenes/01_Village";
        FrostboundGearSetup.EnsureFolder(dir);
        string path = dir + "/NavMesh_Enemigos.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(surface.navMeshData, path);
        AssetDatabase.SaveAssets();
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        return "NavMesh horneado (" + tri.indices.Length / 3 + " triángulos).";
    }

    // Comprueba que ningún campamento cae en una zona segura y que todos tienen suelo navegable.
    public static string Validate()
    {
        var problems = new List<string>();
        foreach (EnemyCamp c in Object.FindObjectsByType<EnemyCamp>())
        {
            Vector3 p = c.transform.position;
            if (SafeZoneContains(p, c.radius + 4f)) problems.Add(c.campId + " está dentro de una zona segura");
            if (!NavMesh.SamplePosition(p, out NavMeshHit _, 4f, NavMesh.AllAreas)) problems.Add(c.campId + " no tiene NavMesh");
        }
        return problems.Count == 0 ? "Validación OK." : "Problemas: " + string.Join("; ", problems);
    }

    private static bool SafeZoneContains(Vector3 p, float margin)
    {
        foreach (SafeZone z in Object.FindObjectsByType<SafeZone>())
        {
            Vector3 d = p - z.transform.position;
            d.y = 0f;
            if (d.magnitude < z.radius + margin) return true;
        }
        return false;
    }
}
