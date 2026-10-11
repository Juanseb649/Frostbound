using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Co-op local (misma pantalla): un mando libre pulsa START, el jugador 2 elige héroe y entra junto al jugador 1.
// Su héroe (clase, nivel, equipo, mochila, cinturón y monedas) se guarda dentro de la partida del jugador 1
// y vuelve solo al cambiar de zona o al cargar la partida si hay un mando conectado.
// Mando del jugador 2: stick = moverse · X = atacar · RB = habilidad · A = rodar · Y = recoger/hablar ·
// cruceta = cinturón · SELECT = mochila (mantener 2 s = salir) · START = pausa.
public class CoopManager : MonoBehaviour
{
    public static CoopManager Instance { get; private set; }

    public const float LeashDistance = 24f;
    public const float LeaveHoldSeconds = 2f;

    private GameObject _p1;
    private GameObject _template;
    private Transform _holder;
    private PlayerController _p2;
    private float _sceneStart;
    private float _selectHeld;
    private bool _rejoinTried;

    // ---------- Ciclo de vida ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

    public static CoopManager Ensure()
    {
        if (Instance != null) return Instance;
        if (!SceneIds.IsGameplay(SceneManager.GetActiveScene().name)) return null;
        GameObject p1 = GameObject.Find("Player");
        if (p1 == null || p1.GetComponent<PlayerController>() == null) return null;
        var go = new GameObject("Coop");
        return go.AddComponent<CoopManager>();
    }

    void Awake()
    {
        Instance = this;
        _sceneStart = Time.unscaledTime;
        _p1 = GameObject.Find("Player");
        BuildTemplate();
        BuildHud();
        GameSession.BeforeSave += Capture;
    }

    void OnDestroy()
    {
        GameSession.BeforeSave -= Capture;
        if (_pickerOpen) GameplayInput.Unblock();
        if (Instance == this) Instance = null;
    }

    // Copia del jugador 1 antes de que se vista (su Start aún no corrió): de ahí sale el jugador 2.
    private void BuildTemplate()
    {
        if (_p1 == null) return;
        _holder = new GameObject("Coop_Plantilla").transform;
        _holder.SetParent(transform, false);
        _holder.gameObject.SetActive(false);
        _template = Instantiate(_p1, _holder);
        _template.name = "Player2_Plantilla";
        foreach (PlayerPersistence c in _template.GetComponentsInChildren<PlayerPersistence>(true)) DestroyImmediate(c);
        foreach (OutfitTester c in _template.GetComponentsInChildren<OutfitTester>(true)) DestroyImmediate(c);
        foreach (ProgressionDebug c in _template.GetComponentsInChildren<ProgressionDebug>(true)) DestroyImmediate(c);
        foreach (AudioListener c in _template.GetComponentsInChildren<AudioListener>(true)) DestroyImmediate(c);
    }

    public PlayerController Second => _p2;
    public bool HasSecond => _p2 != null;

    // Un mando con START une al jugador 2 si no es el que está usando el jugador 1.
    public static bool IsJoinPad(Gamepad g)
    {
        if (g == null || Instance == null || Instance._p2 != null || Instance._template == null) return false;
        if (PlayerController.Claimed(g)) return false;
        Gamepad p1 = PlayerController.RecentP1Pad;
        return p1 == null || p1 != g;
    }

    private static CompanionData Saved => GameSession.Instance != null && GameSession.Instance.Current != null ? GameSession.Instance.Current.companion : null;

    void Update()
    {
        if (_pickerOpen) { UpdatePicker(); return; }
        if (_p2 == null)
        {
            if (_p1 == null) _p1 = GameObject.Find("Player");
            TryAutoRejoin();
            if (!PauseMenu.IsPaused && !LoadingScreen.Active)
                foreach (Gamepad g in Gamepad.all)
                    if (g.startButton.wasPressedThisFrame && IsJoinPad(g)) { OpenPicker(g); break; }
        }
        else
        {
            SyncPhysics();
            Leash();
            UpdateSecondInput();
        }
        UpdateHud();
    }

    // Al llegar a una zona con el jugador 2 activo en la partida, vuelve a entrar con el primer mando libre.
    private void TryAutoRejoin()
    {
        if (_rejoinTried || Time.unscaledTime - _sceneStart < 0.4f) return;
        _rejoinTried = true;
        CompanionData c = Saved;
        if (c == null || !c.active || !c.Exists) return;
        Gamepad pad = null;
        foreach (Gamepad g in Gamepad.all) if (!PlayerController.Claimed(g) && g != PlayerController.RecentP1Pad) { pad = g; break; }
        if (pad == null) return;
        Spawn(pad, c, true);
    }

    // La copia nace con la física del jugador 1 en el momento de entrar a la zona (a veces quieta mientras se genera):
    // en cuanto el jugador 1 se mueve libre, el 2 también.
    private void SyncPhysics()
    {
        if (_p1 == null) return;
        Rigidbody a = _p1.GetComponent<Rigidbody>(), b = _p2.GetComponent<Rigidbody>();
        PlayerRespawn r = _p2.GetComponent<PlayerRespawn>();
        if (a == null || b == null || (r != null && r.IsDying)) return;
        if (b.isKinematic && !a.isKinematic) b.isKinematic = false;
    }

    private void Leash()
    {
        if (_p1 == null || !Players.IsAlive(_p2)) return;
        PlayerController p1 = _p1.GetComponent<PlayerController>();
        if (!Players.IsAlive(p1)) return;
        Vector3 d = _p2.transform.position - _p1.transform.position;
        if (Mathf.Abs(d.y) < 6f && new Vector2(d.x, d.z).magnitude < LeashDistance) return;
        Warp(_p2.gameObject, _p1.transform.position + _p1.transform.right * 1.4f);
        Notifications.Show("El jugador 2 se reúne con el jugador 1", FrostboundUI.Muted);
    }

    private static void Warp(GameObject hero, Vector3 pos)
    {
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) pos = hit.position + Vector3.up * 0.05f;
        Rigidbody rb = hero.GetComponent<Rigidbody>();
        if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; }
        hero.transform.position = pos;
    }

    // SELECT abre la mochila del jugador 2; mantenerlo 2 s le hace salir de la partida.
    private void UpdateSecondInput()
    {
        Gamepad g = _p2.pad;
        if (g == null || !g.added)
        {
            Notifications.Show("Se desconectó el mando del jugador 2", FrostboundUI.Negative);
            Leave();
            return;
        }
        if (g.selectButton.isPressed)
        {
            _selectHeld += Time.unscaledDeltaTime;
            if (_selectHeld >= LeaveHoldSeconds)
            {
                _selectHeld = 0f;
                InventoryScreen inv = FindAnyObjectByType<InventoryScreen>();
                if (inv != null && inv.IsOpen) inv.Close();
                Leave();
                return;
            }
        }
        else _selectHeld = 0f;

        if (g.selectButton.wasPressedThisFrame && !PauseMenu.IsPaused)
        {
            InventoryScreen inv = FindAnyObjectByType<InventoryScreen>();
            Equipment eq = _p2.GetComponent<Equipment>();
            // Abrir aquí; cerrar lo hace la propia mochila con el SELECT de su dueño.
            if (inv != null && eq != null && !inv.IsOpen && inv.ClosedFrame != Time.frameCount) inv.OpenFor(eq);
        }
    }

    // ---------- Entrar y salir ----------

    public PlayerController Spawn(Gamepad pad, CompanionData data, bool restore)
    {
        if (_template == null || _p2 != null || _p1 == null) return null;
        GameDatabase db = GameDatabase.Instance;
        CharacterClass cls = db != null ? db.FindClass(data.classId) : null;
        if (cls == null) return null;

        GameObject go = Instantiate(_template, _holder);
        go.name = "Player2";
        PlayerController pc = go.GetComponent<PlayerController>();
        pc.playerIndex = 1;
        pc.pad = pad;
        PlayerLoadout loadout = go.GetComponent<PlayerLoadout>();
        if (loadout != null)
        {
            loadout.overrideClass = cls;
            loadout.overridePlumage = data.plumageId;
            loadout.overrideName = data.heroName;
        }
        Equipment eq = go.GetComponent<Equipment>();
        if (go.GetComponent<PotionBelt>() == null) go.AddComponent<PotionBelt>();
        CompanionData snapshot = data;
        eq.restoreHook = e =>
        {
            e.Inventory.Clear();
            return restore && Restore(e, snapshot);
        };
        eq.finishHook = e => FinishRestore(e, snapshot);

        Vector3 pos = _p1.transform.position + _p1.transform.right * 1.5f;
        go.transform.SetParent(null, true);
        go.transform.rotation = _p1.transform.rotation;
        Warp(go, pos);
        _p2 = pc;

        CompanionData saved = Saved;
        if (saved != null && saved != data) CopyIdentity(data, saved);
        if (saved != null) saved.active = true;
        Notifications.Show("¡Entra el jugador 2: " + (string.IsNullOrWhiteSpace(data.heroName) ? cls.displayName : data.heroName) + "!", FrostboundUI.Gold);
        BonfireBanner.Show("JUGADOR 2", BonfireBanner.Victory);
        StartCoroutine(SaveSoon());
        return pc;
    }

    private IEnumerator SaveSoon()
    {
        yield return null;
        yield return null;
        if (GameSession.Instance != null) GameSession.Instance.SaveNow();
    }

    public void Leave()
    {
        if (_p2 == null) return;
        CompanionData c = Saved;
        if (c != null) { CaptureInto(_p2.GetComponent<Equipment>(), c); c.active = false; }
        Destroy(_p2.gameObject);
        _p2 = null;
        if (GameSession.Instance != null) GameSession.Instance.SaveNow();
        Notifications.Show("El jugador 2 se retira. Su héroe queda guardado en la partida.", FrostboundUI.Muted);
    }

    private static void CopyIdentity(CompanionData from, CompanionData to)
    {
        to.heroName = from.heroName;
        to.classId = from.classId;
        to.plumageId = from.plumageId;
        to.level = from.level;
        to.experience = from.experience;
        to.pointsAvailable = from.pointsAvailable;
        to.allocated = from.allocated;
        to.health = from.health;
        to.mana = from.mana;
        to.coins = from.coins;
        to.inventory = new List<SavedStack>(from.inventory);
        to.equipment = new List<SavedStack>(from.equipment);
        to.belt = new List<string>(from.belt);
    }

    // ---------- Guardado del jugador 2 ----------

    private void Capture(SaveData data)
    {
        if (data == null || _p2 == null) return;
        CaptureInto(_p2.GetComponent<Equipment>(), data.companion);
        data.companion.active = true;
    }

    private static void CaptureInto(Equipment eq, CompanionData c)
    {
        if (eq == null || c == null) return;
        CharacterStats stats = eq.Stats;
        PlayerLoadout loadout = eq.GetComponent<PlayerLoadout>();
        if (loadout != null && loadout.overrideClass != null)
        {
            c.classId = loadout.overrideClass.name;
            c.plumageId = loadout.overridePlumage ?? "";
            c.heroName = loadout.overrideName ?? "";
        }
        c.level = stats.level;
        c.experience = stats.experience;
        c.pointsAvailable = stats.pointsAvailable;
        c.allocated = stats.allocated;
        c.health = stats.IsDead ? stats.MaxHealth : stats.currentHealth;
        c.mana = stats.currentMana;
        Wallet wallet = eq.GetComponent<Wallet>();
        c.coins = wallet != null ? wallet.Coins : 0;
        c.inventory.Clear();
        Inventory inv = eq.Inventory;
        for (int i = 0; i < inv.Capacity; i++)
        {
            ItemStack s = inv.Get(i);
            if (s == null || s.item == null) continue;
            SavedStack d = SavedStack.From(s);
            d.index = i;
            c.inventory.Add(d);
        }
        c.equipment.Clear();
        foreach (EquipSlot slot in Equipment.Slots)
        {
            ItemStack s = eq.GetStack(slot);
            if (s == null || s.item == null) continue;
            SavedStack d = SavedStack.From(s);
            d.slot = slot;
            c.equipment.Add(d);
        }
        c.belt.Clear();
        PotionBelt belt = eq.GetComponent<PotionBelt>();
        if (belt != null) foreach (ItemDefinition item in belt.slots) c.belt.Add(item != null ? item.id : "");
    }

    private static bool Restore(Equipment eq, CompanionData c)
    {
        ItemDatabase db = GameDatabase.Instance != null ? GameDatabase.Instance.items : null;
        if (c == null || db == null) return false;
        CharacterStats stats = eq.Stats;
        stats.level = Mathf.Max(1, c.level);
        stats.experience = Mathf.Max(0, c.experience);
        stats.pointsAvailable = Mathf.Max(0, c.pointsAvailable);
        stats.allocated = c.allocated;
        Wallet.Of(eq.gameObject).Set(c.coins);
        foreach (SavedStack d in c.inventory)
        {
            ItemStack s = d.ToStack(db);
            if (s != null && !eq.Inventory.PutAt(d.index, s)) eq.Inventory.AddStack(s);
        }
        foreach (SavedStack d in c.equipment)
        {
            ItemStack s = d.ToStack(db);
            if (s != null) eq.SetEquippedStack(d.slot, s);
        }
        PotionBelt belt = eq.GetComponent<PotionBelt>();
        if (belt != null)
        {
            var slots = new ItemDefinition[PotionBelt.Size];
            for (int i = 0; i < slots.Length && i < c.belt.Count; i++)
                slots[i] = string.IsNullOrEmpty(c.belt[i]) ? null : db.Find(c.belt[i]);
            belt.Restore(slots);
        }
        return c.equipment.Count > 0 || c.inventory.Count > 0 || c.level > 1;
    }

    private static void FinishRestore(Equipment eq, CompanionData c)
    {
        CharacterStats stats = eq.Stats;
        stats.currentHealth = c.health > 0f ? Mathf.Min(stats.MaxHealth, c.health) : stats.MaxHealth;
        stats.currentMana = c.mana >= 0f ? Mathf.Min(stats.MaxMana, c.mana) : stats.MaxMana;
    }

    // ---------- Selección de héroe ----------

    private bool _pickerOpen;
    private Gamepad _pickerPad;
    private readonly List<(string label, string detail, CompanionData data, bool restore)> _options = new List<(string, string, CompanionData, bool)>();
    private int _pick;
    private RectTransform _picker;
    private readonly List<(Image frame, TextMeshProUGUI title, TextMeshProUGUI detail)> _cards = new List<(Image, TextMeshProUGUI, TextMeshProUGUI)>();
    private float _navAt;

    public void OpenPicker(Gamepad pad)
    {
        GameDatabase db = GameDatabase.Instance;
        if (db == null || db.classes.Count == 0) return;
        _options.Clear();
        CompanionData saved = Saved;
        if (saved != null && saved.Exists)
        {
            CharacterClass sc = db.FindClass(saved.classId);
            if (sc != null)
                _options.Add(("Continuar", (string.IsNullOrWhiteSpace(saved.heroName) ? sc.displayName : saved.heroName) + "\n" + sc.displayName + " · nivel " + saved.level, saved, true));
        }
        string[] plumages = { "rojo", "verde", "amarillo", "morado" };
        for (int i = 0; i < db.classes.Count; i++)
        {
            CharacterClass c = db.classes[i];
            if (c == null) continue;
            string plum = db.palette != null && db.palette.entries.Count > 0 ? db.palette.entries[(i * 3 + 2) % db.palette.entries.Count].id : plumages[i % plumages.Length];
            _options.Add(("Nuevo héroe", c.displayName + "\nempieza en nivel 1", new CompanionData { classId = c.name, plumageId = plum, heroName = c.displayName }, false));
        }
        _pickerPad = pad;
        _pick = 0;
        _pickerOpen = true;
        GameplayInput.Block();
        BuildPicker();
        RefreshPicker();
    }

    private void ClosePicker()
    {
        _pickerOpen = false;
        GameplayInput.Unblock();
        if (_picker != null) Destroy(_picker.gameObject);
        _picker = null;
        _cards.Clear();
    }

    private void UpdatePicker()
    {
        Gamepad g = _pickerPad;
        if (g == null || !g.added) { ClosePicker(); return; }
        float x = g.leftStick.ReadValue().x;
        int move = 0;
        if (g.dpad.left.wasPressedThisFrame) move = -1;
        else if (g.dpad.right.wasPressedThisFrame) move = 1;
        else if (Mathf.Abs(x) > 0.6f && Time.unscaledTime > _navAt) { move = x > 0 ? 1 : -1; _navAt = Time.unscaledTime + 0.25f; }
        if (Mathf.Abs(x) < 0.3f) _navAt = 0f;
        if (move != 0)
        {
            _pick = (_pick + move + _options.Count) % _options.Count;
            RefreshPicker();
        }
        if (g.buttonEast.wasPressedThisFrame) { ClosePicker(); return; }
        if (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame)
        {
            var o = _options[_pick];
            ClosePicker();
            Spawn(g, o.data, o.restore);
        }
    }

    private void BuildPicker()
    {
        _picker = FrostUI.Node(_canvas.transform, "Coop_Eleccion");
        FrostUI.Stretch(_picker);
        Image dim = FrostUI.Img(_picker, "Velo", null, new Color(0.02f, 0.04f, 0.08f, 0.72f));
        FrostUI.Stretch(dim.rectTransform);
        TextMeshProUGUI title = FrostUI.Text(_picker, "Titulo", "JUGADOR 2", 46f, FrostboundUI.Title, TextAlignmentOptions.Center, FrostUI.Display);
        FrostUI.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(800f, 60f));
        title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        TextMeshProUGUI sub = FrostUI.Text(_picker, "Sub", "Elige tu héroe con la cruceta · A para entrar · B para cancelar", 18f, FrostboundUI.Muted, TextAlignmentOptions.Center);
        FrostUI.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(900f, 30f));
        sub.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        float w = 190f, gap = 14f;
        float total = _options.Count * w + (_options.Count - 1) * gap;
        for (int i = 0; i < _options.Count; i++)
        {
            Image frame = FrostUI.Img(_picker, "Opcion_" + i, null, FrostboundUI.Border);
            FrostUI.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-total / 2f + w / 2f + i * (w + gap), 10f), new Vector2(w, 170f));
            frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Image fill = FrostUI.Img(frame.transform, "Fondo", null, FrostboundUI.Surface);
            FrostUI.Stretch(fill.rectTransform, 3f);
            TextMeshProUGUI t = FrostUI.Text(frame.transform, "Tipo", _options[i].label.ToUpperInvariant(), 15f, FrostboundUI.Gold, TextAlignmentOptions.Center);
            FrostUI.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(w - 20f, 26f));
            TextMeshProUGUI d = FrostUI.Text(frame.transform, "Detalle", _options[i].detail, 20f, FrostboundUI.Text, TextAlignmentOptions.Center);
            d.textWrappingMode = TextWrappingModes.Normal;
            FrostUI.Place(d.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(w - 20f, 100f));
            d.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _cards.Add((frame, t, d));
        }
    }

    private void RefreshPicker()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            bool on = i == _pick;
            _cards[i].frame.color = on ? FrostboundUI.Ice : FrostboundUI.Border;
            _cards[i].frame.rectTransform.localScale = Vector3.one * (on ? 1.08f : 1f);
            _cards[i].detail.color = on ? FrostboundUI.Title : FrostboundUI.Muted;
        }
    }

    // ---------- HUD del jugador 2 ----------

    private Canvas _canvas;
    private RectTransform _panel;
    private TextMeshProUGUI _name, _info, _hint;
    private RectTransform _hpFill, _mpFill, _xpFill;

    private void BuildHud()
    {
        var root = new GameObject("HUD_Coop", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 40;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(FrostboundUI.ReferenceWidth, FrostboundUI.ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;

        _panel = FrostUI.Node(root.transform, "Jugador2");
        FrostUI.Place(_panel, new Vector2(1f, 0f), new Vector2(-18f, 150f), new Vector2(300f, 104f));
        Image bg = FrostUI.Img(_panel, "Fondo", null, new Color(0.02f, 0.04f, 0.08f, 0.72f));
        FrostUI.Stretch(bg.rectTransform);
        Image accent = FrostUI.Img(_panel, "Acento", null, FrostboundUI.Gold);
        FrostUI.Place(accent.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, 104f));
        _name = FrostUI.Text(_panel, "Nombre", "", 17f, FrostboundUI.Title);
        FrostUI.Place(_name.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(276f, 24f));
        _hpFill = Bar("Vida", -38f, FrostboundUI.HealthBar, 12f);
        _mpFill = Bar("Mana", -55f, FrostboundUI.ManaBar, 9f);
        _xpFill = Bar("Exp", -69f, FrostboundUI.XpBar, 4f);
        _info = FrostUI.Text(_panel, "Info", "", 13f, FrostboundUI.Muted);
        FrostUI.Place(_info.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -78f), new Vector2(276f, 20f));

        _hint = FrostUI.Text(root.transform, "Aviso", "Mando: pulsa START para que entre el JUGADOR 2", 16f, FrostboundUI.Ice, TextAlignmentOptions.Center);
        FrostUI.Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 168f), new Vector2(700f, 26f));
        _hint.rectTransform.pivot = new Vector2(0.5f, 0f);
        _panel.gameObject.SetActive(false);
        _hint.gameObject.SetActive(false);
    }

    private RectTransform Bar(string name, float y, Color color, float height)
    {
        Image back = FrostUI.Img(_panel, name, null, new Color(0f, 0f, 0f, 0.55f));
        FrostUI.Place(back.rectTransform, new Vector2(0f, 1f), new Vector2(14f, y), new Vector2(272f, height));
        Image fill = FrostUI.Img(back.transform, "Relleno", null, color);
        RectTransform rt = fill.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private void UpdateHud()
    {
        bool hint = _p2 == null && Gamepad.all.Count > 0 && Time.unscaledTime - _sceneStart < 14f && !LoadingScreen.Active;
        if (_hint.gameObject.activeSelf != hint) _hint.gameObject.SetActive(hint);
        bool show = _p2 != null;
        if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);
        if (!show) return;
        CharacterStats st = _p2.Stats;
        if (st == null) return;
        PlayerLoadout lo = _p2.GetComponent<PlayerLoadout>();
        string heroName = lo != null && !string.IsNullOrWhiteSpace(lo.overrideName) ? lo.overrideName : st.characterClass != null ? st.characterClass.displayName : "Jugador 2";
        _name.text = "<color=#E0B44A>J2</color>  " + heroName + "  <size=13><color=#9FB3C8>" + (st.characterClass != null ? st.characterClass.displayName : "") + " · nv " + st.level
            + (st.pointsAvailable > 0 ? "  <color=#7FDB94>+" + st.pointsAvailable + " puntos</color>" : "") + "</color></size>";
        _hpFill.anchorMax = new Vector2(st.MaxHealth > 0f ? Mathf.Clamp01(st.currentHealth / st.MaxHealth) : 0f, 1f);
        _mpFill.anchorMax = new Vector2(st.MaxMana > 0f ? Mathf.Clamp01(st.currentMana / st.MaxMana) : 0f, 1f);
        _xpFill.anchorMax = new Vector2(st.LevelProgress, 1f);
        Inventory inv = _p2.GetComponent<Inventory>();
        int hp = 0, mp = 0, scrolls = 0;
        if (inv != null)
            for (int i = 0; i < inv.Capacity; i++)
            {
                ItemStack s = inv.Get(i);
                if (s == null || s.item == null || !s.item.IsConsumable) continue;
                if (s.item.townPortal) scrolls += s.quantity;
                else if (s.item.heal > 0f) hp += s.quantity;
                else if (s.item.restoreMana > 0f) mp += s.quantity;
            }
        Wallet w = _p2.GetComponent<Wallet>();
        _info.text = st.IsDead ? "<color=#F07A6E>Caído: se levantará junto a su compañero</color>"
            : "<color=#F07A6E>Vida ×" + hp + "</color>   <color=#4F8BFF>Maná ×" + mp + "</color>   Pergaminos ×" + scrolls + "   <color=#E0B44A>" + (w != null ? w.Coins : 0) + " monedas</color>";
    }
}
