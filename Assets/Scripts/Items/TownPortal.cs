using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pergamino de regreso: tras leerlo un instante, el héroe (y su compañero) vuelve a la plaza del poblado.
public class TownPortal : MonoBehaviour
{
    public const string ArrivalId = "pergamino_poblado";
    public const float CastSeconds = 1.4f;

    public static bool Casting { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Casting = false;

    public static bool InVillage => SceneManager.GetActiveScene().name == SceneIds.Village;

    // Lo llama Equipment.Use. El pergamino se gasta al terminar la lectura.
    public static bool TryRead(Equipment user, int inventoryIndex, out string message)
    {
        message = "";
        if (user == null) return false;
        if (InVillage) { message = "Ya estás en el poblado"; return false; }
        if (Casting || LoadingScreen.Active) { message = "Ya estás leyendo un pergamino"; return false; }
        CharacterStats stats = user.Stats;
        if (stats != null && stats.IsDead) return false;
        ItemStack s = user.Inventory.Get(inventoryIndex);
        if (s == null || !s.item.townPortal) return false;
        user.Inventory.RemoveAt(inventoryIndex, 1);
        var go = new GameObject("Portal_Poblado");
        go.transform.SetParent(user.transform, false);
        go.AddComponent<TownPortal>().StartCoroutine(go.GetComponent<TownPortal>().Read(user));
        return true;
    }

    private IEnumerator Read(Equipment user)
    {
        Casting = true;
        Notifications.Show("Lees el pergamino... el viento te llevará al poblado.", new Color(1f, 0.86f, 0.55f));
        var lightGo = new GameObject("Luz");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = Vector3.up * 0.8f;
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.85f, 0.55f);
        l.range = 4f;
        l.shadows = LightShadows.None;
        var ring = gameObject.AddComponent<LineRenderer>();
        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null) ring.sharedMaterial = new Material(sh);
        ring.loop = true;
        ring.positionCount = 32;
        ring.useWorldSpace = false;
        ring.widthMultiplier = 0.06f;
        ring.startColor = ring.endColor = new Color(1f, 0.85f, 0.55f, 0.9f);

        float t = 0f;
        while (t < CastSeconds)
        {
            t += Time.deltaTime;
            float k = t / CastSeconds;
            l.intensity = Mathf.Lerp(0.5f, 6f, k);
            float r = Mathf.Lerp(1.4f, 0.4f, k);
            for (int i = 0; i < 32; i++)
            {
                float a = i / 32f * Mathf.PI * 2f + t * 3f;
                ring.SetPosition(i, new Vector3(Mathf.Sin(a) * r, 0.05f + k * 1.6f, Mathf.Cos(a) * r));
            }
            CharacterStats st = user != null ? user.Stats : null;
            if (st == null || st.IsDead)
            {
                Casting = false;
                Destroy(gameObject);
                yield break;
            }
            yield return null;
        }
        Casting = false;
        ZoneTravel.Go(SceneIds.Village, ArrivalId);
        Destroy(gameObject);
    }
}
