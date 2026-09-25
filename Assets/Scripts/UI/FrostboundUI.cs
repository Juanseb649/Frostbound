using UnityEngine;

// Tokens visuales compartidos por el menú principal y la selección de clase.
public static class FrostboundUI
{
    public static readonly Color Bg = Hex("#0A1222");
    public static readonly Color Surface = Hex("#0F1B31");
    public static readonly Color SurfaceSelected = Hex("#1B2C4B");
    public static readonly Color Border = Hex("#2A3D5C");
    public static readonly Color Ice = Hex("#BFEAFF");
    public static readonly Color Text = Hex("#E8F1FA");
    public static readonly Color Title = Hex("#F2F9FF");
    public static readonly Color Muted = Hex("#9FB3C8");
    public static readonly Color Body = Hex("#C9D7E6");
    public static readonly Color Disabled = Hex("#6F839B");
    public static readonly Color Version = Hex("#5B6F8A");
    public static readonly Color Gold = Hex("#E0B44A");
    public static readonly Color ChipGoldBg = Hex("#3A2E14");
    public static readonly Color ChipGoldText = Hex("#F3D48A");
    public static readonly Color White = Color.white;

    // Juego: barras, rarezas y comparaciones.
    public static readonly Color HealthBar = Hex("#E0524A");
    public static readonly Color ManaBar = Hex("#4F8BFF");
    public static readonly Color XpBar = Hex("#E0B44A");
    public static readonly Color Positive = Hex("#7FDB94");
    public static readonly Color Negative = Hex("#F07A6E");
    public static readonly Color Dim = new Color(0.039f, 0.071f, 0.133f, 0.88f);

    public static Color Rarity(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Uncommon: return Hex("#6FD08C");
            case ItemRarity.Rare: return Hex("#5AA9FF");
            case ItemRarity.Epic: return Hex("#B57CFF");
            case ItemRarity.Unique: return Hex("#F3B94A");
            default: return Hex("#C9D7E6");
        }
    }

    public static string RichHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    public const float ReferenceWidth = 1440f;
    public const float ReferenceHeight = 900f;
    public const float HoverScale = 1.03f;
    public const float HoverDuration = 0.1f;
    public const float FadeDuration = 0.25f;

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    // Espaciado de letras de CSS (px) a unidades de TextMeshPro (1/100 em).
    public static float Tracking(float px, float fontSize) => px * 100f / fontSize;
}
