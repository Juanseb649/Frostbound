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

    // Gótico de escarcha (guía /d2r-ui): hierro, piedra, oro viejo y hueso sobre noche.
    public static readonly Color Abyss = Hex("#05080F");
    public static readonly Color Slate = Hex("#141C2B");
    public static readonly Color Iron = Hex("#4A5568");
    public static readonly Color IronLight = Hex("#9AA8BC");
    public static readonly Color FrostDeep = Hex("#3A8FC4");
    public static readonly Color OldGold = Hex("#B8923E");
    public static readonly Color OldGoldLight = Hex("#E8C877");
    public static readonly Color OldGoldDeep = Hex("#4A3410");
    public static readonly Color Bone = Hex("#D9D2C3");
    public static readonly Color Ash = Hex("#8A93A3");
    public static readonly Color Blood = Hex("#B3261E");

    // Menús (FrostUI): letra helada, paneles, chips y estados.
    public static readonly Color IceDeep = Hex("#5AA6D6");
    public static readonly Color OutlineNight = Hex("#04101F");
    public static readonly Color InnerOutline = Hex("#1B4F86");
    // Casi opaco: el marco de hierro tiene el centro hueco y la placa es la única capa del panel.
    public static readonly Color MenuPanel = new Color(0.043f, 0.075f, 0.133f, 0.97f);
    public static readonly Color MenuPanelSolid = Hex("#0B1322");
    public static readonly Color MenuBorder = Hex("#5F8FB8");
    public static readonly Color MenuGold = Hex("#F3D48A");
    public static readonly Color MenuDisabled = Hex("#56677E");
    public static readonly Color MenuLabel = Hex("#8FC6EA");
    public static readonly Color Danger = Hex("#C0392B");
    public static readonly Color ChipIceBg = Hex("#13284A");
    public static readonly Color ChipGoldBorder = Hex("#B88A2E");
    public static readonly Color LoadingBg = Hex("#050A14");
    public static readonly Color WindowBg = Hex("#08101D");
    public static readonly Color StatLife = Hex("#F07A6E");
    public static readonly Color StatMana = Hex("#7FB0FF");

    // Iconos de botones del mando (colores de los botones de Xbox y del dibujo del mando).
    public static readonly Color PadA = Hex("#5DBB3E");
    public static readonly Color PadB = Hex("#E2463F");
    public static readonly Color PadX = Hex("#3B82E0");
    public static readonly Color PadY = Hex("#F2B71F");
    public static readonly Color PadBody = Hex("#20283A");
    public static readonly Color PadRim = Hex("#8FA3BF");
    public static readonly Color PadInk = Hex("#05080E");
    public static readonly Color PadStick = Hex("#2E3950");

    // Calidad del botín (LootQualityInfo).
    public static readonly Color QualityWorn = Hex("#8A97A8");
    public static readonly Color QualityNormal = Hex("#E8F1FA");
    public static readonly Color QualityMagic = Hex("#6FA8FF");
    public static readonly Color QualityRare = Hex("#F3D48A");
    public static readonly Color QualitySet = Hex("#6FD08A");
    public static readonly Color QualityUnique = Hex("#E0B44A");
    public static readonly Color QualityRune = Hex("#7FD8FF");

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

    public static Color StatColor(StatType type)
    {
        switch (type)
        {
            case StatType.Strength: return Hex("#F08A5D");
            case StatType.Mana: return Hex("#6FA8FF");
            case StatType.Agility: return Hex("#7FDB94");
            default: return Hex("#F0646A");
        }
    }

    public static string RichHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    public const float ReferenceWidth = 1440f;
    public const float ReferenceHeight = 900f;
    public const float HoverScale = 1.01f;
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
