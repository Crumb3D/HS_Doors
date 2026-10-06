import math
import common as C


def build():
    # 3-wing rotor. Parent is the hub cell. Wings radiate in XZ, 3 m tall.
    parts = [C.cylinder("hsdoorsRotor3_post", 0.08, 2.96, 16, (0.5, 1.5, 0.5), C.MAT_FRAME)]
    hub_x, hub_z = 0.5, 0.5
    inner, outer, t, h = 0.12, 1.42, 0.036, 2.88
    for i in range(3):
        a = i * (math.tau / 3.0)
        ca, sa = math.cos(a), math.sin(a)
        mid = (inner + outer) * 0.5
        length = outer - inner
        cx = hub_x + ca * mid
        cz = hub_z + sa * mid
        # Thin pane along the wing axis. Local X is radial, Z is thickness.
        glass = C.box("hsdoorsRotor3_g" + str(i), (length, h, t), (cx, 0.06 + h * 0.5, cz), C.MAT_GLASS)
        glass.rotation_euler[1] = -a
        frame_l = C.box("hsdoorsRotor3_f" + str(i), (length, 0.045, 0.05), (cx, 0.08, cz), C.MAT_FRAME)
        frame_l.rotation_euler[1] = -a
        frame_t = C.box("hsdoorsRotor3_t" + str(i), (length, 0.045, 0.05), (cx, 0.06 + h - 0.03, cz), C.MAT_FRAME)
        frame_t.rotation_euler[1] = -a
        parts.extend([glass, frame_l, frame_t])
    # Apply rotation so join bakes it
    bpy = __import__("bpy")
    bpy.context.view_layer.update()
    for p in parts:
        if p.rotation_euler[1] != 0.0:
            bpy.context.view_layer.objects.active = p
            p.select_set(True)
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
            p.select_set(False)
    return [C.join_under("hsdoorsRotor3", parts)]
