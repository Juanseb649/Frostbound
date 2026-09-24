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
            smr.rootBone = root != null ? root : (_bodyRenderer != null ? _bodyRenderer.rootBone : transform);
            smr.transform.SetParent(transform, false);
            smr.transform.localPosition = Vector3.zero;
            smr.transform.localRotation = Quaternion.identity;
            smr.transform.localScale = Vector3.one;
            smr.gameObject.name = "Outfit_" + item.name;
            if (_bodyRenderer != null) smr.gameObject.layer = _bodyRenderer.gameObject.layer;
            ApplyMaterial(smr, item);
            equipped.spawned.Add(smr.gameObject);
        }
        DestroySafe(instance);
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
