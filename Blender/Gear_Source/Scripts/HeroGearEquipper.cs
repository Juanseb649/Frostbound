using System;
using System.Collections.Generic;
using UnityEngine;

public class HeroGearEquipper : MonoBehaviour
{
    [Serializable]
    public class ClassGear
    {
        public string className;
        public GameObject gearPrefab;
    }

    [Tooltip("Objeto que tiene la malla de Penguin.obj. El equipo se cuelga como hijo en la posición (0,0,0).")]
    public Transform penguinModel;
    public List<ClassGear> gear = new List<ClassGear>();
    public string startingClass = "Knight";

    GameObject current;

    public string CurrentClass { get; private set; }

    void Start()
    {
        if (!string.IsNullOrEmpty(startingClass))
            Equip(startingClass);
    }

    public bool Equip(string className)
    {
        var entry = gear.Find(g => string.Equals(g.className, className, StringComparison.OrdinalIgnoreCase));
        if (entry == null || entry.gearPrefab == null)
        {
            Debug.LogWarning($"[HeroGearEquipper] No hay equipo para la clase '{className}'.");
            return false;
        }

        Unequip();
        var parent = penguinModel != null ? penguinModel : transform;
        current = Instantiate(entry.gearPrefab, parent, false);
        current.name = entry.gearPrefab.name;
        current.transform.localPosition = Vector3.zero;
        current.transform.localRotation = Quaternion.identity;
        current.transform.localScale = Vector3.one;
        CurrentClass = entry.className;
        return true;
    }

    public void Unequip()
    {
        if (current != null)
            Destroy(current);
        current = null;
        CurrentClass = null;
    }

    public Transform GetPart(string partName)
    {
        return current != null ? current.transform.Find(partName) : null;
    }

    public void SetPartVisible(string partName, bool visible)
    {
        var part = GetPart(partName);
        if (part != null)
            part.gameObject.SetActive(visible);
    }
}
