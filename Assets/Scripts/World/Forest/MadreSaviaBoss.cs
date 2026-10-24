using System.Collections.Generic;
using UnityEngine;

// La Madre Savia, chamana corrupta del Bosque (jefa del capítulo II). Pelea en tres fases:
// 1. Orbes de savia: cada uno deja un charco venenoso donde cae.
// 2. Raíces (66 %): bajo el héroe brota un círculo y, si no se aparta, las raíces lo golpean; llama a sus corruptos.
// 3. Lluvia de savia (33 %): círculos alrededor del héroe que descargan charcos. Hay que rodar.
[RequireComponent(typeof(BossController))]
public class MadreSaviaBoss : MonoBehaviour
{
    public static readonly Color Plumage = new Color(0.3f, 0.42f, 0.26f);
    private static readonly Color Robe = new Color(0.12f, 0.2f, 0.1f);
    private static readonly Color RootColor = new Color(0.55f, 0.95f, 0.3f);
    private static readonly Color RainColor = new Color(0.4f, 0.85f, 0.2f);

    private enum Phase { None, Orbs, Roots, Rain }

    private BossController _boss;
    private EnemyBrain _brain;
    private Phase _phase = Phase.None;
    private float _nextSpell;
    private readonly List<GameObject> _roots = new List<GameObject>();
    private static Material _orb, _bark, _vein;

    void Start()
    {
        _boss = GetComponent<BossController>();
        _brain = GetComponent<EnemyBrain>();
        _brain.Shot += OnShot;
        Dress();
        SetPhase(Phase.Orbs);
        _nextSpell = Time.time + 6f;
    }

    void OnDestroy()
    {
        if (_brain != null) _brain.Shot -= OnShot;
        foreach (GameObject r in _roots) if (r != null) Destroy(r);
    }

    void Update()
    {
        _roots.RemoveAll(r => r == null);
        if (_boss.Health.IsDead || _boss.Speaking) return;
        float hp = _boss.Health.Health / Mathf.Max(1f, _boss.Health.maxHealth);
        Phase want = hp > 0.66f ? Phase.Orbs : hp > 0.33f ? Phase.Roots : Phase.Rain;
        if (want != _phase) SetPhase(want);
        if (!_boss.Engaged || Time.time < _nextSpell) return;
        Transform target = _brain.Target;
        if (target == null) return;
        if (_phase == Phase.Roots)
        {
            _nextSpell = Time.time + 5f;
            RootBurst(target.position);
        }
        else if (_phase == Phase.Rain)
        {
            _nextSpell = Time.time + 6.5f;
            SapRain(target.position);
        }
    }

    private float Power => BossTuning.SapPower(_brain.Level);

    private void OnShot(EnemyProjectile p)
    {
        if (p == null) return;
        float dps = BossTuning.SapPoison(_brain.Level);
        p.Ended = at => PoisonPuddle.Spawn(at, 2f, dps, 6f);
    }

    private void SetPhase(Phase p)
    {
        Phase old = _phase;
        _phase = p;
        EnemyDefinition d = _brain.definition;
        switch (p)
        {
            case Phase.Orbs:
                d.ranged = true; d.attackRange = 12f; d.preferredDistance = new Vector2(7f, 11f); d.projectileSpeed = 10f;
                d.windup = 0.8f; d.cooldown = 1.4f; d.hitsPerAttack = 2; d.areaRadius = 0f;
                d.projectileMaterial = Orb();
                break;
            case Phase.Roots:
                d.ranged = true; d.attackRange = 11f; d.cooldown = 2.2f; d.hitsPerAttack = 2;
                break;
            case Phase.Rain:
                d.ranged = false; d.attackRange = 2.4f; d.windup = 0.6f; d.cooldown = 1.3f; d.hitsPerAttack = 1; d.areaRadius = 2.8f;
                d.moveSpeed = 3.8f;
                break;
        }
        if (old == Phase.None) return;
        AudioManager.Play3D(AudioId.Boss_Grunt1, transform.position, 1.1f, 3f, 35f);
        string line = p == Phase.Roots ? "¡Raíces mías, sujetadlo! ¡Hijos del bosque, a mí!" : "¡Que llueva la savia! El Corazón tiene sed...";
        if (WorldHUD.Instance != null) WorldHUD.Instance.Say(transform, 1.55f * transform.lossyScale.y, line, 3f);
        if (p == Phase.Roots) CallChildren();
    }

    // Las raíces brotan donde estaba el héroe.
    private void RootBurst(Vector3 at)
    {
        GroundWarning.Spawn(at, 2f, 1.2f, Power * 1.3f, RootColor, Roots);
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 3f, "¡Raíces!", RootColor, 0.9f);
    }

    private void Roots(Vector3 at)
    {
        if (_bark == null)
        {
            _bark = MenuRuins.Lit("Raíz de la Madre", new Color(0.2f, 0.15f, 0.12f), 0.1f);
            _vein = MenuRuins.Lit("Vena de la Madre", new Color(0.3f, 0.9f, 0.3f), 0.5f, 0f, new Color(0.3f, 1.1f, 0.25f));
        }
        var root = new GameObject("Raices_Madre");
        root.transform.position = at;
        for (int i = 0; i < 6; i++)
        {
            var pivot = new GameObject("Raiz").transform;
            pivot.SetParent(root.transform, false);
            pivot.localRotation = Quaternion.Euler(Random.Range(15f, 40f), i * 60f + Random.Range(-15f, 15f), 0f);
            var c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(c.GetComponent<Collider>());
            c.transform.SetParent(pivot, false);
            float len = Random.Range(1.2f, 2f);
            c.transform.localPosition = new Vector3(0f, len * 0.5f, 0f);
            c.transform.localScale = new Vector3(0.25f, len * 0.5f, 0.25f);
            c.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? _bark : _vein;
        }
        root.AddComponent<SinkAndDie>().Begin(2.2f, 1.6f);
        _roots.Add(root);
    }

    private void SapRain(Vector3 around)
    {
        float dps = BossTuning.SapPoison(_brain.Level);
        GroundWarning.Spawn(around, 1.6f, 1.4f, Power, RainColor, at => PoisonPuddle.Spawn(at, 1.6f, dps, 5f));
        for (int i = 0; i < 5; i++)
        {
            Vector3 at = around + Quaternion.Euler(0f, i * 72f + Random.Range(-20f, 20f), 0f) * Vector3.forward * Random.Range(2.5f, 5.5f);
            GroundWarning.Spawn(at, 1.6f, 1.4f + i * 0.12f, Power, RainColor, p => PoisonPuddle.Spawn(p, 1.6f, dps, 5f));
        }
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 3f, "¡Lluvia de savia!", RainColor, 0.9f);
    }

    // Dos corruptos del bosque salen de entre los árboles.
    private void CallChildren()
    {
        GameDatabase db = GameDatabase.Instance;
        if (db == null) return;
        string[] ids = { ForestCreature.SapspitterId, ForestCreature.StalkerId };
        for (int i = 0; i < ids.Length; i++)
        {
            EnemyDefinition def = db.FindEnemy(ids[i]);
            if (def == null || def.prefab == null) continue;
            Vector3 at = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 6f;
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
            GameObject go = Instantiate(def.prefab, at, Quaternion.identity);
            EnemyBrain b = go.GetComponent<EnemyBrain>();
            b.Setup(def, null, Mathf.Max(1, _brain.Level - 1), false, Plumage);
            b.leashOverride = 60f;
            b.Alert();
        }
    }

    private static Material Orb()
    {
        if (_orb == null) _orb = MenuRuins.Lit("Orbe de savia", new Color(0.25f, 0.55f, 0.15f), 0.8f, 0f, new Color(0.5f, 1.6f, 0.3f));
        return _orb;
    }

    // Túnica verde musgo sin cinturón y una corona de ramas secas.
    private void Dress()
    {
        PenguinOutfit outfit = GetComponentInChildren<PenguinOutfit>();
        if (outfit != null && _brain.definition.gear != null)
        {
            var gear = ScriptableObject.CreateInstance<OutfitItem>();
            gear.name = "Madre_Savia";
            gear.displayName = "Ropajes de la Madre Savia";
            gear.slot = _brain.definition.gear.slot;
            gear.alsoOccupies = _brain.definition.gear.alsoOccupies;
            gear.attachMode = OutfitAttachMode.Skinned;
            gear.prefab = _brain.definition.gear.prefab;
            gear.onlyParts = new[] { "Robe", "Cloak" };
            outfit.UnequipAll();
            outfit.Equip(gear);
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in outfit.GetComponentsInChildren<Renderer>(true))
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", Robe);
                r.SetPropertyBlock(block);
            }
        }
        Transform head = null;
        PenguinRigAnimator rig = GetComponentInChildren<PenguinRigAnimator>();
        if (rig != null)
            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
                if (t.name == "Head") { head = t; break; }
        if (head != null)
        {
            if (_bark == null) _bark = MenuRuins.Lit("Raíz de la Madre", new Color(0.2f, 0.15f, 0.12f), 0.1f);
            for (int i = 0; i < 7; i++)
            {
                var twig = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(twig.GetComponent<Collider>());
                twig.name = "Corona_Rama";
                float a = (i - 3) * 22f;
                twig.transform.SetPositionAndRotation(rig.transform.TransformPoint(new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.14f, 1.02f, 0.05f)),
                    rig.transform.rotation * Quaternion.Euler(0f, 0f, -a * 0.9f));
                twig.transform.localScale = Vector3.Scale(new Vector3(0.025f, 0.12f + (i % 2) * 0.05f, 0.025f), rig.transform.lossyScale);
                twig.GetComponent<Renderer>().sharedMaterial = _bark;
                twig.transform.SetParent(head, true);
            }
        }
        GetComponent<Damageable>().RefreshBaseColors();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _orb = null;
        _bark = null;
        _vein = null;
    }
}

// Algo que brota, se queda un momento y se hunde en el suelo (raíces de la Madre Savia).
public class SinkAndDie : MonoBehaviour
{
    private float _stay, _sink, _born;
    private Vector3 _start;

    public void Begin(float stay, float sink)
    {
        _stay = stay;
        _sink = sink;
        _born = Time.time;
        _start = transform.position;
        transform.localScale = new Vector3(1f, 0.05f, 1f);
    }

    void Update()
    {
        float age = Time.time - _born;
        if (age < 0.18f) transform.localScale = new Vector3(1f, Mathf.Lerp(0.05f, 1f, age / 0.18f), 1f);
        else transform.localScale = Vector3.one;
        if (age > _stay) transform.position = _start + Vector3.down * 1.8f * Mathf.Clamp01((age - _stay) / _sink);
        if (age > _stay + _sink) Destroy(gameObject);
    }
}
