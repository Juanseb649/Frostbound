using System;
using System.Collections.Generic;
using UnityEngine;

// Equipa el equipo completo de una clase sobre el pingüino con esqueleto.
// Mantiene la API del paquete Frostbound_Gear (Equip, Unequip, GetPart, SetPartVisible),
// pero usa PenguinOutfit para que cada pieza siga a su hueso.
[RequireComponent(typeof(PenguinOutfit))]
public class HeroGearEquipper : MonoBehaviour
{
    public List<CharacterClass> classes = new List<CharacterClass>();
    [Tooltip("Clase que se equipa al iniciar (vacío = ninguna).")]
    public string startingClass = "";

    private PenguinOutfit _outfit;
    private OutfitItem _current;

    public string CurrentClass { get; private set; }

    void Awake()
    {
        _outfit = GetComponent<PenguinOutfit>();
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(startingClass)) Equip(startingClass);
    }

    public bool Equip(string className)
    {
        CharacterClass cls = classes.Find(c => c != null && Matches(c, className));
        if (cls == null)
        {
            Debug.LogWarning("[HeroGearEquipper] No hay equipo para la clase '" + className + "'.");
            return false;
        }
        return Equip(cls);
    }

    public bool Equip(CharacterClass cls)
    {
        if (_outfit == null) _outfit = GetComponent<PenguinOutfit>();
        if (cls == null || cls.startingOutfit == null || cls.startingOutfit.Length == 0) return false;
        Unequip();
        foreach (OutfitItem item in cls.startingOutfit) _outfit.Equip(item);
        _current = cls.startingOutfit[0];
        CurrentClass = cls.name;
        return true;
    }

    public void Unequip()
    {
        if (_outfit == null) _outfit = GetComponent<PenguinOutfit>();
        _outfit.UnequipAll();
        _current = null;
        CurrentClass = null;
    }

    public Transform GetPart(string partName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Outfit_") && t.name.EndsWith("_" + partName)) return t;
        return null;
    }

    public void SetPartVisible(string partName, bool visible)
    {
        Transform part = GetPart(partName);
        if (part != null) part.gameObject.SetActive(visible);
    }

    private static bool Matches(CharacterClass c, string key)
    {
        return string.Equals(c.name, key, StringComparison.OrdinalIgnoreCase)
            || c.name.StartsWith(key + "_", StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.className, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.displayName, key, StringComparison.OrdinalIgnoreCase);
    }
}
