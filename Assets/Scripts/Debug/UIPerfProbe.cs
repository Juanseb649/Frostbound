using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

// Mide el coste de la UI durante unos fotogramas con los contadores del Profiler (sin abrirlo): asignaciones de
// memoria (GC.Alloc), reconstrucciones de lotes de Canvas y tiempo de preparar los lienzos. Solo funciona en el
// editor y en builds de desarrollo, donde existen esos contadores. Se lanza con UIPerfProbe.Begin(fotogramas) y el
// resultado queda en UIPerfProbe.Last (lo leen las comprobaciones del bridge).
public class UIPerfProbe : MonoBehaviour
{
    private static readonly string[] Markers = { "GC.Alloc", "Canvas.BuildBatch", "Canvas.SendWillRenderCanvases", "Layout.Rebuild" };

    public static string Last { get; private set; } = "";
    public static bool Running => _instance != null;
    private static UIPerfProbe _instance;

    private Recorder[] _rec;
    private long[] _count;
    private double[] _ms;
    private int _frames, _target;
    private float _maxFrameMs;

    public static void Begin(int frames)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_instance != null) Destroy(_instance.gameObject);
        var go = new GameObject("Sonda de rendimiento de UI");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<UIPerfProbe>();
        _instance._target = Mathf.Max(1, frames);
        Last = "midiendo…";
#else
        Last = "solo en el editor o en builds de desarrollo";
#endif
    }

    void Awake()
    {
        _rec = new Recorder[Markers.Length];
        _count = new long[Markers.Length];
        _ms = new double[Markers.Length];
        for (int i = 0; i < Markers.Length; i++)
        {
            _rec[i] = Recorder.Get(Markers[i]);
            _rec[i].enabled = true;
        }
    }

    // Los Recorder dan los datos del fotograma anterior, así que se lee al principio de cada uno.
    void Update()
    {
        if (_frames > 0)
        {
            for (int i = 0; i < _rec.Length; i++)
            {
                _count[i] += _rec[i].sampleBlockCount;
                _ms[i] += _rec[i].elapsedNanoseconds / 1e6;
            }
            _maxFrameMs = Mathf.Max(_maxFrameMs, Time.unscaledDeltaTime * 1000f);
        }
        if (++_frames <= _target) return;

        int n = _target;
        var sb = new StringBuilder();
        sb.Append(n).Append(" fotogramas");
        for (int i = 0; i < Markers.Length; i++)
            sb.Append(" | ").Append(Markers[i]).Append(": ").Append((_count[i] / (double)n).ToString("0.##")).Append("/fot, ")
              .Append((_ms[i] / n).ToString("0.000")).Append(" ms/fot");
        sb.Append(" | peor fotograma ").Append(_maxFrameMs.ToString("0.0")).Append(" ms");
        Last = sb.ToString();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_rec != null) foreach (Recorder r in _rec) if (r != null) r.enabled = false;
        if (_instance == this) _instance = null;
    }
}
