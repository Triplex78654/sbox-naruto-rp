# Script Blender pour générer le Masque ANBU kitsune en porcelaine.
# blender.exe --background --factory-startup --python masque_anbu.py -- [--no-render]
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

NAME = "masque_anbu"
SCRATCH_DIR = nb.SCRATCH
OUT_DIR = f"{nb.PROJECT}/Assets/models/ninja/{NAME}"
os.makedirs(SCRATCH_DIR, exist_ok=True)

# 1. Importation du modèle Citizen male de référence pour ajustement morphologique parfait
arm, meshes = nb.import_ref(nb.REF_MALE)
head = meshes["human_male_head"]
tree = nb.world_bvh(head)

# 2. Dimensions & Paramètres du Masque Kitsune
# Hauteur totale visage ~20 cm (du menton z=1.475 au front z=1.675, oreilles jusqu'à 1.725)
Z_CHIN = 1.475
Z_BROW = 1.640
Z_FOREHEAD = 1.675
Z_EAR_TIP = 1.720

Z_EYE_C = 1.633     # Hauteur centre des yeux
X_EYE_C = 0.036     # Écartement centre œil

U_COLS = 30         # Résolution horizontale
V_ROWS = 24         # Résolution verticale

def get_face_depth(x, z):
    """Raycast vers +Y pour trouver la surface exacte de la peau du Citizen."""
    orig = Vector((x, -0.40, z))
    hit, nrm, _, _ = tree.ray_cast(orig, Vector((0, 1, 0)))
    if hit is not None:
        return hit.y
    # Secours si le rayon passe à côté (menton bas ou oreilles)
    return -0.09 - 0.04 * math.sqrt(max(0.0, 1.0 - (x / 0.08) ** 2))

# Construction du maillage du masque
bm = bmesh.new()
uv_layer = bm.loops.layers.uv.new("UVMap")

verts_grid = []
mask_faces = []

# Génération des sommets du masque épousant le visage
for j in range(V_ROWS):
    v_norm = j / (V_ROWS - 1)
    z = Z_CHIN + (Z_FOREHEAD - Z_CHIN) * v_norm
    
    # Largeur du masque à cette hauteur
    if v_norm < 0.25: # menton effilé
        half_w = 0.035 + 0.040 * (v_norm / 0.25)
    elif v_norm < 0.75: # pommettes larges
        half_w = 0.075 + 0.005 * math.sin((v_norm - 0.25) / 0.5 * math.pi)
    else: # front légèrement resserré
        half_w = 0.078 - 0.010 * ((v_norm - 0.75) / 0.25)
        
    row = []
    for i in range(U_COLS):
        u_norm = i / (U_COLS - 1)
        x = -half_w + 2 * half_w * u_norm
        
        # Profondeur de base sur le visage
        # Rayon vers la peau à cette position
        y_skin = get_face_depth(x, z)
        
        # Jeu constant de 3.5 mm pour flotter proprement sur le visage
        y_base = y_skin - 0.0035
        
        # Museau kitsune effilé vers l'avant au centre (arête du renard)
        snout_factor = math.exp(- (x / 0.024) ** 2)
        # Profil longitudinal du museau
        snout_z = math.exp(- ((z - 1.585) / 0.055) ** 2)
        muzzle_bulge = 0.018 * snout_factor * snout_z
        
        # Courbure des pommettes kitsune (pommettes légèrement rehaussées)
        cheek_x = math.exp(- ((abs(x) - 0.045) / 0.022) ** 2)
        cheek_z = math.exp(- ((z - 1.605) / 0.045) ** 2)
        cheek_bulge = 0.005 * cheek_x * cheek_z
        
        # Oreilles de renard pointues intégrées sur les côtés supérieurs
        ear_bulge_z = 0.0
        ear_bulge_y = 0.0
        if v_norm > 0.88:
            for side in (-1, 1):
                ear_center_x = side * 0.054
                dist_ear = abs(x - ear_center_x)
                if dist_ear < 0.025:
                    ear_shape = math.cos(dist_ear / 0.025 * math.pi * 0.5)
                    ear_h_factor = (v_norm - 0.88) / 0.12
                    ear_bulge_z += (Z_EAR_TIP - Z_FOREHEAD) * ear_h_factor * ear_shape
                    ear_bulge_y -= 0.008 * ear_h_factor * ear_shape # s'incline légèrement en avant
        
        y_final = y_base - muzzle_bulge - cheek_bulge + ear_bulge_y
        z_final = z + ear_bulge_z
        
        v = bm.verts.new((x, y_final, z_final))
        row.append(v)
    verts_grid.append(row)

# Création des faces avec découpe des fentes oculaires (fentes renard en amande inclinées)
def is_in_eye_slit(x, z):
    """Détecte si un point tombe dans les fentes oculaires caractéristiques ANBU."""
    for side in (-1, 1):
        cx = side * X_EYE_C
        cz = Z_EYE_C
        # Rotation de la fente (~15 degrés montant vers les tempes)
        angle = side * math.radians(-16)
        dx = x - cx
        dz = z - cz
        rx = dx * math.cos(angle) - dz * math.sin(angle)
        rz = dx * math.sin(angle) + dz * math.cos(angle)
        # Fente en amande allongée : demi-longueur 1.8 cm, demi-hauteur 0.35 cm
        if (rx / 0.019) ** 2 + (rz / 0.0042) ** 2 <= 1.0:
            return True
    return False

for j in range(V_ROWS - 1):
    for i in range(U_COLS - 1):
        v00 = verts_grid[j][i]
        v10 = verts_grid[j][i + 1]
        v11 = verts_grid[j + 1][i + 1]
        v01 = verts_grid[j + 1][i]
        
        # Centre de la face
        cx = (v00.co.x + v10.co.x + v11.co.x + v01.co.x) * 0.25
        cz = (v00.co.z + v10.co.z + v11.co.z + v01.co.z) * 0.25
        
        # Si la face est dans l'orbite oculaire, on laisse un trou net
        if is_in_eye_slit(cx, cz):
            continue
            
        f = bm.faces.new((v00, v10, v11, v01))
        mask_faces.append(f)
        
        # Mapping UV frontal étendu [0, 1] x [0, 1]
        for l, v in zip(f.loops, (v00, v10, v11, v01)):
            u_coord = (v.co.x + 0.085) / 0.170
            v_coord = (v.co.z - Z_CHIN) / (Z_EAR_TIP - Z_CHIN + 0.01)
            l[uv_layer].uv = (np.clip(u_coord, 0.0, 1.0), np.clip(v_coord, 0.0, 1.0))

bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

# Solidify pour donner l'épaisseur de porcelaine (2.2 mm vers l'intérieur)
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.0022)

mat_anbu = nb.material("masque_anbu", (0.95, 0.95, 0.93), metallic=0.04, rough=0.18)
mask_obj = nb.mesh_obj("masque_shell", bm, mat_anbu)

# 3. Anneaux d'attaches latéraux (Grommets métalliques)
grommets = []
mat_metal = nb.material("grommet_metal", (0.35, 0.35, 0.38), metallic=0.9, rough=0.3)
for side in (-1, 1):
    gx = side * 0.076
    gz = 1.632
    gy = get_face_depth(gx, gz) - 0.002
    
    bm_ring = bmesh.new()
    bmesh.ops.create_circle(bm_ring, cap_ends=False, segments=12, radius=0.0045,
                            matrix=Matrix.Translation((gx, gy, gz)) @ Matrix.Rotation(math.radians(side * -75), 4, 'Z'))
    bmesh.ops.extrude_edge_only(bm_ring, edges=bm_ring.edges[:])
    bmesh.ops.translate(bm_ring, vec=Vector((side * 0.002, 0, 0)), verts=[v for v in bm_ring.verts if len(v.link_faces) == 0])
    bmesh.ops.solidify(bm_ring, geom=bm_ring.faces[:], thickness=0.001)
    grommets.append(nb.mesh_obj(f"grommet_{side}", bm_ring, mat_metal))

# 4. Lanière élastique noire passant derrière l'occiput
strap_pts = []
N_STRAP = 24
for k in range(N_STRAP + 1):
    frac = k / N_STRAP
    # Angle d'arc derrière la tête de -75° à +75°
    theta = math.radians(-75 + 150 * frac)
    # Centre de l'occiput
    cx, cy, cz = 0.0, -0.015, 1.628
    # Rayon arrière du crâne
    rx = 0.082
    ry = 0.105
    sx = cx + rx * math.sin(theta)
    sy = cy + ry * math.cos(theta)
    sz = cz - 0.008 * math.sin(frac * math.pi) # descend légèrement sous la bosse occipitale
    strap_pts.append(Vector((sx, sy, sz)))

strap_wdirs = [Vector((0, 0, 1)) for _ in strap_pts]
strap_widths = [0.014 for _ in strap_pts]
mat_strap = nb.material("strap_black", (0.05, 0.05, 0.05), metallic=0.0, rough=0.85)
strap_obj = nb.ribbon("strap", strap_pts, strap_wdirs, strap_widths, 0.0018, mat_strap, closed=False)

# Assemblage des pièces du masque
mask_full = nb.join([mask_obj] + grommets + [strap_obj], NAME)

# 5. Rendu procédural de la texture peinte Kitsune (motifs traditionnels rouge sang)
TEX_RES = 1024
tex_color = np.ones((TEX_RES, TEX_RES, 4), dtype=np.float32)
# Fond blanc ivoire porcelaine
tex_color[:, :, 0] = 0.96
tex_color[:, :, 1] = 0.95
tex_color[:, :, 2] = 0.93
tex_color[:, :, 3] = 1.0

# Normal map de base (Z = 1.0 -> RGBA (0.5, 0.5, 1.0, 1.0))
tex_normal = np.zeros((TEX_RES, TEX_RES, 4), dtype=np.float32)
tex_normal[:, :, 0] = 0.5
tex_normal[:, :, 1] = 0.5
tex_normal[:, :, 2] = 1.0
tex_normal[:, :, 3] = 1.0

# Grille de coordonnées UV [0, 1]
Y_grid, X_grid = np.mgrid[0:TEX_RES, 0:TEX_RES]
U = X_grid / (TEX_RES - 1.0)
V = Y_grid / (TEX_RES - 1.0)

# Espace masque centré x_mm dans [-85, 85], z_mm dans [0, 245]
X_mm = (U - 0.5) * 170.0
Z_mm = V * 250.0

def draw_brush_stroke(pts, width_fn, color_rgb=(0.78, 0.08, 0.10)):
    """Trace un trait calligraphique à largeur variable avec bordure nette et relief normal."""
    global tex_color, tex_normal
    sd = np.full(X_mm.shape, 1e9, dtype=np.float32)
    for (ax, az), (bx, bz), w in zip(pts[:-1], pts[1:], width_fn):
        dx, dz = bx - ax, bz - az
        L2 = dx * dx + dz * dz
        if L2 <= 0: continue
        t = np.clip(((X_mm - ax) * dx + (Z_mm - az) * dz) / L2, 0.0, 1.0)
        d = np.hypot(X_mm - ax - t * dx, Z_mm - az - t * dz) - w * 0.5
        np.minimum(sd, d, out=sd)
        
    mask = np.clip(0.5 - sd / 1.2, 0.0, 1.0)
    for c in range(3):
        tex_color[:, :, c] = tex_color[:, :, c] * (1.0 - mask) + color_rgb[c] * mask
        
    # Relief dans la normal map (bordure biseautée peinte)
    d_x = np.gradient(mask, axis=1)
    d_y = np.gradient(mask, axis=0)
    tex_normal[:, :, 0] = np.clip(tex_normal[:, :, 0] - d_x * 0.35, 0.0, 1.0)
    tex_normal[:, :, 1] = np.clip(tex_normal[:, :, 1] - d_y * 0.35, 0.0, 1.0)

# 5.1 Motifs rouges autour des fentes des yeux (Flammes / Ailes kitsune)
for side in (-1, 1):
    # Trait supérieur de l'œil (aile s'étirant vers les tempes)
    eye_top = []
    eye_top_w = []
    for k in range(32):
        t = k / 31.0
        x = side * (20.0 + 44.0 * t)
        z = 160.0 + 14.0 * math.sin(t * math.pi) + side * 4.0 * t
        w = 1.2 + 5.5 * math.sin(t * math.pi)
        eye_top.append((x, z))
        eye_top_w.append(w)
    draw_brush_stroke(eye_top, eye_top_w)
    
    # Trait inférieur en virgule sous l'œil
    eye_bot = []
    eye_bot_w = []
    for k in range(24):
        t = k / 23.0
        x = side * (26.0 + 36.0 * t)
        z = 153.0 - 8.0 * math.sin(t * math.pi) + side * 2.0 * t
        w = 0.8 + 4.0 * math.sin(t * math.pi)
        eye_bot.append((x, z))
        eye_bot_w.append(w)
    draw_brush_stroke(eye_bot, eye_bot_w)

# 5.2 Moustaches kitsune sur chaque joue (3 traits vigoureux par joue)
for side in (-1, 1):
    for idx, (z_start, angle_deg) in enumerate([(136.0, -10), (124.0, -16), (112.0, -22)]):
        whisk = []
        whisk_w = []
        for k in range(28):
            t = k / 27.0
            rad = math.radians(side * angle_deg)
            dist = 14.0 + 48.0 * t
            x = side * (12.0 + dist * math.cos(rad))
            z = z_start + dist * math.sin(rad) - 3.0 * (t ** 2)
            w = 1.0 + 4.2 * math.sin(t * math.pi) * (1.0 - 0.3 * t)
            whisk.append((x, z))
            whisk_w.append(w)
        draw_brush_stroke(whisk, whisk_w)

# 5.3 Emblème ANBU / Flamme sacrée au front
flame_pts = []
flame_w = []
for k in range(36):
    t = k / 35.0
    x = 0.0 + 4.5 * math.sin(t * 3 * math.pi) * (1.0 - t)
    z = 188.0 + 36.0 * t
    w = 1.0 + 7.5 * math.sin(t * math.pi * 0.9) * (1.0 - t * 0.4)
    flame_pts.append((x, z))
    flame_w.append(w)
draw_brush_stroke(flame_pts, flame_w)

# Ailes latérales de l'emblème frontal
for side in (-1, 1):
    w_pts = []
    w_widths = []
    for k in range(24):
        t = k / 23.0
        x = side * (4.0 + 18.0 * t)
        z = 196.0 + 12.0 * math.sin(t * math.pi) - 8.0 * t
        w = 1.0 + 4.0 * math.sin(t * math.pi)
        w_pts.append((x, z))
        w_widths.append(w)
    draw_brush_stroke(w_pts, w_widths)

# 5.4 Museau / nez kitsune : petit triangle stylisé rouge
snout_pts = [(-3.5, 110.0), (0.0, 114.0), (3.5, 110.0), (0.0, 107.0), (-3.5, 110.0)]
draw_brush_stroke(snout_pts, [3.2] * len(snout_pts))

# Sauvegarde des textures dans scratch
tex_c_path = f"{SCRATCH_DIR}/{NAME}_color.png"
tex_n_path = f"{SCRATCH_DIR}/{NAME}_normal.png"
nb.save_png(tex_c_path, tex_color)
nb.save_png(tex_n_path, tex_normal, non_color=True)
print("Textures générées :", tex_c_path, tex_n_path)

# Assignation du matériau dans Blender pour les rendus de contrôle
nt = mat_anbu.node_tree
bsdf = nt.nodes.get("Principled BSDF")
bsdf.inputs["Roughness"].default_value = 0.22
bsdf.inputs["Metallic"].default_value = 0.0

img_c = bpy.data.images.load(tex_c_path)
img_n = bpy.data.images.load(tex_n_path)
img_n.colorspace_settings.name = "Non-Color"

node_c = nt.nodes.new("ShaderNodeTexImage")
node_c.image = img_c
nt.links.new(node_c.outputs["Color"], bsdf.inputs["Base Color"])

node_n = nt.nodes.new("ShaderNodeTexImage")
node_n.image = img_n
node_nm = nt.nodes.new("ShaderNodeNormalMap")
node_nm.inputs["Strength"].default_value = 1.0
nt.links.new(node_n.outputs["Color"], node_nm.inputs["Color"])
nt.links.new(node_nm.outputs["Normal"], bsdf.inputs["Normal"])

# 6. Rigging : Pondération 100% sur l'os 'head' du Citizen
vg = mask_full.vertex_groups.new(name="head")
vg.add(range(len(mask_full.data.vertices)), 1.0, "REPLACE")
mask_full.parent = arm
mask_full.matrix_parent_inverse = arm.matrix_world.inverted()
mod_arm = mask_full.modifiers.new("Armature", "ARMATURE")
mod_arm.object = arm

# 7. Contrôle des métriques & Validation
tris = nb.tri_count(mask_full)
bb = [mask_full.matrix_world @ Vector(c) for c in mask_full.bound_box]
h_cm = (max(v.z for v in bb) - min(v.z for v in bb)) * 100.0
w_cm = (max(v.x for v in bb) - min(v.x for v in bb)) * 100.0
print(f"TRIS {NAME}: {tris}, Hauteur: {h_cm:.1f} cm, Largeur: {w_cm:.1f} cm")
assert tris < 5000, f"Trop de triangles : {tris}"
assert 18.0 <= h_cm <= 26.0, f"Hauteur hors gabarit : {h_cm:.1f} cm"

# 8. Export FBX dans scratch
fbx_scratch = f"{SCRATCH_DIR}/{NAME}.fbx"
nb.export_fbx(fbx_scratch, [mask_full, arm])
print("FBX exporté dans scratch :", fbx_scratch)

nb.save_blend(NAME)

# 9. Rendus de contrôle
if "--no-render" not in A:
    nb.setup_eevee()
    tgt = Vector((0, -0.06, 1.63))
    nb.render_view(f"{SCRATCH_DIR}/{NAME}_face.png", tgt, (0, -1, 0.05), 0.70)
    nb.render_view(f"{SCRATCH_DIR}/{NAME}_34.png", tgt, (0.75, -0.70, 0.20), 0.75)
    nb.render_view(f"{SCRATCH_DIR}/{NAME}_profil.png", tgt, (1.0, 0, 0.0), 0.70)
    print("Rendus sauvegardés dans scratch.")
