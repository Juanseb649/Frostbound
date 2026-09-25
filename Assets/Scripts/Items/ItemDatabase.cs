using System.Collections.Generic;
using UnityEngine;

// Lista de todos los objetos del juego: sirve para guardar por id y para el loot aleatorio.
[CreateAssetMenu(menuName = "Frostbound/Base de datos de objetos", fileName = "ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemDefinition> items = new List<ItemDefinition>();

    public ItemDefinition Find(string id)
    {
        foreach (ItemDefinition item in items)
            if (item != null && item.id == id) return item;
        return null;
    }

    public ItemDefinition Random()
    {
        if (items.Count == 0) return null;
        return items[UnityEngine.Random.Range(0, items.Count)];
    }
}
