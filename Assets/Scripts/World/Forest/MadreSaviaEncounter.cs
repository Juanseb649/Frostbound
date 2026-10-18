using System.Collections;
using UnityEngine;

// El corazón del Bosque Corrupto (capítulo II): la Madre Savia emerge de la savia cuando el héroe llega al claro
// (misión "la_madre_savia", etapa 2). Al caer confiesa que Bobby nunca estuvo aquí, y el capullo de hielo junto al
// árbol madre se puede romper: dentro está Pipo, vivo, que cuenta lo que vio y vuelve al poblado con Steve.
public class MadreSaviaEncounter : MonoBehaviour
{
    public const string Quest = "la_madre_savia";
    public const int StageFight = 2;      // Derrota a la Madre Savia
    public const int StageCocoon = 3;     // Libera a Pipo
    public const int StageHome = 4;       // Vuelve con Steve
    public const string BossTitle = "Madre Savia";
    public const string CocoonMarker = "capullo_pipo";

    public static readonly string[] DyingLines =
    {
        "Ah... la savia se enfría...",
        "¿Bobby? No, pequeño héroe. Bobby nunca estuvo aquí.",
        "Me dejó el bosque para que alimentara al Corazón con la savia de los pinos.",
        "El niño, el hermano de Rocco... duerme en el capullo, junto al árbol madre.",
        "Sube a la cumbre si quieres. El Corazón ya te está esperando..."
    };

    public static readonly string[] PipoLines =
    {
        "¡Brrr! ¿Un héroe? ¿Te mandó Rocco? ¡Sabía que vendría alguien!",
        "Bobby me trajo aquí. Hablaba solo... con una voz que salía del hielo.",
        "Decía que el Corazón le devolvería a alguien que perdió. Que solo tenía que abrir el sello del todo.",
        "Subió a la cumbre. ¡Tienes que pararlo antes de que sea tarde!",
        "Yo vuelvo al poblado con Steve. ¡Me sé el camino! ¡Te espero allí!"
    };

    public static readonly Color PipoPlumage = new Color(0.62f, 0.78f, 0.98f);

    public GameObject npcPrefab;
    public Vector3 cocoonAt;
    public Vector3 exitAt;

    public BossController Boss { get; private set; }
    public GameObject Pipo { get; private set; }
    private Transform _cocoon;
    private QuestMarker _marker;

    private static int Stage => QuestLog.Stage(Quest);

    void Start()
    {
        if (Stage < StageHome) BuildCocoon();
        if (Stage == StageFight) Spawn(false);
        QuestLog.Changed += OnQuest;
    }

    void OnDestroy() => QuestLog.Changed -= OnQuest;

    private void OnQuest(string id, int stage)
    {
        if (id == Quest && stage == StageFight && Boss == null) Spawn(true);
    }

    public void Spawn(bool rising)
    {
        GameDatabase db = GameDatabase.Instance;
        EnemyDefinition mage = db != null ? db.FindEnemy("Corrupt_Mage") : null;
        if (mage == null || mage.prefab == null) return;
        GameObject go = Instantiate(mage.prefab, transform.position, Quaternion.identity, transform);
        go.name = "Madre_Savia";
        EnemyBrain brain = go.GetComponent<EnemyBrain>();
        brain.Setup(mage, null, 12, false, MadreSaviaBoss.Plumage);
        Boss = BossController.Make(brain, BossTitle, "Chamana corrupta del bosque", 10f, 1.3f, 1.8f, def =>
        {
            def.displayName = BossTitle;
            def.sightRange = 22f;
            def.experience = 650;
            def.lootPicks = 6;
        });
        Boss.dyingLines = DyingLines;
        Boss.speechAt = 0.08f;
        go.AddComponent<MadreSaviaBoss>();
        Boss.Defeated += OnDefeated;
        if (rising)
        {
            BonfireBanner.Show("LA MADRE SAVIA", new Color(0.55f, 0.95f, 0.4f));
            if (WorldHUD.Instance != null) WorldHUD.Instance.Say(go.transform, 1.55f * go.transform.lossyScale.y, "¿Quién pisa mis raíces? Ven, pequeño. El bosque tiene hambre.", 4f);
            brain.Alert();
        }
    }

    private void OnDefeated(BossController b) => StartCoroutine(Aftermath());

    private IEnumerator Aftermath()
    {
        yield return new WaitForSeconds(1.2f);
        BonfireBanner.Show("LA MADRE SAVIA HA CAÍDO", BonfireBanner.Victory);
        yield return new WaitForSeconds(2.2f);
        Notifications.Show("El capullo de hielo junto al árbol madre se resquebraja. Pipo está dentro.", FrostboundUI.Gold);
    }

    // ---------- El capullo y Pipo ----------

    private void BuildCocoon()
    {
        _cocoon = new GameObject("Capullo_Pipo").transform;
        _cocoon.SetParent(transform, false);
        _cocoon.position = cocoonAt;
        Pipo = MakePipo(cocoonAt, transform);
        if (Pipo != null) Pipo.transform.rotation = Quaternion.LookRotation(exitAt - cocoonAt);

        Material ice = MenuRuins.Lit("Hielo del capullo", new Color(0.55f, 0.78f, 0.98f), 0.95f, 0f, new Color(0.08f, 0.22f, 0.4f));
        Mesh octa = MenuRuins.Octahedron();
        // Esquirlas alrededor de Pipo, con huecos para que se le vea dentro.
        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * Mathf.PI * 2f;
            var shard = new GameObject("Esquirla");
            shard.transform.SetParent(_cocoon, false);
            float h = 1.1f + (i % 3) * 0.35f;
            shard.transform.localPosition = new Vector3(Mathf.Sin(a) * 0.62f, h * 0.45f, Mathf.Cos(a) * 0.62f);
            shard.transform.localRotation = Quaternion.Euler(Mathf.Cos(a) * -14f, a * Mathf.Rad2Deg, Mathf.Sin(a) * 14f);
            shard.transform.localScale = new Vector3(0.32f, h, 0.32f);
            shard.AddComponent<MeshFilter>().sharedMesh = octa;
            shard.AddComponent<MeshRenderer>().sharedMaterial = ice;
        }
        var light = new GameObject("Luz_Capullo").AddComponent<Light>();
        light.transform.SetParent(_cocoon, false);
        light.transform.localPosition = Vector3.up * 1.2f;
        light.type = LightType.Point;
        light.color = new Color(0.6f, 0.8f, 1f);
        light.range = 6f;
        light.intensity = 1.6f;
        light.shadows = LightShadows.None;

        var col = _cocoon.gameObject.AddComponent<CapsuleCollider>();
        col.radius = 0.9f;
        col.height = 2f;
        col.center = Vector3.up;
        _marker = _cocoon.gameObject.AddComponent<QuestMarker>();
        _marker.id = CocoonMarker;
        _marker.type = QuestObjective.Interactuar;
        _marker.displayName = "Capullo de hielo";
        _marker.actionLabel = "Romper el hielo";
        _marker.notYetMessage = "Dentro hay un pingüino pequeño, dormido. La Madre Savia no dejará que lo saques.";
        _marker.message = "";
        _marker.Activated += () => StartCoroutine(Free());
    }

    // Pipo: un pingüino pequeño de plumaje azul claro.
    public GameObject MakePipo(Vector3 at, Transform parent)
    {
        if (npcPrefab == null) return null;
        GameObject p = Instantiate(npcPrefab, at, Quaternion.identity, parent);
        p.name = "Pipo";
        p.transform.localScale = Vector3.one * 0.62f;
        PenguinAppearance look = p.GetComponentInChildren<PenguinAppearance>();
        if (look != null) look.SetPlumage(PipoPlumage);
        foreach (Collider c in p.GetComponentsInChildren<Collider>()) Destroy(c);
        return p;
    }

    private IEnumerator Free()
    {
        // El hielo salta en pedazos.
        AudioManager.Play3D(AudioId.Corrupt_Death, _cocoon.position, 0.6f, 2f, 25f, 0.1f);
        var shards = _cocoon.GetComponentsInChildren<MeshRenderer>();
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            foreach (MeshRenderer r in shards)
                if (r != null) r.transform.localPosition += r.transform.localPosition.normalized * Time.deltaTime * 4f + Vector3.up * Time.deltaTime;
            yield return null;
        }
        foreach (MeshRenderer r in shards) if (r != null) Destroy(r.gameObject);
        Light l = _cocoon.GetComponentInChildren<Light>();
        if (l != null) l.enabled = false;
        if (Pipo == null) yield break;

        NameTag tag = Pipo.AddComponent<NameTag>();
        tag.SetName("Pipo", "Hermano de Rocco");
        foreach (string line in PipoLines)
        {
            float s = Mathf.Clamp(2.2f + line.Length * 0.05f, 3.5f, 7f);
            if (WorldHUD.Instance != null) WorldHUD.Instance.Say(Pipo.transform, 0.9f, line, s);
            yield return new WaitForSeconds(s + 0.15f);
        }
        // Se va corriendo hacia el camino de vuelta.
        Vector3 from = Pipo.transform.position;
        Vector3 dir = exitAt - from;
        dir.y = 0f;
        Pipo.transform.rotation = Quaternion.LookRotation(dir.normalized);
        for (float t = 0f; t < 3f && Pipo != null; t += Time.deltaTime)
        {
            Pipo.transform.position = from + dir.normalized * t * 3.2f;
            if (t > 2.2f) Pipo.transform.localScale = Vector3.one * 0.62f * (1f - (t - 2.2f) / 0.8f);
            yield return null;
        }
        if (Pipo != null) Destroy(Pipo);
    }
}
