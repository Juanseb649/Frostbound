using UnityEngine;

// Dónde está la vaina en la espalda (espacio del pingüino en reposo): boca y punta. La genera el editor.
public class SheathInfo : ScriptableObject
{
    public Vector3 mouth;
    public Vector3 tip;
    public string partName = "Saya";

    public static SheathInfo Ninja => Resources.Load<SheathInfo>("Sheath_Ninja");
}
