using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Mide el balanceo del héroe frame a frame mientras arranca, camina y se detiene.
// exec SwaySampler.Start → espera ~4 s → exec SwaySampler.Report
public static class SwaySampler
{
    private struct Sample { public float t, speed, y, roll, pitch; }

    private static readonly List<Sample> Samples = new List<Sample>();
    private static PenguinBodySway _sway;
    private static Rigidbody _rb;
    private static int _frames;
    private static Vector3 _origin;

    public static string Start()
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player == null) return "sin Player";
        _sway = player.GetComponent<PenguinBodySway>();
        _rb = player.GetComponent<Rigidbody>();
        Samples.Clear();
        _frames = 0;
        _origin = player.transform.position;
        player.MoveTo(_origin + player.transform.forward * 7f);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        return "midiendo";
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || _sway == null || _sway.model == null) { EditorApplication.update -= Tick; return; }
        Transform m = _sway.model;
        Vector3 e = m.localEulerAngles;
        Samples.Add(new Sample
        {
            t = Time.time,
            speed = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude,
            y = m.localPosition.y,
            roll = Mathf.DeltaAngle(0f, e.z),
            pitch = Mathf.DeltaAngle(0f, e.x)
        });
        if (++_frames > 600) EditorApplication.update -= Tick;
    }

    public static string Report()
    {
        EditorApplication.update -= Tick;
        if (Samples.Count < 3) return "sin muestras";
        var sb = new StringBuilder("t,speed,y,roll,pitch\n");
        float maxJerkY = 0f, maxJerkRoll = 0f;
        int signFlipsRoll = 0;
        for (int i = 0; i < Samples.Count; i++)
        {
            Sample s = Samples[i];
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F2},{2:F4},{3:F2},{4:F2}", s.t, s.speed, s.y, s.roll, s.pitch));
            if (i < 2) continue;
            float dy1 = Samples[i].y - Samples[i - 1].y, dy0 = Samples[i - 1].y - Samples[i - 2].y;
            float dr1 = Samples[i].roll - Samples[i - 1].roll, dr0 = Samples[i - 1].roll - Samples[i - 2].roll;
            maxJerkY = Mathf.Max(maxJerkY, Mathf.Abs(dy1 - dy0));
            maxJerkRoll = Mathf.Max(maxJerkRoll, Mathf.Abs(dr1 - dr0));
            if (Mathf.Sign(dr1) != Mathf.Sign(dr0) && Mathf.Abs(dr1) > 0.2f && Mathf.Abs(dr0) > 0.2f) signFlipsRoll++;
        }
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "FrostboundBridge", "sway.csv"), sb.ToString());
        return string.Format(CultureInfo.InvariantCulture, "frames {0} | salto máx. altura {1:F4} m | salto máx. roll {2:F2}° | cambios bruscos de roll {3}",
            Samples.Count, maxJerkY, maxJerkRoll, signFlipsRoll);
    }
}
