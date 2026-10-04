# Plan de mejora y optimización — Frostbound

**Basado en:** Auditoría técnica del 28 de septiembre de 2026 (`Docs/Auditoria_Frostbound.md`)
**Proyecto:** `My project (2)` · Unity 6000.5.7f1 · URP · Input System
**Objetivo:** eliminar duplicados y deuda técnica, dejar una arquitectura sólida y cerrar el bucle de juego (combate → loot → poblado) hasta un vertical slice jugable.

Este documento se ejecuta **en orden**. Cada tarea tiene un ID, los archivos afectados, los pasos y un criterio de aceptación verificable. Una tarea no se da por terminada hasta cumplir su criterio.

---

## 0. Reglas de ejecución

1. **Una rama por fase:** `fase/1-limpieza`, `fase/2-arquitectura`, etc. Al terminar la fase, se hace merge a `main` y se crea un tag (`v0.2-limpieza`…).
2. **Un commit por tarea**, con el ID en el mensaje: `F1.3 Migra NPC de PenguinAnimator a PenguinRigAnimator`.
3. **Antes de borrar cualquier script o asset**, buscar su GUID en escenas, prefabs y assets. Si tiene referencias, se migran primero y se borra después. Nunca queda un *Missing Script*.
4. **Después de cada tarea**, correr la **prueba de humo**: Menú → Nueva partida → elegir clase → Poblado → caminar, atacar un muñeco, abrir el inventario, hablar con un NPC. Cero errores en la consola.
5. **Congelamiento de contenido:** hasta terminar la Fase 3 no se agregan armas, ítems, ropa ni NPC nuevos. Solo sistemas.
6. **Las escenas se editan en Unity.** Las herramientas de editor que generan escenas se usan solo para crear prefabs (ver F2.9).

### Definición de terminado (aplica a todas las tareas)

- [ ] Compila sin errores ni advertencias nuevas.
- [ ] La prueba de humo pasa.
- [ ] Los tests EditMode pasan (a partir de F0.3).
- [ ] No quedan referencias rotas (*Missing Script* o *Missing Prefab*).
- [ ] El commit tiene el ID de la tarea.

---

## Resumen de fases

| Fase | Nombre | Resultado | Esfuerzo |
|---|---|---|---|
| **F0** | Red de seguridad | Punto de retorno y tests base | 0,5 día |
| **F1** | Limpieza y duplicados | Un solo sistema por responsabilidad | 2–3 días |
| **F2** | Arquitectura | Código modular, asmdef, una sola vida | 4–6 días |
| **F3** | Bucle de combate | Enemigos, muerte, loot, zona Foothills | 6–8 días |
| **F4** | Economía y guardado | Oro, tienda, reparación, "Continuar" funcional | 4–5 días |
| **F5** | Calidad y rendimiento | Tests automáticos, perfilado, build estable | 2–3 días |
| **F6** | Vertical slice | Rocco, mini-jefe, audio, arte de mazmorra | 5–7 días |

Dependencias: F0 → F1 → F2 → F3 → F4. F5 va en paralelo desde F2. F6 empieza cuando termina F4.

---

## F0 — Red de seguridad

| ID | Tarea | Pasos | Aceptación |
|---|---|---|---|
| **F0.1** | Punto de retorno | Commit de todo lo pendiente. Tag `v0.1-pre-refactor`. Confirmar que Git LFS está instalado en la máquina (`git lfs install`). | `git status` limpio, el tag existe |
| **F0.2** | Build de referencia | Hacer una build de Windows y guardar capturas del menú, la selección de clase y el poblado. Anotar los FPS en el poblado. | Existen la build y las capturas en `Docs/baseline/` |
| **F0.3** | Tests mínimos | Crear `Assets/Tests/EditMode/` con su asmdef (referencia a `nunit` y al Test Framework). Portar a `[Test]` los casos de `FrostboundInventoryTests` y `FrostboundWeaponTests`: agregar y quitar del inventario, equipar y desequipar, bonus de set, durabilidad, `MaxHealth`/`MaxMana`. | Test Runner → EditMode: todo en verde |

---

## F1 — Limpieza y eliminación de duplicados

**Meta:** que cada responsabilidad tenga **un solo** dueño en el código.

### F1.1 Menú viejo → menú nuevo
- **Eliminar:** `Scripts/MenuManager.cs`, sus 3 instancias y los `Canvas` viejos en `Main menu.unity`, y el `Player` sobrante de esa escena.
- **Rescatar antes de borrar:** la lógica de volumen, resolución y pantalla completa de `MenuManager` (`SetVolume`, `SetupResolutions`, `StepResolution`, `ApplyResolution`, `SetFullscreen`). Moverla a `Scripts/Core/GameSettings.cs`, que la usará el panel de Opciones en F4.6.
- **Eliminar:** `Scenes/scene.unity`, que es la escena vieja.
- **Eliminar:** la escritura de `PlayerPrefs["username"]` en `ClassSelectController`. El nombre vive en `GameSession`.
- ✅ La escena del menú solo tiene `MainMenuCanvas`, `ClassSelectCanvas`, la cámara, la cámara de preview y el `EventSystem`. No existe ninguna referencia a `MenuManager`.

### F1.2 Efectos de botón: `MenuButtonEffects` → `UIButtonFx`
- Reemplazar los componentes en `Main menu.unity` y borrar `MenuButtonEffects.cs`.
- ✅ Todos los botones usan `UIButtonFx` y se comportan igual (borde `ice` y escala 1.03).

### F1.3 Animador del pingüino: `PenguinAnimator` → `PenguinRigAnimator`
- `PenguinAnimator` y `Penguin.obj` siguen en uso en `SampleScene`.
- Reemplazar cada pingüino que use el `.obj` por una instancia de `Prefabs/Penguin_Rigged.prefab` con su plumaje y outfit, manteniendo su posición y su `VillagerNPC`.
- Borrar `Scripts/PenguinAnimator.cs` y mover `Penguin.obj` a `Assets/_Archive/` (fuera del build). Pasadas dos semanas sin problemas, se borra.
- ✅ No quedan referencias al GUID de `PenguinAnimator` ni de `Penguin.obj`.

### F1.4 Avisos duplicados: `GameHUD.ShowMessage` y `WorldHUD.Toast`
- Crear `Scripts/UI/NotificationService.cs`, estático o singleton de escena: `Notify(string text, NotificationKind kind)`.
- `GameHUD` y `WorldHUD` dejan de tener cada uno su toast. Los dos llaman al servicio y se usa un solo prefab de aviso.
- ✅ Hay un solo lugar donde se dibujan los avisos y ninguno se superpone con otro.

### F1.5 Tokens visuales: `FrostboundUI` y `UISkin`
- **Una sola fuente de verdad:** `FrostboundUI` (estático) guarda **colores y medidas**, como pide la spec del menú. `UISkin` (ScriptableObject) solo guarda **referencias a assets**: fuentes TMP, sprites 9-slice e iconos.
- Quitar de `UISkin` los campos de color que repiten los de `FrostboundUI` y reemplazar sus usos en `GameHUD`, `InventoryScreen`, `UIFactory` y `SetupInventory`.
- ✅ Buscar un hex de la paleta (por ejemplo `BFEAFF`) en `Scripts/` devuelve un solo archivo.

### F1.6 Herramientas de editor duplicadas o viejas
| Herramienta | Acción |
|---|---|
| `SetupMainMenu.cs` (*Tools/Frostbound/Setup Main Menu*) | **Eliminar.** La reemplaza `FrostboundMenuBuilder` |
| `SetupPenguinPlayer.cs` (*Tools/Penguin/Setup Penguin as Player*) | **Eliminar** si `SetupPenguinWardrobe` ya arma al jugador con rig. Verificarlo antes |
| `SetupCharacterClasses.cs` | Mover a `Editor/Legacy`. Las clases ya existen como assets |
| `SnowTextureGenerator.cs`, `PropTexturesGenerator.cs` | Mover a `Editor/Legacy`. Las texturas ya están generadas |
| `SetupSteve.cs` (*Agregar a Steve en el bosque*) | Revisar si sigue vigente. Si no, a `Legacy` |
| `FrostboundCapture.cs` (*Debug/…*) | Mantener, dentro del submenú `Tools/Frostbound/Debug` |
- Todos los menús quedan bajo `Tools/Frostbound/…`. Desaparece `Tools/Penguin/`.
- ✅ El menú Tools muestra una sola raíz ordenada y ninguna herramienta repite a otra.

### F1.7 Escenas: nombres y referencias
- Renombrar `Main menu.unity` → `00_MainMenu.unity` y `SampleScene.unity` → `01_Village.unity`.
- Crear `Scripts/Core/SceneIds.cs` con las constantes (`MainMenu`, `Village`, `Foothills`).
- Reemplazar los strings sueltos (`"SampleScene"` en `ClassSelectController` y `ZoneExit`) por `SceneIds`.
- Actualizar Build Settings.
- ✅ No quedan strings de nombres de escena fuera de `SceneIds`.

### F1.8 Plantilla y paquetes sin uso
- Borrar `Assets/TutorialInfo/` y `Assets/Readme.asset`.
- Quitar del `manifest.json`: `com.unity.ai.assistant`, `com.unity.ai.inference`, `com.unity.visualscripting`, `com.unity.multiplayer.center` y `com.unity.collab-proxy`. **Mantener** `ai.navigation`, que se usa en F3.
- ✅ El proyecto compila y la prueba de humo pasa.

### F1.9 Herramientas de depuración fuera del juego final
- Mover `ProgressionDebug.cs` y `OutfitTester.cs` a `Scripts/Debug/` y envolverlos en `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- En `01_Village`, agruparlos en un objeto `[Debug]`.
- ✅ En una build de release, estos scripts no están compilados.

### F1.10 FrostboundBridge bajo interruptor
- Envolver `FrostboundBridge.cs` en `#if FROSTBOUND_BRIDGE`. El símbolo se activa o desactiva desde `Tools/Frostbound/Debug/Bridge ON-OFF` (modifica los Scripting Define Symbols).
- ✅ Con el símbolo apagado, el bridge no arranca al abrir el editor.

### F1.11 Opciones de NPC honestas
- Mientras no exista F4, `NPCInteractable` solo muestra **Hablar**. Las demás opciones quedan ocultas mediante un campo por NPC (`services`) que se activará en F4.5.
- ✅ Ningún NPC ofrece una opción que no haga nada.

**Cierre de F1:** tag `v0.2-limpieza`. Resultado esperado: entre 1.500 y 2.000 líneas menos, 1 escena menos y 5 paquetes menos.

---

## F2 — Arquitectura

**Meta:** módulos claros, responsabilidades pequeñas y un solo modelo de vida y daño.

### F2.1 Estructura de carpetas
```
Assets/
├─ _Archive/                 (fuera del build, temporal)
├─ Frostbound/               (UI: fuentes, iconos, sprites, fondos)
├─ Data/                     (ScriptableObjects: Classes, Items, Enemies, Loot, Shops, Outfits, Procgen, UI)
├─ Models/  Materials/  Prefabs/  Shaders/  Textures/
├─ Scenes/                   00_MainMenu · 01_Village · 02_Foothills
├─ Scripts/
│  ├─ Core/        GameSession, SceneIds, GameSettings, SaveSystem, Registry<T>
│  ├─ Player/      PlayerMotor, PlayerInputRouter, PlayerInteractor, CursorTargeting, PlayerDodge
│  ├─ Camera/      IsometricCamera (antes CameraFollow)
│  ├─ Combat/      Damageable, DamageInfo, StatusEffects, PlayerCombat, Projectile, LightningArc
│  ├─ Characters/  CharacterClass, CharacterStats, Appearance, Outfit/*, Rig/*
│  ├─ Enemies/     EnemyDefinition, EnemyBrain, EnemySpawner      (F3)
│  ├─ Items/       Inventory, Equipment, ItemDefinition, ItemStack, WorldItem, Loot/*
│  ├─ Economy/     Wallet, ShopService, RepairService             (F4)
│  ├─ World/       NPCInteractable, VillagerNPC, NameTag, ZoneExit, FlickerLight
│  ├─ Procgen/     (igual que ahora)
│  ├─ UI/          Menu/, HUD/, Inventory/, Common/
│  └─ Debug/
├─ Editor/        Tools/, Legacy/
└─ Tests/         EditMode/, PlayMode/
```
- Mover los archivos **desde Unity**, arrastrando en la ventana Project, para conservar los `.meta` y los GUID. Nunca desde el explorador de archivos.
- ✅ Nada queda suelto en la raíz de `Scripts/`.

### F2.2 Assembly Definitions y namespaces
- `Frostbound.Runtime.asmdef` (Scripts/), `Frostbound.Editor.asmdef` (Editor/, solo editor) y `Frostbound.Tests.EditMode.asmdef` / `Frostbound.Tests.PlayMode.asmdef`.
- Namespaces por carpeta: `Frostbound.Core`, `Frostbound.Player`, `Frostbound.Combat`, etc.
- ✅ Cambiar un script de UI no recompila los tests ni las herramientas de editor.

### F2.3 Un solo sistema de vida *(crítico)*
- **Hoy:** `Damageable` (con estados congelado, veneno, regeneración y eventos) y `CharacterStats` (`currentHealth`, `TakeDamage`, `Died`) llevan la vida por separado.
- **Después:**
  - `Damageable` es la **única** fuente de vida para jugador, enemigos, muñecos y objetos rompibles.
  - `CharacterStats` solo **calcula** stats: `MaxHealth`, `Defense`, `Damage`, `MoveSpeed`, `MaxMana`, etc. Al cambiar (`StatsChanged`) actualiza `Damageable.maxHealth`.
  - Se eliminan de `CharacterStats` los campos `currentHealth`, `TakeDamage`, `Die` y el evento `Died`. El maná se queda en `CharacterStats`.
  - Los efectos de estado (fuego, veneno, escarcha, empuje) salen de `Damageable` a `StatusEffects.cs`, que usan por igual el jugador y los enemigos.
  - `GameHUD` lee la vida del `Damageable` del jugador.
- **Tests:** daño básico, defensa, muerte (una sola vez), curación con poción, veneno por tick y el recálculo de vida máxima al equipar.
- ✅ `grep currentHealth` solo aparece en `Damageable`. Los tests pasan.

### F2.4 Dividir `PlayerController` (455 líneas)
| Nuevo componente | Responsabilidad |
|---|---|
| `PlayerMotor` | Moverse con `NavMeshAgent` (WASD = dirección, clic = destino). Nada más |
| `PlayerInputRouter` | Traducir la entrada: clic en el suelo → mover; clic en enemigo → atacar; clic en NPC u objeto → interactuar. Respeta `GameplayInput.Blocked` |
| `CursorTargeting` | Un raycast por frame desde una cámara cacheada. Expone `Hovered` (NPC, ítem o enemigo) y controla el resaltado |
| `PlayerInteractor` | Recoger objetos y hablar con NPC usando `Registry<T>` en vez de `FindObjectsByType` |
- ✅ Ningún componente del jugador supera las 200 líneas y el comportamiento es igual al de antes.

### F2.5 Registros y cámara cacheada
- Crear `Registry<T>`: los objetos se agregan en `OnEnable` y se quitan en `OnDisable`. Lo usan `WorldItem`, `NPCInteractable` y, en F3, `EnemyBrain`.
- Reemplazar las 7 llamadas a `Camera.main` por una referencia que se cachea una vez (`CameraRig.Main`).
- ✅ `FindObjectsByType` y `Camera.main` no aparecen en código que se ejecute durante el juego.

### F2.6 Dividir `WorldHUD` (644 líneas)
- `NameTagLayer` (nombres), `SpeechBubbleLayer` (globos de diálogo) y `NpcMenuPanel` (opciones del NPC). Los avisos ya salieron a `NotificationService` en F1.4.
- ✅ Cada pieza tiene menos de 250 líneas.

### F2.7 Dividir `InventoryScreen` (825 líneas)
- `InventoryGridView`, `EquipmentPanelView`, `StatsPanelView`, `ItemTooltipView` y `ItemDragController`. `InventoryScreen` queda como coordinador de unas 150 líneas.
- Diseñar las vistas pensando en reutilizarlas en la tienda (F4.2): `InventoryGridView` debe poder mostrar cualquier `Inventory`.
- ✅ La tienda podrá armarse con las mismas vistas, sin copiar código.

### F2.8 Ordenar `PenguinRigAnimator` (835 líneas)
- Separarlo en `RigLocomotion` (caminar, correr, rodar), `RigIdleActions` (Search, FootTap, Shrug) y `RigCombatPoses` (combo y armas), coordinados por `PenguinRigAnimator`.
- **Prioridad baja:** hacerlo después de F3.2, cuando los enemigos también usen el rig y se sepa qué necesitan.
- ✅ Los NPC y los enemigos pueden usar solo la locomoción sin cargar la lógica de combate.

### F2.9 Política de herramientas de editor
- Convertir las piezas del poblado (casas, iglús, hoguera, empalizada, puestos) y cada NPC en **prefabs** en `Prefabs/Village/`.
- `Construir Poblado` pasa a llamarse `Regenerar Poblado (destructivo)` y pide confirmación con un diálogo.
- A partir de ahí, `01_Village` se edita a mano.
- ✅ `01_Village.unity` pesa bastante menos que los 4,9 MB actuales, porque las piezas pasan a ser instancias de prefabs.

**Cierre de F2:** tag `v0.3-arquitectura`.

---

## F3 — Bucle de combate *(la fase que convierte el proyecto en un juego)*

### F3.1 NavMesh
- `NavMeshSurface` en `01_Village`, con el NavMesh horneado.
- `DungeonGenerator`: al terminar de generar, llama a `NavMeshSurface.BuildNavMesh()` en su raíz.
- `PlayerMotor` usa `NavMeshAgent`. Rodar sigue usando la física durante su duración y después vuelve a sincronizar el agente.
- ✅ El clic lleva al jugador alrededor de obstáculos, y en la mazmorra no se traba en las esquinas.

### F3.2 Enemigos
- `EnemyDefinition` (ScriptableObject): nombre, modelo o prefab, plumaje y corrupción, vida, daño, velocidad, rango de visión, rango de ataque, tiempo de preparación del ataque, XP, `LootTable`.
- `EnemyBrain`: máquina de estados **Idle → Alerta → Perseguir → Atacar (con aviso visual previo) → Recuperarse → Morir**. Usa `NavMeshAgent` y `Damageable`.
- `EnemySpawner`: genera grupos en un radio a partir de la seed de la sala.
- **Primer enemigo:** *Frostbound Hunter*. Es el pingüino rigged con plumaje apagado, cristales de hielo oscuro (outfit) y un arma de la base existente.
- ✅ Cinco Hunters persiguen y atacan al jugador, reaccionan a los golpes y a las runas (se congelan, arden, salen empujados) y mueren.

### F3.3 Daño al jugador, muerte y reaparición
- Los ataques enemigos usan `Damageable` y respetan la defensa del equipo y la invulnerabilidad al rodar.
- Al morir: cámara lenta breve y pantalla "Has caído". El jugador reaparece en el poblado y pierde un 10 % de durabilidad del equipo (esto le da uso al herrero).
- ✅ El jugador puede morir, y reaparecer no deja errores ni estados colgados (bloqueo de input, menús abiertos).

### F3.4 Loot
- `LootTable` (ScriptableObject): entradas con peso, cantidad mínima y máxima, rareza y probabilidad de "nada".
- Las tiradas usan `DeterministicRng` con flujo `loot` a partir de la seed del piso.
- Al morir, el enemigo suelta `WorldItem`s y da XP con `CharacterStats.AddExperience`.
- Test EditMode: la misma seed produce el mismo loot.
- ✅ Matar enemigos da XP y objetos que se pueden recoger y equipar.

### F3.5 Sensación de combate
- Ya existen el hitstop en crítico y el combo. Hay que agregar: destello blanco en el enemigo golpeado, números de daño flotantes (usando `NotificationService` o una capa propia), un pequeño temblor de cámara al golpear fuerte y un sonido de impacto provisional.
- ✅ Golpear se siente distinto a fallar, incluso con los modelos provisionales.

### F3.6 Zona Foothills
- Crear la escena `02_Foothills`: una entrada hecha a mano, `DungeonGenerator` con `Frozen_Castle`, `EnemySpawner` por sala (salas `Normal`), cofre en la sala `Treasure` y una sala `Boss` con un Hunter élite provisional.
- `ZoneExit` del poblado → `02_Foothills`, y la salida de Foothills → `01_Village`. `GameSession` conserva el estado del jugador entre escenas.
- ✅ Recorrido completo: Poblado → Foothills → limpiar salas → élite → volver al poblado con loot.

**Cierre de F3:** tag `v0.4-combate`. **Primer momento en que el proyecto es un juego.**

---

## F4 — Economía, servicios y guardado

### F4.1 Oro
- `Wallet` en el jugador. Los enemigos sueltan oro según su `LootTable`.
- `ItemDefinition` recibe `buyPrice` y `sellPrice` (este último se calcula a partir de la rareza si queda en 0).
- El HUD y el inventario muestran el oro.

### F4.2 Tienda — *Mercader Tico*
- `ShopDefinition` (ScriptableObject): stock, precios y reabastecimiento.
- `ShopService` escucha `NPCInteractable.OptionChosen` (Comprar y Vender).
- La UI reutiliza `InventoryGridView` (F2.7): dos grillas, la del jugador y la de la tienda.
- ✅ Se puede comprar, vender, y el oro se actualiza bien. Hay tests del cálculo de precios.

### F4.3 Reparación — *Herrera Brunna*
- `RepairService`: el costo depende de la durabilidad que falta y de la rareza. Permite "Reparar todo" o reparar pieza por pieza.
- ✅ La durabilidad que se perdió en combate o al morir se recupera pagando oro.

### F4.4 Sistema de guardado
- `SaveData` (serializable): versión del formato, clase, plumaje, nombre, nivel, XP, stats repartidos, inventario (id + cantidad + durabilidad), equipo, oro, `worldSeed`, última zona.
- `SaveSystem`: guarda en JSON en `Application.persistentDataPath/frostbound_save.json`, con escritura atómica (primero a `.tmp` y luego renombra).
- Autoguardado al cambiar de zona y al salir al menú. "Continuar" carga ese archivo.
- `PlayerPrefs` queda **solo** para opciones (volumen, resolución, pantalla completa).
- `ItemDatabase` resuelve los ids al cargar.
- Test: guardar → cargar → comparar = idéntico.
- ✅ Cerrar el juego y darle a "Continuar" deja al jugador donde estaba, con su equipo y su oro.

### F4.5 Activar las opciones de NPC
- En cada `NPCInteractable` se configura qué servicios ofrece (`Talk`, `Shop`, `Repair`). Se habilitan Tico (Tienda) y Brunna (Reparar).

### F4.6 Panel de Opciones y Créditos
- Opciones: volumen, resolución y pantalla completa, usando `GameSettings` (F1.1). Créditos: texto con las licencias OFL de las fuentes.

**Cierre de F4:** tag `v0.5-economia`.

---

## F5 — Calidad y rendimiento *(en paralelo desde F2)*

| ID | Tarea | Aceptación |
|---|---|---|
| **F5.1** | Tests EditMode: inventario, equipo, stats, daño, estados, loot determinista, precios, guardado | Al menos 40 tests en verde |
| **F5.2** | Test PlayMode de humo: carga el menú → selecciona una clase → carga el poblado → avanza 60 frames → carga Foothills. Falla si hay errores en la consola | Pasa en menos de 60 s |
| **F5.3** | Logging: envolver `Debug.Log` en `Log.Info/Warn/Error` con `[Conditional("FROSTBOUND_LOG")]` | Una build de release no escribe logs de juego |
| **F5.4** | Rendimiento: perfilar el poblado y Foothills con 20 enemigos. GPU Instancing en `FrostboundToon`, static batching en la escenografía, pooling de proyectiles, números de daño y `WorldItem` | 60 FPS estables en tu PC; sin picos de GC al atacar |
| **F5.5** | Asignaciones de memoria: sin `new List`, LINQ ni concatenación de strings en `Update` (usar el `_scratch` que ya existe en `PlayerCombat`) | El Profiler muestra 0 B de GC por frame en combate normal |

---

## F6 — Contenido del vertical slice

*(Aquí se levanta el congelamiento de contenido.)*

- **F6.1** Rocco Vendaval junto a la hoguera, según la spec §6 (ya existen su material y su modelo).
- **F6.2** Mini-jefe de Foothills: *Corrupted Warden*, con 2 fases, ataques con aviso visual y un loot garantizado.
- **F6.3** Audio: música del poblado y de la zona, pasos en nieve, golpes, UI. Un `AudioService` con mezclador (Music, SFX, UI) conectado a Opciones.
- **F6.4** Kit modular de mazmorra: suelos, muros, esquinas y puertas en el estilo del juego, que reemplacen los cubos de `DungeonGenerator.AddBox`.
- **F6.5** Clases: pulir **Caballero** al 100 % (una habilidad activa con maná) antes de dar a las otras tres su habilidad propia.
- **F6.6** Build final del slice y prueba con alguien externo (sesión de 20 minutos, anotando dónde se traba o se aburre).

**Cierre:** tag `v1.0-vertical-slice`.

---

## Anexo A — Inventario de eliminaciones y fusiones

| Elemento | Acción | Tarea |
|---|---|---|
| `MenuManager.cs` + instancias + Canvas viejos | Eliminar (rescatar opciones) | F1.1 |
| `Scenes/scene.unity` | Eliminar | F1.1 |
| `PlayerPrefs["username"]` | Eliminar | F1.1 |
| `MenuButtonEffects.cs` | Fusionar en `UIButtonFx` | F1.2 |
| `PenguinAnimator.cs` | Eliminar tras migrar los NPC | F1.3 |
| `Penguin.obj` | Archivar y luego eliminar | F1.3 |
| Toast de `GameHUD` + Toast de `WorldHUD` | Fusionar en `NotificationService` | F1.4 |
| Colores en `UISkin` | Fusionar en `FrostboundUI` | F1.5 |
| `SetupMainMenu.cs`, `SetupPenguinPlayer.cs` | Eliminar | F1.6 |
| `SetupCharacterClasses`, `SnowTextureGenerator`, `PropTexturesGenerator`, `SetupSteve` | Mover a `Editor/Legacy` | F1.6 |
| `TutorialInfo/`, `Readme.asset` | Eliminar | F1.8 |
| 5 paquetes sin uso | Quitar | F1.8 |
| Vida en `CharacterStats` | Fusionar en `Damageable` | F2.3 |
| `FindObjectsByType` en la interacción | Reemplazar por `Registry<T>` | F2.5 |
| 7 × `Camera.main` | Reemplazar por la cámara cacheada | F2.5 |
| `frostbound_save` (clave huérfana) | Reemplazar por `SaveSystem` | F4.4 |
| Tests caseros por bridge | Migrar al Test Framework | F0.3 / F5.1 |

## Anexo B — Seguimiento

| Fase | Estado | Tag | Fecha |
|---|---|---|---|
| F0 Red de seguridad | 🟡 F0.1 hecha · F0.2 y F0.3 pendientes | `v0.1-pre-refactor` | 28-09-2026 |
| F1 Limpieza | ✅ Terminada (rama `fase/1-limpieza`, sin merge) | `v0.2-limpieza` | 28-09-2026 |
| F2 Arquitectura | ⬜ Pendiente | `v0.3-arquitectura` | |
| F3 Combate | ⬜ Pendiente | `v0.4-combate` | |
| F4 Economía y guardado | ⬜ Pendiente | `v0.5-economia` | |
| F5 Calidad | ⬜ Pendiente | — | |
| F6 Vertical slice | ⬜ Pendiente | `v1.0-vertical-slice` | |

## Anexo C — Riesgos

| Riesgo | Mitigación |
|---|---|
| Perder referencias al mover o borrar archivos | Mover siempre desde Unity, buscar el GUID antes de borrar, commit por tarea |
| Que al unificar la vida se rompan el HUD o las pociones | Tests de F2.3 escritos **antes** del cambio |
| Que el NavMesh choque con la física de rodar | Desactivar `updatePosition` del agente durante el rodar y resincronizarlo con `Warp` al terminar |
| Que regenerar el poblado borre los cambios manuales | Diálogo de confirmación y prefabs (F2.9) |
| El alcance vuelve a crecer | Congelamiento de contenido hasta el final de F3 (regla 5) |

## Anexo D — Registro de ejecución de F1 (28-09-2026)

Rama `fase/1-limpieza`, 10 commits sobre `v0.1-pre-refactor`. Balance en `Scripts/` y `Editor/`: +478 / −1.110 líneas.

| Tarea | Resultado |
|---|---|
| F1.1 | Se eliminaron `MenuManager`, sus 3 instancias y los 3 Canvas viejos del menú, además de `scene.unity`. Volumen, resolución y pantalla completa pasan a `Core/GameSettings` (con guardado). Se quitó `PlayerPrefs["username"]`. |
| F1.2 | Se eliminó `MenuButtonEffects`: solo lo usaban los Canvas viejos. |
| F1.3 | **Cambio respecto al plan:** `PenguinAnimator` no duplicaba a `PenguinRigAnimator` (uno mueve el cuerpo entero y el otro los huesos). El duplicado real era el balanceo del cuerpo, programado dos veces: en `PenguinAnimator` (héroe) y en `VillagerNPC` (aldeanos). Ahora los dos usan `Characters/PenguinBodySway`. Se conservó el GUID, así que el Player no perdió su configuración. |
| F1.4 | `Notifications.Show()` es el único punto de avisos. Se quitaron el toast de `WorldHUD` y 2 búsquedas `FindAnyObjectByType<GameHUD>`. Se corrigió una suscripción sin quitar (`ExperienceGained`). |
| F1.5 | La paleta ya estaba centralizada en `FrostboundUI`. Se movió `StatColor` y `WorldHUD` pasó a usar el `UISkin` en vez de sus 7 referencias propias a fuentes y sprites. |
| F1.6 | Los generadores de texturas y de clases pasaron a `Editor/Legacy`, en el menú `Tools/Frostbound/Legacy`. Se eliminaron `SetupMainMenu` y `SetupPenguinPlayer`. `SetupSteve` se mantiene porque lo usa `SetupVillage`. |
| F1.7 | Las escenas ahora son `00_MainMenu` y `01_Village` (mismo GUID). `SceneIds` reemplaza 9 strings sueltos. Build Settings corregido. |
| F1.8 | Se quitaron `visualscripting`, `multiplayer.center` y `collab-proxy`, más `TutorialInfo/` y `Readme.asset`. **Se mantuvieron** `ai.assistant` y `ai.inference`: son una herramienta de trabajo del usuario y queda a su decisión. |
| F1.9 | `ProgressionDebug` y `OutfitTester` están en `Scripts/Debug` y se desactivan fuera del editor y de las builds de desarrollo. |
| F1.10 | El bridge solo arranca con `FROSTBOUND_BRIDGE`. Se activa o desactiva en `Tools/Frostbound/Debug/Bridge de automatización` (ahora está activo). |
| F1.11 | Los NPC solo muestran opciones implementadas. **Corrección a la auditoría:** Reparar sí funcionaba (gratis). Se ocultan Comprar y Vender. |
| F1.3b | Se corrigió el temblor al arrancar y al frenar. La fase del paso se calculaba como `Time.time × frecuencia × velocidad`, así que cada cambio de velocidad hacía saltar la fase y el cuerpo vibraba. Ahora la fase se acumula frame a frame. Medido con `SwaySampler`: el salto máximo de altura bajó de 0,357 m a 0,008 m, el del contoneo de 19,2° a 0,3°, y los cambios bruscos de 45 a 0. (El error venía del `PenguinAnimator` original.) |
| Extra | Se agregaron `Editor/Checks/SmokeTest` (prueba de humo y detección de scripts faltantes) y se limpiaron las advertencias del compilador. |

**No se tocó (con motivo):**
- `CameraFollow` es la única cámara, no un duplicado. Se reorganiza en F2.1.
- `Penguin.obj` lo usa la estatua del fundador en la fuente del poblado.
- **Rocco ya existe como "Steve"**, el músico de la fogata del bosque (`SetupSteve`). No está pendiente.

**Verificación:** compila sin errores ni advertencias, hay 0 scripts faltantes en las 2 escenas y la prueba de humo pasa (menú → selección de clase → poblado → HUD → inventario → herrera → reparar).
**Pendiente conocido (F3.1, sin NavMesh):** si el héroe arranca lejos de Steve, se traba con el tronco y la fogata y cancela el acercamiento.
