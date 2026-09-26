using System;
using System.Collections.Generic;
using UnityEngine;

public struct DamageInfo
{
    public float amount;
    public DamageType type;
    public GameObject source;
    public Vector3 direction;
    public bool periodic;
    public bool critical;
}

// Algo que recibe golpes: enemigos, muñecos de práctica, objetos rompibles.
// Lleva los estados de las runas: quemado, envenenado, ralentizado y congelado.
public class Damageable : MonoBehaviour
{
    public string displayName = "Objetivo";
    [Min(1f)] public float maxHealth = 100f;
    [Tooltip("Muñeco de práctica: no muere, se recupera solo.")]
    public bool trainingDummy;
    [Tooltip("Segundos sin recibir daño antes de recuperarse (solo muñecos).")]
    public float regenDelay = 3f;
    [Tooltip("Altura de los números de daño.")]
    public float popupHeight = 1.6f;
    [Tooltip("Parte que se sacude y se tiñe al recibir golpes (vacío = todo).")]
    public Transform visual;

    public float Health { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsFrozen => Time.time < _frozenUntil;
    public bool IsBurning => Time.time < _burnUntil;
    public bool IsPoisoned => _poison.Count > 0;
    // Para la IA de los enemigos: 0 congelado, 1 normal.
    public float SpeedMultiplier => IsFrozen ? 0f : Time.time < _slowUntil ? 1f - _slow : 1f;

    public event Action<DamageInfo> Damaged;
    public event Action Died;

    private float _burnDps, _burnUntil;
    private readonly List<Vector2> _poison = new List<Vector2>();
    private float _slow, _slowUntil, _frozenUntil;
    private float _tickAt, _lastHit;
    private float _shake;
    private Vector3 _knock;

    private Renderer[] _renderers;
    private Color[][] _baseColors;
    private MaterialPropertyBlock _mpb;
    private Quaternion _visualRot;
    private Vector3 _visualPos;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        Health = maxHealth;
        if (visual == null) visual = transform;
        _visualRot = visual.localRotation;
        _visualPos = visual.localPosition;
        _renderers = visual.GetComponentsInChildren<Renderer>();
        _baseColors = new Color[_renderers.Length][];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Material[] mats = _renderers[i].sharedMaterials;
            _baseColors[i] = new Color[mats.Length];
            for (int m = 0; m < mats.Length; m++)
                _baseColors[i][m] = mats[m] != null && mats[m].HasProperty(BaseColorId) ? mats[m].GetColor(BaseColorId) : Color.white;
        }
        _mpb = new MaterialPropertyBlock();
    }

    public Vector3 PopupPoint => transform.position + Vector3.up * popupHeight;

    public void TakeHit(DamageInfo info)
    {
        if (IsDead || info.amount <= 0f) return;
        Health -= info.amount;
        _lastHit = Time.time;
        if (!info.periodic) _shake = info.critical ? 1.6f : 1f;

        WorldHUD hud = WorldHUD.Instance;
        if (hud != null)
        {
            if (info.critical)
            {
                hud.Popup(PopupPoint + Vector3.up * 0.1f, Mathf.CeilToInt(info.amount) + "!", FrostboundUI.Gold, 1.7f);
                hud.Popup(PopupPoint + Vector3.up * 0.55f, "¡CRÍTICO!", new Color(1f, 0.85f, 0.35f), 1.1f);
            }
            else
                hud.Popup(PopupPoint + UnityEngine.Random.insideUnitSphere * 0.2f, Mathf.CeilToInt(info.amount).ToString(),
                    WeaponCatalog.DamageColor(info.type), info.periodic ? 0.75f : 1f);
        }

        Damaged?.Invoke(info);
        if (Health > 0f) return;
        if (trainingDummy)
        {
            Health = maxHealth;
            if (hud != null) hud.Popup(PopupPoint + Vector3.up * 0.4f, "¡Derribado!", FrostboundUI.Gold, 1.1f);
            return;
        }
        IsDead = true;
        Died?.Invoke();
    }

    // ----- Estados de las runas -----

    public void ApplyBurn(float dps, float seconds)
    {
        if (IsDead) return;
        _burnDps = Mathf.Max(_burnDps * (IsBurning ? 1f : 0f), dps);
        _burnUntil = Time.time + seconds;
        if (_tickAt < Time.time) _tickAt = Time.time + 0.5f;
    }

    public void ApplyPoison(float dps, float seconds, int maxStacks = 3)
    {
        if (IsDead) return;
        if (_poison.Count >= maxStacks) _poison.RemoveAt(0);
        _poison.Add(new Vector2(dps, Time.time + seconds));
        if (_tickAt < Time.time) _tickAt = Time.time + 0.5f;
    }

    public void ApplySlow(float amount, float seconds)
    {
        if (IsDead) return;
        _slow = Mathf.Max(IsSlowed ? _slow : 0f, Mathf.Clamp01(amount));
        _slowUntil = Mathf.Max(_slowUntil, Time.time + seconds);
    }

    private bool IsSlowed => Time.time < _slowUntil;

    public void Freeze(float seconds)
    {
        if (IsDead) return;
        bool was = IsFrozen;
        _frozenUntil = Mathf.Max(_frozenUntil, Time.time + seconds);
        if (!was && WorldHUD.Instance != null)
            WorldHUD.Instance.Popup(PopupPoint + Vector3.up * 0.35f, "¡Congelado!", WeaponCatalog.DamageColor(DamageType.Frost), 0.9f);
    }

    public void Knockback(Vector3 direction, float meters)
    {
        direction.y = 0f;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(direction.normalized * meters * 6f, ForceMode.VelocityChange);
            return;
        }
        // Objetivos fijos (muñecos): se inclinan hacia atrás.
        _knock = direction.normalized * Mathf.Clamp(meters, 0.5f, 3f);
    }

    void Update()
    {
        if (IsDead) return;
        float now = Time.time;

        if (now >= _tickAt && (IsBurning || _poison.Count > 0))
        {
            _tickAt = now + 0.5f;
            if (IsBurning) TakeHit(new DamageInfo { amount = _burnDps * 0.5f, type = DamageType.Fire, periodic = true });
            float poison = 0f;
            for (int i = _poison.Count - 1; i >= 0; i--)
            {
                if (now > _poison[i].y) _poison.RemoveAt(i);
                else poison += _poison[i].x;
            }
            if (poison > 0f) TakeHit(new DamageInfo { amount = poison * 0.5f, type = DamageType.Poison, periodic = true });
        }
        if (!IsBurning) _burnDps = 0f;

        if (trainingDummy && Health < maxHealth && now - _lastHit > regenDelay)
            Health = Mathf.Min(maxHealth, Health + maxHealth * 0.5f * Time.deltaTime);

        Animate();
    }

    private void Animate()
    {
        _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * 4f);
        _knock = Vector3.MoveTowards(_knock, Vector3.zero, Time.deltaTime * 4f);
        if (visual != transform)
        {
            float wobble = Mathf.Sin(Time.time * 38f) * 9f * _shake;
            Vector3 axis = Vector3.Cross(Vector3.up, _knock.sqrMagnitude > 0.001f ? _knock.normalized : transform.right);
            Quaternion lean = Quaternion.AngleAxis(_knock.magnitude * 14f, axis);
            visual.localRotation = Quaternion.Inverse(transform.rotation) * lean * transform.rotation * _visualRot * Quaternion.Euler(0f, 0f, wobble);
            visual.localPosition = _visualPos;
        }

        Color tint = Color.white;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
        if (IsFrozen) tint = new Color(0.55f, 0.85f, 1.25f);
        else if (IsBurning) tint = Color.Lerp(Color.white, new Color(1.4f, 0.7f, 0.35f), 0.4f + 0.3f * pulse);
        else if (IsPoisoned) tint = Color.Lerp(Color.white, new Color(0.6f, 1.25f, 0.45f), 0.35f + 0.25f * pulse);
        else if (IsSlowed) tint = new Color(0.8f, 0.92f, 1.1f);
        if (_shake > 0.6f) tint = Color.Lerp(tint, new Color(1.6f, 1.6f, 1.6f), (_shake - 0.6f) * 2.5f);

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null) continue;
            for (int m = 0; m < _baseColors[i].Length; m++)
            {
                r.GetPropertyBlock(_mpb, m);
                Color c = _baseColors[i][m];
                _mpb.SetColor(BaseColorId, new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a));
                r.SetPropertyBlock(_mpb, m);
            }
        }
    }
}
