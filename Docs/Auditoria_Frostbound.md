# Auditoría técnica — Frostbound

**Fecha:** 28 de septiembre de 2026
**Proyecto:** `My project (2)`, Unity 6000.5.7f1, URP, Input System nuevo
**Alcance:** todo el código de `Assets/`, las escenas, los datos (ScriptableObjects), Build Settings y el historial de git (18 commits)

---

## 1. Resumen ejecutivo

El proyecto avanzó mucho desde la primera revisión. Ya existen el menú y la selección de clase, un pingüino con esqueleto y ropa por piezas, el poblado con 14 NPC, el inventario, el equipo, 18 armas con runas, un combo cuerpo a cuerpo, la acción de rodar, la progresión por niveles y el HUD. La base de datos del juego está bien pensada.

**Diagnóstico corto:** hay mucho sistema y todavía no hay juego. Todavía no existe el bucle central de un ARPG: **entrar a una zona → pelear contra enemigos que te pueden matar → conseguir loot → volver al poblado a gastarlo**. Faltan cuatro piezas para cerrarlo:

1. **No hay enemigos.** Solo muñecos de práctica. No hay IA, ni spawns, ni tablas de loot.
2. **El jugador no puede recibir daño.** `CharacterStats.TakeDamage()` existe, pero ningún script lo llama.
3. **Los NPC muestran "Comprar / Vender / Reparar", pero esas opciones no hacen nada.** Se dispara el evento `NPCInteractable.OptionChosen` y ningún script lo escucha.
4. **La mazmorra procedural no está conectada al juego.** `DungeonGenerator` no aparece en ninguna escena y la salida del poblado (`ZoneExit`) no lleva a ningún lado.

| Área | Nota | Comentario |
|---|---|---|
| Datos y diseño de sistemas | 8/10 | ScriptableObjects claros para clases, ítems, sets, runas, outfits y mazmorras |
| Presentación (menú, personaje, UI) | 8/10 | Muy por encima de lo normal para esta etapa |
| Bucle de juego | 3/10 | Faltan enemigos, daño al jugador, economía y zonas conectadas |
| Arquitectura del código | 5/10 | Funciona, pero hay clases gigantes, duplicados y dos sistemas de vida |
| Mantenibilidad y limpieza | 4/10 | Escenas sobrantes, scripts muertos, sin namespaces ni asmdef, tests caseros |

---

## 2. Cómo está seccionado el proyecto

### 2.1 Carpetas y peso del código

| Módulo | Archivos | Líneas | Qué contiene |
|---|---|---|---|
| `Scripts/` (raíz) | 9 | 1.365 | Jugador (`PlayerController`, `PlayerDodge`), stats, clases, cámara, menú antiguo |
| `Scripts/Combat` | 4 | 735 | `PlayerCombat`, `Damageable`, `Projectile`, `LightningArc` |
| `Scripts/Items` | 11 | 1.584 | Inventario, equipo, definiciones de ítems, armas, runas, sets, loot en el suelo |
| `Scripts/Outfit` | 6 | 1.481 | Ropa por piezas, animación del rig, tela y huesos elásticos |
| `Scripts/UI` | 17 | 2.890 | Menú principal, selección de clase, inventario, HUD, preview 3D |
| `Scripts/World` | 5 | 383 | NPC, interacción, nombres, luces, salida de zona |
| `Scripts/Game` | 5 | 223 | Apariencia, plumaje, equipo inicial por clase |
| `Scripts/Procgen` | 7 | 792 | Seeds, sesión de juego y generador de mazmorras |
| **Total runtime** | **64** | **≈ 9.450** | |
| `Editor/` | 20 | 6.875 | Herramientas que **construyen** escenas, prefabs, iconos y texturas; tests; bridge |

Datos: 4 clases, 57 ítems (18 armas, 8 runas, 4 sets de armadura, piezas por clase, pociones y materiales), 2 configuraciones de mazmorra, una paleta de 8 plumajes y un skin de UI.

### 2.2 Mapa de sistemas

```
               ┌───────────── GameSession (DontDestroyOnLoad) ─────────────┐
               │  clase elegida · plumaje · nombre · worldSeed             │
               └───────────────────────────────────────────────────────────┘
                     ▲ escribe                          ▼ lee
 Main menu.unity ── ClassSelectController ──► SampleScene (poblado) ──► ZoneExit ──► ✗ (no hay escena)
                                                   │
       ┌───────────────────────────────────────────┼──────────────────────────────┐
       ▼                                           ▼                              ▼
 PlayerController ◄──► PlayerCombat ──► Damageable (muñecos)       NPCInteractable ──► OptionChosen ──► ✗ (nadie escucha)
   │   PlayerDodge        │                                          VillagerNPC (IA ambiental)
   ▼                      ▼
 CharacterStats ◄── Equipment ◄── Inventory ◄── WorldItem / ItemPickup
   (vida del jugador,      │
    nunca recibe daño)     ▼
                     WeaponHolder · HeroGearEquipper · PenguinOutfit · PenguinRigAnimator
 UI: GameHUD (vida/maná/XP) · WorldHUD (nombres, diálogo, menú NPC) · InventoryScreen

 DungeonGenerator + RoomPlacer + CorridorBuilder ──► ✗ (no se usa en ninguna escena)
```

### 2.3 Escenas y flujo

| Escena | En Build | Estado |
|---|---|---|
| `Main menu.unity` | Sí (0) | Menú nuevo y selección de clase. **Todavía conserva objetos del menú viejo:** 3 `MenuManager`, 3 `Canvas` y un `Player`. |
| `SampleScene.unity` | Sí (1) | Es el **poblado** (4,9 MB, 14 NPC). El nombre no dice lo que es. |
| `scene.unity` | No | Escena vieja (menú + jugador). Sobra. |

Flujo real hoy: **Menú → Selección de clase → Poblado → (se acaba).**

---

## 3. Estado por sistema

| Sistema | Estado | Detalle |
|---|---|---|
| Menú principal y selección de clase | ✅ Hecho | Sigue la especificación: tokens, preview 3D, plumaje y `GameSession` |
| Pingüino con rig y ropa | ✅ Hecho | `Penguin_Rigged.fbx`, shader toon, outfits por pieza, tela y huesos elásticos |
| Movimiento | 🟡 Parcial | WASD y clic. **Sin NavMesh**: en pasillos y esquinas el pingüino se va a trabar |
| Rodar | ✅ Hecho | `PlayerDodge` con Espacio |
| Combate del jugador | 🟡 Parcial | Combo de 3 golpes, proyectiles, runas y durabilidad. Solo se prueba contra muñecos |
| Enemigos e IA | ❌ No existe | Es el hueco más grande del proyecto |
| Daño al jugador y muerte | ❌ No existe | `TakeDamage` no se llama; `Die()` solo escribe en consola |
| Inventario y equipo | ✅ Hecho | Slots, sets con bonus, stats, pantalla de personaje |
| Loot en el suelo | 🟡 Parcial | `WorldItem` y `ItemPickup` existen, pero no hay tablas de drop porque no hay nada que mate |
| XP, niveles y puntos | ✅ Hecho | Por ahora solo se prueba con `ProgressionDebug` |
| NPC y diálogo | 🟡 Parcial | 14 NPC con diálogo ambiental. **Tienda, venta y reparación no están implementadas** |
| Rocco Vendaval | ❌ Pendiente | Tiene material y modelo, pero no está en la escena (spec §6) |
| Economía (oro) | ❌ No existe | Sin moneda no puede haber tienda ni reparación |
| Guardado | ❌ No existe | "Continuar" revisa `frostbound_save`, pero ningún script escribe esa clave |
| Mazmorra procedural | 🟡 Aislada | Genera salas y pasillos con cubos, pero no está conectada al flujo del juego |
| Zonas (Foothills…) | ❌ No existe | `ZoneExit` no tiene escena de destino |
| Audio | ❌ No existe | Sin música ni efectos |
| Opciones y créditos | ❌ Stub | |

---

## 4. Hallazgos (ordenados por severidad)

### 🔴 Críticos: bloquean el bucle de juego

**C1. Hay dos sistemas de vida separados.**
`Damageable` (enemigos y muñecos) tiene su propia `Health`, eventos `Damaged` y `Died`, estados como congelado o envenenado, y regeneración. `CharacterStats` (jugador) tiene `currentHealth`, `TakeDamage` y `Died` por su cuenta. Cuando existan enemigos que ataquen, cada sistema de efectos (veneno, escarcha, empuje, robo de vida) va a tener que implementarse dos veces.
**Recomendación:** que el jugador también use `Damageable`, alimentado por `CharacterStats` (vida máxima y defensa). Una sola interfaz de daño para todos.

**C2. No hay enemigos.** Sin ellos no se puede validar el combate, el loot, la XP, la durabilidad ni las runas. Todo está "hecho", pero nada está probado en condiciones reales.
**Recomendación:** hacer un `EnemyBrain` mínimo (idle → perseguir → atacar → morir) sobre `Damageable` + `NavMeshAgent`, con un `EnemyDefinition` (ScriptableObject) para el primer tipo, el *Frostbound Hunter*.

**C3. Botones de NPC que no hacen nada.** Para el jugador, que el menú ofrezca "Comprar" o "Reparar" y no pase nada se ve como un bug.
**Recomendación:** esconder esas opciones hasta que existan la tienda y la economía, o implementar `ShopService` y `RepairService` que escuchen `OptionChosen`.

**C4. No hay NavMesh.** El paquete `com.unity.ai.navigation` está instalado y no se usa. El movimiento por clic y la futura IA de enemigos dependen de él.

### 🟠 Altos: arquitectura

**A1. Clases gigantes con demasiadas responsabilidades.**

| Script | Líneas | Qué mezcla |
|---|---|---|
| `PenguinRigAnimator` | 835 | Caminar, correr, idle con 4 acciones, poses de ataque y combo, rodar, armas |
| `InventoryScreen` | 825 | Construye la UI por código, arrastrar y soltar, tooltips, stats, comparación |
| `WorldHUD` | 644 | Nombres, globos de diálogo, menú de NPC y avisos |
| `PlayerController` | 455 | Movimiento, clic, hover, recoger objetos, hablar con NPC, seguir un objetivo para atacar |

`PlayerController` debería mover al jugador y nada más. Recoger objetos y hablar con NPC van en un `PlayerInteractor`, y el hover del cursor en un `CursorTargeting`.

**A2. Las escenas se construyen con scripts de editor.** `SetupVillage.cs` (1.441 líneas), `FrostboundMenuBuilder.cs` (796) y `SetupInventory.cs` (612) generan escenas y UI enteras desde código.
- A favor: todo es reproducible.
- En contra: si editas el poblado a mano en Unity y vuelves a correr la herramienta, **pierdes los cambios**. Además, la UI hecha por código (`UIFactory`) es difícil de ajustar visualmente.
- **Recomendación:** decidir una fuente de verdad. Lo sano es usar las herramientas una sola vez para crear los prefabs y las escenas base, y desde ahí editar en Unity. Hay que marcar las herramientas de un solo uso como tales o moverlas a `Editor/Legacy`.

**A3. Duplicados que confunden.**

| Viejo | Nuevo | Acción |
|---|---|---|
| `MenuManager` | `MainMenuController` + `ClassSelectController` | Borrar el viejo y sus 3 instancias en la escena del menú |
| `PenguinAnimator` | `PenguinRigAnimator` | Borrar el viejo si ya no lo usa ningún prefab |
| `MenuButtonEffects` | `UIButtonFx` | Dejar uno solo |
| `FrostboundUI` (tokens estáticos) | `UISkin` (ScriptableObject) | Unificar en uno: hoy hay dos fuentes de colores |
| `Penguin.obj` (48k caras) | `Penguin_Rigged.fbx` | Archivar el `.obj` si nada lo referencia |
| `CameraFollow`, `OutfitTester` | — | Revisar si siguen en uso |

**A4. La persistencia está dispersa.** `MenuManager` y `ClassSelectController` escriben `PlayerPrefs["username"]`, `MainMenuController` lee `frostbound_save` y `GameSession` guarda la clase y el plumaje en memoria. No hay un único `SaveSystem`.
**Recomendación:** un `SaveData` serializable (clase, plumaje, nombre, nivel, stats, inventario, equipo, oro, seed, zona) que se escriba en JSON en `Application.persistentDataPath`. `PlayerPrefs` solo para opciones como volumen y resolución.

### 🟡 Medios: calidad y mantenimiento

- **M1. Sin namespaces ni Assembly Definitions.** Todo compila en `Assembly-CSharp`, así que cada cambio recompila todo el proyecto y es fácil crear dependencias circulares. Conviene agregar `Frostbound.Runtime.asmdef` y `Frostbound.Editor.asmdef`.
- **M2. Los tests no son tests de Unity.** `FrostboundInventoryTests`, `FrostboundWeaponTests` y `FrostboundPlayTests` son métodos estáticos que se ejecutan a través del bridge; no usan el Test Framework, que está instalado. No salen en el Test Runner ni se pueden automatizar. Conviene migrar al menos inventario, equipo y stats a pruebas EditMode con `[Test]`.
- **M3. `FrostboundBridge` ejecuta comandos desde un archivo de texto**, incluido `exec Tipo.Metodo`, cada vez que se abre el editor (`InitializeOnLoad`). Sirve para automatizar, pero debería activarse solo con un interruptor (por ejemplo, un símbolo de compilación o una opción en el menú) y no quedar siempre encendido. La carpeta `FrostboundBridge/` sí está en `.gitignore`.
- **M4. Nombres de escena inconsistentes.** `SampleScene` es el poblado y `Main menu` lleva un espacio. Conviene renombrarlas a `00_MainMenu`, `01_Village`, `02_Foothills`… y referenciarlas desde una sola clase de constantes en vez de strings sueltos (`"SampleScene"` aparece en 2 scripts).
- **M5. `FindObjectsByType` en la interacción.** Hoy solo se llama al pulsar la tecla, así que no afecta el rendimiento, pero no escala. Lo ideal es que los NPC y los objetos se registren en una lista al activarse, como ya hace `NameTag`.
- **M6. Siete llamadas a `Camera.main`.** Conviene cachear la referencia en `Awake`.
- **M7. Paquetes sin uso:** `ai.assistant`, `ai.inference`, `visualscripting`, `multiplayer.center` y `collab-proxy`. Alargan la importación y la compilación.

### 🟢 Bajos

- `SampleScene` pesa 4,9 MB en YAML, porque todo el poblado está desplegado en la escena. Conviene convertir las casas, iglús y NPC en prefabs.
- `Assets/TutorialInfo` y `Readme.asset` son de la plantilla de URP y se pueden borrar.
- `scene.unity` se puede borrar.

---

## 5. Lo que está bien hecho

- **Diseño guiado por datos.** Clases, ítems, sets, runas, outfits y mazmorras son ScriptableObjects. Agregar contenido no exige tocar código.
- **Eventos en vez de sondeo.** `Inventory.Changed`, `Equipment.Changed`, `CharacterStats.StatsChanged` y `Damageable.Damaged` permiten que la UI se actualice sin revisar el estado en cada frame.
- **Generación determinista.** `SeedUtil` y `DeterministicRng` con un flujo separado por subsistema es la forma correcta de hacerlo y permite reproducir un mapa a partir de su seed.
- **`GameplayInput.Block()`.** Los menús bloquean el control del héroe con un contador, y el estado se reinicia en cada Play.
- **La identidad visual es consistente:** shader toon propio, paleta de plumaje con `MaterialPropertyBlock` (sin duplicar materiales) y tipografías con los caracteres del español.
- **El combate tiene buen diseño de fondo:** ventana de combo, momento del impacto, crítico en el giro final, durabilidad y runas con probabilidad.
- **Git con LFS y los `.gitignore` y `.gitattributes` oficiales de Unity.** Los commits son pequeños y están bien descritos.

---

## 6. Recomendación: próximos pasos en orden

**Fase A — Limpieza (1–2 días).** Sin esto, cada fase siguiente se hace más lenta.
1. Borrar `MenuManager`, sus instancias, `scene.unity`, `TutorialInfo`, y los scripts y paquetes que no se usan.
2. Renombrar las escenas (`00_MainMenu`, `01_Village`) y centralizar sus nombres en una clase `SceneIds`.
3. Unificar `FrostboundUI` y `UISkin`.
4. Esconder las opciones de NPC que no están implementadas.

**Fase B — Cerrar el bucle de combate (lo más importante).**
1. NavMesh en el poblado y en la mazmorra generada (`NavMeshSurface.BuildNavMesh()` al terminar de generar).
2. Unificar la vida: el jugador usa `Damageable`.
3. `EnemyDefinition` + `EnemyBrain` → *Frostbound Hunter*.
4. Muerte del jugador y reaparición en el poblado.
5. Tabla de loot por enemigo, usando `DeterministicRng`.
6. Conectar `ZoneExit` → escena `02_Foothills` con `DungeonGenerator` y spawns de enemigos por sala.

**Fase C — Economía y servicios.**
Oro, `ShopService` (comprar y vender), `RepairService` (usa la durabilidad que ya existe) y un `SaveSystem` en JSON para que "Continuar" funcione.

**Fase D — Refactor por partes**, a medida que se toque cada archivo: separar `PlayerController`, `WorldHUD` e `InventoryScreen`; agregar asmdef; migrar los tests a EditMode.

**Fase E — Contenido del vertical slice:** Rocco, un mini-jefe al final de la mazmorra, audio y la pasada de arte a la mazmorra (reemplazar los cubos por un kit modular).

> **Regla para no volver a desviarse:** no agregar más armas, ítems ni ropa hasta que exista un enemigo que pueda matar al jugador. Hoy hay 18 armas y 0 enemigos.

---

## 7. Métricas

| Métrica | Valor |
|---|---|
| Scripts de juego | 64 (≈ 9.450 líneas) |
| Scripts de editor | 20 (≈ 6.875 líneas) |
| Scripts con más de 500 líneas | 4 de juego, 5 de editor |
| ScriptableObjects de datos | ≈ 90 |
| Escenas | 3 (2 en Build) |
| NPC en el poblado | 14 |
| Enemigos | 0 |
| Tests del Test Framework | 0 |
| `Debug.Log` en runtime | 14 |
