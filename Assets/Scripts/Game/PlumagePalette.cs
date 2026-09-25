using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Frostbound/Plumage Palette", fileName = "PlumagePalette")]
public class PlumagePalette : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string id;
        public string displayName;
        public Color color = Color.white;
    }

    public List<Entry> entries = new List<Entry>();

    public Entry Find(string id)
    {
        if (!string.IsNullOrEmpty(id))
            foreach (Entry e in entries)
                if (string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase)) return e;
        return entries.Count > 0 ? entries[0] : null;
    }

    public int IndexOf(string id)
    {
        for (int i = 0; i < entries.Count; i++)
            if (string.Equals(entries[i].id, id, StringComparison.OrdinalIgnoreCase)) return i;
        return 0;
    }

    public Color ColorOf(string id)
    {
        Entry e = Find(id);
        return e != null ? e.color : Color.white;
    }
}
