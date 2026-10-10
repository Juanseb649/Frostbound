// Nombres y rutas de las escenas del juego. Ningún otro script escribe nombres de escena a mano.
public static class SceneIds
{
    public const string MainMenu = "00_MainMenu";
    public const string Village = "01_Village";
    public const string Foothills = "02_Foothills";

    public const string Folder = "Assets/Scenes/";
    public const string MainMenuPath = Folder + MainMenu + ".unity";
    public const string VillagePath = Folder + Village + ".unity";
    public const string FoothillsPath = Folder + Foothills + ".unity";

    public static bool IsGameplay(string scene) => scene == Village || scene == Foothills;

    public static string Kicker(string scene) => scene == Foothills ? "Capítulo I" : "El Frostspire";
    public static string Title(string scene) => scene == Foothills ? "Las Faldas" : scene == Village ? "El poblado" : "Frostbound";
    public static string Subtitle(string scene) => scene == Foothills
        ? "Donde empieza la montaña"
        : scene == Village ? "Al pie del Frostspire" : "";
}
