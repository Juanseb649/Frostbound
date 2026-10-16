using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Adapta toda la interfaz a cualquier pantalla (de 4:3 a ultrawide 21:9 y 32:9):
// - En pantallas más anchas que el diseño, la interfaz se escala por la altura (nada queda por debajo del borde, como
//   pasaba con el botón de crear héroe en un monitor ultrawide); en las más estrechas, por el ancho.
// - Los HUD marcados con UltrawideFrame se quedan en un marco 16:9 centrado si el jugador lo elige (Configuración → General).
public class ScreenAdapter : MonoBehaviour
{
    private static ScreenAdapter _instance;
    private float _next;
    private Vector2Int _lastSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (_instance != null) return;
        var go = new GameObject("AdaptadorPantalla");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ScreenAdapter>();
    }

    public static void Refresh()
    {
        if (_instance != null) _instance._next = 0f;
    }

    void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (Time.unscaledTime < _next && size == _lastSize) return;
        _next = Time.unscaledTime + 0.5f;
        _lastSize = size;
        float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
        foreach (CanvasScaler s in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include))
        {
            if (s.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            Vector2 r = s.referenceResolution;
            float refAspect = r.y > 0f ? r.x / r.y : 16f / 9f;
            s.matchWidthOrHeight = aspect >= refAspect ? 1f : 0f;
        }
        foreach (UltrawideFrame f in UltrawideFrame.All) if (f != null) f.Apply(aspect);
    }
}

