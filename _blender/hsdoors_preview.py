# Renders preview PNGs of the generated models.
# Run: blender --background <hsdoors_models.blend> --python hsdoors_preview.py -- <out_dir>
import bpy
import math
import os
import sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0]) if argv else os.path.dirname(bpy.data.filepath)

scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_xray = False
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 1600
scene.render.resolution_y = 900
scene.render.film_transparent = False
scene.world = scene.world or bpy.data.worlds.new("w")

for ob in bpy.data.objects:
    if ob.name.startswith("COL_"):
        ob.hide_render = True
for m in bpy.data.materials:
    if m.name == "HS_Glass":
        m.diffuse_color = (0.55, 0.75, 0.85, 0.35)

cam_data = bpy.data.cameras.new("cam")
cam = bpy.data.objects.new("cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


def look(at, dist, yaw, pitch, lens=35):
    cam_data.lens = lens
    d = Vector((math.cos(math.radians(pitch)) * math.cos(math.radians(yaw)),
                math.cos(math.radians(pitch)) * math.sin(math.radians(yaw)),
                math.sin(math.radians(pitch))))
    cam.location = Vector(at) + d * dist
    cam.rotation_euler = (Vector(at) - cam.location).to_track_quat('-Z', 'Y').to_euler()


def shot(name, at, dist, yaw, pitch, lens=35):
    look(at, dist, yaw, pitch, lens)
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)


def only(names):
    for ob in bpy.data.objects:
        if ob.type != 'MESH' or ob.name.startswith("COL_"):
            continue
        ob.hide_render = ob.name not in names


# Overview of every piece in its grid slot.
shot("preview_all", (14, 10, 1.5), 34, -60, 32)

# An assembled 5 m revolving door: hub at origin, 4 wings of 2 leaves, two framed drum sides, canopy, floor.
def place(src, loc, rotz=0.0):
    o = bpy.data.objects[src].copy()
    o.data = bpy.data.objects[src].data
    o.location = Vector(loc)
    o.rotation_euler = (0, 0, math.radians(rotz))
    scene.collection.objects.link(o)
    return o.name

ax = Vector((60, 0, 0))
names = [place("HSPost4", ax)]
for a in (0, 90, 180, 270):
    for k in (1, 2):
        d = Vector((math.cos(math.radians(a)), math.sin(math.radians(a)), 0)) * k
        names.append(place("HSLeafFramedDark", ax + d, a))
names.append(place("HSDrumSide5mFramed", ax + Vector((3, 0, 0)), 0))
names.append(place("HSDrumSide5mFramed", ax + Vector((-3, 0, 0)), 180))
names.append(place("HSCanopy5m", ax + Vector((0, 0, 3))))
names.append(place("HSFloorMat5m", ax + Vector((0, 0, -1))))
only(set(names))
shot("preview_revolving", ax + Vector((0, 0, 1.6)), 11, -35, 18, 30)

# A bi-parting sliding door: 4 leaves + header with sensor.
sx = Vector((80, 0, 0))
names = []
for i in range(4):
    names.append(place("HSLeafFramedSilver", sx + Vector((i - 1.5, 0, 0)), 0))
    names.append(place("HSSlideHeaderSensor" if i in (1, 2) else ("HSSlideHeaderEnd" if i == 3 else "HSSlideHeader"), sx + Vector((i - 1.5, 0, 3)), 0))
only(set(names))
shot("preview_sliding", sx + Vector((0, 0, 1.7)), 8, -70, 12, 30)

with open(os.path.join(OUT, "_preview_done.txt"), "w") as f:
    f.write("DONE\n")
