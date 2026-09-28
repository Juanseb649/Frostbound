using UnityEngine;

public enum WeaponHandling { OneHanded, TwoHanded, Ranged }

public enum WeaponType
{
    None,
    // Dos manos
    Claymore, LongAxe, Spear, Mace, Staff,
    // Una mano
    Knife, Sword, Scimitar, Hammer, Nunchaku, Kunai, Katana, CelticSword, Sickle, Machete, Axe, Shuriken,
    // A distancia
    Bow, Longbow, Crossbow, ThrowingKunai
}

// Cómo sujeta el pingüino el arma (pose de las aletas y orientación del modelo).
public enum WeaponGrip { OneHand, TwoHand, Polearm, Bow, Crossbow, Throwing, Offhand }

public enum DamageType { Physical, Fire, Poison, Frost, Lightning }

public enum RuneEffect { None, Fire, Poison, Frost, Chain, Lifesteal, Sharpness, Swiftness, Knockback }

// Datos fijos de cada tipo de arma: manejo, pose, alcance y proyectil.
public static class WeaponCatalog
{
    public struct Info
    {
        public string name;
        public WeaponHandling handling;
        public WeaponGrip grip;
        public float range;
        public bool projectile;
    }

    public static Info Get(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Claymore: return I("Claymore", WeaponHandling.TwoHanded, WeaponGrip.TwoHand, 2.4f);
            case WeaponType.LongAxe: return I("Hacha larga", WeaponHandling.TwoHanded, WeaponGrip.TwoHand, 2.5f);
            case WeaponType.Spear: return I("Lanza", WeaponHandling.TwoHanded, WeaponGrip.Polearm, 3.0f);
            case WeaponType.Mace: return I("Mazo", WeaponHandling.TwoHanded, WeaponGrip.TwoHand, 2.2f);
            case WeaponType.Staff: return I("Báculo", WeaponHandling.TwoHanded, WeaponGrip.Polearm, 2.3f);
            case WeaponType.Knife: return I("Cuchillo", WeaponHandling.OneHanded, WeaponGrip.OneHand, 1.5f);
            case WeaponType.Sword: return I("Espada", WeaponHandling.OneHanded, WeaponGrip.OneHand, 2.0f);
            case WeaponType.Scimitar: return I("Cimitarra", WeaponHandling.OneHanded, WeaponGrip.OneHand, 2.0f);
            case WeaponType.Hammer: return I("Martillo", WeaponHandling.OneHanded, WeaponGrip.OneHand, 1.8f);
            case WeaponType.Nunchaku: return I("Nunchaku", WeaponHandling.OneHanded, WeaponGrip.OneHand, 1.7f);
            case WeaponType.Kunai: return I("Kunai", WeaponHandling.OneHanded, WeaponGrip.OneHand, 1.5f);
            case WeaponType.Katana: return I("Katana", WeaponHandling.OneHanded, WeaponGrip.OneHand, 2.1f);
            case WeaponType.CelticSword: return I("Espada celta", WeaponHandling.OneHanded, WeaponGrip.OneHand, 2.0f);
            case WeaponType.Sickle: return I("Hoz", WeaponHandling.OneHanded, WeaponGrip.OneHand, 1.6f);
            case WeaponType.Machete: return I("Machete", WeaponHandling.OneHanded, WeaponGrip.OneHand, 1.8f);
            case WeaponType.Axe: return I("Hacha", WeaponHandling.OneHanded, WeaponGrip.OneHand, 1.9f);
            case WeaponType.Shuriken: return I("Shuriken", WeaponHandling.OneHanded, WeaponGrip.Offhand, 1.5f);
            case WeaponType.Bow: return I("Arco", WeaponHandling.Ranged, WeaponGrip.Bow, 14f, true);
            case WeaponType.Longbow: return I("Arco largo", WeaponHandling.Ranged, WeaponGrip.Bow, 20f, true);
            case WeaponType.Crossbow: return I("Ballesta", WeaponHandling.Ranged, WeaponGrip.Crossbow, 18f, true);
            case WeaponType.ThrowingKunai: return I("Kunais arrojadizos", WeaponHandling.Ranged, WeaponGrip.Throwing, 11f, true);
            default: return I("", WeaponHandling.OneHanded, WeaponGrip.OneHand, 1.8f);
        }
    }

    private static Info I(string n, WeaponHandling h, WeaponGrip g, float r, bool p = false)
    {
        return new Info { name = n, handling = h, grip = g, range = r, projectile = p };
    }

    // Los arcos y la ballesta ocupan las dos aletas (como en Diablo II). Los kunais arrojadizos, una.
    public static bool UsesBothHands(WeaponType t)
    {
        Info i = Get(t);
        return i.handling == WeaponHandling.TwoHanded || t == WeaponType.Bow || t == WeaponType.Longbow || t == WeaponType.Crossbow;
    }

    public static string HandlingName(WeaponHandling h)
    {
        switch (h)
        {
            case WeaponHandling.TwoHanded: return "Dos manos";
            case WeaponHandling.Ranged: return "A distancia";
            default: return "Una mano";
        }
    }

    public static Color DamageColor(DamageType t)
    {
        switch (t)
        {
            case DamageType.Fire: return new Color(1f, 0.55f, 0.18f);
            case DamageType.Poison: return new Color(0.45f, 0.9f, 0.3f);
            case DamageType.Frost: return new Color(0.55f, 0.85f, 1f);
            case DamageType.Lightning: return new Color(1f, 0.92f, 0.35f);
            default: return new Color(0.96f, 0.96f, 0.96f);
        }
    }

    public static string RuneName(RuneEffect e)
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

    public static Color RuneColor(RuneEffect e)
    {
        switch (e)
        {
            case RuneEffect.Fire: return DamageColor(DamageType.Fire);
            case RuneEffect.Poison: return DamageColor(DamageType.Poison);
            case RuneEffect.Frost: return DamageColor(DamageType.Frost);
            case RuneEffect.Chain: return DamageColor(DamageType.Lightning);
            case RuneEffect.Lifesteal: return new Color(0.9f, 0.25f, 0.3f);
            case RuneEffect.Sharpness: return new Color(0.85f, 0.9f, 0.95f);
            case RuneEffect.Swiftness: return new Color(0.5f, 1f, 0.85f);
            case RuneEffect.Knockback: return new Color(0.75f, 0.6f, 1f);
            default: return Color.white;
        }
    }
}
