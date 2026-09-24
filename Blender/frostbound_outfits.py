"""
Frostbound — prendas originales para el pingüino (Blender 4.2+ / 5.x)

Necesita Penguin_Rigged.blend (lo genera frostbound_penguin_rig.py). Crea cuatro prendas,
cada una pesada al mismo esqueleto, y las exporta como FBX independientes:

  Outfit_HornedHelmet.fbx  casco de acero con banda de bronce, remaches y dos cuernos curvos
  Outfit_WizardHat.fbx     sombrero puntiagudo doblado hacia atrás, ala ancha, banda y estrella
  Outfit_Armor.fbx         peto ajustado al torso, hombreras, cinturón con hebilla y emblema de cristal
  Outfit_HoodedSuit.fbx    capucha con punta, capa que se abre hacia abajo y bufanda

Los colores van en el atributo de color "Color" (el shader toon de Unity los usa como albedo).
Uso: blender -b -P frostbound_outfits.py -- "carpeta/Penguin_Rigged.blend" "carpeta_salida"
"""

import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector, Matrix

RIG_BLEND = r"C:\Users\Sebastian\My project (2)\Blender\Penguin_Rigged.blend"
OUT_DIR = r"C:\Users\Sebastian\My project (2)\Assets\Models\Outfits"
if "--" in sys.argv:
    extra = sys.argv[sys.argv.index("--") + 1:]
    if len(extra) >= 1:
        RIG_BLEND = extra[0]
    if len(extra) >= 2:
        OUT_DIR = extra[1]

STEEL = (0.62, 0.68, 0.76)
STEEL_DARK = (0.38, 0.42, 0.50)
BRONZE = (0.80, 0.55, 0.22)
IVORY = (0.93, 0.88, 0.74)
INDIGO = (0.22, 0.14, 0.48)
GOLD = (0.98, 0.78, 0.25)
LEATHER = (0.40, 0.24, 0.13)
ICE = (0.45, 0.85, 1.00)
MOSS = (0.20, 0.36, 0.26)
MOSS_DARK = (0.13, 0.22, 0.17)
SCARF = (0.72, 0.16, 0.18)


def log(msg):
    print("[Frostbound] " + msg)


def smoothstep(e0, e1, x):
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3 - 2 * t)


# ---------- construcción de mallas ----------

def new_object(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    for p in me.polygons:
        p.use_smooth = True
    return obj


def paint(obj, color):
    for old in [a for a in obj.data.color_attributes if a.name != "Color"]:
        obj.data.color_attributes.remove(old)
    attr = obj.data.color_attributes.get("Color") or obj.data.color_attributes.new("Color", "FLOAT_COLOR", "POINT")
    obj.data.color_attributes.active_color = attr
    fn = color if callable(color) else (lambda co, c=color: c)
    for i, v in enumerate(obj.data.vertices):
        c = fn(v.co)
        attr.data[i].color = (c[0], c[1], c[2], 1.0)


def solidify(obj, thickness, offset=-1.0):
    mod = obj.modifiers.new("Solidify", "SOLIDIFY")
    mod.thickness = thickness
    mod.offset = offset
    mod.use_even_offset = False
    mod.use_quality_normals = True
    mod.use_rim = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def body_shell(name, body, planes, drop_face, offset, displace=None):
    """Copia la superficie del cuerpo, la corta con planos (bordes limpios) y la infla hacia afuera."""
    bm = bmesh.new()
    bm.from_mesh(body.data)
    for co, no in planes:
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=Vector(co), plane_no=Vector(no))
    bm.faces.ensure_lookup_table()
    dead = [f for f in bm.faces if drop_face(f.calc_center_median())]
    bmesh.ops.delete(bm, geom=dead, context="FACES")
    bmesh.ops.dissolve_degenerate(bm, dist=1e-4, edges=bm.edges[:])
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.normal_update()
    for v in bm.verts:
        extra = displace(v.co) if displace else Vector((0, 0, 0))
        v.co = v.co + v.normal * offset + extra
    return new_object(name, bm)


def tube(name, path_fn, radius_fn, rings=18, segments=12, cap_end=True):
    """Tubo a lo largo de una curva, con marco de transporte paralelo (cuernos, sombrero)."""
    bm = bmesh.new()
    pts = [path_fn(i / rings) for i in range(rings + 1)]
    tangents = []
    for i in range(rings + 1):
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, rings)]
        tangents.append((b - a).normalized())
    ref = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.9 else Vector((1, 0, 0))
    normal = tangents[0].cross(ref).normalized()
    loops = []
    for i, p in enumerate(pts):
        t = tangents[i]
        normal = (normal - t * normal.dot(t)).normalized()
        binormal = t.cross(normal)
        r = radius_fn(i / rings)
        ring = []
        for s in range(segments):
            ang = 2 * math.pi * s / segments
            ring.append(bm.verts.new(p + (normal * math.cos(ang) + binormal * math.sin(ang)) * r))
        loops.append(ring)
    for i in range(rings):
        for s in range(segments):
            a, b = loops[i][s], loops[i][(s + 1) % segments]
            c, d = loops[i + 1][(s + 1) % segments], loops[i + 1][s]
            bm.faces.new((a, b, c, d))
    bm.faces.new(list(reversed(loops[0])))
    if cap_end:
        tip = bm.verts.new(pts[-1] + tangents[-1] * radius_fn(1.0))
        for s in range(segments):
            bm.faces.new((loops[-1][s], loops[-1][(s + 1) % segments], tip))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_object(name, bm)


def sphere(name, center, radius, scale=(1, 1, 1), segs=10, rings=6):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=radius)
    for v in bm.verts:
        v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2])) + Vector(center)
    return new_object(name, bm)


def ring_band(name, center, radius_x, radius_y, height, thickness, segments=40):
    bm = bmesh.new()
    rings = []
    for z in (0.0, height):
        for rr in (1.0, 1.0 + thickness / max(radius_x, 1e-4)):
            ring = [bm.verts.new(Vector(center) + Vector((math.cos(a) * radius_x * rr, math.sin(a) * radius_y * rr, z)))
                    for a in [2 * math.pi * i / segments for i in range(segments)]]
            rings.append(ring)
    inner_lo, outer_lo, inner_hi, outer_hi = rings
    for i in range(segments):
        j = (i + 1) % segments
        bm.faces.new((outer_lo[i], outer_lo[j], outer_hi[j], outer_hi[i]))
        bm.faces.new((inner_lo[j], inner_lo[i], inner_hi[i], inner_hi[j]))
        bm.faces.new((outer_hi[i], outer_hi[j], inner_hi[j], inner_hi[i]))
        bm.faces.new((inner_lo[i], inner_lo[j], outer_lo[j], outer_lo[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_object(name, bm)


def star(name, center, outer, inner, depth, facing, tilt_deg=0.0, points=5):
    bm = bmesh.new()
    front, back = [], []
    for i in range(points * 2):
        r = outer if i % 2 == 0 else inner
        a = math.pi / 2 + i * math.pi / points
        front.append(bm.verts.new((math.cos(a) * r, 0.0, math.sin(a) * r)))
        back.append(bm.verts.new((math.cos(a) * r, depth, math.sin(a) * r)))
    cf = bm.verts.new((0, -depth * 0.4, 0))
    n = len(front)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], front[j], cf))
        bm.faces.new((front[j], front[i], back[i], back[j]))
    bm.faces.new(list(reversed(back)))
    rot = Matrix.Rotation(math.radians(tilt_deg), 4, "X")
    if facing == "+Y":
        rot = Matrix.Rotation(math.pi, 4, "Z") @ rot
    for v in bm.verts:
        v.co = rot @ v.co + Vector(center)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_object(name, bm)


def join(name, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    out = bpy.context.view_layer.objects.active
    out.name = out.data.name = name
    return out


# ---------- pesos ----------

def weight_by_height(obj, arm, fixed_bone=None):
    for b in ("Hips", "Spine", "Head"):
        obj.vertex_groups.new(name=b)
    for v in obj.data.vertices:
        if fixed_bone:
            obj.vertex_groups[fixed_bone].add([v.index], 1.0, "REPLACE")
            continue
        z = v.co.z
        head_w = smoothstep(0.62, 0.78, z)
        spine_w = smoothstep(0.30, 0.52, z) * (1.0 - head_w)
        hips_w = max(0.0, 1.0 - head_w - spine_w)
        for b, w in (("Hips", hips_w), ("Spine", spine_w), ("Head", head_w)):
            if w > 0.001:
                obj.vertex_groups[b].add([v.index], w, "REPLACE")
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm


def export(obj, arm, path):
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        use_mesh_modifiers=False, mesh_smooth_type="FACE", colors_type="LINEAR",
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        use_armature_deform_only=False, bake_anim=False)
    log("exportado " + path)


# ---------- prendas ----------

def head_top(body):
    return max(v.co.z for v in body.data.vertices)


def horned_helmet(body):
    top = head_top(body)
    rim = [((0, 0, 0.845), (0, 0.22, 1))]

    def below_rim(c):
        return c.z < 0.845 - 0.22 * c.y

    cap = body_shell("Helmet_Cap", body, rim, below_rim, offset=0.03)
    solidify(cap, 0.02)
    paint(cap, lambda co: STEEL if co.z > 0.9 - 0.22 * co.y else STEEL_DARK)

    band = body_shell("Helmet_Band", body,
                      [((0, 0, 0.845), (0, 0.22, 1)), ((0, 0, 0.9), (0, 0.22, 1))],
                      lambda c: below_rim(c) or c.z > 0.9 - 0.22 * c.y, offset=0.05)
    solidify(band, 0.03)
    paint(band, BRONZE)

    parts = [cap, band]
    for k in range(10):
        a = 2 * math.pi * k / 10
        dirv = Vector((math.cos(a), math.sin(a), 0))
        hit_y = dirv.y * 0.2
        parts.append(sphere("Rivet", (dirv.x * 0.205, dirv.y * 0.19, 0.872 - 0.22 * hit_y), 0.014, segs=8, rings=5))
        paint(parts[-1], STEEL)

    ridge = tube("Helmet_Ridge", lambda t: Vector((0, -0.19 + 0.36 * t, 0.9 + 0.13 * math.sin(math.pi * t) + (top - 0.95) * 0)),
                 lambda t: 0.018, rings=14, segments=8, cap_end=False)
    paint(ridge, BRONZE)
    parts.append(ridge)

    for side in (1, -1):
        p0 = Vector((0.17 * side, 0.02, 0.9))
        p1 = Vector((0.36 * side, 0.03, 0.93))
        p2 = Vector((0.40 * side, -0.06, 1.13))

        def path(t, p0=p0, p1=p1, p2=p2):
            return p0 * (1 - t) ** 2 + p1 * 2 * t * (1 - t) + p2 * t * t

        horn = tube("Horn", path, lambda t: 0.055 * (1 - t) ** 0.9 + 0.004, rings=16, segments=12)
        paint(horn, lambda co, s=side: IVORY if abs(co.x) > 0.23 else BRONZE)
        parts.append(horn)
    return join("Outfit_HornedHelmet", parts), "Head"


def wizard_hat(body):
    base_z, base_r, height = 0.93, 0.18, 0.58

    def center(t):
        return Vector((0.0, 0.015 + 0.26 * t ** 3, base_z + height * t - 0.16 * t ** 4))

    cone = tube("Hat_Cone", center, lambda t: base_r * (1 - t) + 0.006, rings=20, segments=20)
    paint(cone, INDIGO)

    bm = bmesh.new()
    segs = 40
    inner, outer, outer_b, inner_b = [], [], [], []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        wave = 0.012 * math.sin(a * 5)
        c, s = math.cos(a), math.sin(a)
        inner.append(bm.verts.new((c * 0.17, 0.015 + s * 0.17, base_z + 0.01)))
        outer.append(bm.verts.new((c * 0.36, 0.015 + s * 0.36, base_z - 0.035 + wave)))
        outer_b.append(bm.verts.new((c * 0.36, 0.015 + s * 0.36, base_z - 0.05 + wave)))
        inner_b.append(bm.verts.new((c * 0.17, 0.015 + s * 0.17, base_z - 0.005)))
    for i in range(segs):
        j = (i + 1) % segs
        bm.faces.new((inner[i], inner[j], outer[j], outer[i]))
        bm.faces.new((outer_b[i], outer_b[j], inner_b[j], inner_b[i]))
        bm.faces.new((outer[i], outer[j], outer_b[j], outer_b[i]))
        bm.faces.new((inner_b[i], inner_b[j], inner[j], inner[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    brim = new_object("Hat_Brim", bm)
    paint(brim, INDIGO)

    band = ring_band("Hat_Band", (0, 0.015, base_z + 0.005), base_r * 0.99, base_r * 0.99, 0.06, 0.012)
    paint(band, GOLD)

    t = 0.28
    c = center(t)
    r = base_r * (1 - t)
    slope = math.degrees(math.atan2(base_r, height))
    emblem = star("Hat_Star", (c.x, c.y - r - 0.012, c.z), 0.062, 0.027, 0.02, "-Y", tilt_deg=slope)
    paint(emblem, GOLD)
    return join("Outfit_WizardHat", [cone, brim, band, emblem]), "Head"


def armor(body, arm):
    cuirass = body_shell("Armor_Cuirass", body,
                         [((0, 0, 0.2), (0, 0, 1)), ((0, 0, 0.66), (0, 0, 1))],
                         lambda c: c.z < 0.2 or c.z > 0.66, offset=0.03)
    solidify(cuirass, 0.022)

    def cuirass_color(co):
        if co.z < 0.3 or co.z > 0.63:
            return STEEL_DARK
        return STEEL

    paint(cuirass, cuirass_color)

    belt = body_shell("Armor_Belt", body,
                      [((0, 0, 0.19), (0, 0, 1)), ((0, 0, 0.26), (0, 0, 1))],
                      lambda c: c.z < 0.19 or c.z > 0.26, offset=0.055)
    solidify(belt, 0.025)
    paint(belt, LEATHER)

    front_y = min(v.co.y for v in body.data.vertices if abs(v.co.x) < 0.04 and abs(v.co.z - 0.225) < 0.03)
    buckle = ring_band("Armor_Buckle", (0, front_y - 0.07, 0.195), 0.045, 0.012, 0.06, 0.014, segments=12)
    paint(buckle, GOLD)

    chest_y = min(v.co.y for v in body.data.vertices if abs(v.co.x) < 0.04 and abs(v.co.z - 0.46) < 0.03)
    gem = sphere("Armor_Emblem", (0, chest_y - 0.055, 0.46), 0.06, scale=(0.8, 0.35, 1.15), segs=6, rings=4)
    paint(gem, ICE)

    parts = [cuirass, belt, buckle, gem]
    bones = arm.data.bones
    for side in ("L", "R"):
        root = arm.matrix_world @ bones["Flipper_" + side].head_local
        pad = sphere("Armor_Pauldron", (root.x * 1.02, root.y, root.z - 0.005), 0.13, scale=(1.0, 0.95, 0.62), segs=14, rings=8)
        bm = bmesh.new()
        bm.from_mesh(pad.data)
        dead = [f for f in bm.faces if f.calc_center_median().z < root.z - 0.05]
        bmesh.ops.delete(bm, geom=dead, context="FACES")
        bm.to_mesh(pad.data)
        bm.free()
        solidify(pad, 0.02)
        paint(pad, lambda co, rz=root.z: BRONZE if co.z < rz - 0.03 else STEEL)
        parts.append(pad)
    return join("Outfit_Armor", parts), None


def hooded_suit(body):
    hood = body_shell("Hood", body,
                      [((0, 0, 0.64), (0, 0, 1)), ((0, -0.02, 0), (0, 1, 0)), ((0, 0, 0.93), (0, 0, 1))],
                      lambda c: c.z < 0.64 or (c.y < -0.02 and c.z < 0.93), offset=0.035,
                      displace=lambda co: Vector((0, 0.09, 0.05)) * smoothstep(0.85, 1.0, co.z) * smoothstep(-0.05, 0.15, co.y))
    solidify(hood, 0.022)
    paint(hood, lambda co: MOSS_DARK if co.y < -0.0 and co.z < 0.96 else MOSS)

    cape = body_shell("Cape", body,
                      [((0, 0, 0.12), (0, 0, 1)), ((0, 0, 0.68), (0, 0, 1)), ((0, 0.0, 0), (0, 1, 0))],
                      lambda c: c.z < 0.12 or c.z > 0.68 or c.y < 0.0, offset=0.04,
                      displace=lambda co: Vector((co.x * 0.35, 0.09, 0)) * smoothstep(0.62, 0.12, co.z))
    solidify(cape, 0.018)
    paint(cape, lambda co: MOSS if co.z > 0.2 else MOSS_DARK)

    scarf = ring_band("Scarf", (0, 0.012, 0.55), 0.262, 0.252, 0.07, 0.035, segments=48)
    paint(scarf, SCARF)

    tail = tube("Scarf_Tail", lambda t: Vector((0.1 + 0.03 * t, 0.27 + 0.05 * t, 0.58 - 0.25 * t)),
                lambda t: 0.035 * (1 - 0.3 * t), rings=8, segments=8)
    paint(tail, SCARF)
    return join("Outfit_HoodedSuit", [hood, cape, scarf, tail]), None


def main():
    bpy.ops.wm.open_mainfile(filepath=RIG_BLEND)
    body = bpy.data.objects["Penguin_Body"]
    arm = bpy.data.objects["PenguinRig"]
    os.makedirs(OUT_DIR, exist_ok=True)

    builders = [
        ("HornedHelmet", lambda: horned_helmet(body)),
        ("WizardHat", lambda: wizard_hat(body)),
        ("Armor", lambda: armor(body, arm)),
        ("HoodedSuit", lambda: hooded_suit(body)),
    ]
    made = []
    for key, build in builders:
        obj, fixed = build()
        weight_by_height(obj, arm, fixed)
        export(obj, arm, os.path.join(OUT_DIR, "Outfit_%s.fbx" % key))
        made.append(obj)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(RIG_BLEND), "Outfits.blend"))


if __name__ == "__main__":
    main()
