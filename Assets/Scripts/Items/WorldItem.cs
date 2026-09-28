using UnityEngine;

// Objeto tirado en el suelo. Cae con física (Rigidbody), rebota y se queda quieto; lleva su propio estado
// (durabilidad y runas). El héroe lo recoge haciendo clic sobre él o sobre su etiqueta (se acerca primero), o con E.
public class WorldItem : MonoBehaviour
{
    public ItemStack stack;
    [Tooltip("Material para el icono de los objetos que no tienen modelo 3D (pociones, runas, piezas de armadura).")]
    public Material spriteMaterial;
    [Tooltip("Segundos antes de poder recogerlo (para no recogerlo al tirarlo).")]
    public float pickupDelay = 0.6f;

    public static Material DefaultSpriteMaterial;

    public bool CanPickUp => Time.time - _spawnTime > pickupDelay;
    public string Label => stack == null || stack.item == null ? "" :
        stack.item.displayName + (stack.quantity > 1 ? " ×" + stack.quantity : "") + (stack.IsBroken ? " (rota)" : "");
    public Color LabelColor => stack == null || stack.item == null ? Color.white : FrostboundUI.Rarity(stack.item.rarity);
    public bool Highlighted { get; set; }

    private Rigidbody _rb;
    private float _spawnTime;
    private bool _built;
    private Transform _visual;

    public static WorldItem Spawn(ItemStack stack, Vector3 position, Vector3 velocity, GameObject ignoreCollisionsWith = null)
    {
        if (stack == null || stack.IsEmpty) return null;
        var go = new GameObject("Botin_" + stack.item.name);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
        var wi = go.AddComponent<WorldItem>();
        wi.stack = stack;
        wi.Build();
        wi._rb.linearVelocity = velocity;
        wi._rb.angularVelocity = Random.insideUnitSphere * 8f;
        if (ignoreCollisionsWith != null)
            foreach (Collider mine in go.GetComponentsInChildren<Collider>())
                foreach (Collider other in ignoreCollisionsWith.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(mine, other);
        return wi;
    }

    void Awake()
    {
        _spawnTime = Time.time;
    }

    void Start()
    {
        Build();
        WorldHUD.RegisterItem(this);
    }

    void OnDestroy() => WorldHUD.UnregisterItem(this);

    private void Build()
    {
        if (_built || stack == null || stack.item == null) return;
        _built = true;
        stack.EnsureInstance();
        ItemDefinition item = stack.item;

        var visual = new GameObject("Modelo").transform;
        visual.SetParent(transform, false);
        _visual = visual;

        GameObject pieceModel = item.weaponModel != null ? null : BuildOutfitModel(item.outfit, visual);
        if (item.weaponModel != null || pieceModel != null)
        {
            GameObject m = pieceModel != null ? pieceModel : Instantiate(item.weaponModel, visual, false);
            foreach (Collider c in m.GetComponentsInChildren<Collider>(true)) Destroy(c);
            Bounds b = LocalBounds(m);
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = Vector3.Max(b.size, new Vector3(0.06f, 0.06f, 0.06f));
        }
        else
        {
            // Sin modelo: una tarjeta con el icono que cae, gira y queda boca arriba en el suelo.
            var sr = visual.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = item.icon;
            Material mat = spriteMaterial != null ? spriteMaterial : DefaultSpriteMaterial;
            if (mat != null) sr.sharedMaterial = mat;
            float size = 0.55f;
            float ppu = item.icon != null ? item.icon.pixelsPerUnit : 100f;
            float w = item.icon != null ? item.icon.rect.width / ppu : 1f;
            visual.localScale = Vector3.one * (size / Mathf.Max(0.01f, w));
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(size, size, 0.05f);
        }

        _rb = gameObject.AddComponent<Rigidbody>();
        _rb.mass = 1f;
        _rb.linearDamping = 0.1f;
        _rb.angularDamping = 0.6f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    // Pieza de armadura tal como se ve puesta (casco, peto, botas...), en vez del icono.
    // De las prendas hechas a medida se copian sus mallas en reposo; se centra en el objeto para que ruede bien.
    private GameObject BuildOutfitModel(OutfitItem outfit, Transform parent)
    {
        if (outfit == null || outfit.prefab == null) return null;
        var root = new GameObject("Pieza");
        root.transform.SetParent(parent, false);

        if (outfit.attachMode == OutfitAttachMode.Skinned)
        {
            GameObject temp = Instantiate(outfit.prefab);
            Transform tRoot = temp.transform;
            foreach (SkinnedMeshRenderer smr in temp.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!outfit.UsesPart(smr.name) || smr.sharedMesh == null) continue;
                var part = new GameObject(smr.name);
                part.transform.SetParent(root.transform, false);
                Matrix4x4 rel = tRoot.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                part.transform.localPosition = rel.GetColumn(3);
                part.transform.localRotation = rel.rotation;
                part.transform.localScale = rel.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = smr.sharedMesh;
                var mr = part.AddComponent<MeshRenderer>();
                mr.sharedMaterials = outfit.materialOverride != null ? Fill(outfit.materialOverride, smr.sharedMaterials.Length) : smr.sharedMaterials;
            }
            Destroy(temp);
        }
        else if (outfit.attachMode == OutfitAttachMode.FitToBones)
        {
            // Un par de botas, una junto a la otra.
            for (int i = 0; i < Mathf.Max(1, Mathf.Min(2, outfit.fitBones.Length)); i++)
            {
                GameObject b = Instantiate(outfit.prefab, root.transform, false);
                Bounds mb = LocalBounds(b);
                float size = Mathf.Max(0.001f, Mathf.Max(mb.size.x, mb.size.z));
                b.transform.localScale = Vector3.Scale(Vector3.one * (0.16f / size), outfit.scale);
                b.transform.localPosition = new Vector3((i - 0.5f) * 0.15f, 0f, 0f);
                if (outfit.materialOverride != null)
                    foreach (Renderer r in b.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Fill(outfit.materialOverride, r.sharedMaterials.Length);
            }
        }
        else
        {
            GameObject a = Instantiate(outfit.prefab, root.transform, false);
            a.transform.localRotation = Quaternion.Euler(outfit.rotationOffset);
            a.transform.localScale = outfit.scale;
            if (outfit.materialOverride != null)
                foreach (Renderer r in a.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Fill(outfit.materialOverride, r.sharedMaterials.Length);
        }

        if (root.GetComponentInChildren<Renderer>() == null)
        {
            Destroy(root);
            return null;
        }
        // Centra la pieza sobre el objeto.
        Bounds all = LocalBounds(root);
        root.transform.localPosition -= all.center;
        return root;
    }

    private static Material[] Fill(Material m, int n)
    {
        var mats = new Material[Mathf.Max(1, n)];
        for (int i = 0; i < mats.Length; i++) mats[i] = m;
        return mats;
    }

    private Bounds LocalBounds(GameObject root)
    {
        bool found = false;
        var b = new Bounds();
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            Vector3 c = mb.center, e = mb.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!found) { b = new Bounds(p, Vector3.zero); found = true; }
                else b.Encapsulate(p);
            }
        }
        return found ? b : new Bounds(Vector3.zero, Vector3.one * 0.3f);
    }

    void FixedUpdate()
    {
        if (_rb == null || _rb.isKinematic) return;
        // Cuando deja de moverse se queda fijo: el héroe no lo patea al pasar.
        if (Time.time - _spawnTime > 1.2f && _rb.linearVelocity.sqrMagnitude < 0.01f && _rb.angularVelocity.sqrMagnitude < 0.05f)
            _rb.isKinematic = true;
        if (Time.time - _spawnTime > 6f) _rb.isKinematic = true;
        if (transform.position.y < -20f) Destroy(gameObject);
    }

    // Lo mete en la mochila. Devuelve false si no cabe.
    public bool TryPickUp(Inventory inventory)
    {
        if (inventory == null || stack == null || !CanPickUp) return false;
        if (!inventory.AddStack(stack)) return false;
        Destroy(gameObject);
        return true;
    }
}
