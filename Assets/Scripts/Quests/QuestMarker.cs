using System;
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
    [Tooltip("Nombre y botón al acercarse (solo Interactuar).")]
    public string displayName = "Pista";
    public string actionLabel = "Examinar";
    [Tooltip("Lo que se dice si se usa antes de tiempo.")]
    public string notYetMessage = "Aún no sabes qué hacer con esto.";

    // Al usarlo (para la animación del objeto: la raíz se seca, el altar se enciende...).
    public event Action Activated;

    // Recuerdo de la partida: "marca_<id>" etapa 1 = ya usado.
    public bool Used => QuestLog.Stage(SaveKey) >= 1;
    private string SaveKey => "marca_" + id;

    private NPCInteractable _npc;

    void Start()
    {
        if (type != QuestObjective.Interactuar) return;
        if (Used) return;
        _npc = GetComponent<NPCInteractable>();
        if (_npc == null) _npc = gameObject.AddComponent<NPCInteractable>();
        _npc.displayName = displayName;
        _npc.role = "";
        _npc.talkLabel = actionLabel;
        _npc.headHeight = 1f;
        _npc.talkOverride = Use;
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

    // Solo cuenta si alguna misión en curso espera justo esto ahora.
    private bool Relevant => QuestLog.Expects(type, id);

    // Usarlo (también desde las comprobaciones).
    public bool Use()
    {
        if (Used) return false;
        if (!Relevant)
        {
            if (WorldHUD.Instance != null) WorldHUD.Instance.Say(transform, 1.2f, notYetMessage, 3.5f);
            return true;
        }
        Trigger();
        if (_npc != null) Destroy(_npc);
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
        Activated?.Invoke();
    }
}
