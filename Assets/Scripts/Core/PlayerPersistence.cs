using UnityEngine;

// Une al héroe con la partida guardada: aparece junto a su última hoguera, se guarda cada cierto tiempo
// y al salir del juego.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Equipment))]
public class PlayerPersistence : MonoBehaviour
{
    [Tooltip("Segundos entre autoguardados.")]
    public float autosaveSeconds = 60f;

    private Equipment _eq;
    private CharacterStats _stats;
    private float _nextSave;

    void Awake()
    {
        _eq = GetComponent<Equipment>();
        _stats = GetComponent<CharacterStats>();
        if (GameSession.Instance != null) GameSession.Instance.RegisterPlayer(_eq);
    }

    void Start()
    {
        GameSession s = GameSession.Instance;
        Bonfire b = Bonfire.Find(s != null ? s.LastBonfire : Bonfire.CastleId);
        if (b == null) b = Bonfire.Find(Bonfire.CastleId);
        if (b != null) MoveTo(b.SpawnPoint, b.SpawnRotation);
        _nextSave = Time.time + autosaveSeconds;
    }

    void Update()
    {
        if (Time.time < _nextSave) return;
        _nextSave = Time.time + autosaveSeconds;
        if (_stats != null && _stats.IsDead) return;
        if (GameSession.Instance != null) GameSession.Instance.SaveNow();
    }

    void OnDestroy()
    {
        if (GameSession.Instance != null) GameSession.Instance.UnregisterPlayer(_eq);
    }

    public void MoveTo(Vector3 position, Quaternion rotation)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(position, rotation);
        Camera cam = Camera.main;
        CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            cam.transform.position = position + follow.offset;
            cam.transform.LookAt(position + Vector3.up * follow.lookHeight);
        }
    }
}
