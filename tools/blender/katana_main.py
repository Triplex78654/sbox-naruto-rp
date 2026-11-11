# Génère le katana tenu en main (prop statique ~95 cm, origine dans la prise sous la garde, lame vers +Z Blender).
# Le katana porté dans le dos (avec fourreau, skinné sur le corps) est fait par katana_dos.py.
# blender.exe --background --factory-startup --python katana_main.py -- [--no-render]
import math
import os
import sys

import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nrp_blender as nb  # noqa: E402

A = nb.args()
nb.reset()
mat_blade = nb.material("katana_blade", (0.72, 0.74, 0.78), 1.0, 0.25)
mat_brass = nb.material("katana_brass", (0.76, 0.63, 0.33), 0.92, 0.30)
mat_ito = nb.material("katana_ito", (0.08, 0.08, 0.09), 0.0, 0.85)

# La main se referme autour de z = 0 : la garde est juste au-dessus du poing, le reste du manche dépasse dessous.
GRIP_LO, GRIP_HI = -0.19, 0.05
GUARD_T = 0.008
BLADE_LO = GRIP_HI + GUARD_T
BLADE_LEN = 0.68
SORI = 0.022  # flèche de la courbure, vers le dos de la lame (-X)


def curve(z):
    """Décalage en X de la lame à la hauteur z : courbure douce du katana."""
    t = max(0.0, (z - BLADE_LO) / BLADE_LEN)
    return -SORI * t * t


# --- Lame : section en losange aplati, tranchant vers +X, pointe effilée sur les 7 derniers cm ---
bm = bmesh.new()
N = 14
rings = []
for i in range(N + 1):
    t = i / N
    z = BLADE_LO + BLADE_LEN * t
    taper = 1.0 if t < 0.9 else max(0.05, (1.0 - t) / 0.1)
    w, th = 0.015 * (1 - 0.25 * t) * taper, 0.0032 * taper
    cx = curve(z) + (0.015 * (1 - 0.25 * t) - w)  # la pointe remonte vers le dos, pas vers le milieu
    rings.append([bm.verts.new((cx + x, y, z)) for x, y in ((w, 0), (-w * 0.2, th), (-w, 0), (-w * 0.2, -th))])
for a, b in zip(rings, rings[1:]):
    for k in range(4):
        bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]))
bm.faces.new(rings[0][::-1])
bm.faces.new(rings[-1])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.triangulate(bm, faces=bm.faces)
blade = nb.mesh_obj("blade", bm, mat_blade)

# --- Manche ovale tressé ---
SEG, RINGS = 10, 16
bm = bmesh.new()
G = []
for j in range(RINGS + 1):
    z = GRIP_LO + (GRIP_HI - GRIP_LO) * j / RINGS
    row = []
    for i in range(SEG):
        a = 2 * math.pi * i / SEG
        bump = 1 + 0.07 * math.cos(2 * a + j * math.pi)  # losanges du tressage
        row.append(bm.verts.new((0.0125 * bump * math.cos(a), 0.0095 * bump * math.sin(a), z)))
    G.append(row)
for j in range(RINGS):
    for i in range(SEG):
        bm.faces.new((G[j][i], G[j][(i + 1) % SEG], G[j + 1][(i + 1) % SEG], G[j + 1][i]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
grip = nb.mesh_obj("grip", bm, mat_ito)

# --- Garde (tsuba), collier de lame et pommeau en laiton ---
parts = []
for z0, z1, r in ((GRIP_HI, BLADE_LO, 0.034), (BLADE_LO, BLADE_LO + 0.02, 0.0165), (GRIP_LO - 0.012, GRIP_LO, 0.0135)):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=14, radius1=r, radius2=r, depth=z1 - z0,
                          matrix=Matrix.Translation((0, 0, (z0 + z1) / 2)) @ Matrix.Scale(0.72, 4, (0, 1, 0)), calc_uvs=True)
    parts.append(nb.mesh_obj("fitting", bm, mat_brass))

k = nb.join([blade, grip] + parts, "katana_main")
tris = nb.tri_count(k)
bb = [k.matrix_world @ Vector(c) for c in k.bound_box]
length = max(v.z for v in bb) - min(v.z for v in bb)
print(f"TRIS katana_main: {tris}, longueur {length * 100:.1f} cm, origine (0,0,0) dans la prise")
assert tris < 1500 and 0.90 < length < 1.00

out = f"{nb.PROJECT}/Assets/models/ninja/katana_main/katana_main.fbx"
os.makedirs(os.path.dirname(out), exist_ok=True)
nb.export_fbx(out, [k])
print("EXPORT", out)
nb.save_blend("katana_main")

if "--no-render" not in A:
    nb.setup_render()
    nb.render_view(f"{nb.SCRATCH}/katana_main_face.png", (0, 0, 0.28), (0, -1, 0), 1.6)
    nb.render_view(f"{nb.SCRATCH}/katana_main_34.png", (0, 0, 0.28), (0.7, -0.7, 0.2), 1.6)
