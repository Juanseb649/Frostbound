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
    [Range(0f, 1f)] public float durabilityLoss = 0.1f;

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

    private IEnumerator Die()
    {
        _dying = true;
        _deathPosition = transform.position;
        GameplayInput.Block();
        CloseMenus();
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
                if (s != null && s.item != null && s.item.HasDurability) s.Wear(s.item.maxDurability * durabilityLoss);
            }
            eq.Apply();
        }

        _stats.currentHealth = 1f;
        _stats.Heal(_stats.MaxHealth);
        _stats.RestoreMana(_stats.MaxMana);
        _stats.lastHitDirection = Vector3.zero;
        ResumeControl();
        SetHudVisible(true, null);

        yield return null;
        if (_screen != null) yield return _screen.FadeAwayAndDestroy();
        _screen = null;
        GameplayInput.Unblock();
        _dying = false;
        if (GameSession.Instance != null) GameSession.Instance.SaveNow();
        Notifications.Show(bonfire != null ? "Despiertas junto a la hoguera. Tu equipo se desgastó un poco." : "Despiertas en el poblado. Tu equipo se desgastó un poco.", FrostboundUI.Muted);
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
        Canvas[] canvases = { hud != null ? hud.GetComponentInParent<Canvas>() : null, WorldHUD.Instance != null ? WorldHUD.Instance.GetComponent<Canvas>() : null };
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
    }
}
