using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Nevada ligera dentro del Canvas (funciona con Screen Space Overlay).
[RequireComponent(typeof(RectTransform))]
public class UISnow : MonoBehaviour
{
    public Sprite flake;
    public int count = 70;
    public Vector2 sizeRange = new Vector2(3f, 7f);
    public Vector2 speedRange = new Vector2(18f, 45f);
    public float sway = 14f;

    private class Flake
    {
        public RectTransform rt;
        public float speed, phase, baseX;
    }

    private readonly List<Flake> _flakes = new List<Flake>();
    private RectTransform _area;

    void Start()
    {
        _area = (RectTransform)transform;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Flake", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            img.sprite = flake;
            img.raycastTarget = false;
            float size = Random.Range(sizeRange.x, sizeRange.y);
            img.color = new Color(1f, 1f, 1f, Random.Range(0.35f, 0.85f));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            var f = new Flake { rt = rt, speed = Random.Range(speedRange.x, speedRange.y), phase = Random.value * 10f };
            Respawn(f, true);
            _flakes.Add(f);
        }
    }

    private void Respawn(Flake f, bool anywhere)
    {
        Rect r = _area.rect;
        f.baseX = Random.Range(0f, r.width);
        float y = anywhere ? -Random.Range(0f, r.height) : 10f;
        f.rt.anchoredPosition = new Vector2(f.baseX, y);
    }

    void Update()
    {
        Rect r = _area.rect;
        float dt = Time.unscaledDeltaTime;
        foreach (Flake f in _flakes)
        {
            Vector2 p = f.rt.anchoredPosition;
            p.y -= f.speed * dt;
            p.x = f.baseX + Mathf.Sin(Time.unscaledTime * 0.8f + f.phase) * sway;
            f.rt.anchoredPosition = p;
            if (-p.y > r.height + 10f) Respawn(f, false);
        }
    }
}
