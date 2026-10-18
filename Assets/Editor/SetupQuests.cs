using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Crea las misiones de la campaña (Docs/Campana_Frostbound.md) como assets en Assets/Resources/Quests.
// "Crear misiones" solo crea las que faltan (lo editado a mano en el inspector se respeta);
// "Recrear misiones" las sobrescribe con los textos de aquí. Se puede ejecutar varias veces.
public static class SetupQuests
{
    private const string Root = "Assets/Resources/Quests";

    [MenuItem("Tools/Frostbound/Misiones/Crear misiones")]
    public static void Menu() => Debug.Log(Run(false));

    [MenuItem("Tools/Frostbound/Misiones/Recrear misiones (sobrescribe)")]
    public static void MenuForce() => Debug.Log(Run(true));

    public static string Create() => Run(false);
    public static string Recreate() => Run(true);

    private static string Run(bool overwrite)
    {
        Directory.CreateDirectory(Root);
        int created = 0, updated = 0, kept = 0;
        foreach (QuestDefinition src in Campaign())
        {
            string path = Root + "/" + src.id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (existing == null)
            {
                src.name = src.id;
                AssetDatabase.CreateAsset(src, path);
                created++;
            }
            else if (overwrite)
            {
                EditorUtility.CopySerialized(src, existing);
                existing.name = src.id;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(src);
                updated++;
            }
            else
            {
                Object.DestroyImmediate(src);
                kept++;
            }
        }
        AssetDatabase.SaveAssets();
        return "misiones: creadas=" + created + " recreadas=" + updated + " sin tocar=" + kept;
    }

    // ---------- Datos ----------

    private static QuestDefinition Q(string id, string title, int chapter, int order, string giver, string speaker,
        string requires, int requiresStage, int xp, int coins, string[] offer, params QuestStage[] stages)
    {
        var q = ScriptableObject.CreateInstance<QuestDefinition>();
        q.id = id; q.title = title; q.chapter = chapter; q.order = order;
        q.giver = giver; q.speaker = speaker;
        q.requiresQuest = requires; q.requiresStage = requiresStage;
        q.experience = xp; q.coins = coins;
        q.offerLines = offer ?? new string[0];
        q.stages = new List<QuestStage>(stages);
        return q;
    }

    private static QuestStage S(string objective, QuestObjective type, string target, int count, string onReach, params string[] lines) =>
        new QuestStage { objective = objective, type = type, target = target, count = count, onReach = onReach, giverLines = lines };

    private static QuestStage Done(params string[] lines) => S("", QuestObjective.Ninguno, "", 1, "", lines);

    private static IEnumerable<QuestDefinition> Campaign()
    {
        // ===== Capítulo I (las etapas las avanza el código de cada zona) =====
        yield return Q(QuestLog.Citadel, "La ciudadela caída", 1, 1, "steve", "", "", 0, 0, 0,
            new[]
            {
                "Eh, tú. Sí, tú, el de la espada. Desde mi claro se ve el castillo... y no me gusta lo que veo.",
                "Al pie de sus murallas había un poblado: la ciudadela. Gente buena, guardias valientes.",
                "Hace unas lunas bajó de la montaña una tropa de pingüinos de élite. Malditos, con los ojos como cristales.",
                "Los mandaba un ninja. Dicen que corrompió a los guardias y que ellos mismos atacaron su poblado.",
                "Su capitán, Gorvald, sigue rondando la plaza. Si alguien puede romper ese sello del castillo... eres tú."
            },
            S("Limpia el poblado al pie del castillo de corruptos", QuestObjective.Ninguno, "", 1, "",
                "La ciudadela está al pie del castillo. Ten cuidado con la plaza: ahí se juntan.",
                "Si ves a Gorvald, no dejes que te alcance su hacha. Dicen que ahora gotea veneno."),
            S("Entra al castillo y derrota al ninja maldito", QuestObjective.Ninguno, "", 1, "",
                "¿Gorvald habló antes de caer? Entonces es verdad: el ninja está dentro del castillo.",
                "El sello se rompió. Lo que haya ahí dentro ya sabe que vas."),
            Done("La ciudadela ya respira. Pero lo que la atacó bajó de la montaña."));

        yield return Q(QuestLog.Ascent, "El ascenso", 1, 2, "steve", "", QuestLog.Citadel, QuestLog.CitadelDone, 0, 0,
            new[]
            {
                "Lo hiciste. La ciudadela ya respira... pero esto no ha terminado.",
                "Esa tropa de élite no salió de la nada: bajó de la montaña. Del Frostspire.",
                "Allí arriba algo late bajo el hielo. Lo siento en el suelo cada noche.",
                "Los que suben y vuelven ya no son pingüinos: son cascarones de cristal.",
                "Toma el sendero del norte, al final del camino. Lleva a Las Faldas.",
                "Enciende las hogueras que encuentres. Y héroe... no mires mucho rato al cristal."
            },
            S("Toma el sendero del norte hacia la montaña", QuestObjective.Ninguno, "", 1, "",
                "El sendero de la montaña empieza al final del camino del norte, pasado el bosque.",
                "En Las Faldas la ventisca esconde a los corruptos. Avanza de hoguera en hoguera."),
            S("Asciende por Las Faldas y vence a los guardianes del paso helado", QuestObjective.Ninguno, "", 1, "",
                "¿Ya pisaste Las Faldas? Sigue subiendo: el paso helado está en lo alto del sendero.",
                "Si la montaña late más fuerte, es que vas por buen camino."),
            S("Sube la Escalinata hasta la cima del Frostspire", QuestObjective.Ninguno, "", 1, "",
                "¿Llegaste al paso helado? Más allá está el Bosque Corrupto... nadie ha vuelto de ahí.",
                "Descansa junto al fuego, héroe. La montaña seguirá latiendo mañana."),
            Done("¿Rocco habló de Bobby? Lo conocí de pequeño: nunca le hizo daño a nadie.",
                "Si tiene a Pipo, el rastro irá hacia el Bosque Corrupto. Pasa la Escalinata y sigue el camino."));

        // ===== Capítulo II · El Bosque Corrupto (empiezan solas) =====
        yield return Q("rastro_de_pipo", "El rastro de Pipo", 2, 1, "", "steve", QuestLog.Ascent, QuestLog.AscentRocco, 300, 120, null,
            S("Cruza el Camino del Bosque Corrupto", QuestObjective.Llegar, "bosque_entrada", 1, "",
                "Pasada la Escalinata está el camino del bosque. Los pinos negros no dejan ver el cielo.",
                "Pipo es pequeño y curioso: si pasó por ahí, habrá dejado huellas."),
            S("Sigue el rastro de Pipo por el bosque", QuestObjective.Interactuar, "pista", 3, "Has llegado al Bosque Corrupto",
                "Huellas, una bufanda, dibujos en la nieve... Pipo sabe que alguien lo busca.",
                "No te separes del rastro. El bosque cambia de sitio cuando no lo miras."),
            Done("Si las pistas se hunden en la savia, Pipo está en el corazón del bosque."));

        yield return Q("savia_negra", "Savia negra", 2, 2, "", "steve", "rastro_de_pipo", 3, 350, 150, null,
            S("Destruye las raíces corruptas que alimentan al Frost", QuestObjective.Interactuar, "raiz", 3, "Las pistas llevan a la savia",
                "La savia de esos pinos sube por las raíces hasta el hielo. Córtalas y el bosque respirará.",
                "Dicen que una chamana canta junto a las raíces. La llaman la Madre Savia."),
            Done("Sin raíces, la savia se seca. Lo que la cuidaba vendrá a buscarte."));

        yield return Q("la_madre_savia", "La Madre Savia", 2, 3, "", "steve", "savia_negra", 2, 600, 300, null,
            S("Encuentra a la Madre Savia en el corazón del bosque", QuestObjective.Llegar, "claro_savia", 1, "Las raíces se secan: algo despierta en el corazón del bosque",
                "El corazón del bosque es un claro donde la savia hierve. Ve con la poción a mano."),
            S("Derrota a la Madre Savia", QuestObjective.Jefe, "Madre Savia", 1, "",
                "Su savia envenena. No te quedes en los charcos."),
            S("Libera a Pipo del capullo de hielo", QuestObjective.Interactuar, "capullo_pipo", 1, "La Madre Savia cayó. Busca a Pipo",
                "Si Pipo está vivo, estará cerca de ella. Rompe ese hielo con cuidado."),
            S("Vuelve al poblado y habla con Steve", QuestObjective.Hablar, "steve", 1, "Pipo está a salvo: llévalo con Steve",
                "¿Pipo? ¡Pipo! Ven aquí, pequeño. Te llevaremos junto al fuego."),
            Done("Pipo dice que Bobby hablaba solo, con una voz que salía del hielo.",
                "Y que subió a la cumbre. Al sello. Héroe... ahí empezó todo."));

        // ===== Capítulo III · La cumbre del Frostspire =====
        yield return Q("el_ascenso_final", "La cumbre", 3, 1, "", "steve", "la_madre_savia", 5, 700, 300, null,
            S("Sube a la cumbre del Frostspire", QuestObjective.Llegar, "cumbre_entrada", 1, "",
                "La cumbre se alcanza por las grietas de hielo detrás del bosque. Abrígate."),
            S("Enciende los tres altares del sello", QuestObjective.Interactuar, "altar", 3, "Has llegado a la cumbre del Frostspire",
                "Los antiguos encendían tres altares para que el sello aguantara. Enciéndelos y verás el camino."),
            Done("Los altares arden. La cámara del sello está abierta."));

        yield return Q("el_sello", "El sello", 3, 2, "", "steve", "el_ascenso_final", 3, 1500, 800, null,
            S("Entra a la cámara del sello", QuestObjective.Llegar, "camara_sello", 1, "",
                "Bobby estará donde estaba el sello. No dejes que te hable mucho rato."),
            S("Detén a Bobby", QuestObjective.Jefe, "Bobby", 1, "",
                "Bobby no es malo. Algo habla por él. Si puedes, no lo mates."),
            S("Destruye el Corazón del Frost", QuestObjective.Jefe, "Corazón del Frost", 1, "Bobby se rinde... y el Corazón despierta",
                "Lo que late bajo la montaña está despierto. Rompe lo que lo alimenta."),
            S("Rehaz el sello con Bobby", QuestObjective.Interactuar, "sello", 1, "El Corazón del Frost se apaga",
                "Los fragmentos de Gorvald, el ninja y Rocco: con ellos se hizo el sello la primera vez."),
            Done("El sello aguanta. La ventisca para por primera vez en lunas."));

        yield return Q("epilogo", "De vuelta a casa", 3, 3, "", "steve", "el_sello", 5, 0, 0, null,
            S("Vuelve al poblado y habla con Steve", QuestObjective.Hablar, "steve", 1, "El sello vuelve a estar en pie",
                "Te estábamos esperando, héroe."),
            Done("Pipo no para de contar tu historia. Bobby se quedó a ayudar a reconstruir la ciudadela.",
                "Gracias, héroe. La montaña ya no late. Descansa: te lo has ganado."));
    }
}
