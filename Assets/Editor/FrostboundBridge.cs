using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Canal de comandos para automatizar el editor desde fuera (una línea por comando en FrostboundBridge/inbox.txt).
// Comandos: refresh | menu <ruta> | open <escena> | play | stop | wait <s> | exec <Tipo.Metodo>
//           shot <nombre> | camshot <nombre> px py pz tx ty tz [fov] | player x y z | save
// Solo se activa con el símbolo FROSTBOUND_BRIDGE (Tools > Frostbound > Debug > Bridge de automatización).
#if FROSTBOUND_BRIDGE
[InitializeOnLoad]
#endif
public static class FrostboundBridge
{
    public static bool Active { get; private set; }

    private static readonly string Dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "FrostboundBridge");
    private static string Inbox => Path.Combine(Dir, "inbox.txt");
    private static string Outbox => Path.Combine(Dir, "outbox.txt");
    private static string LogFile => Path.Combine(Dir, "log.txt");
    private static double _nextTick;
    private static double _waitUntil;

#if FROSTBOUND_BRIDGE
    static FrostboundBridge()
    {
        Directory.CreateDirectory(Dir);
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
        Write(Outbox, "READY " + DateTime.Now.ToString("HH:mm:ss") + (EditorApplication.isPlaying ? " (play)" : ""));
    }
#endif

    public static bool Dialog(string title, string message, string ok, string cancel = null)
    {
        if (Active)
        {
            Debug.Log("[Dialog] " + message.Replace("\n", " "));
            return true;
        }
        return cancel == null ? EditorUtility.DisplayDialog(title, message, ok) : EditorUtility.DisplayDialog(title, message, ok, cancel);
    }

    public static bool ConfirmSave()
    {
        if (!Active) return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        EditorSceneManager.SaveOpenScenes();
        return true;
    }

    private static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Log && !msg.StartsWith("[")) return;
        string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + type + ": " + msg;
        if (type == LogType.Exception || type == LogType.Error) line += "\n" + stack;
        Write(LogFile, line);
    }

    private static void Write(string path, string line)
    {
        try { File.AppendAllText(path, line + "\n"); } catch { }
    }

    private static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < _nextTick || now < _waitUntil) return;
        _nextTick = now + 0.25;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!File.Exists(Inbox)) return;

        string cmd;
        try
        {
            var lines = new List<string>(File.ReadAllLines(Inbox));
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0])) lines.RemoveAt(0);
            if (lines.Count == 0) return;
            cmd = lines[0].Trim();
            lines.RemoveAt(0);
            File.WriteAllLines(Inbox, lines);
        }
        catch { return; }

        Active = true;
        try
        {
            string result = Run(cmd);
            Write(Outbox, "OK " + cmd + (string.IsNullOrEmpty(result) ? "" : " -> " + result));
        }
        catch (Exception e)
        {
            Write(Outbox, "ERR " + cmd + ": " + (e.InnerException ?? e).Message);
        }
        finally { Active = false; }
    }

    private static string Run(string cmd)
    {
        int sp = cmd.IndexOf(' ');
        string op = sp < 0 ? cmd : cmd.Substring(0, sp);
        string arg = sp < 0 ? "" : cmd.Substring(sp + 1).Trim();
        string[] a = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        switch (op)
        {
            case "refresh":
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                return "";
            case "menu":
                if (!EditorApplication.ExecuteMenuItem(arg)) throw new Exception("menú no encontrado");
                return "";
            case "open":
                EditorSceneManager.OpenScene(arg, OpenSceneMode.Single);
                return "";
            case "save":
                EditorSceneManager.SaveOpenScenes();
                return "";
            case "play":
                PlayerSettings.runInBackground = true;
                EditorApplication.isPlaying = true;
                return "";
            case "stop":
                EditorApplication.isPlaying = false;
                return "";
            case "wait":
                _waitUntil = EditorApplication.timeSinceStartup + float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture);
                return "";
            case "exec":
                return Exec(arg);
            case "shot":
                string path = Path.Combine(Dir, arg + ".png");
                ScreenCapture.CaptureScreenshot(path);
                return path;
            case "camshot":
                return CamShot(a[0], V(a, 1), V(a, 4), a.Length > 7 ? F(a[7]) : 50f);
            case "player":
                GameObject p = GameObject.Find("Player");
                if (p == null) throw new Exception("no hay Player");
                Rigidbody rb = p.GetComponent<Rigidbody>();
                if (rb != null) rb.position = V(a, 0);
                p.transform.position = V(a, 0);
                return "";
            default:
                throw new Exception("comando desconocido");
        }
    }

    private static string Exec(string target)
    {
        int dot = target.LastIndexOf('.');
        string typeName = target.Substring(0, dot);
        string method = target.Substring(dot + 1);
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(typeName);
            if (t == null) continue;
            MethodInfo m = t.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (m == null) throw new Exception("método no encontrado");
            object r = m.Invoke(null, null);
            return r != null ? r.ToString() : "";
        }
        throw new Exception("tipo no encontrado");
    }

    private static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    private static Vector3 V(string[] a, int i) => new Vector3(F(a[i]), F(a[i + 1]), F(a[i + 2]));

    public static string CamShot(string name, Vector3 pos, Vector3 target, float fov)
    {
        var camGo = new GameObject("BridgeCam") { hideFlags = HideFlags.HideAndDontSave };
        Camera cam = camGo.AddComponent<Camera>();
        cam.transform.position = pos;
        cam.transform.LookAt(target);
        cam.fieldOfView = fov;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 600f;

        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        var req = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
        else cam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        string file = Path.Combine(Dir, name + ".png");
        File.WriteAllBytes(file, tex.EncodeToPNG());
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(camGo);
        return file;
    }
}
