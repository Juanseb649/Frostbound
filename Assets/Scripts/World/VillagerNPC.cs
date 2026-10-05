using System.Collections.Generic;
using UnityEngine;

// Chat: pasea por la plaza y, de vez en cuando, se junta con otro aldeano a charlar (globos de fondo).
public enum VillagerMood { Huddle, Pace, Lookout, Work, Chat }

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

    // ----- Charla entre aldeanos -----
    private static readonly List<VillagerNPC> Chatters = new List<VillagerNPC>();
    private VillagerNPC _partner;
    private bool _leader;
    private Vector3 _meetPoint;
    private string[] _script;
    private int _line;
    private float _nextLineAt, _chatCooldown, _nextTryAt;
    private const float ChatPairRadius = 14f, LineSeconds = 2.9f;

    public bool Chatting => _partner != null;
    public VillagerNPC Partner => _partner;
    public int ChatLine => _line;

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

    void OnEnable() { if (mood == VillagerMood.Chat) Chatters.Add(this); }
    void OnDisable()
    {
        Chatters.Remove(this);
        EndChat();
    }

    void Start()
    {
        _chatCooldown = Time.time + Random.Range(1f, 7f);
        _interact = GetComponent<NPCInteractable>();
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) _player = pc.transform;

        PenguinRigAnimator rig = GetComponentInChildren<PenguinRigAnimator>();
        if (rig != null)
        {
            rig.enableIdleActions = mood == VillagerMood.Lookout || mood == VillagerMood.Pace || mood == VillagerMood.Chat;
            rig.idleActionDelay = Random.Range(2f, 8f);
            rig.idleActionInterval = new Vector2(6f, 12f);
            // Mismo ciclo de pasos que el héroe, escalado a su velocidad de paseo.
            if (mood == VillagerMood.Pace || mood == VillagerMood.Chat) rig.referenceSpeed = walkSpeed;
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
        if (talking && Chatting) EndChat();
        if (talking && _player != null)
        {
            desiredForward = _player.position - transform.position;
            playerNear = true;
        }
        else if (!playerNear || (mood == VillagerMood.Chat && Chatting))
        {
            switch (mood)
            {
                case VillagerMood.Pace:
                    desiredForward = UpdatePacing(desiredForward);
                    break;
                case VillagerMood.Chat:
                    desiredForward = UpdateChat(desiredForward);
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

    // ---------- Charla ----------

    private Vector3 UpdateChat(Vector3 currentForward)
    {
        if (!Chatting)
        {
            if (Time.time > _chatCooldown && Time.time > _nextTryAt) TryStartChat();
            if (!Chatting) return UpdatePacing(currentForward);
        }

        Vector3 slot = _meetPoint + (_leader ? 1f : -1f) * MeetOffset();
        if (WalkTo(slot, out Vector3 dir)) return dir;

        if (_leader) TickConversation();
        if (!Chatting) return currentForward;
        Vector3 toPartner = _partner.transform.position - transform.position;
        // Pequeño balanceo mientras habla: mira un poco a los lados.
        return toPartner + transform.right * Mathf.Sin(Time.time * 0.7f + _phase) * 0.15f;
    }

    // Separación entre los dos aldeanos a lo largo de la línea que los une al empezar.
    private Vector3 MeetOffset()
    {
        Vector3 a = _leader ? transform.position : _partner.transform.position;
        Vector3 b = _leader ? _partner.transform.position : transform.position;
        Vector3 d = a - b;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) d = Vector3.right;
        return d.normalized * 0.8f;
    }

    private bool WalkTo(Vector3 target, out Vector3 dir)
    {
        dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.04f) return false;
        Vector3 step = dir.normalized * walkSpeed * Time.deltaTime;
        if (step.sqrMagnitude > dir.sqrMagnitude) step = dir;
        transform.position += step;
        return true;
    }

    private void TryStartChat()
    {
        _nextTryAt = Time.time + Random.Range(1.5f, 3f);
        if (_interact != null && _interact.Busy) return;
        VillagerNPC best = null;
        float bestD = ChatPairRadius * ChatPairRadius;
        foreach (VillagerNPC other in Chatters)
        {
            if (other == this || other.Chatting || other._chatCooldown > Time.time) continue;
            if (other._interact != null && other._interact.Busy) continue;
            float d = (other.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = other; }
        }
        if (best == null) return;

        _partner = best;
        best._partner = this;
        _leader = true;
        best._leader = false;
        _meetPoint = best._meetPoint = (transform.position + best.transform.position) * 0.5f;
        _script = VillageChatter.Pick();
        _line = 0;
        _nextLineAt = Time.time + 0.6f;
    }

    private void TickConversation()
    {
        if (!Chatting || Time.time < _nextLineAt) return;
        // Espera a que los dos hayan llegado antes de empezar.
        if (_line == 0 && (Vector3.Distance(transform.position, _partner.transform.position) > 2.4f)) return;
        if (_script == null || _line >= _script.Length)
        {
            EndChat();
            return;
        }
        VillagerNPC speaker = _line % 2 == 0 ? this : _partner;
        float height = 1.35f * speaker.transform.lossyScale.y;
        if (speaker._interact != null) height = speaker._interact.headHeight * speaker.transform.lossyScale.y;
        if (WorldHUD.Instance != null) WorldHUD.Instance.SayAmbient(speaker.transform, height, _script[_line], LineSeconds - 0.25f);
        _line++;
        _nextLineAt = Time.time + LineSeconds;
    }

    private void EndChat()
    {
        VillagerNPC p = _partner;
        _partner = null;
        _script = null;
        _chatCooldown = Time.time + Random.Range(8f, 18f);
        _target = transform.position;
        _waitTimer = Random.Range(0.5f, 2f);
        if (p != null && p._partner == this) p.EndChat();
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
        if (mood != VillagerMood.Pace && mood != VillagerMood.Chat) return;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Vector3 center = Application.isPlaying ? _home : transform.position;
        Gizmos.DrawWireSphere(center, wanderRadius);
    }
}
