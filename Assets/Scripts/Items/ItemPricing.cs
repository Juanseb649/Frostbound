using UnityEngine;

// Precios de la tienda de Tico. Vender da monedas según el arma (daño y nivel), la armadura, la calidad y las runas;
// comprar cuesta bastante más.
public static class ItemPricing
{
    public const float BuyMarkup = 4f;

    public static int SellPrice(ItemStack s)
    {
        if (s == null || s.item == null) return 0;
        ItemDefinition it = s.item;
        float v;
        if (it.IsWeapon) v = 4f + s.BaseDamage * 2.2f;
        else if (it.IsRune) v = 30f;
        else if (it.IsConsumable) v = 3f + it.heal * 0.12f + it.restoreMana * 0.12f;
        else if (it.IsEquippable) v = 6f + it.armor * 3f + it.StatBonus.Total * 4f;
        else v = 2f;

        v *= QualityFactor(s.quality, it.rarity);
        v += s.runes != null ? s.runes.Count * 15f : 0f;
        foreach (RolledAffix a in s.affixes) v += Mathf.Abs(a.value) * 0.5f;
        if (it.HasDurability) v *= Mathf.Lerp(0.25f, 1f, s.Durability01);
        return Mathf.Max(1, Mathf.RoundToInt(v)) * Mathf.Max(1, s.quantity);
    }

    public static int BuyPrice(ItemStack s)
    {
        if (s == null || s.item == null) return 0;
        int one = SellPrice(new ItemStack(s.item, 1) { itemLevel = s.itemLevel, quality = s.quality, affixes = s.affixes, rareName = s.rareName });
        return Mathf.Max(2, Mathf.RoundToInt(one * BuyMarkup));
    }

    private static float QualityFactor(LootQuality q, ItemRarity r)
    {
        switch (q)
        {
            case LootQuality.Worn: return 0.5f;
            case LootQuality.Superior: return 1.25f;
            case LootQuality.Magic: return 1.6f;
            case LootQuality.Rare: return 2.6f;
            case LootQuality.Set: return 4f;
            case LootQuality.Unique: return 5f;
        }
        switch (r)
        {
            case ItemRarity.Uncommon: return 1.2f;
            case ItemRarity.Rare: return 1.6f;
            case ItemRarity.Epic: return 2.2f;
            default: return 1f;
        }
    }
}
