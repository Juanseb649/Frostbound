using UnityEngine;
using UnityEngine.AI;

// Crea un enemigo fuera de un campamento (oleadas, interior del castillo, jefes).
public static class EnemySpawner
{
    public static EnemyBrain Spawn(EnemyDefinition def, Vector3 position, float yaw, Transform parent, int level, bool champion, DeterministicRng rng)
    {
        if (def == null || def.prefab == null) return null;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) position = hit.position;
        GameObject go = Object.Instantiate(def.prefab, position, Quaternion.Euler(0f, yaw, 0f), parent);
        go.name = def.displayName;
        EnemyBrain brain = go.GetComponent<EnemyBrain>();
        PlumagePalette palette = GameDatabase.Instance != null ? GameDatabase.Instance.palette : null;
        brain.Setup(def, null, level, champion, EnemyCamp.CorruptPlumage(palette, rng));
        return brain;
    }

    public static EnemyDefinition Find(string id) => GameDatabase.Instance != null ? GameDatabase.Instance.FindEnemy(id) : null;

    // Nivel de monstruo según el héroe, para que las oleadas no se queden atrás.
    public static int LevelForPlayer(int bonus = 0)
    {
        GameObject p = GameObject.Find("Player");
        CharacterStats s = p != null ? p.GetComponent<CharacterStats>() : null;
        return Mathf.Max(1, (s != null ? s.level : 1) + bonus);
    }
}
