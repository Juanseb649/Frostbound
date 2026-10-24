// Nombres y rutas de las escenas del juego. Ningún otro script escribe nombres de escena a mano.
public static class SceneIds
{
    public const string MainMenu = "00_MainMenu";
    public const string Village = "01_Village";
    public const string Foothills = "02_Foothills";
    public const string Stairs = "03_Escalinata";
    public const string Forest = "04_BosqueCorrupto";
    public const string Summit = "05_Cumbre";

    public const string Folder = "Assets/Scenes/";
    public const string MainMenuPath = Folder + MainMenu + ".unity";
    public const string VillagePath = Folder + Village + ".unity";
    public const string FoothillsPath = Folder + Foothills + ".unity";
    public const string StairsPath = Folder + Stairs + ".unity";
    public const string ForestPath = Folder + Forest + ".unity";
    public const string SummitPath = Folder + Summit + ".unity";

    public static bool IsGameplay(string scene) => scene == Village || scene == Foothills || scene == Stairs || scene == Forest || scene == Summit;

    public static string Kicker(string scene) => scene == Foothills || scene == Stairs ? "Capítulo I"
        : scene == Forest ? "Capítulo II" : scene == Summit ? "Capítulo III" : "El Frostspire";
    public static string Title(string scene) => scene == Foothills ? "Las Faldas" : scene == Stairs ? "La Escalinata"
        : scene == Forest ? "El Bosque Corrupto" : scene == Summit ? "La Cumbre" : scene == Village ? "El poblado" : "Frostbound";
    public static string Subtitle(string scene) => scene == Foothills ? "Donde empieza la montaña"
        : scene == Stairs ? "Mil escalones sobre el reino"
        : scene == Forest ? "Donde los pinos sangran savia"
        : scene == Summit ? "Donde late el Corazón del Frost"
        : scene == Village ? "Al pie del Frostspire" : "";
}
