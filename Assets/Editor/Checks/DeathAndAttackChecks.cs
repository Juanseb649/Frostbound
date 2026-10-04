using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DeathAndAttackChecks
{
    private static EnemyBrain _enemy;
    private static Transform _flipper;
    private static Quaternion _rest;
    private static float _maxAngle;
    private static int _strikes, _frames;
    private static bool _wasSwinging;
    private static float _peakAt;

    public static string FaceMelee()
    {
        GameObject p = GameObject.Find("Player");
        if (p == null) return "sin Player";
        EnemyBrain e = Object.FindObjectsByType<EnemyBrain>()
            .Where(b => !b.IsDead && b.definition != null && !b.definition.ranged)
            .OrderBy(b => Vector3.Distance(b.transform.position, p.transform.position)).FirstOrDefault();
        if (e == null) return "sin enemigos cuerpo a cuerpo";
        p.GetComponent<CharacterStats>().invulnerable = true;
        Vector3 pos = e.transform.position + e.transform.forward * 1.3f;
        var rb = p.GetComponent<Rigidbody>();
        if (rb != null) rb.position = pos;
        p.transform.position = pos;
        _enemy = e;
        _flipper = e.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Flipper_R");
        _rest = _flipper != null ? _flipper.localRotation : Quaternion.identity;
        _maxAngle = 0f;
        _strikes = 0;
        _frames = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        e.Alert();
        return e.name + " (" + e.definition.displayName + ")";
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || _enemy == null || _flipper == null || ++_frames > 900) { EditorApplication.update -= Tick; return; }
        float a = Quaternion.Angle(_rest, _flipper.localRotation);
        if (a > _maxAngle) { _maxAngle = a; _peakAt = Time.time; }
        var rig = _enemy.GetComponentInChildren<PenguinLocomotionAnimator>();
        bool swinging = rig != null && rig.Swinging;
        if (swinging && !_wasSwinging) _strikes++;
        _wasSwinging = swinging;
    }

    public static string AttackReport()
    {
        EditorApplication.update -= Tick;
        return string.Format(CultureInfo.InvariantCulture, "golpes animados {0} · giro máx. de la aleta {1:F0}° · estado {2}", _strikes, _maxAngle, _enemy != null ? _enemy.Current.ToString() : "-");
    }

    public static string Mortal()
    {
        GameObject p = GameObject.Find("Player");
        if (p == null) return "sin Player";
        p.GetComponent<CharacterStats>().invulnerable = false;
        return "ok";
    }

    public static string KillPlayer()
    {
        GameObject p = GameObject.Find("Player");
        var stats = p.GetComponent<CharacterStats>();
        stats.invulnerable = false;
        stats.lastHitDirection = -p.transform.forward;
        stats.TakeDamage(stats.MaxHealth * 100f);
        return "vida " + stats.currentHealth;
    }

    public static string DeathState()
    {
        GameObject p = GameObject.Find("Player");
        var rag = p.GetComponent<PenguinRagdoll>();
        var screen = Object.FindAnyObjectByType<DeathScreen>();
        var hips = p.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Hips");
        return "ragdoll " + (rag != null && rag.IsActive) + " cuerpos " + (rag != null ? rag.Bodies.Count : 0)
            + " · cadera y " + (hips != null ? hips.position.y.ToString("F2", CultureInfo.InvariantCulture) : "-")
            + " · pantalla " + (screen != null) + " · bloqueo " + GameplayInput.Blocked;
    }

    public static string ChooseRespawn() => Choose(0);
    public static string ChooseTitle() => Choose(1);

    private static string Choose(int index)
    {
        var screen = Object.FindAnyObjectByType<DeathScreen>();
        if (screen == null) return "sin pantalla";
        var buttons = screen.GetComponentsInChildren<Button>();
        if (buttons.Length <= index) return "sin botón";
        buttons[index].onClick.Invoke();
        return "ok";
    }

    public static string AfterRespawn()
    {
        GameObject p = GameObject.Find("Player");
        var stats = p.GetComponent<CharacterStats>();
        var rag = p.GetComponent<PenguinRagdoll>();
        var rb = p.GetComponent<Rigidbody>();
        int missing = p.GetComponentsInChildren<Rigidbody>().Length;
        return "vida " + Mathf.CeilToInt(stats.currentHealth) + "/" + Mathf.CeilToInt(stats.MaxHealth)
            + " · ragdoll " + (rag != null && rag.IsActive) + " · rigidbodies " + missing
            + " · cinemático " + (rb != null && rb.isKinematic) + " · control " + p.GetComponent<PlayerController>().enabled
            + " · bloqueo " + GameplayInput.Blocked + " · pantalla " + (Object.FindAnyObjectByType<DeathScreen>() != null);
    }

    private static EnemyBrain _posed;

    public static string PoseOverhead() => Pose(PenguinLocomotionAnimator.BruteSwing.Overhead);
    public static string PoseBackhand() => Pose(PenguinLocomotionAnimator.BruteSwing.Backhand);

    private static string Pose(PenguinLocomotionAnimator.BruteSwing swing)
    {
        GameObject p = GameObject.Find("Player");
        if (_posed == null)
            _posed = Object.FindObjectsByType<EnemyBrain>()
                .Where(b => !b.IsDead && b.definition != null && !b.definition.ranged)
                .OrderBy(b => Vector3.Distance(b.transform.position, p.transform.position)).FirstOrDefault();
        if (_posed == null) return "sin enemigo";
        _posed.enabled = false;
        var agent = _posed.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh) agent.ResetPath();
        _posed.GetComponentInChildren<PenguinLocomotionAnimator>().PlayBruteWindup(0.5f, swing);
        return _posed.name;
    }

    public static string Strike()
    {
        if (_posed == null) return "sin enemigo";
        _posed.GetComponentInChildren<PenguinLocomotionAnimator>().PlayBruteStrike();
        return "ok";
    }

    public static string SideShot1() => SideShot("pose_a");
    public static string SideShot2() => SideShot("pose_b");
    public static string SideShot3() => SideShot("pose_c");
    public static string SideShot4() => SideShot("pose_d");

    private static string SideShot(string name)
    {
        if (_posed == null) return "sin enemigo";
        Transform t = _posed.transform;
        Vector3 target = t.position + Vector3.up * 0.55f;
        Vector3 pos = target + (t.right * 1.6f + t.forward * 1.2f).normalized * 2.4f + Vector3.up * 0.35f;
        return FrostboundBridge.CamShot(name, pos, target, 38f);
    }
}
