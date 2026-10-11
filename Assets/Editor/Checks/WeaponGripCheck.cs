using System.Text;
using UnityEditor;
using UnityEngine;

public static class WeaponGripCheck
{
    public static string Poses()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var sb = new StringBuilder();
        foreach (string id in new[] { "mage_staff", "spear", "sword", "claymore", "viking_axe" })
        {
            ItemDefinition item = db.items.Find(id);
            if (item == null) continue;
            GameObject go = Object.Instantiate(db.penguinPrefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            PenguinRigAnimator rig = go.GetComponentInChildren<PenguinRigAnimator>();
            WeaponHolder holder = rig.gameObject.GetComponent<WeaponHolder>();
            if (holder == null) holder = rig.gameObject.AddComponent<WeaponHolder>();
            holder.Show(item, null);
            rig.DebugPose(0f, 0f, PenguinRigAnimator.IdleAction.None, 0f);
            Transform w = holder.MainModel.transform;
            Vector3 tip = rig.transform.InverseTransformDirection(w.up);
            float tilt = Vector3.Angle(Vector3.up, tip);
            Vector3 handL = holder.LeftHand.position, handR = holder.RightHand.position;
            Vector3 a = w.position - w.up * 0.5f, b = w.position + w.up * 1.2f;
            Vector3 closest = a + Vector3.Project(handL - a, b - a);
            sb.Append(id).Append(": inclinación=").Append(tilt.ToString("F0")).Append("° punta=").Append(tip.ToString("F2"))
              .Append(" aletaIzq→mango=").Append(Vector3.Distance(handL, closest).ToString("F2")).Append("m")
              .Append(" manoDer-local=").Append(rig.transform.InverseTransformPoint(handR).ToString("F2"))
              .Append(" manoIzq-local=").Append(rig.transform.InverseTransformPoint(handL).ToString("F2"))
              .Append(" hombroIzq=").Append(rig.transform.InverseTransformPoint(holder.LeftHand.parent.position).ToString("F2"))
              .Append(" base=").Append(rig.transform.InverseTransformPoint(w.position - w.up * 0.45f).ToString("F2")).Append(" | ");
            Object.DestroyImmediate(go);
        }
        return sb.ToString();
    }

    public static string Tune()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var sb = new StringBuilder();
        ItemDefinition item = db.items.Find("mage_staff");
        GameObject go = Object.Instantiate(db.penguinPrefab);
        go.hideFlags = HideFlags.HideAndDontSave;
        PenguinRigAnimator rig = go.GetComponentInChildren<PenguinRigAnimator>();
        WeaponHolder holder = rig.gameObject.AddComponent<WeaponHolder>();
        holder.Show(item, null);
        sb.Append("defecto swing=").Append(rig.polearmSwing).Append(" out=").Append(rig.polearmOut).Append(" | ");
        foreach (float sw in new[] { 50f, 62f, 74f })
            foreach (float o in new[] { -45f, -58f, -70f })
            {
                rig.polearmSwing = sw;
                rig.polearmOut = o;
                rig.DebugPose(0f, 0f, PenguinRigAnimator.IdleAction.None, 0f);
                Transform w = holder.MainModel.transform;
                Vector3 handL = holder.LeftHand.position;
                Vector3 a = w.position - w.up * 0.5f, b = w.position + w.up * 1.2f;
                Vector3 closest = a + Vector3.Project(handL - a, b - a);
                sb.Append(sw).Append("/").Append(o).Append(": R=").Append(rig.transform.InverseTransformPoint(holder.RightHand.position).ToString("F2"))
                  .Append(" L→asta=").Append(Vector3.Distance(handL, closest).ToString("F2"))
                  .Append(" incl=").Append(Vector3.Angle(rig.transform.up, w.up).ToString("F0"));
                float minBody = 9f;
                for (float k = -0.4f; k <= 0.6f; k += 0.05f)
                {
                    Vector3 q = rig.transform.InverseTransformPoint(w.position + w.up * k);
                    if (q.y < 0.15f || q.y > 1.05f) continue;
                    minBody = Mathf.Min(minBody, new Vector2(q.x, q.z).magnitude);
                }
                sb.Append(" cuerpo=").Append(minBody.ToString("F2")).Append(" | ");
            }
        var f = typeof(PenguinRigAnimator).GetField("_bodyRadius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fc = typeof(PenguinRigAnimator).GetField("_bodyCenterLocal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        sb.Append("cuerpo r=").Append(((float)f.GetValue(rig)).ToString("F2")).Append(" centro=").Append(((Vector3)fc.GetValue(rig)).ToString("F2"));
        Renderer body = null;
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()) if (body == null || r.bounds.size.sqrMagnitude > body.bounds.size.sqrMagnitude) body = r;
        if (body != null) sb.Append(" malla=").Append(body.name).Append(" ").Append(rig.transform.InverseTransformPoint(body.bounds.center).ToString("F2")).Append(" tam=").Append(body.bounds.size.ToString("F2"));
        Object.DestroyImmediate(go);
        return sb.ToString();
    }

    public static string GearParts()
    {
        var sb = new StringBuilder();
        foreach (string g in new[] { "Ninja" })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Gear/Frostbound_" + g + "_Gear.fbx");
            if (go == null) continue;
            sb.Append(g).Append(": ");
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string bones = "";
                foreach (var b in smr.bones) if (b != null && (b.name.Contains("Weapon") || b.name.Contains("Hand") || b.name.Contains("Offhand"))) bones += b.name + ",";
                sb.Append(smr.name).Append("(").Append(smr.sharedMesh.bounds.min.ToString("F2")).Append("..").Append(smr.sharedMesh.bounds.max.ToString("F2")).Append(" mats=");
                foreach (var m in smr.sharedMaterials) sb.Append(m != null ? m.name : "null").Append(",");
                sb.Append(") ");
            }
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true)) sb.Append("[rigido ").Append(mr.name).Append("] ");
            sb.Append(" | ");
        }
        return sb.ToString();
    }

    // ¿El arma pasa por dentro de la aleta? Distancia del eje del arma a los vértices de la aleta derecha (malla deformada).
    public static string FlipperContact()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var sb = new StringBuilder();
        foreach (string id in new[] { "sword", "mage_staff", "spear", "claymore", "knife" })
        {
            ItemDefinition item = db.items.Find(id);
            GameObject go = Object.Instantiate(db.penguinPrefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            PenguinRigAnimator rig = go.GetComponentInChildren<PenguinRigAnimator>();
            WeaponHolder holder = rig.gameObject.AddComponent<WeaponHolder>();
            holder.Show(item, null);
            rig.DebugPose(0f, 0f, PenguinRigAnimator.IdleAction.None, 0f);
            Transform w = holder.MainModel.transform;
            float best = 9f, handDist = 9f, at = 0f; int n = 0;
            foreach (var body in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                body.BakeMesh(baked, true);
                int flip = System.Array.FindIndex(body.bones, bn => bn != null && (bn.name == "Flipper_R" || bn.name == "Anchor_Hand_R"));
                if (flip < 0) continue;
                BoneWeight[] bw = body.sharedMesh.boneWeights;
                Vector3[] v = baked.vertices;
                for (int i = 0; i < v.Length && i < bw.Length; i++)
                {
                    bool mine = (bw[i].boneIndex0 == flip && bw[i].weight0 > 0.5f) || (body.bones[bw[i].boneIndex0] != null && body.bones[bw[i].boneIndex0].name.Contains("Hand_R") && bw[i].weight0 > 0.5f);
                    if (!mine) continue;
                    n++;
                    Vector3 p = body.transform.TransformPoint(v[i]);
                    Vector3 rel = p - w.position;
                    float along = Vector3.Dot(rel, w.up);
                    float d = (rel - w.up * along).magnitude;
                    if (d < best) { best = d; at = along; }
                    handDist = Mathf.Min(handDist, rel.magnitude);
                }
                Object.DestroyImmediate(baked);
            }
            var tipAt = new Vector3(at, 0f, 0f);
            sb.Append(id).Append(": verticesAleta=").Append(n).Append(" eje→aleta=").Append(best.ToString("F3")).Append(" en=").Append(tipAt.x.ToString("F2"))
              .Append(" agarre→aleta=").Append(handDist.ToString("F3")).Append(" | ");
            Object.DestroyImmediate(go);
        }
        return sb.ToString();
    }

    public static string MageParts()
    {
        var sb = new StringBuilder();
        var gear = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Gear/Frostbound_Mage_Gear.fbx");
        foreach (var smr in gear.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            sb.Append(smr.name).Append(" mats=");
            foreach (var m in smr.sharedMaterials) sb.Append(m != null ? m.name + "(" + m.shader.name + ")" : "null").Append(",");
            sb.Append(" b=").Append(smr.sharedMesh.bounds.min.ToString("F2")).Append("..").Append(smr.sharedMesh.bounds.max.ToString("F2"));
            sb.Append(" colores=").Append(smr.sharedMesh.colors.Length > 0).Append(" | ");
        }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        foreach (var smr in db.penguinPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            sb.Append("PING ").Append(smr.name).Append(" v=").Append(smr.sharedMesh.vertexCount).Append(" huesos=");
            foreach (var b in smr.bones) sb.Append(b != null ? b.name : "null").Append(",");
            sb.Append(" b=").Append(smr.sharedMesh.bounds.min.ToString("F2")).Append("..").Append(smr.sharedMesh.bounds.max.ToString("F2"));
            sb.Append(" mats=");
            foreach (var m in smr.sharedMaterials) sb.Append(m != null ? m.name + "(" + m.shader.name + ")" : "null").Append(",");
            sb.Append(" | ");
        }
        foreach (string g in new[] { "Pieza_Mage_Torso", "Pieza_Mage_Casco" })
        {
            var o = AssetDatabase.LoadAssetAtPath<OutfitItem>("Assets/Data/Outfits/Pieces/" + g + ".asset");
            sb.Append(g).Append(" slot=").Append(o.slot).Append(" mat=").Append(o.materialOverride != null ? o.materialOverride.name : "-").Append(" | ");
        }
        return sb.ToString();
    }

    // ¿La cabeza asoma por la capucha del shinobi? Por direcciones desde el centro de la cabeza, compara el radio
    // de la cabeza con el de la capucha (mallas deformadas) en reposo y caminando.
    public static string HoodGaps()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var hoodItem = AssetDatabase.LoadAssetAtPath<OutfitItem>("Assets/Data/Outfits/Pieces/Pieza_Ninja_Casco.asset");
        var sb = new StringBuilder();
        GameObject go = Object.Instantiate(db.penguinPrefab);
        go.hideFlags = HideFlags.HideAndDontSave;
        var outfit = go.GetComponentInChildren<PenguinOutfit>();
        outfit.useClassOutfit = false;
        outfit.Equip(hoodItem);
        var rig = go.GetComponentInChildren<PenguinRigAnimator>();
        foreach (var pose in new[] { (0f, 0f), (1.2f, 1f), (2.6f, 1f), (4.2f, 1f) })
        {
            rig.DebugPose(pose.Item1, pose.Item2, PenguinRigAnimator.IdleAction.None, 0f);
            SkinnedMeshRenderer body = null, hood = null;
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r.name == "Penguin_Body") body = r;
                if (r.name.Contains("Hood")) hood = r;
            }
            if (hood == null) return "sin capucha";
            Transform head = System.Array.Find(body.bones, b => b.name == "Head");
            Vector3 c = head.position + head.up * 0.12f;
            var hb = new Mesh(); hood.BakeMesh(hb, true);
            var bb = new Mesh(); body.BakeMesh(bb, true);
            var hoodR = new System.Collections.Generic.Dictionary<int, float>();
            foreach (Vector3 v in hb.vertices)
            {
                Vector3 w = hood.transform.TransformPoint(v) - c;
                int k = Bin(head.InverseTransformDirection(w));
                hoodR[k] = Mathf.Max(hoodR.TryGetValue(k, out float r0) ? r0 : 0f, w.magnitude);
            }
            int out1 = 0, total = 0; float worst = 0f; Vector3 worstDir = Vector3.zero; float minGap = 9f;
            int headIdx = System.Array.FindIndex(body.bones, b => b.name == "Head");
            BoneWeight[] bw = body.sharedMesh.boneWeights;
            Vector3[] bv = bb.vertices;
            for (int i = 0; i < bv.Length; i++)
            {
                if (bw[i].boneIndex0 != headIdx || bw[i].weight0 < 0.5f) continue;
                Vector3 w = body.transform.TransformPoint(bv[i]) - c;
                Vector3 local = head.InverseTransformDirection(w);
                if (local.y < -0.02f) continue;
                total++;
                int k = Bin(local);
                if (!hoodR.TryGetValue(k, out float hr)) continue;
                float poke = w.magnitude - hr;
                if (local.y > 0.05f) minGap = Mathf.Min(minGap, -poke);
                if (poke > 0.002f) { out1++; if (poke > worst) { worst = poke; worstDir = local.normalized; } }
            }
            sb.Append("pose ").Append(pose.Item1).Append(": asoman ").Append(out1).Append("/").Append(total).Append(" peor=").Append(worst.ToString("F3")).Append(" holguraMin=").Append(minGap.ToString("F3")).Append(" | ");
            Object.DestroyImmediate(hb); Object.DestroyImmediate(bb);
        }
        Object.DestroyImmediate(go);
        return sb.ToString();
    }

    private static int Bin(Vector3 d)
    {
        d.Normalize();
        int a = Mathf.Clamp(Mathf.FloorToInt((Mathf.Atan2(d.x, d.z) + Mathf.PI) / (2f * Mathf.PI) * 24f), 0, 23);
        int b = Mathf.Clamp(Mathf.FloorToInt((d.y + 1f) * 0.5f * 12f), 0, 11);
        return a * 12 + b;
    }

    public static string KatanaBack()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Gear/Frostbound_Ninja_Gear.fbx");
        var sb = new StringBuilder();
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.name != "Katana_Back" && smr.name != "Hood") continue;
            Mesh m = smr.sharedMesh;
            Vector3[] v = m.vertices;
            for (int sub = 0; sub < m.subMeshCount; sub++)
            {
                int[] t = m.GetTriangles(sub);
                Bounds b = new Bounds(v[t[0]], Vector3.zero);
                foreach (int i in t) b.Encapsulate(v[i]);
                sb.Append(smr.name).Append("[").Append(sub).Append(" ").Append(smr.sharedMaterials[sub].name).Append("] ").Append(b.min.ToString("F2")).Append("..").Append(b.max.ToString("F2")).Append(" tris=").Append(t.Length / 3).Append(" | ");
            }
            var bones = new StringBuilder();
            BoneWeight[] w = m.boneWeights;
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var bw in w) { string n = smr.bones[bw.boneIndex0].name; counts[n] = counts.TryGetValue(n, out int c) ? c + 1 : 1; }
            foreach (var kv in counts) sb.Append(kv.Key).Append("=").Append(kv.Value).Append(" ");
            sb.Append(" || ");
        }
        return sb.ToString();
    }

    // Bordes abiertos de la capucha (aristas con un solo triángulo), por altura: un agujero arriba se vería al moverse.
    public static string HoodHolesOriginal() => HoodHoles(true);

    public static string HoodHoles() => HoodHoles(false);

    private static string HoodHoles(bool original)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Gear/Generated/Ninja_HoodFull.asset");
        if (original)
            foreach (var smr in AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Gear/Frostbound_Ninja_Gear.fbx").GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.name == "Hood") mesh = smr.sharedMesh;
        Vector3[] v = mesh.vertices;
        var key = new System.Collections.Generic.Dictionary<Vector3, int>();
        int[] id = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 q = new Vector3(Mathf.Round(v[i].x * 1000f), Mathf.Round(v[i].y * 1000f), Mathf.Round(v[i].z * 1000f));
            if (!key.TryGetValue(q, out int k)) { k = key.Count; key[q] = k; }
            id[i] = k;
        }
        var edges = new System.Collections.Generic.Dictionary<long, int>();
        int[] t = mesh.triangles;
        for (int i = 0; i < t.Length; i += 3)
            for (int e = 0; e < 3; e++)
            {
                int a = id[t[i + e]], b = id[t[i + (e + 1) % 3]];
                long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                edges[k] = edges.TryGetValue(k, out int c) ? c + 1 : 1;
            }
        var pos = new Vector3[key.Count];
        foreach (var kv in key) pos[kv.Value] = kv.Key / 1000f;
        int top = 0, all = 0; float maxY = -9f; Vector3 sample = Vector3.zero;
        foreach (var kv in edges)
        {
            if (kv.Value != 1) continue;
            all++;
            Vector3 a = pos[(int)(kv.Key >> 32)];
            if (a.y > 0.85f) { top++; if (a.y > maxY) { maxY = a.y; sample = a; } }
        }
        return "bordes abiertos=" + all + " arriba(>0.85)=" + top + " más alto=" + sample.ToString("F2") + " limites=" + mesh.bounds.min.ToString("F2") + ".." + mesh.bounds.max.ToString("F2");
    }

    public static string Models()
    {
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Resources/GameDatabase.asset");
        var sb = new StringBuilder();
        foreach (ItemDefinition item in db.items.items)
        {
            if (item == null || !item.IsWeapon || item.weaponModel == null) continue;
            Bounds b = new Bounds();
            bool init = false;
            float nearest = float.MaxValue;
            foreach (MeshFilter mf in item.weaponModel.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = item.weaponModel.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                {
                    Vector3 p = m.MultiplyPoint3x4(v);
                    if (!init) { b = new Bounds(p, Vector3.zero); init = true; } else b.Encapsulate(p);
                    nearest = Mathf.Min(nearest, new Vector2(p.x, p.z).magnitude + Mathf.Max(0f, Mathf.Abs(p.y) - 0.05f));
                }
            }
            sb.Append(item.name).Append(" [").Append(item.weaponType).Append("/").Append(item.WeaponInfo.grip).Append("] y=")
              .Append(b.min.y.ToString("F2")).Append("..").Append(b.max.y.ToString("F2")).Append(" xz=")
              .Append(b.extents.x.ToString("F2")).Append("/").Append(b.extents.z.ToString("F2")).Append(" centro=").Append(b.center.ToString("F2"))
              .Append(" escala=").Append(item.weaponModel.transform.localScale.ToString("F2")).Append(" | ");
        }
        return sb.ToString();
    }
}
