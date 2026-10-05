using UnityEngine;

// Estados alterados del héroe. Por ahora, veneno: daño periódico que ignora la armadura y tiñe los números de verde.
public class PlayerAfflictions : MonoBehaviour
{
    public static readonly Color PoisonColor = new Color(0.45f, 0.95f, 0.3f);

    private CharacterStats _stats;
    private float _poisonDps, _poisonUntil, _tickAt;

    public bool Poisoned => Time.time < _poisonUntil && _stats != null && !_stats.IsDead;
    public float PoisonLeft => Mathf.Max(0f, _poisonUntil - Time.time);

    public static PlayerAfflictions On(CharacterStats stats)
    {
        if (stats == null) return null;
        PlayerAfflictions a = stats.GetComponent<PlayerAfflictions>();
        return a != null ? a : stats.gameObject.AddComponent<PlayerAfflictions>();
    }

    void Awake() => _stats = GetComponent<CharacterStats>();

    public void ApplyPoison(float dps, float seconds)
    {
        if (_stats == null || _stats.IsDead) return;
        _poisonDps = Mathf.Max(Poisoned ? _poisonDps : 0f, dps);
        _poisonUntil = Mathf.Max(_poisonUntil, Time.time + seconds);
        if (_tickAt < Time.time) _tickAt = Time.time + 0.5f;
    }

    public void Cure() => _poisonUntil = 0f;

    void Update()
    {
        if (!Poisoned || Time.time < _tickAt) return;
        _tickAt = Time.time + 0.5f;
        if (_stats.invulnerable) return;
        float amount = _poisonDps * 0.5f;
        float before = _stats.currentHealth;
        // El veneno no puede matar de un tic si queda muy poca vida: deja 1 punto (al estilo Souls, el golpe mata, el veneno debilita).
        _stats.currentHealth = Mathf.Max(1f, _stats.currentHealth - amount);
        float taken = before - _stats.currentHealth;
        if (taken > 0f && WorldHUD.Instance != null)
            WorldHUD.Instance.Popup(transform.position + Vector3.up * 1.7f + Random.insideUnitSphere * 0.2f, Mathf.CeilToInt(taken).ToString(), PoisonColor, 0.75f);
    }
}
