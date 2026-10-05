using System.Collections;
using UnityEngine;

// Muerte normal de un corrupto (sin runa de hielo): ragdoll. Cada hueso cae con física empujado por el golpe,
// el cuerpo queda tendido un momento y se hunde en la nieve.
public static class EnemyPhysicsDeath
{
    // keepBody: el cuerpo se queda en el suelo para siempre (jefes).
    public static void Play(EnemyBrain enemy, Vector3 hitDirection, bool keepBody = false)
    {
        enemy.StartCoroutine(Run(enemy, hitDirection, keepBody));
    }

    private static IEnumerator Run(EnemyBrain enemy, Vector3 dir, bool keepBody)
    {
        GameObject go = enemy.gameObject;
        Damageable d = go.GetComponent<Damageable>();
        if (d != null) d.enabled = false;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = -go.transform.forward;
        dir.Normalize();

        PenguinRagdoll ragdoll = go.GetComponent<PenguinRagdoll>();
        if (ragdoll == null) ragdoll = go.AddComponent<PenguinRagdoll>();
        ragdoll.Activate(dir * 1.2f + Vector3.up * 0.6f, dir * 3.5f + Vector3.up * 1.2f);

        // Tendido hasta que se asienta (máx. 4 s) y un momento más.
        float t = 0f;
        while (t < 4f && (t < 1f || !ragdoll.Sleeping))
        {
            t += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(1.5f);

        ragdoll.Freeze();
        if (keepBody)
        {
            go.name += " (cuerpo)";
            NameTag tag = go.GetComponent<NameTag>();
            if (tag != null) tag.enabled = false;
            yield break;
        }
        t = 0f;
        Vector3 start = go.transform.position;
        while (t < 1.4f)
        {
            t += Time.deltaTime;
            go.transform.position = start + Vector3.down * (t / 1.4f) * 0.9f;
            yield return null;
        }
        Object.Destroy(go);
    }
}
