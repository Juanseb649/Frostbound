using System.Collections.Generic;
using UnityEngine;

// Tiradas de botín al estilo Diablo II (spec §5.3–5.5): tabla de tesoro por nivel → base → calidad → afijos.
// Es puro (sin MonoBehaviour) y determinista: la misma seed da el mismo botín.
public static class LootRoller
{
    public struct Table
    {
        public float noDrop, potions, weapons, armor, jewelry, runes;
    }

    public static readonly Table Trooper = new Table { noDrop = 60f, potions = 16f, weapons = 14f, armor = 14f, jewelry = 3f, runes = 1.2f };
    public static readonly Table Elite = new Table { noDrop = 20f, potions = 14f, weapons = 18f, armor = 18f, jewelry = 5f, runes = 3f };

    private struct QualityOdds
    {
        public float unique, set, rare, magic, superior, worn;
    }

    private static readonly QualityOdds TrooperOdds = new QualityOdds { unique = 0.0025f, set = 0.005f, rare = 0.03f, magic = 0.30f, superior = 0.08f, worn = 0.06f };
    private static readonly QualityOdds EliteOdds = new QualityOdds { unique = 0.008f, set = 0.015f, rare = 0.09f, magic = 0.45f, superior = 0.10f, worn = 0f };

    private class AffixDef
    {
        public string id, label;
        public bool prefix, weapon, armor, jewelry;
        public AffixKind kind;
        public float min, max, perLevel;
        public int minLevel;
    }

    private static readonly AffixDef[] Affixes =
    {
        new AffixDef { id = "cortante", label = "Cortante", prefix = true, weapon = true, kind = AffixKind.Damage, min = 1f, max = 3f, perLevel = 0.35f },
        new AffixDef { id = "glacial", label = "Glacial", prefix = true, weapon = true, kind = AffixKind.FrostChance, min = 0.08f, max = 0.15f, perLevel = 0.004f },
        new AffixDef { id = "voraz", label = "Voraz", prefix = true, weapon = true, kind = AffixKind.Lifesteal, min = 0.03f, max = 0.06f, perLevel = 0.002f, minLevel = 2 },
        new AffixDef { id = "firme", label = "Firme", prefix = true, armor = true, kind = AffixKind.Armor, min = 2f, max = 5f, perLevel = 0.6f },
        new AffixDef { id = "radiante", label = "Radiante", prefix = true, armor = true, jewelry = true, kind = AffixKind.MagicFind, min = 5f, max = 12f, perLevel = 0.5f },
        new AffixDef { id = "oso", label = "del Oso", weapon = true, armor = true, jewelry = true, kind = AffixKind.Health, min = 1f, max = 3f, perLevel = 0.3f },
        new AffixDef { id = "morsa", label = "de la Morsa", weapon = true, armor = true, jewelry = true, kind = AffixKind.Strength, min = 1f, max = 3f, perLevel = 0.3f },
        new AffixDef { id = "zorro", label = "del Zorro Ártico", weapon = true, armor = true, jewelry = true, kind = AffixKind.Agility, min = 1f, max = 3f, perLevel = 0.3f },
        new AffixDef { id = "buho", label = "del Búho", weapon = true, armor = true, jewelry = true, kind = AffixKind.Mana, min = 1f, max = 3f, perLevel = 0.3f },
        new AffixDef { id = "ventisca", label = "de la Ventisca", weapon = true, jewelry = true, kind = AffixKind.AttackSpeed, min = 0.05f, max = 0.1f, perLevel = 0.004f },
        new AffixDef { id = "glaciar", label = "del Glaciar", armor = true, kind = AffixKind.Armor, min = 2f, max = 4f, perLevel = 0.5f },
    };

    private static readonly string[] RareFirst = { "Colmillo", "Aullido", "Esquirla", "Grieta", "Lamento", "Corona", "Garra", "Susurro", "Eco", "Filo", "Latido", "Mordida" };
    private static readonly string[] RareSecond = { "Invernal", "Glacial", "de la Ventisca", "del Abismo", "de Escarcha", "del Frostspire", "Sin Nombre", "de la Hoguera" };

    // ---------- API ----------

    public static List<ItemStack> Roll(ItemDatabase db, int itemLevel, int picks, bool elite, float magicFind, DeterministicRng rng, bool guaranteeMagic = false)
    {
        var drops = new List<ItemStack>();
        if (db == null) return drops;
        Table table = elite ? Elite : Trooper;
        for (int p = 0; p < picks; p++)
        {
            bool forced = guaranteeMagic && p == 0;
            ItemStack s = RollOne(db, table, itemLevel, elite, magicFind, rng, forced);
            if (s != null) drops.Add(s);
        }
        return drops;
    }

    public static ItemStack RollOne(ItemDatabase db, Table t, int ilvl, bool elite, float magicFind, DeterministicRng rng, bool forceMagic = false)
    {
        var kinds = new List<int> { 0, 1, 2, 3, 4, 5 };
        var weights = new List<float> { forceMagic ? 0f : t.noDrop, forceMagic ? 0f : t.potions, t.weapons, t.armor, t.jewelry, forceMagic ? 0f : t.runes };
        int kind = rng.PickWeighted(kinds, weights);
        switch (kind)
        {
            case 0: return null;
            case 1: return Simple(db, rng, it => it.IsConsumable && (it.heal > 0f || it.restoreMana > 0f));
            case 5: return Simple(db, rng, it => it.IsRune);
            default:
                return Equipment(db, kind, ilvl, elite, magicFind, rng, forceMagic);
        }
    }

    // ---------- Objetos sin calidad ----------

    private static ItemStack Simple(ItemDatabase db, DeterministicRng rng, System.Predicate<ItemDefinition> filter)
    {
        var pool = new List<ItemDefinition>();
        var w = new List<float>();
        foreach (ItemDefinition it in db.items)
        {
            if (it == null || !filter(it)) continue;
            pool.Add(it);
            w.Add(RarityWeight(it.rarity));
        }
        if (pool.Count == 0) return null;
        return new ItemStack(rng.PickWeighted(pool, w), 1);
    }

    private static float RarityWeight(ItemRarity r)
    {
        switch (r)
        {
            case ItemRarity.Common: return 4f;
            case ItemRarity.Uncommon: return 2f;
            case ItemRarity.Rare: return 1f;
            case ItemRarity.Epic: return 0.4f;
            default: return 0.2f;
        }
    }

    // ---------- Equipo ----------

    private static bool IsWeaponBase(ItemDefinition it) => it.IsWeapon && it.equipSlot == EquipSlot.Weapon;
    private static bool IsArmorBase(ItemDefinition it) => it.IsEquippable && !it.IsWeapon && it.equipSlot != EquipSlot.Amulet;
    private static bool IsJewelry(ItemDefinition it) => it.IsEquippable && it.equipSlot == EquipSlot.Amulet;

    private static System.Predicate<ItemDefinition> KindFilter(int kind)
    {
        if (kind == 2) return IsWeaponBase;
        if (kind == 3) return IsArmorBase;
        return IsJewelry;
    }

    private static ItemStack Equipment(ItemDatabase db, int kind, int ilvl, bool elite, float mf, DeterministicRng rng, bool forceMagic)
    {
        System.Predicate<ItemDefinition> filter = KindFilter(kind);
        LootQuality q = RollQuality(elite ? EliteOdds : TrooperOdds, ilvl, mf, rng);
        if (forceMagic && q < LootQuality.Magic) q = LootQuality.Magic;

        if (q == LootQuality.Unique)
        {
            ItemDefinition u = PickBase(db, ilvl + 3, rng, it => it.IsEquippable && it.rarity == ItemRarity.Unique && it.armorSet == null);
            if (u != null) return new ItemStack(u, 1) { quality = LootQuality.Unique, itemLevel = Mathf.Max(1, ilvl) };
            q = LootQuality.Rare;
        }
        if (q == LootQuality.Set)
        {
            ItemDefinition s = PickBase(db, ilvl + 3, rng, it => it.IsEquippable && it.armorSet != null);
            if (s != null) return new ItemStack(s, 1) { quality = LootQuality.Set, itemLevel = Mathf.Max(1, ilvl) };
            q = LootQuality.Rare;
        }

        ItemDefinition b = PickBase(db, ilvl + 2, rng, it => filter(it) && it.rarity != ItemRarity.Unique && it.armorSet == null);
        if (b == null) b = PickBase(db, 99, rng, it => filter(it) && it.rarity != ItemRarity.Unique && it.armorSet == null);
        if (b == null) return null;

        var stack = new ItemStack(b, 1) { quality = q };
        stack.itemLevel = Mathf.Max(1, ilvl);
        ApplyQuality(stack, ilvl, rng);
        return stack;
    }

    private static ItemDefinition PickBase(ItemDatabase db, int maxLevel, DeterministicRng rng, System.Predicate<ItemDefinition> filter)
    {
        var pool = new List<ItemDefinition>();
        var w = new List<float>();
        foreach (ItemDefinition it in db.items)
        {
            if (it == null || !filter(it) || it.requiredLevel > maxLevel) continue;
            pool.Add(it);
            w.Add(RarityWeight(it.rarity));
        }
        return pool.Count == 0 ? null : rng.PickWeighted(pool, w);
    }

    public static float EffectiveMF(LootQuality q, float mf)
    {
        mf = Mathf.Max(0f, mf);
        switch (q)
        {
            case LootQuality.Unique: return mf * 250f / (mf + 250f);
            case LootQuality.Set: return mf * 500f / (mf + 500f);
            case LootQuality.Rare: return mf * 600f / (mf + 600f);
            default: return mf;
        }
    }

    private static LootQuality RollQuality(QualityOdds o, int ilvl, float mf, DeterministicRng rng)
    {
        float lvl = 1f + 0.02f * Mathf.Max(0, ilvl - 1);
        if (rng.Chance(o.unique * (1f + EffectiveMF(LootQuality.Unique, mf) / 100f) * lvl)) return LootQuality.Unique;
        if (rng.Chance(o.set * (1f + EffectiveMF(LootQuality.Set, mf) / 100f) * lvl)) return LootQuality.Set;
        if (rng.Chance(o.rare * (1f + EffectiveMF(LootQuality.Rare, mf) / 100f) * lvl)) return LootQuality.Rare;
        if (rng.Chance(Mathf.Min(0.9f, o.magic * (1f + mf / 100f) * lvl))) return LootQuality.Magic;
        if (rng.Chance(o.superior)) return LootQuality.Superior;
        if (rng.Chance(o.worn)) return LootQuality.Worn;
        return LootQuality.Normal;
    }

    // ---------- Afijos ----------

    public static void ApplyQuality(ItemStack s, int ilvl, DeterministicRng rng)
    {
        ItemDefinition it = s.item;
        bool weapon = IsWeaponBase(it), jewelry = IsJewelry(it);
        switch (s.quality)
        {
            case LootQuality.Worn:
                if (weapon && it.damage > 0f) s.affixes.Add(new RolledAffix("gastado", AffixKind.Damage, -Mathf.Max(1f, it.damage * 0.25f)));
                else if (it.armor > 0) s.affixes.Add(new RolledAffix("gastado", AffixKind.Armor, -Mathf.Max(1f, Mathf.Round(it.armor * 0.25f))));
                break;
            case LootQuality.Superior:
                if (weapon && it.damage > 0f) s.affixes.Add(new RolledAffix("superior", AffixKind.Damage, Mathf.Max(1f, it.damage * rng.Range(0.1f, 0.15f))));
                else if (it.armor > 0) s.affixes.Add(new RolledAffix("superior", AffixKind.Armor, Mathf.Max(1f, Mathf.Round(it.armor * rng.Range(0.1f, 0.15f)))));
                else s.quality = LootQuality.Normal;
                break;
            case LootQuality.Magic:
            {
                bool pre = rng.Chance(0.7f), suf = rng.Chance(0.7f);
                if (!pre && !suf) { if (rng.Chance(0.5f)) pre = true; else suf = true; }
                if (pre) AddAffix(s, true, ilvl, weapon, jewelry, rng);
                if (suf) AddAffix(s, false, ilvl, weapon, jewelry, rng);
                if (s.affixes.Count == 0) s.quality = LootQuality.Normal;
                break;
            }
            case LootQuality.Rare:
            {
                int total = rng.NextInt(3, 6);
                int pres = 0, sufs = 0;
                for (int i = 0; i < total * 3 && pres + sufs < total; i++)
                {
                    bool pre = rng.Chance(0.5f);
                    if (pre && pres >= 3) pre = false;
                    if (!pre && sufs >= 3) pre = true;
                    if (AddAffix(s, pre, ilvl, weapon, jewelry, rng)) { if (pre) pres++; else sufs++; }
                }
                s.rareName = rng.Pick(RareFirst) + " " + rng.Pick(RareSecond);
                if (s.affixes.Count == 0) s.quality = LootQuality.Normal;
                break;
            }
        }
    }

    private static bool AddAffix(ItemStack s, bool prefix, int ilvl, bool weapon, bool jewelry, DeterministicRng rng)
    {
        var pool = new List<AffixDef>();
        foreach (AffixDef a in Affixes)
        {
            if (a.prefix != prefix || a.minLevel > ilvl) continue;
            if (weapon ? !a.weapon : jewelry ? !a.jewelry : !a.armor) continue;
            bool taken = false;
            foreach (RolledAffix r in s.affixes) if (r.id == a.id) taken = true;
            if (!taken) pool.Add(a);
        }
        if (pool.Count == 0) return false;
        AffixDef d = rng.Pick(pool);
        float v = rng.Range(d.min, d.max) + d.perLevel * (ilvl - 1);
        if (d.kind == AffixKind.Strength || d.kind == AffixKind.Mana || d.kind == AffixKind.Agility || d.kind == AffixKind.Health
            || d.kind == AffixKind.Armor || d.kind == AffixKind.MagicFind)
            v = Mathf.Max(1f, Mathf.Round(v));
        s.affixes.Add(new RolledAffix(d.id, d.kind, v));
        return true;
    }

    public static string AffixLabel(string id)
    {
        foreach (AffixDef a in Affixes) if (a.id == id) return a.label;
        return "";
    }

    public static bool IsPrefix(string id)
    {
        foreach (AffixDef a in Affixes) if (a.id == id) return a.prefix;
        return false;
    }
}
