using UnityEngine;

// Pasos de prueba en Play que el canal de comandos (FrostboundBridge "exec") puede lanzar.
public static class FrostboundPlayTests
{
    public static string ApproachSmith() => Approach("NPC_Herrera_Brunna");
    public static string ApproachSteve() => Approach("NPC_Steve");

    public static string Approach(string npcName)
    {
        GameObject npc = GameObject.Find(npcName);
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (npc == null || player == null) return "falta " + (npc == null ? npcName : "Player");
        Vector3 toCenter = -npc.transform.position;
        toCenter.y = 0f;
        Vector3 from = npc.transform.position + toCenter.normalized * 3.5f + Vector3.up * 0.05f;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.position = from;
        player.transform.position = from;
        player.Interact(npc.GetComponent<NPCInteractable>());
        return "ok";
    }

    public static string ChooseTalk()
    {
        WorldHUD hud = WorldHUD.Instance;
        if (hud == null || !hud.MenuOpen) return "menú cerrado";
        hud.MenuTarget.Choose(NPCOption.Hablar);
        return "ok";
    }

    public static string Probe()
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return "sin Player";
        var f = typeof(PlayerController).GetField("_interactTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var md = typeof(PlayerController).GetField("_moveDirection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var target = f.GetValue(player) as NPCInteractable;
        Rigidbody rb = player.GetComponent<Rigidbody>();
        string d = target != null ? Vector3.Distance(player.transform.position, target.transform.position).ToString("F2") : "-";
        return "pos " + player.transform.position.ToString("F2") + " vel " + rb.linearVelocity.ToString("F2") + " dir " + md.GetValue(player)
            + " target " + (target != null ? target.name : "null") + " dist " + d + " t " + Time.time.ToString("F1") + " ts " + Time.timeScale
            + " speed " + (player.GetComponent<CharacterStats>() != null ? player.GetComponent<CharacterStats>().MoveSpeed.ToString() : "?") + " | " + MenuState();
    }

    public static string MenuState()
    {
        WorldHUD hud = WorldHUD.Instance;
        if (hud == null) return "sin HUD";
        return hud.MenuOpen ? "abierto: " + hud.MenuTarget.displayName : "cerrado";
    }
}
