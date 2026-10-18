using System.Collections.Generic;
using UnityEngine;

// Bobby, el que rompió el sello (jefe del capítulo III). Un ninja medio convertido en cristal que pelea como un mago
// del hielo: aparece a la espalda del héroe, lanza filas de lanzas de escarcha que brotan del suelo y, a media vida,
// se divide en copias de hielo. No muere: al 10 % se rinde (BossController.spare) y vuelve a ser él.
[RequireComponent(typeof(BossController))]
public class BobbyBoss : MonoBehaviour
{
    public static readonly Color Plumage = new Color(0.5f, 0.62f, 0.85f);
    private static readonly Color LanceColor = new Color(0.55f, 0.85f, 1f);

    private BossController _boss;
    private EnemyBrain _brain;
    private float _nextLance;
    private bool _cloned;
    private readonly List<EnemyBrain> _clones = new List<EnemyBrain>();
    private static Material _crystal;

    void Start()
    {
        _boss = GetComponent<BossController>();
        _brain = GetComponent<EnemyBrain>();
        _boss.blinkInterval = 7f;
        _nextLance = Time.time + 5f;
        Crystals();
    }

    void OnDestroy()
    {
        foreach (EnemyBrain c in _clones) if (c != null) Destroy(c.gameObject);
    }

    void Update()
    {
        if (_boss.Health.IsDead || _boss.Speaking || _boss.Spared)
        {
            // Al rendirse, sus copias se deshacen.
            if (_boss.Spared) Shatter();
            return;
        }
        float hp = _boss.Health.Health / Mathf.Max(1f, _boss.Health.maxHealth);
        if (!_cloned && hp < 0.5f)
        {
            _cloned = true;
            Clones();
        }
        if (!_boss.Engaged || Time.time < _nextLance) return;
        Transform target = _brain.Target;
        if (target == null) return;
        _nextLance = Time.time + (hp < 0.5f ? 4.5f : 6f);
        Lances(target.position);
    }

    private float Power => 12f + _brain.Level * 1.8f;

    // Fila de lanzas de escarcha desde Bobby hacia el héroe: brotan una tras otra.
    private void Lances(Vector3 toward)
    {
        Vector3 from = transform.position;
        Vector3 dir = toward - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        dir.Normalize();
        for (int i = 1; i <= 6; i++)
        {
            Vector3 at = from + dir * (i * 1.9f);
            GroundWarning.Spawn(at, 1.1f, 0.6f + i * 0.12f, Power, LanceColor, Spike);
        }
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 2.6f, "¡Lanzas de escarcha!", LanceColor, 0.9f);
    }

    private void Spike(Vector3 at)
    {
        if (_crystal == null) _crystal = MenuRuins.Lit("Cristal del Frost", new Color(0.32f, 0.62f, 0.95f), 0.9f, 0f, new Color(0.25f, 0.7f, 1.6f));
        var go = new GameObject("Lanza_Escarcha");
        go.transform.position = at;
        var spike = new GameObject("Pincho");
        spike.transform.SetParent(go.transform, false);
        spike.transform.localPosition = Vector3.up * 0.8f;
        spike.transform.localRotation = Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
        spike.transform.localScale = new Vector3(0.45f, 1.6f, 0.45f);
        spike.AddComponent<MeshFilter>().sharedMesh = MenuRuins.Octahedron();
        spike.AddComponent<MeshRenderer>().sharedMaterial = _crystal;
        go.AddComponent<SinkAndDie>().Begin(1f, 0.8f);
    }

    // Dos copias de hielo: pocas vidas, pegan flojo y se deshacen solas al rato.
    private void Clones()
    {
        EnemyDefinition copy = Instantiate(_brain.definition);
        copy.name = "Copia_Bobby";
        copy.id = "Bobby_Copia";
        copy.displayName = "Copia de hielo";
        copy.maxHealth = _brain.definition.maxHealth * 0.12f;
        copy.damage = _brain.definition.damage * 0.4f;
        copy.experience = 1;
        copy.lootPicks = 1;
        for (int i = 0; i < 2; i++)
        {
            Vector3 at = transform.position + Quaternion.Euler(0f, i * 180f + 90f, 0f) * Vector3.forward * 3f;
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
            GameObject go = Instantiate(copy.prefab, at, transform.rotation);
            go.name = "Copia_de_hielo";
            EnemyBrain b = go.GetComponent<EnemyBrain>();
            b.Setup(copy, null, _brain.Level, false, new Color(0.75f, 0.9f, 1f));
            b.leashOverride = 60f;
            b.Alert();
            go.AddComponent<SelfDestruct>().seconds = 14f;
            _clones.Add(b);
        }
        if (WorldHUD.Instance != null) WorldHUD.Instance.Say(transform, 1.55f * transform.lossyScale.y, "¿Cuál de nosotros es el de verdad, héroe?", 3f);
    }

    private void Shatter()
    {
        foreach (EnemyBrain c in _clones) if (c != null) Destroy(c.gameObject);
        _clones.Clear();
    }

    // Medio cristal: esquirlas en la espalda y en un brazo.
    private void Crystals()
    {
        if (_crystal == null) _crystal = MenuRuins.Lit("Cristal del Frost", new Color(0.32f, 0.62f, 0.95f), 0.9f, 0f, new Color(0.25f, 0.7f, 1.6f));
        PenguinRigAnimator rig = GetComponentInChildren<PenguinRigAnimator>();
        Transform anchor = rig != null ? rig.transform : transform;
        Mesh octa = MenuRuins.Octahedron();
        for (int i = 0; i < 6; i++)
        {
            var go = new GameObject("Cristal_Bobby");
            go.AddComponent<MeshFilter>().sharedMesh = octa;
            go.AddComponent<MeshRenderer>().sharedMaterial = _crystal;
            float side = i < 4 ? (i - 1.5f) * 0.1f : (i == 4 ? 0.3f : 0.34f);
            Vector3 local = i < 4 ? new Vector3(side, 0.6f + (i % 2) * 0.1f, -0.22f) : new Vector3(side, 0.45f + (i - 4) * 0.12f, 0.02f);
            go.transform.SetPositionAndRotation(anchor.TransformPoint(local), anchor.rotation * Quaternion.Euler(i < 4 ? -30f : 0f, 0f, i < 4 ? side * -150f : -60f));
            go.transform.localScale = Vector3.Scale(new Vector3(0.08f, 0.24f, 0.08f), anchor.lossyScale);
            go.transform.SetParent(anchor, true);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _crystal = null;
}

// Desaparece al cabo de unos segundos (copias de hielo).
public class SelfDestruct : MonoBehaviour
{
    public float seconds = 10f;
    private float _born;

    void Start() => _born = Time.time;

    void Update()
    {
        if (Time.time - _born < seconds) return;
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 1.2f, "*crac*", new Color(0.7f, 0.9f, 1f), 0.8f);
        Destroy(gameObject);
    }
}
