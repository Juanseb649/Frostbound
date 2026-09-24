# Frostbound — pipeline del pingüino vestible

## Archivos
- `frostbound_penguin_rig.py`: Penguin.obj → malla limpia (~9k tris), partes (Body, Flipper_L/R, Foot_L/R),
  máscaras de color, esqueleto con anclajes, exporta `Assets/Models/Penguin/Penguin_Rigged.fbx`.
- `frostbound_outfits.py`: crea las 4 prendas sobre ese esqueleto y exporta `Assets/Models/Outfits/*.fbx`.
- `Penguin_Rigged.blend`, `Outfits.blend`: los resultados, para editarlos a mano.

## Regenerar (Blender 4.2+)
    blender -b -P frostbound_penguin_rig.py -- "..\Assets\Penguin.obj" "..\Assets\Models\Penguin" "."
    blender -b -P frostbound_outfits.py -- "Penguin_Rigged.blend" "..\Assets\Models\Outfits"
Después en Unity: Tools > Frostbound > Configurar Pingüino Vestible.

## Huesos
Root, Hips, Spine, Head, Flipper_L/R, Foot_L/R (deforman).
Anclajes (no deforman): Anchor_Head, Anchor_Chest, Anchor_Back, Anchor_Hand_L, Anchor_Hand_R.

## Máscaras de color (atributo "Mask" del pingüino)
R = barriga y ojos (blanco), G = pico y patas (naranja), B = pupilas. El shader corta en 0.5.

## Prendas nuevas
- Hecha a medida (se deforma): modelarla en `Penguin_Rigged.blend`, pesarla a los mismos huesos, exportar
  el FBX con PenguinRig + la malla, colores en el atributo "Color". OutfitItem en modo Skinned.
- Objeto rígido (sombrero, arma o mochila de otro paquete): OutfitItem en modo Anchor, eliges la ranura
  (Head, Chest, Back, HandL, HandR) y ajustas posición/rotación/escala.
