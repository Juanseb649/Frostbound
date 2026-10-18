using System.Collections;
using UnityEngine;

// La cámara del sello (final del capítulo III). Misión "el_sello":
// 1 llegar a la cámara → 2 Bobby pelea (al 10 % se rinde y confiesa) → 3 despierta el Corazón del Frost →
// 4 con el Corazón roto, rehacer el sello junto a Bobby → 5 fin: empieza el epílogo.
public class SealEncounter : MonoBehaviour
{
    public const string Quest = SummitGenerator.SealQuest;
    public const int StageBobby = 2;
    public const int StageHeart = 3;
    public const int StageSeal = 4;
    public const int StageDone = 5;
    public const string BobbyTitle = "Bobby";
    public const string HeartTitle = "Corazón del Frost";
    public const string SealMarker = "sello";

    public static readonly string[] BobbyLines =
    {
        "¡Basta! Basta... ya no puedo más.",
        "Yo rompí el sello. Rocco solo me ayudó porque yo tenía a Pipo.",
        "La voz del hielo me prometió que me devolvería a mi hermana. Se perdió en la ventisca hace tres inviernos.",
        "Mentía. El Corazón solo quiere salir. Y yo le abrí la puerta.",
        "Ayúdame a cerrarlo, héroe. Por favor... ¡cuidado! ¡Ya despierta!"
    };

    public GameObject npcPrefab;
    public float radius = 18f;

    public BossController Bobby { get; private set; }
    public BossController Heart { get; private set; }
    public QuestMarker Seal { get; private set; }
    private GameObject _bobbyNpc;

    private static int Stage => QuestLog.Stage(Quest);

    void Start()
    {
        int s = Stage;
        if (s == StageBobby) SpawnBobby(false);
        else if (s == StageHeart) { BobbyAside(); SpawnHeart(false); }
        else if (s == StageSeal) { BobbyAside(); SealPoint(); }
        QuestLog.Changed += OnQuest;
    }

    void OnDestroy() => QuestLog.Changed -= OnQuest;

    private void OnQuest(string id, int stage)
    {
        if (id != Quest) return;
        if (stage == StageBobby && Bobby == null) SpawnBobby(true);
    }

    // ---------- Bobby ----------

    public void SpawnBobby(bool intro)
    {
        GameDatabase db = GameDatabase.Instance;
        EnemyDefinition ninja = db != null ? db.FindEnemy("Corrupt_Ninja") : null;
        if (ninja == null || ninja.prefab == null) return;
        Vector3 at = transform.position + Vector3.forward * 4f;
        GameObject go = Instantiate(ninja.prefab, at, Quaternion.Euler(0f, 180f, 0f), transform);
        go.name = "Bobby";
        EnemyBrain brain = go.GetComponent<EnemyBrain>();
        brain.Setup(ninja, null, BossTuning.BobbyLevel, false, BobbyBoss.Plumage);
        Bobby = BossController.Make(brain, BobbyTitle, "El que rompió el sello", BossTuning.BobbyHealth, BossTuning.BobbyDamage, 1.35f, def =>
        {
            def.displayName = BobbyTitle;
            def.sightRange = 24f;
            def.experience = 900;
            def.lootPicks = 6;
        });
        Bobby.dyingLines = BobbyLines;
        Bobby.speechAt = 0.1f;
        Bobby.spare = true;
        go.AddComponent<BobbyBoss>();
        Bobby.Defeated += OnBobbySpared;
        if (!intro) return;
        BonfireBanner.Show("BOBBY", new Color(0.55f, 0.8f, 1f));
        if (WorldHUD.Instance != null) WorldHUD.Instance.Say(go.transform, 1.55f * go.transform.lossyScale.y, "No deberías haber subido. El Corazón me lo prometió... ¡no dejaré que lo estropees!", 4.5f);
        brain.Alert();
    }

    private void OnBobbySpared(BossController b) => StartCoroutine(Awaken());

    // Bobby se aparta y despierta el Corazón.
    private IEnumerator Awaken()
    {
        yield return new WaitForSeconds(1.5f);
        if (Bobby != null)
        {
            Vector3 aside = transform.position + new Vector3(-radius * 0.55f, 0f, -radius * 0.3f);
            if (UnityEngine.AI.NavMesh.SamplePosition(aside, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) aside = hit.position;
            Bobby.Brain.Blink(aside);
        }
        AudioManager.Play3D(AudioId.Boss_Laugh, transform.position, 0.5f, 3f, 50f);
        yield return new WaitForSeconds(1f);
        SpawnHeart(true);
        yield return new WaitForSeconds(3f);
        SayBobby("¡Cuando se proteja, rompe los cristales que lo alimentan! ¡Yo aguantaré el sello todo lo que pueda!");
    }

    // Bobby ya rendido (al volver a la zona): de rodillas a un lado de la cámara.
    private void BobbyAside()
    {
        if (npcPrefab == null || _bobbyNpc != null) return;
        Vector3 at = transform.position + new Vector3(-radius * 0.55f, 0f, -radius * 0.3f);
        if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
        _bobbyNpc = Instantiate(npcPrefab, at, Quaternion.LookRotation(transform.position - at), transform);
        _bobbyNpc.name = "Bobby_Rendido";
        _bobbyNpc.transform.localScale = Vector3.one * 1.1f;
        PenguinAppearance look = _bobbyNpc.GetComponentInChildren<PenguinAppearance>();
        if (look != null) look.SetPlumage(new Color(0.55f, 0.72f, 0.98f));
        foreach (Collider c in _bobbyNpc.GetComponentsInChildren<Collider>()) Destroy(c);
        NameTag tag = _bobbyNpc.AddComponent<NameTag>();
        tag.SetName("Bobby", "Ya libre del Frost");
    }

    private Transform BobbyTransform => Bobby != null ? Bobby.transform : _bobbyNpc != null ? _bobbyNpc.transform : null;

    private void SayBobby(string line)
    {
        Transform t = BobbyTransform;
        if (t != null && WorldHUD.Instance != null) WorldHUD.Instance.Say(t, 1.55f * t.lossyScale.y, line, Mathf.Clamp(2.2f + line.Length * 0.05f, 3.5f, 7f));
    }

    // ---------- El Corazón del Frost ----------

    public void SpawnHeart(bool awakening)
    {
        GameDatabase db = GameDatabase.Instance;
        EnemyDefinition mage = db != null ? db.FindEnemy("Corrupt_Mage") : null;
        if (mage == null || mage.prefab == null) return;
        GameObject go = Instantiate(mage.prefab, transform.position, Quaternion.Euler(0f, 180f, 0f), transform);
        go.name = "Corazon_del_Frost";
        EnemyBrain brain = go.GetComponent<EnemyBrain>();
        brain.Setup(mage, null, BossTuning.HeartLevel, false, new Color(0.4f, 0.6f, 1f));
        Heart = BossController.Make(brain, HeartTitle, "Lo que late bajo la montaña", BossTuning.HeartHealth, BossTuning.HeartDamage, 3f, def =>
        {
            def.displayName = HeartTitle;
            def.experience = 2500;
            def.lootPicks = 10;
        });
        var heart = go.AddComponent<FrostHeartBoss>();
        heart.ChamberRadius = radius;
        Heart.Defeated += OnHeartBroken;
        if (!awakening) return;
        BonfireBanner.Show("EL CORAZÓN DEL FROST", new Color(0.55f, 0.8f, 1f));
        brain.Alert();
    }

    private void OnHeartBroken(BossController b) => StartCoroutine(Broken());

    private IEnumerator Broken()
    {
        FrostHeartBoss h = Heart != null ? Heart.GetComponent<FrostHeartBoss>() : null;
        if (h != null) h.Shatter();
        BonfireBanner.Show("EL CORAZÓN DEL FROST SE ROMPE", BonfireBanner.Victory);
        yield return new WaitForSeconds(2.5f);
        SayBobby("¡Ahora! Los fragmentos del sello... ¡ayúdame a ponerlos en su sitio!");
        SealPoint();
    }

    // El sello roto en el centro de la cámara: rehacerlo termina el capítulo III.
    private void SealPoint()
    {
        if (Seal != null) return;
        var go = new GameObject("Sello_Roto");
        go.transform.SetParent(transform, false);
        go.transform.position = transform.position;
        Material rune = MenuRuins.Lit("Sello", new Color(0.5f, 0.75f, 1f), 0.6f, 0f, new Color(0.4f, 1f, 2.4f));
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * 360f;
            var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(f.GetComponent<Collider>());
            f.name = "Fragmento";
            f.transform.SetParent(go.transform, false);
            f.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.08f, 1.6f + (i % 2) * 0.5f);
            f.transform.localRotation = Quaternion.Euler(0f, a + 20f, 0f);
            f.transform.localScale = new Vector3(0.9f, 0.12f, 0.5f);
            f.GetComponent<Renderer>().sharedMaterial = rune;
        }
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 1.4f;
        col.height = 2f;
        col.center = Vector3.up;
        Seal = go.AddComponent<QuestMarker>();
        Seal.id = SealMarker;
        Seal.type = QuestObjective.Interactuar;
        Seal.displayName = "El sello roto";
        Seal.actionLabel = "Rehacer el sello";
        Seal.message = "";
        Seal.Activated += () => StartCoroutine(Remade());
    }

    private IEnumerator Remade()
    {
        foreach (Transform f in Seal.transform)
            f.localPosition = new Vector3(f.localPosition.x, 0.08f, f.localPosition.z) * 0.55f + Vector3.up * 0.04f;
        SayBobby("Ya está. Lo siento, héroe... Lo siento por todo.");
        yield return new WaitForSeconds(2f);
        EndingSequence.Play();
    }
}
