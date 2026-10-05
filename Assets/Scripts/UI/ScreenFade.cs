using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Fundido a negro de pantalla completa (entrar y salir del castillo).
public class ScreenFade : MonoBehaviour
{
    private static ScreenFade _instance;
    private Image _image;

    public static ScreenFade Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("ScreenFade", typeof(RectTransform));
            _instance = go.AddComponent<ScreenFade>();
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;
            _instance._image = UIFactory.Img(go.transform, "Negro", null, new Color(0f, 0f, 0f, 0f));
            UIFactory.Stretch(_instance._image.rectTransform);
            _instance._image.raycastTarget = false;
            return _instance;
        }
    }

    public float Alpha => _image.color.a;

    public IEnumerator Fade(float to, float seconds)
    {
        float from = _image.color.a;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            _image.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }
        _image.color = new Color(0f, 0f, 0f, to);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
