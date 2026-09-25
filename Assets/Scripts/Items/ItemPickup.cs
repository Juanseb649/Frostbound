using UnityEngine;

// Objeto tirado en el mundo: al tocarlo el jugador, va a su mochila.
[RequireComponent(typeof(SphereCollider))]
public class ItemPickup : MonoBehaviour
{
    public ItemDefinition item;
    [Min(1)] public int quantity = 1;
    [Tooltip("Hijo que flota y gira (icono o modelo).")]
    public Transform visual;
    public float bobHeight = 0.12f;

    private Vector3 _visualStart;

    void Awake()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        if (visual != null) _visualStart = visual.localPosition;
    }

    void Update()
    {
        if (visual == null) return;
        visual.localPosition = _visualStart + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * bobHeight);
        Camera cam = Camera.main;
        if (cam != null) visual.rotation = Quaternion.LookRotation(visual.position - cam.transform.position, Vector3.up);
    }

    void OnTriggerEnter(Collider other)
    {
        Inventory inv = other.GetComponentInParent<Inventory>();
        if (inv == null || item == null) return;
        int left = inv.Add(item, quantity);
        if (left <= 0) Destroy(gameObject);
        else quantity = left;
    }
}
