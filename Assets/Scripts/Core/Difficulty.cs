using UnityEngine;

// Dificultad de la partida: se elige al crear el héroe y se guarda en SaveData.difficulty, así que dura toda la
// partida y sobrevive a los cambios de zona. Todos los números en un solo sitio.
// No es solo vida y daño: cambia lo rápido que reaccionan los corruptos, cuánto avisan antes de golpear, cada cuánto
// atacan, cuántos rodean al héroe a la vez, hasta dónde ven y cuánto aguantan la persecución.
public static class Difficulty
{
    public enum Level { Facil = 0, Normal = 1, Dificil = 2 }

    public struct Tuning
    {
        public string name, summary;
        public float health;       // vida de los enemigos
        public float damage;       // daño de sus golpes
        public float sight;        // distancia a la que ven al héroe
        public float reaction;     // segundos desde que lo ven hasta que dan la alarma
        public float windup;       // aviso antes del golpe (más = más tiempo para esquivar)
        public float cooldown;     // espera entre ataques
        public float pursuit;      // velocidad al perseguir de lejos
        public float leash;        // cuánto se alejan de su campamento persiguiendo
        public int attackers;      // corruptos cuerpo a cuerpo que golpean a la vez
    }

    // Normal es el equilibrio que ya tenía el juego (los jefes se ajustaron con él).
    private static readonly Tuning[] Table =
    {
        new Tuning
        {
            name = "Fácil", summary = "Enemigos más blandos y lentos en reaccionar; avisan más antes de golpear.",
            health = 0.75f, damage = 0.7f, sight = 0.85f, reaction = 0.7f, windup = 1.35f, cooldown = 1.3f, pursuit = 0.9f, leash = 0.8f, attackers = 2
        },
        new Tuning
        {
            name = "Normal", summary = "El equilibrio de Frostbound: hay que esquivar y elegir bien las peleas.",
            health = 1f, damage = 1f, sight = 1f, reaction = 0.3f, windup = 1f, cooldown = 1f, pursuit = 1f, leash = 1f, attackers = 4
        },
        new Tuning
        {
            name = "Difícil", summary = "Ven de más lejos, reaccionan al instante, rodean en grupo y no te dejan huir.",
            health = 1.25f, damage = 1.2f, sight = 1.3f, reaction = 0.05f, windup = 0.8f, cooldown = 0.75f, pursuit = 1.15f, leash = 1.6f, attackers = 5
        },
    };

    public static Level Current
    {
        get
        {
            SaveData d = GameSession.Instance != null ? GameSession.Instance.Current : null;
            return d != null ? (Level)Mathf.Clamp(d.difficulty, 0, Table.Length - 1) : _test;
        }
    }

    // Sin partida (probando una escena en el editor): Normal, o lo que fije una comprobación.
    private static Level _test = Level.Normal;
    public static void SetForTesting(Level l) => _test = l;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _test = Level.Normal;

    public static Tuning Now => Table[(int)Current];
    public static Tuning Of(Level l) => Table[(int)l];
    public static string Name(Level l) => Table[(int)l].name;
    public static int Count => Table.Length;
}
