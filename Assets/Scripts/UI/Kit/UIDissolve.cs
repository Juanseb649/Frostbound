using UnityEngine;
using UnityEngine.UI;

// Disolución helada al cerrar una ventana (guía /d2r-ui). La ventana real se cierra al instante como siempre; aquí
// se crea un "fantasma": una copia solo visual (sin botones ni scripts) en un lienzo propio por encima de todo, cuyos
// marcos y placas se deshacen a manchas con un borde de escarcha (shader Frostbound/UI Frost Frame, _Dissolve)
// mientras el contenido se desvanece. Así el control, el bloqueo de input y el reabrir enseguida no cambian.
// Usa tiempo sin escala: funciona con el juego en pausa.
public class UIDissolve : MonoBehaviour
{
    private const float DefaultSeconds = 0.42f;
    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    private static Shader _shader;
    private static Canvas _layer;

    private Material _mat;
    private CanvasGroup _group;
    private float _start, _seconds;

    public static void Ghost(RectTransform source, float seconds = DefaultSeconds)
    {
        if (source == null || !source.gameObject.activeInHierarchy) return;
        // Toda ventana que se disuelve se está cerrando: suena aquí, aunque falte el shader.
        AudioManager.PlayUI(AudioId.UI_CloseMenu, 0.7f);
        if (_shader == null) _shader = Resources.Load<Shader>("Shaders/UIFrostFrame");
        if (_shader == null) return;
        Canvas layer = Layer();

        GameObject ghost = Instantiate(source.gameObject, source.parent);
        ghost.name = source.name + " (disolución)";
        ghost.transform.SetParent(layer.transform, true);
        Strip(ghost);

        var mat = new Material(_shader) { name = "Disolución helada" };
        // Sin destello ni escarcha: aquí _Rect no describe ningún botón y la franja cruzaría el panel.
        mat.SetColor("_SheenColor", Color.clear);
        mat.SetFloat("_Frost", 0f);
        foreach (Image img in ghost.GetComponentsInChildren<Image>(true)) img.material = mat;

        var d = ghost.AddComponent<UIDissolve>();
        d._mat = mat;
        d._group = ghost.GetComponent<CanvasGroup>();
        if (d._group == null) d._group = ghost.AddComponent<CanvasGroup>();
        d._group.alpha = 1f;
        d._group.interactable = false;
        d._group.blocksRaycasts = false;
        d._seconds = seconds;
        d._start = Time.unscaledTime;
    }

    // Lienzo propio para los fantasmas: por encima de todo y sin recibir clics.
    private static Canvas Layer()
    {
        if (_layer != null) return _layer;
        var go = new GameObject("Disoluciones de UI", typeof(RectTransform));
        go.layer = 5;
        _layer = go.AddComponent<Canvas>();
        _layer.renderMode = RenderMode.ScreenSpaceOverlay;
        _layer.sortingOrder = 1100;
        return _layer;
    }

    // Deja solo lo que se dibuja: fuera botones, efectos y cualquier script de la ventana original.
    private static void Strip(GameObject ghost)
    {
        foreach (MonoBehaviour mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb is Graphic graphic) { graphic.raycastTarget = false; continue; }
            if (mb is LayoutGroup || mb is LayoutElement || mb is ContentSizeFitter || mb is Mask || mb is RectMask2D || mb is CanvasGroup) continue;
            DestroyImmediate(mb);
        }
        foreach (CanvasGroup g in ghost.GetComponentsInChildren<CanvasGroup>(true)) { g.interactable = false; g.blocksRaycasts = false; }
        ghost.SetActive(true);
    }

    void Update()
    {
        float k = Mathf.Clamp01((Time.unscaledTime - _start) / Mathf.Max(0.01f, _seconds));
        if (_mat != null) _mat.SetFloat(DissolveId, k * 1.15f);
        // El contenido (textos, iconos) aguanta un poco y luego se apaga más rápido que los marcos.
        _group.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.9f, k));
        if (k >= 1f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
