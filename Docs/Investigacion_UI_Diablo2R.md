# Investigación de UI/UX: Diablo II: Resurrected como referencia para Frostbound

**Fecha:** 17 de octubre de 2026
**Rama:** `feature/campana-completa`
**Objetivo:** entender los principios de interfaz de Diablo II: Resurrected (D2R) y traducirlos a una propuesta viable para Frostbound con las tecnologías que el proyecto ya usa.

> **Cómo leer este documento.** Cada afirmación lleva una etiqueta:
> - **[OFICIAL]** — Blizzard, sus desarrolladores o una ficha oficial (GDC, entrevistas con nombre y cargo).
> - **[PRENSA]** — medios especializados que citan a Blizzard.
> - **[COMUNIDAD]** — comunidad de modding (archivos del juego extraídos).
> - **[INFERENCIA]** — conclusión propia a partir del análisis visual. No es un hecho.
> - **[PROPUESTA]** — recomendación para Frostbound.
>
> No se usan recursos de D2R. La idea es tomar sus **principios**, no sus assets: el logo, las fuentes con licencia comercial y los arts son propiedad de Blizzard o de terceros.

---

## 1. Diagnóstico previo: qué es Frostbound hoy

Este diagnóstico salió de inspeccionar el repositorio antes de cualquier recomendación.

| Aspecto | Estado actual | Dónde |
|---|---|---|
| Motor | Unity **6000.5.7f1** | `ProjectSettings/ProjectVersion.txt` |
| Render | **URP 17.5**, posprocesado por Volume (bloom, viñeta, ACES, gradación) | `NightFrostAtmosphere.cs` |
| Lenguaje | C# (154 scripts de juego, sin asmdef) | `Assets/Scripts` |
| Sistema de UI | **uGUI (Canvas) + TextMeshPro**. 0 usos de UI Toolkit y 175 referencias a `CanvasScaler`/`TextMeshProUGUI` | `Assets/Scripts/UI` |
| Construcción de la UI | **Todo por código**: `FrostMenu` (969 líneas), `FrostUI` (fábrica de estilo), `UIFactory`, `GameHUD`, `InventoryScreen`. No hay prefabs de UI | `Scripts/UI/Menu`, `Scripts/UI/Inventory` |
| Resolución | Lienzo de referencia 1440 × 900. `ScreenAdapter` escala por alto o por ancho de 4:3 a 32:9. `UltrawideFrame` centra el HUD en 16:9 de forma opcional | `ScreenAdapter.cs`, `UltrawideFrame.cs` |
| Menú principal | Escena 3D real detrás del Canvas: `MenuRuins` (ruinas del Frostspire, pedestal, antorchas, cristal flotante, luna, nieve, bruma) y `NightFrostAtmosphere` | `World/Atmosphere/` |
| Tipografía | **Lilita One** (títulos "letra helada"), **Nunito** 500/700/800 (texto) y **Cinzel** SemiBold/ExtraBold (importada, poco usada). Las tres con licencia OFL | `Assets/Frostbound/UI/Fonts` |
| Sprites | Rectángulos redondeados con 9-slice (`UI_Round10…20`, `UI_Pill`), diamante, chevron, copo, pedestal y 60 iconos. Sin Sprite Atlas | `Assets/Frostbound/UI/Sprites`, `Icons` |
| HUD | **Ya sigue a D2:** orbes de vida y maná con marco y estatuas laterales (`HUD_Statue_Life/Mana`, `HUD_OrbFrame_*`, `HUD_Bar`) | `Assets/Frostbound/UI/HUD`, `SetupOrbHud.cs` |
| Paleta | Azul noche + hielo + oro: `#0A1222`, `#0F1B31`, `#BFEAFF`, `#E0B44A`… | `FrostboundUI.cs` |
| Efectos UI | Hover a hielo y escala 1.03 en 0,1 s (`UIButtonFx`, `FrostButton`), nieve en el Canvas (`UISnow`), fundido (`ScreenFade`), pulso (`UIPulse`) y pantalla de carga con letras de hielo | `Scripts/UI` |
| Shaders | Solo `FrostboundToon.shader` (personajes). Ninguno de UI | `Assets/Shaders` |
| Paquetes útiles ya instalados | uGUI 2.5, TextMeshPro, Input System 1.20, URP 17.5, Timeline 1.8 y 2D Sprite (atlas) | `Packages/manifest.json` |

### Limitaciones de la UI actual

1. **Dos fuentes de color.** `FrostboundUI` (`Ice #BFEAFF`, `Border #2A3D5C`…) y `FrostUI` (`Ice #BFEAFF`, `Border #5F8FB8`, `BorderDim #2A3D5C`…) repiten y contradicen tokens. Es la condición F1.5 de la auditoría, que volvió a aparecer.
2. **La identidad visual está partida en dos.** El HUD es oscuro y ornamental, como D2. Los menús son "cozy": letra redondeada (Lilita/Nunito), esquinas muy redondas y botones de hielo claro. Hoy no se leen como el mismo juego.
3. **Sin materiales en la UI.** Todo son colores planos sobre sprites blancos 9-slice. Faltan textura (piedra, metal, cuero o hielo envejecido), relieve y luz, que es justo lo que define el look de D2R.
4. **La UI hecha solo por código** cuesta iterarla visualmente: cada ajuste de un píxel requiere recompilar. Ya estaba señalado en la auditoría (A2).
5. **Sin Sprite Atlas.** Cada sprite es su propia textura. Con más ornamentos subirán los draw calls de la UI.

---

## 2. Investigación tecnológica de Diablo II: Resurrected

### 2.1 Lo que está confirmado

| Tema | Dato | Etiqueta | Fuente |
|---|---|---|---|
| Desarrolladores | Blizzard Entertainment y **Vicarious Visions**. VV hizo el *front-end* y el cliente 3D, y Blizzard los servidores y Battle.net | [OFICIAL] vía prensa | [Wikipedia](https://en.wikipedia.org/wiki/Diablo_II:_Resurrected) |
| Lanzamiento | 23-09-2021. PC, PS4/PS5, Xbox One/Series y Switch | [OFICIAL] | [Wikipedia](https://en.wikipedia.org/wiki/Diablo_II:_Resurrected) |
| Arquitectura | **La lógica original sigue debajo y encima hay una capa 3D nueva.** Kevin Todisco (Senior Software Engineer): el cambio legacy/nuevo existe para demostrar que "el mismo código y motor" corre debajo, y "nos mantuvo honestos" | [OFICIAL] | [NME](https://www.nme.com/features/diablo-2-resurrected-interview-accessibility-to-the-front-3050815) |
| Render | "Completamente reconstruido en 3D" con un "pipeline de render muy moderno" (Rob Gallerani y Michael Bukowski) | [OFICIAL] vía prensa | [Purediablo](https://www.purediablo.com/?p=2959), [OneMoreGame](https://onemoregame.ph/2021/09/diablo-ii-resurrected-challenge-of-staying-true/) |
| Legacy / nuevo | Se alterna entre los gráficos de 1999 y los nuevos con una tecla | [OFICIAL] | [Wikipedia](https://en.wikipedia.org/wiki/Diablo_II:_Resurrected) |
| Filosofía de arte | **"70/30":** el 70 % del contenido (arte y audio) se queda como estaba y hay un 30 % de margen para mejorarlo | [OFICIAL] vía prensa | [Dexerto](https://www.dexerto.com/gaming/diablo-2-resurrected-devs-reveal-biggest-challenges-of-pulling-off-remaster-1520042/) |
| Front-end | Gallerani: dedicaron mucho tiempo al punto de entrada. Front-end rediseñado, "más moderno", con un **botón de jugar grande** y juego online e invitaciones desde dentro | [OFICIAL] | [NME](https://www.nme.com/features/diablo-2-resurrected-interview-accessibility-to-the-front-3050815) |
| Accesibilidad | Fuente más grande, modo daltónico, cámara retocada, control de volumen, stick intercambiable para jugar con una mano y comparación de objetos | [OFICIAL] | [NME](https://www.nme.com/features/diablo-2-resurrected-interview-accessibility-to-the-front-3050815) |
| Opciones de UI | Categorías Gráficos, Juego, Accesibilidad (visión reducida, daltonismo, subtítulos), Automapa (tamaño, lado, opacidad), UI y Social (4 tamaños de fuente del chat) | [PRENSA] | [Blizzplanet](https://blizzplanet.substack.com/p/diablo-ii-resurrected-settings-ui) |
| Resolución | 4K. **Sin 21:9 real:** se limita a 19:9 con viñetas laterales porque la IA no registraba golpes desde la franja extra | [OFICIAL] vía prensa | [TechRadar](https://www.techradar.com/news/diablo-2-resurrected-cant-handle-219-ultrawide-monitors-heres-why), [The FPS Review](https://www.thefpsreview.com/2021/09/08/blizzard-drops-219-ultrawide-support-in-diablo-ii-resurrected-says-it-was-breaking-the-game) |
| UI añadida tras el lanzamiento | Diciembre de 2021: barra de habilidades para PC parecida al esquema de mando | [PRENSA] | [Wikipedia → GameSpot](https://en.wikipedia.org/wiki/Diablo_II:_Resurrected) |
| Charla técnica | GDC 2022: *"Diablo 2: Resurrected: The Art Direction and Pipelines for Resurrecting Evil"*, de Dustin King (Blizzard). Trata los pilares de arte y los pipelines por disciplina. La ficha **no nombra herramientas** | [OFICIAL] | [GDC Vault](https://gdcvault.com/play/1027883/-Diablo-2-Resurrected-The) |
| Logo | El wordmark de Diablo/Diablo II es una versión dirigida de **Exocet** (Jonathan Barnbrook, Emigre), una fuente **comercial** | [PRENSA] | [Fonts In Use](https://www.fontsinuse.com/uses/60790/diablo-ii-logo-and-menu) |
| Juego original (1999) | Erich Schaefer: modelos en 3D Studio Max, texturas y UI 2D en Photoshop. **Es del original, no de D2R** | [PRENSA] | [Blizzplanet](https://blizzplanet.substack.com/p/why-diablo-ii-resurrected-is-a-big-deal) |

### 2.2 Lo que dice la comunidad de modding (archivos extraídos)

| Dato | Etiqueta | Fuente |
|---|---|---|
| Los datos van en un almacén **CASC** y se extraen con CASC Viewer. Los textos y varias tablas son **JSON** | [COMUNIDAD] | [Foro de Blizzard: guía de modding](https://us.forums.blizzard.com/en/d2r/t/modding-guides-episode-1-how-to-show-item-levels-by-zax/160091) |
| Los paneles se describen en **JSON de layout** (por ejemplo `data/global/ui/layouts/hudpanelhd.json` y `showitemspanelhd.json`) con perfiles por plataforma (`_profilehd.json`). Las imágenes son `.sprite` en `data/hd/global/ui/panel/...` | [COMUNIDAD] | [Inven 7840](https://www.inven.co.kr/board/diablo2/5842/7840), [Inven 7635](https://www.inven.co.kr/board/diablo2/5842/7635) |
| Hay herramientas de mods que reescriben esos JSON por script | [COMUNIDAD] | [D2RMM docs](https://github.com/olegbl/d2rmm/blob/master/DOCS.md) |

**[INFERENCIA]** La UI de D2R parece estar **dirigida por datos**: hay layouts declarativos (JSON), recursos de imagen separados (sprites HD) y perfiles por plataforma (PC, mando). Es un patrón de **separar la estructura del arte**, no una tecnología concreta.

### 2.3 Lo que **no** es público

- **El nombre del motor 3D o del framework de UI.** Ninguna fuente primaria lo nombra. No hay que atribuirle Unreal, Unity, Scaleform, Coherent ni nada parecido.
- **Herramientas de arte del remaster** (Substance, ZBrush, Maya, Houdini…). La ficha del GDC no las nombra y las entrevistas tampoco.
- **Las tipografías exactas de la UI de D2R.** Solo está documentado el logo (Exocet). Lo que circula sobre la letra de los objetos son observaciones, no datos.
- **Shaders y posprocesado concretos.** Solo se sabe que el pipeline es "moderno" y que buscaban algo "más sangriento, sucio y oscuro" (Todisco, [NME](https://www.nme.com/features/diablo-2-resurrected-interview-accessibility-to-the-front-3050815)).

**Consecuencia para Frostbound:** las técnicas de las secciones 4 y 5 son **alternativas viables con Unity/URP** para lograr un resultado parecido. No pretenden reproducir el pipeline de Blizzard.

---

## 3. Desglose visual de D2R (ingeniería inversa)

> Todo este apartado es **[INFERENCIA]** a partir de material público del juego (tráilers oficiales, capturas de prensa y gameplay). Conviene validarlo contra capturas propias antes de la fase de arte (tarea UI-0).

### 3.1 Menú principal y selección de personaje
- **Composición cinematográfica.** Detrás del menú hay una **escena 3D en vivo**, no una imagen plana. El personaje elegido está de pie en un entorno iluminado por fuego y la cámara queda fija con encuadre de cine. La prensa del alfa destacó que la selección es donde se ven los modelos remasterizados y los fondos "impresionantes" ([Windows Central](https://windowscentral.com/diablo-2-resurrected-tech-alpha-hands)). La pantalla de la hoguera de las clases es un icono de la saga ([Gematsu](https://www.gematsu.com/games/diablo-ii-resurrected)).
- **Jerarquía.** (1) Personaje y escena, (2) lista de personajes en un panel lateral, (3) **un botón principal grande centrado abajo** (lo confirma [NME](https://www.nme.com/features/diablo-2-resurrected-interview-accessibility-to-the-front-3050815)) y (4) acciones secundarias pequeñas (crear, borrar, online u offline).
- **Mucho espacio negativo oscuro.** La escena ocupa del 60 al 70 % de la pantalla y la UI se reduce a los bordes.

### 3.2 Botones y controles
- **Forma:** rectangulares o con esquinas biseladas, **nunca redondeadas**. Llevan un marco metálico (bronce o hierro envejecido) con un relieve de 1 a 3 px, y por dentro una placa oscura con textura de piedra o cuero.
- **Texto:** en mayúsculas, con serifa de inspiración lapidaria y un color crema u oro apagado, más una sombra para separarlo del fondo.
- **Estados:** en hover el marco y el texto se iluminan (de oro apagado a oro brillante) y aparece un resplandor cálido. Al pulsar, el botón se oscurece o se hunde. Desactivado se ve gris y sin brillo. Todo va acompañado de **sonido**.
- **Animación:** contenida. Hay fundidos y brillos, pero **sin rebotes ni escalados elásticos**. La sobriedad forma parte del tono.

### 3.3 Paneles, marcos y ornamentos
- **Marcos ornamentales** por 9-slice: esquinas con remaches o florones, bordes de metal envejecido y fondos casi negros con una textura sutil.
- **Tooltips:** fondo negro semitransparente sin marco pesado. El nombre se colorea según la calidad del objeto: blanco normal, azul mágico, amarillo raro, dorado único, verde de set y gris para los objetos con huecos. Esa **codificación por color** es central en la UX de la saga.
- **Iconografía:** objetos en una cuadrícula de celdas visibles (el inventario Tetris) e iconos pintados con luz de arriba.

### 3.4 Paleta
- Base: **negro cálido y marrón muy oscuro**, no negro puro.
- Metales: **bronce y oro viejo** en marcos y títulos.
- Acentos: **rojo sangre** (vida, peligro) y **azul** (maná).
- Las luces calientes (fuego, antorchas) contrastan con sombras frías y profundas. Hay viñeta fuerte y mucho contraste local.

### 3.5 Tipografía
- **Dos niveles:** títulos con una serifa de capitales lapidarias y mucho carácter, y texto con una serifa más sobria y legible.
- Mucho uso de **mayúsculas con espaciado** en botones y encabezados.

### 3.6 Fondos y atmósfera
- Iluminación puntual cálida (fuego) contra sombras frías. Hay partículas (brasas, ceniza o polvo), niebla volumétrica en capas de profundidad y un ligero *grain*.
- Las pantallas de carga muestran **arte de la zona** y el nombre del acto.

### 3.7 Transiciones
- **Fundido a negro** entre pantallas y paneles que aparecen con un fundido corto, sin desplazamientos largos. En las cargas, arte estático y una barra discreta.

### 3.8 Escalabilidad
- [OFICIAL] 4K y relaciones de aspecto anchas limitadas a 19:9 con viñetas. **[INFERENCIA]** La UI se ancla a los bordes con perfiles por plataforma y el mando tiene un esquema propio, más radial y con botones más grandes.

---

## 4. Análisis comparativo

### 4.1 Qué principios encajan y cuáles no

| Principio de D2R | ¿Encaja? | Motivo |
|---|---|---|
| Escena 3D viva detrás del menú | ✅ **Ya existe** | `MenuRuins` y `NightFrostAtmosphere` cumplen el papel. Falta encuadre cinematográfico, luz cálida y partículas |
| HUD con orbes y estatuas | ✅ **Ya existe** | `HUD_Statue_*` y `HUD_Orb*`. Solo hay que unificarlo con el resto |
| Marcos de metal envejecido, sin esquinas redondas | ✅ Con adaptación | Cambiar los sprites `UI_Round*` por marcos biselados de **hierro escarchado** |
| Paleta cálida bronce/oro sobre negro | ⚠️ **Adaptar** | Frostbound es azul y hielo. Proponemos **noche azul + plata/hierro + oro** como acento, y reservar el cálido para el fuego (hogueras, antorchas) |
| Serifa lapidaria en títulos | ✅ | **Cinzel ya está importada** (OFL), así que no hace falta Exocet, que además es comercial |
| Codificación de rareza por color | ✅ **Ya existe** | `FrostboundUI.Rarity`. Para daltónicos se le añade forma o icono |
| Animación sobria | ⚠️ Revisar | Hoy el botón crece a 1.03. Se puede mantener sutil y pasar al brillo como señal principal |
| Tono grotesco ("más sangriento") | ❌ No copiar | Frostbound tiene pingüinos y un tono propio. Su "oscuridad" es **frío, corrupción y cristal**, no gore |
| Layouts declarativos por datos | ⚠️ Parcial | Llevar el estilo a **ScriptableObjects y prefabs** sin reescribir todo (ver 5.3) |

### 4.2 Recursos: qué se reutiliza y qué hay que crear

| Recurso | Acción |
|---|---|
| `MenuRuins`, `NightFrostAtmosphere`, `UISnow`, `ScreenFade`, `LoadingScreen` | **Reutilizar y ajustar** |
| HUD de orbes y estatuas | **Reutilizar**. Es el ancla visual del nuevo estilo |
| 60 iconos de objetos y runas | **Reutilizar**. Opcional: un marco de celda por rareza |
| Fuentes Cinzel (títulos) y Nunito (texto) | Cinzel pasa a ser **la fuente de títulos**. Nunito puede seguir en el texto por su legibilidad, o cambiarse por una serifa OFL (ver 5.1). **Lilita One se retira** de la UI principal |
| `UI_Round*`, `UI_Pill` | **Sustituir** por marcos 9-slice nuevos (ver 4.4) |
| Fondos `MainMenu_Background`, `ClassSelect_Background` | Revisar si siguen en uso. La escena 3D los hace prescindibles |
| **Nuevo:** set de marcos (panel, botón, tooltip, celda, separador, esquina ornamental), texturas de placa (piedra helada, cuero oscuro), máscara de brillo y partículas de UI (escarcha, brasas) | **Crear** (ver 5.4) |

### 4.3 Compatibilidad técnica e impacto
- **Sin cambiar de sistema de UI.** uGUI + TMP alcanza para todo lo propuesto. Migrar a UI Toolkit no compensa: habría que reescribir unas 6.400 líneas de UI y uGUI convive mejor con la escena 3D y el texto SDF que ya existen.
- **Sin dependencias nuevas obligatorias.** Shader Graph viene con URP, y Sprite Atlas y TMP ya están instalados.
- **Rendimiento:** el riesgo está en el *overdraw* de paneles translúcidos a pantalla completa y en los *rebuilds* de Canvas. Se controla con atlas, Canvas separados (estático o dinámico) y shaders de UI baratos.
- **Mantenibilidad:** unificar los tokens y pasar los estilos a un ScriptableObject **reduce** la deuda (sección 1, limitaciones 1 y 4).

### 4.4 Dirección de arte propuesta: "Gótico de escarcha"

[PROPUESTA] Se queda la identidad de Frostbound (noche helada, cristal del Frost, pingüinos) y se le aplica la **gramática** de D2R: oscuridad, marcos de metal, sobriedad, mucho contraste y la escena como protagonista.

- **Material de los marcos:** hierro forjado con escarcha en las aristas. El metal es gris azulado con brillos plateados y en los remaches aparecen cristales de hielo.
- **Placas:** piedra oscura azulada con vetas muy sutiles, o cuero curtido casi negro.
- **Acentos:** oro viejo para lo importante (botón principal, títulos de jefe, objetos únicos) y **cian del Frost** para lo mágico o corrupto.
- **Lo cálido es escaso:** solo el fuego (hogueras, antorchas, el héroe) es cálido. Así se mantiene el contraste de D2R entre fuego y sombra, en clave fría.

---

## 5. Propuesta de diseño (guía visual)

### 5.1 Tokens (un solo origen)

**Color**

| Token | Hex | Uso |
|---|---|---|
| `Abyss` | `#05080F` | Fondo más profundo, viñeta |
| `Night` | `#0A1222` | Fondo de paneles (se mantiene) |
| `Slate` | `#141C2B` | Placa de botón y panel |
| `Iron` | `#4A5568` | Marco base |
| `IronLight` | `#9AA8BC` | Arista iluminada del marco |
| `Frost` | `#BFEAFF` | Brillo mágico, foco, selección (se mantiene) |
| `FrostDeep` | `#3A8FC4` | Corrupción, maná |
| `OldGold` | `#B8923E` | Texto de botón principal y títulos |
| `OldGoldLight` | `#E8C877` | Hover del oro |
| `Bone` | `#D9D2C3` | Texto principal (crema, menos frío que el blanco) |
| `Ash` | `#8A93A3` | Texto secundario |
| `Blood` | `#B3261E` | Vida y peligro |
| Rarezas | Se mantienen las de `FrostboundUI.Rarity` | Más una forma por rareza (ver accesibilidad) |

**Tipografía**

| Nivel | Fuente | Tamaño (lienzo 1440 × 900) | Estilo |
|---|---|---|---|
| Título de pantalla | Cinzel ExtraBold | 56–72 | Mayúsculas, tracking +4 %, degradado OldGold → OldGoldLight, *underlay* Abyss |
| Encabezado de panel | Cinzel SemiBold | 24–28 | Mayúsculas, tracking +6 %, Bone |
| Botón | Cinzel SemiBold | 20–22 | Mayúsculas, tracking +8 % |
| Texto | Nunito Bold (o serifa OFL como **EB Garamond** o **Cormorant**, de Google Fonts) | 16–18 | Bone/Ash, interlineado 1,3 |
| Datos (números de daño, stats) | Nunito ExtraBold | según el contexto | Se mantiene, por legibilidad |

> Si se quiere un logo con un carácter parecido a Exocet, hay que **diseñarlo a medida** (o partir de una fuente OFL y modificarla). Exocet es comercial y está asociada visualmente a Diablo.

**Medidas**
- Unidad base de **4 px**. Márgenes de panel de 24 px. Separación entre botones de 12 px.
- Marcos: grosor visual de 6–10 px en el lienzo de referencia, con esquinas biseladas de 45°.
- Botón principal de 320 × 64. Secundario de 240 × 48. Icono-botón de 48 × 48.

**Movimiento**
- Hover: brillo del marco y el texto en 120 ms (*ease-out*). La escala baja de 1.03 a **1.00–1.01**: el brillo pasa a ser la señal principal.
- Pulsado: el botón se oscurece un 15 % y el texto baja 1 px en 60 ms.
- Panel que aparece: fundido de 180 ms más 8 px de desplazamiento.
- Cambio de pantalla: fundido a negro de 250 ms (`FadeDuration`, que ya existe).
- Siempre con sonido de UI: hover suave, clic metálico y apertura de panel con una "ráfaga helada".

### 5.2 Componentes reutilizables

| Componente | Descripción | Base actual |
|---|---|---|
| `FBPanel` | Marco de hierro 9-slice, placa con textura, cabecera opcional con separador ornamental y esquinas con cristales | `FrostUI.Img` |
| `FBButton` | Variantes Primario (oro), Secundario (hierro), Peligro (sangre) e Icono. Estados normal, hover, pulsado, desactivado y foco (mando) | `FrostButton` + `UIButtonFx` |
| `FBTooltip` | Fondo Abyss al 92 %, título coloreado por rareza, separadores finos y comparación en verde o rojo | `InventoryScreen` |
| `FBSlot` | Celda de inventario con marco por rareza y brillo al pasar | `ItemSlotView` |
| `FBTitle` | Título con material TMP de "oro viejo" o "letra helada" | `FrostUI.IceMaterial` |
| `FBDivider` | Separador horizontal con ornamento central (diamante o cristal) | `UI_Diamond` |
| `FBModal` | Diálogo de confirmación con oscurecimiento del fondo | `BuildConfirm` en `FrostMenu` |
| `FBBanner` | Carteles de jefe, nueva misión y zona | `BonfireBanner` |

### 5.3 Pantallas

1. **Título / selección de personaje** (`00_MainMenu`):
   - **Encuadre cinematográfico:** cámara baja con un FOV de 30–35° y el héroe en el tercio izquierdo.
   - **Luz:** antorcha cálida como luz principal y luna azul como contraluz.
   - **Partículas:** brasas y escarcha.
   - **Paneles:** a la derecha va la lista de ranuras (`FBPanel`) y abajo en el centro el botón **JUGAR** grande (`FBButton` primario), como en D2R. Las acciones secundarias van pequeñas en la esquina.
   - **Logo:** arriba, con el material de oro viejo y escarcha.
2. **Creación de héroe:** las tarjetas de clase pasan a ser placas de hierro con el retrato iluminado. La descripción va en serifa y la habilidad en un `FBPanel` pequeño.
3. **HUD:** se mantienen orbes y estatuas. La barra de pociones y de habilidad se rehace con el mismo marco de hierro y la XP con un filete de oro viejo.
4. **Inventario y comercio:** son `FBPanel` con cabecera, cuadrícula de `FBSlot` y `FBTooltip`.
5. **Pausa, opciones y muerte:** comparten `FBPanel` y `FBButton`. En la muerte, la pantalla se tiñe de Abyss y aparece "HAS CAÍDO" en Cinzel sangre.
6. **Carga:** arte o vista 3D de la zona, el nombre del capítulo en Cinzel, un consejo y una barra fina de oro viejo.

### 5.4 Escalabilidad y accesibilidad
- Se mantienen `ScreenAdapter` y `UltrawideFrame`, que ya resuelven de 4:3 a 32:9 (mejor que D2R, que se queda en 19:9).
- **Escala de texto** en Opciones (100/115/130 %) aplicada a los TMP de texto y tooltips. D2R ofrece una fuente más grande como opción de accesibilidad.
- **Rareza sin depender del color:** una forma en la esquina de la celda (círculo, rombo, estrella, corona) además del color.
- **Contraste:** texto Bone sobre Slate de al menos 7:1, y Ash sobre Night de al menos 4,5:1.
- Navegación con mando y teclado en todos los componentes. Ya existe con `UIDeviceFocus` y foco en `FrostButton`.

---

## 6. Matriz tecnológica

| Tecnología | Función | Evidencia de que D2R la use | Compatibilidad con Frostbound | Recomendación |
|---|---|---|---|---|
| **uGUI (Canvas)** | Estructura de UI | No pública | ✅ Instalada y en uso | **Mantener** |
| **TextMeshPro** (SDF: contorno, *underlay*, *glow*, *face texture*, degradado) | Títulos metálicos y helados, texto nítido a 4K | No pública | ✅ En uso (`FrostUI.IceMaterial`) | **Mantener**. Añadir un material "OldGold" |
| **Shader Graph, destino Canvas/UI** (URP) | Brillo que recorre el marco, escarcha que avanza, disolución, ruido animado | No pública | ✅ Viene con URP 17 / Unity 6. Comprobar en el editor que el destino de UI esté disponible en esta versión | **Adoptar** para 2–3 shaders de UI |
| **Sprite Atlas** (2D Sprite) | Agrupar marcos, iconos y ornamentos | No pública | ✅ El paquete está presente | **Adoptar** |
| **9-slice** (`Image.Type.Sliced`) | Marcos escalables | [COMUNIDAD] Paneles con sprites HD separados | ✅ Ya se usa | **Mantener** con los sprites nuevos |
| **Volume de URP** (bloom, viñeta, gradación, *film grain*) | Atmósfera del menú y del juego | [OFICIAL] Pipeline "moderno" y tono "más oscuro" | ✅ En uso | **Ajustar** (grain y viñeta más fuertes en el menú) |
| **Canvas Screen Space – Camera** | Partículas o 3D entre capas de UI | No pública | ✅ Nativo | **Usar** solo en el menú y la carga |
| **Particle System** | Brasas, escarcha, nieve | No pública | ✅ Nativo | **Adoptar** dentro de la escena 3D del menú |
| **Timeline** | Intro cinematográfica del menú y presentación de jefes | No pública | ✅ Instalado | **Opcional**, fase 3 |
| **ScriptableObject de estilo (`UITheme`)** | Tokens y referencias en un asset editable | [INFERENCIA] D2R separa layout y arte | ✅ Ya existe `UISkin` | **Ampliar** `UISkin` y unificar con `FrostboundUI` y `FrostUI` |
| **Prefabs de componentes** | Iterar el diseño sin recompilar | [INFERENCIA] Layouts declarativos | ✅ Nativo | **Adoptar** de forma gradual |
| UI Toolkit (USS/UXML) | Alternativa declarativa tipo CSS | — | ⚠️ Posible, pero obliga a reescribir | **No adoptar** ahora |
| UIParticle (paquete externo `ParticleEffectForUGUI`, MIT) | Partículas dentro de un Canvas Overlay | — | ⚠️ Dependencia externa | **Evitar** (sirve Screen Space Camera) |
| DOTween u otro paquete de tweening | Animaciones de UI | — | ⚠️ Dependencia | **Evitar**. Las corrutinas actuales bastan |
| Addressables | Carga de recursos | — | ⚠️ Excesivo para el tamaño actual | **No**. Basta con referencias directas y `Resources` |
| **Blender** (ya en el repo: `Blender/`) | Modelar ornamentos y marcos y renderizarlos como sprites con luz real | No pública | ✅ El equipo ya lo usa | **Adoptar** para marcos y esquinas |
| **Krita / GIMP** (gratis) o Photoshop | Pintado, texturas de placa, iconos | Photoshop en el original de 1999 [PRENSA] | ✅ Independiente del motor | **Cualquiera** |
| **Material Maker** (gratis) / Substance 3D Designer | Texturas procedurales de piedra, hierro y escarcha | No pública | ✅ Exportan PNG | **Opcional** |
| **Google Fonts (OFL)**: Cinzel, EB Garamond, Cormorant | Tipografía libre | — | ✅ Cinzel ya está | **Adoptar**. Exocet: **no** |

---

## 7. Plan de implementación

Las tareas van en orden. Cada una tiene un criterio de aceptación verificable. Un commit por tarea en `feature/campana-completa` con el prefijo `UI-n`.

| ID | Tarea | Depende de | Prioridad | Aceptación |
|---|---|---|---|---|
| **UI-0** | **Tablero de referencia:** reunir de 15 a 20 capturas propias de D2R (menú, selección, inventario, tooltip, HUD, carga) y de 5 a 10 de Frostbound, y anotar cada principio de la sección 3 | — | Alta | `Docs/ui_referencias/` con capturas y notas. Se valida o corrige la sección 3 |
| **UI-1** | **Unificar tokens:** un solo origen de color, tipografía y medidas (`FrostboundUI`). `FrostUI` lo consume y no redefine nada. `UISkin` se queda solo con assets | — | **Crítica** | Buscar `#BFEAFF` en `Scripts/` devuelve 1 archivo. Los menús se ven igual que antes (sin regresión) |
| **UI-2** | **Paleta y tipografía nuevas** (5.1): Cinzel en títulos y botones, Bone/Ash en el texto, material TMP "OldGold" | UI-1 | Alta | Las capturas del menú, la pausa y el inventario solo usan los tokens nuevos. Lilita One no aparece en la UI principal |
| **UI-3** | **Kit de marcos:** modelar en Blender o pintar el marco de panel, el de botón (3 variantes), el de tooltip, la celda, el separador y las esquinas. Exportar PNG a 2× con borde 9-slice definido y meterlos en un **Sprite Atlas** `UI_Frost` | UI-0 | Alta | Los sprites escalan sin deformar las esquinas de 0,5× a 3×. El atlas reduce los draw calls del inventario (Frame Debugger, antes y después) |
| **UI-4** | **Componentes** `FBPanel`, `FBButton`, `FBTooltip`, `FBSlot`, `FBDivider` y `FBModal`: un prefab o fábrica por componente, todos con estados y foco de mando | UI-2, UI-3 | Alta | Una escena de catálogo `_UICatalog` muestra todos los estados. Se navega con mando sin perder el foco |
| **UI-5** | **Shaders de UI** (Shader Graph): (a) brillo que recorre el marco en hover, (b) escarcha que avanza por el borde en el foco y (c) disolución para cerrar paneles | UI-4 | Media | Funcionan en Screen Space Overlay y Camera, cuestan menos de 0,1 ms de GPU en el menú (Profiler) y tienen alternativa estática si se desactivan |
| **UI-6** | **Menú principal cinematográfico** (5.3): encuadre, luz cálida de antorcha, brasas y escarcha, botón JUGAR grande abajo y lista de ranuras a la derecha | UI-4 | Alta | Se ve bien en 1280×720, 1920×1080, 2560×1080 y 3440×1440. 60 FPS en el menú en el PC de referencia |
| **UI-7** | **Migrar pantallas:** creación de héroe → pausa/opciones → inventario/comercio → muerte → carga → carteles de jefe | UI-4 | Media | Cada pantalla usa solo componentes FB. La prueba de humo (`Editor/Checks/SmokeTest`) pasa sin errores |
| **UI-8** | **HUD integrado:** rehacer la barra de pociones, habilidad y XP con el marco de hierro. Se mantienen los orbes y las estatuas | UI-3 | Media | HUD de un jugador y de co-op legibles en 16:9 y en ultrawide con `UltrawideFrame` |
| **UI-9** | **Sonido de UI:** hover, clic, apertura y cierre de panel y confirmación, con `AudioManager` | UI-4 | Media | Cada interacción tiene sonido y el volumen se controla desde Opciones |
| **UI-10** | **Accesibilidad:** escala de texto, forma por rareza y revisión de contraste | UI-7 | Media | Las opciones se guardan. Los contrastes cumplen 5.4 (medidos sobre capturas) |
| **UI-11** | **Rendimiento:** separar Canvas estáticos y dinámicos, quitar `Raycast Target` en lo decorativo y revisar el *overdraw* | UI-7 | Media | Sin *rebuilds* de Canvas en reposo (Profiler › UI). 0 B de GC por frame con el inventario abierto |
| **UI-12** | **Guía visual viva:** actualizar este documento con capturas finales y reglas de uso de cada componente | UI-7 | Baja | La guía cubre todos los componentes y tokens |

### Flujo de trabajo para cada recurso

1. **Diseño:** boceto en Krita o Photoshop sobre una captura de la pantalla real, a 1440 × 900.
2. **Producción:**
   - Marcos y ornamentos: modelado simple en Blender con material de metal y escarcha, luz desde arriba a la izquierda y render ortográfico a PNG con alfa a 2×.
   - Placas: textura procedural (Material Maker o Substance) en mosaico de 512².
3. **Importación:** Texture Type `Sprite (2D and UI)`, sin mipmaps en la UI, compresión de alta calidad, borde 9-slice en el Sprite Editor y alta en el atlas `UI_Frost`.
4. **Integración:** se asigna en `UISkin` y el componente lo toma de ahí. Nada de rutas en código.
5. **Verificación:** escena `_UICatalog`, capturas en 4 resoluciones y prueba de humo.

### Pruebas
- **Visuales:** capturas automáticas por resolución (se puede reutilizar `FrostboundCapture`) comparadas contra la guía.
- **Funcionales:** navegar con ratón, teclado y mando por cada pantalla, sin focos perdidos y con todas las acciones alcanzables.
- **Rendimiento:** Profiler (UI, GPU) y Frame Debugger en el menú, el inventario y un combate con HUD.
- **Regresión:** `SmokeTest`, `ControlsChecks` y los 32 tests EditMode en verde.

---

## 8. Guía visual implementada (estado al 17-10-2026)

Esta sección describe lo que **ya está en el juego** (rama `feature/campana-completa`) y las reglas para mantenerlo coherente. Las capturas están en `Docs/ui_capturas/` y se hicieron en el juego corriendo a 1920×1080. Donde lo implementado se apartó de la propuesta de las secciones 5 a 7, se indica.

### 8.1 Dónde vive cada cosa

| Pieza | Archivo | Regla |
|---|---|---|
| Colores y medidas | `Scripts/UI/FrostboundUI.cs` | **Único** lugar con hex. `FrostUI`, `InputGlyph` y `LootQualityInfo` solo reenvían |
| Fábrica de menús | `Scripts/UI/Menu/FrostUI.cs` | `Title` (Cinzel oro), `Kicker` (Cinzel en mayúsculas), `Text` (Nunito), `Panel` (marco de hierro con placa) |
| Botón | `Scripts/UI/Menu/FrostButton.cs` | `Create(..., Style.Normal/Primary/Danger)`: hierro, oro o sangre. Destello y escarcha al enfocar |
| Marcos | `Scripts/UI/Kit/FrostFrames.cs` | Generados por código en una textura (hace de atlas): hierro, oro, sangre, plata y placa. **Cambio:** no se modelaron en Blender |
| Cajas del juego | `Scripts/UI/Inventory/UIFactory.cs` → `Frame` | Las formas redondeadas pasan al marco de plata, que se tiñe con el color del borde (rareza o foco) |
| Efectos | `Resources/Shaders/UIFrostFrame.shader` | Destello, escarcha y disolución. **Cambio:** HLSL escrito a mano en vez de Shader Graph |
| Disolución | `Scripts/UI/Kit/UIDissolve.cs` | `UIDissolve.Ghost(panel)` al cerrar: una copia visual se deshace, la ventana real cierra al instante |
| Accesibilidad | `Scripts/Core/AccessibilitySettings.cs` | Tamaño del texto de lectura y forma por rareza |
| Catálogo | Escena `_UICatalog` (`Tools/Frostbound/UI/Crear catálogo de UI`) | Todos los componentes y estados en una pantalla |
| Fondo del menú | `Scripts/World/Atmosphere/HauntedManor.cs` | Mansión helada 3D; `MenuRuins` sigue disponible |

### 8.2 Reglas de uso

1. **Títulos:** siempre `FrostUI.Title` (Cinzel ExtraBold, oro viejo, sin cursiva). Etiquetas pequeñas: `FrostUI.Kicker`.
2. **Texto y datos:** Nunito (`FrostUI.Text` o `skin.nunito800`). Cinzel solo para títulos, botones y nombres.
3. **Botones:** uno solo principal (oro) por pantalla: la acción que se espera. Peligro (sangre) solo para borrar o salir sin guardar.
4. **Foco y selección:** siempre en oro (`OldGoldLight`). El hielo queda para lo mágico (maná, Frost).
5. **Paneles:** `FrostUI.Panel` para ventanas y cajas, y `UIFactory.Frame` para casillas y botones pequeños. Nada de esquinas redondeadas nuevas.
6. **Rareza:** color del marco más forma en la esquina (círculo, rombo, cruz, estrella). Nunca solo color.
7. **Animación:** escala ≤ 1,01 al pasar el ratón, sin rebotes. Cerrar ventanas con `UIDissolve.Ghost`.
8. **Sonido:** `AudioManager.PlayUI` con `UI_Click`, `UI_OpenMenu`, `UI_CloseMenu`, `UI_Equip` y `UI_Error`. Los botones del kit ya lo hacen.
9. **Rendimiento:** no rearmar textos en `Update` si el valor no cambió. Solo lo interactivo detecta clics.
10. **Efectos de shader en UI:** el shader recibe posiciones en el espacio del **Canvas raíz**; los rectángulos se le pasan en ese espacio (ver `FrostButton.UpdateFx`).

### 8.3 Pantallas

| Pantalla | Captura | Notas |
|---|---|---|
| Título | ![Título](ui_capturas/01_titulo.jpg) | Mansión helada de fondo, último héroe y JUGAR de oro al centro, fila de acciones abajo |
| Selección de personaje | ![Personajes](ui_capturas/02_personajes.jpg) | Resumen a la izquierda, ranuras a la derecha y fondo desenfocado (profundidad de campo) |
| Crear héroe | ![Crear](ui_capturas/03_crear.jpg) | Tarjetas de clase de hierro y la elegida en oro |
| HUD | ![HUD](ui_capturas/04_hud.jpg) | Orbes y estatuas originales; habilidad con marco de hierro (oro cuando está lista) y XP en línea de oro |
| Inventario | ![Inventario](ui_capturas/05_inventario.jpg) | Casillas de plata teñidas por rareza con su forma |
| Equipo | ![Equipo](ui_capturas/06_equipo.jpg) | Ranuras vacías con silueta |
| Personaje | ![Personaje](ui_capturas/07_personaje.jpg) | Atributos y habilidad de clase |
| Tienda | ![Tienda](ui_capturas/08_tienda.jpg) | Título en oro; comprar y vender suenan |
| Menú de NPC | ![NPC](ui_capturas/09_npc.jpg) | Marco de hierro, nombre en oro y opciones de plata |
| Pausa | ![Pausa](ui_capturas/10_pausa.jpg) | REANUDAR como principal |
| Configuración | ![Configuración](ui_capturas/11_configuracion.jpg) | Incluye tamaño del texto y forma por rareza |
| Carga | ![Carga](ui_capturas/12_carga.jpg) | Nombre de la zona en oro y barra recta de oro |
| Muerte | ![Muerte](ui_capturas/13_muerte.jpg) | "HAS MUERTO" en rojo Cinzel (ya tenía el estilo) |
| Jugador 2 | ![Jugador 2](ui_capturas/17_jugador2.jpg) | Tarjetas de hierro, la elegida en oro |
| Catálogo | ![Catálogo](ui_capturas/14_catalogo.jpg) | Referencia de todos los componentes y tokens |
| Destello del foco | ![Destello](ui_capturas/15_destello.jpg) | Franja dorada sobre el marco y ribete de escarcha |
| Disolución | ![Disolución](ui_capturas/16_disolucion.jpg) | Capturada a mitad, en versión lenta |

### 8.4 Verificación en la build de desarrollo (18-10-2026)

Build de desarrollo de Windows (`Tools/Frostbound/Release/Windows (desarrollo)`) con autoprueba sin intervención (`Scripts/Debug/BuildAutoTest.cs`; se lanza con `-autotest <carpeta>` desde `ReleaseBuilder.AutoTest*`).

| Prueba | Resultado |
|---|---|
| Errores y excepciones | **0** (tras arreglar el Frostspire, ver abajo) |
| Asignaciones por fotograma | Título **0,05** · partida 45 · inventario 15. Con toda la UI apagada la partida da ~70 de ~82 antes del arreglo: la UI aporta ~12 pequeñas, el resto es del juego |
| Reconstrucciones de Canvas | Título 0 · partida ~2/fot (nombres que siguen a los personajes y minimapa, 0,03 ms) |
| FPS (1920×1080, en ventana) | Título ~82 · partida ~65 |
| 21:9 y 32:9 | El título cabe entero; HUD e inventario centrados y escalados por altura. Se probó la **proporción** con ventanas de 1920×822 y 1920×540 porque el monitor es 16:9 |
| Mando (virtual, en la build) | Título: JUGAR → abajo OPCIONES → derecha CRÉDITOS → SALIR → arriba JUGAR. A en OPCIONES abre Configuración, flechas dentro y B la cierra devolviendo el foco a OPCIONES |
| Co-op | El jugador 2 entra con un mando virtual: dos HUD completos con el estilo nuevo y cartel "JUGADOR 2" en oro |

| 21:9 | 32:9 | Co-op |
|---|---|---|
| ![21:9](ui_capturas/18_titulo_21x9.jpg) | ![32:9](ui_capturas/19_inventario_32x9.jpg) | ![Co-op](ui_capturas/20_coop.jpg) |

**Error encontrado y corregido:** al cargar el poblado en la build salía `CollisionMeshData couldn't be created … "Combined Mesh (root: scene) 3"`. El Frostspire de la escena estaba marcado para *static batching*: al compilar, Unity juntaba sus mallas, así que `VillageWorldGenerator` ni podía ponerle colisión ni moverlo, y la montaña quedaba sin colisión en el juego compilado. Se quitó solo esa bandera (`SetupVillage.NoStaticBatching`, aplicado a la escena con `Tools/Frostbound/Poblado/Frostspire sin static batching`).

### 8.5 Pendiente

- Probar con un **mando de Xbox físico** (la navegación ya se verificó con uno virtual) y con un monitor ultrawide real.
- Las ~12 asignaciones por fotograma que quedan en la UI son pequeñas y repartidas; no hay un culpable claro.
- Fase 2 del realismo de la mansión: texturas descargadas (Poly Haven), cielo HDRI y casa modelada.

## 9. Referencias

**Diablo II: Resurrected**
- Wikipedia: [Diablo II: Resurrected](https://en.wikipedia.org/wiki/Diablo_II:_Resurrected)
- GDC 2022, Dustin King: [The Art Direction and Pipelines for Resurrecting Evil](https://gdcvault.com/play/1027883/-Diablo-2-Resurrected-The)
- NME: [entrevista con Rob Gallerani y Kevin Todisco](https://www.nme.com/features/diablo-2-resurrected-interview-accessibility-to-the-front-3050815)
- Dexerto: [los mayores retos del remaster (70/30)](https://www.dexerto.com/gaming/diablo-2-resurrected-devs-reveal-biggest-challenges-of-pulling-off-remaster-1520042/)
- PCGamesN: [entrevista con Gallerani y Chris Amaral](https://www.pcgamesn.com/diablo-2-resurrected/fix-diablo-2)
- Purediablo: [resumen del panel](https://www.purediablo.com/?p=2959) · OneMoreGame: [fidelidad al original](https://onemoregame.ph/2021/09/diablo-ii-resurrected-challenge-of-staying-true/)
- TechRadar: [por qué no hay 21:9](https://www.techradar.com/news/diablo-2-resurrected-cant-handle-219-ultrawide-monitors-heres-why) · The FPS Review: [19:9 con viñetas](https://www.thefpsreview.com/2021/09/08/blizzard-drops-219-ultrawide-support-in-diablo-ii-resurrected-says-it-was-breaking-the-game)
- Blizzplanet: [menú de opciones](https://blizzplanet.substack.com/p/diablo-ii-resurrected-settings-ui) · [herramientas del original (1999)](https://blizzplanet.substack.com/p/why-diablo-ii-resurrected-is-a-big-deal)
- Windows Central: [alfa técnica](https://windowscentral.com/diablo-2-resurrected-tech-alpha-hands) · Gematsu: [ficha](https://www.gematsu.com/games/diablo-ii-resurrected)
- Fonts In Use: [logo y menú de Diablo II (Exocet)](https://www.fontsinuse.com/uses/60790/diablo-ii-logo-and-menu)

**Comunidad de modding**
- Foro de Blizzard: [guía de modding (CASC, JSON)](https://us.forums.blizzard.com/en/d2r/t/modding-guides-episode-1-how-to-show-item-levels-by-zax/160091)
- Inven (coreano): [archivos de layout y sprites de paneles](https://www.inven.co.kr/board/diablo2/5842/7840) · [herramienta de posiciones del HUD](https://www.inven.co.kr/board/diablo2/5842/7635)
- [D2RMM: documentación](https://github.com/olegbl/d2rmm/blob/master/DOCS.md)

**Tecnologías propuestas**
- Unity: [comparación de sistemas de UI](https://docs.unity3d.com/Manual/UI-system-compare.html)
- Fuentes OFL: [Cinzel](https://fonts.google.com/specimen/Cinzel) · [EB Garamond](https://fonts.google.com/specimen/EB+Garamond) · [Cormorant](https://fonts.google.com/specimen/Cormorant)
- [Material Maker](https://www.materialmaker.org/) · [Krita](https://krita.org/) · [Blender](https://www.blender.org/)

> **Pendiente de verificar:** las referencias visuales (capturas) no están en este documento porque son material con derechos de Blizzard. La tarea UI-0 las reúne para uso interno del equipo.
