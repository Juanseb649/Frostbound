using System;
using System.Collections.Generic;
using UnityEngine;

// Qué hay que hacer para cumplir el objetivo de una etapa. Lo avanzan los avisos de QuestEvents.Report.
public enum QuestObjective
{
    Ninguno,      // Lo avanza el código de la zona (asedio, encuentro con un jefe...).
    Hablar,       // Hablar con un NPC (objetivo = id del que da la misión, p. ej. "steve").
    Llegar,       // Entrar en una zona marcada con QuestMarker (objetivo = id del marcador).
    Matar,        // Matar enemigos (objetivo = id de EnemyDefinition, vacío = cualquiera).
    Jefe,         // Derrotar a un jefe (objetivo = título del jefe).
    Recoger,      // Recoger objetos (objetivo = id del objeto).
    Interactuar   // Usar algo del mundo: pistas, raíces, altares (objetivo = id del marcador).
}

[Serializable]
public class QuestStage
{
    [Tooltip("Texto del panel de misiones. Vacío en la última etapa: misión cumplida.")]
    public string objective = "";
    public QuestObjective type = QuestObjective.Ninguno;
    [Tooltip("Id del enemigo, jefe, objeto, NPC o marcador. Vacío = cualquiera del tipo.")]
    public string target = "";
    [Min(1)] public int count = 1;
    [Tooltip("Lo que dice el que da la misión mientras estás en esta etapa (rota entre las líneas).")]
    [TextArea(1, 3)] public string[] giverLines = new string[0];
    [Tooltip("Aviso al llegar a esta etapa (vacío = ninguno).")]
    public string onReach = "";
}

// Una misión como dato: etapas con objetivo, diálogo del que la da y recompensa al cumplirla.
// Se guarda solo "id + etapa" (y el contador del objetivo), así que se pueden editar los textos sin romper partidas.
[CreateAssetMenu(menuName = "Frostbound/Misión", fileName = "Mision")]
public class QuestDefinition : ScriptableObject
{
    public string id = "";
    public string title = "";
    [Min(1)] public int chapter = 1;
    [Tooltip("Orden dentro de la campaña: el panel sigue la primera misión pendiente.")]
    public int order;

    [Header("Quién la da")]
    [Tooltip("Id del NPC (QuestGiver.giverId). Vacío = empieza sola al cumplirse el requisito.")]
    public string giver = "";
    [Tooltip("Quién comenta las etapas (las líneas de cada etapa). Vacío = el que la da.")]
    public string speaker = "";
    [Tooltip("Misión y etapa que hay que haber alcanzado antes (vacío = ninguna).")]
    public string requiresQuest = "";
    public int requiresStage;
    [Tooltip("Lo que cuenta el NPC al dar la misión (línea a línea).")]
    [TextArea(1, 3)] public string[] offerLines = new string[0];

    [Header("Etapas (la 1 es la primera; la última, sin objetivo, es misión cumplida)")]
    public List<QuestStage> stages = new List<QuestStage>();

    [Header("Recompensa al cumplirla")]
    public int experience;
    public int coins;

    public string Speaker => string.IsNullOrEmpty(speaker) ? giver : speaker;

    public int DoneStage => Mathf.Max(1, stages.Count);

    // Etapa 1..DoneStage; null fuera de rango (0 = sin empezar).
    public QuestStage Stage(int stage) => stage >= 1 && stage <= stages.Count ? stages[stage - 1] : null;

    public bool Unlocked => string.IsNullOrEmpty(requiresQuest) || QuestLog.Stage(requiresQuest) >= requiresStage;
}
