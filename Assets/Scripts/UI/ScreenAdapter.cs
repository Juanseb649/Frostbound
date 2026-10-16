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
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyAll();
        if (_instance != null) return;
        var go = new GameObject("AdaptadorPantalla");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ScreenAdapter>();
    }

    // Antes del primer fotograma de cada escena: los menús ya salen a su tamaño final (sin el salto que se veía).
    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => ApplyAll();

    private static float Aspect => Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;

    // Lo llaman los lienzos creados por código justo al crearse.
    public static void Match(CanvasScaler s)
    {
        if (s == null || s.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return;
        Vector2 r = s.referenceResolution;
        float refAspect = r.y > 0f ? r.x / r.y : 16f / 9f;
        s.matchWidthOrHeight = Aspect >= refAspect ? 1f : 0f;
    }

    private static void ApplyAll()
    {
        foreach (CanvasScaler s in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include)) Match(s);
        foreach (UltrawideFrame f in UltrawideFrame.All) if (f != null) f.Apply(Aspect);
    }

    public static void Refresh()
    {
        if (_instance != null) _instance._next = -1f;
    }

    void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        // Solo cuando cambia el tamaño de la pantalla (o lo pide Configuración); los lienzos nuevos se ajustan al crearse.
        if (size == _lastSize && _next >= 0f) return;
        _lastSize = size;
        _next = 1f;
        ApplyAll();
    }
}
