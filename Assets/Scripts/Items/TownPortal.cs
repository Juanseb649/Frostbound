using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pergamino de regreso al estilo Diablo II: tras leerlo un instante se abre un portal azul donde está el héroe.
// El portal sigue abierto en el exterior (y se guarda en la partida); su pareja aparece en un punto fijo de la plaza
// del poblado. Cruzar el del exterior lleva al poblado; cruzar el del poblado devuelve al sitio exacto donde se abrió
// y cierra los dos. Leer otro pergamino en otro sitio reemplaza el portal anterior.
public class TownPortal : MonoBehaviour
{
    public const string ArrivalId = "pergamino_poblado";
    public const string FieldArrivalId = "portal_exterior";
    public const float CastSeconds = 1.4f;

    public static bool Casting { get; private set; }

    // Vuelta pendiente por el portal del poblado: escena y punto donde aparece el héroe.
    private static string _returnScene = "";
    private static Vector3 _returnPos;
    private static float _returnYaw;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Casting = false;
        _returnScene = "";
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static SaveData Save => GameSession.Instance != null ? GameSession.Instance.Current : null;

    // Hay un portal abierto en alguna zona del exterior.
    public static bool IsOpen => Save != null && SceneIds.IsGameplay(Save.portalZone) && Save.portalZone != SceneIds.Village;

    public static string OpenZone => IsOpen ? Save.portalZone : "";

    // Al cargar una zona: crea el portal que sigue abierto, o el punto de llegada si se vuelve por el portal.
    // sceneLoaded llega antes de los Start, así que PlayerPersistence ya encuentra la llegada.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (!string.IsNullOrEmpty(_returnScene))
        {
            if (scene.name == _returnScene)
                ZoneArrival.Create(null, FieldArrivalId, _returnPos, _returnYaw);
            _returnScene = "";
        }
        if (!IsOpen) return;
        SaveData d = Save;
        if (scene.name == d.portalZone)
            PortalGate.Create(new Vector3(d.portalX, d.portalY, d.portalZ), d.portalYaw, false);
        else if (scene.name == SceneIds.Village)
            new GameObject("Portal_Plaza").AddComponent<VillagePortalAnchor>();
    }

    public static bool InVillage => SceneManager.GetActiveScene().name == SceneIds.Village;

    // Lo llama Equipment.Use. El pergamino se gasta al terminar la lectura.
    public static bool TryRead(Equipment user, int inventoryIndex, out string message)
    {
        message = "";
        if (user == null) return false;
        if (InVillage) { message = "Ya estás en el poblado"; return false; }
        if (Casting || LoadingScreen.Active) { message = "Ya estás leyendo un pergamino"; return false; }
        // El interior del castillo se construye al entrar: un portal ahí no tendría suelo al volver.
        if (CastleInterior.Instance != null && CastleInterior.Instance.PlayerInside) { message = "El portal no se abre bajo estas bóvedas"; return false; }
        CharacterStats stats = user.Stats;
        if (stats != null && stats.IsDead) return false;
        ItemStack s = user.Inventory.Get(inventoryIndex);
        if (s == null || !s.item.townPortal) return false;
        user.Inventory.RemoveAt(inventoryIndex, 1);
        var go = new GameObject("Lectura_Pergamino");
        go.transform.SetParent(user.transform, false);
        go.AddComponent<TownPortal>().StartCoroutine(go.GetComponent<TownPortal>().Read(user));
        return true;
    }

    private IEnumerator Read(Equipment user)
    {
        Casting = true;
        AudioManager.Play3D("SFX/Portal_Open", transform.position, 1f, 0f, 3f, 30f, 0f);
        Notifications.Show("Lees el pergamino... el aire se rasga.", PortalGate.Blue);
        var lightGo = new GameObject("Luz");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = Vector3.up * 0.8f;
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = PortalGate.Blue;
        l.range = 4f;
        l.shadows = LightShadows.None;
        var ring = gameObject.AddComponent<LineRenderer>();
        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null) ring.sharedMaterial = new Material(sh);
        ring.loop = true;
        ring.positionCount = 32;
        ring.useWorldSpace = false;
        ring.widthMultiplier = 0.06f;
        ring.startColor = ring.endColor = new Color(0.6f, 0.82f, 1f, 0.9f);

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
        if (user != null) Open(user.transform);
        Destroy(gameObject);
    }

    // Abre el portal delante del héroe, mirándolo, y lo apunta en la partida.
    private static void Open(Transform hero)
    {
        Vector3 fwd = hero.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 pos = hero.position + fwd * 1.8f;
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) pos = hit.position;
        float yaw = Quaternion.LookRotation(-fwd).eulerAngles.y;

        PortalGate.CloseAll();
        PortalGate.Create(pos, yaw, false);
        AudioManager.Play3D("SFX/Portal_Open", pos, 1f, 0f, 3f, 30f, 0f);
        Notifications.Show("Se abre un portal al poblado.", PortalGate.Blue);

        SaveData d = Save;
        if (d == null) return;
        d.portalZone = SceneManager.GetActiveScene().name;
        d.portalX = pos.x;
        d.portalY = pos.y;
        d.portalZ = pos.z;
        d.portalYaw = yaw;
        GameSession.Instance.SaveNow();
    }

    // Cruza un portal: desde el exterior al poblado; desde el poblado, de vuelta al sitio donde se abrió (y se cierra).
    public static void Enter(PortalGate gate)
    {
        if (LoadingScreen.Active) return;
        if (!gate.InVillage)
        {
            ZoneTravel.Go(SceneIds.Village, ArrivalId);
            return;
        }
        SaveData d = Save;
        if (!IsOpen || !Application.CanStreamedLevelBeLoaded(d.portalZone)) return;
        Quaternion rot = Quaternion.Euler(0f, d.portalYaw, 0f);
        _returnScene = d.portalZone;
        _returnPos = new Vector3(d.portalX, d.portalY, d.portalZ) + rot * Vector3.forward * 1.6f;
        _returnYaw = d.portalYaw;
        d.portalZone = "";
        ZoneTravel.Go(_returnScene, FieldArrivalId);
    }
}

// Pone el portal del poblado en la plaza, detrás del punto donde aparece el héroe al llegar por el portal.
// Espera a que el generador del poblado cree ese punto.
public class VillagePortalAnchor : MonoBehaviour
{
    private float _giveUpAt;

    void Start() => _giveUpAt = Time.time + 10f;

    void Update()
    {
        ZoneArrival a = ZoneArrival.Find(TownPortal.ArrivalId);
        if (a != null)
        {
            Transform t = a.transform;
            Vector3 pos = t.position - t.forward * 2.6f;
            PortalGate.Create(pos, t.eulerAngles.y, true);
            Destroy(gameObject);
        }
        else if (Time.time > _giveUpAt) Destroy(gameObject);
    }
}
