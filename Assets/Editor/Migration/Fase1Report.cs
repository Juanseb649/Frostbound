using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Fase1Report
{
    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

    static string Dump(string scenePath, System.Func<GameObject, bool> filter, int maxDepth)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var sb = new StringBuilder();
        foreach (var root in scene.GetRootGameObjects())
            Walk(root.transform, 0, maxDepth, filter, sb);
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "FrostboundBridge", "report.txt"), sb.ToString());
        return "ok";
    }

    static void Walk(Transform t, int d, int max, System.Func<GameObject, bool> filter, StringBuilder sb)
    {
        if (filter == null || filter(t.gameObject))
        {
            var comps = t.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().Name).Where(n => n != "Transform" && n != "RectTransform");
            sb.AppendLine(new string(' ', d * 2) + (t.gameObject.activeSelf ? "" : "(off) ") + Path(t) + "  [" + string.Join(",", comps) + "]");
        }
        if (d < max) foreach (Transform c in t) Walk(c, d + 1, max, filter, sb);
    }

    public static string Menu() => Dump("Assets/Scenes/Main menu.unity", null, 2);

    public static string VillageUsage()
    {
        string[] names = { "PenguinAnimator", "CameraFollow", "OutfitTester", "ProgressionDebug", "MenuManager" };
        Mesh obj = AssetDatabase.LoadAllAssetsAtPath("Assets/Penguin.obj").OfType<Mesh>().FirstOrDefault();
        return Dump("Assets/Scenes/SampleScene.unity", go =>
            go.GetComponents<Component>().Any(c => c == null || names.Contains(c.GetType().Name)) ||
            (obj != null && go.GetComponent<MeshFilter>() != null && go.GetComponent<MeshFilter>().sharedMesh == obj), 12);
    }
}
