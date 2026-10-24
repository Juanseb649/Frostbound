"""
Frostbound — armaduras nuevas de caballero y shinobi, derivadas del equipo de clase
(Frostbound_Knight_Gear / Frostbound_Ninja_Gear, modelado sobre Penguin.obj).

Cada set reutiliza las piezas base (mismos nombres: Helm, Chest, Hood, Suit…, así el pesado de
frostbound_gear_to_rig.py y las piezas de Unity siguen funcionando), cambia la paleta y añade
detalles nuevos que se unen a la pieza correspondiente:
  • Lobo Negro (caballero, inspirado en el berserker de Berserk): yelmo de hocico de lobo con orejas,
    ojos rojos, hombreras con púas y capa negra.
  • Guardián de Plata (caballero, inspirado en los caballeros de plata de FromSoftware): plata y oro,
    penacho blanco y emblema solar en el peto.
  • Búho (shinobi, inspirado en Sekiro): plumas de búho en la capucha, gorguera y manto de plumas.
  • Lobo Manco (shinobi, inspirado en Sekiro): bufanda ocre, coleta y prótesis de hierro en la aleta izquierda.

Uso: blender -b -P frostbound_armors.py -- "Gear_Source/Models"
Escribe Frostbound_<Set>_Gear.obj/.mtl en esa carpeta; después se pasan por frostbound_gear_to_rig.py.
"""

import math
import os
import sys

import bpy
from mathutils import Euler, Vector

GEAR_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Gear_Source", "Models")
if "--" in sys.argv:
    a = sys.argv[sys.argv.index("--") + 1:]
    if a:
        GEAR_DIR = a[0]

# Tras importar el OBJ: Z arriba, el frente mira a -Y, +X es la izquierda del pingüino.
HEAD_C = Vector((0.0, 0.0, 0.30))


def log(msg):
    print("[Frostbound] " + msg)


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def material(name, color, emit=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = hex_rgb(color) + (1.0,)
    bsdf.inputs["Roughness"].default_value = 1.0
    bsdf.inputs["Specular IOR Level"].default_value = 0.0
    if emit:
        bsdf.inputs["Emission Color"].default_value = hex_rgb(emit) + (1.0,)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    else:
        bsdf.inputs["Emission Strength"].default_value = 0.0
    m.diffuse_color = hex_rgb(color) + (1.0,)
    return m


def load_base(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.obj_import(filepath=os.path.join(GEAR_DIR, name))
    objs = {}
    for o in list(bpy.data.objects):
        if o.type != "MESH":
            continue
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        objs[o.name.split(".")[0]] = o
    return objs


def drop(objs, *names):
    for n in names:
        o = objs.pop(n, None)
        if o:
            bpy.data.objects.remove(o, do_unlink=True)


def recolor(objs, mapping):
    """mapping: material base -> (nuevo nombre, color, emisión)."""
    made = {}
    for o in objs.values():
        for slot in o.material_slots:
            if slot.material and slot.material.name in mapping:
                old = slot.material.name
                if old not in made:
                    made[old] = material(*mapping[old])
                slot.material = made[old]


def finish(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = False
    return obj


def cone(mat, base, tip, r1, r2=0.0, verts=8):
    base, tip = Vector(base), Vector(tip)
    d = tip - base
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=d.length,
                                    location=(base + tip) * 0.5)
    o = bpy.context.active_object
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return finish(o, mat)


def ellipsoid(mat, loc, size, rot=(0, 0, 0), seg=10, rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1.0, location=loc)
    o = bpy.context.active_object
    o.scale = size
    o.rotation_euler = Euler([math.radians(r) for r in rot])
    return finish(o, mat)


def box(mat, loc, size, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=2.0, location=loc)
    o = bpy.context.active_object
    o.scale = size
    o.rotation_euler = Euler([math.radians(r) for r in rot])
    return finish(o, mat)


def ring(mat, loc, major, minor, rot=(0, 0, 0), seg=20):
    bpy.ops.mesh.primitive_torus_add(major_segments=seg, minor_segments=6, major_radius=major,
                                     minor_radius=minor, location=loc,
                                     rotation=[math.radians(r) for r in rot])
    return finish(bpy.context.active_object, mat)


def cylinder(mat, loc, radius, depth, rot=(0, 0, 0), verts=10):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc,
                                        rotation=[math.radians(r) for r in rot])
    return finish(bpy.context.active_object, mat)


def attach(objs, piece, *extras):
    """Une los detalles a la pieza base para que hereden su nombre y su pesado."""
    target = objs[piece]
    bpy.ops.object.select_all(action="DESELECT")
    for e in extras:
        bpy.context.view_layer.objects.active = e
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        e.select_set(True)
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()
    target.name = target.data.name = piece


def surface_y(obj, x, z, front=True):
    """Y de la superficie de una pieza (el vértice más adelantado o atrasado cerca de x, z)."""
    best = None
    for v in obj.data.vertices:
        if abs(v.co.x - x) < 0.06 and abs(v.co.z - z) < 0.06:
            if best is None or (v.co.y < best if front else v.co.y > best):
                best = v.co.y
    return best if best is not None else (-0.35 if front else 0.35)


def top_z(obj, x, y):
    best = None
    for v in obj.data.vertices:
        if abs(v.co.x - x) < 0.06 and abs(v.co.y - y) < 0.06:
            best = v.co.z if best is None else max(best, v.co.z)
    return best if best is not None else 0.5


def export(objs, name, title):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs.values():
        o.select_set(True)
    path = os.path.join(GEAR_DIR, "Frostbound_%s_Gear.obj" % name)
    bpy.ops.wm.obj_export(filepath=path, export_selected_objects=True, export_materials=True,
                          export_normals=True, export_uv=True, export_triangulated_mesh=False,
                          forward_axis="NEGATIVE_Z", up_axis="Y", path_mode="STRIP")
    with open(path, "r", encoding="utf-8") as f:
        body = f.read()
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("# Frostbound gear kit: %s\n# Units/axes match Penguin.obj (Y up, +Z = front)\n" % title + body)
    log("exportado " + path)


# ---------- Caballeros ----------

def knight_black_wolf():
    objs = load_base("Frostbound_Knight_Gear.obj")
    drop(objs, "Weapon_R")
    recolor(objs, {
        "Knight_Steel": ("Wolf_Iron", "#2B2D33"),
        "Knight_Iron": ("Wolf_Black", "#16171B"),
        "Knight_Gold": ("Wolf_Bronze", "#5C2A1E"),
        "Knight_Cloth": ("Wolf_Cape", "#1E1416"),
        "Knight_Leather": ("Wolf_Leather", "#2A1C14"),
        "Knight_Slit": ("Wolf_Eyes", "#FF2A1A", "#FF2410"),
        "Knight_Frost": ("Wolf_Ember", "#C8321E", "#FF3A1A"),
    })
    iron = bpy.data.materials["Wolf_Iron"]
    black = bpy.data.materials["Wolf_Black"]
    fang = material("Wolf_Fang", "#D9D2BF")
    eyes = bpy.data.materials["Wolf_Eyes"]

    helm = objs["Helm"]
    fy = surface_y(helm, 0.0, 0.24)
    top = top_z(helm, 0.0, 0.0)
    sy = fy + 0.04
    extras = [
        # Hocico de lobo, mandíbula y colmillos
        ellipsoid(iron, (0, sy - 0.07, 0.24), (0.12, 0.15, 0.075), seg=12),
        ellipsoid(black, (0, sy - 0.05, 0.17), (0.10, 0.12, 0.04), seg=10),
        ellipsoid(black, (0, sy - 0.10, 0.31), (0.06, 0.13, 0.035), rot=(-12, 0, 0)),
        cone(fang, (0.06, sy - 0.12, 0.205), (0.06, sy - 0.13, 0.155), 0.016, 0.0, 6),
        cone(fang, (-0.06, sy - 0.12, 0.205), (-0.06, sy - 0.13, 0.155), 0.016, 0.0, 6),
        cone(fang, (0.03, sy - 0.17, 0.205), (0.03, sy - 0.175, 0.17), 0.012, 0.0, 6),
        cone(fang, (-0.03, sy - 0.17, 0.205), (-0.03, sy - 0.175, 0.17), 0.012, 0.0, 6),
        # Ojos rojos
        ellipsoid(eyes, (0.095, fy + 0.01, 0.335), (0.04, 0.015, 0.016), rot=(0, -18, 0), seg=8, rings=4),
        ellipsoid(eyes, (-0.095, fy + 0.01, 0.335), (0.04, 0.015, 0.016), rot=(0, 18, 0), seg=8, rings=4),
        # Orejas
        cone(iron, (0.16, 0.03, top - 0.06), (0.24, 0.08, top + 0.16), 0.075, 0.0, 6),
        cone(iron, (-0.16, 0.03, top - 0.06), (-0.24, 0.08, top + 0.16), 0.075, 0.0, 6),
    ]
    # Cresta de púas sobre el yelmo
    for i, t in enumerate((-0.12, -0.02, 0.08, 0.18)):
        z = top_z(helm, 0.0, t)
        extras.append(cone(black, (0, t, z - 0.03), (0, t + 0.07, z + 0.09 - i * 0.015), 0.03, 0.0, 6))
    attach(objs, "Helm", *extras)

    for side in (1, -1):
        sh = objs["Shoulder_L" if side > 0 else "Shoulder_R"]
        spikes = []
        for y, h in ((-0.08, 0.13), (0.0, 0.18), (0.08, 0.13)):
            z = top_z(sh, side * 0.28, y)
            spikes.append(cone(black, (side * 0.28, y, z - 0.03), (side * 0.36, y * 1.3, z + h), 0.04, 0.0, 6))
        attach(objs, sh.name.split(".")[0], *spikes)

    # Peto: costillas de hierro negro
    chest = objs["Chest"]
    ribs = []
    for z in (-0.02, -0.09, -0.16):
        y = surface_y(chest, 0.0, z)
        ribs.append(box(black, (0, y - 0.012, z), (0.13, 0.012, 0.016), rot=(0, 0, 0)))
    attach(objs, "Chest", *ribs)
    export(objs, "BlackWolf", "Armadura del Lobo Negro")


def knight_silver():
    objs = load_base("Frostbound_Knight_Gear.obj")
    drop(objs, "Weapon_R")
    recolor(objs, {
        "Knight_Steel": ("Silver_Plate", "#C7CFDA"),
        "Knight_Iron": ("Silver_Dark", "#7D8796"),
        "Knight_Gold": ("Silver_Gold", "#D9B24C"),
        "Knight_Cloth": ("Silver_Tabard", "#ECE6D6"),
        "Knight_Leather": ("Silver_Leather", "#5A3E2A"),
        "Knight_Slit": ("Silver_Slit", "#0B0C10"),
        "Knight_Frost": ("Silver_Gem", "#7FD3FF", "#3FA8FF"),
    })
    gold = bpy.data.materials["Silver_Gold"]
    plume = material("Silver_Plume", "#F2EEE4")
    plate = bpy.data.materials["Silver_Plate"]

    helm = objs["Helm"]
    extras = []
    # Penacho: cadena de plumas sobre la cresta, cae hacia atrás
    for i in range(7):
        t = i / 6.0
        y = -0.14 + t * 0.52
        z = top_z(helm, 0.0, min(y, 0.3)) + 0.06 - t * t * 0.32
        extras.append(ellipsoid(plume, (0, y, z), (0.045, 0.09, 0.06 - t * 0.012), rot=(-20 - t * 50, 0, 0)))
    z0 = top_z(helm, 0.0, -0.14)
    extras.append(cylinder(gold, (0, -0.14, z0 + 0.01), 0.035, 0.06))
    # Corona fina de oro alrededor del yelmo
    fy = surface_y(helm, 0.0, 0.42)
    extras.append(ring(gold, (0, 0, 0.42), abs(fy) + 0.005, 0.014))
    attach(objs, "Helm", *extras)

    chest = objs["Chest"]
    y = surface_y(chest, 0.0, -0.1)
    sun = [cylinder(gold, (0, y - 0.01, -0.1), 0.06, 0.02, rot=(90, 0, 0), verts=16)]
    for k in range(8):
        a = k * math.pi / 4
        sun.append(cone(gold, (math.cos(a) * 0.05, y - 0.012, -0.1 + math.sin(a) * 0.05),
                        (math.cos(a) * 0.105, y - 0.012, -0.1 + math.sin(a) * 0.105), 0.022, 0.0, 4))
    attach(objs, "Chest", *sun)

    for side in (1, -1):
        name = "Shoulder_L" if side > 0 else "Shoulder_R"
        sh = objs[name]
        z = top_z(sh, side * 0.27, 0.0)
        attach(objs, name, ellipsoid(plate, (side * 0.27, 0.0, z + 0.01), (0.11, 0.13, 0.035), rot=(0, side * -25, 0), seg=12))
    export(objs, "Silver", "Armadura del Guardián de Plata")


# ---------- Shinobis ----------

def shinobi_owl():
    objs = load_base("Frostbound_Ninja_Gear.obj")
    drop(objs, "Weapon_R", "Weapon_L")
    recolor(objs, {
        "Ninja_Black": ("Owl_Cloth", "#3D3833"),
        "Ninja_Charcoal": ("Owl_Wrap", "#8B7D67"),
        "Ninja_Cord": ("Owl_Cord", "#6B4A8E"),
        "Ninja_Steel": ("Owl_Steel", "#B8C2CE"),
        "Ninja_DarkSteel": ("Owl_DarkSteel", "#4A4F59"),
        "Ninja_IceEdge": ("Owl_Edge", "#C9B6FF", "#8A6CFF"),
    })
    feather = material("Owl_Feather", "#6E5E4A")
    light = material("Owl_FeatherLight", "#B9A98C")

    hood = objs["Hood"]
    top = top_z(hood, 0.0, 0.0)
    tufts = []
    for side in (1, -1):
        for k, (dx, h) in enumerate(((0.0, 0.2), (0.05, 0.15))):
            tufts.append(cone(feather if k == 0 else light, (side * (0.13 + dx), 0.02, top - 0.07),
                              (side * (0.22 + dx * 1.5), 0.07, top + h), 0.05, 0.0, 6))
    attach(objs, "Hood", *tufts)

    suit = objs["Suit"]
    extras = []
    # Gorguera de plumas al cuello
    for k in range(18):
        a = k * 2 * math.pi / 18
        d = Vector((math.cos(a), math.sin(a), 0.0))
        base = d * 0.27 + Vector((0, 0, 0.09))
        tip = d * 0.40 + Vector((0, 0, 0.0))
        extras.append(cone(feather if k % 2 else light, base, tip, 0.055, 0.0, 4))
    # Manto de plumas en la espalda (tres filas escalonadas)
    for row, z in enumerate((0.02, -0.09, -0.20)):
        n = 5 - row
        for i in range(n):
            x = (i - (n - 1) / 2) * 0.11
            y = surface_y(suit, x, z, front=False)
            extras.append(ellipsoid(feather if (i + row) % 2 else light, (x, y + 0.02, z - 0.04),
                                    (0.055, 0.02, 0.11), rot=(-8, 0, (i - (n - 1) / 2) * 6)))
    attach(objs, "Suit", *extras)
    export(objs, "Owl", "Atuendo del Búho")


def shinobi_lone_wolf():
    objs = load_base("Frostbound_Ninja_Gear.obj")
    drop(objs, "Weapon_R", "Weapon_L")
    recolor(objs, {
        "Ninja_Black": ("Lone_Cloth", "#383D46"),
        "Ninja_Charcoal": ("Lone_Leather", "#5E4B39"),
        "Ninja_Cord": ("Lone_Cord", "#C08A3E"),
        "Ninja_Steel": ("Lone_Steel", "#C2CAD4"),
        "Ninja_DarkSteel": ("Lone_DarkSteel", "#3E434B"),
        "Ninja_IceEdge": ("Lone_Edge", "#FFE3B0", "#FFB04A"),
    })
    scarf = material("Lone_Scarf", "#B5652A")
    iron = material("Lone_Iron", "#4B4F57")
    brass = material("Lone_Brass", "#A88443")
    hair = material("Lone_Hair", "#22242A")

    suit = objs["Suit"]
    by = surface_y(suit, 0.08, -0.05, front=False)
    attach(objs, "Suit",
           ring(scarf, (0, 0, 0.085), 0.30, 0.05, seg=24),
           box(scarf, (0.09, by + 0.03, -0.08), (0.055, 0.018, 0.17), rot=(-10, 0, 8)),
           box(scarf, (0.15, by + 0.025, -0.13), (0.04, 0.016, 0.15), rot=(-12, 0, 18)))

    hood = objs["Hood"]
    hy = surface_y(hood, 0.0, 0.40, front=False)
    attach(objs, "Hood",
           ellipsoid(hair, (0, hy + 0.03, 0.40), (0.06, 0.05, 0.06)),
           cone(hair, (0, hy + 0.06, 0.40), (0, hy + 0.2, 0.30), 0.045, 0.01, 6),
           ring(scarf, (0, hy + 0.05, 0.40), 0.04, 0.012, rot=(90, 0, 0), seg=12))

    # Prótesis de hierro en la aleta izquierda
    attach(objs, "Wraps_L",
           box(iron, (0.40, 0.0, -0.14), (0.065, 0.04, 0.07)),
           cylinder(brass, (0.40, 0.0, -0.075), 0.06, 0.03, verts=12),
           cylinder(iron, (0.43, 0.0, -0.22), 0.035, 0.06, verts=8),
           cone(material("Lone_Blade", "#C2CAD4"), (0.44, 0.0, -0.25), (0.46, 0.0, -0.31), 0.02, 0.0, 4))
    export(objs, "LoneWolf", "Atuendo del Lobo Manco")


def main():
    knight_black_wolf()
    knight_silver()
    shinobi_owl()
    shinobi_lone_wolf()


if __name__ == "__main__":
    main()
