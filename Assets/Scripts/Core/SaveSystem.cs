using System;
using System.IO;
using UnityEngine;

// Guarda cada partida en su propio archivo JSON (4 ranuras). La escritura es atómica: primero a .tmp y luego se reemplaza.
public static class SaveSystem
{
    public const int SlotCount = 4;

    private static string _overrideFolder;

    public static string Folder => _overrideFolder ?? Path.Combine(Application.persistentDataPath, "Saves");

    public static void UseFolderForTests(string folder) => _overrideFolder = folder;

    public static string PathOf(int slot) => Path.Combine(Folder, "partida_" + (slot + 1) + ".json");

    public static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

    public static bool Exists(int slot) => IsValidSlot(slot) && File.Exists(PathOf(slot));

    public static bool AnyExists()
    {
        for (int i = 0; i < SlotCount; i++) if (Exists(i)) return true;
        return false;
    }

    public static SaveData Load(int slot)
    {
        if (!Exists(slot)) return null;
        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathOf(slot)));
            if (data != null) data.slot = slot;
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveSystem] No se pudo leer la ranura " + (slot + 1) + ": " + e.Message);
            return null;
        }
    }

    public static bool Save(SaveData data)
    {
        if (data == null || !IsValidSlot(data.slot)) return false;
        try
        {
            Directory.CreateDirectory(Folder);
            data.version = SaveData.CurrentVersion;
            data.savedUtc = DateTime.UtcNow.ToString("o");
            if (string.IsNullOrEmpty(data.createdUtc)) data.createdUtc = data.savedUtc;
            string path = PathOf(data.slot);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveSystem] No se pudo guardar la ranura " + (data.slot + 1) + ": " + e.Message);
            return false;
        }
    }

    public static void Delete(int slot)
    {
        if (!IsValidSlot(slot)) return;
        string path = PathOf(slot);
        if (File.Exists(path)) File.Delete(path);
    }
}
