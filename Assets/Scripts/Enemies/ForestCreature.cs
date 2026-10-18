using UnityEngine;

// Comportamiento propio de los corruptos del Bosque (capítulo II), encima del EnemyBrain:
// - Leñador: hachazo en área con aviso (un círculo rojo crece en el suelo mientras prepara el golpe).
// - Escupesavia: cada escupitajo deja un charco de savia venenosa donde cae.
// - Acechador: se esconde en la niebla y aparece a la espalda del héroe para golpear.
// Cada uno tiene su plumaje (corteza, verde enfermizo, negro violáceo).
public class ForestCreature : MonoBehaviour
{
    public const string WoodcutterId = "Corrupt_Woodcutter";
    public const string SapspitterId = "Corrupt_Sapspitter";
    public const string StalkerId = "Corrupt_Stalker";

    public enum Kind { Lenador, Escupesavia, Acechador }

    public Kind kind;
    private EnemyBrain _brain;
    private Transform _ring;
    private Renderer _ringRenderer;
    private MaterialPropertyBlock _mpb;
    private float _nextBlink;
    private static Material _ringMat;
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    public static void Attach(EnemyBrain brain, EnemyDefinition def)
    {
        Kind kind;
        if (def.id == WoodcutterId) kind = Kind.Lenador;
        else if (def.id == SapspitterId) kind = Kind.Escupesavia;
        else if (def.id == StalkerId) kind = Kind.Acechador;
        else return;
        ForestCreature c = brain.GetComponent<ForestCreature>();
        if (c == null) c = brain.gameObject.AddComponent<ForestCreature>();
        c.kind = kind;
        c._brain = brain;
        c.Tint();
    }

    private void Tint()
    {
        Color plumage = kind == Kind.Lenador ? new Color(0.36f, 0.25f, 0.18f)
            : kind == Kind.Escupesavia ? new Color(0.36f, 0.48f, 0.22f) : new Color(0.16f, 0.12f, 0.22f);
        PenguinAppearance look = GetComponentInChildren<PenguinAppearance>();
        if (look != null) look.SetPlumage(plumage);
        Damageable d = GetComponent<Damageable>();
        if (d != null) d.RefreshBaseColors();
    }

    void OnEnable()
    {
        if (_brain == null) _brain = GetComponent<EnemyBrain>();
        if (_brain != null) _brain.Shot += OnShot;
        _nextBlink = Time.time + Random.Range(3f, 6f);
    }

    void OnDisable()
    {
        if (_brain != null) _brain.Shot -= OnShot;
    }

    private void OnShot(EnemyProjectile p)
    {
        if (kind != Kind.Escupesavia || p == null) return;
        float level = _brain != null ? _brain.Level : 1;
        p.Ended = at => PoisonPuddle.Spawn(at, 1.5f, 2.5f + level * 0.35f, 5f);
    }

    void Update()
    {
        if (_brain == null || _brain.Current == EnemyBrain.State.Dead)
        {
            if (_ring != null) _ring.gameObject.SetActive(false);
            return;
        }
        if (kind == Kind.Lenador) Telegraph();
        else if (kind == Kind.Acechador) Stalk();
    }

    // Círculo del hachazo: crece mientras el leñador levanta el hacha.
    private void Telegraph()
    {
        bool winding = _brain.Current == EnemyBrain.State.Windup;
        if (!winding)
        {
            if (_ring != null) _ring.gameObject.SetActive(false);
            return;
        }
        if (_ring == null) BuildRing();
        _ring.gameObject.SetActive(true);
        float r = Mathf.Max(1f, _brain.definition.areaRadius);
        float k = Mathf.Clamp01(_brain.WindupProgress);
        _ring.position = transform.position + Vector3.up * 0.05f;
        _ring.localScale = new Vector3(r * 2f * Mathf.Lerp(0.3f, 1f, k), 0.01f, r * 2f * Mathf.Lerp(0.3f, 1f, k));
        _mpb.SetColor(ColorId, new Color(1f, 0.18f, 0.12f, Mathf.Lerp(0.45f, 0.9f, k)));
        _ringRenderer.SetPropertyBlock(_mpb);
    }

    private void BuildRing()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.name = "Aviso_Hachazo";
        _ring = go.transform;
        if (_ringMat == null) _ringMat = MenuRuins.Transparent("Aviso de golpe", MenuRuins.Soft(32), new Color(0.95f, 0.2f, 0.15f, 0.5f), false);
        _ringRenderer = go.GetComponent<Renderer>();
        _ringRenderer.sharedMaterial = _ringMat;
        _ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _mpb = new MaterialPropertyBlock();
    }

    // Acechador: si persigue al héroe, cada pocos segundos desaparece y sale a su espalda.
    private void Stalk()
    {
        if (_brain.Current != EnemyBrain.State.Chase || Time.time < _nextBlink) return;
        Transform p = _brain.Target;
        if (p == null) return;
        float dist = Vector3.Distance(p.position, transform.position);
        if (dist < 3f || dist > 14f) return;
        _nextBlink = Time.time + Random.Range(5f, 8f);
        Vector3 behind = p.position - p.forward * 2f;
        if (!UnityEngine.AI.NavMesh.SamplePosition(behind, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) return;
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 1.3f, "...", new Color(0.6f, 0.9f, 0.6f), 0.8f);
        _brain.Blink(hit.position);
        transform.rotation = Quaternion.LookRotation((p.position - hit.position).normalized);
        _brain.ForceAttack();
    }

    void OnDestroy()
    {
        if (_ring != null) Destroy(_ring.gameObject);
    }
}
