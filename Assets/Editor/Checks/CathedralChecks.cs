using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

// Diagnóstico de la Catedral Corrupta (el interior del castillo): qué ambiente, niebla, cielo y posprocesado
// hay al entrar. Por el bridge: exec CathedralChecks.Enter → wait → exec CathedralChecks.State
public static class CathedralChecks
{
    public static string Enter()
    {
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelBarbarian);
        if (Players.Primary != null && Players.Primary.Stats != null) Players.Primary.Stats.invulnerable = true;
        CastleInterior.Instance.Enter();
        return "entrando";
    }

    public static string State()
    {
        var sb = new StringBuilder();
        var i = CastleInterior.Instance;
        sb.Append("dentro=").Append(i != null && i.PlayerInside);
        sb.Append(" | ambiente ").Append(RenderSettings.ambientMode).Append(' ').Append(RenderSettings.ambientLight)
          .Append(" cielo/eq/suelo ").Append(RenderSettings.ambientSkyColor).Append(RenderSettings.ambientEquatorColor).Append(RenderSettings.ambientGroundColor);
        sb.Append(" | niebla ").Append(RenderSettings.fog).Append(' ').Append(RenderSettings.fogMode).Append(' ').Append(RenderSettings.fogColor)
          .Append(" dens=").Append(RenderSettings.fogDensity).Append(" ini/fin=").Append(RenderSettings.fogStartDistance).Append('/').Append(RenderSettings.fogEndDistance);
        sb.Append(" | skybox=").Append(RenderSettings.skybox != null ? RenderSettings.skybox.name : "-");
        Camera cam = Camera.main;
        if (cam != null) sb.Append(" | cámara clear=").Append(cam.clearFlags).Append(" fondo=").Append(cam.backgroundColor).Append(" far=").Append(cam.farClipPlane);
        foreach (Light l in Object.FindObjectsByType<Light>())
            if (l.type == LightType.Directional && l.isActiveAndEnabled) sb.Append(" | sol ").Append(l.name).Append(" i=").Append(l.intensity.ToString("0.00")).Append(' ').Append(l.color);
        foreach (Volume v in Object.FindObjectsByType<Volume>())
        {
            if (!v.isActiveAndEnabled) continue;
            sb.Append(" | volumen ").Append(v.name).Append(" p=").Append(v.priority).Append(" w=").Append(v.weight).Append(" global=").Append(v.isGlobal).Append(" [");
            VolumeProfile p = v.sharedProfile;
            if (p != null) foreach (VolumeComponent c in p.components) if (c.active) sb.Append(c.GetType().Name).Append(' ');
            sb.Append(']');
        }
        return sb.ToString();
    }

    public static string GoRoom()
    {
        var i = CastleInterior.Instance;
        var room = i.Layout.rooms.Find(r => r.theme != CastleInteriorLayout.Theme.Vestibule && r.theme != CastleInteriorLayout.Theme.Throne);
        PlayerPersistence mover = Object.FindAnyObjectByType<PlayerPersistence>();
        mover.MoveTo(CastleInterior.CellToWorld(room.Center.x, room.Center.y) + Vector3.back * 3f, Quaternion.identity);
        return CastleInteriorLayout.ThemeName(room.theme);
    }
}
