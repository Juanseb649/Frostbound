using System.Text;
using UnityEngine;
using UnityEngine.AI;

// Comprobaciones en Play del poblado (01_Village): cierre hasta hablar con Steve, camino a la Ciudadela Caída,
// encuentros por el camino y capturas fijas para comparar antes/después. Por el bridge: exec VillageChecks.X
public static class VillageChecks
{
    private static VillageLayout.Result Layout => Object.FindAnyObjectByType<VillageWorldGenerator>()?.Layout;

    private static void Teleport(Vector3 p, float yaw = 0f)
    {
        PlayerPersistence mover = Object.FindAnyObjectByType<PlayerPersistence>();
        if (mover != null) mover.MoveTo(p, Quaternion.Euler(0f, yaw, 0f));
        else if (Players.PrimaryObject != null) Players.PrimaryObject.transform.position = p;
    }

    private static Vector3 Hero => Players.PrimaryObject != null ? Players.PrimaryObject.transform.position : Vector3.zero;

    // ---------- Capturas fijas (para comparar materiales e iluminación) ----------

    public static string ShotsBefore() => Shots("v_antes");
    public static string ShotsAfter() => Shots("v_despues");

    private static string Shots(string prefix)
    {
        var L = Layout;
        if (L == null) return "sin poblado";
        var c = L.castle;
        var sb = new StringBuilder();
        sb.Append(FrostboundBridge.CamShot(prefix + "_plaza", new Vector3(0f, 12f, -24f), new Vector3(0f, 1f, 6f), 50f)).Append(" | ");
        sb.Append(FrostboundBridge.CamShot(prefix + "_casas", new Vector3(-6f, 3.5f, -8f), new Vector3(-16f, 2.5f, 10f), 55f)).Append(" | ");
        sb.Append(FrostboundBridge.CamShot(prefix + "_puerta_norte", new Vector3(3f, 5f, 18f), new Vector3(0f, 3f, 34f), 50f)).Append(" | ");
        sb.Append(FrostboundBridge.CamShot(prefix + "_steve", VillageLayout.SteveClearing + new Vector3(8f, 7f, 10f), VillageLayout.SteveClearing, 50f)).Append(" | ");
        Vector3 toVillage = (Vector3.zero - c.Gate);
        toVillage.y = 0f;
        toVillage.Normalize();
        Vector3 eye = c.Gate + toVillage * 45f + Vector3.up * 14f;
        sb.Append(FrostboundBridge.CamShot(prefix + "_ciudadela", eye, c.TownCenter + Vector3.up * 8f, 55f));
        return sb.ToString();
    }

    // Vista de juego (cámara del héroe, con su posprocesado): el héroe en la plaza y ante la ciudadela.
    public static string HeroAtPlaza() { Teleport(new Vector3(0f, 0.1f, -6f)); return "héroe en la plaza"; }

    public static string HeroAtCitadel()
    {
        var c = Layout.castle;
        Vector3 toVillage = (Vector3.zero - c.Gate);
        toVillage.y = 0f;
        Teleport(c.Gate + toVillage.normalized * 12f + Vector3.up * 0.1f);
        return "héroe ante la ciudadela " + Hero.ToString("0");
    }

    // ---------- Cierre del poblado ----------

    // Partida recién empezada (en memoria): todas las misiones a 0 y el héroe en la plaza.
    public static string FreshStart()
    {
        foreach (QuestDefinition q in QuestLog.All) QuestLog.SetStage(q.id, 0);
        Teleport(new Vector3(0f, 0.1f, -6f));
        if (Players.Primary != null && Players.Primary.Stats != null) Players.Primary.Stats.invulnerable = true;
        return LockState();
    }

    public static string LockState()
    {
        VillageLock l = VillageLock.Instance;
        if (l == null) return "sin cierre";
        var path = new NavMeshPath();
        NavMesh.SamplePosition(new Vector3(0f, 0f, -6f), out NavMeshHit a, 3f, NavMesh.AllAreas);
        NavMesh.SamplePosition(new Vector3(0f, 0f, 46f), out NavMeshHit b, 3f, NavMesh.AllAreas);
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        return "abierto=" + VillageLock.Open + " rastrillo bajado=" + l.PortcullisDown + " plaza→fuera norte=" + path.status
            + " etapa=" + QuestLog.Stage(QuestLog.Citadel) + " héroe=" + Hero.ToString("0");
    }

    // Manda al héroe andando fuera por la puerta norte (después: exec VillageChecks.Where).
    public static string WalkNorth()
    {
        Teleport(new Vector3(0f, 0.1f, 22f));
        Players.Primary.MoveTo(new Vector3(0f, 0f, 48f));
        return "andando al norte";
    }

    // Manda al héroe del camino del sur hacia el bosque, saliéndose del camino.
    public static string WalkSouthForest()
    {
        Teleport(new Vector3(-3f, 0.1f, -40f));
        Players.Primary.MoveTo(new Vector3(-26f, 0f, -36f));
        return "andando al bosque del sur";
    }

    public static string GoSouthRoad() { Teleport(new Vector3(-3f, 0.1f, -40f)); return "en el camino del sur"; }

    // Lo saca 6,5 m del camino de golpe (como si atravesara la empalizada): el límite de respaldo debe devolverlo.
    public static string ForceOut()
    {
        GameObject p = Players.PrimaryObject;
        Rigidbody rb = p.GetComponent<Rigidbody>();
        Vector3 to = p.transform.position + new Vector3(7f, 0f, 0f);
        if (rb != null) rb.position = to;
        p.transform.position = to;
        return "empujado a " + to.ToString("0") + " permitido=" + VillageLock.Allowed(to);
    }

    public static string Where() => "héroe=" + Hero.ToString("0") + " dentro=" + VillageLock.Allowed(Hero) + " abierto=" + VillageLock.Open;

    // Termina la charla de Steve (como si se hubieran dicho todas las líneas).
    public static string FinishSteve()
    {
        var steve = GameObject.Find(MapSystem.SteveName);
        QuestGiver g = steve != null ? steve.GetComponent<QuestGiver>() : null;
        if (g == null) return "sin Steve";
        g.SkipIntro();
        return "etapa=" + QuestLog.Stage(QuestLog.Citadel);
    }

    // Hablar con Steve de verdad (empieza la charla; el desbloqueo llega al terminarla).
    public static string TalkToSteve()
    {
        var steve = GameObject.Find(MapSystem.SteveName);
        if (steve == null) return "sin Steve";
        Teleport(steve.transform.position + steve.transform.forward * 2f);
        steve.GetComponent<NPCInteractable>().Choose(NPCOption.Hablar);
        return "hablando · abierto ya=" + VillageLock.Open;
    }

    // Por dónde pasa el camino del NavMesh de la plaza al exterior norte (para buscar fugas).
    public static string PathCorners()
    {
        var path = new NavMeshPath();
        NavMesh.SamplePosition(new Vector3(0f, 0f, -6f), out NavMeshHit a, 3f, NavMesh.AllAreas);
        NavMesh.SamplePosition(new Vector3(0f, 0f, 46f), out NavMeshHit b, 3f, NavMesh.AllAreas);
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        var sb = new StringBuilder(path.status + ": ");
        foreach (Vector3 c in path.corners) sb.Append(c.ToString("0")).Append(' ');
        return sb.ToString();
    }

    // ---------- Camino de la Ciudadela Caída ----------

    private static VillageWorldGenerator Gen => Object.FindAnyObjectByType<VillageWorldGenerator>();

    // Que el camino exista, sea transitable de la plaza a la ciudadela y tenga el pasillo libre.
    public static string RoadInfo()
    {
        var L = Layout;
        if (L == null || L.citadelRoad.Count == 0) return "sin camino";
        float len = 0f;
        for (int i = 1; i < L.citadelRoad.Count; i++) len += Vector3.Distance(L.citadelRoad[i - 1], L.citadelRoad[i]);
        var sb = new StringBuilder("camino=" + Mathf.RoundToInt(len) + " m (" + L.citadelRoad.Count + " puntos)");
        // ¿Andando de la plaza a la plaza de la ciudadela?
        var path = new NavMeshPath();
        NavMesh.SamplePosition(new Vector3(0f, 0f, -6f), out NavMeshHit a, 3f, NavMesh.AllAreas);
        NavMesh.SamplePosition(L.castle.TownCenter, out NavMeshHit b, 6f, NavMesh.AllAreas);
        NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
        float pathLen = 0f;
        for (int i = 1; i < path.corners.Length; i++) pathLen += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        sb.Append(" | plaza→ciudadela=").Append(path.status).Append(' ').Append(Mathf.RoundToInt(pathLen)).Append(" m");
        // Cada punto del camino, ¿en el NavMesh?
        int off = 0;
        string firstOff = "";
        foreach (Vector3 q in L.citadelRoad)
            if (!NavMesh.SamplePosition(q, out NavMeshHit nh, 2f, NavMesh.AllAreas))
            {
                off++;
                if (firstOff.Length == 0)
                {
                    NavMesh.SamplePosition(q, out NavMeshHit far, 10f, NavMesh.AllAreas);
                    float ground = Physics.Raycast(q + Vector3.up * 20f, Vector3.down, out RaycastHit rh, 40f, ~0, QueryTriggerInteraction.Ignore) ? rh.point.y : -99f;
                    firstOff = q.ToString("0") + " suelo=" + ground.ToString("0.0") + " navmesh más cerca=" + far.position.ToString("0.0") + (rh.collider != null ? " (" + rh.collider.name + ")" : "");
                }
            }
        sb.Append(" | puntos fuera del NavMesh=").Append(off).Append(' ').Append(firstOff);
        // Obstáculos cerca del centro del camino.
        int pines = 0, mounds = 0, fires = 0;
        foreach (VillageLayout.Spot p in L.pines) if (VillageLayout.DistanceToCitadelRoad(p.position) < 5f) pines++;
        foreach (VillageLayout.Spot p in L.mounds) if (VillageLayout.DistanceToCitadelRoad(p.position) < 5f) mounds++;
        foreach (VillageLayout.Spot p in L.bonfires) if (VillageLayout.DistanceToCitadelRoad(p.position) < 5f) fires++;
        sb.Append(" | a <5 m: pinos=").Append(pines).Append(" montones=").Append(mounds).Append(" hogueras=").Append(fires);
        // Colisionadores que corten el camino (a la altura del pecho).
        int blocked = 0;
        foreach (Vector3 q in L.citadelRoad)
            foreach (Collider c in Physics.OverlapSphere(q + Vector3.up * 1f, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                if (c.GetComponentInParent<PlayerController>() == null && c.GetComponentInParent<EnemyBrain>() == null && !(c is TerrainCollider) && c.bounds.size.y > 0.6f) { blocked++; break; }
        sb.Append(" | puntos con choque=").Append(blocked);
        return sb.ToString();
    }

    public static string Camps()
    {
        var g = Gen;
        if (g == null) return "sin generador";
        var L = Layout;
        var sb = new StringBuilder();
        Vector3 prev = Vector3.positiveInfinity;
        foreach (EnemyCamp c in g.RoadCamps)
        {
            Vector3 p = c.transform.position;
            bool nav = NavMesh.SamplePosition(p, out NavMeshHit h, 2f, NavMesh.AllAreas);
            sb.Append(c.campId).Append(": a ").Append(VillageLayout.DistanceToCitadelRoad(p).ToString("0.0")).Append(" m del camino")
              .Append(" navmesh=").Append(nav).Append(" zona segura=").Append(SafeZone.Contains(p))
              .Append(" grupo=").Append(c.groupSize).Append(" nivel=").Append(c.monsterLevel);
            if (!float.IsInfinity(prev.x)) sb.Append(" sep=").Append(Vector3.Distance(prev, p).ToString("0"));
            sb.Append(" | ");
            prev = p;
        }
        sb.Append("ciudadela a ").Append(Vector3.Distance(prev, L.castle.ToWorld(L.town.camp[0])).ToString("0")).Append(" m del último");
        return sb.ToString();
    }

    // Recorre el camino por tramos (teletransporte): cuenta los corruptos despiertos alrededor de cada punto.
    private static int _walkStep;
    public static string WalkRoadStep()
    {
        var L = Layout;
        float[] ts = { 0.05f, 0.3f, 0.55f, 0.8f, 1f };
        if (_walkStep >= ts.Length) _walkStep = 0;
        float t = ts[_walkStep++];
        Vector3 p = VillageLayout.CitadelRoadPoint(L, t, out Vector3 dir);
        Teleport(p + Vector3.up * 0.1f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
        int near = 0;
        foreach (EnemyBrain b in Object.FindObjectsByType<EnemyBrain>())
            if (b.Current != EnemyBrain.State.Dead && Vector3.Distance(b.transform.position, p) < 30f) near++;
        string siege = CitadelSiege.Instance != null ? CitadelSiege.Instance.Current.ToString() : "-";
        return "tramo " + t.ToString("0.00") + " en " + p.ToString("0") + " corruptos a <30 m=" + near + " asedio=" + siege + " etapa=" + QuestLog.Stage(QuestLog.Citadel);
    }

    public static string ResetWalk() { _walkStep = 0; return "ok"; }

    // Al llegar a la plaza de la ciudadela (para ver si el asedio arranca solo con la misión activa).
    public static string GoCitadelSquare()
    {
        var L = Layout;
        Teleport(L.castle.TownCenter + Vector3.up * 0.1f);
        return "en la plaza de la ciudadela";
    }

    public static string SiegeState() => "asedio=" + (CitadelSiege.Instance != null ? CitadelSiege.Instance.Current.ToString() : "-") + " etapa=" + QuestLog.Stage(QuestLog.Citadel);

    public static string RoadShot()
    {
        var L = Layout;
        Vector3 p = VillageLayout.CitadelRoadPoint(L, 0.12f, out Vector3 dir);
        string a = FrostboundBridge.CamShot("r_desvio", p - dir * 10f + Vector3.up * 6f, VillageLayout.CitadelRoadPoint(L, 0.4f, out _) + Vector3.up * 2f, 55f);
        Vector3 q = VillageLayout.CitadelRoadPoint(L, 0.6f, out Vector3 d2);
        string b = FrostboundBridge.CamShot("r_medio", q - d2 * 8f + Vector3.up * 5f, L.castle.TownCenter + Vector3.up * 10f, 55f);
        string c = FrostboundBridge.CamShot("r_aereo", new Vector3(L.castle.Gate.x * 0.4f, 90f, L.castle.Gate.z * 0.4f - 50f), new Vector3(L.castle.Gate.x * 0.45f, 0f, L.castle.Gate.z * 0.45f + 10f), 60f);
        return a + " | " + b + " | " + c;
    }

    // ---------- Rendimiento: apagar por partes para ver qué cuesta ----------

    public static string AtmosphereOff()
    {
        var a = Object.FindAnyObjectByType<VillageAtmosphere>();
        if (a == null) return "sin ambiente";
        var v = a.GetComponent<UnityEngine.Rendering.Volume>();
        if (v != null) v.enabled = false;
        return "volumen del ambiente apagado";
    }

    public static string RoadOff()
    {
        GameObject r = GameObject.Find("Camino_Ciudadela");
        if (r != null) r.SetActive(false);
        return "camino apagado";
    }

    // Cuenta lo que dibuja la cámara: renderers visibles con el sombreador triplanar y en total.
    public static string RenderStats()
    {
        int vis = 0, tri = 0, lights = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>())
        {
            if (!r.isVisible) continue;
            vis++;
            if (r.sharedMaterial != null && r.sharedMaterial.shader.name == "Frostbound/WorldTriplanarLit") tri++;
        }
        foreach (Light l in Object.FindObjectsByType<Light>()) if (l.isActiveAndEnabled && l.type != LightType.Directional) lights++;
        return "visibles=" + vis + " triplanar=" + tri + " luces puntuales activas=" + lights;
    }
}
