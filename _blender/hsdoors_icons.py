# Inventory icons: blender --background hsdoors_models.blend --python hsdoors_icons.py -- <out_dir>
import bpy
import math
import os
import sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0]) if argv else os.path.join(os.path.dirname(bpy.data.filepath), "..", "UIAtlases", "ItemIconAtlas")
os.makedirs(OUT, exist_ok=True)

ICONS = [
    ("HSLeafFramedSilver", "hsdoorsLeafFramedSilver"),
    ("HSLeafFramedDark", "hsdoorsLeafFramedDark"),
    ("HSLeafFrameless", "hsdoorsLeafFrameless"),
    ("HSPostPlain", "hsdoorsPostPlain"),
    ("HSPost2", "hsdoorsPost2"),
    ("HSPost4", "hsdoorsPost4"),
    ("HSRotor3_3m", "hsdoorsRotor3_3m"),
    ("HSRotor3_5m", "hsdoorsRotor3_5m"),
    ("HSDrumSide3mFramed", "hsdoorsDrumSide3mFramed"),
    ("HSDrumSide3mGlass", "hsdoorsDrumSide3mGlass"),
    ("HSDrumSide3mSolid", "hsdoorsDrumSide3mSolid"),
    ("HSDrumSide5mFramed", "hsdoorsDrumSide5mFramed"),
    ("HSDrumSide5mGlass", "hsdoorsDrumSide5mGlass"),
    ("HSDrumSide5mSolid", "hsdoorsDrumSide5mSolid"),
    ("HSDrumWall3mGlass", "hsdoorsDrumWall3mGlass"),
    ("HSDrumWall3mSolid", "hsdoorsDrumWall3mSolid"),
    ("HSDrumWall5mGlass", "hsdoorsDrumWall5mGlass"),
    ("HSDrumWall5mSolid", "hsdoorsDrumWall5mSolid"),
    ("HSCanopy3m", "hsdoorsCanopy3m"),
    ("HSCanopy5m", "hsdoorsCanopy5m"),
    ("HSFloorMat3m", "hsdoorsFloorMat3m"),
    ("HSFloorMat5m", "hsdoorsFloorMat5m"),
    ("HSSlideHeader", "hsdoorsSlideHeader"),
    ("HSSlideHeaderSensor", "hsdoorsSlideHeaderSensor"),
    ("HSSlideHeaderEnd", "hsdoorsSlideHeaderEnd"),
]

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 232
scene.render.resolution_y = 160
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"

for m in bpy.data.materials:
    if m.name == "HS_Glass":
        m.diffuse_color = (0.45, 0.72, 0.88, 0.55)

for ob in bpy.data.objects:
    if ob.name.startswith("COL_"):
        ob.hide_render = True
        ob.hide_viewport = True

cam_data = bpy.data.cameras.new("icon_cam")
cam_data.lens = 45
cam = bpy.data.objects.new("icon_cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


def mesh_objects(root):
    out = []
    stack = [root]
    while stack:
        ob = stack.pop()
        if ob.type == "MESH" and not ob.name.startswith("COL_"):
            out.append(ob)
        stack.extend(list(ob.children))
    return out


def world_bounds(obs):
    mn = Vector((1e9, 1e9, 1e9))
    mx = Vector((-1e9, -1e9, -1e9))
    anyv = False
    for ob in obs:
        for corner in ob.bound_box:
            w = ob.matrix_world @ Vector(corner)
            mn.x, mn.y, mn.z = min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)
            mx.x, mx.y, mx.z = max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)
            anyv = True
    if not anyv:
        return Vector((0, 0, 0)), 1.0
    center = (mn + mx) * 0.5
    size = (mx - mn).length
    return center, max(size, 0.35)


def hide_all():
    for ob in bpy.data.objects:
        if ob.type == "MESH":
            ob.hide_render = True


def show_tree(root):
    for ob in mesh_objects(root):
        ob.hide_render = False


log = []
for obj_name, icon_name in ICONS:
    root = bpy.data.objects.get(obj_name)
    if root is None:
        log.append("MISSING " + obj_name)
        continue
    hide_all()
    show_tree(root)
    center, size = world_bounds(mesh_objects(root))
    dist = size * 1.35
    yaw, pitch = math.radians(-38), math.radians(22)
    d = Vector((
        math.cos(pitch) * math.cos(yaw),
        math.cos(pitch) * math.sin(yaw),
        math.sin(pitch),
    ))
    cam.location = center + d * dist
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = os.path.join(OUT, icon_name + ".png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    log.append(icon_name + " -> " + path)

with open(os.path.join(OUT, "_icons_log.txt"), "w") as f:
    f.write("\n".join(log) + "\nDONE\n")
