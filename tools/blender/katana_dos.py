# Script Blender pour générer le Katana de dos & son Fourreau (Chokuto / Katana shinobi).
# blender.exe --background --factory-startup --python katana_dos.py -- [--no-render]
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nrp_blender as nb

A = nb.args()
nb.reset()

NAME = "katana_dos"
SCRATCH_DIR = nb.SCRATCH
OUT_DIR = f"{nb.PROJECT}/Assets/models/ninja/{NAME}"
os.makedirs(SCRATCH_DIR, exist_ok=True)

# 1. Import de la référence Citizen pour calage sur le dos (spine_2)
arm, meshes = nb.import_ref(nb.REF_MALE)
chest = meshes["human_male_chest"]
spine_bone = arm.data.bones["spine_2"]

# Matériaux du Katana
mat_saya = nb.material("katana_saya", (0.05, 0.05, 0.06), metallic=0.1, rough=0.15)       # Bois laqué noir profond
mat_brass = nb.material("katana_brass", (0.76, 0.63, 0.33), metallic=0.92, rough=0.30)   # Tsuba / ferrures en laiton
mat_ito = nb.material("katana_ito", (0.08, 0.08, 0.09), metallic=0.0, rough=0.85)         # Tressage noir du manche
mat_sageo = nb.material("katana_sageo", (0.75, 0.08, 0.11), metallic=0.0, rough=0.70)     # Cordon rouge vif
mat_strap = nb.material("katana_strap", (0.16, 0.11, 0.08), metallic=0.0, rough=0.75)     # Lanière cuir de maintien

# 2. Dimensions & Géométrie axiale du Katana (longueur totale ~104 cm)
# L'axe principal du sabre est aligné le long de Z en local avant orientation finale
L_SAYA = 0.76     # Fourreau 76 cm (de z = -0.76 à 0.0)
L_TSUBA = 0.005   # Épaisseur garde 5 mm (de z = 0.0 à 0.005)
L_TSUKA = 0.26    # Manche 26 cm (de z = 0.005 à 0.265)
L_TOTAL = L_SAYA + L_TSUBA + L_TSUKA

# 3. Fourreau (Saya) en bois laqué noir
# Section ovale aplatie (3.2 cm large x 1.7 cm épais) avec légère courbure élégante (sori)
bm_saya = bmesh.new()
uv_saya = bm_saya.loops.layers.uv.new("UVMap")
SAYA_SEGS = 16
SAYA_RINGS = 24

saya_rows = []
for j in range(SAYA_RINGS + 1):
    s = j / SAYA_RINGS
    z = - L_SAYA * (1.0 - s) # de -0.76 à 0.0
    
    # Courbure sori discrète (flèche ~1.2 cm au milieu du fourreau)
    curve_x = 0.012 * math.sin(s * math.pi)
    
    # Légère conicité vers le bout inférieur (kojiri)
    scale_w = 0.85 + 0.15 * s
    rx = 0.016 * scale_w
    ry = 0.009 * scale_w
    
    row = []
    for i in range(SAYA_SEGS):
        ang = 2 * math.pi * i / SAYA_SEGS
        # Section ovale
        x = curve_x + math.cos(ang) * rx
        y = math.sin(ang) * ry
        row.append(bm_saya.verts.new((x, y, z)))
    saya_rows.append(row)

for j in range(SAYA_RINGS):
    for i in range(SAYA_SEGS):
        i_next = (i + 1) % SAYA_SEGS
        f = bm_saya.faces.new((saya_rows[j][i], saya_rows[j][i_next],
                               saya_rows[j + 1][i_next], saya_rows[j + 1][i]))
        for l, (vi, vj) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
            l[uv_saya].uv = (vi / SAYA_SEGS, 0.10 + 0.60 * vj / SAYA_RINGS)

# Embout inférieur de protection (Kojiri) en laiton
tip_vert = bm_saya.verts.new((0.0, 0.0, -L_SAYA - 0.006))
for i in range(SAYA_SEGS):
    i_next = (i + 1) % SAYA_SEGS
    bm_saya.faces.new((saya_rows[0][i_next], saya_rows[0][i], tip_vert))

bmesh.ops.recalc_face_normals(bm_saya, faces=bm_saya.faces)
saya_obj = nb.mesh_obj("saya", bm_saya, mat_saya)

# 4. Kojiri & Koiguchi (Ferrures métalliques de tête et de pied du fourreau)
parts = []
# Koiguchi (collerette en laiton à l'entrée du fourreau z = -0.01 à 0.0)
bm_koi = bmesh.new()
bmesh.ops.create_cone(bm_koi, cap_ends=True, segments=16, radius1=0.0175, radius2=0.0175,
                      depth=0.012, matrix=Matrix.Translation((0, 0, -0.006)))
parts.append(nb.mesh_obj("koiguchi", bm_koi, mat_brass))

# Kojiri (embout laiton au bout inférieur z = -0.76)
bm_koj = bmesh.new()
bmesh.ops.create_cone(bm_koj, cap_ends=True, segments=16, radius1=0.014, radius2=0.006,
                      depth=0.025, matrix=Matrix.Translation((0, 0, -L_SAYA + 0.010)))
parts.append(nb.mesh_obj("kojiri", bm_koj, mat_brass))

# Kurigata (passant de cordon saillant sur le flanc du fourreau à z = -0.08)
bm_kuri = bmesh.new()
bmesh.ops.create_cube(bm_kuri, size=1.0, matrix=Matrix.Translation((0.019, 0.0, -0.085)) @ Matrix.Diagonal((0.012, 0.018, 0.038, 1.0)))
parts.append(nb.mesh_obj("kurigata", bm_kuri, mat_saya))

# 5. Cordon Sageo (tressage rouge autour du haut du fourreau)
# 4 tours de cordon serré autour du fourreau sous le kurigata
bm_sag = bmesh.new()
N_SAG_TURNS = 5
for t_idx in range(N_SAG_TURNS):
    z_sag = -0.065 - 0.011 * t_idx
    bmesh.ops.create_circle(bm_sag, cap_ends=False, segments=16, radius=0.018,
                            matrix=Matrix.Translation((0, 0, z_sag)))
bmesh.ops.extrude_edge_only(bm_sag, edges=bm_sag.edges[:])
bmesh.ops.translate(bm_sag, vec=Vector((0, 0, 0.006)), verts=[v for v in bm_sag.verts if len(v.link_faces) == 0])
bmesh.ops.solidify(bm_sag, geom=bm_sag.faces[:], thickness=0.002)
# Nœud décoratif pendant (tassels)
bmesh.ops.create_uvsphere(bm_sag, u_segments=10, v_segments=6, radius=0.009,
                         matrix=Matrix.Translation((0.022, 0.005, -0.12)))
parts.append(nb.mesh_obj("sageo", bm_sag, mat_sageo))

# 6. Garde (Tsuba) en laiton sculpté
# Disque mokko ovale (diamètre ~7.4 cm, épaisseur 4.5 mm)
bm_tsuba = bmesh.new()
uv_tsuba = bm_tsuba.loops.layers.uv.new("UVMap")
TSUBA_SEGS = 24
tsuba_top = []
tsuba_bot = []
for k in range(TSUBA_SEGS):
    ang = 2 * math.pi * k / TSUBA_SEGS
    # Profil polylobé mokko
    lobe = 1.0 + 0.07 * math.cos(ang * 4.0)
    tx = math.cos(ang) * 0.036 * lobe
    ty = math.sin(ang) * 0.031 * lobe
    tsuba_top.append(bm_tsuba.verts.new((tx, ty, L_TSUBA)))
    tsuba_bot.append(bm_tsuba.verts.new((tx, ty, 0.0)))

# Faces extérieures et tranches
for k in range(TSUBA_SEGS):
    k_next = (k + 1) % TSUBA_SEGS
    bm_tsuba.faces.new((tsuba_bot[k], tsuba_bot[k_next], tsuba_top[k_next], tsuba_top[k]))

# Trou intérieur pour la soie de lame
hole_top = []
hole_bot = []
for k in range(TSUBA_SEGS):
    ang = 2 * math.pi * k / TSUBA_SEGS
    hx = math.cos(ang) * 0.014
    hy = math.sin(ang) * 0.007
    hole_top.append(bm_tsuba.verts.new((hx, hy, L_TSUBA)))
    hole_bot.append(bm_tsuba.verts.new((hx, hy, 0.0)))

for k in range(TSUBA_SEGS):
    k_next = (k + 1) % TSUBA_SEGS
    bm_tsuba.faces.new((hole_top[k], hole_top[k_next], tsuba_top[k_next], tsuba_top[k]))
    bm_tsuba.faces.new((tsuba_bot[k], tsuba_bot[k_next], hole_bot[k_next], hole_bot[k]))
    bm_tsuba.faces.new((hole_top[k_next], hole_top[k], hole_bot[k], hole_bot[k_next]))

bmesh.ops.recalc_face_normals(bm_tsuba, faces=bm_tsuba.faces)
parts.append(nb.mesh_obj("tsuba", bm_tsuba, mat_brass))

# 7. Habaki (Bague en laiton poli scellant la lame)
bm_habaki = bmesh.new()
bmesh.ops.create_cone(bm_habaki, cap_ends=True, segments=12, radius1=0.013, radius2=0.015,
                      depth=0.018, matrix=Matrix.Translation((0, 0, L_TSUBA + 0.009)))
parts.append(nb.mesh_obj("habaki", bm_habaki, mat_brass))

# 8. Poignée tressée (Tsuka) avec bourrelets hélicoïdaux (Ito)
bm_tsuka = bmesh.new()
uv_tsuka = bm_tsuka.loops.layers.uv.new("UVMap")
TSUKA_SEGS = 16
TSUKA_RINGS = 28
Z0_TSUKA = L_TSUBA + 0.010
Z1_TSUKA = L_TSUBA + L_TSUKA - 0.015

tsuka_grid = []
for j in range(TSUKA_RINGS + 1):
    frac_z = j / TSUKA_RINGS
    z = Z0_TSUKA + (Z1_TSUKA - Z0_TSUKA) * frac_z
    row = []
    rx = 0.0145
    ry = 0.0095
    for i in range(TSUKA_SEGS):
        ang = 2 * math.pi * i / TSUKA_SEGS
        # Relief de tressage en losange classique japonais (Ito)
        ito_cross = 0.08 * math.cos(ang * 2.0 + frac_z * math.pi * 18.0) * math.cos(ang * 2.0 - frac_z * math.pi * 18.0)
        r_scale = 1.0 + ito_cross
        x = math.cos(ang) * rx * r_scale
        y = math.sin(ang) * ry * r_scale
        row.append(bm_tsuka.verts.new((x, y, z)))
    tsuka_grid.append(row)

for j in range(TSUKA_RINGS):
    for i in range(TSUKA_SEGS):
        i_next = (i + 1) % TSUKA_SEGS
        f = bm_tsuka.faces.new((tsuka_grid[j][i], tsuka_grid[j][i_next],
                                tsuka_grid[j + 1][i_next], tsuka_grid[j + 1][i]))
        for l, (vi, vj) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
            l[uv_tsuka].uv = (vi / TSUKA_SEGS, vj / TSUKA_RINGS)

bmesh.ops.recalc_face_normals(bm_tsuka, faces=bm_tsuka.faces)
parts.append(nb.mesh_obj("tsuka", bm_tsuka, mat_ito))

# 9. Fuchi & Kashira (Bague et Pommeau sculpté en laiton)
# Fuchi (à la base du manche)
bm_fuchi = bmesh.new()
bmesh.ops.create_cone(bm_fuchi, cap_ends=True, segments=16, radius1=0.0155, radius2=0.0155,
                      depth=0.010, matrix=Matrix.Translation((0, 0, Z0_TSUKA - 0.005)))
parts.append(nb.mesh_obj("fuchi", bm_fuchi, mat_brass))

# Kashira (pommeau d'extrémité du manche)
bm_kash = bmesh.new()
bmesh.ops.create_cone(bm_kash, cap_ends=True, segments=16, radius1=0.015, radius2=0.013,
                      depth=0.014, matrix=Matrix.Translation((0, 0, Z1_TSUKA + 0.007)))
parts.append(nb.mesh_obj("kashira", bm_kash, mat_brass))

# 10. Baudrier de dos (Lanière de maintien en cuir reliant le sabre au torse)
bm_strap = bmesh.new()
# Anneau de sangle enveloppant le fourreau à z = -0.15
bmesh.ops.create_cone(bm_strap, cap_ends=False, segments=16, radius1=0.019, radius2=0.019,
                      depth=0.025, matrix=Matrix.Translation((0, 0, -0.15)))
bmesh.ops.solidify(bm_strap, geom=bm_strap.faces[:], thickness=0.002)
parts.append(nb.mesh_obj("strap_ring", bm_strap, mat_strap))

# Assemblage du katana complet droit avant placement sur le dos
katana_mesh = nb.join([saya_obj] + parts, NAME)

# 11. Placement du Katana en diagonale le long du dos (attaché à spine_2)
# Dans le repère Citizen :
# L'épaule droite est à +X, le dos est à +Y, le haut est à +Z.
# La poignée (tsuka) dépasse au-dessus de l'épaule droite (X=+0.14, Y=+0.15, Z=1.52).
# Le bas du fourreau descend vers la hanche gauche (X=-0.14, Y=+0.16, Z=0.88).
# Rotation en diagonale le long du dos :
# Angle d'inclinaison de ~38° autour de Y, et légère inclinaison de ~12° épousant la cambrure dorsale.
mat_align = (Matrix.Translation((0.01, 0.158, 1.24))
             @ Matrix.Rotation(math.radians(38), 4, 'Y')
             @ Matrix.Rotation(math.radians(-10), 4, 'X')
             @ Matrix.Rotation(math.radians(-90), 4, 'Z'))

katana_mesh.matrix_world = mat_align
bpy.context.view_layer.objects.active = katana_mesh
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# 12. Textures procédurales pour le Katana
TEX_RES = 1024
tex_color = np.ones((TEX_RES, TEX_RES, 4), dtype=np.float32)
# Fond noir laqué Kurorishiki profond (#0E0E10)
tex_color[:, :, 0] = 0.05
tex_color[:, :, 1] = 0.05
tex_color[:, :, 2] = 0.06
tex_color[:, :, 3] = 1.0

# Normal map de base
tex_normal = np.zeros((TEX_RES, TEX_RES, 4), dtype=np.float32)
tex_normal[:, :, 0] = 0.5
tex_normal[:, :, 1] = 0.5
tex_normal[:, :, 2] = 1.0
tex_normal[:, :, 3] = 1.0

# Relief du tressage hélicoïdal sur le quart supérieur de la texture (Ito)
Y_grid, X_grid = np.mgrid[0:TEX_RES, 0:TEX_RES]
U = X_grid / (TEX_RES - 1.0)
V = Y_grid / (TEX_RES - 1.0)

# Zone de tressage Ito (losanges croisés)
diamond_wave1 = np.sin((U * 16.0 + V * 32.0) * np.pi)
diamond_wave2 = np.sin((U * 16.0 - V * 32.0) * np.pi)
diamond_relief = np.clip(diamond_wave1 * diamond_wave2 * 0.4, -0.3, 0.3)
tex_normal[:, :, 0] = np.clip(tex_normal[:, :, 0] + diamond_relief, 0.0, 1.0)
tex_normal[:, :, 1] = np.clip(tex_normal[:, :, 1] + diamond_relief, 0.0, 1.0)

tex_c_path = f"{SCRATCH_DIR}/{NAME}_color.png"
tex_n_path = f"{SCRATCH_DIR}/{NAME}_normal.png"
nb.save_png(tex_c_path, tex_color)
nb.save_png(tex_n_path, tex_normal, non_color=True)
print("Textures katana générées :", tex_c_path, tex_n_path)

# 13. Rigging : Pondération 100% sur l'os 'spine_2'
vg = katana_mesh.vertex_groups.new(name="spine_2")
vg.add(range(len(katana_mesh.data.vertices)), 1.0, "REPLACE")
katana_mesh.parent = arm
katana_mesh.matrix_parent_inverse = arm.matrix_world.inverted()
mod_arm = katana_mesh.modifiers.new("Armature", "ARMATURE")
mod_arm.object = arm

# 14. Validation des métriques
tris = nb.tri_count(katana_mesh)
bb = [katana_mesh.matrix_world @ Vector(c) for c in katana_mesh.bound_box]
length = math.sqrt((max(v.x for v in bb) - min(v.x for v in bb))**2 +
                   (max(v.z for v in bb) - min(v.z for v in bb))**2) * 100.0
print(f"TRIS {NAME}: {tris}, Longueur réelle : {length:.1f} cm")
assert tris < 4500, f"Trop de triangles : {tris}"
assert 95.0 <= length <= 115.0, f"Longueur katana hors gabarit : {length:.1f} cm"

fbx_scratch = f"{SCRATCH_DIR}/{NAME}.fbx"
nb.export_fbx(fbx_scratch, [katana_mesh, arm])
print("FBX katana exporté dans scratch :", fbx_scratch)

nb.save_blend(NAME)

if "--no-render" not in A:
    nb.setup_eevee()
    # Vue de dos centrée sur spine_2
    tgt = Vector((0.0, 0.12, 1.25))
    nb.render_view(f"{SCRATCH_DIR}/{NAME}_dos.png", tgt, (0.0, 1.0, 0.12), 1.35)
    nb.render_view(f"{SCRATCH_DIR}/{NAME}_34_dos.png", tgt, (0.75, 0.70, 0.20), 1.40)
    nb.render_view(f"{SCRATCH_DIR}/{NAME}_profil.png", tgt, (1.0, 0.0, 0.05), 1.30)
    print("Rendus de contrôle sauvegardés.")
