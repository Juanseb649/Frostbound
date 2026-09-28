"""
Frostbound — armas.

Genera los modelos low-poly de las armas (estilo Club Penguin: formas simples, colores planos) y
extrae las armas del equipo inicial del paquete Frostbound_Gear. Cada arma se exporta como FBX
independiente con el ORIGEN EN LA EMPUÑADURA y la punta/hoja hacia +Z (en Unity: +Y), para que
el pingüino la sujete con la aleta (Anchor_Hand_R / Anchor_Hand_L).

Uso: blender -b -P frostbound_weapons.py -- "<carpeta Gear/Models>" "<salida Assets/Models/Weapons>"
"""

import math
import os
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

GEAR_DIR = r"C:\Users\Sebastian\My project (2)\Blender\Gear_Source\Models"
OUT_DIR = r"C:\Users\Sebastian\My project (2)\Assets\Models\Weapons"
if "--" in sys.argv:
    a = sys.argv[sys.argv.index("--") + 1:]
    GEAR_DIR, OUT_DIR = (a + [GEAR_DIR, OUT_DIR][len(a):])[:2]

COLORS = {
    "W_Steel": (0.74, 0.78, 0.84),
    "W_DarkIron": (0.26, 0.28, 0.33),
    "W_Wood": (0.56, 0.37, 0.21),
    "W_DarkWood": (0.34, 0.21, 0.12),
    "W_Leather": (0.38, 0.22, 0.13),
    "W_Gold": (0.95, 0.74, 0.28),
    "W_Bronze": (0.72, 0.47, 0.22),
    "W_Ice": (0.55, 0.86, 1.00),
    "W_Red": (0.74, 0.18, 0.18),
    "W_Blue": (0.20, 0.38, 0.78),
    "W_String": (0.92, 0.90, 0.82),
    "W_Black": (0.10, 0.10, 0.12),
    "W_Feather": (0.95, 0.95, 0.95),
}


def log(msg):
    print("[Frostbound] " + msg)


def mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        c = COLORS.get(name, (0.8, 0.8, 0.8))
        m.diffuse_color = (c[0], c[1], c[2], 1.0)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (c[0], c[1], c[2], 1.0)
    return m


# ---------- Primitivas ----------

class Builder:
    def __init__(self, name):
        self.name = name
        self.parts = []

    def _obj(self, bm, material):
        me = bpy.data.meshes.new(self.name + "_part")
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(self.name + "_part", me)
        bpy.context.scene.collection.objects.link(ob)
        me.materials.append(mat(material))
        self.parts.append(ob)
        return ob

    def box(self, center, size, material, rot=(0, 0, 0)):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        m = Matrix.Translation(Vector(center)) @ euler(rot) @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
        return self._obj(bm, material)

    def cyl(self, a, b, r1, material, r2=None, segs=8):
        """Cilindro (o cono) entre los puntos a y b."""
        a, b = Vector(a), Vector(b)
        r2 = r1 if r2 is None else r2
        d = b - a
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=d.length)
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        bmesh.ops.transform(bm, matrix=Matrix.Translation((a + b) * 0.5) @ rot, verts=bm.verts)
        return self._obj(bm, material)

    def sphere(self, center, radius, material, scale=(1, 1, 1), segs=8):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=max(4, segs // 2), radius=radius)
        m = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0))
        bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
        return self._obj(bm, material)

    def loft(self, sections, material):
        """Hoja con sección de rombo. sections: [(x, z, ang, medioAncho, medioGrosor)], ang = dirección de la hoja en XZ (rad desde +Z)."""
        bm = bmesh.new()
        rings = []
        for (x, z, ang, hw, ht) in sections:
            t = Vector((math.sin(ang), 0, math.cos(ang)))
            n = Vector((t.z, 0, -t.x))
            p = Vector((x, 0, z))
            ring = [bm.verts.new(p - n * hw), bm.verts.new(p + Vector((0, ht, 0))),
                    bm.verts.new(p + n * hw), bm.verts.new(p - Vector((0, ht, 0)))]
            rings.append(ring)
        for r0, r1 in zip(rings, rings[1:]):
            for i in range(4):
                j = (i + 1) % 4
                bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return self._obj(bm, material)

    def ring(self, center, radius, thick, material, axis="Y", segs=12):
        # Anillo hecho de cilindros cortos.
        c = Vector(center)
        pts = []
        for i in range(segs):
            a = i / segs * math.tau
            if axis == "Y":
                pts.append(c + Vector((math.cos(a) * radius, 0, math.sin(a) * radius)))
            else:
                pts.append(c + Vector((math.cos(a) * radius, math.sin(a) * radius, 0)))
        for i in range(segs):
            self.cyl(pts[i], pts[(i + 1) % segs], thick, material, segs=6)

    def finish(self, scale=1.0):
        bpy.ops.object.select_all(action="DESELECT")
        for p in self.parts:
            p.select_set(True)
        bpy.context.view_layer.objects.active = self.parts[0]
        bpy.ops.object.join()
        ob = bpy.context.view_layer.objects.active
        ob.name = ob.data.name = self.name
        if scale != 1.0:
            ob.data.transform(Matrix.Diagonal((scale, scale, scale, 1.0)))
        for poly in ob.data.polygons:
            poly.use_smooth = False
        # Mismo material repetido → una sola ranura por material.
        bpy.ops.object.mode_set(mode="OBJECT")
        merge_material_slots(ob)
        return ob


def euler(rot):
    return Matrix.Rotation(rot[2], 4, "Z") @ Matrix.Rotation(rot[1], 4, "Y") @ Matrix.Rotation(rot[0], 4, "X")


def merge_material_slots(ob):
    names = [s.material.name if s.material else "" for s in ob.material_slots]
    uniq = []
    for n in names:
        if n not in uniq:
            uniq.append(n)
    remap = [uniq.index(n) for n in names]
    indices = [remap[poly.material_index] for poly in ob.data.polygons]
    ob.data.materials.clear()
    for n in uniq:
        ob.data.materials.append(bpy.data.materials[n])
    for poly, i in zip(ob.data.polygons, indices):
        poly.material_index = i


def straight_blade(b, z0, length, width, thick, material="W_Steel", tip=0.18, taper=0.85, leaf=0.0, ridge=True):
    secs = []
    steps = 6
    for i in range(steps + 1):
        t = i / steps
        z = z0 + length * (1 - tip) * t
        w = width * (1 - (1 - taper) * t) * (1 + leaf * math.sin(t * math.pi))
        secs.append((0, z, 0, w, thick))
    secs.append((0, z0 + length, 0, 0.002, 0.002))
    return b.loft(secs, material)


def curved_blade(b, z0, length, width, thick, curve, material="W_Steel", tip=0.2, back_straight=False, steps=10, widen=0.0):
    secs = []
    for i in range(steps + 1):
        t = i / steps
        x = curve * t * t
        z = z0 + length * t
        ang = math.atan2(2 * curve * t, length)
        w = width * (1 + widen * t) * (1 if t < 1 - tip else max(0.05, (1 - t) / tip))
        xo = x + (w * 0.5 if back_straight else 0)
        secs.append((xo, z, ang, w, thick))
    return b.loft(secs, material)


def grip(b, z0, z1, r=0.022, material="W_Leather", bands=3):
    b.cyl((0, 0, z0), (0, 0, z1), r, material)
    for i in range(bands):
        z = z0 + (z1 - z0) * (i + 0.5) / bands
        b.cyl((0, 0, z - 0.006), (0, 0, z + 0.006), r * 1.25, "W_DarkWood" if material != "W_DarkWood" else "W_Leather")


# ---------- Armas nuevas ----------

def w_claymore():
    b = Builder("W_claymore")
    grip(b, -0.1, 0.24, 0.026)
    b.sphere((0, 0, -0.13), 0.045, "W_Gold")
    b.box((0, 0, 0.26), (0.42, 0.06, 0.05), "W_DarkIron")
    for s in (-1, 1):
        b.cyl((s * 0.21, 0, 0.26), (s * 0.25, 0, 0.3), 0.03, "W_DarkIron")
    straight_blade(b, 0.28, 1.0, 0.07, 0.016, taper=0.8, tip=0.12)
    b.box((0, 0, 0.36), (0.1, 0.035, 0.12), "W_Gold")
    return b


def w_longaxe():
    b = Builder("W_longaxe")
    b.cyl((0, 0, -0.35), (0, 0, 1.15), 0.028, "W_Wood")
    b.cyl((0, 0, -0.38), (0, 0, -0.33), 0.035, "W_DarkIron")
    grip(b, -0.3, 0.05, 0.031, bands=2)
    secs = []
    for i in range(9):
        t = i / 8
        bulge = math.sin(t * math.pi)
        secs.append((0.1 + 0.07 * bulge, 0.7 + 0.5 * t, 0.0, 0.05 + 0.07 * bulge, 0.012))
    b.loft(secs, "W_Steel")
    b.box((0.05, 0, 0.95), (0.1, 0.05, 0.34), "W_DarkIron")
    b.cyl((0, 0, 1.15), (0, 0, 1.28), 0.03, "W_Steel", r2=0.002)
    return b


def w_spear():
    b = Builder("W_spear")
    b.cyl((0, 0, -0.55), (0, 0, 1.05), 0.024, "W_Wood")
    b.cyl((0, 0, -0.6), (0, 0, -0.55), 0.03, "W_DarkIron", r2=0.024)
    grip(b, -0.12, 0.18, 0.027, bands=2)
    b.cyl((0, 0, 1.0), (0, 0, 1.1), 0.034, "W_DarkIron")
    straight_blade(b, 1.08, 0.34, 0.06, 0.014, leaf=0.6, tip=0.35, taper=0.6)
    b.box((0.05, 0.0, 1.02), (0.09, 0.008, 0.1), "W_Red", rot=(0, 0.4, 0))
    b.box((-0.05, 0.0, 1.02), (0.09, 0.008, 0.1), "W_Red", rot=(0, -0.4, 0))
    return b


def w_maul():
    b = Builder("W_maul")
    b.cyl((0, 0, -0.3), (0, 0, 0.85), 0.03, "W_DarkWood")
    grip(b, -0.28, 0.1, 0.034, bands=3)
    b.cyl((-0.13, 0, 0.88), (0.13, 0, 0.88), 0.11, "W_DarkIron", segs=8)
    for s in (-1, 1):
        b.cyl((s * 0.13, 0, 0.88), (s * 0.17, 0, 0.88), 0.085, "W_Steel", segs=8)
    for z in (0.8, 0.96):
        for s in (-1, 1):
            b.cyl((0, s * 0.1, z), (0, s * 0.15, z), 0.022, "W_Steel", r2=0.002, segs=5)
    b.cyl((0, 0, 0.99), (0, 0, 1.06), 0.03, "W_Steel", r2=0.002, segs=6)
    return b


def w_knife():
    b = Builder("W_knife")
    b.cyl((0, 0, -0.05), (0, 0, 0.1), 0.02, "W_DarkWood")
    b.box((0, 0, 0.105), (0.06, 0.03, 0.02), "W_DarkIron")
    curved_blade(b, 0.11, 0.2, 0.03, 0.007, -0.025, back_straight=True, tip=0.35)
    return b


def w_sword():
    b = Builder("W_sword")
    grip(b, -0.04, 0.13, 0.022)
    b.sphere((0, 0, -0.06), 0.032, "W_Bronze")
    b.box((0, 0, 0.14), (0.24, 0.045, 0.04), "W_Bronze")
    straight_blade(b, 0.15, 0.68, 0.05, 0.012, taper=0.85, tip=0.14)
    return b


def w_scimitar():
    b = Builder("W_scimitar")
    grip(b, -0.04, 0.13, 0.022, material="W_Red")
    b.sphere((0, 0, -0.06), 0.03, "W_Gold")
    b.box((0, 0, 0.14), (0.2, 0.04, 0.035), "W_Gold")
    curved_blade(b, 0.15, 0.66, 0.055, 0.011, 0.2, tip=0.25, widen=0.5)
    return b


def w_hammer():
    b = Builder("W_hammer")
    b.cyl((0, 0, -0.08), (0, 0, 0.5), 0.022, "W_Wood")
    grip(b, -0.06, 0.12, 0.025, bands=2)
    b.box((0.02, 0, 0.52), (0.26, 0.1, 0.1), "W_DarkIron")
    b.box((0.15, 0, 0.52), (0.04, 0.12, 0.12), "W_Steel")
    b.cyl((-0.1, 0, 0.52), (-0.19, 0, 0.56), 0.035, "W_DarkIron", r2=0.006, segs=6)
    return b


def w_nunchaku():
    b = Builder("W_nunchaku")
    b.cyl((0, 0, -0.05), (0, 0, 0.26), 0.022, "W_Black")
    b.cyl((0, 0, 0.0), (0, 0, 0.02), 0.025, "W_Gold")
    b.cyl((0, 0, 0.2), (0, 0, 0.22), 0.025, "W_Gold")
    pts = [Vector((0, 0, 0.27)), Vector((0.03, 0, 0.3)), Vector((0.07, 0, 0.31)), Vector((0.1, 0, 0.29))]
    for p0, p1 in zip(pts, pts[1:]):
        b.cyl(p0, p1, 0.006, "W_Steel", segs=5)
    b.cyl((0.1, 0, 0.29), (0.14, 0, -0.01), 0.022, "W_Black")
    b.cyl((0.135, 0, 0.03), (0.138, 0, 0.01), 0.025, "W_Gold")
    return b


def kunai_parts(b, offset=Vector((0, 0, 0)), rot=0.0, scale=1.0):
    parts = Builder(b.name + "_k")
    parts.cyl((0, 0, -0.06), (0, 0, 0.07), 0.013, "W_Black")
    for i in range(3):
        z = -0.04 + i * 0.045
        parts.cyl((0, 0, z), (0, 0, z + 0.01), 0.016, "W_Red")
    parts.ring((0, 0, -0.09), 0.025, 0.005, "W_DarkIron", axis="Y", segs=8)
    straight_blade(parts, 0.07, 0.17, 0.035, 0.008, material="W_DarkIron", leaf=0.5, tip=0.45, taper=0.5)
    m = Matrix.Translation(offset) @ Matrix.Rotation(rot, 4, "Y") @ Matrix.Diagonal((scale, scale, scale, 1))
    for p in parts.parts:
        p.data.transform(m)
        b.parts.append(p)


def w_kunai():
    b = Builder("W_kunai")
    kunai_parts(b)
    return b


def w_katana():
    b = Builder("W_katana")
    grip(b, -0.12, 0.13, 0.021, material="W_Black", bands=5)
    b.cyl((0, 0, -0.14), (0, 0, -0.12), 0.024, "W_Gold")
    b.cyl((0, 0, 0.135), (0, 0, 0.15), 0.06, "W_Gold", segs=10)
    curved_blade(b, 0.15, 0.72, 0.03, 0.01, 0.07, tip=0.12, back_straight=True)
    return b


def w_celtic():
    b = Builder("W_celtic")
    grip(b, -0.03, 0.13, 0.022, material="W_Leather")
    b.ring((0, 0, -0.08), 0.045, 0.01, "W_Bronze", axis="Y", segs=10)
    b.box((0, 0, 0.14), (0.14, 0.04, 0.03), "W_Bronze")
    straight_blade(b, 0.15, 0.62, 0.042, 0.012, leaf=0.45, tip=0.2, taper=0.75)
    return b


def w_sickle():
    b = Builder("W_sickle")
    grip(b, -0.05, 0.2, 0.022, material="W_Wood", bands=2)
    b.cyl((0, 0, 0.19), (0, 0, 0.23), 0.026, "W_DarkIron")
    secs = []
    for i in range(12):
        t = i / 11
        ang = -0.2 + t * 2.6
        r = 0.2
        cx, cz = 0.2 - r * math.cos(ang), 0.23 + r * math.sin(ang) * 1.1
        w = 0.04 * (1 - t) + 0.004
        secs.append((cx, cz, math.pi / 2 - ang, w, 0.008))
    b.loft(secs, "W_Steel")
    b.cyl((0.0, 0, 0.23), (0.02, 0, 0.26), 0.012, "W_Ice", segs=5)
    return b


def w_machete():
    b = Builder("W_machete")
    grip(b, -0.03, 0.13, 0.023, material="W_DarkWood", bands=2)
    b.box((0, 0, 0.14), (0.08, 0.04, 0.02), "W_DarkIron")
    curved_blade(b, 0.15, 0.5, 0.05, 0.009, -0.05, tip=0.15, widen=0.7, back_straight=True)
    return b


def bow_limbs(b, half, depth, thick, material, recurve=0.0):
    for s in (1, -1):
        secs = []
        for i in range(9):
            t = i / 8
            z = s * (0.07 + (half - 0.07) * t)
            y = -depth * t * t + recurve * max(0, t - 0.8) * 5
            secs.append((y, z, 0, thick * (1 - 0.4 * t), thick * 0.7))
        # La curva va en el eje Y: construimos en XZ y rotamos.
        ob = b.loft([(p[0], p[1], p[2], p[3], p[4]) for p in secs], material)
        ob.data.transform(Matrix.Rotation(math.pi / 2, 4, "Z"))
    tip_y = -depth + recurve
    b.cyl((0, tip_y, half), (0, tip_y, -half), 0.004, "W_String", segs=4)


def w_bow():
    b = Builder("W_bow")
    b.cyl((0, 0, -0.08), (0, 0, 0.08), 0.024, "W_Leather")
    bow_limbs(b, 0.45, 0.12, 0.022, "W_Wood")
    return b


def w_longbow():
    b = Builder("W_longbow")
    b.cyl((0, 0, -0.09), (0, 0, 0.09), 0.025, "W_Leather")
    bow_limbs(b, 0.78, 0.14, 0.024, "W_DarkWood")
    b.cyl((0, 0, 0.1), (0, 0, 0.13), 0.028, "W_Gold")
    b.cyl((0, 0, -0.13), (0, 0, -0.1), 0.028, "W_Gold")
    return b


def w_crossbow():
    b = Builder("W_crossbow")
    # Culata a lo largo de +Z (hacia adelante al apuntar); empuñadura en el origen.
    b.box((0, 0, 0.12), (0.05, 0.06, 0.5), "W_Wood")
    b.box((0, -0.04, -0.02), (0.04, 0.08, 0.06), "W_DarkWood")
    b.box((0, 0.035, 0.2), (0.012, 0.01, 0.34), "W_DarkIron")
    for s in (-1, 1):
        secs = []
        for i in range(6):
            t = i / 5
            secs.append((s * (0.03 + 0.27 * t), 0.35 - 0.08 * t * t, math.pi / 2 * s, 0.012, 0.01))
        b.loft(secs, "W_DarkIron")
    b.cyl((-0.3, 0.0, 0.27), (0.3, 0.0, 0.27), 0.004, "W_String", segs=4)
    b.cyl((0, 0.05, 0.12), (0, 0.05, 0.36), 0.006, "W_Wood", segs=5)
    b.cyl((0, 0.05, 0.36), (0, 0.05, 0.4), 0.012, "W_Steel", r2=0.001, segs=5)
    return b


def w_throwing_kunai():
    b = Builder("W_throwing_kunai")
    kunai_parts(b, Vector((0, 0, 0)), 0.0, 1.0)
    kunai_parts(b, Vector((0.035, 0.01, -0.01)), 0.35, 0.9)
    kunai_parts(b, Vector((-0.035, -0.01, -0.01)), -0.35, 0.9)
    return b


def w_arrow():
    b = Builder("W_arrow")
    b.cyl((0, 0, -0.35), (0, 0, 0.3), 0.006, "W_Wood", segs=5)
    b.cyl((0, 0, 0.3), (0, 0, 0.38), 0.016, "W_Steel", r2=0.001, segs=4)
    for i in range(3):
        a = i / 3 * math.tau
        b.box((math.cos(a) * 0.014, math.sin(a) * 0.014, -0.3), (0.028, 0.003, 0.08), "W_Feather", rot=(0, 0, a))
    return b


def w_bolt():
    b = Builder("W_bolt")
    b.cyl((0, 0, -0.16), (0, 0, 0.14), 0.008, "W_DarkWood", segs=5)
    b.cyl((0, 0, 0.14), (0, 0, 0.2), 0.016, "W_DarkIron", r2=0.001, segs=4)
    for i in range(2):
        a = i / 2 * math.tau
        b.box((math.cos(a) * 0.012, math.sin(a) * 0.012, -0.13), (0.022, 0.003, 0.05), "W_Red", rot=(0, 0, a))
    return b


SCALES = {"w_claymore": 0.85, "w_longaxe": 0.85, "w_spear": 0.8, "w_maul": 0.9, "w_longbow": 0.88}

NEW_WEAPONS = [w_claymore, w_longaxe, w_spear, w_maul, w_knife, w_sword, w_scimitar, w_hammer, w_nunchaku,
               w_kunai, w_katana, w_celtic, w_sickle, w_machete, w_bow, w_longbow, w_crossbow, w_throwing_kunai,
               w_arrow, w_bolt]

# ---------- Armas del equipo inicial (se extraen del paquete) ----------

# (obj, pieza, nombre, fracción de la altura donde está la empuñadura, invertir (punta hacia abajo en el original))
STARTER = [
    ("Frostbound_Knight_Gear.obj", "Weapon_R", "W_knight_sword", 0.1, False),
    ("Frostbound_Mage_Gear.obj", "Weapon_R", "W_mage_staff", 0.42, False),
    ("Frostbound_Ninja_Gear.obj", "Weapon_R", "W_ninja_kunai", 0.82, True),
    ("Frostbound_Ninja_Gear.obj", "Weapon_L", "W_ninja_shuriken", 0.5, False),
    ("Frostbound_Viking_Gear.obj", "Weapon_R", "W_viking_axe", 0.28, False),
    ("Frostbound_Viking_Gear.obj", "Offhand_L", "W_viking_shield", 0.5, False),
]


def extract_starter(obj_file, piece, name, grip_frac, flip):
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=os.path.join(GEAR_DIR, obj_file))
    new = [o for o in bpy.data.objects if o not in before]
    target = None
    for o in new:
        if o.name.split(".")[0] == piece and target is None:
            target = o
        else:
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    pts = [v.co.copy() for v in target.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) * 0.5
    gz = lo.z + (hi.z - lo.z) * grip_frac
    m = Matrix.Translation(Vector((-center.x, -center.y, -gz)))
    if flip:
        m = Matrix.Rotation(math.pi, 4, "Y") @ m
    target.data.transform(m)
    target.name = target.data.name = name
    for s in target.material_slots:
        if s.material:
            wanted = "W_" + s.material.name.split(".")[0]
            existing = bpy.data.materials.get(wanted)
            if existing is not None and existing != s.material:
                s.material = existing
            else:
                s.material.name = wanted
    return target


def export(ob):
    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    path = os.path.join(OUT_DIR, ob.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False, path_mode="STRIP")
    pts = [v.co for v in ob.data.vertices]
    log("exportado %s  (alto %.2f m, %d vértices, materiales %s)" % (
        path, max(p.z for p in pts) - min(p.z for p in pts), len(pts), [m.name for m in ob.data.materials]))


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for fn in NEW_WEAPONS:
        export(fn().finish(SCALES.get(fn.__name__, 1.0)))
    for spec in STARTER:
        path = os.path.join(GEAR_DIR, spec[0])
        if os.path.exists(path):
            export(extract_starter(*spec))
        else:
            log("no existe " + path)


if __name__ == "__main__":
    main()
