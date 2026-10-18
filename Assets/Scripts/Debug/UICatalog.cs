using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Catálogo del kit de UI «gótico de escarcha» (guía /d2r-ui, tarea UI-4): muestra en una sola pantalla todos los
// componentes y sus estados para revisarlos de un vistazo: títulos y textos, botones (normal, principal, peligro,
// desactivado y enfocado), paneles de tres tamaños, marcos por rareza con su forma, separador, paleta de tokens y
// la disolución. Se navega con ratón, teclado o mando. Vive en la escena de herramienta _UICatalog (fuera de la build).
public class UICatalog : MonoBehaviour
{
    private RectTransform _root, _demoPanel;
    private FrostButton _first;

    void Start()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FrostboundUI.Abyss;
        }
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        var go = new GameObject("Catálogo de UI", typeof(RectTransform));
        go.layer = 5;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        ScreenAdapter.Match(scaler);
        go.AddComponent<GraphicRaycaster>();
        _root = (RectTransform)go.transform;

        Typography();
        Buttons();
        Panels();
        Rarities();
        Palette();
        Dissolve();
        if (_first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_first.gameObject);
    }

    private static RectTransform At(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private void Section(string title, float x, float y)
    {
        TextMeshProUGUI k = FrostUI.Kicker(_root, "Seccion_" + title, title, 16f);
        At(k.rectTransform, x, y, 600f, 24f);
    }

    private void Typography()
    {
        TextMeshProUGUI t = FrostUI.Title(_root, "Titulo", "CATÁLOGO DE UI", 64f, TextAlignmentOptions.Left);
        At(t.rectTransform, 60f, 30f, 900f, 80f);
        TextMeshProUGUI k = FrostUI.Kicker(_root, "Antetitulo", "Gótico de escarcha · kit /d2r-ui", 18f);
        At(k.rectTransform, 64f, 108f, 900f, 26f);
        TextMeshProUGUI body = FrostUI.Text(_root, "Cuerpo", "Texto en Nunito sobre placa de piedra. Los títulos van en Cinzel oro viejo.", 18f, FrostboundUI.Bone);
        At(body.rectTransform, 64f, 140f, 900f, 28f);
        Image div = FrostUI.Img(_root, "Separador", FrostFrames.Divider, Color.white);
        At(div.rectTransform, 64f, 176f, 520f, 24f);
    }

    private void Buttons()
    {
        Section("Botones", 64f, 220f);
        var specs = new (string label, FrostButton.Style style, bool disabled, bool selected)[]
        {
            ("Normal", FrostButton.Style.Normal, false, false),
            ("Principal", FrostButton.Style.Primary, false, false),
            ("Peligro", FrostButton.Style.Danger, false, false),
            ("Desactivado", FrostButton.Style.Normal, true, false),
            ("Seleccionado", FrostButton.Style.Normal, false, true),
        };
        for (int i = 0; i < specs.Length; i++)
        {
            var s = specs[i];
            FrostButton b = FrostButton.Create(_root, "Boton_" + s.label, s.label, new Vector2(240f, 56f), s.style, 26f);
            At((RectTransform)b.transform, 64f + (i % 3) * 256f, 252f + (i / 3) * 72f, 240f, 56f);
            b.button.interactable = !s.disabled;
            b.selectedLook = s.selected;
            b.Refresh();
            if (i == 0) _first = b;
        }
        TextMeshProUGUI hint = FrostUI.Text(_root, "Pista", "Pasa el ratón o mueve el foco: destello dorado y ribete de escarcha.", 15f, FrostboundUI.Ash);
        At(hint.rectTransform, 64f, 400f, 800f, 22f);
    }

    private void Panels()
    {
        Section("Paneles", 64f, 440f);
        float[] radii = { 18f, 12f, 10f };
        string[] names = { "Panel grande", "Caja", "Chip" };
        float x = 64f;
        for (int i = 0; i < radii.Length; i++)
        {
            float w = i == 0 ? 320f : i == 1 ? 240f : 160f, h = i == 0 ? 180f : i == 1 ? 120f : 44f;
            Image p = FrostUI.Panel(_root, names[i], out _, radii[i]);
            At(p.rectTransform, x, 472f, w, h);
            TextMeshProUGUI l = FrostUI.Text(p.transform, "Nombre", names[i], 16f, FrostboundUI.Bone, TextAlignmentOptions.Center);
            FrostUI.Stretch(l.rectTransform);
            x += w + 20f;
        }
        Image gold = FrostUI.Panel(_root, "Chip oro", out _, 10f, FrostboundUI.ChipGoldBorder, FrostboundUI.ChipGoldBg);
        At(gold.rectTransform, x - 160f - 20f, 532f, 160f, 44f);
        TextMeshProUGUI gl = FrostUI.Text(gold.transform, "Nombre", "Chip oro", 15f, FrostUI.Gold, TextAlignmentOptions.Center);
        FrostUI.Stretch(gl.rectTransform);
    }

    private void Rarities()
    {
        Section("Rarezas · marco de plata teñido y forma", 64f, 690f);
        var items = new (string name, Color color, Sprite shape)[]
        {
            ("Común", FrostboundUI.Border, null),
            ("Poco común", FrostboundUI.Rarity(ItemRarity.Uncommon), FrostUI.Skin != null ? FrostUI.Skin.circle : null),
            ("Raro", FrostboundUI.Rarity(ItemRarity.Rare), FrostUI.Diamond(true)),
            ("Épico", FrostboundUI.Rarity(ItemRarity.Epic), FrostUI.Icon("Plus")),
            ("Único", FrostboundUI.Rarity(ItemRarity.Unique), FrostUI.Icon("Ninja")),
            ("Foco", FrostboundUI.OldGoldLight, null),
        };
        for (int i = 0; i < items.Length; i++)
        {
            Image cell = UIFactory.Frame(_root, "Casilla_" + items[i].name, FrostUI.Skin != null ? FrostUI.Skin.round12 : null, FrostboundUI.Surface, items[i].color, out _);
            At(cell.rectTransform, 64f + i * 112f, 722f, 88f, 88f);
            if (items[i].shape != null)
            {
                Image shape = FrostUI.Img(cell.transform, "Forma", items[i].shape, items[i].color);
                FrostUI.Place(shape.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(12f, 12f));
            }
            TextMeshProUGUI l = FrostUI.Text(_root, "Rareza_" + items[i].name, items[i].name, 14f, items[i].color, TextAlignmentOptions.Center);
            At(l.rectTransform, 54f + i * 112f, 816f, 108f, 22f);
        }
    }

    private void Palette()
    {
        Section("Tokens de color (FrostboundUI)", 1060f, 220f);
        var tokens = new (string name, Color c)[]
        {
            ("Abyss", FrostboundUI.Abyss), ("Night (Bg)", FrostboundUI.Bg), ("Slate", FrostboundUI.Slate), ("Iron", FrostboundUI.Iron),
            ("IronLight", FrostboundUI.IronLight), ("Frost (Ice)", FrostboundUI.Ice), ("FrostDeep", FrostboundUI.FrostDeep), ("OldGold", FrostboundUI.OldGold),
            ("OldGoldLight", FrostboundUI.OldGoldLight), ("Bone", FrostboundUI.Bone), ("Ash", FrostboundUI.Ash), ("Blood", FrostboundUI.Blood),
        };
        for (int i = 0; i < tokens.Length; i++)
        {
            float x = 1060f + (i % 2) * 400f, y = 254f + (i / 2) * 58f;
            Image sw = UIFactory.Frame(_root, "Muestra_" + tokens[i].name, FrostUI.Skin != null ? FrostUI.Skin.round10 : null, tokens[i].c, FrostboundUI.IronLight, out _);
            At(sw.rectTransform, x, y, 64f, 44f);
            TextMeshProUGUI l = FrostUI.Text(_root, "Token_" + tokens[i].name, tokens[i].name + "  <color=#8A93A3>#" + ColorUtility.ToHtmlStringRGB(tokens[i].c) + "</color>", 17f, FrostboundUI.Bone);
            At(l.rectTransform, x + 78f, y + 8f, 310f, 28f);
        }
    }

    private void Dissolve()
    {
        Section("Disolución al cerrar", 1060f, 640f);
        _demoPanel = MakeDemoPanel();
        FrostButton b = FrostButton.Create(_root, "Disolver", "Disolver", new Vector2(240f, 56f), FrostButton.Style.Primary, 26f);
        At((RectTransform)b.transform, 1400f, 700f, 240f, 56f);
        b.button.onClick.AddListener(() =>
        {
            UIDissolve.Ghost(_demoPanel, 1.2f);
            Destroy(_demoPanel.gameObject);
            Invoke(nameof(Rebuild), 1.6f);
        });
    }

    private RectTransform MakeDemoPanel()
    {
        Image p = FrostUI.Panel(_root, "Panel de prueba", out _, 18f);
        At(p.rectTransform, 1060f, 672f, 320f, 160f);
        TextMeshProUGUI t = FrostUI.Title(p.transform, "Titulo", "VENTANA", 34f);
        FrostUI.Stretch(t.rectTransform);
        return p.rectTransform;
    }

    private void Rebuild() => _demoPanel = MakeDemoPanel();
}
