---
name: d2r-ui
description: Implementa la interfaz de Frostbound con la estética de Diablo II Resurrected ("gótico de escarcha") y el fondo 3D de la mansión helada del Frost en el menú. Úsala al crear o rediseñar cualquier pantalla, panel, botón, HUD, inventario, menú, pantalla de carga o el decorado del menú, o cuando el usuario diga /d2r-ui, "estilo Diablo", "UI D2R" o "mansión embrujada". Acepta como argumento el ID de una tarea (p. ej. "UI-4" o "M-2"); sin argumento, sigue con la próxima tarea pendiente.
---

# UI estilo D2R + mansión helada del Frost

La investigación completa está en `Docs/Investigacion_UI_Diablo2R.md`. Esta skill la convierte en reglas y tareas. El avance se registra en `PROGRESO.md`, en esta misma carpeta.

## Al invocarla

1. Leer `PROGRESO.md` y confirmar que la rama actual es `feature/campana-completa` (`git branch --show-current`). Si es otra, avisar antes de tocar nada.
2. Elegir la tarea: la del argumento o, si no hay, la primera ⬜ cuyas dependencias estén ✅. Decirle al usuario cuál es y qué va a cambiar.
3. Leer **antes de editar** los archivos que la tarea nombra. Este código cambia rápido y los números de línea de este documento no se mantienen.
4. Implementar siguiendo las reglas de abajo.
5. Verificar según el criterio de la tarea y la sección "Verificación".
6. Marcar la tarea en `PROGRESO.md` (✅, fecha y nota breve) y proponer el commit `UI-n <descripción en español>` o `M-n …`. **No commitear sin que el usuario lo pida.**

## Reglas duras

- **Nada de recursos de Blizzard.** Ni capturas, ni sprites extraídos, ni el logo, ni la fuente **Exocet** (es comercial y la asocian a Diablo). Se copian los principios, no los assets.
- **Sin tecnologías nuevas.** uGUI + TextMeshPro + URP + Shader Graph + Sprite Atlas, todo ya instalado. No usar UI Toolkit, DOTween, UIParticle ni Addressables. Si algo parece imprescindible, preguntar antes.
- **Un solo origen de tokens:** `Assets/Scripts/UI/FrostboundUI.cs`. `FrostUI` y los componentes leen de ahí. `UISkin` solo guarda referencias a assets (fuentes, sprites, iconos). Prohibido escribir un hex suelto en otro archivo.
- **Seguir el estilo del repositorio:** comentarios en español al inicio de cada clase, explicando qué hace y para qué. Nombres en inglés para el código y en español para lo visible. UI construida por código como hoy (`FrostUI.Img`, `FrostUI.Text`). Las herramientas de editor van en `Tools/Frostbound/…` y se pueden ejecutar varias veces.
- **Compatibilidad con lo que ya hay:** foco con mando y teclado (`UIDeviceFocus`, `FrostButton`), `GameplayInput.Block()` en los menús, `ScreenAdapter` y `UltrawideFrame` para resoluciones, `Notifications` para avisos y `AudioManager` para sonidos de UI.
- **La identidad de Frostbound manda:** pingüinos, noche helada, cristal del Frost. Lo oscuro es frío y corrupción, **no gore**. Lo cálido (fuego, velas) se usa poco y a propósito.

## Dirección de arte: "gótico de escarcha"

D2R aporta: oscuridad, marcos de metal envejecido, **sin esquinas redondeadas**, animación sobria, gran contraste fuego/sombra, la escena 3D como protagonista, botón principal grande abajo al centro y la rareza codificada por color.

Frostbound aporta: azul noche, hielo y cristal. Los marcos son de **hierro forjado con escarcha en las aristas**, las placas de piedra azulada o cuero casi negro, el **oro viejo** solo para lo importante y el **cian del Frost** para lo mágico o corrupto.

### Tokens (añadir a `FrostboundUI` en UI-1/UI-2)

| Token | Hex | Uso |
|---|---|---|
| `Abyss` | `#05080F` | Viñeta, fondo de tooltip |
| `Night` | `#0A1222` | Fondo de panel (ya existe como `Bg`) |
| `Slate` | `#141C2B` | Placa de botón y panel |
| `Iron` / `IronLight` | `#4A5568` / `#9AA8BC` | Marco / arista iluminada |
| `Frost` / `FrostDeep` | `#BFEAFF` / `#3A8FC4` | Foco y magia / corrupción y maná |
| `OldGold` / `OldGoldLight` | `#B8923E` / `#E8C877` | Botón principal y títulos / hover |
| `Bone` / `Ash` | `#D9D2C3` / `#8A93A3` | Texto principal / secundario |
| `Blood` | `#B3261E` | Vida y peligro |

**Tipografía** (lienzo de 1440 × 900):

| Nivel | Fuente | Tamaño | Estilo |
|---|---|---|---|
| Títulos | Cinzel ExtraBold | 56–72 | Mayúsculas, tracking +4 % (`FrostboundUI.Tracking`), material TMP "OldGold" (degradado y *underlay* Abyss) |
| Encabezados | Cinzel SemiBold | 24–28 | Mayúsculas, tracking +6 % |
| Botones | Cinzel SemiBold | 20–22 | Mayúsculas, tracking +8 % |
| Texto | Nunito Bold | 16–18 | Color Bone/Ash |
| Números | Nunito ExtraBold | — | Daño, stats |

Lilita One sale de la UI principal. Solo puede quedar en algún detalle infantil, como los pingüinitos.

**Medidas:** base de 4 px, margen de panel de 24, separación de 12. Marcos de 6–10 px con biselado de 45°. Botón principal de 320 × 64, secundario de 240 × 48, icono de 48 × 48.

**Movimiento:**
- Hover: brillo en 120 ms (*ease-out*). La escala baja a ≤ 1.01 (hoy `HoverScale` es 1.03).
- Pulsado: el botón se oscurece un 15 % y el texto baja 1 px en 60 ms.
- Panel: fundido de 180 ms con 8 px de desplazamiento.
- Cambio de pantalla: fundido a negro con `FadeDuration`.
- Sin rebotes ni efectos elásticos.

### Componentes (prefijo `FB`, en `Assets/Scripts/UI/Kit/`)

| Componente | Contenido | Parte de |
|---|---|---|
| `FBPanel` | Marco de hierro 9-slice, placa con textura, cabecera y separador opcionales, esquinas con cristal | `FrostUI.Img` |
| `FBButton` | Variantes Primario (oro), Secundario (hierro), Peligro (sangre) e Icono. Estados normal, hover, pulsado, desactivado y foco | `FrostButton`, `UIButtonFx` |
| `FBTooltip` | Fondo Abyss al 92 %, título por rareza, separadores finos y comparación verde/rojo | `InventoryScreen` |
| `FBSlot` | Celda con marco por rareza, forma de rareza en la esquina y brillo | `ItemSlotView` |
| `FBTitle`, `FBDivider`, `FBModal`, `FBBanner` | Título de oro o hielo, separador con diamante, confirmación con fondo oscurecido y carteles de jefe o misión | `FrostUI.IceMaterial`, `BonfireBanner` |

Cada componente debe tener una API de fábrica estática como las de `FrostUI`, por ejemplo `FBButton.Create(parent, "JUGAR", FBButton.Kind.Primary, onClick)`, para que las pantallas actuales migren cambiando una sola llamada.

## Fondo del menú: la mansión helada del Frost

Una mansión gótica abandonada, tragada por el hielo corrupto, en una colina nevada de noche. En las ventanas late la luz azul del Frost y solo una conserva una vela cálida. Reemplaza a `MenuRuins` como decorado de `00_MainMenu`. **`MenuRuins` no se borra**: queda para volver atrás o para la pantalla de carga.

> Opcional y pendiente de que el usuario lo confirme: atarla a la historia como **la casa de los Vendaval**, la familia de Rocco y Pipo. No ponerle nombre en pantalla sin preguntar.

**Script:** `Assets/Scripts/World/Atmosphere/HauntedManor.cs`, con el mismo patrón que `MenuRuins`:
- `MeshKit` + `Combine` para la geometría estática.
- Materiales `Lit(...)` con emisión.
- `System.Random` con seed fija (siempre la misma mansión).
- `Awake() => Build()`.
- Una constante `public static readonly Vector3 HeroSpot` que sustituye a `MenuRuins.PedestalTop` donde la use `FrostMenu.BuildActor`.

**Composición (cámara fija y cinematográfica):**
- La cámara está baja, a unos 1,6 m, con FOV de 30–35°, mirando un poco hacia arriba. Así la mansión domina el fondo y se recorta contra la luna.
- El héroe queda en el **tercio izquierdo**, en el escalón del portón de hierro o en el camino de entrada. El tercio derecho es para la lista de ranuras y abajo al centro va el botón JUGAR.
- Hay tres planos de profundidad: delante la verja, lápidas y árboles muertos que enmarcan; en el medio el camino y la escalinata del porche; al fondo la mansión, y detrás la luna y la silueta del Frostspire en la niebla.

**Piezas:**
- **Fachada:** cuerpo central de dos plantas y dos alas, una torre con chapitel roto, un tejado a dos aguas hundido en parte y chimeneas.
- **Ventanas:** alrededor de 12 marcos góticos (arco apuntado). Unas 8 tienen luz azul del Frost con parpadeo, 3 están oscuras con cristales rotos y **1 tiene una vela cálida**. Esa ventana es el único punto cálido junto al héroe.
- **Hielo corrupto:** cristales del Frost (reutilizar el octaedro de `MenuRuins`) que brotan de las grietas de la fachada y del suelo, con estalactitas en los aleros.
- **Entorno:** verja y portón de hierro entreabierto, camino de losas con nieve, 4–6 árboles muertos y escarchados, 3–5 lápidas inclinadas y un farol apagado.
- **Vida:** cuervos o murciélagos que cruzan de vez en cuando, una cortina que se mueve en una ventana, un **destello del Frost** cada 12–20 s (todas las ventanas laten a la vez y la niebla se tiñe de azul) y nieve con bruma baja que se arrastra.

**Luz y atmósfera:**
- **Luces principales:** la luna azul como contraluz con sombras suaves (la única con sombras), la luz cálida de la ventana con la vela y una antorcha o farol cerca del héroe para su luz principal.
- **Luces de relleno:** de 3 a 5 luces puntuales azules en las ventanas, sin sombras. **Máximo 8 luces en tiempo real.**
- `NightFrostAtmosphere` se reutiliza: niebla exponencial, bloom en los emisivos, viñeta de 0,45–0,5, *film grain* suave y gradación fría.

**Herramienta:** ampliar `SetupFrostMenu`. Pasa a llamarse "Montar menús" y gana una opción "Decorado: Mansión / Ruinas". Coloca la cámara con el encuadre de arriba. Debe poder ejecutarse varias veces.

## Tareas

El orden importa. Las dependencias están entre paréntesis. El detalle y el estado viven en `PROGRESO.md`.

**Base**
- **UI-0** Tablero de referencias: capturas propias en `Docs/ui_referencias/`, solo para uso interno.
- **UI-1** Unificar los tokens en `FrostboundUI`. `FrostUI` deja de definir colores.
- **UI-2** Paleta y tipografía nuevas, y material TMP OldGold (UI-1).

**Decorado del menú**
- **M-1** `HauntedManor`: geometría de fachada, ventanas, verja, camino y árboles (—).
- **M-2** Luz, emisivos, parpadeo de ventanas, vela cálida y atmósfera (M-1).
- **M-3** Vida: cuervos, cortina, destello del Frost, bruma y nieve (M-2).
- **M-4** Encuadre, `HeroSpot` y conexión en `FrostMenu` y `SetupFrostMenu` (M-1).

**Kit y pantallas**
- **UI-3** Kit de marcos (Blender o pintados), PNG a 2× con 9-slice y Sprite Atlas `UI_Frost` (UI-0).
- **UI-4** Componentes `FB*` y escena de catálogo `_UICatalog` (UI-2, UI-3).
- **UI-5** Shaders de UI con Shader Graph: brillo de marco, escarcha en el foco y disolución (UI-4).
- **UI-6** Menú de inicio rehecho con FB sobre la mansión: ranuras a la derecha y JUGAR abajo (UI-4, M-4).
- **UI-7** Migrar el resto de pantallas en este orden: creación de héroe → pausa/opciones → inventario/comercio/arcón → muerte → carga → carteles (UI-4).
- **UI-8** HUD: barra de pociones, habilidad y XP con el marco de hierro. Se mantienen orbes y estatuas (UI-3).
- **UI-9** Sonidos de UI en `AudioManager` (UI-4).
- **UI-10** Accesibilidad: escala de texto, forma por rareza y contraste (UI-7).
- **UI-11** Rendimiento: separar Canvas, quitar Raycast Target de lo decorativo y revisar el *overdraw* (UI-7).
- **UI-12** Actualizar la guía visual en `Docs/Investigacion_UI_Diablo2R.md` con capturas finales (UI-7).

## Verificación (en cada tarea)

- **Compila sin errores ni advertencias nuevas.** Se puede comprobar por el bridge, ya que `FROSTBOUND_BRIDGE` está activo, o pidiéndole al usuario que mire la consola de Unity.
- **La prueba de humo pasa:** `Editor/Checks/SmokeTest`, y `ControlsChecks` si se tocó la navegación.
- **Revisión visual** en 1280×720, 1920×1080, 2560×1080 y 3440×1440. `FrostboundCapture` saca capturas. **Mirarlas**: no dar una tarea visual por buena sin ver la imagen.
- **Mando:** se puede recorrer la pantalla completa sin perder el foco.
- **Rendimiento del menú con la mansión:** 60 FPS en el PC del usuario, ≤ 8 luces en tiempo real y una sola con sombras.
- Si algo no se pudo verificar (por ejemplo, porque Unity no está abierto), decirlo tal cual en el resumen y en `PROGRESO.md`.

## Errores que hay que evitar

- Esquinas redondeadas, rebotes o escalados elásticos. Son del estilo "cozy" que se está dejando.
- Negro puro (`#000`) en paneles: usar `Abyss` o `Night`.
- Varios acentos cálidos a la vez. El oro solo va en lo importante y el fuego solo en sus fuentes.
- Colores o rutas de sprites escritos en una pantalla en vez de en tokens o `UISkin`.
- Rehacer una pantalla entera de golpe. Se migra componente a componente y se comprueba en cada paso.
