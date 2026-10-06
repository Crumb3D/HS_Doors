import common as C


def _leaf(name, framed):
    # 1x3 cell. Thin pane in Z, origin at parent min corner. Pane sits in the middle of the cell (Z=0.5).
    w, h, t = 0.92, 2.88, 0.036
    cx, cy, cz = 0.5, 0.06 + h * 0.5, 0.5
    parts = [C.box(name + "_g", (w, h, t), (cx, cy, cz), C.MAT_GLASS)]
    if framed:
        f = 0.045
        parts.append(C.box(name + "_stL", (f, h, f + 0.01), (cx - w * 0.5 + f * 0.5, cy, cz), C.MAT_FRAME))
        parts.append(C.box(name + "_stR", (f, h, f + 0.01), (cx + w * 0.5 - f * 0.5, cy, cz), C.MAT_FRAME))
        parts.append(C.box(name + "_railB", (w, f, f + 0.01), (cx, 0.06 + f * 0.5, cz), C.MAT_FRAME))
        parts.append(C.box(name + "_railT", (w, f, f + 0.01), (cx, 0.06 + h - f * 0.5, cz), C.MAT_FRAME))
        parts.append(C.box(name + "_mid", (w, f * 0.7, t + 0.01), (cx, 0.06 + h * 0.5, cz), C.MAT_FRAME))
    return C.join_under(name, parts)


def build():
    out = []
    out.append(_leaf("hsdoorsLeaf", True))
    out.append(_leaf("hsdoorsLeafFrameless", False))
    # 1x3 centre post
    out.append(C.cylinder("hsdoorsPost", 0.09, 2.96, 16, (0.5, 1.5, 0.5), C.MAT_FRAME))
    # Sliding operator header: 2x1 spanning grey box, like the photo. Parent is left cell.
    header = C.box("hsdoorsHeader_body", (1.96, 0.22, 0.28), (1.0, 0.11, 0.5), C.MAT_FRAME)
    motor = C.box("hsdoorsHeader_motor", (0.28, 0.22, 0.22), (0.18, 0.11, 0.72), C.MAT_FRAME)
    out.append(C.join_under("hsdoorsHeader", [header, motor]))
    # Motion sensor puck
    body = C.box("hsdoorsSensor_b", (0.22, 0.08, 0.12), (0.5, 0.22, 0.5), C.MAT_RUBBER)
    lens = C.box("hsdoorsSensor_l", (0.14, 0.04, 0.04), (0.5, 0.18, 0.56), C.MAT_GLASS)
    out.append(C.join_under("hsdoorsSensor", [body, lens]))
    return out
