using System.Text;
using UnityEngine;
using UnityEngine.AI;

// Comprobaciones de la fuente del poblado (Fuente_Helada, montada por SetupFountain). Por el bridge en 01_Village:
// exec FountainChecks.Near (qué hay en la plaza) · exec FountainChecks.Blocking (colisión y NavMesh, en Play)
public static class FountainChecks
{
    // Todo lo que se dibuja a menos de 4.5 m del centro de la plaza, con su ruta y el centro de sus límites.
    public static string Near()
    {
        var sb = new StringBuilder();
        int n = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>())
        {
            Vector3 c = r.bounds.center;
            if (new Vector2(c.x, c.z).magnitude > 4.5f || c.y > 6f) continue;
            string path = r.name;
            for (Transform t = r.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            sb.Append(path).Append(" @").Append(c.ToString("F2")).Append(" r=").Append(r.bounds.extents.x.ToString("F2")).Append("; ");
            n++;
        }
        return n + " piezas: " + sb;
    }

    // La fuente sigue cortando el NavMesh y bloqueando al héroe como antes: el centro no es transitable y el anillo
    // de alrededor sí.
    public static string Blocking()
    {
        bool center = NavMesh.SamplePosition(Vector3.zero, out NavMeshHit h0, 0.5f, NavMesh.AllAreas);
        int around = 0, total = 0;
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI / 8f;
            Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.4f;
            total++;
            if (NavMesh.SamplePosition(p, out NavMeshHit h, 0.6f, NavMesh.AllAreas)) around++;
        }
        GameObject g = GameObject.Find("Fuente_Helada");
        Collider col = g != null ? g.GetComponent<Collider>() : null;
        return "centro transitable=" + center + " · anillo a 4.4 m transitable " + around + "/" + total
            + " · colisionador=" + (col != null ? col.GetType().Name + " r=" + ((CapsuleCollider)col).radius : "ninguno")
            + " · colisionadores en piezas=" + (g != null ? g.GetComponentsInChildren<Collider>().Length - (col != null ? 1 : 0) : -1);
    }

    // Para medir el coste de cada parte en Play (exec FountainChecks.X y luego UIKitChecks.PerfStart/PerfResult).
    public static string ProbeOff() => Toggle<ReflectionProbe>(false);
    public static string FountainOff()
    {
        GameObject g = GameObject.Find("Fuente_Helada");
        foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (Light l in g.GetComponentsInChildren<Light>()) l.enabled = false;
        return "fuente oculta";
    }

    private static string Toggle<T>(bool on) where T : Behaviour
    {
        GameObject g = GameObject.Find("Fuente_Helada");
        int n = 0;
        foreach (T c in g.GetComponentsInChildren<T>()) { c.enabled = on; n++; }
        return typeof(T).Name + " x" + n + " = " + on;
    }
}
