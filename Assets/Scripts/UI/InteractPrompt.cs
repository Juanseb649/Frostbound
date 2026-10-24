using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Sobre el NPC más cercano a cada héroe aparece el botón con el que interactúa (B del mando, E del teclado, o lo que
// tenga asignado en Configuración → Control) y debajo, en letra pequeña, «Hablar». Mientras un NPC cuenta una misión,
// junto a la esquina de su globo aparece, más tenue, el mismo botón con «Saltar».
public class InteractPrompt : MonoBehaviour
{
    private class Prompt
    {
        public RectTransform root, glyphSlot;
        public TextMeshProUGUI label;
        public CanvasGroup group;
        public string glyphKey = "";
    }

    private Canvas _canvas;
    private readonly List<Prompt> _prompts = new List<Prompt>();
    private readonly List<NPCInteractable> _npcs = new List<NPCInteractable>();
    private float _rescanAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Ensure();
    }

    private static void OnLoaded(Scene s, LoadSceneMode m) => Ensure();

    private static void Ensure()
    {
        if (!SceneIds.IsGameplay(SceneManager.GetActiveScene().name)) return;
        if (FindAnyObjectByType<InteractPrompt>() != null) return;
        new GameObject("IndicadorInteractuar", typeof(RectTransform)).AddComponent<InteractPrompt>();
    }

    void Awake()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 12;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        ScreenAdapter.Match(scaler);
    }

    private Prompt Get(int i)
    {
        while (_prompts.Count <= i)
        {
            var p = new Prompt();
            p.root = FrostUI.Node(transform, "Indicador_" + _prompts.Count);
            p.root.anchorMin = p.root.anchorMax = Vector2.zero;
            p.root.pivot = new Vector2(0.5f, 0f);
            p.root.sizeDelta = new Vector2(120f, 64f);
            p.glyphSlot = FrostUI.Node(p.root, "Boton");
            p.glyphSlot.anchorMin = p.glyphSlot.anchorMax = p.glyphSlot.pivot = new Vector2(0.5f, 1f);
            p.glyphSlot.anchoredPosition = Vector2.zero;
            p.glyphSlot.sizeDelta = new Vector2(40f, 40f);
            p.label = FrostUI.Text(p.root, "Texto", "Hablar", 14f, Color.white, TextAlignmentOptions.Center);
            p.label.rectTransform.anchorMin = p.label.rectTransform.anchorMax = p.label.rectTransform.pivot = new Vector2(0.5f, 0f);
            p.label.rectTransform.anchoredPosition = Vector2.zero;
            p.label.rectTransform.sizeDelta = new Vector2(120f, 20f);
            p.label.outlineWidth = 0.25f;
            p.label.outlineColor = new Color32(5, 8, 14, 255);
            p.group = p.root.gameObject.AddComponent<CanvasGroup>();
            p.group.interactable = p.group.blocksRaycasts = false;
            _prompts.Add(p);
        }
        return _prompts[i];
    }

    // Se reutiliza en cada fotograma (crear uno nuevo cada vez genera basura).
    private readonly HashSet<NPCInteractable> _shown = new HashSet<NPCInteractable>();

    void LateUpdate()
    {
        if (Time.unscaledTime > _rescanAt)
        {
            _rescanAt = Time.unscaledTime + 1f;
            _npcs.Clear();
            _npcs.AddRange(FindObjectsByType<NPCInteractable>());
        }
        Camera cam = Camera.main;
        WorldHUD hud = WorldHUD.Instance;
        bool hideAll = cam == null || GameplayInput.Blocked || PauseMenu.IsPaused || LoadingScreen.Active;
        int used = 0;
        HashSet<NPCInteractable> shown = _shown;
        shown.Clear();
        if (!hideAll)
            foreach (PlayerController pc in Players.All)
            {
                if (pc == null || !Players.IsAlive(pc) || pc.InDialogue) continue;
                NPCInteractable npc = Telling(pc);
                bool skip = npc != null;
                if (!skip) npc = Nearest(pc);
                if (npc == null || shown.Contains(npc) || (hud != null && hud.MenuOpen && hud.MenuTarget == npc) || (npc.Busy && !skip)) continue;
                // El globo de la charla ocupa la cabeza: el aviso de saltar se cuelga de su esquina inferior derecha.
                Vector3 sp;
                if (skip)
                {
                    if (hud == null || !hud.BubbleCorner(npc.transform, out Vector2 corner)) continue;
                    sp = new Vector3(corner.x + 46f * _canvas.scaleFactor, corner.y - 10f * _canvas.scaleFactor, 1f);
                }
                else
                {
                    sp = cam.WorldToScreenPoint(npc.transform.position + Vector3.up * (npc.headHeight * npc.transform.lossyScale.y + 0.75f));
                    if (sp.z <= 0f) continue;
                }
                shown.Add(npc);
                Prompt p = Get(used++);
                p.root.gameObject.SetActive(true);
                float k = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
                p.root.anchoredPosition = new Vector2(sp.x / k, sp.y / k);
                p.label.text = skip ? "Saltar" : "Hablar";
                p.group.alpha = skip ? 0.9f : 1f;
                SetGlyph(p, pc);
            }
        for (int i = used; i < _prompts.Count; i++) _prompts[i].root.gameObject.SetActive(false);
    }

    private static NPCInteractable NearestOf(PlayerController pc, List<NPCInteractable> list)
    {
        NPCInteractable best = null;
        float bestD = float.MaxValue;
        foreach (NPCInteractable n in list)
        {
            if (n == null || !n.isActiveAndEnabled) continue;
            Vector3 d = n.transform.position - pc.transform.position;
            d.y = 0f;
            float reach = n.interactRange + 1.2f;
            float m = d.sqrMagnitude;
            if (m < reach * reach && m < bestD) { bestD = m; best = n; }
        }
        return best;
    }

    private NPCInteractable Nearest(PlayerController pc) => NearestOf(pc, _npcs);

    // El NPC que cuenta una misión, si este héroe está lo bastante cerca para saltarla.
    private static NPCInteractable Telling(PlayerController pc)
    {
        QuestGiver g = QuestGiver.Telling;
        if (g == null || g.Npc == null) return null;
        Vector3 d = g.transform.position - pc.transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= QuestGiver.SkipRange * QuestGiver.SkipRange ? g.Npc : null;
    }

    private static void SetGlyph(Prompt p, PlayerController pc)
    {
        bool pad = PlayerController.PadOf(pc) != null && !PlayerController.UsesKeyboard(pc);
        InputMap.Device d = pad ? InputMap.Device.Gamepad : InputMap.Device.KeyboardMouse;
        string key = d + ":" + (pad ? InputMap.PadOf(GameAction.Interact) : InputMap.KeyOf(GameAction.Interact));
        if (key == p.glyphKey) return;
        p.glyphKey = key;
        foreach (Transform c in p.glyphSlot) Destroy(c.gameObject);
        RectTransform g = InputGlyph.ForAction(p.glyphSlot, GameAction.Interact, d, 36f);
        g.anchorMin = g.anchorMax = g.pivot = new Vector2(0.5f, 0.5f);
        g.anchoredPosition = Vector2.zero;
    }
}
