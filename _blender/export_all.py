"""Headless export: blender --background --python export_all.py"""
import sys
from pathlib import Path

import bpy

def _script_dir():
    if "__file__" in globals():
        return Path(__file__).resolve().parent
    for a in sys.argv:
        if a.replace("\\", "/").endswith("export_all.py"):
            return Path(a).resolve().parent
    return Path(".").resolve()


ROOT = _script_dir()
sys.path.insert(0, str(ROOT))

import common as C
import drum
import leaves
import rotor3

C.setup_scene()
objs = []
objs += drum.build()
objs += leaves.build()
objs += rotor3.build()

models = ROOT.parent / "_unity" / "Assets" / "HSDoors" / "Models"
models.mkdir(parents=True, exist_ok=True)
exported = []
for obj in objs:
    if obj is None:
        continue
    dest = models / (obj.name + ".fbx")
    C.export_fbx(obj, dest)
    exported.append(str(dest))
    print("FBX " + obj.name)

blend = ROOT / "HSDoors.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend))
print("BLEND " + str(blend))
print("EXPORTED " + str(len(exported)))
