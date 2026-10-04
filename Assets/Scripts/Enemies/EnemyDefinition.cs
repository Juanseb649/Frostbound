using UnityEngine;

// Datos de un tipo de enemigo: modelo, estadísticas a nivel 1, comportamiento y botín.
[CreateAssetMenu(menuName = "Frostbound/Enemigo", fileName = "NuevoEnemigo")]
public class EnemyDefinition : ScriptableObject
{
    [Header("Info")]
    public string id = "";
    public string displayName = "Pingüino corrupto por el Frost";
    [Tooltip("Prefab con el pingüino, el equipo corrupto, NavMeshAgent, Damageable y EnemyBrain.")]
    public GameObject prefab;
    [Tooltip("Equipo corrupto (prenda con esqueleto) que se pone al aparecer.")]
    public OutfitItem gear;
    [Tooltip("Élite: equipo de una clase inicial corrompida.")]
    public bool elite;

    [Header("Estadísticas a nivel de monstruo 1")]
    public float maxHealth = 45f;
    [Tooltip("Daño por golpe (mín., máx.) antes de la armadura del jugador.")]
    public Vector2 damage = new Vector2(3f, 5f);
    public float moveSpeed = 3.6f;
    [Tooltip("XP que da al morir.")]
    public int experience = 9;

    [Header("Ataque")]
    public bool ranged;
    public float attackRange = 1.6f;
    [Tooltip("Segundos de aviso antes de que el golpe impacte.")]
    public float windup = 0.4f;
    public float cooldown = 1.4f;
    [Tooltip("Golpes por ataque (el ninja corrupto golpea dos veces).")]
    [Min(1)] public int hitsPerAttack = 1;
    [Tooltip("Radio del golpe en área (0 = solo al jugador delante).")]
    public float areaRadius;

    [Header("Distancia (solo a distancia)")]
    public float projectileSpeed = 14f;
    public Material projectileMaterial;
    public Vector2 preferredDistance = new Vector2(6f, 9f);

    [Header("Percepción")]
    public float sightRange = 10f;

    [Header("Botín")]
    [Tooltip("Tiradas de botín al morir.")]
    [Min(1)] public int lootPicks = 1;

    // Escalado por nivel de monstruo (spec §4.1).
    public float HealthAt(int level) => maxHealth * (1f + 0.18f * (level - 1));
    public float DamageScaleAt(int level) => 1f + 0.12f * (level - 1);
    public int ExperienceAt(int level) => Mathf.RoundToInt(experience * (1f + 0.15f * (level - 1)));

    void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
    }
}
