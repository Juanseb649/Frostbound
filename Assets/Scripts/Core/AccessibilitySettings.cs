using System;
using UnityEngine;

// Opciones de accesibilidad (Configuración → General), guardadas en PlayerPrefs:
// - Tamaño del texto de lectura (nombres, globos, avisos y mensajes): 100 %, 115 % o 130 %. Los menús no se escalan
//   porque están maquetados a 1920 × 1080 y se saldrían de la pantalla.
// - Forma por rareza: un símbolo en la esquina de cada casilla para no depender solo del color.
public static class AccessibilitySettings
{
    private const string KeyText = "a11y_text_scale", KeyShapes = "a11y_rarity_shapes";
    public static readonly float[] TextSteps = { 1f, 1.15f, 1.3f };

    public static event Action Changed;

    public static int TextStep
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KeyText, 0), 0, TextSteps.Length - 1);
        set
        {
            PlayerPrefs.SetInt(KeyText, Mathf.Clamp(value, 0, TextSteps.Length - 1));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static float TextScale => TextSteps[TextStep];

    public static bool RarityShapes
    {
        get => PlayerPrefs.GetInt(KeyShapes, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(KeyShapes, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Changed = null;
}
