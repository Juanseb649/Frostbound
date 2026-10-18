using UnityEngine;

// Un punto del mundo que avisa a las misiones: al llegar (Llegar) o al usarlo como a un NPC (Interactuar),
// p. ej. una pista de Pipo, una raíz corrupta o un altar. Se usa una sola vez por partida.
public class QuestMarker : MonoBehaviour
{
    public string id = "";
    public QuestObjective type = QuestObjective.Llegar;
    [Tooltip("Distancia a la que cuenta como llegar.")]
    public float radius = 6f;
    [Tooltip("Lo que se dice al usarlo o al llegar (vacío = nada).")]
    [TextArea(1, 3)] public string message = "";
    [Tooltip("Solo cuenta mientras esta misión está activa (vacío = siempre).")]
    public string quest = "";

    // Recuerdo de la partida: "marca_<id>" etapa 1 = ya usado.
    public bool Used => QuestLog.Stage(SaveKey) >= 1;
    private string SaveKey => "marca_" + id;

    private NPCInteractable _npc;

    void Start()
    {
        if (type != QuestObjective.Interactuar) return;
        _npc = GetComponent<NPCInteractable>();
        if (_npc == null) _npc = gameObject.AddComponent<NPCInteractable>();
        _npc.talkOverride = Use;
        if (Used) _npc.enabled = false;
    }

    void Update()
    {
        if (type != QuestObjective.Llegar || Used || !Relevant) return;
        foreach (PlayerController p in Players.All)
            if (p != null && (p.transform.position - transform.position).sqrMagnitude < radius * radius)
            {
                Trigger();
                return;
            }
    }

    private bool Relevant => string.IsNullOrEmpty(quest) || QuestLog.Active(quest);

    // Usarlo (también desde las comprobaciones).
    public bool Use()
    {
        if (Used || !Relevant) return false;
        Trigger();
        if (_npc != null) _npc.enabled = false;
        return true;
    }

    private void Trigger()
    {
        QuestLog.SetStage(SaveKey, 1);
        if (!string.IsNullOrEmpty(message))
        {
            if (WorldHUD.Instance != null && type == QuestObjective.Interactuar)
                WorldHUD.Instance.Say(transform, 1.2f, message, Mathf.Clamp(2.2f + message.Length * 0.05f, 3.5f, 7f));
            else Notifications.Show(message, FrostboundUI.Gold);
        }
        QuestLog.Report(type, id);
    }
}
