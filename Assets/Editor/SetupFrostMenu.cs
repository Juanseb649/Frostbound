using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Monta los menús de inicio nuevos (FrostMenu) y el ambiente de noche helada en 00_MainMenu, con el decorado
// elegido: la mansión helada (por defecto) o las ruinas del Frostspire. Se puede ejecutar varias veces.
public static class SetupFrostMenu
{
    private const string FontDir = "Assets/Frostbound/UI/Fonts";
    private const string PenguinGuid = "fd2c52740e49f0a4a8e0be67f7beef44";

    private const string ManorName = "Mansión helada";
    private const string RuinsName = "Ruinas del Frostspire";

    [MenuItem("Tools/Frostbound/Menús/Montar menús de noche helada")]
    public static void Run() => Run(null);

    [MenuItem("Tools/Frostbound/Menús/Decorado: Mansión")]
    public static void RunManor() => Run(FrostMenu.Backdrop.Manor);

    [MenuItem("Tools/Frostbound/Menús/Decorado: Ruinas")]
    public static void RunRuins() => Run(FrostMenu.Backdrop.Ruins);

    // Sin decorado indicado se mantiene el que ya tenga la escena (la mansión si es la primera vez).
    private static void Run(FrostMenu.Backdrop? backdrop)
    {
        AssetDatabase.Refresh();
        GameDatabase db = AssetDatabase.LoadAssetAtPath<GameDatabase>(SetupProceduralVillage.DatabasePath);
        if (db == null) { Debug.LogError("[FrostMenu] falta GameDatabase"); return; }
        if (db.uiSkin != null)
        {
            db.uiSkin.lilita = Lilita();
            EditorUtility.SetDirty(db.uiSkin);
        }
        db.penguinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(PenguinGuid));
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.OpenScene(SceneIds.MainMenuPath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "MainMenuCanvas") Object.DestroyImmediate(root);

        GameObject menu = GameObject.Find("FrostMenu");
        if (menu == null) menu = new GameObject("FrostMenu");
        if (menu.GetComponent<NightFrostAtmosphere>() == null) menu.AddComponent<NightFrostAtmosphere>();
        FrostMenu frost = menu.GetComponent<FrostMenu>();
        if (frost == null) frost = menu.AddComponent<FrostMenu>();
        if (backdrop.HasValue) frost.backdrop = backdrop.Value;
        EditorUtility.SetDirty(frost);
        bool manor = frost.backdrop == FrostMenu.Backdrop.Manor;

        // Solo queda en la escena el decorado elegido; el otro se quita (sus scripts siguen en el proyecto).
        GameObject keep = GameObject.Find(manor ? ManorName : RuinsName);
        GameObject drop = GameObject.Find(manor ? RuinsName : ManorName);
        if (drop != null) Object.DestroyImmediate(drop);
        if (keep == null) keep = new GameObject(manor ? ManorName : RuinsName);
        if (manor && keep.GetComponent<HauntedManor>() == null) keep.AddComponent<HauntedManor>();
        if (!manor && keep.GetComponent<MenuRuins>() == null) keep.AddComponent<MenuRuins>();

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = manor ? HauntedManor.TitleCamPos : new Vector3(0f, 2.5f, -11.8f);
            cam.transform.LookAt(manor ? HauntedManor.TitleCamLook : new Vector3(0f, 2.3f, 2f));
            cam.fieldOfView = manor ? HauntedManor.TitleFov : 38f;
            // La mansión necesita perspectiva: en ortográfica el fondo se ve tan grande como el primer plano.
            if (manor) cam.orthographic = false;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 220f;
            menu.GetComponent<NightFrostAtmosphere>().Setup(cam);
            EditorUtility.SetDirty(cam);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FrostMenu] menús montados · decorado: " + (manor ? "mansión" : "ruinas"));
    }

    private static TMP_FontAsset Lilita()
    {
        const string name = "LilitaOne-Regular";
        string path = FontDir + "/" + name + " SDF.asset";
        TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (fa != null) return fa;
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/" + name + ".ttf");
        if (font == null) { Debug.LogError("[FrostMenu] falta " + name + ".ttf"); return null; }
        fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = name + " SDF";
        AssetDatabase.CreateAsset(fa, path);
        fa.material.name = name + " SDF Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.atlasTextures[0].name = name + " SDF Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?¿¡·—-áéíóúÁÉÍÓÚñÑüÜ'\"()/+");
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }
}
