using UnityEngine;

// Pingüino que hereda del animador del héroe solo el movimiento (caminar, patas, aletas al paso, respiración)
// y añade el golpe tosco de los corruptos: alzan el arma, tiemblan cargando y descargan de un golpe seco.
// No usa el combo de espada del héroe ni sus acciones de espera.
public class PenguinLocomotionAnimator : PenguinRigAnimator
{
    public enum BruteSwing { Overhead, Backhand }

    [Header("Golpe de los corruptos")]
    [Tooltip("Segundos que tarda el arma en caer desde arriba.")]
    public float strikeTime = 0.16f;
    [Tooltip("Segundos para volver a la guardia después del golpe.")]
    public float recoverTime = 0.4f;
    [Range(0f, 1f)] public float overheadChance = 0.6f;

    private enum Phase { None, Windup, Strike, Recover }

    private Phase _phase;
    private float _t;
    private float _windupDuration = 0.4f;
    private float _raiseAtStrike;
    private BruteSwing _swing;

    public bool Swinging => _phase != Phase.None;

    protected override bool AllowsIdleActions => false;

    public override void PlayAction(IdleAction action) { }

    public override void SetWeaponPose(bool armed, WeaponGrip grip, Vector3 tipRest) { }

    public override void SetWeaponPose(bool armed, WeaponGrip grip) { }

    public override void PlaySwordCombo(int step, float duration) { }

    public override void PlayAttack(float duration) { }

    public void PlayBruteWindup(float duration, BruteSwing? swing = null)
    {
        _swing = swing ?? (Random.value < overheadChance ? BruteSwing.Overhead : BruteSwing.Backhand);
        _windupDuration = Mathf.Max(0.08f, duration);
        _phase = Phase.Windup;
        _t = 0f;
    }

    public void PlayBruteStrike()
    {
        if (_phase == Phase.None) PlayBruteWindup(0.08f);
        _raiseAtStrike = Raise();
        _phase = Phase.Strike;
        _t = 0f;
    }

    public BruteSwing NextSwing => _swing == BruteSwing.Overhead ? BruteSwing.Backhand : BruteSwing.Overhead;

    private float Raise()
    {
        switch (_phase)
        {
            case Phase.Windup: return Ease(Mathf.Clamp01(_t / (_windupDuration * 0.8f)));
            case Phase.Strike: return _raiseAtStrike * (1f - SmoothStep01(0f, strikeTime, _t));
            default: return 0f;
        }
    }

    private float Chop()
    {
        switch (_phase)
        {
            case Phase.Strike: return SmoothStep01(0f, strikeTime * 0.85f, _t);
            case Phase.Recover: return 1f - SmoothStep01(0.08f, recoverTime, _t);
            default: return 0f;
        }
    }

    private static float Ease(float x) => 1f - (1f - x) * (1f - x);

    protected override void ModifyPose(ref float swingL, ref float swingR, ref float outL, ref float outR,
        ref float spineYaw, ref float headPitch, ref float headRoll, ref float lean, ref float extraCrouch)
    {
        if (_phase == Phase.None) return;
        if (Application.isPlaying) _t += Time.deltaTime;

        if (_phase == Phase.Windup && _t > _windupDuration + 0.6f) PlayBruteStrike();
        if (_phase == Phase.Strike && _t >= strikeTime) { _phase = Phase.Recover; _t = 0f; }
        else if (_phase == Phase.Recover && _t >= recoverTime) { _phase = Phase.None; return; }

        float raise = Raise();
        float chop = Chop();
        float charging = _phase == Phase.Windup ? Mathf.Clamp01(_t / _windupDuration) : 0f;
        float shake = Mathf.Sin(_t * 70f) * 3.5f * charging * raise;

        if (_swing == BruteSwing.Overhead)
        {
            swingR += 168f * raise + 52f * chop + shake;
            outR += 12f * raise - 4f * chop;
            swingL += 35f * raise - 25f * chop;
            outL += 28f * raise + 10f * chop;
            spineYaw += 8f * raise - 6f * chop;
            lean += -16f * raise + 24f * chop;
            headPitch += -12f * raise + 16f * chop;
            extraCrouch += 0.06f * chop;
        }
        else
        {
            swingR += 105f * raise + 55f * chop + shake;
            outR += -50f * raise + 75f * chop;
            outL += 30f * raise - 10f * chop;
            swingL += -20f * raise + 20f * chop;
            spineYaw += 38f * raise - 42f * chop;
            headRoll += -6f * raise + 8f * chop;
            lean += -6f * raise + 14f * chop;
            extraCrouch += 0.03f * chop;
        }
    }
}
