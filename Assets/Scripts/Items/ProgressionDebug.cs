using UnityEngine;
using UnityEngine.InputSystem;

// Atajos para probar la progresión mientras no hay enemigos. Solo funciona en el editor y en builds de desarrollo.
// X: +40 % de la experiencia del nivel · Mayús+X: subir un nivel · G: objeto al azar a la mochila.
[RequireComponent(typeof(CharacterStats))]
public class ProgressionDebug : MonoBehaviour
{
    public ItemDatabase database;

    private CharacterStats _stats;
    private Inventory _inventory;

    void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _inventory = GetComponent<Inventory>();
        if (!Application.isEditor && !Debug.isDebugBuild) enabled = false;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || GameplayInput.Blocked) return;
        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

        if (kb.xKey.wasPressedThisFrame && !_stats.IsMaxLevel)
        {
            int need = _stats.ExperienceToNextLevel();
            _stats.AddExperience(shift ? need - _stats.experience : Mathf.Max(1, Mathf.RoundToInt(need * 0.4f)));
        }

        if (kb.gKey.wasPressedThisFrame && database != null && _inventory != null)
        {
            ItemDefinition item = database.Random();
            if (item != null && _inventory.Add(item, item.IsStackable ? Random.Range(1, 4) : 1) > 0)
                Debug.Log("[Progresión] La mochila está llena.");
        }
    }
}
