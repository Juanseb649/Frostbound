using System;
using System.Collections.Generic;
using UnityEngine;

public enum NPCOption { Hablar, Comprar, Vender, Reparar }

// NPC con el que el héroe puede interactuar: se acerca, le hace clic y elige una opción.
public class NPCInteractable : MonoBehaviour
{
    public string displayName = "Aldeano";
    public string role = "";
    public List<NPCOption> options = new List<NPCOption> { NPCOption.Hablar };
    [Tooltip("Distancia a la que el héroe se detiene para hablar.")]
    public float interactRange = 2.2f;
    [Tooltip("Altura de la cabeza para el globo de diálogo.")]
    public float headHeight = 1.35f;
    [TextArea(2, 4)] public string[] lines = new string[0];

    // Otros sistemas (tienda, reparación, diálogos) se suscriben aquí.
    public static event Action<NPCInteractable, NPCOption> OptionChosen;

    // Si devuelve true, otro sistema (una misión) se encargó de la charla.
    public Func<bool> talkOverride;

    public bool InConversation { get; set; }
    public float TalkUntil { get; set; }

    private int _line;

    void Awake()
    {
        if (lines == null || lines.Length == 0)
        {
            VillagerNPC v = GetComponent<VillagerNPC>();
            if (v != null && v.dialogue != null && v.dialogue.Length > 0) lines = v.dialogue;
        }
    }

    public bool Busy => InConversation || Time.time < TalkUntil;

    // Opciones que ya tienen un sistema detrás. Comprar y Vender se habilitan con la tienda (F4).
    public static bool IsImplemented(NPCOption o) => o == NPCOption.Hablar || o == NPCOption.Reparar;

    public List<NPCOption> AvailableOptions()
    {
        var list = new List<NPCOption>();
        foreach (NPCOption o in options)
            if (IsImplemented(o) && !list.Contains(o)) list.Add(o);
        if (list.Count == 0) list.Add(NPCOption.Hablar);
        return list;
    }

    public string NextLine()
    {
        if (lines == null || lines.Length == 0) return "...";
        string s = lines[_line % lines.Length];
        _line++;
        return s;
    }

    public void Choose(NPCOption option)
    {
        OptionChosen?.Invoke(this, option);
        WorldHUD hud = WorldHUD.Instance;
        if (hud == null) return;

        hud.CloseMenu();
        if (option == NPCOption.Hablar && talkOverride != null && talkOverride()) return;
        if (option == NPCOption.Hablar)
        {
            string text = NextLine();
            float seconds = Mathf.Clamp(2f + text.Length * 0.05f, 3f, 7f);
            TalkUntil = Time.time + seconds;
            hud.Say(transform, headHeight * transform.lossyScale.y, text, seconds);
        }
        else if (option == NPCOption.Reparar)
        {
            Equipment player = FindAnyObjectByType<Equipment>();
            int n = player != null ? player.RepairAll() : 0;
            string text = n > 0 ? "¡Listo! " + (n == 1 ? "Tu arma quedó" : "Tus " + n + " armas quedaron") + " como nuevas." : "Tus armas están en perfecto estado.";
            TalkUntil = Time.time + 3.5f;
            hud.Say(transform, headHeight * transform.lossyScale.y, text, 3.5f);
        }
    }

    public static string Label(NPCOption o)
    {
        switch (o)
        {
            case NPCOption.Comprar: return "Comprar";
            case NPCOption.Vender: return "Vender";
            case NPCOption.Reparar: return "Reparar";
            default: return "Hablar";
        }
    }
}
