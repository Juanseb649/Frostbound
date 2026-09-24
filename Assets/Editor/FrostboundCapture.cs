using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Renderiza vistas de prueba a PNG en la carpeta Captures/ del proyecto (fuera de Assets).
public static class FrostboundCapture
{
    [MenuItem("Tools/Frostbound/Debug/Capturar vestuario")]
    public static void CaptureWardrobe()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SetupPenguinWardrobe.PrefabPath);
        if (prefab == null) return;
        string[] items = { null, "Casco_Cuernos", "Sombrero_Mago", "Armadura", "Traje_Capucha" };
        var temp = new List<GameObject>();
        Vector3 origin = new Vector3(1000f, 0f, 1000f);

        for (int i = 0; i < items.Length; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = origin + new Vector3((i - 2) * 1.3f, 0f, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 180f - 20f, 0f);
            PenguinOutfit outfit = go.GetComponent<PenguinOutfit>();
            outfit.CacheBones();
            if (items[i] != null) outfit.Equip(SetupPenguinWardrobe.LoadItem(items[i]));
            temp.Add(go);
        }

        var lightGo = new GameObject("CaptureLight");
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.2f;
        l.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
        temp.Add(lightGo);

        Render(origin + new Vector3(0f, 1.6f, -7f), origin + new Vector3(0f, 0.6f, 0f), 30f, "wardrobe_front.png", new Color(0.8f, 0.87f, 0.95f));
        foreach (GameObject g in temp) g.transform.Rotate(0f, 180f, 0f);
        Render(origin + new Vector3(0f, 1.6f, -7f), origin + new Vector3(0f, 0.6f, 0f), 30f, "wardrobe_back.png", new Color(0.8f, 0.87f, 0.95f));
        foreach (GameObject g in temp) Object.DestroyImmediate(g);
    }

    [MenuItem("Tools/Frostbound/Debug/Capturar campamento")]
    public static void CaptureCamp()
    {
        Render(new Vector3(0f, 9f, -14.5f), new Vector3(0f, 1f, -5.5f), 50f, "camp_player.png", Color.black, true);
        Render(new Vector3(18f, 26f, -30f), new Vector3(0f, 0f, 2f), 45f, "camp_overview.png", Color.black, true);
    }

    private static void Render(Vector3 pos, Vector3 target, float fov, string file, Color bg, bool skybox = false)
    {
        var camGo = new GameObject("CaptureCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.transform.position = pos;
        cam.transform.LookAt(target);
        cam.fieldOfView = fov;
        cam.clearFlags = skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        cam.backgroundColor = bg;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 500f;

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

        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Captures");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
        Debug.Log("[Capture] " + Path.Combine(dir, file));

        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
    }
}
