using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Frostbound > Agregar rodar al jugador: añade PlayerDodge al Player del poblado.
public static class SetupDodge
{
    private const string ScenePath = SceneIds.VillagePath;

    [MenuItem("Tools/Frostbound/Agregar rodar al jugador")]
    public static void Setup()
    {
        if (!FrostboundBridge.ConfirmSave()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            FrostboundBridge.Dialog("Frostbound", "No se encontró el objeto Player en " + ScenePath + ".", "OK");
            return;
        }
        if (player.GetComponent<PlayerDodge>() == null) player.AddComponent<PlayerDodge>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        FrostboundBridge.Dialog("Frostbound", "Listo: Espacio (o A en el mando) hace rodar al pingüino.", "OK");
    }
}

public static class FrostboundDodgeTests
{
    public static string Roll()
    {
        PlayerDodge d = Object.FindAnyObjectByType<PlayerDodge>();
        if (d == null) return "sin PlayerDodge";
        return d.TryRoll() ? "rodando desde " + d.transform.position.ToString("F2") : "no puede rodar";
    }

    // Voltereta lenta (2 s) para poder capturarla a mitad de giro.
    public static string RollSlow()
    {
        PlayerDodge d = Object.FindAnyObjectByType<PlayerDodge>();
        if (d == null) return "sin PlayerDodge";
        d.duration = 2f;
        d.speedMultiplier = 0.3f;
        return Roll();
    }

    public static string Where()
    {
        PlayerDodge d = Object.FindAnyObjectByType<PlayerDodge>();
        if (d == null) return "sin PlayerDodge";
        Transform m = d.model;
        return "pos " + d.transform.position.ToString("F2") + " rodando " + d.IsRolling
            + " modelo " + (m != null ? m.localEulerAngles.ToString("F0") : "-");
    }

    public static string Shot()
    {
        PlayerDodge d = Object.FindAnyObjectByType<PlayerDodge>();
        if (d == null) return "sin PlayerDodge";
        Vector3 p = d.transform.position;
        return FrostboundBridge.CamShot("roll_side", p + d.transform.right * 3.2f + Vector3.up * 1.1f, p + Vector3.up * 0.5f, 40f);
    }
}
