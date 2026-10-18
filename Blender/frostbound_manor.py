# Frostbound — mansión helada del menú (guía /d2r-ui, fase 2).
# Construye la mansión con geometría de verdad (ventanas góticas recortadas en el muro, marcos, parteluces,
# esquineras, cornisas con ménsulas, tejados con alero, torre con chapitel roto, porche con columnas) en las
# MISMAS coordenadas que HauntedManor usa en Unity, para que cristales, luces, cortina y carámbanos encajen.
# Los materiales llevan el nombre que HauntedManor busca: Piedra, PiedraOscura, Cornisa, Pizarra, Madera, Hierro,
# FrostA, FrostB, FrostC, Rota y Vela. Exporta un FBX.
#
# Uso:  blender -b -P frostbound_manor.py -- "<ruta del .fbx>"
#
# Coordenadas: se escribe todo en el espacio de Unity (x derecha, y arriba, z hacia el fondo). U() lo pasa a Blender.

import math
import sys

import bmesh
import bpy
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "Mansion.fbx"


def U(x, y, z):
    """Unity (x, y, z) -> Blender (-x, -z, y). Con el exportador (eje -Z delante, Y arriba) y el volteo de X que hace
    Unity al importar, vuelve al espacio de Unity (comprobado con UIKitChecks.ManorBounds: la ventana de la vela cae
    en x = +9,4 y la fachada en z = 15). Es un espejo: finish() recalcula las normales hacia fuera."""
    return Vector((-x, -z, y))


# ---------- Escena ----------

bpy.ops.wm.read_factory_settings(use_empty=True)
MATS = {}


def mat(name):
    if name not in MATS:
        m = bpy.data.materials.new(name)
        m.use_nodes = False
        MATS[name] = m
    return MATS[name]


PARTS = {}  # nombre de material -> lista de bmesh por unir


def add(bm, material):
    PARTS.setdefault(material, []).append(bm)


def finish():
    """Une los bmesh de cada material en un objeto, con normales hacia fuera."""
    for material, pieces in PARTS.items():
        me = bpy.data.meshes.new("Mansion_" + material)
        total = bmesh.new()
        for bm in pieces:
            tmp = bpy.data.meshes.new("tmp")
            bm.to_mesh(tmp)
            total.from_mesh(tmp)
            bpy.data.meshes.remove(tmp)
            bm.free()
        bmesh.ops.remove_doubles(total, verts=total.verts, dist=0.0005)
        bmesh.ops.recalc_face_normals(total, faces=total.faces)
        total.to_mesh(me)
        total.free()
        for p in me.polygons:
            p.use_smooth = False
        me.materials.append(mat(material))
        ob = bpy.data.objects.new("Mansion_" + material, me)
        bpy.context.scene.collection.objects.link(ob)


# ---------- Primitivas en espacio de Unity ----------

def box(center, size, material, rot_y=0.0, rot_x=0.0, rot_z=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        p = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        p = rotate(p, rot_x, rot_y, rot_z)
        v.co = U(center[0] + p.x, center[1] + p.y, center[2] + p.z)
    add(bm, material)


def rotate(p, rx, ry, rz):
    # Rotación en el orden de Unity (z, x, y) en grados, en espacio de Unity.
    def rot(v, axis, deg):
        a = math.radians(deg)
        c, s = math.cos(a), math.sin(a)
        x, y, z = v
        if axis == "x":
            return Vector((x, y * c - z * s, y * s + z * c))
        if axis == "y":
            return Vector((x * c + z * s, y, -x * s + z * c))
        return Vector((x * c - y * s, x * s + y * c, z))
    p = rot(p, "z", rz)
    p = rot(p, "x", rx)
    return rot(p, "y", ry)


def prism_from_outline(points, z0, z1, material, offset=(0.0, 0.0)):
    """Extruye un contorno 2D (x, y) entre z0 y z1 (espacio de Unity)."""
    bm = bmesh.new()
    front = [bm.verts.new(U(offset[0] + x, offset[1] + y, z0)) for x, y in points]
    back = [bm.verts.new(U(offset[0] + x, offset[1] + y, z1)) for x, y in points]
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    n = len(points)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], front[j], back[j], back[i]))
    add(bm, material)


def ring_from_outlines(outer, inner, z0, z1, material, offset=(0.0, 0.0)):
    """Marco: el anillo entre dos contornos con el mismo número de puntos, extruido entre z0 y z1."""
    bm = bmesh.new()
    n = len(outer)
    of = [bm.verts.new(U(offset[0] + x, offset[1] + y, z0)) for x, y in outer]
    ob = [bm.verts.new(U(offset[0] + x, offset[1] + y, z1)) for x, y in outer]
    inf = [bm.verts.new(U(offset[0] + x, offset[1] + y, z0)) for x, y in inner]
    inb = [bm.verts.new(U(offset[0] + x, offset[1] + y, z1)) for x, y in inner]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((of[i], of[j], inf[j], inf[i]))
        bm.faces.new((ob[j], ob[i], inb[i], inb[j]))
        bm.faces.new((of[j], of[i], ob[i], ob[j]))
        bm.faces.new((inf[i], inf[j], inb[j], inb[i]))
    add(bm, material)


def arch(w, h, rise, steps=10):
    """Contorno de ventana gótica: rectángulo de w × h (centrado) rematado por un arco apuntado de altura rise."""
    # Arco apuntado de verdad: cada mitad es un arco de círculo de radio r = 0.9·w con el centro sobre la línea de
    # arranque, del lado contrario; las dos mitades se cruzan en la clave (x = 0). Se escala en altura hasta rise.
    r = 0.9 * w
    cx = w / 2 - r                      # centro del arco de la mitad derecha
    top = math.acos(-cx / r)            # ángulo en el que el arco llega a x = 0
    natural = r * math.sin(top)
    k = rise / natural
    right = []
    for i in range(1, steps + 1):
        a = top * i / steps
        right.append((cx + r * math.cos(a), h / 2 + r * math.sin(a) * k))
    pts = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2)]
    pts += right                         # sube por la derecha hasta la clave
    pts += [(-x, y) for x, y in reversed(right[:-1])]  # y baja, en espejo, por la izquierda
    pts.append((-w / 2, h / 2))
    return pts


def cylinder(center, diameter, height, material, sides=12):
    pts = [(diameter / 2 * math.cos(2 * math.pi * i / sides), diameter / 2 * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
    bm = bmesh.new()
    bot = [bm.verts.new(U(center[0] + x, center[1] - height / 2, center[2] + y)) for x, y in pts]
    top = [bm.verts.new(U(center[0] + x, center[1] + height / 2, center[2] + y)) for x, y in pts]
    bm.faces.new(bot)
    bm.faces.new(list(reversed(top)))
    for i in range(sides):
        j = (i + 1) % sides
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    add(bm, material)


def cone(base, diameter, height, material, sides=8, tilt_x=0.0, tilt_z=0.0):
    bm = bmesh.new()
    ring = []
    for i in range(sides):
        a = 2 * math.pi * i / sides + math.pi / sides
        p = rotate(Vector((diameter / 2 * math.cos(a), 0.0, diameter / 2 * math.sin(a))), tilt_x, 0, tilt_z)
        ring.append(bm.verts.new(U(base[0] + p.x, base[1] + p.y, base[2] + p.z)))
    tip = rotate(Vector((0.0, height, 0.0)), tilt_x, 0, tilt_z)
    apex = bm.verts.new(U(base[0] + tip.x, base[1] + tip.y, base[2] + tip.z))
    bm.faces.new(ring)
    for i in range(sides):
        bm.faces.new((ring[(i + 1) % sides], ring[i], apex))
    add(bm, material)


def gable_roof(base_center, x_len, depth, height, material, overhang=0.35, thick=0.22):
    """Tejado a dos aguas con la cumbrera a lo largo de x: dos faldones con grosor, alero y cumbrera."""
    cx, by, cz = base_center
    half = depth / 2 + overhang
    slope = math.atan2(height, depth / 2)
    length = math.hypot(depth / 2 + overhang, height + overhang * math.tan(slope))
    for side in (-1, 1):
        # Cada faldón es una losa inclinada que baja de la cumbrera al alero.
        mid_z = cz + side * half / 2
        mid_y = by + height - (height + overhang * math.tan(slope)) / 2
        box((cx, mid_y, mid_z), (x_len + overhang * 2, thick, length), material, rot_x=side * -math.degrees(slope))
    box((cx, by + height + 0.05, cz), (x_len + overhang * 2, 0.16, 0.24), "Cornisa")
    # Hastiales (los triángulos de los extremos) en piedra.
    for s in (-1, 1):
        pts = [(-depth / 2, 0.0), (depth / 2, 0.0), (0.0, height)]
        bm = bmesh.new()
        x = cx + s * (x_len / 2 - 0.1)
        a = [bm.verts.new(U(x - 0.1, by + py, cz + px)) for px, py in pts]
        b = [bm.verts.new(U(x + 0.1, by + py, cz + px)) for px, py in pts]
        bm.faces.new(a)
        bm.faces.new(list(reversed(b)))
        for i in range(3):
            j = (i + 1) % 3
            bm.faces.new((a[i], a[j], b[j], b[i]))
        add(bm, "Piedra")


# ---------- Mansión ----------

FRONT_MAIN, FRONT_WING, FRONT_TOWER = 15.0, 16.5, 13.4
KIND_MAT = {0: "FrostA", 1: "FrostB", 2: "FrostC", 3: "Rota", 4: "Vela"}


def quoins(x, z_front, z_back, y0, y1, s):
    """Esquineras: sillares grandes alternos en la arista vertical del muro."""
    y = y0
    i = 0
    while y < y1 - 0.2:
        h = 0.55
        long = 0.9 if i % 2 == 0 else 0.55
        box((x + s * 0.02, y + h / 2, z_front + long / 2 - 0.02), (0.14, h - 0.04, long), "Cornisa")
        y += h
        i += 1


def corbels(x0, x1, y, z, step=0.9):
    x = x0
    while x <= x1:
        box((x, y, z - 0.12), (0.22, 0.3, 0.28), "Cornisa")
        x += step


def body(cx, cz, w, h, d, base_y=1.0):
    box((cx, base_y / 2, cz), (w + 0.6, base_y, d + 0.6), "PiedraOscura")  # zócalo
    box((cx, base_y + h / 2, cz), (w, h, d), "Piedra")
    zf = cz - d / 2
    for s in (-1, 1):
        quoins(cx + s * (w / 2 + 0.05), zf, cz + d / 2, base_y, base_y + h, s)


def window(c, w, h, kind, z):
    rise = w * 0.62
    outer = arch(w + 0.36, h + 0.18, rise + 0.22)
    inner = arch(w, h, rise)
    # Marco de piedra que sobresale del muro y vidrio hundido dentro del marco, por delante de la cara del muro
    # (z es la cara del muro; hacia la cámara es z menor).
    ring_from_outlines(outer, inner, z - 0.2, z + 0.02, "Cornisa", (c[0], c[1]))
    prism_from_outline(inner, z - 0.06, z - 0.02, KIND_MAT[kind], (c[0], c[1]))
    # Parteluz y travesaño de hierro, delante del vidrio.
    box((c[0], c[1] + 0.12, z - 0.09), (0.06, h + rise * 0.8, 0.05), "Hierro")
    box((c[0], c[1] + h * 0.18, z - 0.09), (w, 0.06, 0.05), "Hierro")
    # Alféizar con nieve.
    box((c[0], c[1] - h / 2 - 0.14, z - 0.12), (w + 0.6, 0.16, 0.36), "Cornisa")
    box((c[0], c[1] - h / 2 - 0.04, z - 0.14), (w + 0.45, 0.06, 0.28), "Nieve")
    if kind == 3:
        # Ventana rota: esquirlas de hielo pegadas al vidrio oscuro.
        for dx, dy, rz in ((-0.2, -0.3, 25), (0.18, 0.05, -30), (0.05, -0.55, 10)):
            box((c[0] + dx * w, c[1] + dy, z - 0.08), (0.16, 0.4, 0.02), "FrostA", rot_z=rz)


WINDOWS = [
    ((-3.6, 3.4), 1.1, 1.9, 3, FRONT_MAIN), ((3.6, 3.4), 1.1, 1.9, 0, FRONT_MAIN),
    ((-3.4, 6.9), 1.1, 1.9, 1, FRONT_MAIN), ((0.0, 6.9), 1.1, 1.9, 2, FRONT_MAIN), ((3.4, 6.9), 1.1, 1.9, 0, FRONT_MAIN),
    ((-9.6, 3.0), 1.0, 1.6, 2, FRONT_WING), ((-9.6, 5.4), 1.0, 1.6, 3, FRONT_WING),
    ((6.8, 3.0), 1.0, 1.6, 1, FRONT_WING), ((9.4, 3.0), 1.0, 1.6, 3, FRONT_WING),
    ((6.8, 5.4), 1.0, 1.6, 0, FRONT_WING), ((9.4, 5.4), 1.0, 1.6, 4, FRONT_WING),
    ((-5.6, 8.2), 0.8, 1.5, 1, FRONT_TOWER),
]


def build():
    # Cuerpo central (10 × 8 × 8) y alas (6 × 6 × 5.5), retrasadas.
    body(0.0, 19.0, 10.0, 8.0, 8.0)
    box((0.0, 5.2, 19.0), (10.2, 0.18, 8.2), "Cornisa")  # imposta entre plantas
    box((0.0, 9.05, 19.0), (10.5, 0.3, 8.5), "Cornisa")  # cornisa superior
    corbels(-4.8, 4.8, 8.75, FRONT_MAIN)
    for s in (-1, 1):
        x = s * 8.0
        body(x, 19.25, 6.0, 6.0, 5.5)
        box((x, 7.05, 19.25), (6.4, 0.25, 5.9), "Cornisa")
        corbels(x - 2.8, x + 2.8, 6.8, FRONT_WING)
        gable_roof((x, 7.2, 19.25), 6.4, 5.9, 3.0, "Pizarra")
        buttress(s * 11.0, 16.5, 6.6)
    for s in (-1, 1):
        buttress(s * 5.0, 15.0, 8.8)

    # Tejado del cuerpo central, hundido entre x=2 y x=4.2: dos tramos y las vigas al aire.
    gable_roof((-1.65, 9.2, 19.0), 7.3, 8.6, 4.2, "Pizarra", overhang=0.3)
    gable_roof((4.75, 9.2, 19.0), 1.1, 8.6, 4.2, "Pizarra", overhang=0.3)
    slope = math.degrees(math.atan2(4.2, 4.3))
    for x in (2.4, 3.1, 3.8):
        box((x, 11.3, 17.1), (0.14, 0.14, 5.0), "Madera", rot_x=-slope)
        box((x, 11.3, 20.9), (0.14, 0.14, 5.0), "Madera", rot_x=slope)
    box((3.1, 13.35, 19.0), (2.2, 0.16, 0.16), "Madera")

    # Hastial gótico sobre la puerta con óculo.
    pts = [(-1.6, 0.0), (1.6, 0.0), (0.0, 3.4)]
    prism_from_outline(pts, 14.75, 15.1, "Piedra", (0.0, 9.2))
    gable_roof((0.0, 9.2, 16.1), 2.6, 3.2, 3.4, "Pizarra", overhang=0.2)
    ring_from_outlines([(0.42 * math.cos(a), 0.42 * math.sin(a)) for a in [2 * math.pi * i / 16 for i in range(16)]],
                       [(0.3 * math.cos(a), 0.3 * math.sin(a)) for a in [2 * math.pi * i / 16 for i in range(16)]],
                       14.6, 14.76, "Cornisa", (0.0, 10.4))
    prism_from_outline([(0.3 * math.cos(a), 0.3 * math.sin(a)) for a in [2 * math.pi * i / 16 for i in range(16)]], 14.68, 14.72, "Rota", (0.0, 10.4))

    # Torre de la esquina con chapitel roto (la punta cayó sobre el tejado).
    box((-5.6, 0.55, 15.0), (3.6, 1.1, 3.6), "PiedraOscura")
    box((-5.6, 5.5, 15.0), (3.2, 11.0, 3.2), "Piedra")
    box((-5.6, 6.5, 15.0), (3.35, 0.2, 3.35), "Cornisa")
    box((-5.6, 11.2, 15.0), (3.7, 0.4, 3.7), "Cornisa")
    for dx in (-1.5, -0.5, 0.5, 1.5):  # almenas
        for dz in (-1.75, 1.75):
            box((-5.6 + dx, 11.65, 15.0 + dz), (0.5, 0.5, 0.25), "Cornisa")
    cone((-5.6, 11.4, 15.0), 3.9, 3.6, "Pizarra", tilt_x=6.0)
    cone((-3.4, 12.25, 17.6), 0.9, 1.5, "Pizarra", tilt_x=-30.0, tilt_z=95.0)

    # Chimeneas con remate y cañones.
    for x, y, z, h in ((-3.8, 12.3, 20.5, 4.2), (4.75, 12.0, 21.5, 3.6)):
        box((x, y, z), (0.85, h, 0.85), "PiedraOscura")
        box((x, y + h / 2 + 0.1, z), (1.05, 0.2, 1.05), "Cornisa")
        for dx in (-0.2, 0.2):
            cylinder((x + dx, y + h / 2 + 0.45, z), 0.22, 0.5, "PiedraOscura")

    # Porche: escalinata, columnas (la derecha partida), dintel, tejadillo y puerta de arco apuntado.
    for i in range(4):
        depth = 2.0 - i * 0.5
        box((0.0, 0.125 + i * 0.25, 12.6 - depth / 2), (4.4 - i * 0.1, 0.25, depth), "PiedraOscura")
    box((0.0, 0.5, 13.8), (4.8, 1.0, 2.4), "PiedraOscura")
    column(-2.1, 12.9, 3.0, broken=False)
    column(2.1, 12.9, 2.2, broken=True)
    box((0.0, 4.05, 12.9), (5.0, 0.3, 0.32), "Madera")
    gable_roof((0.0, 4.2, 13.75), 5.0, 2.8, 1.3, "Pizarra", overhang=0.2)
    door = arch(1.7, 2.6, 1.0)
    ring_from_outlines(arch(2.2, 2.8, 1.3), door, 14.74, 15.0, "Cornisa", (0.0, 2.3))
    prism_from_outline(door, 14.88, 14.96, "Madera", (0.0, 2.3))
    for i in range(-3, 4):  # juntas entre tablones de la puerta
        box((i * 0.24, 2.4, 14.865), (0.03, 2.9, 0.03), "PiedraOscura")
    for y in (1.6, 3.0):  # bisagras
        box((-0.45, y, 14.85), (0.7, 0.08, 0.03), "Hierro")

    for (c, w, h, kind, z) in WINDOWS:
        window(c, w, h, kind, z)


def buttress(x, z_front, h):
    box((x, h / 2, z_front), (0.7, h, 0.7), "PiedraOscura")
    box((x, 1.0, z_front - 0.3), (0.85, 2.0, 1.0), "PiedraOscura", rot_x=-12)
    box((x, h * 0.55, z_front - 0.15), (0.78, 0.25, 0.85), "Cornisa", rot_x=-20)
    box((x, h + 0.1, z_front), (0.85, 0.2, 0.85), "Cornisa")


def column(x, z, h, broken):
    cylinder((x, 1.0 + 0.12, z), 0.6, 0.24, "Cornisa", sides=8)  # basa
    cylinder((x, 1.0 + h / 2, z), 0.36, h, "Cornisa", sides=12)
    if not broken:
        cylinder((x, 1.0 + h + 0.1, z), 0.62, 0.2, "Cornisa", sides=8)  # capitel
    else:
        # El tambor que cayó a la nieve.
        bm_len = 0.8
        box((x + 0.8, 0.2, z - 1.4), (0.36, 0.36, bm_len), "Cornisa", rot_y=35)


build()
finish()
bpy.ops.export_scene.fbx(
    filepath=OUT,
    use_selection=False,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    mesh_smooth_type="FACE",
    use_mesh_modifiers=True,
    add_leaf_bones=False,
)
print("[Mansion] exportada a", OUT, "materiales:", sorted(MATS))
