# Frostbound — equipamiento de héroes

Equipamiento modelado sobre `Penguin.obj`, con las mismas medidas y ejes (Y arriba, +Z al frente).
Cada clase es un solo archivo con sus piezas por separado. Colócalo como hijo del pingüino en (0,0,0) y encaja solo.

| Archivo | Piezas | Triángulos |
|---|---|---|
| Frostbound_Knight_Gear.obj | Helm, Chest, Surcoat, Cape, Shoulder_L/R, Gauntlet_L/R, Weapon_R (mandoble) | ~8.000 |
| Frostbound_Mage_Gear.obj | Hat, Beard, Robe, Cloak, Belt, Weapon_R | ~5.800 |
| Frostbound_Ninja_Gear.obj | Hood, Suit, Wraps_L/R, Katana_Back, Weapon_R (kunai), Weapon_L (shuriken) | ~3.700 |
| Frostbound_Viking_Gear.obj | Helm, Fur, Shoulder_R, Belt, Kilt, Weapon_R, Offhand_L | ~4.900 |

## NPC
- `Models/NPC/Frostbound_NPC_Rocker_Gear.obj`: Rocco Vendaval, el rockero de la hoguera (Hair, Glasses, Coat, Sleeve_L/R, Pendant, ~4.400 tris). Plumaje amarillo #F2C230.
  Los rizos van pintados en la textura `Rocker_Hair_Curls.png`. El paso 1 también crea su prefab y le pone la textura al material del cabello.

## Instalación
1. Copia la carpeta `Assets/Frostbound/Gear` dentro de `Assets/` de tu proyecto.
2. En Unity: **Tools > Frostbound > Equipamiento > 1. Preparar prefabs de equipo**.
   Crea materiales con el shader `Frostbound/Toon` (color plano, sombra dura y contorno) y prefabs en `Gear/Prefabs`.
   Copia la escala de importación de `Penguin.obj` para que coincidan.
3. Selecciona el jugador y ejecuta **2. Añadir equipador al objeto seleccionado**.
   El componente `HeroGearEquipper` queda con las 4 clases cargadas. `startingClass` define cuál aparece al iniciar.
4. Opcional: **3. Aplicar toon al pingüino seleccionado** para que el cuerpo use el mismo shader (conserva su textura).

## Desde código
```csharp
GetComponent<HeroGearEquipper>().Equip("Mage");
GetComponent<HeroGearEquipper>().SetPartVisible("Helm", false);
```

## Notas
- Las armas cuelgan de la punta de las aletas (Weapon_R / Offhand_L). Como el pingüino aún no tiene rig, se mueven con el cuerpo entero.
- Cuando el pingüino tenga huesos, cada pieza se puede volver a colgar de su hueso sin volver a modelarla.
- Los materiales de hielo y cristal tienen emisión: con Bloom activo en el Volume de URP brillan.
