# HS Doors model generator.
# Run: blender --background --factory-startup --python hsdoors_models.py -- <fbx_out_dir> [<blend_out>]
#
# Conventions (match 7DTD ModelEntity with ModelOffset="0,0,0"):
#   - 1 Blender unit = 1 block = 1 m. Blender Z is up.
#   - Origin = bottom centre of the parent block cell. Footprints are odd so the game adds no X/Z shift.
#   - Objects named COL_* become BoxColliders in Unity (mesh renderer removed).
#   - Material slot names are mapped to Unity materials by name.

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT_DIR = os.path.abspath(argv[0]) if len(argv) > 0 else os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "_unity", "Assets", "HSDoors", "Models"))
BLEND_OUT = os.path.abspath(argv[1]) if len(argv) > 1 else os.path.abspath(os.path.join(os.path.dirname(__file__), "hsdoors_models.blend"))

DOOR_H = 3.0          # glass / leaf height (3 blocks)
GLASS_T = 0.012       # glass thickness
FRAME = 0.05          # frame section
MAT_COLORS = {
    "HS_Glass": (0.75, 0.85, 0.9, 0.25),
    "HS_FrameSilver": (0.78, 0.79, 0.8, 1.0),
    "HS_FrameDark": (0.08, 0.08, 0.09, 1.0),
    "HS_Rubber": (0.02, 0.02, 0.02, 1.0),
    "HS_Mat": (0.12, 0.12, 0.13, 1.0),
    "HS_Floor": (0.35, 0.35, 0.36, 1.0),
    "HS_Sensor": (0.03, 0.03, 0.035, 1.0),
    "HS_Light": (1.0, 0.97, 0.9, 1.0),
}


def material(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.diffuse_color = MAT_COLORS.get(name, (0.5, 0.5, 0.5, 1.0))
    return m


class Piece:
    """One exported model: a single render mesh with several material slots, plus collider boxes."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.cols = []

    def mat_index(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _finish(self, faces, mat, smooth=False):
        mi = self.mat_index(mat)
        for f in faces:
            f.material_index = mi
            f.smooth = smooth

    # Axis-aligned box (then rotated about Z through the origin by rotz degrees).
    def box(self, mat, center, size, rotz=0.0, collide=False):
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res["verts"]
        m = Matrix.Rotation(math.radians(rotz), 4, 'Z') @ Matrix.Translation(Vector(center)) @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self._finish(faces, mat)
        if collide:
            self.collider(center, size, rotz)

    def cylinder(self, mat, center, radius, height, segs=24):
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs,
                                    radius1=radius, radius2=radius, depth=height)
        verts = res["verts"]
        bmesh.ops.translate(self.bm, vec=Vector((center[0], center[1], center[2] + height * 0.5)), verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self._finish(faces, mat, smooth=True)

    # Curved slab around the Z axis through (cx, cy). Angles in degrees, 0 = +X, CCW.
    def arc(self, mat, cx, cy, r_in, r_out, a0, a1, z0, z1, segs=24, caps=True, smooth=True):
        bm = self.bm
        ring = []
        for i in range(segs + 1):
            a = math.radians(a0 + (a1 - a0) * i / segs)
            c, s = math.cos(a), math.sin(a)
            ring.append((
                bm.verts.new((cx + r_in * c, cy + r_in * s, z0)),
                bm.verts.new((cx + r_out * c, cy + r_out * s, z0)),
                bm.verts.new((cx + r_out * c, cy + r_out * s, z1)),
                bm.verts.new((cx + r_in * c, cy + r_in * s, z1)),
            ))
        faces = []
        for i in range(segs):
            a, b = ring[i], ring[i + 1]
            faces.append(bm.faces.new((a[1], b[1], b[2], a[2])))   # outer
            faces.append(bm.faces.new((a[3], b[3], b[0], a[0])))   # inner
            faces.append(bm.faces.new((a[2], b[2], b[3], a[3])))   # top
            faces.append(bm.faces.new((a[0], b[0], b[1], a[1])))   # bottom
        self._finish(faces, mat, smooth)
        if caps:
            e = ring[0]
            f0 = bm.faces.new((e[0], e[1], e[2], e[3]))
            e = ring[-1]
            f1 = bm.faces.new((e[3], e[2], e[1], e[0]))
            self._finish([f0, f1], mat)

    def disc(self, mat, cx, cy, radius, z0, z1, segs=48):
        self.cylinder(mat, (cx, cy, z0), radius, z1 - z0, segs)

    def collider(self, center, size, rotz=0.0):
        self.cols.append((tuple(center), tuple(size), rotz))

    # Ring of collider boxes following an arc (chord boxes).
    def arc_colliders(self, cx, cy, r_mid, thick, a0, a1, z0, z1, n):
        step = (a1 - a0) / n
        chord = 2.0 * r_mid * math.sin(math.radians(abs(step)) * 0.5) + 0.02
        for i in range(n):
            a = a0 + step * (i + 0.5)
            ar = math.radians(a)
            c = (cx + r_mid * math.cos(ar), cy + r_mid * math.sin(ar), (z0 + z1) * 0.5)
            # box local X is radial thickness, local Y is along the chord
            self.cols.append((c, (thick, chord, z1 - z0), a, True))

    def build(self, collection):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(material(m))
        root = bpy.data.objects.new(self.name, me)
        collection.objects.link(root)
        for i, c in enumerate(self.cols):
            if len(c) == 4:
                center, size, rotz, about_self = c
            else:
                center, size, rotz = c
                about_self = False
            bm = bmesh.new()
            bmesh.ops.create_cube(bm, size=1.0)
            cm = bpy.data.meshes.new("COL_%s_%d" % (self.name, i))
            bm.to_mesh(cm)
            bm.free()
            ob = bpy.data.objects.new("COL_%d" % i, cm)
            ob.parent = root
            if about_self:
                ob.location = Vector(center)
                ob.rotation_euler = (0.0, 0.0, math.radians(rotz))
            else:
                rot = Matrix.Rotation(math.radians(rotz), 3, 'Z')
                ob.location = rot @ Vector(center)
                ob.rotation_euler = (0.0, 0.0, math.radians(rotz))
            ob.scale = Vector(size)
            collection.objects.link(ob)
        return root


# ---------------------------------------------------------------- pieces

def leaf(name, frame_mat, framed=True):
    """1 m wide x 3 m door leaf in the cell's centre plane (X = width, Y = depth)."""
    p = Piece(name)
    w, h = 1.0, DOOR_H
    if framed:
        stile = FRAME
        top, bottom = 0.08, 0.16
        p.box(frame_mat, (-w / 2 + stile / 2, 0, h / 2), (stile, FRAME, h))
        p.box(frame_mat, (w / 2 - stile / 2, 0, h / 2), (stile, FRAME, h))
        p.box(frame_mat, (0, 0, h - top / 2), (w - 2 * stile, FRAME, top))
        p.box(frame_mat, (0, 0, bottom / 2 + 0.01), (w - 2 * stile, FRAME, bottom))
        p.box("HS_Rubber", (0, 0, 0.005), (w - 0.02, FRAME * 0.6, 0.01))
        p.box("HS_Glass", (0, 0, (bottom + h - top) / 2), (w - 2 * stile, GLASS_T, h - top - bottom))
    else:
        rail = 0.06
        p.box(frame_mat, (0, 0, h - rail / 2), (w, 0.03, rail))
        p.box(frame_mat, (0, 0, rail / 2), (w, 0.03, rail))
        p.box("HS_Glass", (0, 0, h / 2), (w - 0.006, GLASS_T, h - 2 * rail))
        # patch fittings
        for x in (-w / 2 + 0.08, w / 2 - 0.08):
            p.box(frame_mat, (x, 0, h - 0.1), (0.12, 0.035, 0.08))
            p.box(frame_mat, (x, 0, 0.1), (0.12, 0.035, 0.08))
    p.collider((0, 0, h / 2), (w, 0.06, h))
    return p


def post(name, stubs):
    """Revolving centre post. stubs = angles (deg) of half-wings that close the gap from the post to the next cell."""
    p = Piece(name)
    p.cylinder("HS_FrameDark", (0, 0, 0), 0.075, DOOR_H, 32)
    p.cylinder("HS_FrameDark", (0, 0, DOOR_H - 0.04), 0.11, 0.04, 32)
    p.cylinder("HS_FrameDark", (0, 0, 0), 0.11, 0.03, 32)
    p.collider((0, 0, DOOR_H / 2), (0.16, 0.16, DOOR_H))
    for a in stubs:
        half = 0.5 - 0.075
        cx = 0.075 + half / 2
        p.box("HS_FrameDark", (cx, 0, DOOR_H - 0.04), (half, FRAME, 0.08), rotz=a)
        p.box("HS_FrameDark", (cx, 0, 0.09), (half, FRAME, 0.16), rotz=a)
        p.box("HS_Glass", (cx, 0, DOOR_H / 2), (half, GLASS_T, DOOR_H - 0.24), rotz=a)
        p.box("HS_Rubber", (cx, 0, 0.005), (half, FRAME * 0.6, 0.01), rotz=a)
        p.collider((cx, 0, DOOR_H / 2), (half, 0.06, DOOR_H), rotz=a)
    return p


def rotor3(name, radius):
    """Three wings at 120 deg, post included. Placed in the hub cell; the wings overhang into neighbours."""
    p = post(name, [])
    length = radius - 0.075 - 0.03
    for a in (90.0, 210.0, 330.0):
        cx = 0.075 + length / 2
        p.box("HS_FrameDark", (cx, 0, DOOR_H - 0.04), (length, FRAME, 0.08), rotz=a)
        p.box("HS_FrameDark", (cx, 0, 0.09), (length, FRAME, 0.16), rotz=a)
        p.box("HS_FrameDark", (0.075 + length - FRAME / 2, 0, DOOR_H / 2), (FRAME, FRAME, DOOR_H), rotz=a)
        p.box("HS_Glass", (cx - FRAME / 2, 0, DOOR_H / 2), (length - FRAME, GLASS_T, DOOR_H - 0.24), rotz=a)
        p.box("HS_Rubber", (cx, 0, 0.005), (length, FRAME * 0.6, 0.01), rotz=a)
        p.collider((cx, 0, DOOR_H / 2), (length, 0.06, DOOR_H), rotz=a)
    return p


def drum_side(name, radius, cells_out, framed, solid=False, height=None):
    """Curved side wall covering 90 deg. Parent cell sits cells_out blocks from the hub (+X in Blender);
    the arc's inner face touches the parent cell's inner face. solid=True is an opaque metal wall.
    height=None uses the 3-block door; pass 1.0 for a stackable paint-sized ring."""
    p = Piece(name)
    cx = -float(cells_out)
    h = DOOR_H if height is None else float(height)
    a0, a1 = -45.0, 45.0
    segs = 32
    if solid:
        thick = 0.12
        r_in, r_out = radius, radius + thick
        p.arc("HS_Mat", cx, 0, r_in, r_out, a0, a1, 0.0, h, segs, caps=True, smooth=True)
        p.arc_colliders(cx, 0, (r_in + r_out) / 2, thick, a0, a1, 0.0, h, 8)
        return p
    r_in, r_out = radius, radius + GLASS_T
    frame_mat = "HS_FrameDark"
    rail_b, rail_t = (0.16, 0.10) if framed else (0.06, 0.06)
    if h <= 1.01:
        rail_b, rail_t = (0.04, 0.04)
    p.arc("HS_Glass", cx, 0, r_in, r_out, a0, a1, rail_b, h - rail_t, segs, caps=False)
    p.arc(frame_mat, cx, 0, r_in - 0.02, r_out + 0.03, a0, a1, 0.0, rail_b, segs)
    p.arc(frame_mat, cx, 0, r_in - 0.02, r_out + 0.03, a0, a1, h - rail_t, h, segs)
    ends = (a0, a1) if not framed else (a0, -15.0, 15.0, a1)
    for a in ends:
        ar = math.radians(a)
        rr = (r_in + r_out) / 2
        p.box(frame_mat, (cx + rr * math.cos(ar), rr * math.sin(ar), h / 2), (0.08, 0.08, h), rotz=0)
    p.arc_colliders(cx, 0, (r_in + r_out) / 2, 0.08, a0, a1, 0.0, h, 8)
    return p


def canopy(name, radius):
    """Round ceiling over the drum. Sits in the hub cell directly above the 3-block opening."""
    p = Piece(name)
    p.disc("HS_FrameDark", 0, 0, radius + 0.1, 0.0, 0.08, 64)
    p.arc("HS_FrameDark", 0, 0, radius + 0.02, radius + 0.12, 0.0, 360.0, 0.0, 0.45, 96, caps=False, smooth=True)
    p.disc("HS_FrameDark", 0, 0, radius + 0.1, 0.40, 0.45, 64)
    p.arc("HS_Light", 0, 0, radius * 0.55, radius * 0.6, 0.0, 360.0, -0.005, 0.0, 96, caps=False)
    p.collider((0, 0, 0.225), (2 * radius * 0.72, 2 * radius * 0.72, 0.45))
    p.collider((0, 0, 0.225), (2 * radius * 0.72, 2 * radius * 0.72, 0.45), rotz=45)
    return p


def floor_mat(name, radius):
    """Full floor block with a round entrance mat set flush into the top. Goes under the hub."""
    p = Piece(name)
    p.box("HS_Floor", (0, 0, 0.5), (1.0, 1.0, 1.0), collide=True)
    p.disc("HS_Mat", 0, 0, radius, 0.995, 1.004, 64)
    p.arc("HS_FrameDark", 0, 0, radius, radius + 0.04, 0.0, 360.0, 0.99, 1.006, 96, caps=False)
    return p


def slide_header(name, sensor):
    """Operator box above sliding leaves. Placed in the row directly above the leaves, one per block."""
    p = Piece(name)
    p.box("HS_FrameSilver", (0, 0, 0.2), (1.0, 0.24, 0.4), collide=True)
    p.box("HS_FrameSilver", (0, -0.125, 0.2), (1.0, 0.01, 0.38))
    if sensor:
        p.box("HS_Sensor", (0, -0.15, 0.08), (0.28, 0.05, 0.07))
        p.box("HS_Light", (0.1, -0.176, 0.08), (0.02, 0.002, 0.02))
    return p


def slide_header_end(name):
    p = slide_header(name, False)
    p.box("HS_FrameDark", (0.49, 0, 0.2), (0.02, 0.25, 0.41))
    return p


PIECES = [
    lambda: leaf("HSLeafFramedSilver", "HS_FrameSilver", True),
    lambda: leaf("HSLeafFramedDark", "HS_FrameDark", True),
    lambda: leaf("HSLeafFrameless", "HS_FrameSilver", False),
    lambda: post("HSPostPlain", []),
    lambda: post("HSPost2", [0.0, 180.0]),
    lambda: post("HSPost4", [0.0, 90.0, 180.0, 270.0]),
    lambda: rotor3("HSRotor3_3m", 1.5),
    lambda: rotor3("HSRotor3_5m", 2.5),
    lambda: drum_side("HSDrumSide3mFramed", 1.5, 2, True),
    lambda: drum_side("HSDrumSide3mGlass", 1.5, 2, False),
    lambda: drum_side("HSDrumSide3mSolid", 1.5, 2, False, solid=True),
    lambda: drum_side("HSDrumSide5mFramed", 2.5, 3, True),
    lambda: drum_side("HSDrumSide5mGlass", 2.5, 3, False),
    lambda: drum_side("HSDrumSide5mSolid", 2.5, 3, False, solid=True),
    lambda: drum_side("HSDrumWall3mGlass", 1.5, 2, False, height=1.0),
    lambda: drum_side("HSDrumWall3mSolid", 1.5, 2, False, solid=True, height=1.0),
    lambda: drum_side("HSDrumWall5mGlass", 2.5, 3, False, height=1.0),
    lambda: drum_side("HSDrumWall5mSolid", 2.5, 3, False, solid=True, height=1.0),
    lambda: canopy("HSCanopy3m", 1.5),
    lambda: canopy("HSCanopy5m", 2.5),
    lambda: floor_mat("HSFloorMat3m", 1.5),
    lambda: floor_mat("HSFloorMat5m", 2.5),
    lambda: slide_header("HSSlideHeader", False),
    lambda: slide_header("HSSlideHeaderSensor", True),
    lambda: slide_header_end("HSSlideHeaderEnd"),
]


def clear_scene():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)


def export_piece(root, path):
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for c in root.children:
        c.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
        bake_anim=False,
    )


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    clear_scene()
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    log = []
    spacing = 7.0
    for i, make in enumerate(PIECES):
        piece = make()
        coll = bpy.data.collections.new(piece.name)
        scene.collection.children.link(coll)
        root = piece.build(coll)
        bpy.context.view_layer.update()
        path = os.path.join(OUT_DIR, piece.name + ".fbx")
        export_piece(root, path)
        log.append("%s verts=%d cols=%d -> %s" % (piece.name, len(root.data.vertices), len(root.children), path))
        # lay the pieces out in a row in the .blend so they are easy to look at
        root.location.x = (i % 5) * spacing
        root.location.y = (i // 5) * spacing
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    with open(os.path.join(OUT_DIR, "_export_log.txt"), "w") as f:
        f.write("\n".join(log) + "\nDONE\n")


try:
    main()
except Exception as e:
    import traceback
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(os.path.join(OUT_DIR, "_export_log.txt"), "w") as f:
        f.write("FAILED\n" + traceback.format_exc())
    raise
