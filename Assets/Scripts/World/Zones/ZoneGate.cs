using System;
using UnityEngine;

// Paso a otra zona (sendero de la montaña, bajada al poblado...). Construye su arco de piedra con banderines,
// y al cruzarlo lleva al héroe a la escena de destino con la pantalla de carga.
// Si "canPass" dice que no, el héroe es empujado hacia atrás y se muestra el aviso.
public class ZoneGate : MonoBehaviour
{
    public string targetScene = "";
    public string arrival = "";
    public string label = "";
    public Func<bool> canPass;
    public string blockedMessage = "";
    public Action onPass;

    private Transform _player;
    private float _noticeAt;
    private bool _used;
    private float _readyAt;

    public float Width { get; private set; } = 6f;

    public static ZoneGate Build(Transform parent, string name, Vector3 position, float yaw, float width, Material stone, Material cloth)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        ZoneGate g = go.AddComponent<ZoneGate>();
        g.Width = width;
        float h = 5.2f;
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pilar";
            pillar.transform.SetParent(go.transform, false);
            pillar.transform.localPosition = new Vector3(side * (width * 0.5f + 0.6f), h * 0.5f - 0.3f, 0f);
            pillar.transform.localScale = new Vector3(1.1f, h, 1.1f);
            pillar.transform.localRotation = Quaternion.Euler(0f, side * 4f, side * 2.5f);
            pillar.GetComponent<Renderer>().sharedMaterial = stone;
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = "Remate";
            cap.transform.SetParent(go.transform, false);
            cap.transform.localPosition = new Vector3(side * (width * 0.5f + 0.6f), h - 0.1f, 0f);
            cap.transform.localScale = new Vector3(1.5f, 0.45f, 1.5f);
            cap.GetComponent<Renderer>().sharedMaterial = stone;
            UnityEngine.Object.Destroy(cap.GetComponent<Collider>());
        }
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Dintel";
        beam.transform.SetParent(go.transform, false);
        beam.transform.localPosition = new Vector3(0f, h + 0.35f, 0f);
        beam.transform.localScale = new Vector3(width + 2.8f, 0.6f, 0.9f);
        beam.transform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
        beam.GetComponent<Renderer>().sharedMaterial = stone;
        UnityEngine.Object.Destroy(beam.GetComponent<Collider>());
        int flags = Mathf.Max(3, Mathf.RoundToInt(width / 1.1f));
        for (int i = 0; i < flags; i++)
        {
            GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flag.name = "Banderin";
            flag.transform.SetParent(go.transform, false);
            float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, (i + 0.5f) / flags);
            float sag = Mathf.Sin((i + 0.5f) / flags * Mathf.PI) * 0.45f;
            flag.transform.localPosition = new Vector3(x, h - 0.2f - sag, 0.05f);
            flag.transform.localScale = new Vector3(0.6f, 0.75f, 1f);
            flag.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            flag.GetComponent<Renderer>().sharedMaterial = cloth;
            UnityEngine.Object.Destroy(flag.GetComponent<Collider>());
        }
        BoxCollider trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.5f, 0f);
        trigger.size = new Vector3(width, 3f, 1.6f);
        return g;
    }

    void Start() => _readyAt = Time.time + 1.5f;

    void OnTriggerEnter(Collider other)
    {
        if (_used || Time.time < _readyAt) return;
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        _player = pc.transform;
        if (canPass != null && !canPass())
        {
            if (Time.time > _noticeAt)
            {
                _noticeAt = Time.time + 3f;
                Notifications.Show(blockedMessage, FrostboundUI.Gold);
            }
            PushBack(pc);
            return;
        }
        if (string.IsNullOrEmpty(targetScene) || !Application.CanStreamedLevelBeLoaded(targetScene))
        {
            if (Time.time > _noticeAt)
            {
                _noticeAt = Time.time + 3f;
                Notifications.Show(string.IsNullOrEmpty(blockedMessage) ? label + ": aún no se puede pasar" : blockedMessage, FrostboundUI.Gold);
            }
            PushBack(pc);
            return;
        }
        _used = true;
        onPass?.Invoke();
        ZoneTravel.Go(targetScene, arrival);
    }

    // Devuelve al héroe un par de pasos por donde vino.
    private void PushBack(PlayerController pc)
    {
        Vector3 local = transform.InverseTransformPoint(pc.transform.position);
        Vector3 back = transform.TransformDirection(new Vector3(0f, 0f, local.z >= 0f ? 1f : -1f));
        PlayerPersistence persist = pc.GetComponent<PlayerPersistence>();
        Vector3 target = pc.transform.position + back * 2.2f;
        if (UnityEngine.AI.NavMesh.SamplePosition(target, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) target = hit.position;
        if (persist != null) persist.MoveTo(target, Quaternion.LookRotation(back));
        else pc.transform.position = target;
        pc.MoveTo(target);
    }
}
