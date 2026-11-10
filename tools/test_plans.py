# Tests des plans v3 (quart de cercle) : python tools/test_plans.py
import importlib.util, json, math, os, sys, tempfile
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from carte import TIERS

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
        if math.hypot(x, y) > 500 + 1e-6 or not 225 - 1e-6 <= a <= 315 + 1e-6: err(d, f"hors du quart ({x:.0f},{y:.0f})"); break

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
    if vd["V6"]["level"] != "bas" or vd["V6"]["shape"] != {"t": "circle", "x": 0.0, "y": -420.0, "r": 30}: erreurs.append(f"{v} V6 mal placée")
    a1 = vd["A1"]["shape"]
    if (a1["r"], a1["a0"], a1["a1"]) != (500, 225, 315): erreurs.append(f"{v} A1 pas un quart")
    for k in ("X1", "X2"):
        r = math.hypot(vd[k]["shape"]["x"], vd[k]["shape"]["y"])
        if not 560 <= r <= 800 or abs(vd[k]["shape"]["x"]) < 20: erreurs.append(f"{v} {k} mal placée")
    for k in ("S1", "S2", "S3", "H1", "T1", "T2"):
        if k not in vd: erreurs.append(f"{v} {k} manquant")
    if "door" in vd["H1"]: erreurs.append(f"{v} H1 ne doit pas avoir de porte")
    if not (vd["T1"]["door"]["oneWay"] and vd["T1"]["door"]["rule"] == "Kage" and vd["T2"]["door"]["rule"] == "SpecialUnit"): erreurs.append(f"{v} portes tunnels")
    doms = [f"{v}-{d['code']}" for d in docs if d["village"] == v and d["group"] == "Domaines" and d["layer"] == "surface"]
    refs = [d["door"]["domain"] for d in docs if d["village"] == v and d["code"].startswith("S") and d["door"]["rule"] == "Clan"]
    if sorted(doms) != sorted(refs): erreurs.append(f"{v} refuges {refs} != domaines {doms}")

if erreurs:
    print("\n".join(erreurs)); sys.exit(1)
print(f"OK : {len(docs)} éléments, plans v3 valides")
