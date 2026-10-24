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
    private Coroutine _tell;
    private QuestDefinition _told;
    private float _tellStart;

    // El que está contando ahora una misión (solo hay una charla de misión a la vez).
    public static QuestGiver Telling { get; private set; }
    public NPCInteractable Npc => _npc;

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
    void OnDisable()
    {
        QuestLog.Changed -= OnQuest;
        if (Telling == this) Telling = null;
    }
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
        if (_telling) { SkipTell(); return true; }
        // Las misiones que esperaban que hablaras con él avanzan antes de que conteste.
        QuestLog.Report(QuestObjective.Hablar, giverId);
        QuestDefinition offer = Offer();
        if (offer != null)
        {
            _tell = StartCoroutine(Tell(offer));
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
        _told = quest;
        _tellStart = Time.time;
        Telling = this;
        foreach (string line in quest.offerLines)
        {
            float s = Say(line);
            yield return new WaitForSeconds(s + 0.15f);
        }
        FinishTell();
    }

    // Igual al final de la charla, se llegue leyendo todo o saltándola: empieza la misión una sola vez.
    private void FinishTell()
    {
        QuestDefinition quest = _told;
        _telling = false;
        _told = null;
        _tell = null;
        _line = 0;
        if (Telling == this) Telling = null;
        if (quest != null) QuestLog.Begin(quest.id);
    }

    // Corta la charla de misión: quita el globo y da la misión como si hubiera terminado.
    public void SkipTell()
    {
        if (!_telling) return;
        if (_tell != null) StopCoroutine(_tell);
        _npc.TalkUntil = 0f;
        if (WorldHUD.Instance != null) WorldHUD.Instance.HideSay(transform);
        FinishTell();
    }

    // Para el héroe: salta la charla en curso si está lo bastante cerca para oírla.
    public static bool TrySkip(Vector3 listener)
    {
        QuestGiver g = Telling;
        // La misma pulsación que eligió «Hablar» no puede saltar la charla que acaba de abrir.
        if (g == null || !g._telling || Time.time - g._tellStart < 0.3f) return false;
        Vector3 d = g.transform.position - listener;
        d.y = 0f;
        if (d.sqrMagnitude > SkipRange * SkipRange) return false;
        g.SkipTell();
        return true;
    }

    public const float SkipRange = 12f;

    // Para las comprobaciones: da la misión que toque sin esperar la charla.
    public void SkipIntro()
    {
        StopAllCoroutines();
        _telling = false;
        _told = null;
        _tell = null;
        if (Telling == this) Telling = null;
        QuestDefinition offer = Offer();
        if (offer != null) QuestLog.SetStage(offer.id, 1);
    }
}
