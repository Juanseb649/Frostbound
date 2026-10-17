using System.IO;
using UnityEngine;

// Comprobaciones del kit de UI (FrostFrames). Por el bridge: exec UIKitChecks.DumpAtlas
public static class UIKitChecks
{
    // Guarda la textura de marcos en FrostboundBridge/ui_atlas.png y resume el alfa de la placa.
    public static string DumpAtlas()
    {
        Texture2D tex = FrostFrames.AtlasTexture;
        string file = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "FrostboundBridge", "ui_atlas.png");
        File.WriteAllBytes(file, tex.EncodeToPNG());
        Rect r = FrostFrames.Plate.textureRect;
        Color center = tex.GetPixel((int)r.center.x, (int)r.center.y);
        return file + " placa_centro=" + center;
    }
}
