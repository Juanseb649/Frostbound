# Auditoría técnica — Frostbound

**Fecha:** 17 de octubre de 2026 (actualiza la del 28 de septiembre de 2026)
**Proyecto:** Frostbound 0.1.3, Unity 6000.5.7f1, URP, Input System nuevo
**Alcance:** todo el código de `Assets/`, las escenas, los datos (ScriptableObjects), Build Settings, paquetes y el historial de git (131 commits; tags `v0.1.0` a `v0.1.3`)

---

## 1. Resumen ejecutivo

En tres semanas el proyecto pasó de "mucho sistema y nada de juego" a **un juego jugable de principio a fin del Capítulo I**. El bucle central de un ARPG ya está cerrado: **poblado → zona → enemigos que te pueden matar → botín y XP → pergamino de regreso → tienda y herrera → volver a salir**. Hay 3 jefes con discurso al caer, 2 misiones, 4 escenas, co-op local, guardado por ranuras, audio y builds para Windows, macOS y Linux.

**Diagnóstico corto:** ya hay juego, pero la deuda técnica creció al mismo ritmo que el contenido. Los cuatro bloqueos de la auditoría anterior (enemigos, daño al jugador, NPC que no hacían nada, zonas sin conectar) están resueltos. Lo que queda pendiente es estructural:

1. **Siguen existiendo dos sistemas de vida** (`CharacterStats` para el héroe y `Damageable` para todo lo demás). Ahora el veneno ya está programado dos veces.
2. **Las clases gigantes crecieron todavía más**, y aparecieron otras nuevas de más de 700 líneas.
3. **No hay asmdef ni namespaces**: 154 scripts compilan en `Assembly-CSharp`.
4. **Riesgos de entorno:** un paquete apunta a una ruta local de `Downloads`, el bridge de automatización está activo en los defines de Standalone y hay cambios sin commitear.

| Área | Antes | Ahora | Comentario |
|---|---|---|---|
| Datos y diseño de sistemas | 8/10 | 8/10 | Se sumaron enemigos, calidades de botín, afijos y precios sin tocar código |
| Presentación (menú, personaje, UI) | 8/10 | 9/10 | Menús de noche helada, UI adaptable de 4:3 a 32:9, mapa con niebla y HUD de orbes |
| Bucle de juego | 3/10 | 8/10 | Combate, muerte, botín, economía, misiones y zonas conectadas |
| Contenido | — | 7/10 | Capítulo I completo: ciudadela, castillo, Las Faldas, La Escalinata y 3 jefes |
| Arquitectura del código | 5/10 | 4/10 | Creció sin modularizar: más clases gigantes, más `Camera.main` y más búsquedas globales |
| Mantenibilidad y limpieza | 4/10 | 5/10 | Tests NUnit reales, `Editor/Checks`, `SceneIds` y `ReleaseBuilder`, pero sin asmdef |

---

## 2. Cómo está seccionado el proyecto

### 2.1 Carpetas y peso del código

| Módulo | Archivos | Líneas | Qué contiene |
|---|---|---|---|
| `Scripts/` (raíz) | 6 | 1.137 | `PlayerController`, `PlayerDodge`, `CharacterStats`, `CharacterClass`, `CameraFollow`, `GameplayInput` |
| `Scripts/UI` | 31 | 6.424 | Menú (`FrostMenu`), HUD, inventario, comercio, pausa, muerte, carga, opciones y controles |
| `Scripts/World` | 32 | 6.433 | Poblado procedural, castillo, montaña, escalinata, zonas, hogueras, NPC y atmósfera |
| `Scripts/Items` | 22 | 2.660 | Inventario, equipo, cartera, arcón, pociones, portal, precios y botín (calidad, afijos) |
| `Scripts/Combat` | 13 | 2.555 | `PlayerCombat`, `Damageable`, habilidades, respawn, aflicciones, gólem y orbes |
| `Scripts/Outfit` | 6 | 1.771 | Ropa por piezas, `PenguinRigAnimator`, tela y huesos elásticos |
| `Scripts/Enemies` | 11 | 1.412 | `EnemyBrain`, `BossController`, campamentos, spawner, chamán, proyectiles y muertes |
| `Scripts/Core` | 9 | 957 | `SaveSystem`, `SaveData`, `AudioManager`, `GameSettings`, `SceneIds`, `GameDatabase` |
| `Scripts/Procgen` | 7 | 850 | Seeds, `GameSession` y el `DungeonGenerator` (sin uso, ver N3) |
| `Scripts/Coop` | 1 | 734 | `CoopManager`: jugador 2 con mando o teclado |
| `Scripts/Map` | 3 | 719 | Mapa por zona con niebla de guerra y marcadores |
| `Scripts/Input` | 2 | 405 | Mapa de controles reasignable e iconos de Xbox |
| `Scripts/Characters`, `Game`, `Quests`, `Debug` | 11 | 776 | Balanceo y ragdoll, apariencia, misiones y herramientas de depuración |
| **Total runtime** | **154** | **≈ 26.800** | (era 64 / ≈ 9.450) |
| `Editor/` | 51 | ≈ 9.470 | Herramientas de construcción (22), `Checks` (17), `Tests` (7), `Legacy` (3) y `Build` (2) |

Datos: 108 ScriptableObjects. 4 clases, 7 enemigos, 18 armas, 8 runas, 4 sets, piezas por clase, pociones, pergamino de regreso, 2 configuraciones de mazmorra, outfits del héroe y de los corruptos, paleta de plumajes y skin de UI.

### 2.2 Mapa de sistemas

```
        ┌──────────── GameSession (DontDestroyOnLoad) ────────────┐
        │ SaveData actual · ranura · clase · plumaje · seed       │◄──► SaveSystem (4 ranuras, JSON atómico)
        │ misiones (QuestLog) · hogueras · niebla del mapa · co-op│
        └─────────────────────────────────────────────────────────┘
                 ▲                                   ▼
 00_MainMenu ─ FrostMenu ─► 01_Village ──ZoneTravel──► 02_Foothills ──► 03_Escalinata ──► (Bosque Corrupto: pendiente)
                              │  VillageWorldGenerator (seed)      MountainGenerator      StairsGenerator
                              │  CastleBuilder / CitadelSiege      EnemyCamp · hordas     RoccoEncounter
                              │  CastleInterior / ThroneEncounter  guardianes del paso    + ShamanCaster
                              ▼
 PlayerController ◄─► PlayerCombat · PlayerAbility · PlayerDodge ──► Damageable (enemigos) ──► EnemyLoot ──► WorldItem
        │                                                                       ▲
        ▼                                                                       │
 CharacterStats (vida del héroe) ◄── EnemyBrain.HitPlayer · EnemyProjectile ── EnemyBrain / BossController
        │  PlayerAfflictions · PlayerRespawn ──► Bonfire
        ▼
 Equipment ◄─ Inventory ◄─ Wallet / Stash ◄─ TradeWindow ◄─ NPCInteractable (Tico: comercio · Brunna: reparar)
 UI: GameHUD (orbes) · WorldHUD · InventoryScreen · MapUI · PauseMenu · DeathScreen · LoadingScreen
 Transversal: AudioManager (música por zona, tema de jefe, SFX) · InputMap · CoopManager · Notifications
```

### 2.3 Escenas, flujo y misiones

| Escena | En Build | Contenido |
|---|---|---|
| `00_MainMenu.unity` | Sí (0) | Menú de noche helada, ranuras de guardado, selección de clase y plumaje |
| `01_Village.unity` | Sí (1) | Poblado procedural (3,4 MB), claro de Steve, iglús con escaramuzas, ciudadela, castillo e interior |
| `02_Foothills.unity` | Sí (2) | Las Faldas: montaña con ventisca, 6 campamentos, hordas, ruinas, 2 hogueras y guardianes del paso |
| `03_Escalinata.unity` | Sí (3) | Escalera de piedra en S hasta la explanada de Rocco Vendaval |
| `04_BosqueCorrupto.unity` | Sí (4) | Bosque de pinos negros: pistas de Pipo, raíces corruptas, corazón del bosque con la Madre Savia |
| `05_Cumbre.unity` | Sí (5) | Grieta de hielo con tres altares, muro de hielo y la cámara del sello (Bobby y el Corazón del Frost) |

Las misiones son datos (`Assets/Resources/Quests`, `QuestDefinition`): etapas con objetivo, líneas del NPC y recompensa.
Diseño completo en `Docs/Campana_Frostbound.md`.

**Capítulo I:**
- **"La ciudadela caída":** Steve da la misión → asedio de la plaza → **Gorvald, el Bárbaro Corrupto** (rompe el sello del castillo) → interior del castillo → **Smeagol, el Ninja Maldito** en la sala del trono.
- **"El ascenso":** sendero del norte → Las Faldas → guardianes del paso helado → La Escalinata → **Rocco Vendaval** (3 fases: báculo, arco y mandoble). Al caer revela a Bobby y Pipo, y se despiertan los chamanes.

**Capítulo II · El Bosque Corrupto:** "El rastro de Pipo" (3 pistas) → "Savia negra" (3 raíces) → "La Madre Savia" (jefa en 3 fases; Pipo libre en un capullo de hielo y de vuelta con Steve). Enemigos nuevos: leñador, escupesavia y acechador.

**Capítulo III · La Cumbre:** "La cumbre" (3 altares) → "El sello": **Bobby** se rinde y confiesa → **Corazón del Frost** (oleadas, escudo con 3 cristales, anillos de hielo) → rehacer el sello → "De vuelta a casa" (Bobby y Pipo en el poblado) → créditos. Enemigos nuevos: cristalizado (débil al fuego) e hijo del Frost (se parte en dos).

---

## 3. Estado por sistema

| Sistema | Estado | Detalle |
|---|---|---|
| Menú, ranuras y selección de clase | ✅ Hecho | 4 ranuras, plumaje, preview 3D y menús de noche helada |
| Movimiento | ✅ Hecho | WASD, clic y mando. NavMesh horneado en runtime por cada generador. Red de seguridad contra caídas |
| Combate del héroe | ✅ Hecho | Combo ligero y pesado, rodar, runas, durabilidad, críticos y hitstop |
| Habilidades de clase | ✅ Hecho | Carga de escudo, gólem de nieve, abanico de kunais y grito de guerra (maná y enfriamiento) |
| Enemigos e IA | ✅ Hecho | 7 tipos de corruptos. Las clases malditas aparecen tras el primer jefe. Chamanes que reviven |
| Jefes | ✅ Hecho | `BossController`: barra, arma encantada, teletransporte y discurso sin maldición al caer |
| Daño al jugador, muerte y reaparición | ✅ Hecho | Reaparece en la hoguera encendida más cercana con el equipo algo desgastado. Veneno estilo Souls |
| Botín | ✅ Hecho | `LootRoller` con calidad y afijos, haz de luz, monedas y hallazgo mágico |
| Economía | ✅ Hecho | Cartera, arcón de 60 casillas, tienda de Tico, venta y reparación de pago con afilado |
| Guardado | ✅ Hecho | JSON por ranura con escritura atómica. Autoguardado al cambiar de zona y de misión |
| Misiones | ✅ Hecho | `QuestLog` con etapas guardadas y panel de objetivo |
| Zonas | ✅ Hecho | `ZoneTravel`, `ZoneGate` y `ZoneArrival` entre las 4 escenas, más el pergamino de regreso |
| Mapa | ✅ Hecho | Foto cenital por zona, niebla de guerra guardada y marcadores |
| Audio | ✅ Hecho | Música por zona con fundido, tema de jefe, combate, voces de corruptos, runas y UI |
| Opciones | ✅ Hecho | Pantalla, resolución de 720p a 4K, ultrawide, periférico preferido y reasignación |
| Co-op local | ✅ Hecho | Jugador 2 con mando o teclado, HUD propio, mochila propia y enemigos reforzados |
| Builds | ✅ Hecho | `ReleaseBuilder` para Windows, macOS y Linux |
| NPC | ✅ Hecho | Diálogo ambiental, servicios reales y cháchara del poblado |
| Mazmorra `DungeonGenerator` | ❌ Sin uso | Lo reemplazaron `CastleInterior` y los generadores de montaña (ver N3) |
| Bosque Corrupto / Capítulo II | ❌ Pendiente | Solo existe el cartel "Camino del Bosque Corrupto" en La Escalinata |
| Créditos | ❌ Pendiente | |

---

## 4. Hallazgos (ordenados por severidad)

### Resueltos desde la auditoría anterior

| ID | Hallazgo | Cómo se resolvió |
|---|---|---|
| C2 | No había enemigos | `EnemyBrain`, `EnemyDefinition`, campamentos, spawner y jefes |
| C3 | Botones de NPC sin efecto | `TradeWindow`, `Wallet` y reparación de pago |
| C4 | Sin NavMesh | Lo hornean en runtime `VillageWorldGenerator`, `MountainGenerator`, `StairsGenerator` y `CastleInterior` |
| A3 | Duplicados | Resuelto en la fase 1 de limpieza (`v0.2-limpieza`) |
| A4 | Persistencia dispersa | `SaveSystem` + `SaveData` único |
| M2 | Tests caseros | **Parcial:** 32 tests `[Test]` reales en `Editor/Tests` (7 archivos). Los checks por bridge siguen aparte |
| M4 | Nombres de escena | `00_`…`03_` y `SceneIds` |
| M7 | Paquetes sin uso | **Parcial:** se quitaron 3. `ai.assistant` y `ai.inference` se mantienen a propósito |

### 🔴 Críticos

**C1. Siguen los dos sistemas de vida (abierto desde la auditoría anterior, ahora con más costo).**
El héroe recibe daño por `CharacterStats.TakeDamage` (lo llaman `EnemyBrain.HitPlayer` y `EnemyProjectile`). Enemigos, aliados y gólems usan `Damageable.TakeHit`. Ya pasó lo que se anticipaba: el veneno del héroe vive en `PlayerAfflictions` y el de los enemigos en `Damageable`, con dos implementaciones. Además, `CharacterStats.Die()` todavía escribe un `Debug.Log`.
**Recomendación:** igual que antes. El héroe usa `Damageable` y `CharacterStats` solo calcula. Hacerlo antes del Capítulo II, porque cada efecto nuevo (quemadura, congelación del héroe) duplicaría trabajo.

**N1. Un paquete apunta a una ruta local.** `Packages/manifest.json` (sin commitear) agrega `"com.justinpbarnett.unity-mcp": "file:C:/Users/MAO/Downloads/unity-mcp-master"`. Si se commitea, el proyecto **no abre** en la máquina de otro integrante ni en CI.
**Recomendación:** no commitear esa línea. Si el MCP hace falta para el equipo, usar una URL de git o mantenerlo fuera del manifest compartido.

### 🟠 Altos: arquitectura

**A1. Las clases gigantes crecieron.**

| Script | Antes | Ahora |
|---|---|---|
| `PenguinRigAnimator` | 835 | 1.114 |
| `InventoryScreen` | 825 | 1.022 |
| `FrostMenu` | — | 882 |
| `CoopManager` | — | 734 |
| `CastleBuilder` | — | 719 |
| `PlayerCombat` | — | 678 |
| `VillageWorldGenerator` | — | 658 |
| `WorldHUD` | 644 | 629 |
| `PlayerController` | 455 | 598 |

Hay 15 scripts de juego con más de 500 líneas (antes eran 4). La recomendación de partir `PlayerController` (motor, entrada, apuntado e interacción) sigue vigente, y `CoopManager` es un candidato nuevo.

**M1. Sin asmdef ni namespaces (abierto).** Hay 0 namespaces y 0 asmdef. Con 154 scripts, cada cambio recompila casi 27.000 líneas, y los tests de `Editor/Tests` compilan junto a las herramientas de editor.

**N2. El bridge está activo en los defines de Standalone.** `FROSTBOUND_BRIDGE` aparece en `scriptingDefineSymbols` de Standalone. El código del bridge está en `Editor/`, así que no entra en la build, pero cualquier `#if FROSTBOUND_BRIDGE` en runtime sí entraría.
**Recomendación:** que `ReleaseBuilder` quite el símbolo antes de compilar y lo restaure después, o moverlo a un perfil de build de desarrollo.

### 🟡 Medios: calidad y mantenimiento

- **M5/M6 empeoraron.** `Camera.main` pasó de 7 a 16 usos y hay 29 llamadas a `FindObjectsByType`/`FindAnyObjectByType`. Conviene un `Registry<T>` y una cámara cacheada (plan F2.5).
- **N3. Código muerto de mazmorra.** `DungeonGenerator`, `RoomPlacer` y `CorridorBuilder` (en `Scripts/Procgen`) no se referencian desde ninguna escena ni script. `Frozen_Castle` y `Castillo_Nuevo` (`DungeonConfig`) quedan huérfanos si nadie los usa. Se pueden borrar o mover a `_Archive`.
- **N4. Seis scripts sueltos en la raíz de `Scripts/`** (`PlayerController`, `CharacterStats`, etc.). Moverlos a `Player/` y `Characters/` desde Unity.
- **N5. `SetupVillage.cs` (1.291 líneas) sigue vivo:** lo usan `SetupProceduralVillage`, `SetupSteve`, `SetupWeapons` y `SetupPenguinWardrobe`, aunque el poblado ahora lo genera `VillageWorldGenerator` en runtime. Revisar qué parte sigue haciendo falta.
- **N6. Cambios locales sin commitear:** las fuentes SDF (≈ 9.500 líneas de diff), `UniversalRenderPipelineGlobalSettings`, `manifest.json` y `packages-lock.json`, además de `.vsconfig` y `Frostbound.slnx` sin seguimiento. Decidir qué se commitea (las fuentes probablemente sí) y qué va al `.gitignore` (`*.slnx`).
- **`Debug.Log` en runtime:** pasó de 14 a 18. Envolverlo en un `Log` condicional (plan F5.3).

### 🟢 Bajos

- Hay unas 20 ramas remotas `feature/*`, `fix/*` y `fase/*` que ya están mergeadas. Se pueden borrar.
- `companyName: DefaultCompany` en Player Settings. Esto afecta la ruta de `persistentDataPath` y, por lo tanto, la ubicación de las partidas guardadas. Cambiarlo moverá los guardados existentes.
- `01_Village.unity` pesa 3,4 MB (antes 4,9 MB). Mejoró, pero todavía conviene usar más prefabs.
- `Docs/baseline/` (plan F0.2) no existe.

---

## 5. Lo que está bien hecho

- **Diseño guiado por datos.** Clases, ítems, enemigos, sets, runas y outfits son ScriptableObjects. Los jefes copian la definición en vez de modificarla.
- **Generación determinista por seed** del poblado, el castillo, la montaña y la escalera, con NavMesh horneado al terminar.
- **Guardado sólido.** Ranuras, escritura atómica (`.tmp` y reemplazo) y estado completo: misiones, hogueras, niebla del mapa, arcón y el héroe del jugador 2.
- **`Editor/Checks`:** prueba de humo, balance, simulación de botín, co-op, audio, controles, NPC, mundo y comercio, ejecutables por el bridge. Junto con los 32 tests NUnit, forman una red de seguridad real.
- **`ReleaseBuilder`** con versionado (`bundleVersion` 0.1.3) y tags por versión.
- **Flujo de git ordenado:** `develop` más ramas `feature/*` y `fix/*` con merges descriptivos en español.
- **Accesibilidad de control y pantalla:** mando de Xbox con iconos, reasignación y UI de 4:3 a 32:9.
- **Narrativa integrada en el juego:** jefes que se liberan y hablan al caer, cuerpos congelados que cuentan lo que pasó e iglús con escaramuzas que se guardan.

---

## 6. Recomendación: próximos pasos en orden

**Fase A — Higiene inmediata (medio día).**
1. Revisar los cambios locales: commitear las fuentes SDF y los ajustes de URP, sacar la línea de `unity-mcp` del manifest compartido y agregar `*.slnx` al `.gitignore`.
2. Hacer que `ReleaseBuilder` quite `FROSTBOUND_BRIDGE` al compilar.
3. Borrar las ramas remotas mergeadas.

**Fase B — Deuda crítica (antes del Capítulo II).**
1. Unificar la vida (C1): el héroe usa `Damageable` y `PlayerAfflictions` se fusiona con los estados de `Damageable`.
2. asmdef (`Frostbound.Runtime`, `Frostbound.Editor`, `Frostbound.Tests`) y namespaces por carpeta. Mover la raíz de `Scripts/`.
3. Borrar `DungeonGenerator`, `RoomPlacer`, `CorridorBuilder` y sus configuraciones si no se van a usar.

**Fase C — Refactor por partes**, a medida que se toque cada archivo: `PlayerController`, `PenguinRigAnimator`, `InventoryScreen`, `CoopManager` y `FrostMenu`. Agregar `Registry<T>` y una cámara cacheada.

**Fase D — Contenido.** Capítulo II: el Bosque Corrupto, Bobby (el que liberó el Frost) y Pipo. Pantalla de créditos con las licencias OFL. Prueba con alguien externo (sesión de 20 minutos).

> **Regla para no volver a desviarse:** no empezar el Capítulo II hasta cerrar C1 y M1. Cada zona nueva sobre dos sistemas de vida y un solo assembly multiplica el costo de arreglarlo después.

---

## 7. Métricas

| Métrica | 28-09-2026 | 17-10-2026 |
|---|---|---|
| Versión | — | 0.1.3 |
| Commits | 18 | 131 |
| Scripts de juego | 64 (≈ 9.450 líneas) | 154 (≈ 26.800 líneas) |
| Scripts de editor | 20 (≈ 6.875 líneas) | 51 (≈ 9.470 líneas) |
| Scripts de juego con más de 500 líneas | 4 | 15 |
| ScriptableObjects de datos | ≈ 90 | 108 |
| Escenas en Build | 2 | 4 |
| Enemigos | 0 | 7 tipos + 3 jefes |
| Misiones | 0 | 2 |
| Tests NUnit | 0 | 32 |
| `Debug.Log` en runtime | 14 | 18 |
| `Camera.main` en runtime | 7 | 16 |
| asmdef / namespaces | 0 / 0 | 0 / 0 |

> Nota: `Docs/Plan_Mejora_Frostbound.md` sigue describiendo el estado del 28-09. Su Anexo B solo registra la fase F1, pero gran parte de F3, F4 y F6 ya está hecha por fuera de ese plan.
