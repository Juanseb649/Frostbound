using UnityEngine;

// El epílogo en el poblado: con el sello rehecho, Bobby aparece junto a Steve y Pipo (una copia de Steve con su
// plumaje) y, al hablar con Steve y cumplir "epilogo", suben los créditos.
public class BobbyAtHome : MonoBehaviour
{
    public const string Epilogue = "epilogo";

    public static readonly string[] Lines =
    {
        "Héroe. No sé cómo darte las gracias... ni cómo pedir perdón a todos.",
        "Ayudaré a levantar la ciudadela, piedra a piedra. Es lo mínimo.",
        "Rocco ya me ha perdonado. Dice que le debo una caña de pescar nueva.",
        "Cuando el viento sopla de la montaña, ya no oigo la voz. Solo viento.",
        "Si algún día vuelve a latir algo bajo el hielo... sabré a quién llamar."
    };

    private GameObject _steve;
    private GameObject _bobby;

    public static void Ensure(GameObject steve)
    {
        if (steve == null || FindAnyObjectByType<BobbyAtHome>() != null) return;
        var go = new GameObject("Bobby_En_Casa");
        go.AddComponent<BobbyAtHome>()._steve = steve;
    }

    void Start()
    {
        Refresh();
        QuestLog.Changed += OnQuest;
    }

    void OnDestroy() => QuestLog.Changed -= OnQuest;

    private void OnQuest(string id, int stage)
    {
        Refresh();
        if (id == Epilogue && QuestLog.Done(Epilogue)) Invoke(nameof(Credits), 4f);
    }

    private void Credits() => CreditsRoll.Show();

    public bool Home => QuestLog.Done(SealEncounter.Quest);

    private void Refresh()
    {
        if (!Home || _bobby != null || _steve == null) return;
        Vector3 at = _steve.transform.position - _steve.transform.right * 1.8f;
        if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
        _bobby = Instantiate(_steve, at, _steve.transform.rotation, _steve.transform.parent);
        _bobby.name = "NPC_Bobby";
        _bobby.transform.localScale = _steve.transform.localScale * 1.08f;
        QuestGiver giver = _bobby.GetComponent<QuestGiver>();
        if (giver != null) Destroy(giver);
        NPCInteractable npc = _bobby.GetComponent<NPCInteractable>();
        if (npc != null)
        {
            npc.displayName = "Bobby";
            npc.role = "Ya libre del Frost";
            npc.lines = Lines;
            npc.talkOverride = null;
            npc.options = new System.Collections.Generic.List<NPCOption> { NPCOption.Hablar };
        }
        VillagerNPC v = _bobby.GetComponent<VillagerNPC>();
        if (v != null)
        {
            v.villagerName = "Bobby";
            v.role = "Ya libre del Frost";
            v.dialogue = Lines;
            v.mood = VillagerMood.Pace;
            v.wanderRadius = 2f;
        }
        NameTag tag = _bobby.GetComponent<NameTag>();
        if (tag != null) tag.SetName("Bobby", "Ya libre del Frost");
        PenguinAppearance look = _bobby.GetComponentInChildren<PenguinAppearance>();
        if (look != null) look.SetPlumage(new Color(0.55f, 0.72f, 0.98f));
        Damageable d = _bobby.GetComponent<Damageable>();
        if (d != null) d.RefreshBaseColors();
    }
}
