using System.Collections;
using UnityEngine;

// Jefe del capítulo I en la explanada de la Escalinata: Rocco Vendaval, el mago corrupto.
// Aparece si aún no ha caído; al vencerlo termina el capítulo y despiertan los chamanes corruptos.
public class RoccoEncounter : MonoBehaviour
{
    public static readonly string[] DyingLines =
    {
        "Espera... no me remates. Escúchame, por favor.",
        "Bobby... Bobby era mi amigo. Fue él quien liberó el Frost.",
        "Yo le ayudé a romper el sello. No tenía elección.",
        "Tiene a mi hermanito Pipo. Si no le ayudaba, Pipo sería el siguiente en congelarse.",
        "Sube, héroe... encuentra a Bobby. Salva a Pipo..."
    };

    public BossController Boss { get; private set; }
    public bool Defeated => QuestLog.Stage(QuestLog.Ascent) >= QuestLog.AscentRocco;

    void Start()
    {
        if (!Defeated) Spawn();
    }

    public void Spawn()
    {
        GameDatabase db = GameDatabase.Instance;
        EnemyDefinition mage = db != null ? db.FindEnemy("Corrupt_Mage") : null;
        if (mage == null || mage.prefab == null) return;
        GameObject go = Instantiate(mage.prefab, transform.position, Quaternion.Euler(0f, 0f, 0f), transform);
        go.name = "Rocco_Vendaval";
        EnemyBrain brain = go.GetComponent<EnemyBrain>();
        brain.Setup(mage, null, BossTuning.RoccoLevel, false, RoccoBoss.Plumage);
        Boss = BossController.Make(brain, "Rocco Vendaval", "Mago corrupto del Frostspire", BossTuning.RoccoHealth, BossTuning.RoccoDamage, 1.7f, def =>
        {
            def.displayName = "Rocco Vendaval";
            def.sightRange = 20f;
            def.experience = 400;
            def.lootPicks = 5;
        });
        Boss.dyingLines = DyingLines;
        Boss.speechAt = 0.08f;
        go.AddComponent<RoccoBoss>();
        Boss.Defeated += OnDefeated;
    }

    private void OnDefeated(BossController b)
    {
        QuestLog.SetStage(QuestLog.Ascent, QuestLog.AscentRocco);
        StartCoroutine(Aftermath());
    }

    private IEnumerator Aftermath()
    {
        yield return new WaitForSeconds(1.2f);
        BonfireBanner.Show("CAPÍTULO I COMPLETADO", BonfireBanner.Victory);
        yield return new WaitForSeconds(2.5f);
        Notifications.Show("Con Rocco caído, chamanes corruptos despiertan en la montaña: invocan gólems y reviven a los caídos.", FrostboundUI.Gold);
    }
}
