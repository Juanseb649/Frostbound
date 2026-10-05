using UnityEngine;

// Conversaciones de fondo entre aldeanos de la plaza. Las líneas alternan: par = quien empieza, impar = quien responde.
public static class VillageChatter
{
    public static readonly string[][] Conversations =
    {
        new[] { "¿Has visto a Pelusa? Salió a pescar hace tres días.", "No... y su iglú sigue con la puerta abierta.", "Dicen que en el bosque se oyen pasos de noche.", "Pasos y crujidos. Como hielo que se rompe." },
        new[] { "Anoche vi una luz azul en el castillo.", "Ese castillo lleva siglos en silencio.", "Pues ya no. Algo se mueve ahí arriba.", "Mejor no mirar. Ni de reojo." },
        new[] { "El pescado viene cada vez más congelado.", "El pescado siempre viene congelado.", "No así. Por dentro. Negro.", "...Ya no tengo hambre." },
        new[] { "Mi primo volvió de la montaña.", "¡Qué alivio! ¿Está bien?", "No me reconoció. Tenía los ojos como cristales.", "Que el Anciano Pingo no se entere todavía." },
        new[] { "¿Crees que el héroe llegará a la cima?", "Si no lo hace él, nadie lo hará.", "Al menos la herrera le afila bien la espada.", "Brunna afila hasta las cucharas, si la dejas." },
        new[] { "Steve sigue acampando solo en el bosque.", "Dice que desde ahí se ve mejor el castillo.", "Y que vio algo raro moviéndose cerca de sus murallas.", "Steve siempre ve cosas raras... pero ojalá se equivoque." },
        new[] { "Hace más frío que ayer.", "Hace más frío que nunca.", "Las abuelas dicen que es el Frost despertando.", "Las abuelas dicen muchas cosas... pero aciertan." },
        new[] { "¿Vas a la taberna de Mora esta noche?", "Si cierra las puertas con tranca, sí.", "Las cierra desde la semana pasada.", "Entonces cuenta conmigo." },
        new[] { "Los vigías han doblado la guardia.", "Kora dice que los corruptos no duermen.", "Nosotros tampoco, desde que empezó todo.", "Al menos la plaza sigue siendo segura." },
        new[] { "Encontré un cristal azul en la nieve.", "¡Tíralo! No lo toques más.", "Brillaba tan bonito...", "Así empezó lo de los cazadores. Tíralo." },
    };

    public static string[] Pick() => Conversations[Random.Range(0, Conversations.Length)];
}
