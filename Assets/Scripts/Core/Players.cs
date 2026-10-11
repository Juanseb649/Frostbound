using System.Collections.Generic;
using UnityEngine;

// Registro de los héroes en la escena (co-op local): el jugador 1 y, si se unió con el mando, el jugador 2.
// Sustituye a GameObject.Find("Player"): los enemigos, el botín y la cámara preguntan aquí por el héroe más cercano.
public static class Players
{
    private static readonly List<PlayerController> _all = new List<PlayerController>();

    public static IReadOnlyList<PlayerController> All => _all;
    public static int Count => _all.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _all.Clear();

    public static void Register(PlayerController p)
    {
        if (p != null && !_all.Contains(p)) _all.Add(p);
        _all.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
    }

    public static void Unregister(PlayerController p) => _all.Remove(p);

    // El jugador 1 (o el primero que quede).
    public static PlayerController Primary => _all.Count > 0 ? _all[0] : null;
    public static GameObject PrimaryObject
    {
        get
        {
            PlayerController p = Primary;
            if (p != null) return p.gameObject;
            return GameObject.Find("Player");
        }
    }

    public static PlayerController Get(int index)
    {
        foreach (PlayerController p in _all) if (p != null && p.playerIndex == index) return p;
        return null;
    }

    public static bool IsAlive(PlayerController p)
    {
        if (p == null) return false;
        CharacterStats s = p.Stats;
        return s == null || !s.IsDead;
    }

    public static bool AnyAlive
    {
        get
        {
            foreach (PlayerController p in _all) if (IsAlive(p)) return true;
            return false;
        }
    }

    // El héroe más cercano a un punto (en el plano). aliveOnly: ignora a los caídos.
    public static PlayerController Nearest(Vector3 position, bool aliveOnly = true)
    {
        PlayerController best = null;
        float bestD = float.MaxValue;
        foreach (PlayerController p in _all)
        {
            if (p == null || (aliveOnly && !IsAlive(p))) continue;
            Vector3 d = p.transform.position - position;
            d.y = 0f;
            float m = d.sqrMagnitude;
            if (m < bestD) { bestD = m; best = p; }
        }
        if (best == null && !aliveOnly) return Primary;
        return best;
    }

    public static Transform NearestTransform(Vector3 position, bool aliveOnly = true)
    {
        PlayerController p = Nearest(position, aliveOnly);
        if (p != null) return p.transform;
        GameObject g = aliveOnly ? null : GameObject.Find("Player");
        return g != null ? g.transform : null;
    }

    public static bool IsPlayer(Component c) => c != null && c.GetComponentInParent<PlayerController>() != null;
}
