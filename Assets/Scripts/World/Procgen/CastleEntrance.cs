using System.Collections;
using UnityEngine;

// Puerta del castillo. Sellada con hielo negro hasta que cae el bárbaro; después lleva al interior.
public class CastleEntrance : MonoBehaviour
{
    public Material sealMaterial;
    private Transform _seal;
    private Transform _player;
    private float _warnAt;
    private bool _dissolving;
    private BoxCollider _block;

    public bool Sealed => QuestLog.Stage(QuestLog.Citadel) < QuestLog.CitadelBarbarian;

    public void Setup(VillageLayout.CastleDesign castle, Material seal)
    {
        sealMaterial = seal;
        transform.SetPositionAndRotation(castle.Door, Quaternion.Euler(0f, castle.yaw, 0f));
        _seal = new GameObject("Sello_Hielo").transform;
        _seal.SetParent(transform, false);
        var rng = new DeterministicRng(castle.detailSeed + 5);
        for (int i = 0; i < 9; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(shard.GetComponent<Collider>());
            shard.transform.SetParent(_seal, false);
            float x = Mathf.Lerp(-1.1f, 1.1f, i / 8f);
            float h = rng.Range(3.5f, 6.2f);
            shard.transform.localPosition = new Vector3(x, h * 0.45f, -0.1f + rng.Range(-0.15f, 0.15f));
            shard.transform.localRotation = Quaternion.Euler(rng.Range(-8f, 8f), rng.Range(0f, 40f), rng.Range(-12f, 12f));
            shard.transform.localScale = new Vector3(rng.Range(0.35f, 0.6f), h, rng.Range(0.3f, 0.5f));
            shard.GetComponent<MeshRenderer>().sharedMaterial = sealMaterial;
        }
        var lightGo = new GameObject("Luz_Sello");
        lightGo.transform.SetParent(_seal, false);
        lightGo.transform.localPosition = new Vector3(0f, 2.6f, 1.2f);
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.3f, 0.55f, 1f);
        l.range = 7f;
        l.intensity = 2.4f;
        _block = gameObject.AddComponent<BoxCollider>();
        var block = _block;
        block.center = new Vector3(0f, 2.5f, -0.1f);
        block.size = new Vector3(2.8f, 5f, 0.5f);
        _seal.gameObject.SetActive(Sealed);
        _block.enabled = Sealed;
        QuestLog.Changed += OnQuest;
    }

    void OnDestroy() => QuestLog.Changed -= OnQuest;

    private void OnQuest(string id, int stage)
    {
        if (id == QuestLog.Citadel && stage >= QuestLog.CitadelBarbarian && _seal != null && _seal.gameObject.activeSelf && !_dissolving)
        {
            _block.enabled = false;
            StartCoroutine(Dissolve());
        }
    }

    private IEnumerator Dissolve()
    {
        _dissolving = true;
        Vector3 s0 = _seal.localScale;
        for (float t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            float k = t / 2.2f;
            _seal.localScale = new Vector3(s0.x * (1f - k * 0.3f), s0.y * (1f - k), s0.z);
            yield return null;
        }
        _seal.gameObject.SetActive(false);
        _seal.localScale = s0;
        _dissolving = false;
    }

    void Update()
    {
        if (_player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            _player = p.transform;
        }
        Vector3 local = transform.InverseTransformPoint(_player.position);
        bool atDoor = Mathf.Abs(local.x) < 1.8f && local.z > -0.2f && local.z < 1.6f && Mathf.Abs(local.y) < 2f;
        if (!atDoor) return;
        if (Sealed)
        {
            if (Time.time < _warnAt) return;
            _warnAt = Time.time + 4f;
            Notifications.Show(QuestLog.Stage(QuestLog.Citadel) == QuestLog.CitadelNone
                ? "Un sello de hielo negro bloquea la puerta. Quizá alguien del poblado sepa algo."
                : "Un sello de hielo negro bloquea la puerta. Su guardián sigue en pie en la ciudadela.", FrostboundUI.Muted);
            return;
        }
        CastleInterior interior = CastleInterior.Instance;
        if (interior != null)
        {
            interior.ExitPoint = transform.position + transform.forward * 3.5f;
            interior.ExitRotation = transform.rotation;
            interior.Enter();
        }
    }
}
