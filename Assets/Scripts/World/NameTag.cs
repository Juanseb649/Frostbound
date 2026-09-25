using UnityEngine;

// Nombre que aparece bajo el pingüino, al estilo Club Penguin. WorldHUD lo dibuja en pantalla.
public class NameTag : MonoBehaviour
{
    public string displayName = "Pingüino";
    [Tooltip("Segunda línea más pequeña (oficio del NPC). Vacío = no se muestra.")]
    public string subtitle = "";
    public bool isPlayer;
    [Tooltip("Punto del mundo, relativo al objeto, donde se ancla el nombre (los pies).")]
    public Vector3 anchorOffset = Vector3.zero;
    [Tooltip("Separación en píxeles de referencia por debajo del ancla.")]
    public float screenOffset = 16f;

    public bool Highlighted { get; set; }
    public int Version { get; private set; }

    void OnEnable() => WorldHUD.Register(this);
    void OnDisable() => WorldHUD.Unregister(this);

    public void SetName(string newName, string newSubtitle = null)
    {
        displayName = newName;
        if (newSubtitle != null) subtitle = newSubtitle;
        Version++;
    }

    public Vector3 Anchor => transform.position + anchorOffset;
}
