"""
Frostbound — preparar el pingüino para vestirlo (Blender 4.2+ / 5.x)

Qué hace:
  1. Importa Penguin.obj (malla única de ~48k caras con piezas sueltas).
  2. La rehace como una sola malla cerrada (voxel remesh) y la reduce a ~9k triángulos.
  3. La separa en Body, Flipper_L/R y Foot_L/R (tapa los huecos del corte).
  4. Pinta máscaras de color en los vértices (R = barriga/cara blanca, G = pico y patas naranja,
     B = pupilas) para que el shader toon de Unity tiña solo el plumaje.
  5. Crea el esqueleto (Root, Hips, Spine, Head, Flipper_L/R, Foot_L/R) y los huesos de anclaje
     Anchor_Head, Anchor_Chest, Anchor_Back, Anchor_Hand_L, Anchor_Hand_R.
  6. Pesa cada parte y exporta Penguin_Rigged.fbx listo para Unity.

Uso desde la interfaz: pestaña Scripting > abrir este archivo > ajustar SRC/OUT_DIR > Run Script.
Uso en consola:  blender -b -P frostbound_penguin_rig.py -- "ruta/Penguin.obj" "carpeta_fbx" "carpeta_blend"
"""

import os
import sys
from collections import defaultdict

import bpy
import bmesh
import numpy as np
from mathutils import Vector

SRC = r"C:\Users\Sebastian\My project (2)\Assets\Penguin.obj"
OUT_DIR = r"C:\Users\Sebastian\My project (2)\Assets\Models\Penguin"
BLEND_DIR = r"C:\Users\Sebastian\My project (2)\Blender"

if "--" in sys.argv:
    extra = sys.argv[sys.argv.index("--") + 1:]
    if len(extra) >= 1:
        SRC = extra[0]
    if len(extra) >= 2:
        OUT_DIR = extra[1]
    if len(extra) >= 3:
        BLEND_DIR = extra[2]

CFG = {
    "voxel_size": 0.008,
    "target_tris": 9000,
    "smooth_iterations": 4,
    "foot_height": 0.087,
    "foot_height_front": 0.112,
    "foot_front_min": 0.12,
    "flipper_min_h": -0.32,
    "flipper_max_h": 0.16,
    "flipper_band": (-0.13, 0.17),
    "flipper_body_width": ([-0.3, -0.2, -0.12, -0.04, 0.04, 0.18],
                           [0.35, 0.335, 0.30, 0.255, 0.245, 0.25]),
    "beak_h": (0.13, 0.30),
    "beak_half_width": 0.13,
    "beak_front": 0.225,
    "eye_centers": [(-0.045, 0.33), (0.045, 0.33)],
    "eye_radius": 0.04,
    "pupil_radius": 0.019,
    "belly": (0.0, -0.14, 0.27, 0.27),
    "belly_min_forward_normal": 0.3,
}

PARTS = ["Body", "Flipper_L", "Flipper_R", "Foot_L", "Foot_R"]


def log(msg):
    print("[Frostbound] " + msg)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_and_clean(path):
    bpy.ops.wm.obj_import(filepath=path)
    obj = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    remesh = obj.modifiers.new("Remesh", "REMESH")
    remesh.mode = "VOXEL"
    remesh.voxel_size = CFG["voxel_size"]
    bpy.ops.object.modifier_apply(modifier=remesh.name)

    faces = len(obj.data.polygons)
    dec = obj.modifiers.new("Decimate", "DECIMATE")
    dec.ratio = min(1.0, CFG["target_tris"] / (faces * 2.0))
    dec.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=dec.name)

    min_z = min(v.co.z for v in obj.data.vertices)
    for v in obj.data.vertices:
        v.co.z -= min_z
    obj.data.materials.clear()
    obj.name = "Penguin"
    log("malla limpia: %d caras (antes %d tras remesh)" % (len(obj.data.polygons), faces))
    return obj


def face_neighbors(me):
    edge_faces = defaultdict(list)
    for p in me.polygons:
        for k in p.edge_keys:
            edge_faces[k].append(p.index)
    nb = [[] for _ in me.polygons]
    for fs in edge_faces.values():
        if len(fs) == 2:
            a, b = fs
            nb[a].append(b)
            nb[b].append(a)
    return nb


def largest_component(mask, nb):
    seen = np.zeros(len(mask), bool)
    best = []
    for s in np.where(mask)[0]:
        if seen[s]:
            continue
        stack, comp = [s], []
        seen[s] = True
        while stack:
            i = stack.pop()
            comp.append(i)
            for j in nb[i]:
                if mask[j] and not seen[j]:
                    seen[j] = True
                    stack.append(j)
        if len(comp) > len(best):
            best = comp
    return best


def segment(obj):
    me = obj.data
    c = np.array([tuple(p.center) for p in me.polygons])
    x, forward, h = c[:, 0], -c[:, 1], c[:, 2] - 0.5024

    xs, ws = CFG["flipper_body_width"]
    body_w = np.interp(h, xs, ws)
    band = CFG["flipper_band"]
    flipper = (np.abs(x) > body_w) & (h > CFG["flipper_min_h"]) & (h < CFG["flipper_max_h"]) \
        & (forward > band[0]) & (forward < band[1])
    foot = (h < -0.5024 + CFG["foot_height"]) | \
           ((h < -0.5024 + CFG["foot_height_front"]) & (forward > CFG["foot_front_min"]))

    cand = np.where(flipper, 1, 0)
    cand[foot] = 2
    nb = face_neighbors(me)
    for _ in range(CFG["smooth_iterations"]):
        new = cand.copy()
        for i in range(len(cand)):
            vals = [cand[j] for j in nb[i]] + [cand[i]]
            new[i] = max(set(vals), key=vals.count)
        cand = new

    labels = np.zeros(len(cand), int)
    for code, (left, right) in [(1, (1, 2)), (2, (3, 4))]:
        for side, lbl in [(1, left), (-1, right)]:
            comp = largest_component((cand == code) & (np.sign(x) == side), nb)
            labels[comp] = lbl
    log("segmentación: " + ", ".join("%s=%d" % (PARTS[k], int((labels == k).sum())) for k in range(5)))
    return labels


def soft_inside(r, width=0.12):
    return 1.0 - smoothstep(1.0 - width, 1.0 + width, r)


def paint_masks(obj):
    """Máscaras continuas: el shader las corta en 0.5, así el borde queda liso aunque la malla sea low-poly."""
    me = obj.data
    attr = me.color_attributes.new("Mask", "FLOAT_COLOR", "POINT")
    bx, by, bax, bay = CFG["belly"]
    bh = CFG["beak_h"]
    for i, v in enumerate(me.vertices):
        x, forward, h = v.co.x, -v.co.y, v.co.z - 0.5024
        front = smoothstep(CFG["belly_min_forward_normal"] - 0.15, CFG["belly_min_forward_normal"] + 0.15, -v.normal.y)

        r_belly = (((x - bx) / bax) ** 2 + ((h - by) / bay) ** 2) ** 0.5
        white = soft_inside(r_belly) * front

        pupil = 0.0
        for ex, eh in CFG["eye_centers"]:
            d = ((x - ex) ** 2 + (h - eh) ** 2) ** 0.5
            if forward > 0.1:
                white = max(white, soft_inside(d / CFG["eye_radius"], 0.2))
                pupil = max(pupil, soft_inside(d / CFG["pupil_radius"], 0.25))

        accent = smoothstep(CFG["beak_front"] - 0.012, CFG["beak_front"] + 0.012, forward) \
            * smoothstep(bh[0] - 0.01, bh[0] + 0.01, h) * (1.0 - smoothstep(bh[1] - 0.01, bh[1] + 0.01, h)) \
            * (1.0 - smoothstep(CFG["beak_half_width"] - 0.015, CFG["beak_half_width"] + 0.015, abs(x)))
        white *= 1.0 - accent
        attr.data[i].color = (white, accent, pupil, 1.0)
    me.color_attributes.active_color = attr


def separate(obj, labels):
    me = obj.data
    for idx, name in enumerate(PARTS):
        mat = bpy.data.materials.get("Penguin_" + name) or bpy.data.materials.new("Penguin_" + name)
        me.materials.append(mat)
    for p in me.polygons:
        p.material_index = int(labels[p.index])

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="MATERIAL")
    bpy.ops.object.mode_set(mode="OBJECT")

    parts = {}
    for o in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
        mat_name = o.data.materials[o.data.polygons[0].material_index].name if o.data.polygons else ""
        for name in PARTS:
            if mat_name == "Penguin_" + name:
                o.name = o.data.name = "Penguin_" + name
                parts[name] = o
    for name, o in parts.items():
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        bm = bmesh.from_edit_mesh(o.data)
        bmesh.ops.holes_fill(bm, edges=bm.edges, sides=0)
        bmesh.update_edit_mesh(o.data)
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
        bpy.ops.object.mode_set(mode="OBJECT")
        for p in o.data.polygons:
            p.material_index = 0
            p.use_smooth = True
        while len(o.data.materials) > 1:
            o.data.materials.pop(index=len(o.data.materials) - 1)
        o.data.materials[0] = bpy.data.materials.get("Penguin_" + name)
    fix_cap_colors(parts)
    return parts


def fix_cap_colors(parts):
    for name, o in parts.items():
        attr = o.data.color_attributes.get("Mask")
        if attr is None:
            continue
        for i, v in enumerate(o.data.vertices):
            if name.startswith("Foot"):
                attr.data[i].color = (0.0, 1.0, 0.0, 1.0)
            elif name.startswith("Flipper"):
                attr.data[i].color = (0.0, 0.0, 0.0, 1.0)


def centroid(o, pick=None):
    pts = np.array([tuple(v.co) for v in o.data.vertices])
    if pick is not None:
        pts = pts[pick(pts)]
    return pts


def build_armature(parts):
    body = centroid(parts["Body"])
    top = body[:, 2].max()

    def flipper_axes(o):
        pts = centroid(o)
        root = pts[pts[:, 2] > pts[:, 2].max() - 0.06].mean(0)
        tip = pts[pts[:, 2] < pts[:, 2].min() + 0.04].mean(0)
        return Vector(root), Vector(tip)

    def foot_axes(o):
        pts = centroid(o)
        back = pts[-pts[:, 1] < np.percentile(-pts[:, 1], 25)].mean(0)
        front = pts[-pts[:, 1] > np.percentile(-pts[:, 1], 85)].mean(0)
        heel = Vector((back[0], back[1], pts[:, 2].max() * 0.6))
        toe = Vector((front[0], front[1], pts[:, 2].max() * 0.35))
        return heel, toe

    arm_data = bpy.data.armatures.new("PenguinRig")
    arm = bpy.data.objects.new("PenguinRig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones

    def bone(name, head, tail, parent=None, deform=True):
        b = eb.new(name)
        b.head, b.tail = Vector(head), Vector(tail)
        if parent:
            b.parent = eb[parent]
        b.use_deform = deform
        b.roll = 0.0
        return b

    bone("Root", (0, 0, 0), (0, 0.15, 0), deform=False)
    bone("Hips", (0, 0, 0.12), (0, 0, 0.45), "Root")
    bone("Spine", (0, 0, 0.45), (0, 0, 0.70), "Hips")
    bone("Head", (0, 0, 0.70), (0, 0, top), "Spine")

    for side in ("L", "R"):
        root, tip = flipper_axes(parts["Flipper_" + side])
        bone("Flipper_" + side, root, tip, "Spine")
        direction = (tip - root).normalized()
        bone("Anchor_Hand_" + side, tip, tip + direction * 0.08, "Flipper_" + side, deform=False)
        heel, toe = foot_axes(parts["Foot_" + side])
        bone("Foot_" + side, heel, toe, "Hips")

    front_chest = body[(np.abs(body[:, 0]) < 0.05) & (np.abs(body[:, 2] - 0.45) < 0.03)]
    chest_y = front_chest[:, 1].min() if len(front_chest) else -0.29
    back_y = front_chest[:, 1].max() if len(front_chest) else 0.28
    bone("Anchor_Head", (0, 0, top), (0, 0, top + 0.1), "Head", deform=False)
    bone("Anchor_Chest", (0, chest_y, 0.45), (0, chest_y - 0.1, 0.45), "Spine", deform=False)
    bone("Anchor_Back", (0, back_y, 0.50), (0, back_y + 0.1, 0.50), "Spine", deform=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def smoothstep(e0, e1, x):
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def skin(parts, arm):
    for name, o in parts.items():
        o.parent = arm
        mod = o.modifiers.new("Armature", "ARMATURE")
        mod.object = arm
        groups = {b: o.vertex_groups.new(name=b) for b in ("Hips", "Spine", "Head")}
        if name == "Body":
            for v in o.data.vertices:
                z = v.co.z
                head_w = smoothstep(0.62, 0.78, z)
                spine_w = smoothstep(0.30, 0.52, z) * (1.0 - head_w)
                hips_w = max(0.0, 1.0 - head_w - spine_w)
                for bname, w in (("Hips", hips_w), ("Spine", spine_w), ("Head", head_w)):
                    if w > 0.001:
                        groups[bname].add([v.index], w, "REPLACE")
        else:
            target = o.vertex_groups.new(name=name)
            parent_bone = "Spine" if name.startswith("Flipper") else "Hips"
            root_z = max(v.co.z for v in o.data.vertices)
            for v in o.data.vertices:
                if name.startswith("Flipper"):
                    w = smoothstep(root_z, root_z - 0.08, v.co.z)
                else:
                    w = 1.0
                target.add([v.index], w, "REPLACE")
                if w < 0.999:
                    groups[parent_bone].add([v.index], 1.0 - w, "REPLACE")


def export_fbx(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=False,
        mesh_smooth_type="FACE",
        colors_type="LINEAR",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        bake_anim=False,
    )
    log("exportado " + path)


def main():
    reset_scene()
    obj = import_and_clean(SRC)
    labels = segment(obj)
    paint_masks(obj)
    parts = separate(obj, labels)
    arm = build_armature(parts)
    skin(parts, arm)
    os.makedirs(OUT_DIR, exist_ok=True)
    os.makedirs(BLEND_DIR, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, "Penguin_Rigged.blend"))
    export_fbx(os.path.join(OUT_DIR, "Penguin_Rigged.fbx"), [arm] + list(parts.values()))


if __name__ == "__main__":
    main()
