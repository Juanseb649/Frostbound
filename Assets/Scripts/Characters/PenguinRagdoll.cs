using System.Collections.Generic;
using UnityEngine;

// Ragdoll del pingüino: al activarse, cada hueso del esqueleto (cadera, torso, cabeza, aletas y patas)
// pasa a ser un Rigidbody unido a su padre con un CharacterJoint. Se construye en tiempo de ejecución
// a partir de los huesos del rig, así sirve para cualquier pingüino (enemigos, y en el futuro NPC o héroe).
public class PenguinRagdoll : MonoBehaviour
{
    [Tooltip("Masa total repartida entre los huesos (kg).")]
    public float totalMass = 8f;
    [Tooltip("Capa de los huesos del ragdoll (2 = Ignore Raycast: no bloquea clics ni proyectiles).")]
    public int ragdollLayer = 2;

    public bool IsActive { get; private set; }
    public readonly List<Rigidbody> Bodies = new List<Rigidbody>();

    private struct Pose
    {
        public Transform t;
        public Vector3 pos;
        public Quaternion rot;
        public int layer;
    }

    private readonly List<Pose> _pose = new List<Pose>();
    private readonly List<Behaviour> _disabled = new List<Behaviour>();
    private readonly List<Collider> _disabledColliders = new List<Collider>();
    private readonly List<Component> _added = new List<Component>();

    private class Part
    {
        public string bone, parent, tip;
        public float mass, radius;
        public float twist, swing;
        public bool sphere;
    }

    // Proporciones del pingüino: cuerpo de huevo, cabeza redonda, aletas finas y patas pequeñas.
    private static readonly Part[] Parts =
    {
        new Part { bone = "Hips", tip = "Spine", mass = 0.40f, radius = 0.26f },
        new Part { bone = "Spine", parent = "Hips", tip = "Head", mass = 0.30f, radius = 0.24f, twist = 20f, swing = 25f },
        new Part { bone = "Head", parent = "Spine", tip = "Anchor_Head", mass = 0.14f, radius = 0.17f, twist = 35f, swing = 40f, sphere = true },
        new Part { bone = "Flipper_L", parent = "Spine", tip = "Anchor_Hand_L", mass = 0.05f, radius = 0.05f, twist = 30f, swing = 80f },
        new Part { bone = "Flipper_R", parent = "Spine", tip = "Anchor_Hand_R", mass = 0.05f, radius = 0.05f, twist = 30f, swing = 80f },
        new Part { bone = "Foot_L", parent = "Hips", mass = 0.03f, radius = 0.07f, twist = 10f, swing = 25f, sphere = true },
        new Part { bone = "Foot_R", parent = "Hips", mass = 0.03f, radius = 0.07f, twist = 10f, swing = 25f, sphere = true },
    };

    // Activa el ragdoll. velocity: velocidad inicial de todo el cuerpo; hit: empujón extra en el torso.
    public bool Activate(Vector3 velocity, Vector3 hit)
    {
        if (IsActive) return true;
        var bones = new Dictionary<string, Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (!bones.ContainsKey(t.name)) bones[t.name] = t;
        if (!bones.ContainsKey("Hips") || !bones.ContainsKey("Spine")) return false;

        _pose.Clear();
        _disabled.Clear();
        _disabledColliders.Clear();
        _added.Clear();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t != transform) _pose.Add(new Pose { t = t, pos = t.localPosition, rot = t.localRotation, layer = t.gameObject.layer });

        // Nada debe seguir escribiendo en los huesos ni en el modelo.
        foreach (PenguinRigAnimator a in GetComponentsInChildren<PenguinRigAnimator>()) Disable(a);
        foreach (PenguinBodySway s in GetComponentsInChildren<PenguinBodySway>()) Disable(s);
        foreach (Animator a in GetComponentsInChildren<Animator>()) Disable(a);
        foreach (Collider c in GetComponentsInChildren<Collider>())
            if (c.enabled) { c.enabled = false; _disabledColliders.Add(c); }
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;

        float scale = transform.lossyScale.y;
        var made = new Dictionary<string, Rigidbody>();
        var colliders = new List<Collider>();
        foreach (Part p in Parts)
        {
            if (!bones.TryGetValue(p.bone, out Transform bone)) continue;
            GameObject go = bone.gameObject;
            go.layer = ragdollLayer;

            Vector3 tipWorld = p.tip != null && bones.TryGetValue(p.tip, out Transform tip) ? tip.position : bone.position + transform.up * p.radius * scale;
            Collider col;
            if (p.sphere)
            {
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = p.radius * scale / Mathf.Max(0.0001f, bone.lossyScale.x);
                sc.center = bone.InverseTransformPoint(Vector3.Lerp(bone.position, tipWorld, 0.5f));
                col = sc;
            }
            else
            {
                var cc = go.AddComponent<CapsuleCollider>();
                Vector3 a = Vector3.zero, b = bone.InverseTransformPoint(tipWorld);
                Vector3 axis = b - a;
                cc.direction = Dominant(axis);
                float len = Mathf.Abs(axis[cc.direction]);
                float r = p.radius * scale / Mathf.Max(0.0001f, bone.lossyScale.x);
                cc.radius = r;
                cc.height = Mathf.Max(len + r * (p.bone == "Hips" || p.bone == "Spine" ? 1f : 2f), r * 2f);
                cc.center = (a + b) * 0.5f;
                col = cc;
            }
            colliders.Add(col);
            _added.Add(col);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = totalMass * p.mass;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 0.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.solverIterations = 12;
            made[p.bone] = rb;
            Bodies.Add(rb);

            if (p.parent != null && made.TryGetValue(p.parent, out Rigidbody parentRb))
            {
                var j = go.AddComponent<CharacterJoint>();
                _added.Add(j);
                j.connectedBody = parentRb;
                j.axis = Vector3.right;
                j.swingAxis = Vector3.forward;
                j.lowTwistLimit = new SoftJointLimit { limit = -p.twist };
                j.highTwistLimit = new SoftJointLimit { limit = p.twist };
                j.swing1Limit = new SoftJointLimit { limit = p.swing };
                j.swing2Limit = new SoftJointLimit { limit = p.swing };
                j.enableProjection = true;
                j.enablePreprocessing = false;
            }
        }

        // Las piezas del mismo pingüino no chocan entre sí, ni con el héroe (no se tropieza con los cuerpos).
        for (int i = 0; i < colliders.Count; i++)
            for (int k = i + 1; k < colliders.Count; k++) Physics.IgnoreCollision(colliders[i], colliders[k]);
        GameObject player = GameObject.Find("Player");
        if (player != null)
            foreach (Collider pc in player.GetComponentsInChildren<Collider>())
                foreach (Collider c in colliders) Physics.IgnoreCollision(c, pc);

        foreach (Rigidbody rb in Bodies) rb.linearVelocity = velocity;
        if (made.TryGetValue("Spine", out Rigidbody spine)) spine.AddForce(hit, ForceMode.VelocityChange);
        if (made.TryGetValue("Head", out Rigidbody head)) head.AddForce(hit * 0.6f, ForceMode.VelocityChange);
        IsActive = true;
        return true;
    }

    private void Disable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        _disabled.Add(b);
    }

    // Deshace el ragdoll: quita la física, devuelve cada hueso a su pose y reactiva los animadores.
    public void Deactivate()
    {
        if (!IsActive) return;
        for (int i = _added.Count - 1; i >= 0; i--)
            if (_added[i] is Joint && _added[i] != null) DestroyImmediate(_added[i]);
        foreach (Rigidbody rb in Bodies)
            if (rb != null) DestroyImmediate(rb);
        foreach (Component c in _added)
            if (c != null) DestroyImmediate(c);
        Bodies.Clear();
        _added.Clear();
        foreach (Pose p in _pose)
        {
            if (p.t == null) continue;
            p.t.localPosition = p.pos;
            p.t.localRotation = p.rot;
            p.t.gameObject.layer = p.layer;
        }
        foreach (Collider c in _disabledColliders) if (c != null) c.enabled = true;
        foreach (Behaviour b in _disabled) if (b != null) b.enabled = true;
        _disabledColliders.Clear();
        _disabled.Clear();
        IsActive = false;
    }

    // Congela el cuerpo donde quedó (para hundirlo en la nieve sin que la física lo mueva).
    public void Freeze()
    {
        foreach (Rigidbody rb in Bodies)
        {
            if (rb == null) continue;
            rb.isKinematic = true;
            Collider c = rb.GetComponent<Collider>();
            if (c != null) c.enabled = false;
        }
    }

    public bool Sleeping
    {
        get
        {
            foreach (Rigidbody rb in Bodies) if (rb != null && rb.linearVelocity.sqrMagnitude > 0.04f) return false;
            return true;
        }
    }

    private static int Dominant(Vector3 v)
    {
        v = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        return v.x > v.y && v.x > v.z ? 0 : v.y > v.z ? 1 : 2;
    }
}
