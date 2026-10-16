using UnityEngine;

// Sonidos de combate: armas al usarse y golpes con gore (bancos en Resources/Audio/Combate, del 400 Sounds Pack).
// Si falta un banco se usa el sonido anterior del juego.
public static class CombatAudio
{
    public const string SwingLight = "Combate/Swing_Ligero/", SwingHeavy = "Combate/Swing_Pesado/", Bow = "Combate/Arco/",
        Throw = "Combate/Lanzar/", Magic = "Combate/Magia/", Gore = "Combate/Gore/", GoreCrit = "Combate/Gore_Critico/",
        GoreDeath = "Combate/Gore_Muerte/", Hurt = "Combate/Herido/", Metal = "Combate/Metal/";

    public static void Attack(PlayerCombat c, ItemStack weapon)
    {
        Vector3 at = c.transform.position + Vector3.up;
        if (weapon == null) { AudioManager.Play3D(SwingLight, at, 0.55f, 0.08f); return; }
        if (c.UsesMagic) { if (!Try(Magic, at, 0.6f)) AudioManager.Play3D(AudioId.Spell_FaeMagic, at, 0.6f); return; }
        if (c.UsesBow) { Try(Bow, at, 0.75f); return; }
        if (c.IsRanged) { Try(Throw, at, 0.7f); return; }
        Try(c.UsesHeavyCombo ? SwingHeavy : SwingLight, at, c.UsesHeavyCombo ? 0.85f : 0.7f);
    }

    public static void Hit(Vector3 at, bool critical, ItemStack weapon)
    {
        if (critical)
        {
            if (!Try(GoreCrit, at, 1f, 0.05f)) AudioManager.Play3D(AudioId.Spell_IceHiss, at, 1f, 2f, 25f);
            return;
        }
        if (!Try(Gore, at, 0.8f, 0.04f)) AudioManager.Play3D(AudioId.Slime_Splat, at, 0.75f, 2f, 22f);
        if (weapon != null && Random.value < 0.35f) Try(Metal, at, 0.35f, 0.15f);
    }

    // Un enemigo golpea al héroe.
    public static void PlayerHit(Vector3 at)
    {
        if (!Try(Gore, at, 0.75f, 0.08f)) AudioManager.Play3D(AudioId.Slime_Splat, at, 0.6f, 2f, 20f);
        Try(Hurt, at, 0.55f, 0.35f);
    }

    public static void Death(Vector3 at) => Try(GoreDeath, at, 0.9f, 0.05f);

    private static bool Try(string bank, Vector3 at, float volume, float minGap = 0f)
    {
        if (!AudioManager.Has(bank)) return false;
        AudioManager.Play3D(bank, at, volume, minGap, 2f, 24f, 0.08f);
        return true;
    }
}
