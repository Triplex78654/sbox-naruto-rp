# Vue 2D de la carte complète (relief ombré + biomes + villages + blocage des territoires).
# Usage : python vue2d.py <racine nrp> <sortie.png>
import sys, os, math, json, glob
root, out = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(root, "tools"))
os.chdir(os.path.join(root, "tools"))
sys.argv = ["x"]
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from test_relief import _generer
import carte, relief
from carte import HALF, INNER, M, CORNERS, local_to_world, territoire, biome_poids, VILLAGE_R, PAYS_R, TIERS

tmp, gen = _generer()
W = 2000
S = W / (2 * HALF)
def P(x, y): return ((x + HALF) * S, (HALF - y) * S)

# relief ombré (carte de hauteurs finale exportée)
R = 2048
h = np.fromfile(os.path.join(root, "tools", "out", "relief.u16"), dtype="<u2").reshape(R, R).astype(float)
z = np.flipud(h / 65535 * 40000 - 2000)
gy, gx = np.gradient(z, 2 * HALF / R)
sh = np.clip(0.85 + (-gx + gy) * 1.6, 0.35, 1.35)
# biomes (échantillonnés)
N = 400
COL = dict(foret=(96, 150, 70), desert=(214, 190, 130), roche=(150, 130, 112), neutre=(150, 145, 105))
bio = np.zeros((N, N, 3))
for j in range(N):
    y = HALF - (j + 0.5) * 2 * HALF / N
    for i in range(N):
        x = -HALF + (i + 0.5) * 2 * HALF / N
        if max(abs(x), abs(y)) > INNER: bio[j, i] = (110, 110, 110); continue
        w = biome_poids(x, y)
        bio[j, i] = sum(np.array(COL[k]) * v for k, v in w.items())
bio = np.array(Image.fromarray(bio.astype(np.uint8)).resize((R, R), Image.BILINEAR)).astype(float)
img = np.clip(bio * sh[..., None], 0, 255).astype(np.uint8)
im = Image.fromarray(img).resize((W, W), Image.BILINEAR).convert("RGBA")
d = ImageDraw.Draw(im, "RGBA")

# frontières de territoire (contour des zones)
step = 200
for x in range(-INNER, INNER, step):
    for y in range(-INNER, INNER, step):
        t = territoire(x, y)
        if territoire(x + step, y) != t or territoire(x, y + step) != t:
            px, py = P(x, y); d.rectangle([px - 1, py - 1, px + 1, py + 1], fill=(255, 255, 255, 200))

# blocage des territoires (boîtes)
def poly(b):
    (x, y, _), (sx, sy, _), (dx, dy, _) = b["c"], b["s"], b["dir"]
    n = math.hypot(dx, dy) or 1
    ux, uy = dx / n, dy / n
    return [P(x + ux * a * sx / 2 - uy * c * sy / 2, y + uy * a * sx / 2 + ux * c * sy / 2) for a, c in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
STY = {"Route": (200, 180, 140, 255), "Eau": (60, 120, 220, 230), "Bassin": (60, 120, 220, 255), "Chute": (140, 200, 255, 255),
       "Lac": (60, 120, 220, 255), "Grande": (140, 200, 255, 255),
       "Pont": (120, 70, 30, 255), "Arche": (170, 90, 50, 255), "Poste-frontière": (220, 30, 30, 255)}
for b in sorted(gen.TERR, key=lambda b: b["name"].startswith("Tronc")):
    k = b["name"].split(" ")[0].split("(")[0]
    if k == "Tronc":
        x, y = P(b["c"][0], b["c"][1]); d.ellipse([x - 1.6, y - 1.6, x + 1.6, y + 1.6], fill=(25, 80, 25, 230)); continue
    d.polygon(poly(b), fill=STY.get(k, (90, 90, 90, 255)))

# villages : rempart, niveaux, éléments des plans
for v in ("konoha", "suna", "kumo"):
    cx, cy = CORNERS[v]
    for r, col, wd in ((VILLAGE_R, (255, 255, 255, 255), 3), (TIERS["milieu"][0], (255, 255, 255, 120), 1), (TIERS["bas"][0], (255, 255, 255, 120), 1)):
        x, y = P(cx, cy); rr = r * M * S; d.ellipse([x - rr, y - rr, x + rr, y + rr], outline=col, width=wd)
FILL = {"built": (230, 225, 210, 255), "domain": (170, 110, 60, 255), "zone": (200, 190, 150, 200), "road": (200, 180, 140, 255),
        "water": (60, 120, 220, 255), "park": (90, 160, 70, 255), "tower": (240, 240, 240, 255), "wall": (80, 70, 60, 255)}
for f in glob.glob(os.path.join(tmp, "*.json")):
    e = json.load(open(f, encoding="utf-8"))
    if e.get("layer") != "surface": continue
    v, s = e["village"], e["shape"]; col = FILL.get(e.get("fill"), (200, 200, 200, 220))
    W2 = lambda x, y: P(*local_to_world(v, x, y))
    t = s["t"]
    if t == "rect":
        a = math.radians(s.get("rot", 0)); ca, sa = math.cos(a), math.sin(a)
        d.polygon([W2(s["x"] + ca * dx - sa * dy, s["y"] + sa * dx + ca * dy) for dx, dy in ((-s["w"]/2, -s["h"]/2), (s["w"]/2, -s["h"]/2), (s["w"]/2, s["h"]/2), (-s["w"]/2, s["h"]/2))], fill=col)
    elif t == "circle":
        x, y = W2(s["x"], s["y"]); rr = s["r"] * M * S; d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=col)
    elif t == "line":
        d.line([W2(*p) for p in s["pts"]], fill=col, width=max(1, int(s["w"] * M * S)))
    elif t == "ring":
        pts = [W2(s["x"] + s["r"] * math.cos(math.radians(a)), s["y"] + s["r"] * math.sin(math.radians(a))) for a in np.linspace(s["a0"], s["a1"], 60)]
        d.line(pts, fill=col, width=max(1, int(s["w"] * M * S)))
    elif t == "houses":
        x, y = W2(s["zx"], s["zy"]); rr = s["zr"] * M * S; d.ellipse([x - rr, y - rr, x + rr, y + rr], outline=(230, 225, 210, 255), width=1)
    if t in ("rect", "circle") and e.get("code"):
        x, y = W2(s["x"], s["y"]); d.text((x - 6, y - 5), e["code"], fill=(0, 0, 0, 255), font_size=10)

# étiquettes + échelle
for v, nom in (("konoha", "KONOHA"), ("suna", "SUNA"), ("kumo", "KUMO"), ("libre", "4e COIN")):
    cx, cy = CORNERS[v]; x, y = P(cx * 0.45, cy * 0.45)
    d.text((x - 40, y), nom, fill=(0, 0, 0, 255), font_size=30, stroke_width=3, stroke_fill=(255, 255, 255, 255))
x, y = P(0, 0); d.text((x - 60, y + 20), "CENTRE", fill=(0, 0, 0, 255), font_size=26, stroke_width=3, stroke_fill=(255, 255, 255, 255))
x0, y0 = 40, W - 30; d.line([x0, y0, x0 + 500 * M * S, y0], fill=(0, 0, 0, 255), width=5); d.text((x0, y0 - 28), "500 m", fill=(0, 0, 0, 255), font_size=22)
im.convert("RGB").save(out)
print("ok", out)
