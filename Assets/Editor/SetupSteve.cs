using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// NPC Steve: el rockero que toca junto a su fogata, en un claro del bosque al sur del poblado.
public static class SetupSteve
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    public const string ObjectName = "NPC_Steve";
    public static readonly Color Plumage = new Color(0xF2 / 255f, 0xC2 / 255f, 0x30 / 255f);

    public static readonly string[] Dialogue =
    {
        "¿Otra vez esa luz azul en la montaña? Mientras suene esta fogata, aquí nadie se congela.",
        "Antes tocaba para cien pingüinos en la plaza. Ahora prefiero el bosque: los pinos no se asustan.",
        "Si subes al Frostspire, cántale algo al viento. Dicen que el hielo escucha."
    };

    [MenuItem("Tools/Frostbound/Agregar a Steve en el bosque")]
    public static void AddToScene()
    {
        if (!FrostboundBridge.ConfirmSave()) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject town = GameObject.Find(SetupVillage.RootName);
        Transform fire = town != null ? town.transform.Find("Claro_Steve/Fogata_Steve") : null;
        if (fire == null)
        {
            FrostboundBridge.Dialog("Frostbound", "No hay claro de Steve en la escena. Ejecuta primero Tools > Frostbound > Construir Poblado.", "OK");
            return;
        }
        if (Add(town.transform, fire) == null) return;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        FrostboundBridge.Dialog("Frostbound", "Steve está tocando junto a su fogata, en el bosque al sur del poblado.", "OK");
    }

    public static GameObject Add(Transform town, Transform fire)
    {
        OutfitItem gear = AssetDatabase.LoadAssetAtPath<OutfitItem>(SetupPenguinWardrobe.OutfitDataDir + "/Gear_Steve.asset");
        if (gear == null) gear = FrostboundGearSetup.GearItem("Gear_Steve", "Frostbound_NPC_Rocker_Gear");
        if (gear == null)
        {
            Debug.LogWarning("[Steve] Falta el equipo de Steve (Tools > Frostbound > Equipamiento > Preparar equipo de clases).");
            return null;
        }

        Transform parent = town.Find("Pinguinos") ?? town;
        foreach (string old in new[] { ObjectName, "NPC_Rocco_Vendaval" })
        {
            Transform existing = parent.Find(old);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }

        float a = 215f * Mathf.Deg2Rad;
        Vector3 pos = fire.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 2.2f;
        var spec = new SetupVillage.NpcSpec
        {
            name = "Steve",
            role = "Músico",
            showRole = true,
            mood = VillagerMood.Huddle,
            pos = pos,
            scale = 1f,
            fear = 0f,
            plumage = Plumage,
            materialName = "Toon_Penguin_Steve",
            outfits = new List<OutfitItem> { gear },
            focus = fire,
            options = new List<NPCOption> { NPCOption.Hablar },
            lines = Dialogue
        };
        GameObject root = SetupVillage.CreateNPC(parent, spec);
        if (root == null) return null;
        root.name = ObjectName;

        PenguinRigAnimator rig = root.GetComponentInChildren<PenguinRigAnimator>();
        if (rig != null) rig.referenceSpeed = 1.3f;

        var pulse = root.AddComponent<EmissionPulse>();
        pulse.partName = "Pendant";
        pulse.emission = new Color(0x4F / 255f, 0xD0 / 255f, 1f);
        pulse.minIntensity = 0.8f;
        pulse.maxIntensity = 1.4f;
        pulse.period = 2f;
        return root;
    }
}
