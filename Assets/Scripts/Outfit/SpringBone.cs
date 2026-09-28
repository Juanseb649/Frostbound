using UnityEngine;

// Movimiento secundario de una pieza rígida de la armadura (casco, hombreras, colgante...): un hueso extra, hijo del hueso
// que la sostiene, que lo sigue con un resorte. La pieza acompaña el movimiento con un pequeño retraso y rebote en lugar
// de ir pegada como una piedra. Con "influence" la pieza sigue en parte a otro hueso (la hombrera acompaña a la aleta).
[DefaultExecutionOrder(260)]
public class SpringBone : MonoBehaviour
{
    [Tooltip("Rigidez del resorte (más alto = sigue más pegado).")]
    public float stiffness = 180f;
    [Tooltip("Amortiguación (más alto = rebota menos).")]
    public float damping = 16f;
    [Tooltip("Otro hueso que la pieza sigue en parte (opcional).")]
    public Transform influence;
    [Range(0f, 1f)] public float influenceWeight;
    [Tooltip("Giro máximo respecto al hueso padre (grados).")]
    public float maxAngle = 12f;
    [Tooltip("Raíz del personaje: el resorte trabaja en su espacio, así girar o desplazarse no deja la pieza atrás.")]
    public Transform root;

    public float Lag { get; private set; }

    private Quaternion _rot;
    private Vector3 _angVel;
    private Quaternion _influenceOffset = Quaternion.identity;
    private bool _ready;

    public void Setup(Transform influenceBone, float weight, Quaternion? restOffset = null)
    {
        influence = influenceBone;
        influenceWeight = weight;
        // Diferencia de reposo entre el hueso de influencia y el padre.
        if (restOffset.HasValue) _influenceOffset = restOffset.Value;
        else if (influence != null && transform.parent != null)
            _influenceOffset = Quaternion.Inverse(influence.rotation) * transform.parent.rotation;
    }

    private Quaternion Target()
    {
        Quaternion target = transform.parent != null ? transform.parent.rotation : transform.rotation;
        if (influence != null && influenceWeight > 0f)
            target = Quaternion.Slerp(target, influence.rotation * _influenceOffset, influenceWeight);
        return target;
    }

    void OnEnable() => _ready = false;

    void LateUpdate()
    {
        // Todo en el espacio del personaje: solo los movimientos propios del cuerpo (aletas, cabeza, tajos)
        // producen el pequeño retraso; caminar, girar o el giro de 360° mueven la pieza sin demora.
        Quaternion rootRot = root != null ? root.rotation : Quaternion.identity;
        Quaternion toLocal = Quaternion.Inverse(rootRot);
        Quaternion target = toLocal * Target();
        if (!_ready)
        {
            _rot = target;
            _angVel = Vector3.zero;
            _ready = true;
        }
        // Pasos pequeños y fijos: con pocos FPS el resorte no se vuelve inestable (eso se veía como tirones y retraso).
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        if (dt <= 0f) return;
        int steps = Mathf.CeilToInt(dt / (1f / 240f));
        float h = dt / steps;
        for (int s = 0; s < steps; s++)
        {
            Quaternion delta = target * Quaternion.Inverse(_rot);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-6f) axis = Vector3.up;
            Vector3 error = axis.normalized * (angle * Mathf.Deg2Rad);
            _angVel += (error * stiffness - _angVel * damping) * h;
            float speed = _angVel.magnitude;
            if (speed > 1e-5f) _rot = Quaternion.AngleAxis(speed * h * Mathf.Rad2Deg, _angVel / speed) * _rot;
        }

        // Nunca se separa demasiado de su hueso.
        Lag = Quaternion.Angle(target, _rot);
        if (Lag > maxAngle)
        {
            _rot = Quaternion.RotateTowards(target, _rot, maxAngle);
            _angVel *= 0.5f;
            Lag = maxAngle;
        }
        transform.rotation = rootRot * _rot;
    }
}
