# Script Blender pour générer le Gilet de Chûnin tactique (Konoha flak jacket).
# blender.exe --background --factory-startup --python gilet_chunin.py -- [--no-render]
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

NAME = "gilet_chunin"
SCRATCH_DIR = nb.SCRATCH
OUT_DIR = f"{nb.PROJECT}/Assets/models/ninja/{NAME}"
os.makedirs(SCRATCH_DIR, exist_ok=True)

# 1. Import de la référence Citizen
arm, meshes = nb.import_ref(nb.REF_MALE)
chest = meshes["human_male_chest"]
tree = nb.world_bvh(chest)

# 2. Dimensions & Paramètres morphologiques du torse
# Z_WAIST ~ 0.98 m, Z_CHEST ~ 1.25 m, Z_SHOULDER ~ 1.44 m, Z_COLLAR_TOP ~ 1.51 m
Z_WAIST = 0.99
Z_COLLAR_BASE = 1.44
Z_COLLAR_TOP = 1.51

# 3. Construction du corps du gilet (Veste sans manches ajustée)
bm = bmesh.new()
uv_layer = bm.loops.layers.uv.new("UVMap")

COLS = 32
ROWS = 20

# Rayon radial autour de l'axe de la colonne vertébrale
def get_chest_radius(theta, z):
    """Calcule la distance de la peau depuis l'axe du torse selon l'angle theta."""
    # Centre approximatif de la colonne à la hauteur z
    center_y = 0.01 + 0.015 * ((z - Z_WAIST) / (Z_COLLAR_BASE - Z_WAIST))
    c = Vector((0.0, center_y, z))
    # Direction horizontale (theta=0 devant, pi/2 gauche, pi arrière, -pi/2 droite)
    # Dans Blender : -Y = avant, +X = droite, +Y = arrière, -X = gauche
    d = Vector((-math.sin(theta), -math.cos(theta), 0.0))
    origin = c + d * 0.50
    hit, _, _, _ = tree.ray_cast(origin, -d, 0.55)
    if hit is not None:
        return (hit - c).length
    # Rayon de secours elliptique si pas d'impact (sous les aisselles)
    rx = 0.16 + 0.02 * math.sin((z - Z_WAIST) / 0.45 * math.pi)
    ry = 0.12 + 0.01 * math.sin((z - Z_WAIST) / 0.45 * math.pi)
    return 1.0 / math.sqrt((math.sin(theta)/rx)**2 + (math.cos(theta)/ry)**2)

# Grille de sommets du torse
grid_verts = []
for j in range(ROWS):
    v_norm = j / (ROWS - 1)
    z = Z_WAIST + (Z_COLLAR_BASE - Z_WAIST) * v_norm
    row = []
    cy = 0.01 + 0.015 * v_norm
    
    for i in range(COLS):
        theta = 2 * math.pi * i / COLS
        r_skin = get_chest_radius(theta, z)
        # Épaisseur du gilet et rembourrage tactique : +7 mm de jeu
        r_vest = r_skin + 0.0075
        
        # Découpe des emmanchures (sans manches) sur les côtés (theta ~ +-90 deg)
        # Entre z = 1.22 et 1.42
        armhole = False
        angle_side = abs((theta + math.pi) % (2 * math.pi) - math.pi) # distance à l'arrière
        # côtés = theta ~ +- pi/2
        is_side = abs(abs(theta) - math.pi / 2) < math.radians(26)
        if 1.24 < z < 1.43 and is_side:
            armhole = True
            
        dx = -math.sin(theta) * r_vest
        dy = -math.cos(theta) * r_vest
        
        # Épaulettes intégrées aux épaules
        if z > 1.38 and abs(dx) > 0.12:
            dy += 0.006 # élargissement
            z += 0.004
            
        v = bm.verts.new((dx, cy + dy, z))
        row.append((v, armhole))
    grid_verts.append(row)

# Faces du torse (en sautant les emmanchures pour créer les manches ouvertes)
for j in range(ROWS - 1):
    for i in range(COLS):
        i_next = (i + 1) % COLS
        v00, a00 = grid_verts[j][i]
        v10, a10 = grid_verts[j][i_next]
        v11, a11 = grid_verts[j + 1][i_next]
        v01, a01 = grid_verts[j + 1][i]
        
        if a00 and a10 and a11 and a01:
            continue # trou de manche
            
        f = bm.faces.new((v00, v10, v11, v01))
        # Dépliage UV cylindrique continu
        for l, (vi, vj) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
            l[uv_layer].uv = (vi / COLS, vj / (ROWS - 1) * 0.70)

# 4. Col haut rembourré (High Padded Standing Collar)
COLLAR_SEGS = 24
COLLAR_RINGS = 6
collar_rows = []
for j in range(COLLAR_RINGS):
    frac_z = j / (COLLAR_RINGS - 1)
    cz = Z_COLLAR_BASE + (Z_COLLAR_TOP - Z_COLLAR_BASE) * frac_z
    # Profil de bourrelet horizontal matelassé (ondulation de rayon)
    quilt_bulge = 0.003 * math.sin(frac_z * math.pi * 3.0)
    
    row = []
    # Le col est ouvert à la gorge devant (-30° à +30°)
    for i in range(COLLAR_SEGS + 1):
        frac_a = i / COLLAR_SEGS
        # Arc de 35° à 325° (entrouvert devant sur la gorge)
        ang = math.radians(35 + 290 * frac_a)
        # Rayon du col autour du cou
        rcx = 0.092 + 0.008 * frac_z + quilt_bulge
        rcy = 0.098 + 0.008 * frac_z + quilt_bulge
        cx = -math.sin(ang) * rcx
        cy = 0.015 - math.cos(ang) * rcy
        row.append(bm.verts.new((cx, cy, cz)))
    collar_rows.append(row)

for j in range(COLLAR_RINGS - 1):
    for i in range(COLLAR_SEGS):
        f = bm.faces.new((collar_rows[j][i], collar_rows[j][i + 1],
                          collar_rows[j + 1][i + 1], collar_rows[j + 1][i]))
        for l, (vi, vj) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
            l[uv_layer].uv = (0.75 + 0.23 * vi / COLLAR_SEGS, 0.72 + 0.26 * vj / (COLLAR_RINGS - 1))

bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
# Solidify pour le tissu rembourré (4 mm)
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.004)

mat_vest = nb.material("gilet_chunin", (0.22, 0.29, 0.19), metallic=0.0, rough=0.75)
vest_body = nb.mesh_obj("vest_body", bm, mat_vest)

# 5. Épaulettes rembourrées avec boutons pression
epaulettes = []
mat_button = nb.material("chunin_stud", (0.32, 0.32, 0.34), metallic=0.9, rough=0.35)
for side in (-1, 1):
    # Bande de l'épaulette
    bm_ep = bmesh.new()
    uv_ep = bm_ep.loops.layers.uv.new("UVMap")
    pts_ep = []
    # De la base du col (x=0.09) à l'extrémité de l'épaule (x=0.20)
    for k in range(8):
        s = k / 7.0
        ex = side * (0.095 + 0.115 * s)
        ez = 1.445 - 0.025 * (s ** 1.5)
        ey = 0.015 - 0.008 * s
        pts_ep.append(Vector((ex, ey, ez)))
        
    ep_obj = nb.ribbon(f"epaulette_{side}", pts_ep, [Vector((0, 1, 0))] * 8, [0.048] * 8, 0.004, mat_vest)
    epaulettes.append(ep_obj)
    
    # Bouton pression métallique au bout de chaque épaulette
    bm_btn = bmesh.new()
    btn_pos = pts_ep[-2] + Vector((0, 0, 0.003))
    bmesh.ops.create_uvsphere(bm_btn, u_segments=10, v_segments=6, radius=0.0055,
                             matrix=Matrix.Translation(btn_pos) @ Matrix.Diagonal((1, 1, 0.45, 1)))
    epaulettes.append(nb.mesh_obj(f"snap_btn_{side}", bm_btn, mat_button))

# 6. Poches à rouleaux tactiques sur la poitrine (Scroll pouches)
pouches = []
# 3 cylindres à gauche, 3 cylindres à droite sur le torse
for side in (-1, 1):
    for p_idx in range(3):
        bm_p = bmesh.new()
        uv_p = bm_p.loops.layers.uv.new("UVMap")
        
        # Positionnement échelonné sur la poitrine
        px = side * (0.042 + 0.038 * p_idx)
        pz = 1.28 + 0.012 * p_idx
        # Profondeur devant le gilet
        py = - (get_chest_radius(math.atan2(abs(px), 1.0), pz) + 0.014)
        
        # Cylindre de la poche à rouleau (diamètre ~2.6 cm, hauteur ~8.5 cm)
        p_mat = Matrix.Translation((px, py, pz)) @ Matrix.Rotation(math.radians(side * -10), 4, 'Y')
        bmesh.ops.create_cone(bm_p, cap_ends=True, segments=12, radius1=0.013, radius2=0.013,
                              depth=0.082, matrix=p_mat)
                              
        # Rabat supérieur de la poche
        bmesh.ops.create_cone(bm_p, cap_ends=True, segments=12, radius1=0.0145, radius2=0.012,
                              depth=0.018, matrix=p_mat @ Matrix.Translation((0, 0, 0.045)))
                              
        p_obj = nb.mesh_obj(f"scroll_pouch_{side}_{p_idx}", bm_p, mat_vest)
        pouches.append(p_obj)

# 7. Poches utilitaires basses (Cargo flap pockets)
for side in (-1, 1):
    bm_c = bmesh.new()
    cx = side * 0.088
    cz = 1.07
    cy = - (get_chest_radius(0.35, cz) + 0.012)
    bmat = Matrix.Translation((cx, cy, cz)) @ Matrix.Rotation(math.radians(side * -8), 4, 'Z')
    bmesh.ops.create_cube(bm_c, size=1.0, matrix=bmat @ Matrix.Diagonal((0.065, 0.024, 0.075, 1.0)))
    pouches.append(nb.mesh_obj(f"cargo_pouch_{side}", bm_c, mat_vest))

# Assemblage du gilet
chunin_full = nb.join([vest_body] + epaulettes + pouches, NAME)

# 8. Génération de la texture procédurale (Camouflage / Tissu militaire + Spirale Uzumaki dans le dos)
TEX_W, TEX_H = 1024, 1024
tex_color = np.ones((TEX_H, TEX_W, 4), dtype=np.float32)
# Vert militaire shinobi (#34472C)
tex_color[:, :, 0] = 0.21
tex_color[:, :, 1] = 0.28
tex_color[:, :, 2] = 0.18
tex_color[:, :, 3] = 1.0

# Normal map de base
tex_normal = np.zeros((TEX_H, TEX_W, 4), dtype=np.float32)
tex_normal[:, :, 0] = 0.5
tex_normal[:, :, 1] = 0.5
tex_normal[:, :, 2] = 1.0
tex_normal[:, :, 3] = 1.0

# Tracé de la Spirale Uzumaki rouge dans le dos
# Dans le dépliage UV cylindrique : le dos se trouve au centre horizontal (U ~ 0.5), Z ~ 0.45 à 0.60
Y_grid, X_grid = np.mgrid[0:TEX_H, 0:TEX_W]
U = X_grid / (TEX_W - 1.0)
V = Y_grid / (TEX_H - 1.0)

# Centre Uzumaki dans l'espace UV
uc_u, uc_v = 0.50, 0.48
# Rayon max de l'emblème spiralé
dist_center = np.hypot(U - uc_u, (V - uc_v) * (TEX_H / TEX_W))
angle_uz = np.arctan2(V - uc_v, U - uc_u)

# Spirale rouge d'Archimède classique de Konoha
turns = 2.5
spiral_phase = (angle_uz / (2 * np.pi) * turns) % 1.0
spiral_mask = (dist_center < 0.11) & (dist_center > 0.02)
# Largeur du trait de la spirale
line_dist = np.abs((dist_center / 0.11 * turns) % 1.0 - 0.5)
uzumaki_alpha = np.clip((0.25 - line_dist) / 0.06, 0.0, 1.0) * spiral_mask

# Application de la couleur rouge Uzumaki (#BA161E)
RED_RGB = (0.75, 0.08, 0.11)
for c in range(3):
    tex_color[:, :, c] = tex_color[:, :, c] * (1.0 - uzumaki_alpha) + RED_RGB[c] * uzumaki_alpha

# Fermeture éclair / couture centrale sur le devant (U=0 et U=1)
front_zipper = np.clip(1.0 - np.minimum(U, 1.0 - U) / 0.015, 0.0, 1.0)
for c in range(3):
    tex_color[:, :, c] = tex_color[:, :, c] * (1.0 - front_zipper * 0.45) + 0.10 * (front_zipper * 0.45)

# Normal map : côtes matelassées horizontales
quilt_pattern = np.sin(V * 48.0 * np.pi) * 0.08
tex_normal[:, :, 1] = np.clip(tex_normal[:, :, 1] + quilt_pattern, 0.0, 1.0)

tex_c_path = f"{SCRATCH_DIR}/{NAME}_color.png"
tex_n_path = f"{SCRATCH_DIR}/{NAME}_normal.png"
nb.save_png(tex_c_path, tex_color)
nb.save_png(tex_n_path, tex_normal, non_color=True)
print("Textures gilet générées :", tex_c_path, tex_n_path)

# 9. Rigging : Pondération sur les os du torse (spine_0, spine_1, spine_2, clavicles, neck)
for bname in ["spine_0", "spine_1", "spine_2", "neck_0", "clavicle_L", "clavicle_R"]:
    chunin_full.vertex_groups.new(name=bname)

# Pondération selon la hauteur Z des sommets
for v in chunin_full.data.vertices:
    z = v.co.z
    x = v.co.x
    if z < 1.08:
        chunin_full.vertex_groups["spine_0"].add([v.index], 1.0, "REPLACE")
    elif z < 1.22:
        f = (z - 1.08) / 0.14
        chunin_full.vertex_groups["spine_0"].add([v.index], 1.0 - f, "REPLACE")
        chunin_full.vertex_groups["spine_1"].add([v.index], f, "REPLACE")
    elif z < 1.36:
        f = (z - 1.22) / 0.14
        chunin_full.vertex_groups["spine_1"].add([v.index], 1.0 - f, "REPLACE")
        chunin_full.vertex_groups["spine_2"].add([v.index], f, "REPLACE")
    elif z < 1.45:
        # Épaules / haut poitrine
        if abs(x) > 0.12:
            cgroup = "clavicle_R" if x > 0 else "clavicle_L"
            chunin_full.vertex_groups[cgroup].add([v.index], 0.70, "REPLACE")
            chunin_full.vertex_groups["spine_2"].add([v.index], 0.30, "REPLACE")
        else:
            chunin_full.vertex_groups["spine_2"].add([v.index], 1.0, "REPLACE")
    else:
        # Col montant
        chunin_full.vertex_groups["neck_0"].add([v.index], 0.85, "REPLACE")
        chunin_full.vertex_groups["spine_2"].add([v.index], 0.15, "REPLACE")

chunin_full.parent = arm
chunin_full.matrix_parent_inverse = arm.matrix_world.inverted()
mod_arm = chunin_full.modifiers.new("Armature", "ARMATURE")
mod_arm.object = arm

# 10. Métriques & Export
tris = nb.tri_count(chunin_full)
bb = [chunin_full.matrix_world @ Vector(c) for c in chunin_full.bound_box]
h_cm = (max(v.z for v in bb) - min(v.z for v in bb)) * 100.0
w_cm = (max(v.x for v in bb) - min(v.x for v in bb)) * 100.0
print(f"TRIS {NAME}: {tris}, Hauteur: {h_cm:.1f} cm, Largeur: {w_cm:.1f} cm")
assert tris < 5000, f"Trop de triangles : {tris}"
assert 42.0 <= h_cm <= 56.0, f"Hauteur gilet hors gabarit : {h_cm:.1f} cm"

fbx_scratch = f"{SCRATCH_DIR}/{NAME}.fbx"
nb.export_fbx(fbx_scratch, [chunin_full, arm])
print("FBX gilet exporté dans scratch :", fbx_scratch)

nb.save_blend(NAME)

if "--no-render" not in A:
    nb.setup_eevee()
    tgt = Vector((0, 0, 1.25))
    nb.render_view(f"{SCRATCH_DIR}/{NAME}_face.png", tgt, (0, -1, 0.10), 1.25)
    nb.render_view(f"{SCRATCH_DIR}/{NAME}_dos.png", tgt, (0, 1, 0.10), 1.25)
    nb.render_view(f"{SCRATCH_DIR}/{NAME}_34.png", tgt, (0.75, -0.75, 0.25), 1.35)
    print("Rendus de contrôle sauvegardés.")
