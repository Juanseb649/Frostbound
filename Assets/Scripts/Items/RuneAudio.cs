using UnityEngine;

// Sonido propio de cada runa: al grabarla en el arma y cada vez que su efecto salta en un golpe.
public static class RuneAudio
{
    public static string Name(RuneEffect e)
    {
        switch (e)
        {
            case RuneEffect.Fire: return "Ignis";
            case RuneEffect.Poison: return "Toxis";
            case RuneEffect.Frost: return "Glacies";
            case RuneEffect.Chain: return "Fulgur";
            case RuneEffect.Lifesteal: return "Sanguis";
            case RuneEffect.Sharpness: return "Acies";
            case RuneEffect.Swiftness: return "Celeris";
            case RuneEffect.Knockback: return "Impetus";
            default: return "";
        }
    }

    public static void Engrave(RuneEffect e)
    {
        string n = Name(e);
        if (n.Length > 0) AudioManager.PlaySFX("SFX/Rune_" + n + "_Engrave", 1f);
    }

    // minGap evita que las runas de cada golpe (fuego, veneno...) se amontonen.
    public static void Proc(RuneEffect e, Vector3 at, float volume = 0.7f, float minGap = 0.3f)
    {
        string n = Name(e);
        if (n.Length > 0) AudioManager.Play3D("SFX/Rune_" + n + "_Proc", at, volume, minGap, 2f, 24f, 0.08f);
    }
}
