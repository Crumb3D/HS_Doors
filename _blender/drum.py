import common as C


def _framed_arc(name, radius, height, a0, a1, segs):
    glass = C.arc_shell(name + "_g", radius, C.GLASS_T, height, a0, a1, segs, 0.0, C.MAT_GLASS)
    # Mullions: vertical frame at both ends + one mid, plus top/bottom rails as thin arcs.
    r = radius
    parts = [glass]
    for t in (0.0, 0.5, 1.0):
        a = a0 + (a1 - a0) * t
        x = (C.FRAME_T * 0.5 + 0.002) * 0 + 0
        cx = (r) * __import__("math").cos(a)
        cz = (r) * __import__("math").sin(a)
        parts.append(C.box(name + "_mull" + str(int(t * 10)), (C.FRAME_T, height, C.FRAME_T), (cx, height * 0.5, cz), C.MAT_FRAME))
    parts.append(C.arc_shell(name + "_railB", radius, C.FRAME_T, 0.06, a0, a1, segs, 0.0, C.MAT_FRAME))
    parts.append(C.arc_shell(name + "_railT", radius, C.FRAME_T, 0.06, a0, a1, segs, height - 0.06, C.MAT_FRAME))
    return C.join_under(name, parts)


def build():
    a0, a1 = 0.0, C.A90
    segs = C.SEGS
    out = []
    # R1.5 drum lives in a 2x3x2 AABB; glass at r=1.48 so it sits inside the 2m footprint.
    out.append(C.arc_shell("hsdoorsDrumGlassR15", 1.48, C.GLASS_T, C.HEIGHT, a0, a1, segs, 0.0, C.MAT_GLASS))
    out.append(_framed_arc("hsdoorsDrumGlassR15Framed", 1.48, C.HEIGHT, a0, a1, segs))
    out.append(C.arc_slab("hsdoorsDrumCanopyR15", 0.12, 1.55, 0.12, a0, a1, segs, C.HEIGHT - 0.12, C.MAT_FRAME))
    out.append(C.arc_slab("hsdoorsDrumFloorR15", 0.12, 1.52, 0.04, a0, a1, segs, 0.0, C.MAT_MAT))
    out.append(C.arc_shell("hsdoorsDrumRingR15", 1.50, 0.08, 0.10, a0, a1, segs, C.HEIGHT - 0.10, C.MAT_FRAME))
    # R2 drum in a 3x3x3 AABB; glass at r=1.98.
    out.append(C.arc_shell("hsdoorsDrumGlassR2", 1.98, C.GLASS_T, C.HEIGHT, a0, a1, segs, 0.0, C.MAT_GLASS))
    out.append(_framed_arc("hsdoorsDrumGlassR2Framed", 1.98, C.HEIGHT, a0, a1, segs))
    out.append(C.arc_slab("hsdoorsDrumCanopyR2", 0.12, 2.08, 0.12, a0, a1, segs, C.HEIGHT - 0.12, C.MAT_FRAME))
    out.append(C.arc_slab("hsdoorsDrumFloorR2", 0.12, 2.02, 0.04, a0, a1, segs, 0.0, C.MAT_MAT))
    out.append(C.arc_shell("hsdoorsDrumRingR2", 2.02, 0.08, 0.10, a0, a1, segs, C.HEIGHT - 0.10, C.MAT_FRAME))
    return out
