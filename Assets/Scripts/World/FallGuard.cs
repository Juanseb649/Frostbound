using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Red de seguridad: recuerda el último punto firme (sobre el NavMesh) de cada héroe y, si cae por un hueco o se sale
// del mundo, lo devuelve ahí en vez de dejarlo cayendo al vacío.
public class FallGuard : MonoBehaviour
{
    public const float FallDepth = 9f;   // metros por debajo del último punto firme

    private class Track { public Vector3 safe; public bool has; public float nextSample; }
    private readonly Dictionary<PlayerController, Track> _tracks = new Dictionary<PlayerController, Track>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Ensure();
    }

    private static void OnLoaded(Scene s, LoadSceneMode m) => Ensure();

    private static void Ensure()
    {
        if (!SceneIds.IsGameplay(SceneManager.GetActiveScene().name)) return;
        if (FindAnyObjectByType<FallGuard>() != null) return;
        new GameObject("RedDeSeguridad").AddComponent<FallGuard>();
    }

    void Update()
    {
        foreach (PlayerController pc in Players.All)
        {
            if (pc == null) continue;
            if (!_tracks.TryGetValue(pc, out Track t)) _tracks[pc] = t = new Track();
            Vector3 p = pc.transform.position;
            if (Time.time >= t.nextSample)
            {
                t.nextSample = Time.time + 0.4f;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 0.8f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - p.y) < 0.8f)
                {
                    t.safe = hit.position;
                    t.has = true;
                }
            }
            if (t.has && p.y < t.safe.y - FallDepth) Rescue(pc, t.safe);
            else if (!t.has && p.y < -30f) Rescue(pc, Vector3.zero + Vector3.up);
        }
    }

    public static void Rescue(PlayerController pc, Vector3 at)
    {
        Vector3 target = at + Vector3.up * 0.15f;
        Quaternion rot = pc.transform.rotation;
        PlayerPersistence persist = pc.GetComponent<PlayerPersistence>();
        if (persist != null) persist.MoveTo(target, rot);
        else pc.transform.position = target;
        pc.MoveTo(target);
        Notifications.Show("Te salvaste por los pelos…", FrostboundUI.Gold);
    }
}
