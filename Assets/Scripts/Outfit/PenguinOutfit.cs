using System.Collections.Generic;
using UnityEngine;

public class PenguinOutfit : MonoBehaviour
{
    [Tooltip("Prendas que se ponen al iniciar.")]
    public List<OutfitItem> startingItems = new List<OutfitItem>();
    [Tooltip("Si el personaje tiene CharacterStats, usa la ropa inicial de su clase.")]
    public bool useClassOutfit = true;

    private readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
    private readonly Dictionary<OutfitSlot, Equipped> _equipped = new Dictionary<OutfitSlot, Equipped>();
    private SkinnedMeshRenderer _bodyRenderer;


    private class Equipped
    {
        public OutfitItem item;
        public readonly List<GameObject> spawned = new List<GameObject>();
    }

    public IEnumerable<OutfitItem> EquippedItems
    {
        get
        {
            var seen = new HashSet<OutfitItem>();
            foreach (Equipped e in _equipped.Values)
                if (seen.Add(e.item)) yield return e.item;
        }
    }

    void Awake()
    {
        CacheBones();
    }

    void Start()
    {
        if (useClassOutfit)
        {
            CharacterStats stats = GetComponentInParent<CharacterStats>();
            if (stats != null && stats.characterClass != null && stats.characterClass.startingOutfit != null)
                foreach (OutfitItem item in stats.characterClass.startingOutfit) Equip(item);
        }
        foreach (OutfitItem item in startingItems) Equip(item);
    }

    public void CacheBones()
    {
        _bones.Clear();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (!_bones.ContainsKey(t.name)) _bones[t.name] = t;

        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name.Contains("Body")) { _bodyRenderer = smr; break; }
            if (_bodyRenderer == null) _bodyRenderer = smr;
        }
    }

    public static string AnchorName(OutfitSlot slot)
    {
        switch (slot)
        {
            case OutfitSlot.Head:
            case OutfitSlot.Face: return "Anchor_Head";
            case OutfitSlot.Chest: return "Anchor_Chest";
            case OutfitSlot.Back: return "Anchor_Back";
            case OutfitSlot.HandL: return "Anchor_Hand_L";
            case OutfitSlot.HandR: return "Anchor_Hand_R";
            case OutfitSlot.Feet: return "Root";
            default: return "Spine";
        }
    }

    public Transform GetAnchor(OutfitSlot slot)
    {
        _bones.TryGetValue(AnchorName(slot), out Transform t);
        return t != null ? t : transform;
    }

    public bool IsEquipped(OutfitItem item)
    {
        return item != null && _equipped.TryGetValue(item.slot, out Equipped e) && e.item == item;
    }

    public void Toggle(OutfitItem item)
    {
        if (IsEquipped(item)) Unequip(item.slot);
        else Equip(item);
    }

    public void Equip(OutfitItem item)
    {
        if (item == null || item.prefab == null) return;
        if (_bones.Count == 0) CacheBones();

        Unequip(item.slot);
        if (item.alsoOccupies != null)
            foreach (OutfitSlot s in item.alsoOccupies) Unequip(s);

        var equipped = new Equipped { item = item };
        if (item.attachMode == OutfitAttachMode.Skinned) SpawnSkinned(item, equipped);
        else if (item.attachMode == OutfitAttachMode.FitToBones) SpawnFitted(item, equipped);
        else SpawnAnchored(item, equipped);

        _equipped[item.slot] = equipped;
        if (item.alsoOccupies != null)
            foreach (OutfitSlot s in item.alsoOccupies) _equipped[s] = equipped;
    }

    public void Unequip(OutfitSlot slot)
    {
        if (!_equipped.TryGetValue(slot, out Equipped e)) return;
        foreach (GameObject go in e.spawned) DestroySafe(go);
        var keys = new List<OutfitSlot>();
        foreach (var pair in _equipped)
            if (pair.Value == e) keys.Add(pair.Key);
        foreach (OutfitSlot k in keys) _equipped.Remove(k);
    }

    public void UnequipAll()
    {

        foreach (OutfitSlot slot in new List<OutfitSlot>(_equipped.Keys)) Unequip(slot);
    }

    private void SpawnSkinned(OutfitItem item, Equipped equipped)
    {
        GameObject instance = Instantiate(item.prefab);
        foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!item.UsesPart(smr.name)) continue;
            Transform[] source = smr.bones;
            var mapped = new Transform[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null && _bones.TryGetValue(source[i].name, out Transform target)) mapped[i] = target;
                else
                {
                    mapped[i] = _bones.TryGetValue("Hips", out Transform hips) ? hips : transform;
                    Debug.LogWarning("[PenguinOutfit] Hueso '" + (source[i] != null ? source[i].name : "null") + "' no existe en " + name + ".");
                }
            }
            Transform root = null;
            if (smr.rootBone != null) _bones.TryGetValue(smr.rootBone.name, out root);

            smr.bones = mapped;
            if (Application.isPlaying) AddSprings(smr, equipped);
            smr.rootBone = root != null ? root : (_bodyRenderer != null ? _bodyRenderer.rootBone : transform);
            smr.transform.SetParent(transform, false);
            smr.transform.localPosition = Vector3.zero;
            smr.transform.localRotation = Quaternion.identity;
            smr.transform.localScale = Vector3.one;
            smr.gameObject.name = "Outfit_" + item.name + "_" + smr.name;
            if (_bodyRenderer != null) smr.gameObject.layer = _bodyRenderer.gameObject.layer;
            ApplyMaterial(smr, item);
            equipped.spawned.Add(smr.gameObject);
        }
        DestroySafe(instance);
    }

    // Piezas rígidas que ganan movimiento secundario: (parte, hueso que la sostiene, hueso de influencia, peso, rigidez, amortiguación).
    private static readonly (string part, string bone, string influence, float weight, float stiffness, float damping)[] SpringParts =
    {
        ("Helm", "Head", null, 0f, 900f, 55f),
        ("Hat", "Head", null, 0f, 500f, 34f),
        ("Hood", "Head", null, 0f, 700f, 48f),
        ("Hair", "Head", null, 0f, 380f, 26f),
        ("Beard", "Head", null, 0f, 420f, 28f),
        ("Glasses", "Head", null, 0f, 1200f, 65f),
        ("Shoulder_R", "Spine", "Flipper_R", 0.35f, 800f, 52f),
        ("Shoulder_L", "Spine", "Flipper_L", 0.35f, 800f, 52f),
        ("Pendant", "Spine", null, 0f, 260f, 18f),
        ("Katana_Back", "Spine", null, 0f, 600f, 40f),
    };

    // Giro de reposo del hueso padre visto desde el de influencia, sacado de las poses de enlace de la malla
    // (así no depende de la pose en que esté el pingüino al vestirse).
    private static Quaternion? RestOffset(SkinnedMeshRenderer smr, string influenceName, string parentName)
    {
        if (influenceName == null || smr.sharedMesh == null) return null;
        Matrix4x4[] bind = smr.sharedMesh.bindposes;
        Transform[] bones = smr.bones;
        int fi = -1, pi = -1;
        for (int i = 0; i < bones.Length && i < bind.Length; i++)
        {
            if (bones[i] == null) continue;
            if (bones[i].name == influenceName) fi = i;
            if (bones[i].name == parentName) pi = i;
        }
        if (fi < 0 || pi < 0) return null;
        Quaternion restInfluence = bind[fi].inverse.rotation;
        Quaternion restParent = bind[pi].inverse.rotation;
        return Quaternion.Inverse(restInfluence) * restParent;
    }

    // Cambia el hueso de la pieza por un hueso-resorte hijo suyo (misma pose de reposo, así el enlace no cambia).
    private void AddSprings(SkinnedMeshRenderer smr, Equipped equipped)
    {
        string partName = smr.name;
        foreach (var sp in SpringParts)
        {
            if (!partName.EndsWith(sp.part)) continue;
            if (!_bones.TryGetValue(sp.bone, out Transform bone)) return;
            var go = new GameObject(sp.bone + "_Resorte_" + sp.part);
            go.transform.SetParent(bone, false);
            var spring = go.AddComponent<SpringBone>();
            spring.stiffness = sp.stiffness;
            spring.damping = sp.damping;
            spring.root = transform;
            // Casco, gafas y hombreras van casi pegados; solo lo que cuelga (colgante, pelo, katana) se balancea más.
            bool rigid = sp.part.Contains("Helm") || sp.part.Contains("Glasses") || sp.part.Contains("Shoulder") || sp.part.Contains("Hood");
            spring.maxAngle = rigid ? 4f : 12f;
            Transform influence = null;
            if (sp.influence != null) _bones.TryGetValue(sp.influence, out influence);
            spring.Setup(influence, sp.weight, RestOffset(smr, sp.influence, sp.bone));

            Transform[] bones = smr.bones;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] == bone) bones[i] = go.transform;
            smr.bones = bones;
            equipped.spawned.Add(go);
            return;
        }
    }

    private void SpawnAnchored(OutfitItem item, Equipped equipped)
    {
        Transform anchor = GetAnchor(item.slot);
        GameObject instance = Instantiate(item.prefab, anchor, false);
        instance.name = "Outfit_" + item.name;
        instance.transform.localPosition = item.positionOffset;
        instance.transform.localRotation = Quaternion.Euler(item.rotationOffset);
        instance.transform.localScale = item.scale;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) ApplyMaterial(r, item);
        foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        equipped.spawned.Add(instance);
    }

    // Coloca una copia en cada hueso, del tamaño de la parte del cuerpo que mueve ese hueso
    // (Penguin_Foot_L para Foot_L). Queda alineada con el pingüino y sigue la animación del hueso.
    private void SpawnFitted(OutfitItem item, Equipped equipped)
    {
        foreach (string boneName in item.fitBones)
        {
            if (!_bones.TryGetValue(boneName, out Transform bone)) continue;
            Bounds part = PartBounds(boneName, out bool found);
            if (!found) part = new Bounds(transform.InverseTransformPoint(bone.position), Vector3.one * 0.15f);

            Vector3 size = Vector3.Scale(part.size, item.scale);
            Vector3 local = new Vector3(part.center.x, part.min.y, part.center.z) + Vector3.Scale(item.positionOffset, part.size);

            GameObject instance = Instantiate(item.prefab);
            instance.name = "Outfit_" + item.name + "_" + boneName;
            instance.transform.SetPositionAndRotation(transform.TransformPoint(local), transform.rotation * Quaternion.Euler(item.rotationOffset));
            instance.transform.localScale = Vector3.Scale(size, transform.lossyScale);
            instance.transform.SetParent(bone, true);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) ApplyMaterial(r, item);
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            equipped.spawned.Add(instance);
        }
    }

    // Caja de la parte del cuerpo en el espacio del pingüino (pose actual).
    private Bounds PartBounds(string boneName, out bool found)
    {
        found = false;
        var bounds = new Bounds();
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!smr.name.EndsWith(boneName) || smr.name.StartsWith("Outfit_") || smr.sharedMesh == null) continue;
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            Transform t = smr.transform;
            foreach (Vector3 v in baked.vertices)
            {
                Vector3 p = transform.InverseTransformPoint(t.position + t.rotation * v);
                if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                else bounds.Encapsulate(p);
            }
            DestroySafe(baked);
            if (found) break;
        }
        return bounds;
    }

    private static void ApplyMaterial(Renderer r, OutfitItem item)
    {
        if (item.materialOverride == null) return;
        var mats = new Material[r.sharedMaterials.Length];
        for (int i = 0; i < mats.Length; i++) mats[i] = item.materialOverride;
        r.sharedMaterials = mats;
    }

    private static void DestroySafe(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
