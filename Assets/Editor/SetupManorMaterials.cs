using System.IO;
using UnityEditor;
using UnityEngine;

// Fase 2 del realismo de la mansión (guía /d2r-ui): prepara las texturas y el cielo de Poly Haven (CC0, en
// Assets/Frostbound/Textures) y crea los materiales que usa HauntedManor en Assets/Resources/Materials.
// - Texturas: junta color + (1 − rugosidad) en un PNG (la suavidad va en el alfa, como espera URP/Lit) y marca el
//   relieve como normal map. El material URP/Lit queda en Resources/Materials/Manor/<id>.mat.
// - Cielo: importa el HDRI como cubemap y crea Resources/Materials/CieloNoche.mat (Skybox/Cubemap).
// Las fuentes quedan fuera de Resources: solo entra en la build lo que referencian los materiales.
// Se puede ejecutar varias veces.
public static class SetupManorMaterials
{
    private const string TexRoot = "Assets/Frostbound/Textures/Manor";
    private const string SkyPath = "Assets/Frostbound/Textures/Sky/kloppenheim_02_puresky_2k.hdr";
    private const string MatRoot = "Assets/Resources/Materials/Manor";
    public const string SkyMaterialPath = "Assets/Resources/Materials/CieloNoche.mat";

    [MenuItem("Tools/Frostbound/Menús/Materiales reales de la mansión")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        Directory.CreateDirectory(MatRoot);
        var sb = new System.Text.StringBuilder();
        foreach (string dir in Directory.GetDirectories(TexRoot))
        {
            string id = Path.GetFileName(dir);
            sb.Append(BuildMaterial(id, dir.Replace('\\', '/'))).Append(" | ");
        }
        sb.Append(BuildSky());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return sb.ToString();
    }

    private static string BuildMaterial(string id, string dir)
    {
        string diffPath = dir + "/" + id + "_diff.jpg", roughPath = dir + "/" + id + "_rough.jpg", norPath = dir + "/" + id + "_nor.jpg";
        if (!File.Exists(diffPath) || !File.Exists(norPath)) return id + ": faltan texturas";

        // Relieve: normal map en espacio de tangente (convención OpenGL, la de Unity).
        var nor = (TextureImporter)AssetImporter.GetAtPath(norPath);
        if (nor.textureType != TextureImporterType.NormalMap) { nor.textureType = TextureImporterType.NormalMap; nor.SaveAndReimport(); }

        // La rugosidad es un dato, no un color: se importa lineal (si no, al leerla saldría alterada por la gamma).
        var ri = File.Exists(roughPath) ? (TextureImporter)AssetImporter.GetAtPath(roughPath) : null;
        if (ri != null && ri.sRGBTexture) { ri.sRGBTexture = false; ri.SaveAndReimport(); }

        // Color + suavidad en el alfa.
        string packedPath = dir + "/" + id + "_albedo_suavidad.png";
        Texture2D diff = ReadPixels(diffPath, true), rough = File.Exists(roughPath) ? ReadPixels(roughPath, false) : null;
        var packed = new Texture2D(diff.width, diff.height, TextureFormat.RGBA32, false);
        Color32[] c = diff.GetPixels32();
        Color32[] r = rough != null && rough.width == diff.width && rough.height == diff.height ? rough.GetPixels32() : null;
        for (int i = 0; i < c.Length; i++) c[i].a = r != null ? (byte)(255 - r[i].r) : (byte)80;
        packed.SetPixels32(c);
        packed.Apply();
        File.WriteAllBytes(packedPath, packed.EncodeToPNG());
        Object.DestroyImmediate(packed);
        Object.DestroyImmediate(diff);
        if (rough != null) Object.DestroyImmediate(rough);
        AssetDatabase.ImportAsset(packedPath);
        var pi = (TextureImporter)AssetImporter.GetAtPath(packedPath);
        pi.sRGBTexture = true;
        pi.alphaSource = TextureImporterAlphaSource.FromInput;
        pi.alphaIsTransparency = false;
        pi.maxTextureSize = 1024;
        pi.SaveAndReimport();

        string matPath = MatRoot + "/" + id + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = id };
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(norPath));
        mat.SetFloat("_BumpScale", 1f);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_SmoothnessTextureChannel", 1f);
        mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        mat.SetFloat("_Smoothness", 1f);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        return id + " ok";
    }

    // Lee una textura importada sin tocar su importador (copia sus píxeles con un blit).
    private static Texture2D ReadPixels(string path, bool srgb)
    {
        var src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, !srgb);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    private static string BuildSky()
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(SkyPath);
        if (imp == null) return "cielo: falta " + SkyPath;
        if (imp.textureShape != TextureImporterShape.TextureCube)
        {
            imp.textureShape = TextureImporterShape.TextureCube;
            imp.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }
        var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Skybox/Cubemap")) { name = "CieloNoche" };
            AssetDatabase.CreateAsset(mat, SkyMaterialPath);
        }
        mat.SetTexture("_Tex", cube);
        mat.SetFloat("_Exposure", 0.55f);
        mat.SetColor("_Tint", new Color(0.62f, 0.7f, 0.85f));
        mat.SetFloat("_Rotation", 200f);
        EditorUtility.SetDirty(mat);
        return "cielo " + (cube != null ? "ok" : "sin cubemap");
    }
}
