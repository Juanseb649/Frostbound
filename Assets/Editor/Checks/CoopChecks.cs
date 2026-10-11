using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Pruebas del co-op con un mando virtual (exec CoopChecks.X).
public static class CoopChecks
{
    private static Gamepad Pad
    {
        get
        {
            foreach (Gamepad g in Gamepad.all) if (g.name.StartsWith("PruebaCoop")) return g;
            return InputSystem.AddDevice<Gamepad>("PruebaCoop");
        }
    }

    public static string Join()
    {
        var cm = CoopManager.Instance;
        if (cm == null) return "sin CoopManager";
        var db = GameDatabase.Instance;
        CharacterClass cls = db.classes.FirstOrDefault(c => c.className == "Mage") ?? db.classes[1];
        var pc = cm.Spawn(Pad, new CompanionData { classId = cls.name, plumageId = db.palette.entries[2].id, heroName = "Pingu Dos" }, false);
        return pc != null ? "entró " + pc.name : "no entró";
    }

    public static string OpenPicker()
    {
        CoopManager.Instance.OpenPicker(Pad);
        return "selector abierto";
    }

    public static string PressA() => Press(GamepadButton.South);
    public static string PressStart() => Press(GamepadButton.Start);
    public static string PressRight() => Press(GamepadButton.DpadRight);

    private static string Press(GamepadButton b)
    {
        Gamepad g = Pad;
        InputSystem.QueueStateEvent(g, new GamepadState().WithButton(b));
        return "pulsado " + b + " (suéltalo con Release)";
    }

    public static string StickRight()
    {
        InputSystem.QueueStateEvent(Pad, new GamepadState { leftStick = new Vector2(1f, 0f) });
        return "stick derecha";
    }

    public static string HoldAttack()
    {
        InputSystem.QueueStateEvent(Pad, new GamepadState().WithButton(GamepadButton.West));
        return "ataque mantenido";
    }

    public static string Release()
    {
        InputSystem.QueueStateEvent(Pad, new GamepadState());
        return "suelto";
    }

    public static string State()
    {
        var sb = new StringBuilder();
        sb.Append("jugadores=").Append(Players.Count);
        foreach (var p in Players.All)
        {
            var st = p.Stats;
            var eq = p.GetComponent<Equipment>();
            sb.Append(" | ").Append(p.name).Append(" #").Append(p.playerIndex).Append(" ").Append(st.characterClass != null ? st.characterClass.className : "?")
              .Append(" nv").Append(st.level).Append(" vida=").Append(Mathf.RoundToInt(st.currentHealth)).Append("/").Append(Mathf.RoundToInt(st.MaxHealth))
              .Append(" arma=").Append(eq.MainWeapon != null ? eq.MainWeapon.item.id : "-")
              .Append(" mochila=").Append(Enumerable.Range(0, eq.Inventory.Capacity).Count(i => eq.Inventory.Get(i) != null))
              .Append(" pos=").Append(p.transform.position.ToString("F1"))
              .Append(" mando=").Append(p.pad != null ? p.pad.name : "-");
        }
        var cam = Camera.main;
        if (cam != null) sb.Append(" | cam=").Append(cam.transform.position.ToString("F1"));
        var save = GameSession.Instance != null ? GameSession.Instance.Current : null;
        if (save != null) sb.Append(" | guardado_j2=").Append(save.companion.active).Append(" ").Append(save.companion.classId).Append(" nv").Append(save.companion.level).Append(" equipo=").Append(save.companion.equipment.Count);
        return sb.ToString();
    }

    public static string EnemyTargets()
    {
        int p1 = 0, p2 = 0;
        foreach (var e in EnemyBrain.All)
        {
            var t = e.Target;
            if (t == null) continue;
            var pc = t.GetComponent<PlayerController>();
            if (pc != null && pc.playerIndex == 0) p1++; else if (pc != null) p2++;
        }
        return "persiguen_j1=" + p1 + " persiguen_j2=" + p2;
    }

    public static string MoveP2NearEnemy()
    {
        var p2 = Players.Get(1);
        var e = EnemyBrain.All.Where(x => x != null && x.Current != EnemyBrain.State.Dead).OrderBy(x => (x.transform.position - p2.transform.position).sqrMagnitude).FirstOrDefault();
        if (e == null) return "sin enemigos";
        var rb = p2.GetComponent<Rigidbody>();
        Vector3 pos = e.transform.position + (p2.transform.position - e.transform.position).normalized * 1.5f;
        rb.position = pos;
        p2.transform.position = pos;
        var p1 = Players.Get(0);
        Vector3 back = pos + (pos - e.transform.position).normalized * 9f;
        p1.GetComponent<Rigidbody>().position = back;
        p1.transform.position = back;
        return "j2 junto a " + e.name + " vida=" + Mathf.RoundToInt(e.GetComponent<Damageable>().Health);
    }

    public static string NearestEnemyHealth()
    {
        var p2 = Players.Get(1);
        var e = EnemyBrain.All.Where(x => x != null).OrderBy(x => (x.transform.position - p2.transform.position).sqrMagnitude).FirstOrDefault();
        return e == null ? "sin enemigos" : e.name + " vida=" + Mathf.RoundToInt(e.GetComponent<Damageable>().Health) + " estado=" + e.Current;
    }

    public static string KillP2()
    {
        var st = Players.Get(1).Stats;
        st.invulnerable = false;
        st.TakeDamage(1e6f);
        return "j2 muerto=" + st.IsDead;
    }

    public static string KillP1()
    {
        var st = Players.Get(0).Stats;
        st.invulnerable = false;
        st.TakeDamage(1e6f);
        return "j1 muerto=" + st.IsDead;
    }

    public static string GiveP2Potion()
    {
        var eq = Players.Get(1).GetComponent<Equipment>();
        eq.Inventory.Add(GameDatabase.Instance.items.Find("health_potion"), 3);
        eq.Stats.TakeDamage(10f);
        return "vida=" + Mathf.RoundToInt(eq.Stats.currentHealth);
    }

    public static string PressDpadLeft() => Press(GamepadButton.DpadLeft);

    public static string Travel()
    {
        ZoneTravel.Go(SceneIds.Foothills, "");
        return "viajando";
    }

    public static string Leave()
    {
        CoopManager.Instance.Leave();
        return "salió";
    }

    public static string OpenP2Inventory() => Press(GamepadButton.Select);

    public static string InventoryOwner()
    {
        var inv = Object.FindAnyObjectByType<InventoryScreen>();
        return inv == null ? "sin inventario" : "abierto=" + inv.IsOpen + " dueño=" + (inv.Owner != null ? inv.Owner.name : "-");
    }

    public static string Belt()
    {
        var b = Players.Get(1).GetComponent<PotionBelt>();
        var st = Players.Get(1).Stats;
        return "cinturon=" + string.Join(",", b.slots.Select(x => x != null ? x.id : "-")) + " vida=" + Mathf.RoundToInt(st.currentHealth) + " mana=" + Mathf.RoundToInt(st.currentMana);
    }

    public static string Paused() => "pausa=" + PauseMenu.IsPaused + " j2=" + (Players.Get(1) != null);
}
