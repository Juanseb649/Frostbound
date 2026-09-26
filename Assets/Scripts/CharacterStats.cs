using System;
using UnityEngine;

public enum StatType { Strength, Mana, Agility, Health }

[Serializable]
public struct StatBlock
{
    public int strength;
    public int mana;
    public int agility;
    public int health;

    public static readonly StatType[] All = { StatType.Strength, StatType.Mana, StatType.Agility, StatType.Health };

    public int Get(StatType type)
    {
        switch (type)
        {
            case StatType.Strength: return strength;
            case StatType.Mana: return mana;
            case StatType.Agility: return agility;
            default: return health;
        }
    }

    public void Add(StatType type, int value)
    {
        switch (type)
        {
            case StatType.Strength: strength += value; break;
            case StatType.Mana: mana += value; break;
            case StatType.Agility: agility += value; break;
            default: health += value; break;
        }
    }

    public int Total => strength + mana + agility + health;

    public static StatBlock operator +(StatBlock a, StatBlock b)
    {
        return new StatBlock
        {
            strength = a.strength + b.strength,
            mana = a.mana + b.mana,
            agility = a.agility + b.agility,
            health = a.health + b.health
        };
    }

    public static string DisplayName(StatType type)
    {
        switch (type)
        {
            case StatType.Strength: return "Fuerza";
            case StatType.Mana: return "Maná";
            case StatType.Agility: return "Agilidad";
            default: return "Salud";
        }
    }
}

public class CharacterStats : MonoBehaviour
{
    [Header("Clase")]
    [Tooltip("ScriptableObject de la clase (Knight, Mage, Ninja, Viking).")]
    public CharacterClass characterClass;

    [Header("Progresión")]
    [Min(1)] public int level = 1;
    [Min(0)] public int experience;
    [Tooltip("Puntos sin repartir.")]
    [Min(0)] public int pointsAvailable;
    [Tooltip("Puntos de atributo que da cada nivel.")]
    [Min(0)] public int pointsPerLevel = 5;
    [Min(1)] public int maxLevel = 50;
    [Tooltip("XP para pasar del nivel 1 al 2.")]
    [Min(1)] public int xpBase = 100;
    [Tooltip("Curva: XP necesaria = xpBase × nivel ^ xpGrowth.")]
    [Range(1f, 3f)] public float xpGrowth = 1.5f;

    [Header("Atributos")]
    [Tooltip("Valores de la clase.")]
    public StatBlock baseStats;
    [Tooltip("Puntos que el jugador repartió al subir de nivel.")]
    public StatBlock allocated;

    [Header("Estado actual")]
    public float currentHealth;
    public float currentMana;

    [Header("Configuración")]
    [Tooltip("Vida máxima extra por cada punto de Salud.")]
    public float healthPerPoint = 8f;
    [Tooltip("Maná máximo extra por cada punto de Maná.")]
    public float manaPerPoint = 5f;
    [Tooltip("Velocidad extra (m/s) por cada punto de Agilidad.")]
    public float agilitySpeedPerPoint = 0.35f;
    [Tooltip("Daño extra por cada punto de Fuerza.")]
    public float strengthDamagePerPoint = 2f;
    [Tooltip("Vida base de la que parten todas las clases.")]
    public float baseMaxHealth = 40f;
    [Tooltip("Maná base del que parten todas las clases.")]
    public float baseMaxMana = 20f;
    [Tooltip("Armadura con la que se reduce la mitad del daño.")]
    public float armorForHalfDamage = 60f;

    public StatBlock EquipmentBonus { get; private set; }
    public int Armor { get; private set; }
    public float WeaponDamage { get; private set; }
    public bool IsDead => currentHealth <= 0f;

    [NonSerialized] public bool invulnerable;

    public event Action StatsChanged;
    public event Action<int> LeveledUp;
    public event Action<int> ExperienceGained;
    public event Action Died;

    void Awake()
    {
        if (characterClass != null) InitFromClass(characterClass);
    }

    void Update()
    {
        if (IsDead) return;
        if (currentMana < MaxMana) currentMana = Mathf.Min(MaxMana, currentMana + ManaRegenPerSecond * Time.deltaTime);
    }

    public void InitFromClass(CharacterClass cls)
    {
        characterClass = cls;
        level = 1;
        experience = 0;
        pointsAvailable = 0;
        baseStats = new StatBlock
        {
            strength = cls.baseStrength,
            mana = cls.baseMana,
            agility = cls.baseAgility,
            health = cls.baseHealth
        };
        allocated = default;
        currentHealth = MaxHealth;
        currentMana = MaxMana;
        Notify();
    }

    // ----- Atributos -----

    public int Get(StatType type) => baseStats.Get(type) + allocated.Get(type) + EquipmentBonus.Get(type);
    public int GetWithoutEquipment(StatType type) => baseStats.Get(type) + allocated.Get(type);

    public int Strength => Get(StatType.Strength);
    public int Mana => Get(StatType.Mana);
    public int Agility => Get(StatType.Agility);
    public int Health => Get(StatType.Health);

    public float MaxHealth => baseMaxHealth + Health * healthPerPoint;
    public float MaxMana => baseMaxMana + Mana * manaPerPoint;
    public float MoveSpeed => (characterClass != null ? characterClass.baseMoveSpeed : 6f) + Agility * agilitySpeedPerPoint;
    public float Damage => (characterClass != null ? characterClass.baseDamage : 10f) + WeaponDamage + Strength * strengthDamagePerPoint;
    public float ManaRegenPerSecond => 0.5f + Mana * 0.15f;
    public float DamageReduction => Armor / (Armor + Mathf.Max(1f, armorForHalfDamage));

    public string EffectDescription(StatType type)
    {
        switch (type)
        {
            case StatType.Strength: return "+" + strengthDamagePerPoint.ToString("0.#") + " de daño por punto";
            case StatType.Mana: return "+" + manaPerPoint.ToString("0.#") + " de maná y más regeneración";
            case StatType.Agility: return "+" + agilitySpeedPerPoint.ToString("0.##") + " m/s por punto";
            default: return "+" + healthPerPoint.ToString("0.#") + " de vida por punto";
        }
    }

    // ----- Puntos -----

    public bool AddPoint(StatType type) => AddPoints(type, 1) > 0;

    public int AddPoints(StatType type, int amount)
    {
        int n = Mathf.Min(amount, pointsAvailable);
        if (n <= 0) return 0;
        float oldHealth = MaxHealth;
        float oldMana = MaxMana;
        StatBlock a = allocated;
        a.Add(type, n);
        allocated = a;
        pointsAvailable -= n;
        currentHealth = Mathf.Min(MaxHealth, currentHealth + (MaxHealth - oldHealth));
        currentMana = Mathf.Min(MaxMana, currentMana + (MaxMana - oldMana));
        Notify();
        return n;
    }

    public void ResetAllocatedPoints()
    {
        pointsAvailable += allocated.Total;
        allocated = default;
        ClampCurrent();
        Notify();
    }

    // ----- Experiencia -----

    public bool IsMaxLevel => level >= maxLevel;

    public int ExperienceToNextLevel() => IsMaxLevel ? 0 : Mathf.RoundToInt(xpBase * Mathf.Pow(level, xpGrowth));

    public float LevelProgress => IsMaxLevel ? 1f : experience / (float)Mathf.Max(1, ExperienceToNextLevel());

    public void AddExperience(int amount)
    {
        if (amount <= 0 || IsMaxLevel) return;
        experience += amount;
        ExperienceGained?.Invoke(amount);
        while (!IsMaxLevel && experience >= ExperienceToNextLevel())
        {
            experience -= ExperienceToNextLevel();
            LevelUp();
        }
        if (IsMaxLevel) experience = 0;
        Notify();
    }

    void LevelUp()
    {
        level++;
        pointsAvailable += pointsPerLevel;
        currentHealth = MaxHealth;
        currentMana = MaxMana;
        Debug.Log("[Progresión] Nivel " + level + ": +" + pointsPerLevel + " puntos para repartir.");
        LeveledUp?.Invoke(level);
    }

    // ----- Equipo -----

    public void SetEquipmentBonus(StatBlock bonus, int armor, float weaponDamage)
    {
        float healthRatio = MaxHealth > 0f ? currentHealth / MaxHealth : 1f;
        float manaRatio = MaxMana > 0f ? currentMana / MaxMana : 1f;
        EquipmentBonus = bonus;
        Armor = armor;
        WeaponDamage = weaponDamage;
        currentHealth = MaxHealth * healthRatio;
        currentMana = MaxMana * manaRatio;
        Notify();
    }

    // ----- Vida y maná -----

    public void Heal(float amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
        Notify();
    }

    public void RestoreMana(float amount)
    {
        currentMana = Mathf.Min(MaxMana, currentMana + amount);
        Notify();
    }

    public bool SpendMana(float amount)
    {
        if (currentMana < amount) return false;
        currentMana -= amount;
        return true;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || invulnerable) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount * (1f - DamageReduction));
        Notify();
        if (currentHealth <= 0f) Die();
    }

    void Die()
    {
        string heroName = characterClass != null ? characterClass.className : "Pingüino";
        Debug.Log("[Progresión] " + heroName + " ha caído.");
        Died?.Invoke();
    }

    void ClampCurrent()
    {
        currentHealth = Mathf.Min(currentHealth, MaxHealth);
        currentMana = Mathf.Min(currentMana, MaxMana);
    }

    void Notify() => StatsChanged?.Invoke();
}
