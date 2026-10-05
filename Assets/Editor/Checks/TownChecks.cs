using System.Linq;
using System.Text;
using UnityEngine;

// Comprobaciones en Play de la plaza (aldeanos charlando), la misión del castillo, el interior y el minimapa.
public static class TownChecks
{
    public static string ChatState()
    {
        var all = Object.FindObjectsByType<VillagerNPC>().Where(v => v.mood == VillagerMood.Chat).ToList();
        var sb = new StringBuilder();
        sb.Append("charlatanes ").Append(all.Count).Append(", charlando ").Append(all.Count(v => v.Chatting));
        if (WorldHUD.Instance != null) sb.Append(", globos ").Append(WorldHUD.Instance.ActiveAmbientBubbles);
        foreach (var v in all.Where(v => v.Chatting && v.Partner != null))
            sb.Append(" | ").Append(v.villagerName).Append("↔").Append(v.Partner.villagerName).Append(" L").Append(v.ChatLine)
              .Append(" ").Append(Vector3.Distance(v.transform.position, v.Partner.transform.position).ToString("0.0")).Append("m");
        bool fire = GameObject.Find("Fogata_Plaza") != null || GameObject.Find("Bancos") != null;
        sb.Append(fire ? " | ¡queda la fogata/bancos!" : " | plaza sin fogata");
        return sb.ToString();
    }

    public static string PlazaShot()
    {
        Vector3 c = new Vector3(-7f, 0f, -7f);
        return FrostboundBridge.CamShot("plaza", c + new Vector3(0f, 11f, -13f), c + Vector3.up, 50f);
    }
}
