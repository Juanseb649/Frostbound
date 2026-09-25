using UnityEngine;

// Aplica al jugador lo elegido en la selección de clase (clase, plumaje y nombre).
// Corre antes que PenguinOutfit para que la ropa de la clase elegida se equipe al iniciar.
[DefaultExecutionOrder(-50)]
public class PlayerLoadout : MonoBehaviour
{
    public PlumagePalette palette;
    [Tooltip("Nombre si se prueba la escena sin pasar por el menú.")]
    public string fallbackName = "Pingüino";

    void Awake()
    {
        GameSession s = GameSession.Instance;
        if (s == null || s.SelectedClass == null) return;
        CharacterStats stats = GetComponent<CharacterStats>();
        if (stats != null) stats.InitFromClass(s.SelectedClass);
    }

    void Start()
    {
        gameObject.name = "Player";
        NameTag tag = GetComponent<NameTag>();
        if (tag == null) tag = gameObject.AddComponent<NameTag>();
        tag.isPlayer = true;
        tag.SetName(HeroName(), "");

        GameSession s = GameSession.Instance;
        if (s == null || s.SelectedClass == null) return;
        PenguinAppearance look = GetComponentInChildren<PenguinAppearance>();
        if (look != null && palette != null) look.SetPlumage(palette.ColorOf(s.PlumageId));
    }

    private string HeroName()
    {
        GameSession s = GameSession.Instance;
        if (s != null && !string.IsNullOrWhiteSpace(s.HeroName)) return s.HeroName.Trim();
        if (s != null && s.SelectedClass != null && !string.IsNullOrEmpty(s.SelectedClass.displayName)) return s.SelectedClass.displayName;
        return fallbackName;
    }
}
