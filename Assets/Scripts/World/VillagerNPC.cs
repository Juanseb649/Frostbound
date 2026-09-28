using UnityEngine;

public enum VillagerMood { Huddle, Pace, Lookout, Work }

public class VillagerNPC : MonoBehaviour
{
    [Header("Identidad")]
    public string villagerName = "Aldeano";
    [Tooltip("Rol para sistemas futuros (diálogo, tienda, reparación).")]
    public string role = "Aldeano";
    [Tooltip("Frases que dice al hablarle (sistema de diálogo pendiente).")]
    [TextArea(2, 4)] public string[] dialogue = new string[0];

    [Header("Comportamiento")]
    public VillagerMood mood = VillagerMood.Pace;
    [Tooltip("Hacia dónde mira mientras está quieto (Huddle: la hoguera; Work: su puesto).")]
    public Transform focusPoint;
    [Tooltip("Dirección que vigila en modo Lookout (la montaña).")]
    public Vector3 lookoutDirection = Vector3.forward;
    public float wanderRadius = 3f;
    public float walkSpeed = 1.3f;
    public float turnSpeed = 6f;
    [Tooltip("Distancia a la que se gira para mirar al héroe.")]
    public float noticeRadius = 4f;

    [Header("Animación procedural")]
    public Transform model;
    [Tooltip("Qué tan asustado está: 0 tranquilo, 1 temblando.")]
    [Range(0f, 1f)] public float fear = 0.6f;

    private Vector3 _home;
    private Vector3 _target;
    private float _waitTimer;
    private float _phase;
    private Transform _player;
    private NPCInteractable _interact;
    private PenguinBodySway _sway;

    void Awake()
    {
        _home = transform.position;
        _target = _home;
        _phase = Random.value * 100f;
        _waitTimer = Random.Range(0.5f, 3f);

        if (model == null && transform.childCount > 0) model = transform.GetChild(0);
        _sway = GetComponent<PenguinBodySway>();
        if (_sway == null)
        {
            _sway = gameObject.AddComponent<PenguinBodySway>();
            _sway.model = model;
            _sway.Configure(13f, 0.07f, 7f, 0f, true, 1.3f, 0.02f);
        }
        _sway.referenceSpeed = walkSpeed;
        _sway.fear = fear;
    }

    void Start()
    {
        _interact = GetComponent<NPCInteractable>();
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) _player = pc.transform;

        PenguinRigAnimator rig = GetComponentInChildren<PenguinRigAnimator>();
        if (rig != null)
        {
            rig.enableIdleActions = mood == VillagerMood.Lookout || mood == VillagerMood.Pace;
            rig.idleActionDelay = Random.Range(2f, 8f);
            rig.idleActionInterval = new Vector2(6f, 12f);
        }
    }

    void Update()
    {
        Vector3 desiredForward = transform.forward;
        bool playerNear = false;

        if (_player != null)
        {
            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < noticeRadius * noticeRadius)
            {
                playerNear = true;
                desiredForward = toPlayer;
            }
        }

        bool talking = _interact != null && _interact.Busy;
        if (talking && _player != null)
        {
            desiredForward = _player.position - transform.position;
            playerNear = true;
        }
        else if (!playerNear)
        {
            switch (mood)
            {
                case VillagerMood.Pace:
                    desiredForward = UpdatePacing(desiredForward);
                    break;
                case VillagerMood.Lookout:
                    desiredForward = lookoutDirection + transform.right * Mathf.Sin(Time.time * 0.4f + _phase) * 0.35f;
                    break;
                case VillagerMood.Huddle:
                case VillagerMood.Work:
                    if (focusPoint != null) desiredForward = focusPoint.position - transform.position;
                    break;
            }
        }

        desiredForward.y = 0f;
        if (desiredForward.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(desiredForward.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        _sway.calm = playerNear ? 0.4f : 1f;
    }

    private Vector3 UpdatePacing(Vector3 currentForward)
    {
        Vector3 toTarget = _target - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > 0.04f)
        {
            Vector3 step = toTarget.normalized * walkSpeed * Time.deltaTime;
            if (step.sqrMagnitude > toTarget.sqrMagnitude) step = toTarget;
            transform.position += step;
            return toTarget;
        }

        _waitTimer -= Time.deltaTime;
        if (_waitTimer <= 0f)
        {
            _waitTimer = Random.Range(1.5f, 4.5f);
            _target = PickWanderPoint();
        }
        return currentForward;
    }

    private Vector3 PickWanderPoint()
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = new Vector3(_home.x + r.x, _home.y, _home.z + r.y);
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 dir = candidate - transform.position;
            dir.y = 0f;
            if (!Physics.SphereCast(origin, 0.3f, dir.normalized, out RaycastHit hit, dir.magnitude + 0.4f, ~0, QueryTriggerInteraction.Ignore)
                || hit.transform.IsChildOf(transform))
                return candidate;
        }
        return transform.position;
    }

    void OnDrawGizmosSelected()
    {
        if (mood != VillagerMood.Pace) return;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Vector3 center = Application.isPlaying ? _home : transform.position;
        Gizmos.DrawWireSphere(center, wanderRadius);
    }
}
