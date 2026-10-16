using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class AudioChecks
{
    public static string State()
    {
        var m = AudioManager.Instance;
        var cur = typeof(AudioManager).GetField("_currentAmbience", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m);
        var src = typeof(AudioManager).GetField("_activeMusicSource", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m) as AudioSource;
        var b = BossController.Active;
        return "música " + cur + " (" + (src != null && src.clip != null ? src.clip.name : "-") + ") · jefe " + (b != null ? b.title + (b.Engaged ? " EN PELEA" : " quieto") : "ninguno");
    }

    public static string Resources()
    {
        var sb = new StringBuilder();
        string[] names = { "Ignis", "Toxis", "Glacies", "Fulgur", "Sanguis", "Acies", "Celeris", "Impetus" };
        int ok = names.Count(n => AudioManager.Has("SFX/Rune_" + n + "_Proc") && AudioManager.Has("SFX/Rune_" + n + "_Engrave"));
        sb.Append("runas ").Append(ok).Append("/8 · pociones ").Append(AudioManager.Has("SFX/Potion_Health") && AudioManager.Has("SFX/Potion_Mana"));
        sb.Append(" · portal ").Append(AudioManager.Has("SFX/Portal_Open"));
        foreach (string bank in new[] { CombatAudio.SwingLight, CombatAudio.SwingHeavy, CombatAudio.Bow, CombatAudio.Throw, CombatAudio.Magic, CombatAudio.Gore, CombatAudio.GoreCrit, CombatAudio.GoreDeath, CombatAudio.Hurt, CombatAudio.Metal })
            sb.Append(" · ").Append(bank.Replace("Combate/", "").TrimEnd('/')).Append(AudioManager.Has(bank) ? " sí" : " no");
        return sb.ToString();
    }

    public static string GoToRocco()
    {
        var p = GameObject.Find("Player");
        Vector3 c = StairsLayout.ArenaCenter + new Vector3(0, 0.2f, 6f);
        p.GetComponent<PlayerPersistence>().MoveTo(c, Quaternion.identity);
        p.GetComponent<CharacterStats>().invulnerable = true;
        return "en la explanada";
    }
}
