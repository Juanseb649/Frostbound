using System.Collections;
using UnityEngine;

// Un NPC que da misiones y las comenta. Todo sale de los QuestDefinition: si hay una misión suya por dar, la cuenta
// línea a línea y la empieza; si no, dice las líneas de la etapa en la que va la misión que comenta.
[RequireComponent(typeof(NPCInteractable))]
public class QuestGiver : MonoBehaviour
{
    [Tooltip("Id con el que las misiones nombran a este NPC (giver / speaker).")]
    public string giverId = "steve";
    public string idleRole = "Explorador";

    private NPCInteractable _npc;
    private NameTag _tag;
    private int _line;
    private bool _telling;

    void Awake()
    {
        _npc = GetComponent<NPCInteractable>();
        _tag = GetComponent<NameTag>();
        _npc.talkOverride = Talk;
        _npc.role = idleRole;
        VillagerNPC v = GetComponent<VillagerNPC>();
        if (v != null) v.role = idleRole;
    }

    void OnEnable() => QuestLog.Changed += OnQuest;
    void OnDisable() => QuestLog.Changed -= OnQuest;
    void Start() => RefreshTag();

    private void OnQuest(string id, int stage) => RefreshTag();

    private void RefreshTag()
    {
        if (_tag == null) return;
        _tag.SetName(_tag.displayName, HasNews ? "¡Tiene algo que contarte!" : idleRole);
    }

    public bool HasNews => Offer() != null;

    // La primera misión suya que ya se puede dar.
    private QuestDefinition Offer()
    {
        foreach (QuestDefinition q in QuestLog.All)
            if (q.giver == giverId && QuestLog.Stage(q.id) == 0 && q.Unlocked) return q;
        return null;
    }

    // Las líneas que tocan ahora: las de la misión en curso que comenta; si no hay, las de la última que avanzó.
    private string[] CurrentLines()
    {
        string[] last = null;
        foreach (QuestDefinition q in QuestLog.All)
        {
            if (q.Speaker != giverId) continue;
            int stage = QuestLog.Stage(q.id);
            QuestStage st = q.Stage(stage);
            if (st == null || st.giverLines == null || st.giverLines.Length == 0) continue;
            if (QuestLog.Active(q.id)) return st.giverLines;
            last = st.giverLines;
        }
        return last;
    }

    private bool Talk()
    {
        if (_telling) return true;
        // Las misiones que esperaban que hablaras con él avanzan antes de que conteste.
        QuestLog.Report(QuestObjective.Hablar, giverId);
        QuestDefinition offer = Offer();
        if (offer != null)
        {
            StartCoroutine(Tell(offer));
            return true;
        }
        string[] pool = CurrentLines();
        if (pool == null) return false;
        Say(pool[_line++ % pool.Length]);
        return true;
    }

    private float Say(string text)
    {
        float seconds = Mathf.Clamp(2.2f + text.Length * 0.05f, 3.5f, 7.5f);
        _npc.TalkUntil = Time.time + seconds;
        if (WorldHUD.Instance != null) WorldHUD.Instance.Say(transform, _npc.headHeight * transform.lossyScale.y, text, seconds);
        return seconds;
    }

    private IEnumerator Tell(QuestDefinition quest)
    {
        _telling = true;
        foreach (string line in quest.offerLines)
        {
            float s = Say(line);
            yield return new WaitForSeconds(s + 0.15f);
        }
        _telling = false;
        _line = 0;
        QuestLog.Begin(quest.id);
    }

    // Para las comprobaciones: da la misión que toque sin esperar la charla.
    public void SkipIntro()
    {
        StopAllCoroutines();
        _telling = false;
        QuestDefinition offer = Offer();
        if (offer != null) QuestLog.SetStage(offer.id, 1);
    }
}
