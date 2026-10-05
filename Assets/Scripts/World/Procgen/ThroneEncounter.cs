using System.Collections.Generic;
using UnityEngine;

// Sala del trono: el ninja maldito y su escolta de élite (una de cada clase maldita).
// La pelea empieza cuando el héroe entra en la sala; si el héroe cae, todos vuelven a su sitio.
public class ThroneEncounter : MonoBehaviour
{
    public static readonly string[] NinjaLines =
    {
        "Cruzaste mi ciudadela... y aún respiras. Gorvald se ablandó.",
        "...El Frost me prometió la cima. Me prometió no volver a sentir frío.",
        "Escucha, héroe: no soy el último. Lo que late bajo la montaña... ya te ha visto."
    };

    private BossController _boss;
    private readonly List<EnemyBrain> _escort = new List<EnemyBrain>();
    private Vector3 _center;
    private float _radius;
    private bool _started;
    private Transform _player;

    public BossController Boss => _boss;
    public bool Started => _started;

    public void Setup(CastleInterior interior, Vector3 center, float radius, DeterministicRng rng, int level, Transform parent)
    {
        _center = center;
        _radius = radius;
        EnemyDefinition ninja = EnemySpawner.Find("Corrupt_Ninja");
        Vector3 seat = center + new Vector3(0f, 0f, radius * 0.55f);
        EnemyBrain b = EnemySpawner.Spawn(ninja, seat, 180f, parent, level + 2, false, rng);
        if (b != null)
        {
            _boss = BossController.Make(b, "Kagero, el Ninja Maldito", "Comandante de la tropa de élite", 5f, 1.15f, 1.3f, def =>
            {
                def.moveSpeed += 0.8f;
                def.cooldown = 1.5f;
                def.windup = 0.3f;
                def.sightRange = 26f;
                def.lootPicks = 6;
            });
            _boss.blinkInterval = 6.5f;
            _boss.dyingLines = NinjaLines;
            _boss.enchantColor = new Color(0.55f, 0.45f, 1f);
            _boss.Defeated += OnDefeated;
        }
        string[] ids = { "Corrupt_Knight", "Corrupt_Mage", "Corrupt_Viking", "Corrupt_Ninja" };
        for (int i = 0; i < ids.Length; i++)
        {
            float a = (i / 4f) * Mathf.PI * 2f + Mathf.PI * 0.25f;
            Vector3 p = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius * 0.45f;
            EnemyBrain e = EnemySpawner.Spawn(EnemySpawner.Find(ids[i]), p, 180f, parent, level, false, rng);
            if (e == null) continue;
            e.leashOverride = 40f;
            e.Slot = i;
            _escort.Add(e);
        }
        if (_boss != null) _boss.Brain.paused = true;
        foreach (EnemyBrain e in _escort) e.paused = true;
    }

    void Update()
    {
        if (_boss == null) return;
        if (_player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            _player = p.transform;
        }
        float d = (_player.position - _center).magnitude;
        if (!_started && d < _radius * 0.8f) Begin();
        if (_started && d > _radius + 30f) Reset();
    }

    private void Begin()
    {
        _started = true;
        BonfireBanner.Show("KAGERO, EL NINJA MALDITO", BonfireBanner.Threat);
        _boss.Brain.paused = false;
        _boss.Brain.Alert();
        foreach (EnemyBrain e in _escort)
            if (e != null && !e.IsDead)
            {
                e.paused = false;
                e.Alert();
            }
    }

    private void Reset()
    {
        _started = false;
        if (_boss != null && !_boss.Health.IsDead && !_boss.Speaking)
        {
            _boss.Brain.GoHome();
            _boss.Health.ResetHealth();
        }
        foreach (EnemyBrain e in _escort) if (e != null && !e.IsDead) e.GoHome();
    }

    private void OnDefeated(BossController b)
    {
        QuestLog.SetStage(QuestLog.Citadel, QuestLog.CitadelDone);
        BonfireBanner.Show("GRAN ENEMIGO DERRIBADO", BonfireBanner.Victory);
        Notifications.Show("Misión completada: La ciudadela caída", FrostboundUI.Gold);
        _boss = null;
        enabled = false;
    }
}
