using System;
using System.Collections.Generic;
using UnityEngine;

// Agrupa piezas (casco, torso, pies...) y da bonos al llevar varias a la vez.
// Las piezas siguen siendo independientes: se pueden mezclar con las de otros sets.
[CreateAssetMenu(menuName = "Frostbound/Set de armadura", fileName = "NuevoSet")]
public class ArmorSet : ScriptableObject
{
    [Serializable]
    public class Bonus
    {
        [Min(1)] public int piecesRequired = 2;
        public List<StatModifier> stats = new List<StatModifier>();
        [Min(0)] public int armor;
    }

    public string displayName = "Set";
    public List<ItemDefinition> pieces = new List<ItemDefinition>();
    public List<Bonus> bonuses = new List<Bonus>();

    public int PieceCount => pieces.Count;

    public static string Describe(Bonus b)
    {
        var parts = new List<string>();
        foreach (StatModifier m in b.stats) parts.Add("+" + m.value + " " + StatBlock.DisplayName(m.stat));
        if (b.armor > 0) parts.Add("+" + b.armor + " Armadura");
        return string.Join(", ", parts);
    }
}
