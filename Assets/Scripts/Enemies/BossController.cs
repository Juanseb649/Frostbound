using System;
using System.Collections;
using UnityEngine;

// Convierte un enemigo en jefe: más vida y daño, barra de jefe, arma encantada (veneno opcional),
// teletransporte opcional y, al quedar casi sin vida, un discurso sin su maldición antes de caer.
[RequireComponent(typeof(EnemyBrain))]
public class BossController : MonoBehaviour
{
    public string title = "Jefe";
    public string subtitle = "";
    [TextArea] public string[] dyingLines = new string[0];
    public float poisonDps;
    public float poisonSeconds = 5f;
    public Color enchantColor = new Color(0.4f, 1f, 0.25f);
    public float blinkInterval;
    [Range(0f, 0.5f)] public float speechAt = 0.1f;

    public event Action<BossController> Defeated;
    public static BossController Active { get; private set; }

    public EnemyBrain Brain { get; private set; }
    public Damageable Health { get; private set; }
    public bool Speaking { get; private set; }
    public bool Engaged => Brain != null && Brain.Current != EnemyBrain.State.Idle;

    private float _nextBlink;
    private bool _warned;
    private Light _glow;
    private readonly System.Collections.Generic.List<(Renderer r, int m)> _weapon = new System.Collections.Generic.List<(Renderer, int)>();
    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // Copia la definición para ajustar al jefe sin tocar la de las tropas normales.
    public static BossController Make(EnemyBrain brain, string title, string subtitle, float healthMult, float damageMult, float scale,
        Action<EnemyDefinition> tune = null)
    {
        EnemyDefinition def = Instantiate(brain.definition);
        def.name = brain.definition.name + "_Jefe";
        tune?.Invoke(def);
        brain.definition = def;
        var boss = brain.gameObject.AddComponent<BossController>();
        boss.title = title;
        boss.subtitle = subtitle;
        brain.damageMultiplier = damageMult;
        brain.leashOverride = 60f;
        Damageable d = brain.GetComponent<Damageable>();
        d.maxHealth *= healthMult;
        d.ResetHealth();
        d.displayName = title;
        brain.transform.localScale *= scale;
        NameTag tag = brain.GetComponent<NameTag>();
        if (tag != null) tag.displayName = title;
        return boss;
    }

    void Awake()
    {
        Brain = GetComponent<EnemyBrain>();
        Health = GetComponent<Damageable>();
        _mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        Active = this;
        Brain.HitPlayerEvent += OnHitPlayer;
        Brain.Killed += OnKilled;
        if (dyingLines != null && dyingLines.Length > 0) Health.healthFloor = Mathf.Max(1f, Health.maxHealth * speechAt);
        if (poisonDps > 0f) Enchant();
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
        if (Brain != null)
        {
            Brain.HitPlayerEvent -= OnHitPlayer;
            Brain.Killed -= OnKilled;
        }
    }

    private void Enchant()
    {
        // El arma: primero los materiales de arma; si el equipo no tiene, el hierro.
        foreach (string key in new[] { "Weapon", "Iron" })
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                    if (mats[m] != null && mats[m].name.Contains(key)) _weapon.Add((r, m));
            }
            if (_weapon.Count > 0) break;
        }
        var go = new GameObject("Brillo_Veneno");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0.45f, 0.9f, 0.3f);
        _glow = go.AddComponent<Light>();
        _glow.type = LightType.Point;
        _glow.color = enchantColor;
        _glow.range = 4.5f;
        _glow.intensity = 2.2f;
        _glow.shadows = LightShadows.None;
    }

    void Update()
    {
        if (Health.IsDead) return;
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 4.2f);
        if (_glow != null) _glow.intensity = 1.6f + pulse * 1.4f;
        foreach (var (r, m) in _weapon)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb, m);
            _mpb.SetColor(EmissionId, enchantColor * (1.2f + pulse * 1.6f));
            _mpb.SetColor(BaseColorId, Color.Lerp(Color.white, enchantColor, 0.55f));
            r.SetPropertyBlock(_mpb, m);
        }

        if (!Speaking && Health.healthFloor > 0f && Health.Health <= Health.healthFloor + 0.01f)
        {
            StartCoroutine(Speech());
            return;
        }

        if (blinkInterval > 0f && !Speaking && Engaged && Time.time >= _nextBlink)
        {
            _nextBlink = Time.time + blinkInterval * UnityEngine.Random.Range(0.8f, 1.25f);
            Transform p = EnemyBrain.Player;
            if (p != null && (p.position - transform.position).magnitude < 16f && Time.time > 3f)
            {
                Vector3 behind = p.position - p.forward * 2.2f;
                if (UnityEngine.AI.NavMesh.SamplePosition(behind, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 1.4f, "...", new Color(0.55f, 0.5f, 0.9f), 1f);
                    Brain.Blink(hit.position);
                    transform.rotation = Quaternion.LookRotation((p.position - hit.position).normalized);
                    Brain.ForceAttack();
                }
            }
        }
    }

    private void OnHitPlayer(CharacterStats stats)
    {
        if (poisonDps <= 0f) return;
        PlayerAfflictions.On(stats).ApplyPoison(poisonDps, poisonSeconds);
        if (!_warned && WorldHUD.Instance != null)
        {
            _warned = true;
            WorldHUD.Instance.Popup(stats.transform.position + Vector3.up * 2.1f, "¡Envenenado!", PlayerAfflictions.PoisonColor, 1.2f);
        }
    }

    // Casi muerto: la maldición se retira, se arrodilla y habla. Luego cae.
    private IEnumerator Speech()
    {
        Speaking = true;
        Brain.paused = true;
        Health.invulnerable = true;
        if (_glow != null) _glow.enabled = false;

        Transform model = Brain.model != null ? Brain.model : transform;
        Quaternion start = model.localRotation;
        Color from = Brain.Plumage, to = Uncorrupted(from);
        PenguinAppearance look = GetComponentInChildren<PenguinAppearance>();
        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 1.4f);
            model.localRotation = start * Quaternion.Euler(22f * k, 0f, 0f);
            if (look != null) look.SetPlumage(Color.Lerp(from, to, k));
            yield return null;
        }
        Health.RefreshBaseColors();

        Transform player = EnemyBrain.Player;
        foreach (string line in dyingLines)
        {
            float seconds = Mathf.Clamp(2.2f + line.Length * 0.045f, 3f, 6.5f);
            if (player != null)
            {
                Vector3 look2 = player.position - transform.position;
                look2.y = 0f;
                if (look2.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look2.normalized);
            }
            if (WorldHUD.Instance != null) WorldHUD.Instance.Say(transform, 1.55f * transform.lossyScale.y, line, seconds);
            yield return new WaitForSeconds(seconds + 0.2f);
        }
        model.localRotation = start;
        Brain.paused = false;
        Health.Kill(player != null ? player.gameObject : null, -transform.forward);
    }

    private static Color Uncorrupted(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        return Color.HSVToRGB(h, Mathf.Clamp01(s * 1.9f + 0.15f), Mathf.Clamp01(v * 1.45f + 0.1f));
    }

    private void OnKilled(EnemyBrain b)
    {
        // El encantamiento se apaga con su dueño.
        if (_glow != null) _glow.enabled = false;
        foreach (var (r, m) in _weapon)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb, m);
            _mpb.SetColor(EmissionId, Color.black);
            _mpb.SetColor(BaseColorId, Color.white);
            r.SetPropertyBlock(_mpb, m);
        }
        Defeated?.Invoke(this);
        if (Active == this) Active = null;
    }
}
