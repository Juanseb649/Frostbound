using UnityEditor;
using UnityEditor.SceneManagement;

// Al darle Play en el editor el juego arranca siempre en 00_MainMenu, como en la versión compilada, sin cambiar la
// escena abierta (al salir de Play se vuelve a ella). Se apaga en Tools/Frostbound/Play desde el menú principal para
// probar una escena suelta. El bridge se lo salta en el siguiente Play cuando abre una escena a propósito ("open"),
// y las escenas de herramienta (nombre con "_" delante) siempre arrancan en sí mismas.
[InitializeOnLoad]
public static class PlayFromMainMenu
{
    private const string MenuPath = "Tools/Frostbound/Play desde el menú principal";
    private const string PrefKey = "Frostbound.PlayFromMainMenu";
    private static bool _skipOnce;

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static PlayFromMainMenu()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        // delayCall no corre si el editor está en segundo plano: se aplica ya y otra vez al salir de la edición.
        Apply();
        EditorApplication.delayCall += Apply;
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    // El siguiente Play arranca en la escena abierta (lo usa el bridge tras "open").
    public static void SkipNextPlay()
    {
        _skipOnce = true;
        Apply();
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode) Apply();
        if (change != PlayModeStateChange.EnteredEditMode) return;
        _skipOnce = false;
        Apply();
    }

    // Para el bridge: exec PlayFromMainMenu.State
    public static string State()
    {
        Apply();
        SceneAsset s = EditorSceneManager.playModeStartScene;
        return "activo=" + Enabled + " saltar=" + _skipOnce + " inicio=" + (s != null ? s.name : "escena abierta");
    }

    private static void Apply()
    {
        // Las escenas de herramienta (nombre que empieza por "_", como _UICatalog) arrancan en sí mismas.
        bool toolScene = EditorSceneManager.GetActiveScene().name.StartsWith("_");
        EditorSceneManager.playModeStartScene = Enabled && !_skipOnce && !toolScene
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneIds.MainMenuPath)
            : null;
    }
}
