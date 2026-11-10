# Génère un kunai (prop statique ~25 cm, origine à la poignée, lame vers +Z Blender).
# blender.exe --background --factory-startup --python kunai.py -- [--no-render]
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nrp_blender as nb  # noqa: E402

A = nb.args()
nb.reset()
mat_metal = nb.material("kunai_metal", (0.30, 0.32, 0.35), 1.0, 0.4)
mat_wrap = nb.material("kunai_wrap", (0.78, 0.72, 0.58), 0.0, 0.9)

# Poignée de z=-0.045 à +0.045 (origine = milieu de la prise).
GRIP_LO, GRIP_HI = -0.045, 0.045

# --- Lame en feuille : sections en losange (demi-largeur w, demi-épaisseur t) ---
prof = [(0.045, 0.006, 0.0035), (0.050, 0.010, 0.004), (0.065, 0.019, 0.005), (0.085, 0.022, 0.005),
        (0.110, 0.019, 0.0045), (0.140, 0.012, 0.0035), (0.170, 0.005, 0.002)]
TIP = 0.165
prof = [(0.045 + (z - 0.045) * 0.8, w, t) for z, w, t in prof]  # lame ~12 cm
bm = bmesh.new()
rings = [[bm.verts.new((x, y, z)) for x, y in ((w, 0), (0, t), (-w, 0), (0, -t))] for z, w, t in prof]
for a, b in zip(rings, rings[1:]):
    for k in range(4):
        bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]))
tip = bm.verts.new((0, 0, TIP))
for k in range(4):
    bm.faces.new((rings[-1][k], rings[-1][(k + 1) % 4], tip))
bm.faces.new(rings[0][::-1])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.triangulate(bm, faces=bm.faces)  # n-gons hors jeu
blade = nb.mesh_obj("blade", bm, mat_metal)
bpy.context.view_layer.objects.active = blade
blade.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project()
bpy.ops.object.mode_set(mode="OBJECT")

# --- Poignée enveloppée : cylindre avec bourrelets en spirale (lecture "bande enroulée") ---
SEG, RINGS, R = 10, 18, 0.0085
bm = bmesh.new()
uv = bm.loops.layers.uv.new()
G = []
for j in range(RINGS + 1):
    z = GRIP_LO + (GRIP_HI - GRIP_LO) * j / RINGS
    row = []
    for i in range(SEG):
        a = 2 * math.pi * i / SEG
        rr = R * (1 + 0.09 * math.cos(a + j * math.pi * 0.85))  # bourrelet qui tourne = enroulement
        row.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), z)))
    G.append(row)
for j in range(RINGS):
    for i in range(SEG):
        f = bm.faces.new((G[j][i], G[j][(i + 1) % SEG], G[j + 1][(i + 1) % SEG], G[j + 1][i]))
        for l, (a, b) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
            l[uv].uv = (a / SEG, b / RINGS)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
grip = nb.mesh_obj("grip", bm, mat_wrap)

# --- Collerette (garde) + col sous la poignée + anneau ---
parts = []
for z0, z1, r0 in ((GRIP_HI - 0.002, GRIP_HI + 0.006, 0.0095), (GRIP_LO - 0.012, GRIP_LO + 0.002, 0.0065)):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=r0, radius2=r0 * 0.85, depth=z1 - z0,
                          matrix=Matrix.Translation((0, 0, (z0 + z1) / 2)), calc_uvs=True)
    parts.append(nb.mesh_obj("collar", bm, mat_metal))
RING_R, RING_T = 0.016, 0.0035
ring_cz = GRIP_LO - 0.012 - RING_R + RING_T * 0.5
bpy.ops.mesh.primitive_torus_add(major_radius=RING_R, minor_radius=RING_T, major_segments=18, minor_segments=6,
                                 location=(0, 0, ring_cz), rotation=(math.pi / 2, 0, 0))
ring = bpy.context.active_object
ring.data.materials.append(mat_metal)

k = nb.join([blade, grip, ring] + parts, "kunai")
tris = nb.tri_count(k)
bb = [k.matrix_world @ Vector(c) for c in k.bound_box]
length = max(v.z for v in bb) - min(v.z for v in bb)
print(f"TRIS kunai: {tris}, longueur {length*100:.1f} cm, origine (0,0,0) au centre de la poignée")
assert tris < 1500 and 0.23 < length < 0.27

out = f"{nb.PROJECT}/Assets/models/ninja/kunai/kunai.fbx"
nb.export_fbx(out, [k])
print("EXPORT", out)
nb.save_blend("kunai")

if "--no-render" not in A:
    nb.setup_render()
    nb.render_view(f"{nb.SCRATCH}/kunai_front.png", (0, 0, 0.037), (0, -1, 0), 0.8)
    nb.render_view(f"{nb.SCRATCH}/kunai_34.png", (0, 0, 0.037), (0.7, -0.7, 0.35), 0.8)
    # À l'échelle à côté de la main gauche de la référence
    arm, meshes = nb.import_ref(nb.REF_MALE)
    hand = arm.matrix_world @ arm.data.bones["hand_L"].head_local
    k.location = hand + Vector((0.13, -0.03, -0.02))
    nb.render_view(f"{nb.SCRATCH}/kunai_hand.png", hand + Vector((0.08, 0, 0.0)), (0.15, -1, 0.1), 0.85)
