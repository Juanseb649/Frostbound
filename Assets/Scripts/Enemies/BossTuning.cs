// Números de los jefes de la campaña en un solo sitio (vida, daño, nivel y fuerza de sus habilidades).
// Referencia: Rocco Vendaval (capítulo I). La vida se mide en "pingüinos normales del mismo nivel":
// Rocco ≈ 18; Madre Savia ≈ 18 (jefa del cap. II), Bobby ≈ 20 (se rinde al 10 %) y el Corazón ≈ 26 con sus
// cristales (jefe final). Las habilidades con aviso quitan un 8–13 % de la vida típica del héroe en su tramo.
// Presión medida en juego con el héroe quieto (el peor caso; BalanceChecks.ProbeStart/ProbeResult), en % de la vida
// típica del héroe por segundo: Rocco 11,5 · Madre Savia 5,8 → 8,2 → 7,6 · Bobby 8,4 · Corazón 4,9 → 6,9 → 8,1.
// BalanceChecks.BossTable recalcula la tabla con estos valores.
public static class BossTuning
{
    // Rocco Vendaval (sin cambios; es la referencia).
    public const int RoccoLevel = 9;
    public const float RoccoHealth = 9f, RoccoDamage = 1.4f;

    // Madre Savia (capítulo II).
    public const int SapLevel = 12;
    public const float SapHealth = 9f, SapDamage = 1.25f;
    public static float SapPower(int level) => 9f + level * 1.4f;        // lluvia; las raíces, ×1,3
    public static float SapPoison(int level) => 2.5f + level * 0.35f;    // vida por segundo en los charcos

    // Bobby (capítulo III).
    public const int BobbyLevel = 16;
    public const float BobbyHealth = 10f, BobbyDamage = 1.9f;
    public const float BobbyCooldown = 1.4f;
    public const float BobbyBlinkEvery = 5f;
    public static float BobbyLance(int level) => 12f + level * 1.6f;
    public const float BobbyLanceEvery = 5f, BobbyLanceEveryHurt = 4f;

    // Corazón del Frost (jefe final).
    public const int HeartLevel = 18;
    public const float HeartHealth = 10f, HeartDamage = 1.7f;
    public static float HeartRing(int level) => 14f + level * 1.7f;
    public static float HeartFeeder(int level) => 150f + level * 12f;
    public const float RingEvery = 3f;
    public const int MaxWaveAdds = 3;
}
