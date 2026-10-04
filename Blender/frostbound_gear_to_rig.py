"""
Frostbound — adapta el equipo del paquete Frostbound_Gear (modelado sobre Penguin.obj)
al pingüino con esqueleto (Penguin_Rigged.blend) y lo exporta como FBX con pesos.

Reglas de pesado por pieza:
  • Cabeza rígida: Helm, Hat, Hood, Hair, Glasses, Beard            → Head
  • Armas rígidas en la punta de la aleta: Weapon_R/L, Offhand_L   → Flipper_R / Flipper_L
  • Rígidas en la espalda o el pecho: Katana_Back, Pendant, Shoulder_* → Spine
  • Brazos (guanteletes, vendas, mangas): copian el peso de la aleta y el cuerpo más cercanos
  • Ropa del torso (peto, túnica, capa, cinturón, pieles, gabardina…): copian el peso del cuerpo

Uso: blender -b -P frostbound_gear_to_rig.py -- "Penguin_Rigged.blend" "carpeta/Models" "salida/Assets/Models/Gear" "Penguin.obj"
"""

import os
import sys

import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree

RIG_BLEND = r"C:\Users\Sebastian\My project (2)\Blender\Penguin_Rigged.blend"
GEAR_DIR = r"C:\Users\Sebastian\My project (2)\Assets\Frostbound\Gear\Models"
OUT_DIR = r"C:\Users\Sebastian\My project (2)\Assets\Models\Gear"
PENGUIN_OBJ = r"C:\Users\Sebastian\My project (2)\Assets\Penguin.obj"
if "--" in sys.argv:
    a = sys.argv[sys.argv.index("--") + 1:]
    RIG_BLEND, GEAR_DIR, OUT_DIR, PENGUIN_OBJ = (a + [RIG_BLEND, GEAR_DIR, OUT_DIR, PENGUIN_OBJ][len(a):])[:4]

GEAR_FILES = [
    os.path.join("Enemies", "Frostbound_Corrupt_Melee_Gear.obj"),
    os.path.join("Enemies", "Frostbound_Corrupt_Ranged_Gear.obj"),
    os.path.join("Enemies", "Frostbound_Corrupt_Knight_Gear.obj"),
    os.path.join("Enemies", "Frostbound_Corrupt_Mage_Gear.obj"),
    os.path.join("Enemies", "Frostbound_Corrupt_Ninja_Gear.obj"),
    os.path.join("Enemies", "Frostbound_Corrupt_Viking_Gear.obj"),
    "Frostbound_Knight_Gear.obj",
    "Frostbound_Mage_Gear.obj",
    "Frostbound_Ninja_Gear.obj",
    "Frostbound_Viking_Gear.obj",
    os.path.join("NPC", "Frostbound_NPC_Rocker_Gear.obj"),
]

HEAD = {"Helm", "Hat", "Hood", "Hair", "Glasses", "Beard", "Eyes"}
SPINE = {"Katana_Back", "Pendant", "Shoulder_L", "Shoulder_R", "Quiver"}
ANY = {"Frost_Icicles", "Frost_Crystals", "Frost_Crust"}
ARMS = {"Gauntlet_L", "Gauntlet_R", "Wraps_L", "Wraps_R", "Sleeve_L", "Sleeve_R"}
WEAPON_SIDE = {"Weapon_R": "R", "Weapon_L": "L", "Offhand_L": "L"}


def log(msg):
    print("[Frostbound] " + msg)


def bounds(points):
    xs = [p.x for p in points]; ys = [p.y for p in points]; zs = [p.z for p in points]
    return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))


def world_verts(obj):
    return [obj.matrix_world @ v.co for v in obj.data.vertices]


def penguin_offset(parts):
    """Traslación que lleva las coordenadas de Penguin.obj al pingüino con esqueleto (centro de la caja)."""
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=PENGUIN_OBJ)
    raw = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    pts = [p for o in raw for p in world_verts(o)]
    lo, hi = bounds(pts)
    for o in raw:
        bpy.data.objects.remove(o, do_unlink=True)
    rig_pts = [p for o in parts.values() for p in world_verts(o)]
    rlo, rhi = bounds(rig_pts)
    offset = (rlo + rhi) * 0.5 - (lo + hi) * 0.5
    log("desplazamiento Penguin.obj → rig: (%.4f, %.4f, %.4f)" % tuple(offset))
    return offset


def weight_sources(parts):
    """Lista de (posición, {hueso: peso}) por vértice del pingüino, separada por grupos."""
    def collect(obj):
        names = {g.index: g.name for g in obj.vertex_groups}
        out = []
        for v in obj.data.vertices:
            w = {names[g.group]: g.weight for g in v.groups if g.weight > 0.001}
            out.append((obj.matrix_world @ v.co, w))
        return out

    body = collect(parts["Body"])
    sources = {
        "body": body,
        "L": body + collect(parts["Flipper_L"]),
        "R": body + collect(parts["Flipper_R"]),
        "all": body + collect(parts["Flipper_L"]) + collect(parts["Flipper_R"]) + (collect(parts["Head"]) if "Head" in parts else []),
    }
    trees = {}
    for key, data in sources.items():
        tree = KDTree(len(data))
        for i, (co, _) in enumerate(data):
            tree.insert(co, i)
        tree.balance()
        trees[key] = (tree, data)
    return trees


def copy_weights(obj, tree_data, k=6):
    tree, data = tree_data
    for v in obj.data.vertices:
        co = obj.matrix_world @ v.co
        acc = {}
        total = 0.0
        for _, idx, dist in tree.find_n(co, k):
            wgt = 1.0 / max(dist, 1e-4) ** 2
            total += wgt
            for bone, w in data[idx][1].items():
                acc[bone] = acc.get(bone, 0.0) + w * wgt
        for bone, w in acc.items():
            w /= total
            if w < 0.01:
                continue
            group = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
            group.add([v.index], w, "REPLACE")


def rigid(obj, bone):
    group = obj.vertex_groups.new(name=bone)
    group.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")


def piece_name(obj):
    return obj.name.split(".")[0]


def convert(gear_path, arm, parts, offset, trees):
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=gear_path)
    pieces = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    base = os.path.splitext(os.path.basename(gear_path))[0]

    for obj in pieces:
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        for v in obj.data.vertices:
            v.co += offset
        name = piece_name(obj)
        obj.name = obj.data.name = name

        if name in HEAD:
            rigid(obj, "Head")
        elif name in WEAPON_SIDE:
            rigid(obj, "Flipper_" + WEAPON_SIDE[name])
        elif name in SPINE:
            rigid(obj, "Spine")
        elif name in ANY:
            copy_weights(obj, trees["all"])
        elif name in ARMS:
            copy_weights(obj, trees[name[-1]])
        else:
            copy_weights(obj, trees["body"])

        obj.parent = arm
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = arm

    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, base + ".fbx")
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    for obj in pieces:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=out, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        use_mesh_modifiers=False, mesh_smooth_type="FACE",
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        use_armature_deform_only=False, bake_anim=False, path_mode="STRIP")
    log("exportado %s (%d piezas)" % (out, len(pieces)))
    for obj in pieces:
        bpy.data.objects.remove(obj, do_unlink=True)


def main():
    bpy.ops.wm.open_mainfile(filepath=RIG_BLEND)
    arm = bpy.data.objects["PenguinRig"]
    parts = {n.replace("Penguin_", ""): o for n, o in ((o.name, o) for o in bpy.data.objects) if n.startswith("Penguin_") and o.type == "MESH"}
    offset = penguin_offset(parts)
    trees = weight_sources(parts)
    for rel in GEAR_FILES:
        path = os.path.join(GEAR_DIR, rel)
        if os.path.exists(path):
            convert(path, arm, parts, offset, trees)
        else:
            log("no existe " + path)


if __name__ == "__main__":
    main()
