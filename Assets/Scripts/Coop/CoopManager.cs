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
                    if (InputMap.PadPressed(g, GameAction.Pause) && IsJoinPad(g)) { OpenPicker(g); break; }
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
        if (c == null || !c.active) return;
        Gamepad pad = null;
        foreach (Gamepad g in Gamepad.all) if (!PlayerController.Claimed(g) && g != PlayerController.RecentP1Pad) { pad = g; break; }
        if (pad == null) return;
        if (c.slot >= 0 && c.slot != CurrentSlot && SaveSystem.Exists(c.slot))
        {
            SaveData d = SaveSystem.Load(c.slot);
            if (d != null) { Spawn(pad, FromSave(d), true, c.slot); return; }
        }
        if (c.Exists) Spawn(pad, c, true, -1);
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
        if (InputMap.PadHeld(g, GameAction.Inventory))
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

        if (InputMap.PadPressed(g, GameAction.Inventory) && !PauseMenu.IsPaused)
        {
            InventoryScreen inv = FindAnyObjectByType<InventoryScreen>();
            Equipment eq = _p2.GetComponent<Equipment>();
            // Abrir aquí; cerrar lo hace la propia mochila con el SELECT de su dueño.
            if (inv != null && eq != null && !inv.IsOpen && inv.ClosedFrame != Time.frameCount) inv.OpenFor(eq);
        }
    }

    // ---------- Entrar y salir ----------

    public PlayerController Spawn(Gamepad pad, CompanionData data, bool restore, int slot = -1)
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
        _p2Slot = slot;

        CompanionData saved = Saved;
        if (saved != null)
        {
            if (slot < 0 && saved != data) CopyIdentity(data, saved);
            saved.slot = slot;
            saved.active = true;
        }
        StartCoroutine(AttachHud(pc));
        DuplicateEnemies();
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
        SaveSecond(Saved);
        CompanionData c = Saved;
        if (c != null) c.active = false;
        Destroy(_p2.gameObject);
        _p2 = null;
        DetachHud();
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

    private int _p2Slot = -1;

    private void Capture(SaveData data)
    {
        if (data == null || _p2 == null) return;
        SaveSecond(data.companion);
        data.companion.active = true;
    }

    // El héroe del jugador 2 se guarda en su propia ranura; si no tiene, dentro de la partida del jugador 1.
    private void SaveSecond(CompanionData companion)
    {
        if (_p2 == null) return;
        Equipment eq = _p2.GetComponent<Equipment>();
        if (_p2Slot >= 0)
        {
            var tmp = new CompanionData();
            CaptureInto(eq, tmp);
            SaveData d = SaveSystem.Load(_p2Slot) ?? new SaveData { slot = _p2Slot, zone = SceneIds.Village };
            ToSave(tmp, d);
            SaveSystem.Save(d);
            if (companion != null) companion.slot = _p2Slot;
        }
        else if (companion != null)
        {
            CaptureInto(eq, companion);
            companion.slot = -1;
        }
    }

    public static CompanionData FromSave(SaveData d)
    {
        return new CompanionData
        {
            slot = d.slot, heroName = d.heroName, classId = d.classId, plumageId = d.plumageId,
            level = d.level, experience = d.experience, pointsAvailable = d.pointsAvailable, allocated = d.allocated,
            health = d.health, mana = d.mana, coins = d.coins,
            inventory = new List<SavedStack>(d.inventory), equipment = new List<SavedStack>(d.equipment), belt = new List<string>(d.belt)
        };
    }

    private static void ToSave(CompanionData c, SaveData d)
    {
        d.heroName = c.heroName;
        d.classId = c.classId;
        d.plumageId = c.plumageId;
        d.level = c.level;
        d.experience = c.experience;
        d.pointsAvailable = c.pointsAvailable;
        d.allocated = c.allocated;
        d.health = c.health;
        d.mana = c.mana;
        d.coins = c.coins;
        d.inventory = new List<SavedStack>(c.inventory);
        d.equipment = new List<SavedStack>(c.equipment);
        d.belt = new List<string>(c.belt);
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
    // Primero los héroes guardados en las otras ranuras (y el compañero de esta partida); al final, la tarjeta «+».
    // Con «+» se elige clase y se crea un héroe nuevo en una ranura libre.

    private enum PickKind { Slot, Companion, Create, Class }

    private class PickOption
    {
        public PickKind kind;
        public int slot = -1;
        public string title, detail;
        public CompanionData data;
        public CharacterClass cls;
    }

    private bool _pickerOpen;
    private Gamepad _pickerPad;
    private readonly List<PickOption> _options = new List<PickOption>();
    private int _pick;
    private bool _classStep;
    private RectTransform _picker;
    private TextMeshProUGUI _pickerSub;
    private readonly List<(Image frame, TextMeshProUGUI title, TextMeshProUGUI detail)> _cards = new List<(Image, TextMeshProUGUI, TextMeshProUGUI)>();
    private float _navAt;

    private static int CurrentSlot => GameSession.Instance != null ? GameSession.Instance.CurrentSlot : -1;

    public void OpenPicker(Gamepad pad)
    {
        GameDatabase db = GameDatabase.Instance;
        if (db == null || db.classes.Count == 0) return;
        _pickerPad = pad;
        _pickerOpen = true;
        GameplayInput.Block();
        ShowHeroStep();
    }

    private void ShowHeroStep()
    {
        GameDatabase db = GameDatabase.Instance;
        _classStep = false;
        _options.Clear();
        for (int slot = 0; slot < SaveSystem.SlotCount; slot++)
        {
            if (slot == CurrentSlot || !SaveSystem.Exists(slot)) continue;
            SaveData d = SaveSystem.Load(slot);
            CharacterClass c = d != null ? db.FindClass(d.classId) : null;
            if (c == null) continue;
            _options.Add(new PickOption
            {
                kind = PickKind.Slot, slot = slot, data = FromSave(d),
                title = "RANURA " + (slot + 1),
                detail = (string.IsNullOrWhiteSpace(d.heroName) ? c.displayName : d.heroName) + "\n<size=16><color=#9FB3C8>" + c.displayName + " · nivel " + d.level + "</color></size>"
            });
        }
        CompanionData saved = Saved;
        if (saved != null && saved.Exists && saved.slot < 0)
        {
            CharacterClass c = db.FindClass(saved.classId);
            if (c != null)
                _options.Add(new PickOption
                {
                    kind = PickKind.Companion, data = saved, title = "COMPAÑERO",
                    detail = (string.IsNullOrWhiteSpace(saved.heroName) ? c.displayName : saved.heroName) + "\n<size=16><color=#9FB3C8>" + c.displayName + " · nivel " + saved.level + "</color></size>"
                });
        }
        _options.Add(new PickOption { kind = PickKind.Create, title = "NUEVO HÉROE", detail = "<size=72>+</size>\n<size=16><color=#9FB3C8>Crear y unirse</color></size>" });
        _pick = 0;
        BuildPicker("Elige un héroe guardado o crea uno nuevo · A entrar · B cancelar");
    }

    private void ShowClassStep()
    {
        GameDatabase db = GameDatabase.Instance;
        _classStep = true;
        _options.Clear();
        foreach (CharacterClass c in db.classes)
        {
            if (c == null) continue;
            _options.Add(new PickOption { kind = PickKind.Class, cls = c, title = "CLASE", detail = c.displayName + "\n<size=16><color=#9FB3C8>empieza en nivel 1</color></size>" });
        }
        _pick = 0;
        BuildPicker("Elige la clase del nuevo héroe · A crear · B volver");
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
        if (g.buttonEast.wasPressedThisFrame)
        {
            if (_classStep) ShowHeroStep();
            else ClosePicker();
            return;
        }
        if (!g.buttonSouth.wasPressedThisFrame && !g.startButton.wasPressedThisFrame) return;
        PickOption o = _options[_pick];
        switch (o.kind)
        {
            case PickKind.Create:
                ShowClassStep();
                return;
            case PickKind.Class:
                ClosePicker();
                CreateAndJoin(g, o.cls);
                return;
            case PickKind.Slot:
                ClosePicker();
                Spawn(g, o.data, true, o.slot);
                return;
            default:
                ClosePicker();
                Spawn(g, o.data, true, -1);
                return;
        }
    }

    // Héroe nuevo en la primera ranura libre (si no queda ninguna, vive dentro de esta partida).
    private void CreateAndJoin(Gamepad pad, CharacterClass cls)
    {
        GameDatabase db = GameDatabase.Instance;
        string plumage = db.palette != null && db.palette.entries.Count > 0
            ? db.palette.entries[(db.classes.IndexOf(cls) * 3 + 2) % db.palette.entries.Count].id : "";
        var data = new CompanionData { classId = cls.name, plumageId = plumage, heroName = cls.displayName };
        int free = -1;
        for (int s = 0; s < SaveSystem.SlotCount; s++)
            if (s != CurrentSlot && !SaveSystem.Exists(s)) { free = s; break; }
        if (free >= 0)
        {
            var save = new SaveData
            {
                slot = free,
                worldSeed = GameSession.Instance != null ? GameSession.Instance.worldSeed : Random.Range(1, int.MaxValue),
                heroName = data.heroName,
                classId = data.classId,
                plumageId = data.plumageId,
                zone = SceneIds.Village
            };
            SaveSystem.Save(save);
            Notifications.Show("Héroe nuevo guardado en la ranura " + (free + 1), FrostboundUI.Muted);
        }
        else Notifications.Show("No quedan ranuras libres: el héroe del jugador 2 se guardará dentro de esta partida", FrostboundUI.Muted);
        Spawn(pad, data, false, free);
    }

    private void BuildPicker(string help)
    {
        if (_picker != null) Destroy(_picker.gameObject);
        _cards.Clear();
        _picker = FrostUI.Node(_canvas.transform, "Coop_Eleccion");
        FrostUI.Stretch(_picker);
        Image dim = FrostUI.Img(_picker, "Velo", null, new Color(0.02f, 0.04f, 0.08f, 0.78f));
        FrostUI.Stretch(dim.rectTransform);
        TextMeshProUGUI title = FrostUI.Text(_picker, "Titulo", "JUGADOR 2", 46f, FrostboundUI.Title, TextAlignmentOptions.Center, FrostUI.Display);
        FrostUI.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(800f, 60f));
        title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _pickerSub = FrostUI.Text(_picker, "Sub", help, 18f, FrostboundUI.Muted, TextAlignmentOptions.Center);
        FrostUI.Place(_pickerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1000f, 30f));
        _pickerSub.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        float w = 200f, gap = 16f;
        float total = _options.Count * w + (_options.Count - 1) * gap;
        float scale = Mathf.Min(1f, 1300f / Mathf.Max(1f, total));
        for (int i = 0; i < _options.Count; i++)
        {
            Image frame = FrostUI.Img(_picker, "Opcion_" + i, null, FrostboundUI.Border);
            FrostUI.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((-total / 2f + w / 2f + i * (w + gap)) * scale, 10f), new Vector2(w, 190f));
            frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Image fill = FrostUI.Img(frame.transform, "Fondo", null, FrostboundUI.Surface);
            FrostUI.Stretch(fill.rectTransform, 3f);
            TextMeshProUGUI t = FrostUI.Text(frame.transform, "Tipo", _options[i].title, 15f, FrostboundUI.Gold, TextAlignmentOptions.Center);
            FrostUI.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(w - 20f, 26f));
            TextMeshProUGUI d = FrostUI.Text(frame.transform, "Detalle", _options[i].detail, 21f, FrostboundUI.Text, TextAlignmentOptions.Center);
            d.textWrappingMode = TextWrappingModes.Normal;
            FrostUI.Place(d.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(w - 20f, 130f));
            d.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _cards.Add((frame, t, d));
        }
        RefreshPicker();
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

    // ---------- HUD ----------
    // El jugador 2 recibe un HUD de orbes igual al del jugador 1; los dos se encogen y cada uno va a una esquina.

    private Canvas _canvas;
    private TextMeshProUGUI _hint;
    private GameHUD _hud2;

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

        _hint = FrostUI.Text(root.transform, "Aviso", "Mando: pulsa START para que entre el JUGADOR 2", 16f, FrostboundUI.Ice, TextAlignmentOptions.Center);
        FrostUI.Place(_hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(700f, 26f));
        _hint.rectTransform.pivot = new Vector2(0.5f, 1f);
        _hint.gameObject.SetActive(false);
    }

    private static GameHUD MainHud()
    {
        foreach (GameHUD h in FindObjectsByType<GameHUD>()) if (!h.secondary) return h;
        return null;
    }

    private IEnumerator AttachHud(PlayerController p2)
    {
        GameHUD main = MainHud();
        if (main == null) yield break;
        var go = new GameObject("HUD_Jugador2", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        var src = (RectTransform)main.transform;
        rt.SetParent(src.parent, false);
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.pivot = src.pivot;
        rt.offsetMin = src.offsetMin;
        rt.offsetMax = src.offsetMax;
        rt.localScale = src.localScale;
        go.SetActive(false);
        _hud2 = go.AddComponent<GameHUD>();
        _hud2.secondary = true;
        _hud2.skin = main.skin;
        _hud2.player = p2.GetComponent<Equipment>();
        _hud2.inventoryScreen = main.inventoryScreen;
        _hud2.orbStatueScale = main.orbStatueScale;
        _hud2.showDebugHints = false;
        go.SetActive(true);
        yield return null;
        LayoutHuds();
    }

    private void LayoutHuds()
    {
        GameHUD main = MainHud();
        bool coop = _p2 != null && _hud2 != null;
        if (main != null) main.SetCoop(coop, false, "J1 · " + HeroName(_p1 != null ? _p1.GetComponent<PlayerController>() : null));
        if (_hud2 != null) _hud2.SetCoop(true, true, "J2 · " + HeroName(_p2));
    }

    private static string HeroName(PlayerController p)
    {
        if (p == null) return "";
        PlayerLoadout lo = p.GetComponent<PlayerLoadout>();
        if (lo != null && !string.IsNullOrWhiteSpace(lo.overrideName)) return lo.overrideName.Trim();
        if (p.playerIndex == 0 && GameSession.Instance != null && !string.IsNullOrWhiteSpace(GameSession.Instance.HeroName)) return GameSession.Instance.HeroName.Trim();
        CharacterStats st = p.Stats;
        return st != null && st.characterClass != null ? st.characterClass.displayName : "Pingüino";
    }

    private void DetachHud()
    {
        if (_hud2 != null) Destroy(_hud2.gameObject);
        _hud2 = null;
        GameHUD main = MainHud();
        if (main != null) main.SetCoop(false, false, "");
    }

    private void UpdateHud()
    {
        bool hint = _p2 == null && Gamepad.all.Count > 0 && Time.unscaledTime - _sceneStart < 14f && !LoadingScreen.Active;
        if (_hint.gameObject.activeSelf != hint) _hint.gameObject.SetActive(hint);
        if (hint) _hint.text = "Mando: pulsa " + InputMap.PadName(InputMap.PadOf(GameAction.Pause)) + " para que entre el JUGADOR 2"
            + (InputMap.Preferred == InputMap.Device.Gamepad ? " (con un segundo mando)" : "");
    }

    // ---------- Enemigos ----------

    // Al unirse el jugador 2, cada corrupto vivo trae a un gemelo (y los campamentos nuevos ya salen dobles).
    private void DuplicateEnemies()
    {
        var current = new List<EnemyBrain>(EnemyBrain.All);
        int made = 0;
        foreach (EnemyBrain e in current)
        {
            if (e == null || e.definition == null || e.Current == EnemyBrain.State.Dead) continue;
            if (e.GetComponent<BossController>() != null) continue;
            Vector2 r = Random.insideUnitCircle.normalized * 1.6f;
            Vector3 pos = e.transform.position + new Vector3(r.x, 0f, r.y);
            EnemyBrain twin = e.Camp != null
                ? e.Camp.SpawnMember(e.definition, pos, e.Level, e.Plumage)
                : EnemySpawner.Spawn(e.definition, pos, Random.Range(0f, 360f), e.transform.parent, e.Level, false, new DeterministicRng(Random.Range(1, int.MaxValue)));
            if (twin == null) continue;
            made++;
            if (e.Current != EnemyBrain.State.Idle) twin.Alert();
        }
        if (made > 0) Notifications.Show("La corrupción siente a un segundo héroe: llegan " + made + " corruptos más", FrostboundUI.Negative);
    }
}
