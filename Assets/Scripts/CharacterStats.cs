using UnityEngine;

public enum StatType { Strength, Mana, Agility, Health }

public class CharacterStats : MonoBehaviour
{
    [Header("Clase")]
    [Tooltip("Arrastra aquí el ScriptableObject de la clase (Warrior, Vikingo, Ninja, Mago...).")]
    public CharacterClass characterClass;

    [Header("Progresión")]
    public int level = 1;
    public float experience;
    [Tooltip("Puntos disponibles para repartir al subir de nivel.")]
    public int pointsAvailable = 0;

    [Header("Stats actuales (base + puntos repartidos)")]
    public int strength;
    public int mana;
    public int agility;
    public int health;

    [Header("Estado actual")]
    public float currentHealth;
    public float currentMana;

    [Header("Configuración")]
    [Tooltip("Vida máxima extra por cada punto de Salud.")]
    public float healthPerPoint = 8f;
    [Tooltip("Mana máximo extra por cada punto de Mana.")]
    public float manaPerPoint = 5f;
    [Tooltip("Velocidad extra (m/s) por cada punto de Agilidad.")]
    public float agilitySpeedPerPoint = 0.35f;
    [Tooltip("Daño extra por cada punto de Fuerza.")]
    public float strengthDamagePerPoint = 2f;
    [Tooltip("Vida base de la que parten todas las clases.")]
    public float baseMaxHealth = 40f;
    [Tooltip("Mana base del que parten todas las clases.")]
    public float baseMaxMana = 20f;

    void Awake()
    {
        if (characterClass != null) InitFromClass(characterClass);
    }

    public void InitFromClass(CharacterClass cls)
    {
        characterClass = cls;
        level = 1;
        experience = 0f;
        pointsAvailable = 0;
        strength = cls.baseStrength;
        mana = cls.baseMana;
        agility = cls.baseAgility;
        health = cls.baseHealth;
        currentHealth = MaxHealth;
        currentMana = MaxMana;
    }

    // ----- Derivados -----

    public float MaxHealth => baseMaxHealth + health * healthPerPoint;
    public float MaxMana => baseMaxMana + mana * manaPerPoint;
    public float MoveSpeed => (characterClass != null ? characterClass.baseMoveSpeed : 6f) + agility * agilitySpeedPerPoint;
    public float Damage => (characterClass != null ? characterClass.baseDamage : 10f) + strength * strengthDamagePerPoint;
    public float ManaRegenPerSecond => 0.5f + mana * 0.15f;

    // ----- Puntos de clase -----

    public void AddPoint(StatType type)
    {
        if (pointsAvailable <= 0) return;
        pointsAvailable--;

        switch (type)
        {
            case StatType.Strength:
                strength++;
                break;
            case StatType.Mana:
                mana++;
                // Al subir el mana también sube el mana máximo (y se rellena esa parte).
                currentMana = Mathf.Min(MaxMana, currentMana + manaPerPoint);
                break;
            case StatType.Agility:
                agility++;
                break;
            case StatType.Health:
                health++;
                // Al subir la salud también sube la vida máxima.
                currentHealth = Mathf.Min(MaxHealth, currentHealth + healthPerPoint);
                break;
        }
    }

    // ----- Nivel -----

    public float ExperienceToNextLevel() => level * 100f;

    public void AddExperience(float amount)
    {
        experience += amount;
        while (experience >= ExperienceToNextLevel())
        {
            experience -= ExperienceToNextLevel();
            LevelUp();
        }
    }

    void LevelUp()
    {
        level++;
        pointsAvailable += 5;
        currentHealth = MaxHealth;
        currentMana = MaxMana;
        Debug.Log("¡Subiste de nivel! Nivel " + level + " — ganaste 5 puntos para repartir.");
    }

    // ----- Daño -----

    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f) Die();
    }

    void Die()
    {
        string name = characterClass != null ? characterClass.className : "Pingüino";
        Debug.Log(name + " ha caído. (Aquí irá la lógica de muerte / respawn).");
    }
}
