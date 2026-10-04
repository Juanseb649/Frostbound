using System.Collections.Generic;
using UnityEngine;

// Referencias que el juego necesita en cualquier escena (cargar partidas, menús creados por código).
// Vive en Resources/GameDatabase.asset.
[CreateAssetMenu(menuName = "Frostbound/Game Database", fileName = "GameDatabase")]
public class GameDatabase : ScriptableObject
{
    public List<CharacterClass> classes = new List<CharacterClass>();
    public ItemDatabase items;
    public PlumagePalette palette;
    public UISkin uiSkin;

    private static GameDatabase _instance;

    public static GameDatabase Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<GameDatabase>("GameDatabase");
            return _instance;
        }
    }

    public CharacterClass FindClass(string id)
    {
        foreach (CharacterClass c in classes)
            if (c != null && c.name == id) return c;
        return null;
    }
}
