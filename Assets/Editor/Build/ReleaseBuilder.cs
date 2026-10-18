using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Compila versiones ejecutables (Windows, macOS y Linux) en la carpeta Builds/ del proyecto.
// Tools/Frostbound/Release/... o por el bridge: exec ReleaseBuilder.Supported / BuildWindows / BuildMac / BuildLinux.
public static class ReleaseBuilder
{
    private const string GameName = "Frostbound";

    private static string Root => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds");

    private static string[] Scenes => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

    public static string Supported()
    {
        var sb = new StringBuilder("version=" + PlayerSettings.bundleVersion + " escenas=" + Scenes.Length);
        sb.Append(" | Windows=").Append(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64));
        sb.Append(" macOS=").Append(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX));
        sb.Append(" Linux=").Append(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64));
        sb.Append(" | backend=").Append(PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone));
        sb.Append(" | editor=").Append(Application.unityVersion).Append(" ").Append(Application.platform);
        return sb.ToString();
    }

    [MenuItem("Tools/Frostbound/Release/Windows")] public static void MenuWindows() => Debug.Log(BuildWindows());
    [MenuItem("Tools/Frostbound/Release/macOS")] public static void MenuMac() => Debug.Log(BuildMac());
    [MenuItem("Tools/Frostbound/Release/Linux")] public static void MenuLinux() => Debug.Log(BuildLinux());

    public static string BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows", GameName + ".exe");
    public static string BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS", GameName + ".app");
    public static string BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Linux", GameName + ".x86_64");

    // Build de desarrollo: trae los contadores del Profiler (UIPerfProbe) y la autoprueba (BuildAutoTest).
    [MenuItem("Tools/Frostbound/Release/Windows (desarrollo)")] public static void MenuWindowsDev() => Debug.Log(BuildWindowsDev());
    public static string BuildWindowsDev() => Build(BuildTarget.StandaloneWindows64, "Windows_Dev", GameName + ".exe", BuildOptions.Development);

    // Autoprueba en ventana a cada resolución: escribe Builds/autotest/<res>/informe.txt y capturas, y se cierra sola.
    public static string AutoTest1080() => AutoTest(1920, 1080);
    // Ultrawide en un monitor 16:9: la ventana no puede ser más grande que la pantalla, así que se prueba la
    // proporción (lo que importa para la UI) con el mismo ancho.
    public static string AutoTest21x9() => AutoTest(1920, 822);
    public static string AutoTest32x9() => AutoTest(1920, 540);

    private static string AutoTest(int w, int h)
    {
        string exe = Path.Combine(Root, "Windows_Dev", GameName + ".exe");
        if (!File.Exists(exe)) return "no existe " + exe + " (ejecuta BuildWindowsDev)";
        string dir = Path.Combine(Root, "autotest", w + "x" + h);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        string args = "-autotest \"" + dir + "\" -screen-width " + w + " -screen-height " + h + " -screen-fullscreen 0 -logFile \"" + Path.Combine(Root, "autotest", w + "x" + h + ".log") + "\"";
        Directory.CreateDirectory(Path.Combine(Root, "autotest"));
        var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, args) { UseShellExecute = false });
        return "autoprueba " + w + "×" + h + " lanzada pid=" + p.Id;
    }

    public static string AutoTestReport()
    {
        string root = Path.Combine(Root, "autotest");
        if (!Directory.Exists(root)) return "sin autopruebas";
        var sb = new StringBuilder();
        foreach (string dir in Directory.GetDirectories(root))
        {
            string file = Path.Combine(dir, "informe.txt");
            sb.Append("== ").Append(Path.GetFileName(dir)).Append('\n').Append(File.Exists(file) ? File.ReadAllText(file) : "(sin informe)\n");
        }
        return sb.ToString();
    }

    private static string Build(BuildTarget target, string folder, string file, BuildOptions extra = BuildOptions.None)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            return folder + ": falta el módulo de compilación (instálalo desde Unity Hub → Installs → Add modules)";
        string dir = Path.Combine(Root, folder);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        var options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = Path.Combine(dir, file),
            target = target,
            targetGroup = BuildTargetGroup.Standalone,
            options = extra
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        var sb = new StringBuilder(folder + ": " + s.result + " · " + (s.totalSize / (1024f * 1024f)).ToString("0.0") + " MB · " + s.totalTime.TotalSeconds.ToString("0") + " s · errores=" + s.totalErrors);
        if (s.result != BuildResult.Succeeded)
            foreach (BuildStep step in report.steps)
                foreach (BuildStepMessage m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) sb.Append(" | ").Append(m.content.Split('\n')[0]);
        File.WriteAllText(Path.Combine(Root, "ultimo_" + folder + ".txt"), sb.ToString());
        return sb.ToString();
    }

    // Prueba de humo: abre el ejecutable de Windows en modo sin gráficos y guarda su registro.
    private static System.Diagnostics.Process _run;

    public static string SmokeRunWindows()
    {
        string exe = Path.Combine(Root, "Windows", GameName + ".exe");
        if (!File.Exists(exe)) return "no existe " + exe;
        string log = Path.Combine(Root, "Windows_prueba.log");
        if (File.Exists(log)) File.Delete(log);
        _run = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "-batchmode -nographics -logFile \"" + log + "\"") { UseShellExecute = false });
        return "lanzado pid=" + _run.Id;
    }

    public static string SmokeStop()
    {
        if (_run == null) return "no hay prueba en marcha";
        bool alive = !_run.HasExited;
        if (alive) _run.Kill();
        string r = alive ? "seguía funcionando (cerrado)" : "se cerró solo, código " + _run.ExitCode;
        _run = null;
        return r;
    }

    // Nombre del juego en la ventana, en el ejecutable y en la carpeta de partidas guardadas.
    public static string SetIdentity()
    {
        PlayerSettings.productName = GameName;
        AssetDatabase.SaveAssets();
        return "producto=" + PlayerSettings.productName + " compañía=" + PlayerSettings.companyName + " versión=" + PlayerSettings.bundleVersion;
    }

    public static string SetVersion011() { PlayerSettings.bundleVersion = "0.1.1"; AssetDatabase.SaveAssets(); return "versión=" + PlayerSettings.bundleVersion; }
    public static string SetVersion013() { PlayerSettings.bundleVersion = "0.1.3"; AssetDatabase.SaveAssets(); return "versión=" + PlayerSettings.bundleVersion; }
    public static string SetVersion012() { PlayerSettings.bundleVersion = "0.1.2"; AssetDatabase.SaveAssets(); return "versión=" + PlayerSettings.bundleVersion; }

    public static string UiCheck()
    {
        var sb = new StringBuilder("pantalla=" + Screen.width + "x" + Screen.height);
        foreach (var s in Object.FindObjectsByType<UnityEngine.UI.CanvasScaler>(FindObjectsInactive.Include))
            sb.Append(" | ").Append(s.name).Append(" match=").Append(s.matchWidthOrHeight.ToString("0.#"));
        sb.Append(" | marcos=").Append(UltrawideFrame.All.Count).Append(" resoluciones=").Append(DisplaySettings.Resolutions().Count);
        return sb.ToString();
    }
}
