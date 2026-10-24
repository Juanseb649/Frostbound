using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Muerte del héroe: el cuerpo cae en ragdoll, la cámara se ralentiza un instante y aparece la pantalla
// "HAS MUERTO". El jugador elige reaparecer en la hoguera encendida más cercana al lugar donde cayó
// (o en el poblado si aún no encendió ninguna; el equipo pierde durabilidad) o volver al título.
[RequireComponent(typeof(CharacterStats))]
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("Dónde reaparece si no hay ninguna hoguera en la escena. Vacío = donde empezó la partida.")]
    public Transform respawnPoint;
    [Tooltip("Segundos de cuerpo en el suelo antes de mostrar la pantalla de muerte.")]
    public float screenDelay = 1.2f;
    [Range(0f, 1f)] public float durabilityLoss = 0.05f;

    private CharacterStats _stats;
    private Rigidbody _rb;
    private PenguinRagdoll _ragdoll;
    private Vector3 _start;
    private Quaternion _startRot;
    private bool _dying;
    private Vector3 _deathPosition;
    private DeathScreen _screen;
    private readonly System.Collections.Generic.List<Behaviour> _paused = new System.Collections.Generic.List<Behaviour>();
    private readonly System.Collections.Generic.List<Canvas> _hiddenHud = new System.Collections.Generic.List<Canvas>();

    public bool IsDying => _dying;

    void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _rb = GetComponent<Rigidbody>();
        _start = transform.position;
        _startRot = transform.rotation;
    }

    void OnEnable() => _stats.Died += OnDied;
    void OnDisable() => _stats.Died -= OnDied;

    private void OnDied()
    {
        if (_dying) return;
        StartCoroutine(Die());
    }

    [Tooltip("Co-op: segundos hasta que un héroe caído se levanta junto a su compañero.")]
    public float coopReviveSeconds = 10f;

    private bool _blocked;
    private bool _awaitingParty;
    public bool AwaitingParty => _awaitingParty;

    private PlayerController Me => GetComponent<PlayerController>();
    private bool IsSecond => Me != null && Me.playerIndex > 0;

    private static PlayerController AlivePartner(PlayerController me)
    {
        foreach (PlayerController p in Players.All)
            if (p != null && p != me && Players.IsAlive(p)) return p;
        return null;
    }

    private IEnumerator Die()
    {
        _dying = true;
        _deathPosition = transform.position;
        bool coop = AlivePartner(Me) != null;
        _blocked = !coop;
        if (_blocked) GameplayInput.Block();
        if (!coop) CloseMenus();
        PauseControl();

        Vector3 dir = _stats.lastHitDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
        dir.Normalize();
        Vector3 carried = _rb != null ? new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z) * 0.5f : Vector3.zero;
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }
        if (_ragdoll == null) _ragdoll = GetComponent<PenguinRagdoll>();
        if (_ragdoll == null) _ragdoll = gameObject.AddComponent<PenguinRagdoll>();
        _ragdoll.Activate(carried + dir * 1.4f + Vector3.up * 0.8f, dir * 3.8f + Vector3.up * 1.4f);

        Time.timeScale = 0.35f;
        yield return new WaitForSecondsRealtime(0.7f);
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(screenDelay);

        // Co-op: mientras el compañero siga en pie, el caído se levanta a su lado al cabo de unos segundos.
        string who = IsSecond ? "El jugador 2" : "El jugador 1";
        float waited = 0f;
        bool announced = false;
        while (AlivePartner(Me) != null && waited < coopReviveSeconds)
        {
            if (!announced) { Notifications.Show(who + " ha caído. Se levantará en " + Mathf.RoundToInt(coopReviveSeconds) + " s si su compañero aguanta.", FrostboundUI.Negative); announced = true; }
            waited += Time.deltaTime;
            yield return null;
        }
        PlayerController partner = AlivePartner(Me);
        if (announced && partner != null)
        {
            ReviveNear(partner.transform, 0.5f);
            yield break;
        }
        // Todo el grupo cayó. El jugador 2 espera a que el jugador 1 elija dónde reaparecer.
        if (IsSecond)
        {
            _awaitingParty = true;
            yield break;
        }
        if (!_blocked) { _blocked = true; GameplayInput.Block(); CloseMenus(); }

        UISkin skin = null;
        GameHUD hud = FindAnyObjectByType<GameHUD>();
        if (hud != null) skin = hud.skin;
        SetHudVisible(false, hud);
        _screen = DeathScreen.Show(skin, () => StartCoroutine(Respawn()), ReturnToTitle);
    }

    private IEnumerator Respawn()
    {
        _ragdoll.Deactivate();

        Vector3 pos = respawnPoint != null ? respawnPoint.position : _start;
        Quaternion rot = respawnPoint != null ? respawnPoint.rotation : _startRot;
        Bonfire bonfire = Bonfire.NearestLit(_deathPosition);
        if (bonfire != null)
        {
            pos = bonfire.SpawnPoint;
            rot = bonfire.SpawnRotation;
        }
        if (GameSession.Instance != null) GameSession.Instance.SetLastBonfire(bonfire != null ? bonfire.id : "");
        if (_rb != null)
        {
            _rb.position = pos;
            _rb.rotation = rot;
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(pos, rot);

        Equipment eq = GetComponent<Equipment>();
        if (eq != null)
        {
            foreach (EquipSlot slot in Equipment.Slots)
            {
                ItemStack s = eq.GetStack(slot);
                // Morir desgasta el equipo, pero nunca lo rompe ni lo quita: el héroe despierta con todo lo que llevaba.
                if (s == null || s.item == null || !s.item.HasDurability) continue;
                s.EnsureInstance();
                if (s.durability > 1f)
                    s.durability = Mathf.Max(1f, s.durability - s.item.maxDurability * durabilityLoss);
            }
            eq.Apply();
        }

        _stats.currentHealth = 1f;
        _stats.Heal(_stats.MaxHealth);
        _stats.RestoreMana(_stats.MaxMana);
        _stats.lastHitDirection = Vector3.zero;
        PlayerAfflictions afflictions = GetComponent<PlayerAfflictions>();
        if (afflictions != null) afflictions.Cure();
        ResumeControl();
        SetHudVisible(true, null);

        // Los compañeros caídos despiertan con él. Se recorre una copia: al revivir, su PlayerController se vuelve
        // a activar y se registra otra vez en Players.All, y modificar la lista durante el foreach cortaba la
        // corrutina (pantalla negra y controles bloqueados).
        foreach (PlayerController p in new System.Collections.Generic.List<PlayerController>(Players.All))
        {
            if (p == null || p.transform == transform) continue;
            PlayerRespawn other = p.GetComponent<PlayerRespawn>();
            if (other != null && other.IsDying) other.ReviveNear(transform, 1f);
        }

        yield return null;
        if (_screen != null) yield return _screen.FadeAwayAndDestroy();
        _screen = null;
        if (_blocked) GameplayInput.Unblock();
        _blocked = false;
        _dying = false;
        ReleaseInput();
        if (GameSession.Instance != null) GameSession.Instance.SaveNow();
        Notifications.Show(bonfire != null ? "Despiertas junto a la hoguera con todo tu equipo (algo desgastado)." : "Despiertas en el poblado con todo tu equipo (algo desgastado).", FrostboundUI.Muted);
    }

    // Levanta al héroe junto a otro (co-op). healthFraction: vida con la que vuelve.
    public void ReviveNear(Transform anchor, float healthFraction)
    {
        StopAllCoroutines();
        if (_screen != null) Destroy(_screen.gameObject);
        _screen = null;
        if (_ragdoll != null) _ragdoll.Deactivate();
        Vector3 side = anchor.right * (IsSecond ? 1.4f : -1.4f);
        Vector3 pos = anchor.position + side;
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) pos = hit.position + Vector3.up * 0.05f;
        if (_rb != null)
        {
            _rb.position = pos;
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
        }
        transform.position = pos;
        _stats.currentHealth = 1f;
        _stats.Heal(_stats.MaxHealth * Mathf.Clamp01(healthFraction) - 1f);
        _stats.lastHitDirection = Vector3.zero;
        PlayerAfflictions afflictions = GetComponent<PlayerAfflictions>();
        if (afflictions != null) afflictions.Cure();
        ResumeControl();
        if (_blocked) GameplayInput.Unblock();
        _blocked = false;
        _awaitingParty = false;
        _dying = false;
        Notifications.Show((IsSecond ? "El jugador 2" : "El jugador 1") + " vuelve a la lucha.", FrostboundUI.Positive);
    }

    private void ReturnToTitle()
    {
        Time.timeScale = 1f;
        GameplayInput.Unblock();
        Bonfire bonfire = Bonfire.NearestLit(_deathPosition);
        if (GameSession.Instance != null)
        {
            GameSession.Instance.SetLastBonfire(bonfire != null ? bonfire.id : "");
            GameSession.Instance.EndGame();
        }
        SceneManager.LoadScene(SceneIds.MainMenu);
    }

    private void SetHudVisible(bool visible, GameHUD hud)
    {
        if (visible)
        {
            foreach (Canvas c in _hiddenHud) if (c != null) c.enabled = true;
            _hiddenHud.Clear();
            return;
        }
        _hiddenHud.Clear();
        Canvas[] canvases = { hud != null ? hud.GetComponentInParent<Canvas>() : null, WorldHUD.Instance != null ? WorldHUD.Instance.GetComponent<Canvas>() : null,
            AdventureHUD.Instance != null ? AdventureHUD.Instance.Canvas : null };
        foreach (Canvas c in canvases)
        {
            if (c == null || !c.enabled) continue;
            c.enabled = false;
            _hiddenHud.Add(c);
        }
    }

    private void CloseMenus()
    {
        if (WorldHUD.Instance != null) WorldHUD.Instance.CloseMenu();
        InventoryScreen inv = FindAnyObjectByType<InventoryScreen>();
        if (inv != null && inv.IsOpen) inv.Close();
    }

    private void PauseControl()
    {
        _paused.Clear();
        foreach (Behaviour b in new Behaviour[] { GetComponent<PlayerController>(), GetComponent<PlayerCombat>(), GetComponent<PlayerDodge>() })
        {
            if (b == null || !b.enabled) continue;
            b.enabled = false;
            _paused.Add(b);
        }
    }

    private void ResumeControl()
    {
        foreach (Behaviour b in _paused) if (b != null) b.enabled = true;
        _paused.Clear();
        // Todo lo que da el control al héroe vuelve encendido y limpio, aunque algo lo apagara durante la muerte.
        foreach (Behaviour b in new Behaviour[] { GetComponent<PlayerController>(), GetComponent<PlayerCombat>(), GetComponent<PlayerDodge>(), GetComponent<PlayerAbility>() })
            if (b != null) b.enabled = true;
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null) pc.ResetControl(transform.rotation);
        PlayerCombat combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.ResetState();
        if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    // Al terminar de reaparecer el grupo entero no hay menús abiertos: suelta cualquier bloqueo que quedara colgado.
    private static void ReleaseInput()
    {
        if (PauseMenu.IsPaused || TradeWindow.IsOpen || LoadingScreen.Active) return;
        InventoryScreen inv = FindAnyObjectByType<InventoryScreen>();
        if (inv != null && inv.IsOpen) return;
        GameplayInput.Clear();
    }
}
