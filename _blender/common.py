# Shared Blender helpers for HS_Doors. Block origin is the parent-cell min corner (0,0,0).
# Units are metres. Y is up in the exported FBX (Unity / 7DTD).
import math
import bmesh
import bpy
from mathutils import Vector

MAT_GLASS = "HS_Glass"
MAT_FRAME = "HS_Frame"
MAT_RUBBER = "HS_Rubber"
MAT_MAT = "HS_Mat"


def setup_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    ensure_materials()


def ensure_materials():
    _mat(MAT_GLASS, (0.55, 0.72, 0.82, 0.28), metallic=0.0, rough=0.04, trans=0.95, alpha=0.28)
    _mat(MAT_FRAME, (0.55, 0.57, 0.6, 1.0), metallic=1.0, rough=0.32, trans=0.0, alpha=1.0)
    _mat(MAT_RUBBER, (0.04, 0.04, 0.05, 1.0), metallic=0.0, rough=0.85, trans=0.0, alpha=1.0)
    _mat(MAT_MAT, (0.12, 0.12, 0.13, 1.0), metallic=0.1, rough=0.7, trans=0.0, alpha=1.0)


def _mat(name, rgba, metallic, rough, trans, alpha):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.blend_method = "BLEND" if alpha < 0.99 else "OPAQUE"
    bsdf = None
    for n in mat.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED":
            bsdf = n
            break
    if bsdf is None:
        return mat
    def set_in(key, val):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = val
    set_in("Base Color", rgba)
    set_in("Metallic", metallic)
    set_in("Roughness", rough)
    set_in("Alpha", alpha)
    set_in("IOR", 1.45)
    set_in("Transmission Weight", trans)
    set_in("Transmission", trans)
    return mat


def new_mesh(name, verts, faces, mat_name, uvs=None):
    mesh = bpy.data.meshes.new(name + "_mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mat = bpy.data.materials.get(mat_name)
    if mat is not None:
        if mesh.materials:
            mesh.materials[0] = mat
        else:
            mesh.materials.append(mat)
    if uvs is None:
        _unwrap_smart(obj)
    else:
        _apply_uv(mesh, uvs)
    return obj


def _unwrap_smart(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    try:
        bpy.ops.uv.smart_project(angle_limit=66.0, island_margin=0.02)
    except TypeError:
        bpy.ops.uv.smart_project()
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)


def _apply_uv(mesh, uvs):
    uv = mesh.uv_layers.new(name="UVMap")
    for i, loop in enumerate(mesh.loops):
        if i < len(uvs):
            uv.data[i].uv = uvs[i]


def box(name, size, center, mat_name):
    sx, sy, sz = size
    cx, cy, cz = center
    hx, hy, hz = sx * 0.5, sy * 0.5, sz * 0.5
    verts = [
        (cx - hx, cy - hy, cz - hz),
        (cx + hx, cy - hy, cz - hz),
        (cx + hx, cy + hy, cz - hz),
        (cx - hx, cy + hy, cz - hz),
        (cx - hx, cy - hy, cz + hz),
        (cx + hx, cy - hy, cz + hz),
        (cx + hx, cy + hy, cz + hz),
        (cx - hx, cy + hy, cz + hz),
    ]
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (2, 6, 7, 3),
        (0, 3, 7, 4),
        (1, 5, 6, 2),
    ]
    return new_mesh(name, verts, faces, mat_name)


def cylinder(name, radius, height, segs, center, mat_name, axis="Y"):
    verts = []
    faces = []
    cx, cy, cz = center
    for ring, y in ((0, cy - height * 0.5), (1, cy + height * 0.5)):
        for i in range(segs):
            a = (i / segs) * math.tau
            verts.append((cx + math.cos(a) * radius, y, cz + math.sin(a) * radius))
    for i in range(segs):
        a = i
        b = (i + 1) % segs
        faces.append((a, b, b + segs, a + segs))
    bot = [i for i in range(segs - 1, -1, -1)]
    top = [segs + i for i in range(segs)]
    faces.append(tuple(bot))
    faces.append(tuple(top))
    return new_mesh(name, verts, faces, mat_name)


def arc_shell(name, radius, thickness, height, a0, a1, segs, y0, mat_name):
    """Quarter (or any) cylinder wall. Origin at (0,0,0). Y up. Arc in XZ."""
    r0 = radius - thickness * 0.5
    r1 = radius + thickness * 0.5
    y1 = y0 + height
    verts = []
    for y in (y0, y1):
        for r in (r0, r1):
            for i in range(segs + 1):
                t = i / segs
                a = a0 + (a1 - a0) * t
                verts.append((math.cos(a) * r, y, math.sin(a) * r))
    # rings: 0 bot-inner, 1 bot-outer, 2 top-inner, 3 top-outer. Each segs+1 verts.
    n = segs + 1
    faces = []
    def ring(ri):
        return ri * n
    for i in range(segs):
        bi, bo, ti, to = ring(0), ring(1), ring(2), ring(3)
        # outer wall
        faces.append((bo + i, bo + i + 1, to + i + 1, to + i))
        # inner wall (reverse winding)
        faces.append((bi + i + 1, bi + i, ti + i, ti + i + 1))
        # bottom cap
        faces.append((bi + i, bi + i + 1, bo + i + 1, bo + i))
        # top cap
        faces.append((ti + i + 1, ti + i, to + i, to + i + 1))
    # end caps
    i0, i1 = 0, segs
    bi, bo, ti, to = ring(0), ring(1), ring(2), ring(3)
    faces.append((bi + i0, bo + i0, to + i0, ti + i0))
    faces.append((bo + i1, bi + i1, ti + i1, to + i1))
    return new_mesh(name, verts, faces, mat_name)


def arc_slab(name, radius_in, radius_out, thick, a0, a1, segs, y, mat_name):
    """Flat quarter disc (floor / canopy) sitting on Y=y, thickness up."""
    y0, y1 = y, y + thick
    verts = []
    for yy in (y0, y1):
        for r in (radius_in, radius_out):
            for i in range(segs + 1):
                t = i / segs
                a = a0 + (a1 - a0) * t
                verts.append((math.cos(a) * r, yy, math.sin(a) * r))
    n = segs + 1
    faces = []
    def ring(ri):
        return ri * n
    for i in range(segs):
        bi, bo, ti, to = ring(0), ring(1), ring(2), ring(3)
        faces.append((bo + i, bo + i + 1, to + i + 1, to + i))
        faces.append((bi + i + 1, bi + i, ti + i, ti + i + 1))
        faces.append((bi + i, bi + i + 1, bo + i + 1, bo + i))
        faces.append((ti + i + 1, ti + i, to + i, to + i + 1))
    i0, i1 = 0, segs
    bi, bo, ti, to = ring(0), ring(1), ring(2), ring(3)
    faces.append((bi + i0, bo + i0, to + i0, ti + i0))
    faces.append((bo + i1, bi + i1, ti + i1, to + i1))
    return new_mesh(name, verts, faces, mat_name)


def join_under(name, objects, keep=False):
    if not objects:
        return None
    if len(objects) == 1:
        objects[0].name = name
        return objects[0]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    objects[0].name = name
    return objects[0]


def export_fbx(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        add_leaf_bones=False,
        mesh_smooth_type="FACE",
        use_tspace=True,
        path_mode="COPY",
        embed_textures=False,
    )
    obj.select_set(False)


A90 = math.pi * 0.5
SEGS = 12
GLASS_T = 0.018
FRAME_T = 0.04
HEIGHT = 3.0
