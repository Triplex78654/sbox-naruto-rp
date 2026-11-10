# Tests des plans v3 (quart de cercle) : python tools/test_plans.py
import importlib.util, json, math, os, subprocess, sys, tempfile
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from carte import TIERS, VILLAGE_R, PAYS_R, ECHELLE_VILLAGE

ICI = os.path.dirname(os.path.abspath(__file__))
REF = "5d0e71c"   # dernier commit avant l'agrandissement à 700 m (tâche 3) : référence des tailles, portes et domaines

tmp = tempfile.mkdtemp()
sys.argv = ["plans-villages-v3.py", tmp]
spec = importlib.util.spec_from_file_location("plans_v3", os.path.join(os.path.dirname(os.path.abspath(__file__)), "plans-villages-v3.py"))
m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)  # génère les JSON dans tmp

docs = [json.load(open(os.path.join(tmp, f), encoding="utf-8")) for f in os.listdir(tmp) if f.endswith(".json")]
surf = [d for d in docs if d["layer"] == "surface"]
erreurs = []
def err(d, msg): erreurs.append(f'{d["village"]}-{d["code"]} : {msg}')

def level_of(r): return "haut" if r < TIERS["milieu"][0] else "milieu" if r < TIERS["bas"][0] else "bas"
def centre_r(s):
    t = s["t"]
    if t == "ring": return math.hypot(s["x"], s["y"]) if s.get("fill") else s["r"]
    if t == "houses": return math.hypot(s["zx"], s["zy"])
    if t in ("rect", "circle"): return math.hypot(s["x"], s["y"])
    return None

for d in docs:
    if d["level"] not in TIERS: err(d, "level invalide")
    if d["layer"] not in ("surface", "souterrain"): err(d, "layer invalide")
    r = centre_r(d["shape"])
    if d["layer"] == "surface" and r is not None and d["code"][0] != "X" and level_of(r) != d["level"]:
        err(d, f'level {d["level"]} incohérent avec r={r:.0f}')
    if d["code"].startswith("S") and "door" not in d: err(d, "salle sans porte")
    if "door" in d and d["door"]["rule"] not in ("Kage", "Council", "SpecialUnit", "Police", "Clan"): err(d, "règle de porte")

for d in surf:
    c = d["code"]
    if c == "A1" or c[0] == "X" or c == "P5" and d["village"] == "konoha": continue
    s = d["shape"]
    if s["t"] == "ring" and not s.get("fill"):
        if s["a0"] < 225 or s["a1"] > 315: err(d, "anneau hors quart")
        continue
    for x, y in m.pts(s):
        a = math.degrees(math.atan2(y, x)) % 360
        if math.hypot(x, y) > VILLAGE_R + 1e-6 or not 225 - 1e-6 <= a <= 315 + 1e-6: err(d, f"hors du quart ({x:.0f},{y:.0f})"); break

bati = [d for d in surf if d["group"] != "Routes" and d["code"] not in ("A1", "P5")]
for d in bati:
    s = d["shape"]
    if d["code"][0] != "X" and s["t"] != "line":
        if any(m.ramp_dist(d["village"], x, y) < m.RAMPE_MARGE for x, y in m.pts(s)): err(d, "trop près d'une rampe")
for i, a in enumerate(bati):
    for b in bati[i + 1:]:
        if a["village"] == b["village"] and any(m.hit(p, q) for p in m.footprint(a["shape"], 0) for q in m.footprint(b["shape"], 0)):
            err(a, f'chevauche {b["code"]}')

for v in ("konoha", "suna", "kumo"):
    vd = {d["code"]: d for d in docs if d["village"] == v}
    if vd["V6"]["level"] != "bas" or vd["V6"]["shape"] != {"t": "circle", "x": 0.0, "y": round(-420 * ECHELLE_VILLAGE, 1), "r": 30}: erreurs.append(f"{v} V6 mal placée")
    a1 = vd["A1"]["shape"]
    if (a1["r"], a1["a0"], a1["a1"]) != (VILLAGE_R, 225, 315): erreurs.append(f"{v} A1 pas un quart")
    for k in ("X1", "X2"):
        x, y = vd[k]["shape"]["x"], vd[k]["shape"]["y"]
        r = math.hypot(x, y)
        # route de la porte : axe local x = 0, du rempart à l'arc du pays
        d_route = abs(x) if -PAYS_R <= y <= -VILLAGE_R else min(math.hypot(x, y + VILLAGE_R), math.hypot(x, y + PAYS_R))
        if not VILLAGE_R < r < PAYS_R - 50 or d_route < 40: erreurs.append(f"{v} {k} mal placée (r {r:.0f}, route à {d_route:.0f} m)")
    for k in ("S1", "S2", "S3", "H1", "T1", "T2"):
        if k not in vd: erreurs.append(f"{v} {k} manquant")
    if "door" in vd["H1"]: erreurs.append(f"{v} H1 ne doit pas avoir de porte")
    if not (vd["T1"]["door"]["oneWay"] and vd["T1"]["door"]["rule"] == "Kage" and vd["T2"]["door"]["rule"] == "SpecialUnit"): erreurs.append(f"{v} portes tunnels")
    doms = [f"{v}-{d['code']}" for d in docs if d["village"] == v and d["group"] == "Domaines" and d["layer"] == "surface"]
    refs = [d["door"]["domain"] for d in docs if d["village"] == v and d["code"].startswith("S") and d["door"]["rule"] == "Clan"]
    if sorted(doms) != sorted(refs): erreurs.append(f"{v} refuges {refs} != domaines {doms}")

# Tunnels T* : loin des salles S* qu'ils ne desservent pas (on n'entre dans une salle que par sa porte)
def dist_seg_rect(p, q, r):
    n = max(1, math.ceil(math.dist(p, q) / 2))
    return min(math.hypot(max(abs(p[0] + (q[0] - p[0]) * k / n - r["x"]) - r["w"] / 2, 0), max(abs(p[1] + (q[1] - p[1]) * k / n - r["y"]) - r["h"] / 2, 0)) for k in range(n + 1))
for t in (d for d in docs if d["code"][0] == "T"):
    p, q = t["shape"]["pts"]
    for sl in (d for d in docs if d["village"] == t["village"] and d["code"][0] == "S" and d["layer"] == "souterrain"):
        if [sl["shape"]["x"], sl["shape"]["y"]] != p and dist_seg_rect(p, q, sl["shape"]) < 10: err(t, f'passe à moins de 10 m de {sl["code"]}')

if TIERS["bas"][1] != VILLAGE_R: erreurs.append(f'TIERS["bas"] finit à {TIERS["bas"][1]} au lieu de {VILLAGE_R}')

# Comparaison avec les plans d'avant l'agrandissement : mêmes éléments, mêmes tailles, mêmes portes et domaines.
ref_dir, ref_out = tempfile.mkdtemp(), tempfile.mkdtemp()
for f in ("carte.py", "relief.py", "plans-villages-v3.py"):
    open(os.path.join(ref_dir, f), "w", encoding="utf-8").write(
        subprocess.run(["git", "show", f"{REF}:tools/{f}"], cwd=ICI, capture_output=True, check=True, encoding="utf-8").stdout)
subprocess.run([sys.executable, os.path.join(ref_dir, "plans-villages-v3.py"), ref_out], check=True, stdout=subprocess.DEVNULL)
avant = {(d["village"], d["code"]): d for d in (json.load(open(os.path.join(ref_out, f), encoding="utf-8")) for f in os.listdir(ref_out) if f.endswith(".json"))}
apres = {(d["village"], d["code"]): d for d in docs}
def taille(s):
    t = s["t"]
    if t == "rect": return (s["w"], s["h"])
    if t == "circle": return s["r"]
    if t == "ring": return (s["r"], s["w"]) if s.get("fill") else s["w"]
    if t == "houses": return (s["n"], s["zr"])
    if t == "line": return s["w"]
if sorted(avant) != sorted(apres): erreurs.append(f"codes différents : {sorted(set(avant) ^ set(apres))}")
for k, d in avant.items():
    if k in apres and taille(d["shape"]) != taille(apres[k]["shape"]): erreurs.append(f"{k} : taille {taille(d['shape'])} -> {taille(apres[k]['shape'])}")
portes = lambda ds: sorted((k, json.dumps(d["door"], sort_keys=True)) for k, d in ds.items() if "door" in d)
if portes(avant) != portes(apres): erreurs.append("portes différentes de la référence")
domaines = lambda ds: sorted(k for k, d in ds.items() if d["group"] == "Domaines")
if domaines(avant) != domaines(apres): erreurs.append("domaines différents de la référence")

if erreurs:
    print("\n".join(erreurs)); sys.exit(1)
print(f"OK : {len(docs)} éléments, plans v3 valides")
