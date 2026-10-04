using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Solo en el editor y en builds de desarrollo. Para probar la ropa en Play: teclas 1-9 ponen/quitan cada prenda de la lista, 0 quita todo.
public class OutfitTester : MonoBehaviour
{
    public PenguinOutfit outfit;
    public List<OutfitItem> items = new List<OutfitItem>();

    void Awake()
    {
        if (outfit == null) outfit = GetComponentInChildren<PenguinOutfit>();
        if (!Application.isEditor && !Debug.isDebugBuild) enabled = false;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || outfit == null) return;
        if (kb.digit0Key.wasPressedThisFrame) outfit.UnequipAll();
        for (int i = 0; i < items.Count && i < 9; i++)
        {
            if (kb[Key.Digit1 + i].wasPressedThisFrame) outfit.Toggle(items[i]);
        }
    }
}
