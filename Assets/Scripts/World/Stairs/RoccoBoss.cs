using UnityEngine;

// Rocco Vendaval, el mago corrupto. Enorme, de plumaje gris azulado, capa roja y una cara monstruosa con colmillos.
// Lucha en tres fases con las armas del juego: báculo (orbes a distancia), arco (andanadas de flechas)
// y mandoble (golpes que barren). Durante todo el combate invoca gólems de nieve corruptos.
[RequireComponent(typeof(BossController))]
public class RoccoBoss : MonoBehaviour
{
    public static readonly Color Plumage = new Color(0.55f, 0.66f, 0.78f);
    private static readonly Color Cape = new Color(0.62f, 0.1f, 0.1f);

    private enum Phase { None, Staff, Bow, Blade }

    private BossController _boss;
    private EnemyBrain _brain;
    private Phase _phase = Phase.None;
    private float _nextSummon;
    private static Material _orb, _fang, _brow;

    void Start()
    {
        _boss = GetComponent<BossController>();
        _brain = GetComponent<EnemyBrain>();
        Dress();
        SetPhase(Phase.Staff);
        _nextSummon = Time.time + 6f;
    }

    void Update()
    {
        if (_boss.Health.IsDead || _boss.Speaking) return;
        float hp = _boss.Health.Health / Mathf.Max(1f, _boss.Health.maxHealth);
        Phase want = hp > 0.65f ? Phase.Staff : hp > 0.35f ? Phase.Bow : Phase.Blade;
        if (want != _phase) SetPhase(want);
        if (_boss.Engaged && Time.time >= _nextSummon)
        {
            _nextSummon = Time.time + (_phase == Phase.Staff ? 13f : _phase == Phase.Bow ? 16f : 19f);
            Summon(_phase == Phase.Blade ? 1 : 2);
        }
    }

    private void SetPhase(Phase p)
    {
        Phase old = _phase;
        _phase = p;
        EnemyDefinition d = _brain.definition;
        ItemDatabase items = GameDatabase.Instance != null ? GameDatabase.Instance.items : null;
        switch (p)
        {
            case Phase.Staff:
                d.ranged = true; d.attackRange = 12f; d.preferredDistance = new Vector2(7f, 11f); d.projectileSpeed = 12f;
                d.windup = 0.75f; d.cooldown = 1.5f; d.hitsPerAttack = 1; d.areaRadius = 0f;
                d.projectileMaterial = Orb();
                if (items != null) _brain.EquipWeapon(items.Find("mage_staff"));
                break;
            case Phase.Bow:
                d.ranged = true; d.attackRange = 14f; d.preferredDistance = new Vector2(8f, 12f); d.projectileSpeed = 20f;
                d.windup = 0.6f; d.cooldown = 1.3f; d.hitsPerAttack = 3; d.areaRadius = 0f;
                if (items != null) _brain.EquipWeapon(items.Find("longbow") ?? items.Find("bow"));
                break;
            case Phase.Blade:
                d.ranged = false; d.attackRange = 2.6f; d.windup = 0.55f; d.cooldown = 1.1f; d.hitsPerAttack = 2; d.areaRadius = 3.2f;
                d.moveSpeed = 4.6f;
                if (items != null) _brain.EquipWeapon(items.Find("claymore"));
                _boss.blinkInterval = 7f;
                break;
        }
        if (old == Phase.None) return;
        string line = p == Phase.Bow ? "¡Quieto ahí! Veamos si esquivas esto." : "¡Basta de juegos! Te aplastaré con mis propias aletas.";
        if (WorldHUD.Instance != null) WorldHUD.Instance.Say(transform, 1.55f * transform.lossyScale.y, line, 3f);
        Summon(2);
    }

    private void Summon(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 at = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(2.5f, 4.5f);
            SnowGolem.Summon(gameObject, at, 70f + _brain.Level * 10f, 6f + _brain.Level, true, 3);
        }
        if (WorldHUD.Instance != null) WorldHUD.Instance.Popup(transform.position + Vector3.up * 3f, "¡Levantaos, hijos del Frost!", new Color(0.5f, 0.85f, 1f), 1.2f);
    }

    private static Material Orb()
    {
        if (_orb == null) _orb = MenuRuins.Lit("Orbe corrupto", new Color(0.55f, 0.3f, 0.9f), 0.8f, 0f, new Color(1.4f, 0.6f, 2.6f));
        return _orb;
    }

    // ---------- Aspecto ----------

    // Túnica oscura y capa roja (sin cinturón ni cinta en la cara), colmillos enormes y cejas de monstruo.
    private void Dress()
    {
        PenguinOutfit outfit = GetComponentInChildren<PenguinOutfit>();
        if (outfit != null && _brain.definition.gear != null)
        {
            var gear = ScriptableObject.CreateInstance<OutfitItem>();
            gear.name = "Rocco_Vendaval";
            gear.displayName = "Ropajes de Rocco";
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
                if (!r.name.Contains("Cloak")) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", Cape);
                r.SetPropertyBlock(block);
            }
        }
        PenguinRigAnimator rig = GetComponentInChildren<PenguinRigAnimator>();
        Transform head = null;
        if (rig != null)
            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
                if (t.name == "Head") { head = t; break; }
        if (head != null)
        {
            if (_fang == null)
            {
                _fang = MenuRuins.Lit("Colmillo", new Color(0.95f, 0.92f, 0.82f), 0.5f);
                _brow = MenuRuins.Lit("Ceja de Rocco", new Color(0.08f, 0.1f, 0.16f), 0.2f);
            }
            Transform root = rig.transform;
            Mesh octa = MenuRuins.Octahedron();
            // Colmillos de arriba: enormes, curvados hacia fuera, caen por debajo del pico.
            foreach (float s in new[] { -1f, 1f })
            {
                Part(octa, _fang, head, root, new Vector3(s * 0.075f, 0.69f, 0.27f), new Vector3(8f, 0f, s * -12f), new Vector3(0.055f, 0.22f, 0.055f));
                Part(octa, _fang, head, root, new Vector3(s * 0.11f, 0.7f, 0.24f), new Vector3(-6f, 0f, s * 18f), new Vector3(0.035f, 0.11f, 0.035f));
                Part(null, _brow, head, root, new Vector3(s * 0.085f, 0.9f, 0.25f), new Vector3(0f, 0f, s * 22f), new Vector3(0.12f, 0.035f, 0.05f));
            }
        }
        GetComponent<Damageable>().RefreshBaseColors();
    }

    private static void Part(Mesh mesh, Material mat, Transform head, Transform root, Vector3 restPos, Vector3 euler, Vector3 scale)
    {
        GameObject go = mesh != null ? new GameObject("Rocco_Rasgo") : GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (mesh == null) Destroy(go.GetComponent<Collider>());
        else
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
        }
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.transform.SetPositionAndRotation(root.TransformPoint(restPos), root.rotation * Quaternion.Euler(euler));
        go.transform.localScale = Vector3.Scale(scale, root.lossyScale);
        go.transform.SetParent(head, true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _orb = null;
        _fang = null;
        _brow = null;
    }
}
