using UnityEditor;

// Importa la mansión modelada en Blender (Blender/frostbound_manor.py → Assets/Resources/Models/Mansion.fbx):
// legible (HauntedManor le proyecta las UVs en tiempo de ejecución), sin cámaras ni luces y con sus nombres de
// material (HauntedManor asigna los suyos por nombre).
public class ManorModelPostprocessor : AssetPostprocessor
{
    private const string Path = "Assets/Resources/Models/Mansion.fbx";

    void OnPreprocessModel()
    {
        if (assetPath != Path) return;
        var mi = (ModelImporter)assetImporter;
        mi.isReadable = true;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        mi.importNormals = ModelImporterNormals.Import;
        mi.globalScale = 1f;
    }
}
