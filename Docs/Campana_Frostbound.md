# Campaña de Frostbound — diseño

**Fecha:** 18 de octubre de 2026 · **Rama:** `feature/campana-completa`
**Decisiones del usuario:** dos capítulos más (II y III); Bobby se redime y el jefe final es lo que late bajo el hielo; la mansión del menú es solo decorado; misiones basadas en datos.

---

## 1. La historia completa

**Prólogo (ya en el juego).** Hace generaciones, los pingüinos sellaron bajo el Frostspire algo vivo hecho de hielo: el **Corazón del Frost**. El sello se rompió y el Frost corrompe a todo el que toca.

**Capítulo I · La ciudadela caída y El ascenso (hecho).** Gorvald, Smeagol y Rocco Vendaval. Rocco confiesa que **Bobby**, su amigo, liberó el Frost y tiene a su hermano **Pipo**.

**Capítulo II · El Bosque Corrupto.** Más allá de la Escalinata, el bosque de pinos negros donde nace la savia corrupta (la de la Runa Toxis). El héroe sigue el rastro de Pipo: huellas pequeñas, una bufanda, dibujos en la nieve. Descubre que Bobby **no** está aquí: dejó el bosque al cuidado de **la Madre Savia**, una chamana corrupta que alimenta al Frost con la savia de los pinos. Al vencerla, el héroe encuentra a Pipo encerrado en un capullo de hielo, vivo. Pipo cuenta lo que vio: Bobby hablaba solo, con una voz que "salía del hielo", y subió a la cumbre.
*Cierre:* Pipo vuelve al poblado con Steve. Se abre el camino a la cumbre.

**Capítulo III · La cumbre del Frostspire.** Una ascensión por grietas de hielo y cavernas cristalizadas hasta la cámara del sello. Arriba espera **Bobby**, ya medio cristal. Al vencerlo se libera como Gorvald y Rocco, pero esta vez no cae: confiesa que el Corazón le prometió devolverle a alguien que perdió, y que él solo rompió el sello. Pide al héroe que lo ayude a rehacerlo. El **Corazón del Frost** despierta: jefe final en tres fases. Al vencerlo, Bobby y el héroe rehacen el sello con los fragmentos de los tres jefes del capítulo I.
*Final:* el Frost retrocede, los corruptos que quedan vuelven a ser pingüinos, la ventisca para. Epílogo en el poblado con Steve, Pipo y Bobby. Créditos.

---

## 2. Capítulos, zonas y misiones

| Cap. | Zona (escena) | Misiones (id) | Jefes |
|---|---|---|---|
| I | `01_Village`, `02_Foothills`, `03_Escalinata` | `ciudadela_caida`, `ascenso_frostspire` (ya existen) | Gorvald, Smeagol, Rocco |
| II | `04_BosqueCorrupto` (nueva) | `rastro_de_pipo` (seguir 3 pistas), `savia_negra` (destruir 3 raíces corruptas), `la_madre_savia` (jefa y rescate) | **Madre Savia** (jefa) |
| III | `05_Cumbre` (nueva) | `el_ascenso_final` (grietas y 3 altares), `el_sello` (Bobby y el Corazón) | **Bobby** (se redime), **Corazón del Frost** (final) |
| Final | `01_Village` | `epilogo` (hablar con Steve, Pipo y Bobby) | — |

---

## 3. Enemigos nuevos (pingüinos con el rig actual y un equipo nuevo)

| Enemigo | Capítulo | Comportamiento | Cómo |
|---|---|---|---|
| **Leñador corrupto** | II | Lento y duro; golpe de hacha en área con aviso | `EnemyDefinition` con `areaRadius` y `windup` largo |
| **Escupesavia** | II | A distancia; escupe savia que deja un charco venenoso unos segundos | Proyectil + charco nuevo (`PoisonPuddle`) |
| **Acechador** | II | Rápido, se camufla en la niebla y aparece por la espalda | `blinkInterval` (ya existe para Smeagol) |
| **Cristalizado** | III | Casi de hielo; resiste el daño físico y se rompe con fuego | Resistencias por tipo de daño (`Damageable`) |
| **Hijo del Frost** | III | Élite; al morir se parte en dos esquirlas menores | Evento de muerte que genera dos enemigos |

## 4. Jefes nuevos

| Jefe | Fases |
|---|---|
| **Madre Savia** | 1: cuerpo a cuerpo y charcos de savia. 2 (66 %): invoca raíces que atrapan. 3 (33 %): lluvia de savia en área con aviso |
| **Bobby** | Duelo de ninja-mago helado: teletransporte, copias de hielo, lanzas de escarcha. Se rinde al 10 % (no muere) |
| **Corazón del Frost** | Cristal gigante inmóvil con escudo. 1: oleadas y rayos. 2: hay que romper 3 cristales que lo alimentan. 3: expulsa el hielo en anillos que hay que esquivar rodando |

## 5. Sistemas que hay que construir (en orden)

| ID | Sistema | Resultado |
|---|---|---|
| **C-1** | **Misiones basadas en datos** | `QuestDefinition` (ScriptableObject) con etapas, objetivos (hablar, llegar, matar, jefe, recoger, interactuar), recompensas y diálogos. `QuestLog` sigue guardando "id + etapa", así las partidas actuales y el código del capítulo I siguen funcionando. Las dos misiones actuales pasan a datos |
| **C-2** | Eventos de objetivo | `QuestEvents.Report(tipo, id)` desde enemigos, jefes, zonas e interactuables; el objetivo activo avanza solo |
| **C-3** | Diálogo con NPC por datos | `QuestGiver` lee sus líneas de la misión y la etapa (Steve, Pipo, Bobby) |
| **C-4** | Zona del Bosque Corrupto | `ForestGenerator` (como `MountainGenerator`), escena `04_BosqueCorrupto`, puerta desde la Escalinata y vuelta |
| **C-5** | Enemigos del capítulo II | Leñador, Escupesavia (y charco venenoso), Acechador |
| **C-6** | Madre Savia y rescate de Pipo | Jefa en 3 fases, capullo de hielo, Pipo vuelve al poblado |
| **C-7** | Zona de la Cumbre | `SummitGenerator`, escena `05_Cumbre`, cámara del sello |
| **C-8** | Enemigos del capítulo III | Cristalizado (resistencias), Hijo del Frost (se parte) |
| **C-9** | Bobby y el Corazón del Frost | Jefes, rendición de Bobby y fases del Corazón |
| **C-10** | Final | Secuencia del sello, epílogo en el poblado, créditos de verdad (fuentes OFL, Poly Haven CC0, sonidos) y "Fin" en el menú |
| **C-11** | Pulido | Mapa por zona, consejos de carga, equilibrio de niveles (II: 8–14, III: 14–20), comprobaciones de cada capítulo |

Cada tarea se verifica con el bridge (comprobaciones en `Editor/Checks`), la autoprueba de la build y capturas, como la UI.
