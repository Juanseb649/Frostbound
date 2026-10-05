using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Comprobaciones de las animaciones de arco y de armas de dos manos.
public static class WeaponAnimChecks
{
    public static string ModelInfo()
    {
        var sb = new StringBuilder();
        foreach (string n in new[] { "W_bow", "W_longbow", "W_arrow", "W_claymore", "W_longaxe", "W_maul", "W_crossbow" })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Weapons/" + n + ".fbx");
            if (go == null) { sb.Append(n).Append(": no\n"); continue; }
            sb.Append(n).Append(":");
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                var v = m.vertices;
                Vector3 top = v.OrderByDescending(p => p.y).First(), bottom = v.OrderBy(p => p.y).First();
                sb.Append(" [").Append(mf.name).Append(" b=").Append(m.bounds.ToString("F3")).Append(" top=").Append(top.ToString("F3")).Append(" bot=").Append(bottom.ToString("F3"))
                  .Append(" sub=").Append(m.subMeshCount).Append(" mats=").Append(string.Join("/", mf.GetComponent<MeshRenderer>().sharedMaterials.Select(x => x != null ? x.name : "-"))).Append(" t=").Append(mf.transform.localPosition.ToString("F2")).Append(mf.transform.localEulerAngles.ToString("F0")).Append(mf.transform.localScale.ToString("F2")).Append("]");
            }
            sb.Append("\n");
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../FrostboundBridge/models.txt"), sb.ToString());
        return "ok";
    }

    public static string ArcherInfo()
    {
        var def = GameDatabase.Instance.FindEnemy("Corrupt_Ranged");
        var player = GameObject.Find("Player").transform;
        var b = EnemySpawner.Spawn(def, player.position + player.forward * 4f, 180f, null, 1, false, new DeterministicRng(1));
        b.paused = true;
        var sb = new StringBuilder();
        foreach (var r in b.GetComponentsInChildren<Renderer>(true))
            sb.Append(r.name).Append(" (").Append(r.GetType().Name).Append(") mats=").Append(string.Join("/", r.sharedMaterials.Select(m => m != null ? m.name : "-")))
              .Append(r is SkinnedMeshRenderer smr && smr.rootBone != null ? " root=" + smr.rootBone.name : "").Append(" parent=").Append(r.transform.parent != null ? r.transform.parent.name : "-").Append("\n");
        foreach (var t in b.GetComponentsInChildren<Transform>(true))
            if (t.name.Contains("Flipper") || t.name.Contains("Spine") || t.name.Contains("Anchor") || t.name.Contains("Head") || t.name.Contains("Hips"))
                sb.Append("bone ").Append(t.name).Append(" <- ").Append(t.parent.name).Append("\n");
        sb.Append("holder ").Append(b.GetComponentInChildren<WeaponHolder>() != null).Append(" rig ").Append(b.GetComponentInChildren<PenguinRigAnimator>()?.GetType().Name);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../FrostboundBridge/archer.txt"), sb.ToString());
        return "ok";
    }

    private static int _shot;
    private static Transform P => GameObject.Find("Player").transform;

    public static string EquipClaymore() => FrostboundWeaponTests.EquipClaymoreFire();
    public static string EquipMaul() => FrostboundWeaponTests.EquipMaul();
    public static string EquipBow() => FrostboundWeaponTests.EquipBowFrost();
    public static string EquipLongbow() => FrostboundWeaponTests.EquipLongbow();

    public static string Attack()
    {
        var c = P.GetComponent<PlayerCombat>();
        bool ok = c.TryAttack(P.position + P.forward * 5f);
        return "ataque " + ok + " pesado " + c.UsesHeavyCombo + " arco " + c.UsesBow;
    }

    public static string SlowMo() { Time.timeScale = 0.15f; return "x0.15"; }
    public static string Normal() { Time.timeScale = 1f; return "x1"; }
    public static string ResetShots() { _shot = 0; return "ok"; }

    // Cámara cercana a un lado del héroe (perfil derecho, algo por delante).
    public static string Side()
    {
        Transform p = P;
        Vector3 pos = p.position + p.right * 3.2f + p.forward * 1.2f + Vector3.up * 1.6f;
        return FrostboundBridge.CamShot("anim_" + (_shot++), pos, p.position + Vector3.up * 0.8f + p.forward * 0.4f, 45f);
    }

    public static string Front()
    {
        Transform p = P;
        Vector3 pos = p.position + p.forward * 3.4f - p.right * 1.4f + Vector3.up * 1.8f;
        return FrostboundBridge.CamShot("anim_" + (_shot++), pos, p.position + Vector3.up * 0.8f, 45f);
    }

    public static string RigState()
    {
        var rig = P.GetComponentInChildren<PenguinRigAnimator>();
        var arch = P.GetComponentInChildren<ArcheryRig>();
        return "aim " + rig.BowAimWeight.ToString("0.00") + " draw " + rig.BowDraw01.ToString("0.00") + " nock " + rig.BowNocked + " shot " + rig.BowShotProgress.ToString("0.00")
            + " pesado " + rig.HeavyCombo + " paso " + rig.ComboStep + " archery " + (arch != null && arch.Ready) + (arch != null && arch.Ready ? " lanza " + arch.LaunchPoint.ToString("0.00") + " | " + arch.DebugState() : "");
    }

    // Arquero corrupto delante del héroe, apuntándole.
    public static string SpawnArcher()
    {
        Transform p = P;
        var def = GameDatabase.Instance.FindEnemy("Corrupt_Ranged");
        var b = EnemySpawner.Spawn(def, p.position + p.forward * 7f, p.eulerAngles.y + 180f, null, 1, false, new DeterministicRng(3));
        b.leashOverride = 99f;
        b.Alert();
        var arch = b.GetComponentInChildren<ArcheryRig>();
        return "arquero " + (arch != null && arch.Ready);
    }

    public static string ArcherShot()
    {
        var b = Object.FindObjectsByType<EnemyBrain>().FirstOrDefault(e => e.definition != null && e.definition.id == "Corrupt_Ranged" && !e.IsDead);
        if (b == null) return "sin arquero";
        Transform t = b.transform;
        Vector3 pos = t.position + t.right * 3f + t.forward * 1.5f + Vector3.up * 1.6f;
        var rig = b.GetComponentInChildren<PenguinRigAnimator>();
        return FrostboundBridge.CamShot("archer_" + (_shot++), pos, t.position + Vector3.up * 0.8f, 45f) + " draw " + rig.BowDraw01.ToString("0.00") + " aim " + rig.BowAimWeight.ToString("0.00");
    }

    private static string Heavy(int step) { P.GetComponentInChildren<PenguinRigAnimator>().PlayHeavyCombo(step, 1.3f); return "pesado " + step; }
    public static string Heavy1() => Heavy(1);
    public static string Heavy2() => Heavy(2);
    public static string Heavy3() => Heavy(3);
    public static string Bow() { P.GetComponentInChildren<PenguinRigAnimator>().PlayBowShot(1.3f, 0.64f); return "arco"; }

    // Los arcos y las flechas necesitan la malla legible (se quita la cuerda y se dibuja aparte).
    public static string MakeBowsReadable()
    {
        int n = 0;
        foreach (string name in new[] { "W_bow", "W_longbow", "W_arrow", "W_bolt" })
        {
            var imp = AssetImporter.GetAtPath("Assets/Models/Weapons/" + name + ".fbx") as ModelImporter;
            if (imp == null || imp.isReadable) continue;
            imp.isReadable = true;
            imp.SaveAndReimport();
            n++;
        }
        return "legibles " + n;
    }
}
