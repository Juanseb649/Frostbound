using UnityEngine;

// Animación procedural sobre el esqueleto: aletas, patas y cabeza.
// Mide la velocidad por el desplazamiento del propio objeto, así sirve para el jugador y los NPC.
public class PenguinRigAnimator : MonoBehaviour
{
    [Tooltip("Velocidad (m/s) que se considera caminar al 100%.")]
    public float referenceSpeed = 4f;
    public float stepFrequency = 9f;

    [Header("Caminar")]
    public float flipperFlap = 28f;
    public float footLift = 22f;
    public float headBob = 4f;

    [Header("Quieto")]
    public float idleFlipperSway = 5f;
    public float idleFrequency = 1.4f;

    private Bone _flipperL, _flipperR, _footL, _footR, _head;
    private Transform _root;
    private Transform _mover;
    private Vector3 _lastPos;
    private float _speed01;
    private float _phase;

    private class Bone
    {
        public Transform t;
        public Quaternion rest;
        public Vector3 raiseAxis;
        public Vector3 pitchAxis;
        public float side;
    }

    void Awake()
    {
        _root = transform;
        _mover = transform.parent != null ? transform.parent : transform;
        _flipperL = Find("Flipper_L");
        _flipperR = Find("Flipper_R");
        _footL = Find("Foot_L");
        _footR = Find("Foot_R");
        _head = Find("Head");
        _lastPos = _mover.position;
        _phase = Random.value * 10f;
    }

    private Bone Find(string boneName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name != boneName) continue;
            Quaternion inv = Quaternion.Inverse(t.rotation);
            Vector3 offset = t.position - _root.position;
            return new Bone
            {
                t = t,
                rest = t.localRotation,
                raiseAxis = inv * _root.forward,
                pitchAxis = inv * _root.right,
                side = Mathf.Sign(Vector3.Dot(offset, _root.right)) == 0 ? 1f : Mathf.Sign(Vector3.Dot(offset, _root.right))
            };
        }
        return null;
    }

    void LateUpdate()
    {
        Vector3 delta = _mover.position - _lastPos;
        _lastPos = _mover.position;
        delta.y = 0f;
        float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        _speed01 = Mathf.MoveTowards(_speed01, Mathf.Clamp01(speed / Mathf.Max(0.01f, referenceSpeed)), Time.deltaTime * 6f);

        float walk = _speed01;
        float idle = 1f - walk;
        float t = Time.time + _phase;
        float step = Mathf.Sin(t * stepFrequency);
        float sway = Mathf.Sin(t * idleFrequency);

        Pose(_flipperL, flipperFlap * walk * Mathf.Max(0f, step) + idleFlipperSway * idle * sway + 6f * walk, 0f);
        Pose(_flipperR, flipperFlap * walk * Mathf.Max(0f, -step) + idleFlipperSway * idle * -sway + 6f * walk, 0f);
        Pose(_footL, 0f, -footLift * walk * Mathf.Max(0f, step));
        Pose(_footR, 0f, -footLift * walk * Mathf.Max(0f, -step));
        if (_head != null)
        {
            Quaternion nod = Quaternion.AngleAxis(Mathf.Sin(t * stepFrequency * 2f) * headBob * walk, _head.pitchAxis);
            Quaternion tilt = Quaternion.AngleAxis(sway * 2f * idle, _head.raiseAxis);
            _head.t.localRotation = _head.rest * nod * tilt;
        }
    }

    private void Pose(Bone b, float raise, float pitch)
    {
        if (b == null) return;
        b.t.localRotation = b.rest
            * Quaternion.AngleAxis(raise * b.side, b.raiseAxis)
            * Quaternion.AngleAxis(pitch, b.pitchAxis);
    }
}
