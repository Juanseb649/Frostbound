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
}
