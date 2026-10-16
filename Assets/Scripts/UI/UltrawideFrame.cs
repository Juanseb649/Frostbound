using System.Collections.Generic;
using UnityEngine;

// Marco de un HUD: a pantalla completa, o en un 16:9 centrado en monitores ultrawide (según DisplaySettings.HudCentered).
public class UltrawideFrame : MonoBehaviour
{
    public static readonly List<UltrawideFrame> All = new List<UltrawideFrame>();

    void OnEnable() { All.Add(this); Apply(Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f); }
    void OnDisable() => All.Remove(this);

    public void Apply(float aspect)
    {
        var rt = (RectTransform)transform;
        float target = 16f / 9f;
        float w = DisplaySettings.HudCentered && aspect > target + 0.01f ? target / aspect : 1f;
        rt.anchorMin = new Vector2((1f - w) * 0.5f, 0f);
        rt.anchorMax = new Vector2(1f - (1f - w) * 0.5f, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Mete todo lo que cuelga de un lienzo en un marco nuevo (para los HUD creados por código).
    public static RectTransform Wrap(Transform canvasRoot, string name = "Marco")
    {
        var frame = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(canvasRoot, false);
        frame.SetAsFirstSibling();
        var children = new List<Transform>();
        foreach (Transform c in canvasRoot) if (c != frame) children.Add(c);
        foreach (Transform c in children) c.SetParent(frame, false);
        frame.gameObject.AddComponent<UltrawideFrame>();
        return frame;
    }
}
