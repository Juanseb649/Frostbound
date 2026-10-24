using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Encuentros obligatorios de "La ciudadela caída": los tres grupos de corruptos del camino (campId
// "camino_ciudadela_1..3"). Cada uno queda registrado en la partida al caer su último miembro
// ("encuentro_<campId>" etapa 1), así que la muerte del héroe, la reaparición de los campamentos o un cambio de zona
// no lo deshacen. Hasta que caigan los tres, hielo del Frost sella la puerta y los huecos de la muralla de la
// ciudadela (con colisión y cortando el NavMesh) y el asedio no puede empezar. Los demás campamentos del mundo
// siguen siendo opcionales.
public class CitadelGate : MonoBehaviour
{
    public static CitadelGate Instance { get; private set; }

    public readonly List<EnemyCamp> camps = new List<EnemyCamp>();
    private readonly List<GameObject> _ice = new List<GameObject>();
    private float _noticeAt;
    private bool _melting;
    private VillageLayout.CastleDesign _castle;
    private Material _iceMat;
    private VillageLayout.Result _layout;

    public static string Key(EnemyCamp c) => "encuentro_" + c.campId;
    public static bool Done(EnemyCamp c) => QuestLog.Stage(Key(c)) >= 1;

    public int Cleared
    {
        get
        {
            int n = 0;
            foreach (EnemyCamp c in camps) if (c != null && Done(c)) n++;
            return n;
        }
    }

    public int Required => camps.Count;
    public bool RoadCleared => Cleared >= Required;

    // ¿Se puede entrar en la ciudadela? Solo la etapa 1 de la misión depende de los encuentros.
    public static bool CitadelOpen
    {
        get
        {
            if (Instance == null) return true;
            int stage = QuestLog.Stage(QuestLog.Citadel);
            return stage >= QuestLog.CitadelBarbarian || Instance.RoadCleared;
        }
    }

    // El grupo pendiente más cercano a un punto (para el mapa).
    public Vector3? NearestPending(Vector3 from)
    {
        EnemyCamp best = null;
        float bestD = float.MaxValue;
        foreach (EnemyCamp c in camps)
        {
            if (c == null || Done(c)) continue;
            float d = (c.transform.position - from).sqrMagnitude;
            if (d < bestD) { bestD = d; best = c; }
        }
        return best != null ? best.transform.position : (Vector3?)null;
    }

    public static CitadelGate Build(Transform parent, VillageLayout.Result layout, IList<EnemyCamp> required, Material ice)
    {
        var go = new GameObject("Sello_Ciudadela");
        go.transform.SetParent(parent, false);
        CitadelGate g = go.AddComponent<CitadelGate>();
        g._castle = layout.castle;
        g._iceMat = ice;
        g.camps.AddRange(required);
        g._layout = layout;
        return g;
    }

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    // El hielo se monta después de hornear el NavMesh: lo corta con sus obstáculos y, al fundirse, el camino queda libre.
    void Start()
    {
        BuildIce(_layout);
        if (CitadelOpen) SetIce(false);
    }

    // Bloques de hielo en cada hueco de la muralla del poblado en ruinas (la puerta, el viaducto y los derrumbes).
    private void BuildIce(VillageLayout.Result layout)
    {
        var town = layout.town;
        Mesh octa = MenuRuins.Octahedron();
        var rng = new System.Random(layout.castle.detailSeed);
        for (int i = 0; i < town.wallPosts.Count; i++)
        {
            if (!town.wallGaps[i]) continue;
            Vector3 a = town.wallPosts[i];
            Vector3 b = town.wallPosts[(i + 1) % town.wallPosts.Count];
            Vector3 mid = _castle.ToWorld((a + b) * 0.5f);
            Vector3 along = _castle.ToWorld(b) - _castle.ToWorld(a);
            along.y = 0f;
            var block = new GameObject("Hielo_Sello");
            block.transform.SetParent(transform, false);
            block.transform.SetPositionAndRotation(mid, Quaternion.LookRotation(along.normalized));
            float len = along.magnitude + 0.6f;
            for (int k = 0; k < 3; k++)
            {
                var shard = new GameObject("Esquirla", typeof(MeshFilter), typeof(MeshRenderer));
                shard.transform.SetParent(block.transform, false);
                float h = 3.2f + (float)rng.NextDouble() * 1.6f;
                shard.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.6f, h * 0.45f, (k - 1) * len * 0.33f);
                shard.transform.localRotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 14f, (float)rng.NextDouble() * 90f, ((float)rng.NextDouble() - 0.5f) * 14f);
                shard.transform.localScale = new Vector3(1.3f, h, len * 0.45f);
                shard.GetComponent<MeshFilter>().sharedMesh = octa;
                shard.GetComponent<MeshRenderer>().sharedMaterial = _iceMat;
            }
            var col = block.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.8f, 0f);
            col.size = new Vector3(1.6f, 3.6f, len);
            var obstacle = block.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = col.center;
            obstacle.size = col.size;
            obstacle.carving = true;
            _ice.Add(block);
        }
    }

    private void SetIce(bool on)
    {
        foreach (GameObject g in _ice) if (g != null) g.SetActive(on);
    }

    void Update()
    {
        // Registro de cada encuentro al caer el último corrupto del grupo.
        foreach (EnemyCamp c in camps)
        {
            if (c == null || Done(c) || !c.Cleared || c.Kills == 0) continue;
            QuestLog.SetStage(Key(c), 1);
            int n = Cleared;
            if (n < Required) Notifications.Show("Grupo del camino derrotado (" + n + "/" + Required + ")", FrostboundUI.Gold);
        }
        if (_melting || _ice.Count == 0 || !_ice[0].activeSelf) return;
        if (CitadelOpen)
        {
            StartCoroutine(Melt());
            return;
        }
        // Aviso al acercarse al hielo.
        if (Time.time < _noticeAt) return;
        foreach (PlayerController pc in Players.All)
        {
            if (pc == null) continue;
            foreach (GameObject g in _ice)
            {
                Vector3 d = pc.transform.position - g.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 25f) continue;
                _noticeAt = Time.time + 5f;
                Notifications.Show("El hielo del Frost sella la ciudadela. Derrota a los corruptos del camino (" + Cleared + "/" + Required + ").", FrostboundUI.Gold);
                return;
            }
        }
    }

    private IEnumerator Melt()
    {
        _melting = true;
        BonfireBanner.Show("EL SELLO DE HIELO SE QUIEBRA", BonfireBanner.Victory);
        Notifications.Show("El camino está limpio: la Ciudadela Caída se abre.", FrostboundUI.Gold);
        foreach (GameObject g in _ice) if (g != null) { var c = g.GetComponent<Collider>(); if (c != null) c.enabled = false; }
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.4f)
        {
            foreach (GameObject g in _ice) if (g != null) g.transform.localScale = new Vector3(1f, 1f - t, 1f);
            yield return null;
        }
        SetIce(false);
        _melting = false;
    }
}
