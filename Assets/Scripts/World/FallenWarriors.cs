using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Caballeros, magos y ninjas caídos: cuerpos que ya están en el suelo cuando llega el héroe (ragdoll que se queda
// congelado donde cayó). Cuentan lo que pasó en la montaña sin decir nada.
public class FallenWarriors : MonoBehaviour
{
    private static readonly string[] Kinds = { "Corrupt_Knight", "Corrupt_Mage", "Corrupt_Ninja" };
    private static readonly string[] Names = { "Caballero caído", "Mago caído", "Ninja caído" };

    public static FallenWarriors Scatter(Transform parent, int seed, IList<Vector3> spots)
    {
        var go = new GameObject("Guerreros_Caidos");
        go.transform.SetParent(parent, false);
        FallenWarriors f = go.AddComponent<FallenWarriors>();
        f._seed = seed;
        f._spots = new List<Vector3>(spots);
        return f;
    }

    private int _seed;
    private List<Vector3> _spots;
    private readonly List<PenguinRagdoll> _bodies = new List<PenguinRagdoll>();
    public int Count => _bodies.Count;

    IEnumerator Start()
    {
        var rng = new DeterministicRng(SeedUtil.Combine(_seed, SeedUtil.FromString("caidos")));
        for (int i = 0; i < _spots.Count; i++)
        {
            int kind = rng.NextInt(0, Kinds.Length);
            EnemyDefinition def = EnemySpawner.Find(Kinds[kind]);
            if (def == null) continue;
            Vector3 at = _spots[i];
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 3f, NavMesh.AllAreas)) at = hit.position;
            EnemyBrain brain = EnemySpawner.Spawn(def, at + Vector3.up * 0.3f, rng.Range(0f, 360f), transform, 1, false, rng);
            if (brain == null) continue;
            PenguinRagdoll body = Lay(brain, Names[kind], rng);
            if (body != null) _bodies.Add(body);
        }
        // Ya tendidos: se quedan fijos donde cayeron.
        float t = 0f;
        while (t < 5f) { t += Time.deltaTime; yield return null; }
        foreach (PenguinRagdoll b in _bodies) if (b != null) b.Freeze();
    }

    public static PenguinRagdoll Lay(EnemyBrain brain, string label, DeterministicRng rng) => Lay(brain, label, RandomDir(rng));

    private static Vector3 RandomDir(DeterministicRng rng)
    {
        float a = rng.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
    }

    // Tumba al pingüino: sin IA ni golpes, ragdoll empujado hacia dir.
    public static PenguinRagdoll Lay(EnemyBrain brain, string label, Vector3 dir)
    {
        GameObject go = brain.gameObject;
        go.name = label;
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            if (!(mb is PenguinRagdoll)) mb.enabled = false;
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        foreach (Canvas c in go.GetComponentsInChildren<Canvas>(true)) c.enabled = false;
        foreach (NameTag tag in go.GetComponentsInChildren<NameTag>(true)) tag.enabled = false;
        PenguinRagdoll ragdoll = go.GetComponent<PenguinRagdoll>();
        if (ragdoll == null) ragdoll = go.AddComponent<PenguinRagdoll>();
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        if (!ragdoll.Activate(dir * 1.5f, dir * 2.5f + Vector3.up * 0.5f)) return null;
        return ragdoll;
    }

    // Congela el cuerpo cuando ya se asentó: se queda donde cayó.
    public static IEnumerator FreezeWhenSettled(PenguinRagdoll body, float maxSeconds = 5f)
    {
        float t = 0f;
        while (body != null && t < maxSeconds && (t < 1.2f || !body.Sleeping)) { t += Time.deltaTime; yield return null; }
        if (body != null) body.Freeze();
    }
}
