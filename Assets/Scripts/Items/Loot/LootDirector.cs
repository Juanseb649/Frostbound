using UnityEngine;

// Punto de acceso del botín en la escena: la base de datos de objetos y el hallazgo mágico del jugador.
public class LootDirector : MonoBehaviour
{
    public static LootDirector Instance { get; private set; }

    public ItemDatabase database;

    private Equipment _playerEquipment;

    void OnEnable() => Instance = this;

    // Las zonas de la montaña no traían este objeto en la escena y sus enemigos no soltaban nada: se crea solo.
    public static LootDirector Ensure()
    {
        if (Instance != null) return Instance;
        GameDatabase db = GameDatabase.Instance;
        if (db == null || db.items == null) return null;
        var go = new GameObject("Botin");
        LootDirector d = go.AddComponent<LootDirector>();
        d.database = db.items;
        return d;
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    public float PlayerMagicFind
    {
        get
        {
            if (_playerEquipment == null)
            {
                GameObject p = GameObject.Find("Player");
                if (p != null) _playerEquipment = p.GetComponent<Equipment>();
            }
            return _playerEquipment != null ? _playerEquipment.AffixSum(AffixKind.MagicFind) : 0f;
        }
    }
}
