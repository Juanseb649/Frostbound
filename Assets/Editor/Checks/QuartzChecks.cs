using UnityEngine;

// Capturas fijas de los cristales de hielo (castillo, camino, ciudadela) para comparar antes/después.
// Por el bridge, en 01_Village en Play: exec QuartzChecks.Shots("q_antes") → exec QuartzChecks.ShotsBefore
public static class QuartzChecks
{
    public static string ShotsBefore() => Shots("q_antes");
    public static string ShotsAfter() => Shots("q_despues");

    private static string Shots(string prefix)
    {
        var gen = Object.FindAnyObjectByType<VillageWorldGenerator>();
        if (gen == null) return "sin poblado";
        var L = gen.Layout;
        var c = L.castle;
        Vector3 toVillage = new Vector3(-c.Gate.x, 0f, -c.Gate.z).normalized;
        string a = FrostboundBridge.CamShot(prefix + "_castillo", c.Gate + toVillage * 40f + Vector3.up * 18f, c.TownCenter + Vector3.up * 10f, 50f);
        // El cristal del camino más cercano a la ciudadela, de cerca y desde dos lados.
        GameObject shard = null;
        float best = float.MaxValue;
        foreach (Transform t in Object.FindObjectsByType<Transform>())
        {
            if (t.name != "Cristal_Camino") continue;
            float d = (t.position - c.Gate).sqrMagnitude;
            if (d < best) { best = d; shard = t.gameObject; }
        }
        string b = "-", d2 = "-";
        if (shard != null)
        {
            Vector3 p = shard.transform.position;
            b = FrostboundBridge.CamShot(prefix + "_cerca_a", p + new Vector3(2.2f, 1.6f, -2.4f), p + Vector3.up * 0.3f, 45f);
            d2 = FrostboundBridge.CamShot(prefix + "_cerca_b", p + new Vector3(-2.6f, 0.9f, 1.8f), p + Vector3.up * 0.4f, 45f);
        }
        string e = FrostboundBridge.CamShot(prefix + "_ciudadela", c.TownCenter + toVillage * 14f + Vector3.up * 8f, c.TownCenter, 60f);
        return a + " | " + b + " | " + d2 + " | " + e;
    }
}
