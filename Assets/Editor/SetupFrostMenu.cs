using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Monta los menús de inicio nuevos (FrostMenu) y el ambiente de noche helada en 00_MainMenu.
// Se puede ejecutar varias veces.
public static class SetupFrostMenu
{
    private const string FontDir = "Assets/Frostbound/UI/Fonts";
    private const string PenguinGuid = "fd2c52740e49f0a4a8e0be67f7beef44";

    [MenuItem("Tools/Frostbound/Menús/Montar menús de noche helada")]
    public static void Run()
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
        if (menu.GetComponent<FrostMenu>() == null) menu.AddComponent<FrostMenu>();
        GameObject ruins = GameObject.Find("Ruinas del Frostspire");
        if (ruins == null) ruins = new GameObject("Ruinas del Frostspire");
        if (ruins.GetComponent<MenuRuins>() == null) ruins.AddComponent<MenuRuins>();

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 2.5f, -11.8f);
            cam.transform.LookAt(new Vector3(0f, 2.3f, 2f));
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 220f;
            menu.GetComponent<NightFrostAtmosphere>().Setup(cam);
            EditorUtility.SetDirty(cam);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FrostMenu] menús montados");
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
