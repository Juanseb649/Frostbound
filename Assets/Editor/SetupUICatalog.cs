using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Crea (o rehace) la escena de herramienta Assets/Scenes/_UICatalog.unity con el catálogo del kit de UI (UICatalog).
// No se añade a Build Settings. Se puede ejecutar varias veces.
public static class SetupUICatalog
{
    public const string ScenePath = "Assets/Scenes/_UICatalog.unity";

    [MenuItem("Tools/Frostbound/UI/Crear catálogo de UI")]
    public static void Run()
    {
        if (!FrostboundBridge.ConfirmSave()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        new GameObject("Catálogo", typeof(UICatalog));
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[UICatalog] escena creada en " + ScenePath);
    }
}
