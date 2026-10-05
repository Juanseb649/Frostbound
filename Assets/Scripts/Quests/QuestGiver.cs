using System.Collections;
using UnityEngine;

// Steve cuenta lo de la ciudadela caída y activa la misión. Según la etapa, da pistas o agradece.
[RequireComponent(typeof(NPCInteractable))]
public class QuestGiver : MonoBehaviour
{
    public static readonly string[] Intro =
    {
        "Eh, tú. Sí, tú, el de la espada. Desde mi claro se ve el castillo... y no me gusta lo que veo.",
        "Al pie de sus murallas había un poblado: la ciudadela. Gente buena, guardias valientes.",
        "Hace unas lunas bajó de la montaña una tropa de pingüinos de élite. Malditos, con los ojos como cristales.",
        "Los mandaba un ninja. Dicen que corrompió a los guardias y que ellos mismos atacaron su poblado.",
        "Su capitán, Gorvald, sigue rondando la plaza. Si alguien puede romper ese sello del castillo... eres tú."
    };

    private static readonly string[] Waiting =
    {
        "La ciudadela está al pie del castillo. Ten cuidado con la plaza: ahí se juntan.",
        "Si ves a Gorvald, no dejes que te alcance su hacha. Dicen que ahora gotea veneno."
    };

    private static readonly string[] AfterBarbarian =
    {
        "¿Gorvald habló antes de caer? Entonces es verdad: el ninja está dentro del castillo.",
        "El sello se rompió. Lo que haya ahí dentro ya sabe que vas."
    };

    private static readonly string[] Done =
    {
        "Lo hiciste. Esta noche la ciudadela descansa. Gracias, héroe.",
        "Si la montaña aún late... será otro día. Hoy, descansa junto al fuego."
    };

    private NPCInteractable _npc;
    private NameTag _tag;
    private int _line;
    private bool _telling;

    void Awake()
    {
        _npc = GetComponent<NPCInteractable>();
        _tag = GetComponent<NameTag>();
        _npc.talkOverride = Talk;
        _npc.role = "Explorador";
        VillagerNPC v = GetComponent<VillagerNPC>();
        if (v != null) v.role = "Explorador";
    }

    void OnEnable() => QuestLog.Changed += OnQuest;
    void OnDisable() => QuestLog.Changed -= OnQuest;
    void Start() => RefreshTag();

    private void OnQuest(string id, int stage) => RefreshTag();

    private void RefreshTag()
    {
        if (_tag == null) return;
        _tag.subtitle = QuestLog.Stage(QuestLog.Citadel) == QuestLog.CitadelNone ? "¡Tiene algo que contarte!" : "Explorador";
    }

    public bool HasNews => QuestLog.Stage(QuestLog.Citadel) == QuestLog.CitadelNone;

    private bool Talk()
    {
        if (_telling) return true;
        int stage = QuestLog.Stage(QuestLog.Citadel);
        if (stage == QuestLog.CitadelNone)
        {
            StartCoroutine(Tell());
            return true;
        }
        string[] pool = stage == QuestLog.CitadelTalked ? Waiting : stage == QuestLog.CitadelBarbarian ? AfterBarbarian : Done;
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

    private IEnumerator Tell()
    {
        _telling = true;
        foreach (string line in Intro)
        {
            float s = Say(line);
            yield return new WaitForSeconds(s + 0.15f);
        }
        _telling = false;
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelTalked);
        BonfireBanner.Show("NUEVA MISIÓN", BonfireBanner.Victory);
        Notifications.Show("Nueva misión: " + QuestLog.Title(QuestLog.Citadel), FrostboundUI.Gold);
    }

    // Para las comprobaciones: activa la misión sin esperar la charla.
    public void SkipIntro()
    {
        StopAllCoroutines();
        _telling = false;
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelTalked);
    }
}
