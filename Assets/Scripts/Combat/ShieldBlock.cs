using UnityEngine;

// Bloqueo con escudo al estilo Diablo II: cada golpe enemigo tiene una probabilidad de quedar anulado.
// La probabilidad sale del escudo (blockChance), sube con la Agilidad y baja al correr. Tope: 75 %.
// Las runas grabadas en el escudo actúan al bloquear: castigan al atacante o protegen al héroe.
public static class ShieldBlock
{
    public const float MaxChance = 0.75f;
    // Cada punto de Agilidad suma un 0,2 % (100 de Agilidad = +20 %).
    public const float AgilityPerPoint = 0.002f;
    // Correr reduce el bloqueo a dos tercios (como en Diablo II).
    public const float MovingFactor = 0.66f;

    // Ranuras de runa de un escudo según su rareza: común 0, poco común 1, raro 2, épico o mejor 3.
    // Un escudo del botín usa la mejor de las dos: la rareza del objeto o su calidad (mágico 1, raro 2, set o único 3).
    public static int RuneSlots(ItemStack shield)
    {
        if (shield == null || shield.item == null || !shield.item.IsShield) return 0;
        int byRarity = shield.item.rarity >= ItemRarity.Epic ? 3 : shield.item.rarity == ItemRarity.Rare ? 2 : shield.item.rarity == ItemRarity.Uncommon ? 1 : 0;
        int byQuality = shield.quality >= LootQuality.Set ? 3 : shield.quality == LootQuality.Rare ? 2 : shield.quality == LootQuality.Magic ? 1 : 0;
        return Mathf.Min(3, Mathf.Max(byRarity, byQuality));
    }

    public static ItemStack ShieldOf(Equipment eq)
    {
        ItemStack s = eq != null ? eq.GetStack(EquipSlot.Offhand) : null;
        if (s == null || s.item == null || !s.item.IsShield || s.IsBroken) return null;
        ItemStack main = eq.MainWeapon;
        if (main != null && main.item.UsesBothHands) return null;
        return s;
    }

    // Probabilidad de bloqueo quieto (0..0.75). Las runas Filo y Celeridad del escudo la suben.
    public static float Chance(Equipment eq)
    {
        ItemStack shield = ShieldOf(eq);
        if (shield == null) return 0f;
        float chance = shield.item.blockChance / 100f;
        CharacterStats st = eq.Stats;
        if (st != null) chance += st.Get(StatType.Agility) * AgilityPerPoint;
        foreach (ItemDefinition r in shield.runes)
        {
            if (r == null) continue;
            if (r.runeEffect == RuneEffect.Sharpness) chance += r.runePower * 0.5f;
            else if (r.runeEffect == RuneEffect.Swiftness) chance += r.runePower * 0.33f;
        }
        return Mathf.Clamp(chance, 0f, MaxChance);
    }

    // Lo llama EnemyBrain.DamagePlayer antes de aplicar el daño. Devuelve true si el golpe quedó bloqueado.
    public static bool TryBlock(CharacterStats stats, float amount, GameObject attacker)
    {
        if (stats == null || amount <= 0f) return false;
        Equipment eq = stats.GetComponent<Equipment>();
        float chance = Chance(eq);
        if (chance <= 0f) return false;
        Rigidbody rb = stats.GetComponent<Rigidbody>();
        if (rb != null && new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).sqrMagnitude > 1f) chance *= MovingFactor;
        if (Random.value >= chance) return false;

        Vector3 at = stats.transform.position + Vector3.up;
        CombatAudio.Block(at);
        if (WorldHUD.Instance != null)
            WorldHUD.Instance.Popup(stats.transform.position + Vector3.up * 1.7f, "¡Bloqueo!", new Color(0.85f, 0.92f, 1f), 0.9f);
        ApplyRunes(eq, stats, amount, attacker);
        return true;
    }

    private static void ApplyRunes(Equipment eq, CharacterStats stats, float blocked, GameObject attacker)
    {
        ItemStack shield = ShieldOf(eq);
        if (shield == null || shield.runes.Count == 0) return;
        Damageable target = attacker != null ? attacker.GetComponentInParent<Damageable>() : null;
        if (target != null && (target.IsDead || target.friendly)) target = null;
        Vector3 push = target != null ? target.transform.position - stats.transform.position : Vector3.zero;
        foreach (ItemDefinition r in shield.runes)
        {
            if (r == null) continue;
            switch (r.runeEffect)
            {
                case RuneEffect.Lifesteal:
                    // Sanguis: el golpe parado cura una parte de lo que habría hecho.
                    stats.Heal(blocked * r.runePower * 2f);
                    RuneAudio.Proc(r.runeEffect, stats.transform.position, 0.5f, 0.4f);
                    break;
                case RuneEffect.Fire:
                    if (target == null) break;
                    target.ApplyBurn(r.runePower, r.runeDuration);
                    RuneAudio.Proc(r.runeEffect, target.transform.position, 0.6f, 0.3f);
                    break;
                case RuneEffect.Poison:
                    if (target == null) break;
                    target.ApplyPoison(r.runePower, r.runeDuration);
                    RuneAudio.Proc(r.runeEffect, target.transform.position, 0.55f, 0.3f);
                    break;
                case RuneEffect.Frost:
                    if (target == null) break;
                    target.ApplySlow(r.runePower, r.runeDuration);
                    if (Random.value < r.runeChance) target.Freeze(1.2f);
                    RuneAudio.Proc(r.runeEffect, target.transform.position, 0.6f, 0.3f);
                    break;
                case RuneEffect.Chain:
                    if (target == null || Random.value >= r.runeChance) break;
                    PlayerCombat combat = stats.GetComponent<PlayerCombat>();
                    if (combat != null) combat.Chain(target, Mathf.Max(blocked, stats.Damage) * r.runePower * 2f);
                    RuneAudio.Proc(r.runeEffect, target.transform.position, 0.8f, 0.2f);
                    break;
                case RuneEffect.Knockback:
                    if (target == null) break;
                    target.Knockback(push, r.runePower);
                    RuneAudio.Proc(r.runeEffect, target.transform.position, 0.8f, 0.2f);
                    break;
            }
        }
    }

    // Texto corto del efecto de una runa grabada en un escudo.
    public static string RuneOnShield(ItemDefinition r)
    {
        if (r == null) return "";
        switch (r.runeEffect)
        {
            case RuneEffect.Fire: return "al bloquear, quema al atacante";
            case RuneEffect.Poison: return "al bloquear, envenena al atacante";
            case RuneEffect.Frost: return "al bloquear, ralentiza y puede congelar";
            case RuneEffect.Chain: return "al bloquear, puede soltar un rayo en cadena";
            case RuneEffect.Lifesteal: return "al bloquear, te cura";
            case RuneEffect.Sharpness: return "+" + Mathf.RoundToInt(r.runePower * 50f) + " % de bloqueo";
            case RuneEffect.Swiftness: return "+" + Mathf.RoundToInt(r.runePower * 33f) + " % de bloqueo";
            case RuneEffect.Knockback: return "al bloquear, empuja al atacante";
            default: return "";
        }
    }
}
