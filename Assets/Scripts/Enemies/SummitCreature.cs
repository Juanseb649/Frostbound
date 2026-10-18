using UnityEngine;

// Corruptos de la Cumbre (capítulo III), encima del EnemyBrain:
// - Cristalizado: casi todo hielo; resiste los golpes físicos y el frío, el fuego lo rompe (doble de daño).
// - Hijo del Frost: élite; al morir se parte en dos esquirlas más pequeñas que siguen peleando.
// Los dos llevan cristales del Frost brotando de la espalda.
public class SummitCreature : MonoBehaviour
{
    public const string CrystallizedId = "Corrupt_Crystallized";
    public const string FrostSpawnId = "Corrupt_FrostSpawn";
    public const string ShardId = "Corrupt_FrostShard";

    public enum Kind { Cristalizado, HijoDelFrost, Esquirla }

    public Kind kind;
    private EnemyBrain _brain;
    private bool _split;
    private static Material _crystal;

    public static void Attach(EnemyBrain brain, EnemyDefinition def)
    {
        Kind kind;
        if (def.id == CrystallizedId) kind = Kind.Cristalizado;
        else if (def.id == FrostSpawnId) kind = Kind.HijoDelFrost;
        else if (def.id == ShardId) kind = Kind.Esquirla;
        else return;
        SummitCreature c = brain.GetComponent<SummitCreature>();
        if (c == null) c = brain.gameObject.AddComponent<SummitCreature>();
        c.kind = kind;
        c._brain = brain;
        c.Setup();
    }

    private void Setup()
    {
        Damageable d = GetComponent<Damageable>();
        Color plumage = kind == Kind.Cristalizado ? new Color(0.72f, 0.86f, 1f) : new Color(0.16f, 0.24f, 0.5f);
        PenguinAppearance look = GetComponentInChildren<PenguinAppearance>();
        if (look != null) look.SetPlumage(plumage);
        if (kind == Kind.Cristalizado && d != null)
            d.damageScale = t => t == DamageType.Fire ? 2f : t == DamageType.Frost ? 0.3f : t == DamageType.Physical ? 0.6f : 1f;
        if (kind != Kind.Esquirla) Shards(kind == Kind.Cristalizado ? 5 : 3);
        if (d != null) d.RefreshBaseColors();
        if (kind == Kind.HijoDelFrost && _brain != null) _brain.Killed += OnKilled;
    }

    void OnDestroy()
    {
        if (_brain != null) _brain.Killed -= OnKilled;
    }

    // Cristales que brotan de la espalda.
    private void Shards(int count)
    {
        if (_crystal == null) _crystal = MenuRuins.Lit("Cristal del Frost", new Color(0.32f, 0.62f, 0.95f), 0.9f, 0f, new Color(0.25f, 0.7f, 1.6f));
        PenguinRigAnimator rig = GetComponentInChildren<PenguinRigAnimator>();
        Transform anchor = rig != null ? rig.transform : transform;
        Transform spine = null;
        foreach (Transform t in anchor.GetComponentsInChildren<Transform>(true))
            if (t.name == "Spine" || t.name == "Chest" || t.name == "Body") { spine = t; break; }
        Mesh octa = MenuRuins.Octahedron();
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Cristal_Espalda");
            go.AddComponent<MeshFilter>().sharedMesh = octa;
            go.AddComponent<MeshRenderer>().sharedMaterial = _crystal;
            float x = (i - (count - 1) * 0.5f) * 0.12f;
            go.transform.SetPositionAndRotation(anchor.TransformPoint(new Vector3(x, 0.55f + (i % 2) * 0.12f, -0.22f)),
                anchor.rotation * Quaternion.Euler(-35f, 0f, x * -120f));
            go.transform.localScale = Vector3.Scale(new Vector3(0.09f, 0.26f + (i % 2) * 0.08f, 0.09f), anchor.lossyScale);
            go.transform.SetParent(spine != null ? spine : anchor, true);
        }
    }

    // Hijo del Frost: al morir, dos esquirlas.
    private void OnKilled(EnemyBrain b)
    {
        if (_split || b == null) return;
        _split = true;
        EnemyDefinition shard = ShardOf(b.definition);
        for (int i = 0; i < 2; i++)
        {
            Vector3 at = transform.position + Quaternion.Euler(0f, i * 180f + Random.Range(-30f, 30f), 0f) * Vector3.right * 1.2f;
            EnemyBrain s;
            if (b.Camp != null) s = b.Camp.SpawnMember(shard, at, Mathf.Max(1, b.Level - 2), new Color(0.3f, 0.45f, 0.8f));
            else
            {
                GameObject go = Instantiate(shard.prefab, at, Quaternion.identity);
                s = go.GetComponent<EnemyBrain>();
                s.Setup(shard, null, Mathf.Max(1, b.Level - 2), false, new Color(0.3f, 0.45f, 0.8f));
            }
            if (s == null) continue;
            s.transform.localScale *= 0.65f;
            s.Alert();
        }
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 1.6f, "¡Se parte!", new Color(0.55f, 0.85f, 1f), 1f);
    }

    // Copia ligera de la definición del Hijo del Frost para sus esquirlas (no se vuelven a partir).
    private static EnemyDefinition ShardOf(EnemyDefinition parent)
    {
        EnemyDefinition d = Instantiate(parent);
        d.name = ShardId;
        d.id = ShardId;
        d.displayName = "Esquirla del Frost";
        d.maxHealth = parent.maxHealth * 0.3f;
        d.damage = parent.damage * 0.5f;
        d.moveSpeed = parent.moveSpeed * 1.3f;
        d.experience = Mathf.Max(1, parent.experience / 4);
        d.areaRadius = 0f;
        d.windup = 0.35f;
        d.lootPicks = 1;
        return d;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _crystal = null;
}
