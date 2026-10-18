using UnityEngine;

// Pipo en el poblado: cuando el héroe lo libera en el Bosque Corrupto, aparece junto a Steve (una copia pequeña
// de Steve con plumaje azul claro) y cuenta lo que vio. Se queda ahí el resto de la partida.
public class PipoAtHome : MonoBehaviour
{
    public static readonly string[] Lines =
    {
        "¡Héroe! Steve me ha dado sopa caliente. ¡Y una bufanda nueva!",
        "Bobby no es malo. Antes me enseñaba a pescar en el lago helado...",
        "La voz del hielo le prometía cosas. Bobby lloraba cuando la oía.",
        "La cumbre está detrás del bosque, por las grietas. Ten cuidado arriba.",
        "Cuando vuelvas, ¿me cuentas cómo es el Corazón del Frost? ¡Pero de lejos!"
    };

    private GameObject _steve;
    private GameObject _pipo;

    public static void Ensure(GameObject steve)
    {
        if (steve == null || FindAnyObjectByType<PipoAtHome>() != null) return;
        var go = new GameObject("Pipo_En_Casa");
        go.AddComponent<PipoAtHome>()._steve = steve;
    }

    void Start()
    {
        Refresh();
        QuestLog.Changed += OnQuest;
    }

    void OnDestroy() => QuestLog.Changed -= OnQuest;

    private void OnQuest(string id, int stage) => Refresh();

    public bool Home => QuestLog.Stage(MadreSaviaEncounter.Quest) >= MadreSaviaEncounter.StageHome;

    private void Refresh()
    {
        if (!Home || _pipo != null || _steve == null) return;
        Vector3 at = _steve.transform.position + _steve.transform.right * 1.6f;
        if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
        _pipo = Instantiate(_steve, at, _steve.transform.rotation, _steve.transform.parent);
        _pipo.name = "NPC_Pipo";
        _pipo.transform.localScale = _steve.transform.localScale * 0.62f;
        QuestGiver giver = _pipo.GetComponent<QuestGiver>();
        if (giver != null) Destroy(giver);
        NPCInteractable npc = _pipo.GetComponent<NPCInteractable>();
        if (npc != null)
        {
            npc.displayName = "Pipo";
            npc.role = "Hermano de Rocco";
            npc.lines = Lines;
            npc.talkOverride = null;
            npc.options = new System.Collections.Generic.List<NPCOption> { NPCOption.Hablar };
        }
        VillagerNPC v = _pipo.GetComponent<VillagerNPC>();
        if (v != null)
        {
            v.villagerName = "Pipo";
            v.role = "Hermano de Rocco";
            v.dialogue = Lines;
            v.mood = VillagerMood.Pace;
            v.wanderRadius = 2f;
        }
        NameTag tag = _pipo.GetComponent<NameTag>();
        if (tag != null) tag.SetName("Pipo", "Hermano de Rocco");
        PenguinAppearance look = _pipo.GetComponentInChildren<PenguinAppearance>();
        if (look != null) look.SetPlumage(MadreSaviaEncounter.PipoPlumage);
        Damageable d = _pipo.GetComponent<Damageable>();
        if (d != null) d.RefreshBaseColors();
    }
}
