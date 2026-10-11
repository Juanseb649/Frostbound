using UnityEngine;

// Aplica al jugador lo elegido en la selección de clase (clase, plumaje y nombre).
// Corre antes que PenguinOutfit para que la ropa de la clase elegida se equipe al iniciar.
[DefaultExecutionOrder(-50)]
public class PlayerLoadout : MonoBehaviour
{
    public PlumagePalette palette;
    [Tooltip("Nombre si se prueba la escena sin pasar por el menú.")]
    public string fallbackName = "Pingüino";

    // Jugador 2: su propia clase, plumaje y nombre (los pone CoopManager antes de activarlo).
    [System.NonSerialized] public CharacterClass overrideClass;
    [System.NonSerialized] public string overridePlumage;
    [System.NonSerialized] public string overrideName;

    private bool IsSecond => overrideClass != null;

    void Awake()
    {
        GameSession s = GameSession.Instance;
        CharacterClass cls = IsSecond ? overrideClass : s != null ? s.SelectedClass : null;
        if (cls == null) return;
        CharacterStats stats = GetComponent<CharacterStats>();
        if (stats != null) stats.InitFromClass(cls);
    }

    void Start()
    {
        if (IsSecond)
        {
            gameObject.name = "Player2";
            NameTag t2 = GetComponent<NameTag>();
            if (t2 == null) t2 = gameObject.AddComponent<NameTag>();
            t2.isPlayer = true;
            t2.SetName(string.IsNullOrWhiteSpace(overrideName) ? overrideClass.displayName : overrideName.Trim(), "Jugador 2");
            PenguinAppearance look2 = GetComponentInChildren<PenguinAppearance>();
            if (look2 != null && palette != null && !string.IsNullOrEmpty(overridePlumage)) look2.SetPlumage(palette.ColorOf(overridePlumage));
            return;
        }
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
