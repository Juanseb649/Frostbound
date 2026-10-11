using UnityEngine;

// Cómo lleva el héroe su arma entre golpe y golpe. Cada estilo decide si el arma está lista en la mano,
// qué pasa al pedir un ataque y qué hace cuando deja de atacar (polimorfismo: PlayerCombat solo habla con WeaponCarry).
public abstract class WeaponCarry
{
    protected readonly WeaponHolder holder;
    protected readonly PenguinRigAnimator rig;

    protected WeaponCarry(WeaponHolder holder, PenguinRigAnimator rig)
    {
        this.holder = holder;
        this.rig = rig;
    }

    // El arma está en la mano y se puede golpear ya.
    public virtual bool Ready => true;

    // Se pidió un ataque con el arma guardada.
    public virtual void RequestDraw() { }

    // Cada fotograma: "attacking" = hay un golpe en curso o pendiente.
    public virtual void Tick(bool attacking, float dt) { }

    // El estilo se cambia (otra arma u otra clase): deja el arma en la mano.
    public virtual void Release() { }

    // Espadas que el shinobi guarda en la vaina de la espalda (las hachas, mazas o lanzas no).
    public static bool IsSword(ItemDefinition item)
    {
        if (item == null || !item.IsWeapon) return false;
        switch (item.weaponType)
        {
            case WeaponType.Sword:
            case WeaponType.Katana:
            case WeaponType.Scimitar:
            case WeaponType.CelticSword:
            case WeaponType.Machete:
                return true;
            default:
                return false;
        }
    }

    public static WeaponCarry For(CharacterClass cls, ItemDefinition weapon, WeaponHolder holder, PenguinRigAnimator rig)
    {
        if (holder != null && rig != null && holder.MainModel != null && cls != null && cls.className == "Ninja" && IsSword(weapon))
        {
            BackSheathCarry sheath = BackSheathCarry.TryCreate(holder, rig);
            if (sheath != null) return sheath;
        }
        return new HandCarry(holder, rig);
    }
}

// Lo normal: el arma siempre en la mano.
public class HandCarry : WeaponCarry
{
    public HandCarry(WeaponHolder holder, PenguinRigAnimator rig) : base(holder, rig) { }
}

// Shinobi: la espada vive en la vaina de la espalda. Al atacar la desenfunda en un parpadeo, encadena los tajos
// y al dejar de atacar la vuelve a envainar.
public class BackSheathCarry : WeaponCarry
{
    private enum State { Sheathed, Drawing, Drawn, Sheathing }

    public const float DrawTime = 0.2f;
    public const float SheathTime = 0.34f;
    public const float IdleBeforeSheath = 1.1f;

    private readonly Transform _anchor, _hilt;
    private readonly GameObject _model;
    private readonly Vector3 _handPos, _handScale;
    private readonly Quaternion _handRot;
    private readonly Transform _hand;
    private State _state = State.Drawn;
    private float _t, _idle;
    private bool _swapped;

    public override bool Ready => _state == State.Drawn;
    public bool Sheathed => _state == State.Sheathed;

    private BackSheathCarry(WeaponHolder holder, PenguinRigAnimator rig, Transform anchor, Transform hilt) : base(holder, rig)
    {
        _anchor = anchor;
        _hilt = hilt;
        _model = holder.MainModel;
        _hand = _model.transform.parent;
        _handPos = _model.transform.localPosition;
        _handRot = _model.transform.localRotation;
        _handScale = _model.transform.lossyScale;
    }

    // Necesita la vaina del traje (parte "Saya") y los datos de dónde está.
    public static BackSheathCarry TryCreate(WeaponHolder holder, PenguinRigAnimator rig)
    {
        SheathInfo info = SheathInfo.Ninja;
        if (info == null) return null;
        SkinnedMeshRenderer saya = null;
        foreach (SkinnedMeshRenderer r in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.name.EndsWith("_" + info.partName)) { saya = r; break; }
        if (saya == null || saya.sharedMesh == null) return null;

        // Hueso que lleva la vaina y su pose de enlace: pasa la boca y la punta (pose de reposo) al espacio del hueso.
        Matrix4x4[] bind = saya.sharedMesh.bindposes;
        BoneWeight w0 = saya.sharedMesh.boneWeights.Length > 0 ? saya.sharedMesh.boneWeights[0] : new BoneWeight();
        int bi = w0.boneIndex0;
        if (bi < 0 || bi >= saya.bones.Length || bi >= bind.Length || saya.bones[bi] == null) return null;
        Transform bone = saya.bones[bi];
        Vector3 mouth = bind[bi].MultiplyPoint3x4(info.mouth);
        Vector3 tip = bind[bi].MultiplyPoint3x4(info.tip);

        var anchor = new GameObject("Vaina_Ancla").transform;
        anchor.SetParent(bone, false);
        anchor.localPosition = mouth;
        anchor.localRotation = Quaternion.FromToRotation(Vector3.up, (tip - mouth).normalized);
        var hilt = new GameObject("Vaina_Empunadura").transform;
        hilt.SetParent(anchor, false);
        hilt.localPosition = new Vector3(0f, -0.1f, 0f);

        var carry = new BackSheathCarry(holder, rig, anchor, hilt);
        carry.SnapSheathed();
        return carry;
    }

    // Guarda la espada sin animación (al equiparla o en el menú).
    public void SnapSheathed()
    {
        ToSheath();
        _state = State.Sheathed;
        rig.SetWeaponPose(false, holder.CurrentGrip);
    }

    public override void RequestDraw()
    {
        if (_state == State.Drawn || _state == State.Drawing) return;
        if (_state == State.Sheathing && !_swapped)
        {
            _state = State.Drawn;
            rig.SetWeaponPose(true, holder.CurrentGrip);
            return;
        }
        _state = State.Drawing;
        _t = 0f;
        _swapped = false;
        rig.PlaySheath(true, DrawTime, _hilt);
    }

    public override void Tick(bool attacking, float dt)
    {
        if (_model == null) return;
        switch (_state)
        {
            case State.Drawing:
                _t += dt / DrawTime;
                if (!_swapped && _t >= 0.45f)
                {
                    ToHand();
                    _swapped = true;
                    rig.SetWeaponPose(true, holder.CurrentGrip);
                }
                if (_t >= 1f)
                {
                    _state = State.Drawn;
                    _idle = 0f;
                }
                break;
            case State.Drawn:
                _idle = attacking ? 0f : _idle + dt;
                if (_idle >= IdleBeforeSheath)
                {
                    _state = State.Sheathing;
                    _t = 0f;
                    _swapped = false;
                    rig.PlaySheath(false, SheathTime, _hilt);
                }
                break;
            case State.Sheathing:
                _t += dt / SheathTime;
                if (!_swapped && _t >= 0.55f)
                {
                    ToSheath();
                    _swapped = true;
                    rig.SetWeaponPose(false, holder.CurrentGrip);
                }
                if (_t >= 1f) _state = State.Sheathed;
                break;
        }
    }

    public override void Release()
    {
        if (_model != null)
        {
            ToHand();
            rig.SetWeaponPose(true, holder.CurrentGrip);
        }
        if (_anchor != null)
        {
            if (Application.isPlaying) Object.Destroy(_anchor.gameObject);
            else Object.DestroyImmediate(_anchor.gameObject);
        }
    }

    private void ToHand()
    {
        if (_hand == null) return;
        Transform m = _model.transform;
        m.SetParent(_hand, false);
        m.localPosition = _handPos;
        m.localRotation = _handRot;
        SetWorldScale(m);
        holder.SetTrail(false);
    }

    private void ToSheath()
    {
        Transform m = _model.transform;
        m.SetParent(_anchor, false);
        m.localPosition = Vector3.zero;
        m.localRotation = Quaternion.identity;
        SetWorldScale(m);
        holder.SetTrail(false);
    }

    private void SetWorldScale(Transform m)
    {
        Vector3 p = m.parent != null ? m.parent.lossyScale : Vector3.one;
        m.localScale = new Vector3(_handScale.x / Mathf.Max(1e-4f, p.x), _handScale.y / Mathf.Max(1e-4f, p.y), _handScale.z / Mathf.Max(1e-4f, p.z));
    }
}
