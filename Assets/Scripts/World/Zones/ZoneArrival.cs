using System.Collections.Generic;
using UnityEngine;

// Punto donde aparece el héroe al llegar desde otra zona.
public class ZoneArrival : MonoBehaviour
{
    public string id = "";

    private static readonly List<ZoneArrival> All = new List<ZoneArrival>();

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public static ZoneArrival Find(string id)
    {
        foreach (ZoneArrival a in All) if (a != null && a.id == id) return a;
        return null;
    }

    public static ZoneArrival Create(Transform parent, string id, Vector3 position, float yaw)
    {
        var go = new GameObject("Llegada_" + id);
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        ZoneArrival a = go.AddComponent<ZoneArrival>();
        a.id = id;
        return a;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();
}
