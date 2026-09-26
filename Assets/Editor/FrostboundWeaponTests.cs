using System.Text;
using UnityEditor;
using UnityEngine;

// Pasos de prueba de armas y runas en Play para el canal de comandos (FrostboundBridge "exec").
public static class FrostboundWeaponTests
{
    private static Equipment Player => Object.FindAnyObjectByType<Equipment>();

    private static ItemDefinition Item(string path) => AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);

    private static string Equip(string path, params string[] runes)
    {
        Equipment eq = Player;
        ItemDefinition item = Item(path);
        if (eq == null || item == null) return "falta " + (eq == null ? "Player" : path);
        eq.EquipDirect(item);
        foreach (string r in runes)
        {
            ItemDefinition rune = Item("Assets/Data/Items/Runes/" + r + ".asset");
            if (rune != null) eq.MainWeapon.runes.Add(rune);
        }
        eq.Apply();
        return State();
    }

    public static string EquipClaymoreFire() => Equip("Assets/Data/Items/Weapons/claymore.asset", "rune_fire", "rune_chain");
    public static string EquipKatanaPoison() => Equip("Assets/Data/Items/Weapons/katana.asset", "rune_poison", "rune_frost");
    public static string EquipSpear() => Equip("Assets/Data/Items/Weapons/spear.asset");
    public static string EquipBowFrost() => Equip("Assets/Data/Items/Weapons/bow.asset", "rune_frost");
    public static string EquipLongbow() => Equip("Assets/Data/Items/Weapons/longbow.asset");
    public static string EquipCrossbow() => Equip("Assets/Data/Items/Weapons/crossbow.asset", "rune_chain");
    public static string EquipKunais() => Equip("Assets/Data/Items/Weapons/throwing_kunai.asset", "rune_poison");
    public static string EquipScimitar() => Equip("Assets/Data/Items/Weapons/scimitar.asset");
    public static string EquipMaul() => Equip("Assets/Data/Items/Weapons/maul.asset", "rune_knockback", "rune_fire", "rune_lifesteal");
    public static string EquipSickle() => Equip("Assets/Data/Items/Weapons/sickle.asset");
    public static string EquipStarter() => Equip("Assets/Data/Items/knight_sword.asset");

    // Pone al héroe delante del primer muñeco, mirándolo.
    public static string FaceDummy()
    {
        GameObject dummy = GameObject.Find("Muneco_Practica_1");
        Equipment eq = Player;
        if (dummy == null || eq == null) return "falta muñeco o Player";
        Vector3 d = dummy.transform.position;
        Vector3 toCenter = (-new Vector3(d.x, 0f, d.z)).normalized;
        Vector3 pos = d + toCenter * 1.6f + Vector3.up * 0.05f;
        Rigidbody rb = eq.GetComponent<Rigidbody>();
        if (rb != null) rb.position = pos;
        eq.transform.position = pos;
        eq.transform.rotation = Quaternion.LookRotation(-toCenter);
        if (rb != null) rb.rotation = eq.transform.rotation;
        return "ok";
    }

    public static string FaceDummyFar()
    {
        string r = FaceDummy();
        Equipment eq = Player;
        GameObject dummy = GameObject.Find("Muneco_Practica_1");
        if (eq == null || dummy == null) return r;
        Vector3 d = dummy.transform.position;
        Vector3 toCenter = (-new Vector3(d.x, 0f, d.z)).normalized;
        Vector3 pos = d + toCenter * 7f + Vector3.up * 0.05f;
        eq.GetComponent<Rigidbody>().position = pos;
        eq.transform.position = pos;
        return "ok";
    }

    public static string Attack()
    {
        GameObject dummy = GameObject.Find("Muneco_Practica_1");
        PlayerCombat combat = Object.FindAnyObjectByType<PlayerCombat>();
        if (dummy == null || combat == null) return "falta muñeco o combate";
        return combat.TryAttack(dummy.transform.position) ? "ataque" : "en espera";
    }

    public static string DropWeapon()
    {
        Equipment eq = Player;
        if (eq == null || eq.MainWeapon == null) return "sin arma";
        ItemStack s = eq.MainWeapon;
        if (!eq.Unequip(EquipSlot.Weapon, out string msg)) return msg;
        Inventory inv = eq.Inventory;
        for (int i = 0; i < inv.Capacity; i++)
        {
            if (inv.Get(i) != s) continue;
            inv.RemoveAt(i);
            Transform t = eq.transform;
            WorldItem.Spawn(s, t.position + Vector3.up * 1.1f + t.forward * 0.5f, t.forward * 2.2f + Vector3.up * 3f, eq.gameObject);
            return "tirada " + s.item.displayName + " dur " + s.durability + " runas " + s.runes.Count;
        }
        return "no encontrada";
    }

    public static string PickNearest()
    {
        Equipment eq = Player;
        WorldItem best = null;
        float bd = float.MaxValue;
        foreach (WorldItem w in Object.FindObjectsByType<WorldItem>())
        {
            float d = (w.transform.position - eq.transform.position).sqrMagnitude;
            if (d < bd) { bd = d; best = w; }
        }
        if (best == null) return "no hay objetos";
        string label = best.Label;
        Object.FindAnyObjectByType<PlayerController>().PickUp(best);
        return "recogiendo " + label + " a " + Mathf.Sqrt(bd).ToString("F1") + " m";
    }

    public static string LootState()
    {
        var sb = new StringBuilder();
        foreach (WorldItem w in Object.FindObjectsByType<WorldItem>())
        {
            Rigidbody rb = w.GetComponent<Rigidbody>();
            sb.Append(w.stack.item.id).Append(" y=").Append(w.transform.position.y.ToString("F2"))
              .Append(rb != null && rb.isKinematic ? " quieto" : " cayendo").Append("; ");
        }
        return sb.ToString();
    }

    public static string State()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Player";
        var sb = new StringBuilder();
        ItemStack w = eq.MainWeapon;
        sb.Append(w != null ? w.item.displayName + " dur " + w.durability.ToString("F1") + "/" + w.item.maxDurability + " runas " + w.runes.Count : "sin arma");
        WeaponHolder h = eq.GetComponentInChildren<WeaponHolder>();
        if (h != null) sb.Append(" | agarre ").Append(h.CurrentGrip).Append(h.MainModel != null ? " modelo " + h.MainModel.name + " en " + h.MainModel.transform.parent.name : " sin modelo");
        sb.Append(" | daño ").Append(eq.Stats.Damage.ToString("F1")).Append(" vida ").Append(eq.Stats.currentHealth.ToString("F0"));
        GameObject dummy = GameObject.Find("Muneco_Practica_1");
        if (dummy != null)
        {
            Damageable d = dummy.GetComponent<Damageable>();
            sb.Append(" | muñeco ").Append(d.Health.ToString("F0")).Append("/").Append(d.maxHealth)
              .Append(d.IsBurning ? " ARDE" : "").Append(d.IsPoisoned ? " VENENO" : "").Append(d.IsFrozen ? " CONGELADO" : "");
        }
        sb.Append(" | mochila ").Append(eq.Inventory.UsedSlots);
        return sb.ToString();
    }

    public static string GripProbe()
    {
        Equipment eq = Player;
        WeaponHolder h = eq.GetComponentInChildren<WeaponHolder>();
        if (h == null || h.MainModel == null) return "sin modelo";
        Transform root = h.transform;
        Transform m = h.MainModel.transform;
        Transform hand = m.parent;
        return "playerFwd " + eq.transform.forward.ToString("F2") + " rootFwd " + root.forward.ToString("F2")
            + " | armaUp(root) " + root.InverseTransformDirection(m.up).ToString("F2")
            + " armaFwd(root) " + root.InverseTransformDirection(m.forward).ToString("F2")
            + " armaRight(root) " + root.InverseTransformDirection(m.right).ToString("F2")
            + " | pos(root) " + root.InverseTransformPoint(m.position).ToString("F2")
            + " mano(root) " + root.InverseTransformPoint(hand.position).ToString("F2")
            + " escala " + m.lossyScale.ToString("F2");
    }

    // Abre el inventario con el arma equipada seleccionada (para ver su ficha).
    public static string InspectWeapon()
    {
        InventoryScreen screen = Object.FindAnyObjectByType<InventoryScreen>();
        if (screen == null) return "sin inventario";
        screen.Open();
        var views = typeof(InventoryScreen).GetField("_equipViews", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(screen)
            as System.Collections.Generic.Dictionary<EquipSlot, ItemSlotView>;
        var select = typeof(InventoryScreen).GetMethod("Select", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        select.Invoke(screen, new object[] { views[EquipSlot.Weapon] });
        typeof(InventoryScreen).GetField("_hovered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(screen, null);
        typeof(InventoryScreen).GetMethod("RefreshDetail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(screen, null);
        return "ok";
    }

    public static string InspectRune()
    {
        Equipment eq = Player;
        ItemDefinition rune = Item("Assets/Data/Items/Runes/rune_frost.asset");
        eq.Inventory.Add(rune, 2);
        InventoryScreen screen = Object.FindAnyObjectByType<InventoryScreen>();
        screen.Open();
        var cells = typeof(InventoryScreen).GetField("_cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(screen)
            as System.Collections.Generic.List<ItemSlotView>;
        var select = typeof(InventoryScreen).GetMethod("Select", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (ItemSlotView c in cells)
            if (c.Item == rune) { select.Invoke(screen, new object[] { c }); return "ok"; }
        return "no está";
    }

    public static string CloseInventory()
    {
        InventoryScreen screen = Object.FindAnyObjectByType<InventoryScreen>();
        if (screen != null) screen.Close();
        return "ok";
    }

    // Graba el combo de espada: mantiene el ataque pulsado ~3 s y guarda fotogramas desde una cámara fija.
    private static float _filmStart, _filmNext;
    private static int _filmFrame;
    private static Vector3 _filmCam, _filmTarget;

    private static Vector3 _filmCamB;
    private static float _maxLag;

    public static string SpringLag()
    {
        string r = "retraso máximo " + _maxLag.ToString("F1") + "°";
        _maxLag = 0f;
        return r;
    }

    public static string StartComboFilm()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Player";
        GameObject dummy = GameObject.Find("Muneco_Practica_1");
        if (dummy == null) return "sin muñeco";
        Vector3 d = dummy.transform.position;
        Vector3 toCenter = (-new Vector3(d.x, 0f, d.z)).normalized;
        Vector3 pos = d + toCenter * 2.6f + Vector3.up * 0.05f;
        Rigidbody rb = eq.GetComponent<Rigidbody>();
        if (rb != null) { rb.position = pos; rb.rotation = Quaternion.LookRotation(-toCenter); }
        eq.transform.position = pos;
        eq.transform.rotation = Quaternion.LookRotation(-toCenter);

        Vector3 fwd = -toCenter;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        _filmTarget = pos + Vector3.up * 0.6f + fwd * 0.3f;
        _filmCam = _filmTarget + Vector3.up * 4.2f + fwd * 0.9f - right * 0.2f;
        _filmCamB = _filmTarget + fwd * 2.1f - right * 2.3f + Vector3.up * 0.8f;
        _filmStart = (float)EditorApplication.timeSinceStartup;
        _filmNext = _filmStart;
        _filmFrame = 0;
        EditorApplication.update -= FilmTick;
        EditorApplication.update += FilmTick;
        return "grabando";
    }

    private static void FilmTick()
    {
        float now = (float)EditorApplication.timeSinceStartup;
        if (!EditorApplication.isPlaying || now - _filmStart > 2.9f)
        {
            EditorApplication.update -= FilmTick;
            return;
        }
        foreach (SpringBone sb in Object.FindObjectsByType<SpringBone>())
            if (sb.transform.parent != null) _maxLag = Mathf.Max(_maxLag, sb.Lag);
        PlayerCombat combat = Object.FindAnyObjectByType<PlayerCombat>();
        GameObject dummy = GameObject.Find("Muneco_Practica_1");
        if (combat != null && dummy != null && now - _filmStart < 1.6f) combat.TryAttack(dummy.transform.position);
        if (now >= _filmNext)
        {
            _filmNext = now + 0.06f;
            Equipment eq = Player;
            if (eq != null)
            {
                Transform t = eq.transform;
                _filmTarget = t.position + Vector3.up * 0.6f + t.forward * 0.3f;
                _filmCam = _filmTarget + Vector3.up * 4.2f + t.forward * 0.9f;
                _filmCamB = _filmTarget + t.forward * 2.4f + t.right * 1.5f + Vector3.up * 0.9f;
            }
            FrostboundBridge.CamShot("combo_" + _filmFrame.ToString("00"), _filmCam, _filmTarget, 45f);
            FrostboundBridge.CamShot("comboB_" + _filmFrame.ToString("00"), _filmCamB, _filmTarget, 42f);
            _filmFrame++;
        }
    }

    // Qué huesos mueven cada prenda del jugador (para revisar que la ropa sigue la animación).
    private static readonly System.Collections.Generic.Dictionary<string, Vector3> _driftMin = new System.Collections.Generic.Dictionary<string, Vector3>();
    private static readonly System.Collections.Generic.Dictionary<string, Vector3> _driftMax = new System.Collections.Generic.Dictionary<string, Vector3>();
    private static int _driftFrames;
    private static double _driftEnd;

    // Camina en zigzag y mide cuánto se separa cada pieza de armadura del cuerpo (centro de la malla en el espacio del hueso Root).
    public static string WalkDrift()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Player";
        _driftMin.Clear(); _driftMax.Clear(); _driftFrames = 0;
        _driftEnd = EditorApplication.timeSinceStartup + 3.0;
        _walkLeg = 0;
        EditorApplication.update -= DriftTick;
        EditorApplication.update += DriftTick;
        return "caminando";
    }

    private static int _walkLeg;
    public static string _driftTag = "walkA";
    public static string TagB() { _driftTag = "walkB"; return "ok"; }
    private static readonly Mesh _bake = null;

    private static void DriftTick()
    {
        Equipment eq = Player;
        double now = EditorApplication.timeSinceStartup;
        if (!EditorApplication.isPlaying || eq == null || now > _driftEnd) { EditorApplication.update -= DriftTick; return; }
        int leg = (int)((3.0 - (_driftEnd - now)) / 0.75);
        PlayerController pc = eq.GetComponent<PlayerController>();
        if (leg != _walkLeg || _driftFrames == 0)
        {
            _walkLeg = leg;
            Vector3 dir = Quaternion.Euler(0f, 120f * leg, 0f) * Vector3.forward;
            pc.MoveTo(eq.transform.position + dir * 5f);
        }
        _driftFrames++;
        if (_driftFrames % 20 == 0)
        {
            Transform tp = eq.transform;
            Vector3 tgt = tp.position + Vector3.up * 0.6f;
            FrostboundBridge.CamShot(_driftTag + "_" + (_driftFrames / 20).ToString("00"), tgt + tp.right * 2.2f + tp.forward * 0.8f + Vector3.up * 0.6f, tgt, 40f);
        }
        Transform root = null;
        foreach (Transform t in eq.GetComponentsInChildren<Transform>()) if (t.name == "Root") { root = t; break; }
        if (root == null) return;
        var mesh = new Mesh();
        foreach (SkinnedMeshRenderer smr in eq.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!smr.name.StartsWith("Outfit_")) continue;
            smr.BakeMesh(mesh, true);
            Vector3[] v = mesh.vertices;
            if (v.Length == 0) continue;
            Vector3 c = Vector3.zero;
            for (int i = 0; i < v.Length; i += 7) c += smr.transform.TransformPoint(v[i]);
            c /= Mathf.Ceil(v.Length / 7f);
            Vector3 l = root.InverseTransformPoint(c);
            string k = smr.name.Replace("Outfit_", "");
            _driftMin[k] = _driftMin.TryGetValue(k, out Vector3 a) ? Vector3.Min(a, l) : l;
            _driftMax[k] = _driftMax.TryGetValue(k, out Vector3 b) ? Vector3.Max(b, l) : l;
        }
        Object.DestroyImmediate(mesh);
        foreach (Cloth c in eq.GetComponentsInChildren<Cloth>())
        {
            Vector3[] sim = c.vertices;
            float mx = 0f;
            ClothSkinningCoefficient[] k = c.coefficients;
            for (int i = 0; i < sim.Length && i < k.Length; i++) if (k[i].maxDistance <= 0f) { }
            // distancia media de las partículas a la pieza sin simular
            var m2 = new Mesh();
            c.GetComponent<SkinnedMeshRenderer>().BakeMesh(m2, true);
            Vector3[] rest = m2.vertices;
            Object.DestroyImmediate(m2);
            if (rest.Length == 0 || sim.Length == 0) continue;
            // compara solo el centro de cada una
            Vector3 cs = Vector3.zero, cr = Vector3.zero;
            foreach (Vector3 v in sim) cs += v; foreach (Vector3 v in rest) cr += v;
            cs /= sim.Length; cr /= rest.Length;
            mx = (cs - cr).magnitude;
            string key = "gap_" + c.name.Replace("Outfit_Pieza_Knight_", "");
            _driftMax[key] = Vector3.Max(_driftMax.TryGetValue(key, out Vector3 o) ? o : Vector3.zero, new Vector3(mx, 0, 0));
            _driftMin[key] = Vector3.zero;
        }
    }

    public static string ClothGap() => DriftReport();

    public static string DriftReport()
    {
        var sb = new StringBuilder("frames " + _driftFrames + ": ");
        foreach (var p in _driftMin)
        {
            Vector3 d = _driftMax[p.Key] - p.Value;
            sb.Append(p.Key.Replace("Pieza_Knight_", "")).Append(" ").Append((d.magnitude * 100f).ToString("F1")).Append("cm; ");
        }
        return sb.ToString();
    }

    public static string RendererList()
    {
        Equipment eq = Player;
        var sb = new StringBuilder();
        foreach (Renderer r in eq.GetComponentsInChildren<Renderer>(true))
        {
            string path = r.name; Transform t = r.transform.parent;
            while (t != null && t != eq.transform) { path = t.name + "/" + path; t = t.parent; }
            sb.Append(path).Append(r.enabled && r.gameObject.activeInHierarchy ? "" : " (oculto)").Append(r is SkinnedMeshRenderer ? " skin" : " " + r.GetType().Name).Append("; ");
        }
        return sb.ToString();
    }

    public static string BoneProbe()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Player";
        var sb = new StringBuilder();
        foreach (SkinnedMeshRenderer smr in Object.FindObjectsByType<SkinnedMeshRenderer>())
        {
            if (!smr.name.StartsWith("Outfit_") && !smr.name.Contains("Body")) continue;
            if (!smr.transform.IsChildOf(eq.transform)) { sb.Append("FUERA:").Append(smr.name).Append("; "); continue; }
            int outside = 0, nul = 0, springs = 0;
            foreach (Transform b in smr.bones)
            {
                if (b == null) { nul++; continue; }
                if (!b.IsChildOf(eq.transform)) outside++;
                if (b.GetComponent<SpringBone>() != null) springs++;
            }
            sb.Append(smr.name.Replace("Outfit_", "")).Append(" en ").Append(smr.transform.parent.name)
              .Append(" huesos ").Append(smr.bones.Length).Append(" fuera ").Append(outside).Append(" null ").Append(nul)
              .Append(" resortes ").Append(springs).Append(" root ").Append(smr.rootBone ? smr.rootBone.name : "null")
              .Append(smr.enabled ? "" : " OFF").Append(smr.updateWhenOffscreen ? " uwo" : "").Append("; ");
        }
        foreach (Cloth c in eq.GetComponentsInChildren<Cloth>())
        {
            sb.Append(" || ");
            float mx = 0f; int pinned = 0;
            foreach (ClothSkinningCoefficient k in c.coefficients) { mx = Mathf.Max(mx, k.maxDistance); if (k.maxDistance <= 0f) pinned++; }
            sb.Append(" pin ").Append(pinned).Append(" caps ").Append(c.capsuleColliders.Length);
            foreach (CapsuleCollider cc in c.capsuleColliders) if (cc != null) sb.Append(" ").Append(cc.name).Append("@").Append(cc.transform.parent.name).Append(" r").Append((cc.radius * cc.transform.lossyScale.y).ToString("F2")).Append(" off").Append(eq.transform.InverseTransformPoint(cc.bounds.center).ToString("F2"));
            sb.Append(" TELA ").Append(c.name.Replace("Outfit_", "")).Append(" vert ").Append(c.vertices.Length).Append(" maxDist ").Append(mx > 1000f ? "LIBRE" : mx.ToString("F2"));
        }
        sb.Append(" SpringBones ").Append(Object.FindObjectsByType<SpringBone>().Length);
        foreach (SpringBone b in Object.FindObjectsByType<SpringBone>()) sb.Append(" [").Append(b.name).Append(b.enabled ? "" : " off").Append(" root ").Append(b.root ? b.root.name : "null").Append("]");
        return sb.ToString();
    }

    public static string Reequip()
    {
        Equipment eq = Player;
        eq.Apply();
        return "reaplicado";
    }

    public static string SkinProbe()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Player";
        var sb = new StringBuilder();
        foreach (SkinnedMeshRenderer smr in eq.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Mesh m = smr.sharedMesh;
            sb.Append(smr.name).Append(" root=").Append(smr.rootBone != null ? smr.rootBone.name : "null").Append(" [");
            var totals = new System.Collections.Generic.Dictionary<string, float>();
            if (m != null && m.isReadable)
            {
                BoneWeight[] bw = m.boneWeights;
                foreach (BoneWeight w in bw)
                {
                    void Add(int i, float v) { if (v <= 0f || i >= smr.bones.Length || smr.bones[i] == null) return; string n = smr.bones[i].name; totals[n] = (totals.TryGetValue(n, out float o) ? o : 0f) + v; }
                    Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                }
                foreach (var pair in totals) sb.Append(pair.Key).Append(":").Append((pair.Value / Mathf.Max(1, bw.Length)).ToString("F2")).Append(" ");
                sb.Append("| bp ").Append(m.bindposes.Length).Append(" bones ").Append(smr.bones.Length);
            }
            else sb.Append("no legible");
            sb.Append("]; ");
        }
        return sb.ToString();
    }

    // Tira el casco y el torso equipados: deben caer como piezas 3D, no como iconos.
    public static string DropArmor()
    {
        Equipment eq = Player;
        if (eq == null) return "sin Player";
        var sb = new StringBuilder();
        foreach (EquipSlot slot in new[] { EquipSlot.Head, EquipSlot.Chest, EquipSlot.Feet })
        {
            ItemStack s = eq.GetStack(slot);
            if (s == null || !eq.Unequip(slot, out _)) continue;
            Inventory inv = eq.Inventory;
            for (int i = 0; i < inv.Capacity; i++)
            {
                if (inv.Get(i) != s) continue;
                inv.RemoveAt(i);
                Transform t = eq.transform;
                WorldItem w = WorldItem.Spawn(s, t.position + Vector3.up * 1.2f + t.forward * 0.6f + t.right * (sb.Length > 0 ? 0.5f : -0.5f), t.forward * 1.8f + Vector3.up * 2.5f, eq.gameObject);
                sb.Append(s.item.id).Append(w != null && w.GetComponentInChildren<MeshRenderer>() != null ? " (modelo) " : " (icono) ");
                break;
            }
        }
        return sb.ToString();
    }

    public static string DropHelmet() => DropArmor();

    public static string CloseUp()
    {
        Equipment eq = Player;
        Transform t = eq.transform;
        Vector3 target = t.position + Vector3.up * 0.55f;
        Vector3 cam = target + t.forward * 2.3f + t.right * 1.1f + Vector3.up * 0.5f;
        return FrostboundBridge.CamShot("arma_primerplano", cam, target, 40f);
    }

    public static string CloseUpSide()
    {
        Equipment eq = Player;
        Transform t = eq.transform;
        Vector3 target = t.position + Vector3.up * 0.55f;
        Vector3 cam = target + t.right * -2.4f + Vector3.up * 0.4f + t.forward * 0.4f;
        return FrostboundBridge.CamShot("arma_lateral", cam, target, 40f);
    }
}
