# Génère le bandeau frontal ninja ajusté à la tête de citizen_human_male_REF.
# blender.exe --background --factory-startup --python bandeau_frontal.py -- \
#     [<village> | --emblem konoha|suna|kumo|none] [--cloth R,G,B] [--no-render]
# Sorties : Assets/models/ninja/bandeau_frontal/bandeau_<village>.fbx (+ _color/_normal.png),
#           bandeau_frontal.fbx pour "none" (plaque vierge), .blend + rendus dans SCRATCH.
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import emblemes  # noqa: E402
import nrp_blender as nb  # noqa: E402

A = nb.args()
if "--emblem" in A:
    EMBLEM = A[A.index("--emblem") + 1]
elif A and not A[0].startswith("--"):
    EMBLEM = A[0]
else:
    EMBLEM = "none"

assert EMBLEM in emblemes.VILLAGES, f"Village/emblème inconnu : {EMBLEM}"
emblem_fn, default_cloth = emblemes.VILLAGES[EMBLEM]
CLOTH_RGB = tuple(float(c) for c in A[A.index("--cloth") + 1].split(",")) if "--cloth" in A else default_cloth
METAL_RGB = (0.62, 0.64, 0.68)
NAME = "bandeau_frontal" if EMBLEM == "none" else f"bandeau_{EMBLEM}"
OUT_DIR = f"{nb.PROJECT}/Assets/models/ninja/bandeau_frontal"

# Gabarit (mètres). Hauteurs du centre du bandeau relatives au sommet du crâne (lu sur le mesh).
BAND_H = 0.036        # hauteur du tissu
FRONT_DROP = 0.060    # centre du bandeau à l'avant, sous le sommet du crâne
BACK_DROP = 0.078     # à l'arrière (plus bas, sous l'occiput, comme un vrai bandeau)
GAP = 0.002           # jeu tissu/peau
CLOTH_T = 0.0025      # épaisseur tissu
PLATE_W, PLATE_H, PLATE_T = 0.098, 0.044, 0.0025
COLS, ROWS = 64, 6

nb.reset()
arm, meshes = nb.import_ref(nb.REF_MALE)
head = meshes["human_male_head"]
tree = nb.world_bvh(head)
top_z = max((head.matrix_world @ v.co).z for v in head.data.vertices)


def centre_at(z):
    """Centre XY de la tranche du crâne à la hauteur z (lu sur le mesh)."""
    pts = [head.matrix_world @ v.co for v in head.data.vertices]
    sl = [p for p in pts if abs(p.z - z) < 0.006]
    return Vector((0.0, (min(p.y for p in sl) + max(p.y for p in sl)) / 2, z))


def surface_r(theta, z, c):
    """Rayon horizontal de la surface extérieure de la tête (lancer de rayon de l'extérieur vers le centre)."""
    d = nb.ring_dir(theta)
    origin = Vector((c.x, c.y, z)) + d * 0.4
    hit, _, _, _ = tree.ray_cast(origin, -d, 0.5)
    assert hit is not None, f"pas d'impact theta={theta:.2f} z={z:.3f}"
    return (hit - Vector((c.x, c.y, z))).length


def band_z(theta):
    """Hauteur du centre du bandeau : plus haut devant, plus bas derrière."""
    k = (1 - math.cos(theta)) / 2
    return top_z - (FRONT_DROP + (BACK_DROP - FRONT_DROP) * k)


C = centre_at(top_z - (FRONT_DROP + BACK_DROP) / 2)

# Grille de rayons lus sur la tête, puis lissage (efface oreilles/sourcils) sans jamais rentrer dans la peau.
thetas = [2 * math.pi * i / COLS for i in range(COLS)]
raw = [[surface_r(t, band_z(t) + BAND_H * (j / (ROWS - 1) - 0.5), C) for j in range(ROWS)] for t in thetas]
r = [row[:] for row in raw]
for _ in range(6):
    r = [[(r[i - 1][j] + 2 * r[i][j] + r[(i + 1) % COLS][j]) / 4 for j in range(ROWS)] for i in range(COLS)]
r = [[max(r[i][j], raw[i][j]) + GAP for j in range(ROWS)] for i in range(COLS)]


def band_pt(i, j, extra=0.0):
    t = thetas[i % COLS]
    z = band_z(t) + BAND_H * (j / (ROWS - 1) - 0.5)
    return Vector((C.x, C.y, z)) + nb.ring_dir(t) * (r[i % COLS][j] + extra)


# --- Bande de tissu (anneau fermé) ---
mat_cloth = nb.material("headband_cloth", CLOTH_RGB, 0.0, 0.85)
mat_metal = nb.material("headband_metal", METAL_RGB, 1.0, 0.35)
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
V = [[bm.verts.new(band_pt(i, j)) for j in range(ROWS)] for i in range(COLS)]
for i in range(COLS):
    for j in range(ROWS - 1):
        f = bm.faces.new((V[i][j], V[(i + 1) % COLS][j], V[(i + 1) % COLS][j + 1], V[i][j + 1]))
        for l, (a, b) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
            l[uv].uv = (a / COLS, b / (ROWS - 1) * 0.1)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=-CLOTH_T)  # épaisseur vers l'extérieur
band = nb.mesh_obj("band", bm, mat_cloth)


# --- Plaque métal : règle verticale qui suit la courbure du tissu ---
def plate_r(a):
    """Rayon de la plaque à l'angle a : max du tissu sur la hauteur, interpolé entre colonnes."""
    x = (a % (2 * math.pi)) / (2 * math.pi) * COLS
    i, f = int(x), x - int(x)
    m = lambda k: max(r[k % COLS])
    return m(i) * (1 - f) + m(i + 1) * f + CLOTH_T + 0.0008


R_plate = plate_r(0)
zc = band_z(0)
half_ang = (PLATE_W / 2) / R_plate
PC, PR = 12, 3
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
P = [[bm.verts.new(Vector((C.x, C.y, zc + PLATE_H * (j / PR - 0.5)))
                   + nb.ring_dir(-half_ang + 2 * half_ang * i / PC) * plate_r(-half_ang + 2 * half_ang * i / PC))
      for j in range(PR + 1)] for i in range(PC + 1)]
for i in range(PC):
    for j in range(PR):
        f = bm.faces.new((P[i][j], P[i][j + 1], P[i + 1][j + 1], P[i + 1][j]))
        for l, (a, b) in zip(f.loops, ((i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j))):
            l[uv].uv = (a / PC, b / PR)  # UV 0-1 = toute la face avant (texture d'emblème)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=-PLATE_T)
plate = nb.mesh_obj("plate", bm, mat_metal)

# --- 6 Rivets : 3 de chaque côté (haut, milieu, bas), dômes bien définis et propres ---
rivets = []
margin_x = 0.0080  # 8.0 mm du bord latéral de la plaque
margin_z = 0.0075  # 7.5 mm du bord haut/bas (le milieu est à zc)
rivet_r = 0.0028   # rayon 2.8 mm (diamètre 5.6 mm)

for sx in (-1, 1):
    a = sx * (half_ang - margin_x / R_plate)
    d = nb.ring_dir(a)
    u_rivet = 0.5 + sx * (half_ang - margin_x / R_plate) / (2 * half_ang)
    for sz in (-1, 0, 1):
        z_offset = sz * (PLATE_H / 2 - margin_z)
        v_rivet = 0.5 + z_offset / PLATE_H
        pos = Vector((C.x, C.y, zc + z_offset)) + d * (plate_r(a) + PLATE_T)
        bm = bmesh.new()
        uv = bm.loops.layers.uv.new("UVMap")
        mat_trans = (Matrix.Translation(pos)
                     @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()
                     @ Matrix.Diagonal((1, 1, 0.55, 1)))
        bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=5, radius=rivet_r, matrix=mat_trans)
        mat_inv = mat_trans.inverted()
        for f in bm.faces:
            for l in f.loops:
                local_p = mat_inv @ l.vert.co
                l[uv].uv = (u_rivet + local_p.x / PLATE_W, v_rivet + local_p.y / PLATE_H)
        rivets.append(nb.mesh_obj(f"rivet_{sx}_{sz}", bm, mat_metal))

# --- Nœud + deux pans à l'arrière ---
back = nb.ring_dir(math.pi)
zb = band_z(math.pi)
r_back = r[COLS // 2][ROWS // 2] + CLOTH_T
knot_c = Vector((C.x, C.y, zb)) + back * (r_back + 0.007)
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))


def ellipse(c, u, v, a, b, n=16):
    return [c + u * (a * math.cos(2 * math.pi * k / n)) + v * (b * math.sin(2 * math.pi * k / n)) for k in range(n)]


# Nœud : un tour serré au centre + deux boucles inclinées en sens opposés (elles se croisent sous le tour).
knot_parts = []
pts = ellipse(knot_c + Y * 0.004, X, Y, 0.0095, 0.0100)
knot_parts.append(nb.ribbon("knot_wrap", pts, [Z] * len(pts), [0.024] * len(pts), 0.002, mat_cloth, closed=True))
for side, tilt in ((-1, 22), (1, -22)):
    d1 = Vector((side * math.cos(math.radians(tilt)), 0, math.sin(math.radians(tilt))))
    m = (Y + d1.cross(Y).normalized()).normalized()  # boucle entrouverte, visible de dos
    c = knot_c + d1 * 0.017 + Y * 0.007
    pts = ellipse(c, d1, m, 0.019, 0.010)
    wd = d1.cross(m).normalized()
    knot_parts.append(nb.ribbon("knot_loop", pts, [wd] * len(pts), [0.017] * len(pts), 0.002, mat_cloth, closed=True))

# Pans : tombent en S léger vers l'arrière, s'écartent, et vrillent progressivement.
tails = []
TL, TS = 0.15, 14
for side in (-1, 1):
    pts = []
    for k in range(TS + 1):
        s = k / TS
        pts.append(knot_c + Vector((side * (0.004 + 0.030 * s + 0.006 * math.sin(s * math.pi * 1.5)),
                                    0.002 + 0.028 * s * s + 0.006 * math.sin(s * math.pi),
                                    -0.004 - TL * s)))
    wdirs, widths = [], []
    for k, p in enumerate(pts):
        t = (pts[min(k + 1, TS)] - pts[max(k - 1, 0)]).normalized()
        base_dir = (X - t * X.dot(t)).normalized()
        tw = side * math.radians(55) * (k / TS) ** 1.3
        wdirs.append(Matrix.Rotation(tw, 3, t) @ base_dir)
        widths.append(BAND_H * (0.80 - 0.12 * k / TS))
    tails.append(nb.ribbon("tail", pts, wdirs, widths, CLOTH_T, mat_cloth))

hb = nb.join([band, plate] + knot_parts + rivets + tails, NAME)

# --- Plaque métal : fond en acier brossé + emblème à l'échelle fit() ---
base = emblemes.brushed_steel(PLATE_W * 1000, PLATE_H * 1000, METAL_RGB)
strokes = emblemes.fit(emblem_fn(), PLATE_H * 1000, fill=0.72) if emblem_fn else None
col, nrm = emblemes.bake(strokes, PLATE_W * 1000, PLATE_H * 1000, METAL_RGB, base=base)

img_c = nb.save_png(f"{OUT_DIR}/{NAME}_color.png", col)
img_n = nb.save_png(f"{OUT_DIR}/{NAME}_normal.png", nrm, non_color=True)
nt = mat_metal.node_tree
bsdf = nt.nodes["Principled BSDF"]
tc, tn = nt.nodes.new("ShaderNodeTexImage"), nt.nodes.new("ShaderNodeTexImage")
nm = nt.nodes.new("ShaderNodeNormalMap")
tc.image, tn.image = img_c, img_n
nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])

# --- Skin : 100 % sur l'os "head" de la référence ---
vg = hb.vertex_groups.new(name="head")
vg.add(range(len(hb.data.vertices)), 1.0, "REPLACE")
hb.parent = arm
hb.matrix_parent_inverse = arm.matrix_world.inverted()
mod = hb.modifiers.new("Armature", "ARMATURE")
mod.object = arm

# --- Contrôle d'ajustement : distance signée face intérieure du tissu / peau ---
inner = [band_pt(i, j) for i in range(COLS) for j in range(ROWS)]
dists = [nb.signed_dist(tree, p) for p in inner]
print(f"FIT male: inner band min {min(dists)*100:.2f} cm, max {max(dists)*100:.2f} cm")
print(f"TRIS {NAME}: {nb.tri_count(hb)}")
assert nb.tri_count(hb) < 3000

# --- Comparaison avec la tête féminine (même mesh) ---
bpy.ops.object.select_all(action="DESELECT")
farm, fmeshes = nb.import_ref(nb.REF_FEMALE)
fhead = next(o for n, o in fmeshes.items() if "head" in n)
ftree = nb.world_bvh(fhead)
fd = [nb.signed_dist(ftree, p) for p in inner]
print(f"FIT female (mesh homme tel quel): min {min(fd)*100:.2f} cm (négatif = pénétration), max gap {max(fd)*100:.2f} cm")
for o in fmeshes.values():
    o.hide_render = True

# --- Export (armature de référence + bandeau, sans les meshes du corps) ---
out = f"{OUT_DIR}/{NAME}.fbx"
nb.export_fbx(out, [hb, arm])
print("EXPORT", out)
nb.save_blend(NAME)

if "--no-render" not in A:
    nb.setup_eevee()
    tgt = Vector((0, C.y, top_z - 0.09))
    if EMBLEM == "none":
        views = (("front", (0, -1, 0.08)), ("34", (0.8, -0.75, 0.15)), ("back", (0.3, 1, 0.12)))
        for name, d in views:
            nb.render_view(f"{nb.SCRATCH}/bandeau_{name}.png", tgt, d, 0.85)
    else:
        plate_c = Vector((C.x, C.y, zc)) + nb.ring_dir(0) * R_plate
        nb.render_view(f"{nb.SCRATCH}/{NAME}_plaque.png", plate_c, (0, -1, 0.06), 0.30)
        nb.render_view(f"{nb.SCRATCH}/{NAME}_34.png", tgt, (0.8, -0.75, 0.15), 0.85)
        nb.render_view(f"{nb.SCRATCH}/{NAME}_dos.png", knot_c, (0.35, 1, 0.25), 0.40)
        if EMBLEM == "konoha":  # même mesh (ajusté homme) posé sur la tête féminine
            for o in meshes.values():
                o.hide_render = True
            for o in fmeshes.values():
                o.hide_render = False
            nb.render_view(f"{nb.SCRATCH}/{NAME}_femme.png", tgt, (0.6, -0.8, 0.12), 0.75)
