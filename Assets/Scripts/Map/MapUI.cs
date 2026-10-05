using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Minimapa en la esquina (sigue al héroe, norte arriba) y mapa completo con M: lo explorado,
// la posición actual, las hogueras, el poblado, Steve y el objetivo de la misión; lo demás en niebla.
public class MapUI : MonoBehaviour
{
    public const float MinimapSize = 196f;
    public const float MinimapRadiusMeters = 42f;

    private UISkin _skin;
    private RectTransform _miniRoot, _miniContent;
    private RawImage _miniMap, _miniFog;
    private RectTransform _miniMarkers;
    private TextMeshProUGUI _miniTitle;
    private readonly List<Image> _miniIcons = new List<Image>();

    private RectTransform _fullRoot, _fullMapRect, _fullMarkers, _legend;
    private RawImage _fullMap, _fullFog;
    private TextMeshProUGUI _fullTitle, _fullFooter;
    private readonly List<(Image icon, TextMeshProUGUI label)> _fullIcons = new List<(Image, TextMeshProUGUI)>();
    private CanvasGroup _fullGroup;

    public bool FullOpen { get; private set; }

    public void Build(Transform canvas, UISkin skin)
    {
        _skin = skin;
        BuildMinimap(canvas);
        BuildFull(canvas);
    }

    private void BuildMinimap(Transform canvas)
    {
        _miniRoot = UIFactory.Node(canvas, "Minimapa");
        UIFactory.Anchored(_miniRoot, new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(MinimapSize + 12f, MinimapSize + 34f));

        Sprite circle = _skin != null ? _skin.circle : null;
        Image ring = UIFactory.Img(_miniRoot, "Aro", circle, FrostboundUI.Border);
        ring.preserveAspect = false;
        UIFactory.Anchored(ring.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(MinimapSize + 12f, MinimapSize + 12f));
        Image inner = UIFactory.Img(ring.transform, "Fondo", circle, new Color(0.03f, 0.05f, 0.09f, 1f));
        inner.preserveAspect = false;
        UIFactory.Stretch(inner.rectTransform, 4f);

        Image mask = UIFactory.Img(ring.transform, "Mascara", circle, Color.white);
        mask.preserveAspect = false;
        UIFactory.Stretch(mask.rectTransform, 6f);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        _miniContent = UIFactory.Node(mask.transform, "Contenido");
        _miniContent.anchorMin = _miniContent.anchorMax = new Vector2(0.5f, 0.5f);
        _miniContent.pivot = new Vector2(0.5f, 0.5f);
        _miniMap = RawImg(_miniContent, "Mapa");
        _miniFog = RawImg(_miniContent, "Niebla");
        _miniMarkers = UIFactory.Node(mask.transform, "Marcadores");
        UIFactory.Stretch(_miniMarkers);

        _miniTitle = UIFactory.Text(_miniRoot, "Zona", "", _skin != null ? _skin.nunito700 : null, 13f, FrostboundUI.Muted, TextAlignmentOptions.Center, 1f, true);
        UIFactory.Anchored(_miniTitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(MinimapSize + 40f, 20f));
        TextMeshProUGUI hint = UIFactory.Text(ring.transform, "Tecla", "M", _skin != null ? _skin.nunito800 : null, 12f, FrostboundUI.Gold, TextAlignmentOptions.Center);
        UIFactory.Anchored(hint.rectTransform, new Vector2(0.15f, 0.1f), Vector2.zero, new Vector2(22f, 18f));
        TextMeshProUGUI north = UIFactory.Text(ring.transform, "Norte", "N", _skin != null ? _skin.cinzel600 : null, 14f, FrostboundUI.Ice, TextAlignmentOptions.Center);
        UIFactory.Anchored(north.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(22f, 18f));
    }

    private void BuildFull(Transform canvas)
    {
        _fullRoot = UIFactory.Node(canvas, "MapaCompleto");
        UIFactory.Stretch(_fullRoot);
        _fullGroup = _fullRoot.gameObject.AddComponent<CanvasGroup>();
        _fullGroup.blocksRaycasts = false;
        Image dim = UIFactory.Img(_fullRoot, "Velo", null, new Color(0f, 0.01f, 0.03f, 0.78f));
        UIFactory.Stretch(dim.rectTransform);

        Sprite round = _skin != null ? _skin.round12 : null;
        Image frame = UIFactory.Frame(_fullRoot, "Marco", round, new Color(0.04f, 0.06f, 0.11f, 0.98f), FrostboundUI.Border, out _);
        UIFactory.Anchored(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-90f, -6f), new Vector2(760f, 800f));

        _fullTitle = UIFactory.Text(frame.transform, "Titulo", "MAPA", _skin != null ? _skin.cinzel600 : null, 26f, FrostboundUI.Title, TextAlignmentOptions.Center, 3f, true);
        UIFactory.Anchored(_fullTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(700f, 34f));

        RectTransform viewport = UIFactory.Node(frame.transform, "Vista");
        UIFactory.Anchored(viewport, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(700f, 700f));
        Image vbg = viewport.gameObject.AddComponent<Image>();
        vbg.color = new Color(0.02f, 0.03f, 0.06f, 1f);
        viewport.gameObject.AddComponent<RectMask2D>();
        _fullMapRect = UIFactory.Node(viewport, "Contenido");
        UIFactory.Stretch(_fullMapRect);
        _fullMap = RawImg(_fullMapRect, "Mapa");
        UIFactory.Stretch(_fullMap.rectTransform);
        _fullFog = RawImg(_fullMapRect, "Niebla");
        UIFactory.Stretch(_fullFog.rectTransform);
        _fullMarkers = UIFactory.Node(_fullMapRect, "Marcadores");
        UIFactory.Stretch(_fullMarkers);

        _fullFooter = UIFactory.Text(frame.transform, "Pie", "", _skin != null ? _skin.nunito700 : null, 14f, FrostboundUI.Muted, TextAlignmentOptions.Center);
        UIFactory.Anchored(_fullFooter.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(700f, 22f));

        // Leyenda a la derecha.
        Image legend = UIFactory.Frame(_fullRoot, "Leyenda", round, new Color(0.04f, 0.06f, 0.11f, 0.96f), FrostboundUI.Border, out _);
        UIFactory.Anchored(legend.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(395f, 150f), new Vector2(200f, 300f));
        _legend = legend.rectTransform;
        TextMeshProUGUI lt = UIFactory.Text(legend.transform, "Titulo", "LEYENDA", _skin != null ? _skin.cinzel600 : null, 16f, FrostboundUI.Gold, TextAlignmentOptions.Center, 2f);
        UIFactory.Anchored(lt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(180f, 22f));
        var entries = new (MapSystem.MarkerKind k, string label)[]
        {
            (MapSystem.MarkerKind.Player, "Tu posición"), (MapSystem.MarkerKind.Objective, "Objetivo"),
            (MapSystem.MarkerKind.BonfireLit, "Hoguera encendida"), (MapSystem.MarkerKind.Bonfire, "Hoguera apagada"),
            (MapSystem.MarkerKind.Village, "Poblado"), (MapSystem.MarkerKind.Steve, "Steve"),
            (MapSystem.MarkerKind.Castle, "Castillo"), (MapSystem.MarkerKind.Citadel, "Ciudadela caída"), (MapSystem.MarkerKind.Exit, "Salida")
        };
        for (int i = 0; i < entries.Length; i++)
        {
            Image ic = UIFactory.Img(legend.transform, "Icono", MapIcons.Get(MapSystem.ShapeOf(entries[i].k)), MapSystem.ColorOf(entries[i].k));
            UIFactory.TopLeft(ic.rectTransform, 18f, 46f + i * 27f, 20f, 20f);
            TextMeshProUGUI t = UIFactory.Text(legend.transform, "Texto", entries[i].label, _skin != null ? _skin.nunito700 : null, 14f, FrostboundUI.Body, TextAlignmentOptions.Left);
            UIFactory.TopLeft(t.rectTransform, 46f, 46f + i * 27f, 150f, 20f);
        }
        _fullRoot.gameObject.SetActive(false);
    }

    private static RawImage RawImg(Transform parent, string name)
    {
        RectTransform rt = UIFactory.Node(parent, name);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.raycastTarget = false;
        return img;
    }

    public void SetVisible(bool visible)
    {
        if (_miniRoot != null) _miniRoot.gameObject.SetActive(visible);
        if (!visible) Close();
    }

    public void Toggle()
    {
        if (FullOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (FullOpen || _fullRoot == null) return;
        FullOpen = true;
        _fullRoot.gameObject.SetActive(true);
    }

    public void Close()
    {
        if (!FullOpen) return;
        FullOpen = false;
        _fullRoot.gameObject.SetActive(false);
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.mKey.wasPressedThisFrame && !SaveSlotsPanel.IsOpen) Toggle();
        if (FullOpen && kb != null && kb.escapeKey.wasPressedThisFrame) Close();

        MapSystem map = MapSystem.Instance;
        if (map == null || _miniRoot == null) return;
        MapSystem.Area area = map.CurrentArea;
        if (area == null || area.map == null) return;
        UpdateMinimap(map, area);
        if (FullOpen) UpdateFull(map, area);
    }

    private Vector3 PlayerPos(MapSystem map)
    {
        foreach (MapSystem.Marker m in map.markers) if (m.kind == MapSystem.MarkerKind.Player) return m.position;
        return area0;
    }

    private static readonly Vector3 area0 = Vector3.zero;

    private void UpdateMinimap(MapSystem map, MapSystem.Area area)
    {
        _miniTitle.text = area.title;
        float ppm = (MinimapSize * 0.5f - 6f) / MinimapRadiusMeters;
        float size = area.half * 2f * ppm;
        _miniMap.texture = area.map;
        _miniFog.texture = area.fogTex;
        _miniContent.sizeDelta = new Vector2(size, size);
        _miniMap.rectTransform.anchorMin = Vector2.zero;
        _miniMap.rectTransform.anchorMax = Vector2.one;
        _miniMap.rectTransform.offsetMin = _miniMap.rectTransform.offsetMax = Vector2.zero;
        _miniFog.rectTransform.anchorMin = Vector2.zero;
        _miniFog.rectTransform.anchorMax = Vector2.one;
        _miniFog.rectTransform.offsetMin = _miniFog.rectTransform.offsetMax = Vector2.zero;
        Vector3 player = PlayerPos(map);
        Vector2 uv = area.ToUV(player);
        _miniContent.anchoredPosition = -(uv - new Vector2(0.5f, 0.5f)) * size;

        float radius = MinimapSize * 0.5f - 6f;
        int used = 0;
        foreach (MapSystem.Marker m in map.markers)
        {
            if (!area.Contains(m.position)) continue;
            if (!m.always && !area.IsExplored(m.position)) continue;
            Vector2 offset = new Vector2(m.position.x - player.x, m.position.z - player.z) * ppm;
            if (offset.magnitude > radius - 8f)
            {
                if (!m.clampToEdge) continue;
                offset = offset.normalized * (radius - 10f);
            }
            Image icon = Icon(_miniIcons, _miniMarkers, used++);
            Style(icon, m, 16f);
            icon.rectTransform.anchoredPosition = offset;
            if (m.kind == MapSystem.MarkerKind.Player) icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -PlayerYaw());
        }
        for (int i = used; i < _miniIcons.Count; i++) _miniIcons[i].gameObject.SetActive(false);
    }

    private void UpdateFull(MapSystem map, MapSystem.Area area)
    {
        _fullTitle.text = area.title;
        _fullMap.texture = area.map;
        _fullFog.texture = area.fogTex;
        _fullFooter.text = "Explorado " + Mathf.RoundToInt(area.ExploredPercent) + "%   ·   M o Esc para cerrar";
        Vector2 size = _fullMapRect.rect.size;
        int used = 0;
        foreach (MapSystem.Marker m in map.markers)
        {
            if (!area.Contains(m.position)) continue;
            if (!m.always && !area.IsExplored(m.position)) continue;
            Vector2 uv = area.ToUV(m.position);
            while (_fullIcons.Count <= used)
            {
                Image ic = UIFactory.Img(_fullMarkers, "Icono", null, Color.white);
                ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = Vector2.zero;
                TextMeshProUGUI t = UIFactory.Text(ic.transform, "Etiqueta", "", _skin != null ? _skin.nunito800 : null, 13f, FrostboundUI.Text, TextAlignmentOptions.Center);
                UIFactory.Anchored(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -16f), new Vector2(160f, 18f));
                t.outlineWidth = 0.25f;
                t.outlineColor = new Color32(0, 0, 0, 220);
                _fullIcons.Add((ic, t));
            }
            var (icon, label) = _fullIcons[used++];
            icon.gameObject.SetActive(true);
            Style(icon, m, 22f);
            icon.rectTransform.anchoredPosition = new Vector2(uv.x * size.x, uv.y * size.y);
            icon.rectTransform.localRotation = m.kind == MapSystem.MarkerKind.Player ? Quaternion.Euler(0f, 0f, -PlayerYaw()) : Quaternion.identity;
            label.text = m.kind == MapSystem.MarkerKind.Player || m.kind == MapSystem.MarkerKind.Objective ? "" : m.label;
            label.rectTransform.localRotation = Quaternion.Inverse(icon.rectTransform.localRotation);
        }
        for (int i = used; i < _fullIcons.Count; i++) _fullIcons[i].icon.gameObject.SetActive(false);
    }

    private static Image Icon(List<Image> pool, RectTransform parent, int index)
    {
        while (pool.Count <= index)
        {
            Image img = UIFactory.Img(parent, "Icono", null, Color.white);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            pool.Add(img);
        }
        Image i = pool[index];
        i.gameObject.SetActive(true);
        i.rectTransform.localRotation = Quaternion.identity;
        return i;
    }

    private static void Style(Image icon, MapSystem.Marker m, float baseSize)
    {
        icon.sprite = MapIcons.Get(MapSystem.ShapeOf(m.kind));
        icon.color = MapSystem.ColorOf(m.kind);
        float s = baseSize * MapSystem.SizeOf(m.kind);
        if (m.kind == MapSystem.MarkerKind.Objective) s *= 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f);
        icon.rectTransform.sizeDelta = new Vector2(s, s);
        icon.transform.SetAsLastSibling();
    }

    private float PlayerYaw()
    {
        GameObject p = GameObject.Find("Player");
        return p != null ? p.transform.eulerAngles.y : 0f;
    }
}
