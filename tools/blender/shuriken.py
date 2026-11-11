# Génère un shuriken traditionnel à 4 branches (hira-shuriken, ~10 cm, trou central, acier trempé).
# blender.exe --background --factory-startup --python shuriken.py -- [--no-render]
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nrp_blender as nb

A = nb.args()
nb.reset()

mat_metal = nb.material("shuriken_metal", (0.24, 0.25, 0.28), 0.95, 0.35)

bm = bmesh.new()

# Dimensions en mètres (diamètre 10 cm)
R_TIP = 0.052    # Pointe des 4 branches
R_BASE = 0.018   # Creux entre les branches
R_HOLE = 0.009   # Rayon du trou central
THICK_CENTER = 0.0035  # Demi-épaisseur au centre (7 mm total)
THICK_EDGE = 0.0006    # Tranchant biseauté (1.2 mm total)

# 8 sommets du profil étoilé (4 pointes à 0, 90, 180, 270 deg et 4 creux à 45, 135, 225, 315 deg)
top_verts = []
bot_verts = []
for k in range(8):
    ang = math.radians(k * 45)
    r = R_TIP if k % 2 == 0 else R_BASE
    t = THICK_EDGE if k % 2 == 0 else (THICK_CENTER * 0.7)
    x = math.cos(ang) * r
    y = math.sin(ang) * r
    top_verts.append(bm.verts.new((x, y, t)))
    bot_verts.append(bm.verts.new((x, y, -t)))

# 8 sommets de l'arête centrale supérieure et inférieure (anneau de renfort autour du trou)
hole_top = []
hole_bot = []
for k in range(8):
    ang = math.radians(k * 45)
    x = math.cos(ang) * R_HOLE
    y = math.sin(ang) * R_HOLE
    hole_top.append(bm.verts.new((x, y, THICK_CENTER)))
    hole_bot.append(bm.verts.new((x, y, -THICK_CENTER)))

# Faces du dessus (de l'anneau central vers les pointes/creux)
for k in range(8):
    k_next = (k + 1) % 8
    bm.faces.new((hole_top[k], hole_top[k_next], top_verts[k_next], top_verts[k]))

# Faces du dessous
for k in range(8):
    k_next = (k + 1) % 8
    bm.faces.new((hole_bot[k_next], hole_bot[k], bot_verts[k], bot_verts[k_next]))

# Tranchant latéral extérieur (bords biseautés des branches)
for k in range(8):
    k_next = (k + 1) % 8
    bm.faces.new((top_verts[k], top_verts[k_next], bot_verts[k_next], bot_verts[k]))

# Paroi intérieure cylindrique du trou central
for k in range(8):
    k_next = (k + 1) % 8
    bm.faces.new((hole_top[k_next], hole_top[k], hole_bot[k], hole_bot[k_next]))

bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.triangulate(bm, faces=bm.faces)

shuriken = nb.mesh_obj("shuriken", bm, mat_metal)
bpy.context.view_layer.objects.active = shuriken
shuriken.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project()
bpy.ops.object.mode_set(mode="OBJECT")

tris = nb.tri_count(shuriken)
bb = [shuriken.matrix_world @ Vector(c) for c in shuriken.bound_box]
diam = max(v.x for v in bb) - min(v.x for v in bb)
print(f"TRIS shuriken: {tris}, diamètre {diam*100:.1f} cm, centré en (0,0,0)")

out_fbx = nb.SCRATCH + "/shuriken.fbx"
nb.export_fbx(out_fbx, [shuriken])
print("EXPORT FBX:", out_fbx)
