using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Comprobaciones de NPC: monedas del arcón en compras, bloqueo durante el diálogo, indicador de interactuar y escalado de menús.
public static class NpcChecks
{
    private static GameObject P => GameObject.Find("Player");
    private static NPCInteractable Npc(string name) => Object.FindObjectsByType<NPCInteractable>().FirstOrDefault(n => n.displayName == name);

    public static string StashSpend()
    {
        Wallet w = Wallet.Of(P);
        w.Set(30);
        Stash.DepositCoins(w, 30);
        w.Set(10);
        bool ok = w.SpendWithStash(25);
        string r = "pagó 25 con bolsa 10 + arcón 30 → " + ok + " · bolsa " + w.Coins + " · arcón " + Stash.Coins;
        bool fail = w.SpendWithStash(999);
        return r + " · pagar 999 → " + fail + " (bolsa " + w.Coins + ", arcón " + Stash.Coins + ")";
    }

    public static string GoToTico()
    {
        NPCInteractable n = Npc("Mercader Tico");
        if (n == null) return "sin Tico";
        Vector3 at = n.transform.position + n.transform.forward * 2f;
        P.GetComponent<PlayerPersistence>().MoveTo(at, Quaternion.LookRotation(-n.transform.forward));
        return "junto a Tico";
    }

    public static string Prompt()
    {
        InteractPrompt ip = Object.FindAnyObjectByType<InteractPrompt>();
        if (ip == null) return "sin indicador";
        var sb = new StringBuilder();
        foreach (TMPro.TextMeshProUGUI t in ip.GetComponentsInChildren<TMPro.TextMeshProUGUI>(false)) sb.Append(t.text).Append(" ");
        int glyphs = ip.GetComponentsInChildren<Image>(false).Length;
        return "visibles: [" + sb.ToString().Trim() + "] imágenes " + glyphs;
    }

    public static string TalkToTico()
    {
        NPCInteractable n = Npc("Mercader Tico");
        P.GetComponent<PlayerController>().Interact(n);
        return "hablando";
    }

    public static string MenuState()
    {
        WorldHUD h = WorldHUD.Instance;
        var pc = P.GetComponent<PlayerController>();
        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return "menú " + (h != null && h.MenuOpen) + " · en diálogo " + pc.InDialogue + " · ocupado " + PlayerController.Busy(pc) + " · seleccionado " + (sel != null ? sel.name : "-") + " · tienda " + TradeWindow.IsOpen;
    }

    public static string ChooseTalk() { Npc("Mercader Tico").Choose(NPCOption.Hablar); return MenuState(); }
    public static string ChooseBuy() { Npc("Mercader Tico").Choose(NPCOption.Comprar); return MenuState(); }
    public static string CloseTrade() { var t = Object.FindAnyObjectByType<TradeWindow>(); if (t != null) t.SendMessage("Close", SendMessageOptions.DontRequireReceiver); return MenuState(); }

    public static string Scalers()
    {
        var sb = new StringBuilder("pantalla " + Screen.width + "x" + Screen.height + ": ");
        foreach (CanvasScaler s in Object.FindObjectsByType<CanvasScaler>())
            if (s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) sb.Append(s.name).Append("=").Append(s.matchWidthOrHeight.ToString("0.#")).Append(" ");
        return sb.ToString();
    }
}
