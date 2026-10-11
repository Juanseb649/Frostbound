// Nombres y rutas de las escenas del juego. Ningún otro script escribe nombres de escena a mano.
public static class SceneIds
{
    public const string MainMenu = "00_MainMenu";
    public const string Village = "01_Village";
    public const string Foothills = "02_Foothills";
    public const string Stairs = "03_Escalinata";

    public const string Folder = "Assets/Scenes/";
    public const string MainMenuPath = Folder + MainMenu + ".unity";
    public const string VillagePath = Folder + Village + ".unity";
    public const string FoothillsPath = Folder + Foothills + ".unity";
    public const string StairsPath = Folder + Stairs + ".unity";

    public static bool IsGameplay(string scene) => scene == Village || scene == Foothills || scene == Stairs;

    public static string Kicker(string scene) => scene == Foothills || scene == Stairs ? "Capítulo I" : "El Frostspire";
    public static string Title(string scene) => scene == Foothills ? "Las Faldas" : scene == Stairs ? "La Escalinata" : scene == Village ? "El poblado" : "Frostbound";
    public static string Subtitle(string scene) => scene == Foothills
        ? "Donde empieza la montaña"
        : scene == Stairs ? "Mil escalones sobre el reino" : scene == Village ? "Al pie del Frostspire" : "";
}
