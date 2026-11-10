# Plans v3 des villages : quart de cercle (angles locaux 225-315°, grande porte à 270°, coin = origine, -Y vers le centre du monde).
# Trois niveaux par rayon (haut / milieu / bas, voir carte.TIERS), rampes aux frontières, souterrains (salles, tunnels, sorties).
# Écrit un JSON par élément dans <sortie>/ (même format que la v2 + `level`, `layer`, `door`).
# Placement automatique sans chevauchement, dans l'anneau du niveau, entre 228° et 312°, à 12 m au moins des rampes.
# Usage : python tools/plans-villages-v3.py <sortie>
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from carte import TIERS, RAMPS
from relief import RAMPE_LARG

out_dir = sys.argv[1]
os.makedirs(out_dir, exist_ok=True)

RAMPE_MARGE = RAMPE_LARG[1] + 12  # distance mini à l'axe d'une rampe (demi-largeur + 12 m)
ANG_MIN, ANG_MAX = 228, 312       # fenêtre d'angles du placement automatique
HAUT_R0 = 45                      # le haut commence après la falaise / les emplacements

def pol(r, a):
    return (round(r * math.cos(math.radians(a)), 1), round(r * math.sin(math.radians(a)), 1))
def C(x, y, r): return {"t": "circle", "x": x, "y": y, "r": r}
def R(x, y, w, h, rot=0): return {"t": "rect", "x": x, "y": y, "w": w, "h": h, "rot": rot}
def RING(x, y, r, w, a0, a1, fill=False): return {"t": "ring", "x": x, "y": y, "r": r, "w": w, "a0": a0, "a1": a1, "fill": fill}
def L(pts, w): return {"t": "line", "pts": [list(p) for p in pts], "w": w}
def H(n, style, zx, zy, zr): return {"t": "houses", "n": n, "style": style, "zx": zx, "zy": zy, "zr": zr}

# ── Bâtiments placés automatiquement : code, groupe, nom, couleur, taille ((w,h) = rect, nombre = rayon), niveau, angle visé, rayon visé (None = milieu de l'anneau), description[, caché]
COMMON = [
    ("P2", "Pouvoir", "Salle du conseil", "built", (34, 22), "haut", 242, None, "Réunions du Kage, du conseil et des chefs d'unité."),
    ("P3", "Pouvoir", "Bureau des missions", "built", (30, 20), "haut", 298, None, "PNJ des missions D et C, tableau des missions B à S données en RP."),
    ("P4", "Pouvoir", "Archives et bibliothèque", "built", (26, 20), "haut", 270, 150, "Parchemins, registres des ninjas, savoir du village."),
    ("U1", "Unités", "Caserne de la garde", "built", (36, 24), "milieu", 235, None, "Unité de garde : relèves aux portes et rondes."),
    ("U2", "Unités", "QG de la police et prison", "built", (38, 26), "milieu", 305, None, "Police du village, cellules pour les arrestations RP."),
    ("U3", "Unités", "QG de l'unité spéciale (caché)", "built", (24, 18), "milieu", 252, None, "Entrée discrète au fond d'une ruelle. Réservé à l'unité spéciale.", True),
    ("U4", "Unités", "Hôpital (corps médical)", "built", (38, 28), "milieu", 285, None, "Soins, médic-nin, relève des blessés."),
    ("F1", "Formation", "Académie", "built", (40, 28), "milieu", 265, None, "Formation des élèves, cour intérieure."),
    ("F2", "Formation", "Terrain d'entraînement", "zone", (42, 34), "milieu", 243, None, "Poteaux, cibles, espace pour les duels d'entraînement."),
    ("D1", "Domaines", "Domaine (est)", "domain", (32, 26), "milieu", 232, None, "Grande propriété murée pour un clan ou une famille importante."),
    ("D2", "Domaines", "Domaine (ouest)", "domain", (32, 26), "milieu", 308, None, "Grande propriété murée pour un clan ou une famille importante."),
    ("D3", "Domaines", "Domaine caché (nord)", "domain", (28, 22), "milieu", 272, None, "Contre la limite du niveau, accès par une seule ruelle.", True),
    ("D4", "Domaines", "Domaine (sud-est)", "domain", (30, 24), "milieu", 295, None, "Grande propriété murée."),
    ("D5", "Domaines", "Domaine caché (sud-ouest)", "domain", (26, 20), "milieu", 256, None, "Peu visible depuis les rues.", True),
    ("M1", "Métiers", "Boutique officielle du village", "built", (20, 16), "bas", 258, 440, "La boutique du village (prix fixés par le staff)."),
    ("M2", "Métiers", "Forge et armurerie", "built", (24, 18), "bas", 295, None, "Fabrication et réparation d'armes (V2)."),
    ("M3", "Métiers", "Atelier de parchemins", "built", (20, 16), "bas", 240, None, "Fabrication de parchemins et sceaux (V2)."),
    ("M4", "Métiers", "Herboristerie", "built", (18, 14), "bas", 234, None, "Ingrédients médicaux, plantes récoltées dans le pays."),
    ("M5", "Métiers", "Comptoir des ressources", "built", (24, 18), "bas", 302, None, "Achat et vente des ressources récoltées (minerai, bois, plantes)."),
    ("M6", "Métiers", "Banque du village", "built", (20, 18), "bas", 280, None, "Coffres des habitants. Lieu sensible pour le RP."),
    ("V1", "Vie du village", "Maison de thé", "built", (16, 14), "bas", 250, None, "Lieu de rencontre et de discussion RP."),
    ("V2", "Vie du village", "Auberge des voyageurs", "built", (26, 18), "bas", 288, 470, "Pour les étrangers et les voyageurs de passage."),
    ("V4", "Vie du village", "Volière des messagers", "built", (16, 16), "bas", 240, 480, "Oiseaux messagers vers les autres villages."),
    ("V5", "Vie du village", "Mémorial et cimetière", "zone", (34, 24), "bas", 306, 480, "Noms des ninjas tombés. Cérémonies RP."),
]

# Éléments propres à chaque village. `fixed` : position donnée ; `extra` : placés automatiquement (même format que COMMON).
VILLAGES = {
    "konoha": dict(status="fait", house_style="square", houses=(32, 30, 28), fixed=[
        ("A1", "Défense", "Enceinte", "wall", RING(0, 0, 500, 3, 225, 315), "bas", "Mur de 16 m, ouvert seulement à la grande porte."),
        ("A2", "Défense", "Grande porte", "tower", R(0, -495, 47.5, 6), "bas", "Grande porte face au centre du monde."),
        ("A3", "Défense", "Poste de garde", "built", R(45, -480, 20, 12), "bas", "Contrôle des entrées."),
        ("P1", "Pouvoir", "Tour du Hokage", "tower", C(0, -100, 19), "haut", "Bureau du Hokage."),
        ("P5", "Pouvoir", "Falaise des Kage", "mountain", R(0, -35, 85, 30), "haut", "Grande falaise dans le relief, derrière la tour."),
        ("P6", "Pouvoir", "Emplacements des visages", "mark", R(0, -70, 50, 5), "haut", "5 visages vides pour les Kage élus par les joueurs."),
        ("M7", "Métiers", "Échoppes (côté gauche)", "built", R(-21, -470, 12, 30), "bas", "Petites boutiques le long de la rue commerçante."),
        ("M8", "Métiers", "Échoppes (côté droit)", "built", R(21, -470, 12, 30), "bas", "Petites boutiques le long de la rue commerçante."),
    ], extra=[
        ("V3", "Vie du village", "Échoppe de ramen", "tower", (12, 10), "bas", 255, 450, "Le restaurant préféré des ninjas."),
        ("V7", "Vie du village", "Parc", "park", 30, "bas", 232, None, "Arbres et bancs."),
        ("F3", "Formation", "Arène des examens chûnin", "rock2", 34, "bas", 270, 380, "Examens de grade et duels encadrés."),
    ]),
    "suna": dict(status="propose", house_style="dome", houses=(18, 18, 16), fixed=[
        ("A1", "Défense", "Rempart de falaises", "rock", RING(0, 0, 500, 30, 225, 315), "bas", "Falaises naturelles en cratère, ouvertes par la faille."),
        ("A2", "Défense", "Faille d'entrée (grande porte)", "road", R(0, -480, 22, 36), "bas", "Passage étroit dans la falaise, face au centre du monde."),
        ("A3", "Défense", "Poste de garde", "built", R(45, -480, 18, 12), "bas", "Contrôle des entrées."),
        ("P1", "Pouvoir", "Tour du Kazekage", "tower", C(0, -100, 28), "haut", "Grand bâtiment rond à dôme."),
        ("P5", "Pouvoir", "Mur des Kazekage", "mark", R(0, -60, 56, 6), "haut", "5 emblèmes vides pour les Kazekage élus."),
        ("M7", "Métiers", "Marché couvert (gauche)", "built", R(-19, -470, 12, 30), "bas", "Échoppes sous toiles le long de l'avenue."),
        ("M8", "Métiers", "Marché couvert (droite)", "built", R(19, -470, 12, 30), "bas", "Échoppes sous toiles le long de l'avenue."),
    ], extra=[
        ("V8", "Vie du village", "Réservoir d'eau", "water", 22, "bas", 236, None, "La réserve d'eau du village (l'eau est rare)."),
        ("M9", "Métiers", "Ateliers de marionnettistes", "built", (28, 20), "bas", 296, 400, "Fabrication de marionnettes de combat."),
        ("F3", "Formation", "Arène des examens", "rock2", 28, "bas", 270, 380, "Examens de grade et duels encadrés."),
    ]),
    "kumo": dict(status="propose", house_style="square", houses=(16, 16, 15), fixed=[
        ("A1", "Défense", "Pics rocheux", "mountain", RING(0, 0, 500, 40, 225, 315), "bas", "Pics de montagne qui forment un rempart naturel."),
        ("A2", "Défense", "Col d'entrée (grande porte)", "road", R(0, -478, 18, 40), "bas", "Seul passage entre les pics, pont de pierre."),
        ("A3", "Défense", "Poste de garde", "built", R(45, -480, 18, 12), "bas", "Contrôle des entrées."),
        ("P1", "Pouvoir", "Tour du Raikage", "tower", C(0, -95, 26), "haut", "Sur le pic le plus haut."),
        ("P5", "Pouvoir", "Sommet des Raikage", "mark", R(0, -60, 50, 8), "haut", "5 socles de statues vides pour les Raikage élus."),
    ], extra=[
        ("U5", "Unités", "Grande caserne", "built", (34, 44), "milieu", 300, None, "Village militaire : dortoirs et salle de briefing des unités."),
        ("V3", "Vie du village", "Bains chauds", "water", 20, "bas", 300, 380, "Source chaude, lieu de repos et de RP."),
        ("F3", "Formation", "Arène des examens", "rock2", 28, "bas", 270, 380, "Examens de grade et duels encadrés."),
    ]),
}

# Sorties cachées X1 / X2 : (angle, rayon) hors rempart, loin de la route de la porte (axe 270°)
SORTIES = {"X1": (240, 650), "X2": (288, 700)}

def pts(s, step=4):
    """Points d'échantillonnage d'une forme de surface (bords + intérieur), repère local."""
    t = s["t"]
    if t == "rect":
        nx, ny = max(1, math.ceil(s["w"] / step)), max(1, math.ceil(s["h"] / step))
        return [(s["x"] - s["w"] / 2 + s["w"] * i / nx, s["y"] - s["h"] / 2 + s["h"] * j / ny) for i in range(nx + 1) for j in range(ny + 1)]
    if t == "circle": cx, cy, r = s["x"], s["y"], s["r"]
    elif t == "houses": cx, cy, r = s["zx"], s["zy"], s["zr"]
    elif t == "ring": cx, cy, r = s["x"], s["y"], s["r"] + s["w"] / 2
    else: return []
    return [(cx, cy)] + [(cx + k * r * math.cos(math.radians(a)), cy + k * r * math.sin(math.radians(a))) for k in (0.5, 1) for a in range(0, 360, 30)]

def ramp_dist(v, x, y):
    """Distance (m) de (x, y) à l'axe de la rampe la plus proche (segment radial centré sur chaque frontière de niveau)."""
    hw = 22.5 if v == "kumo" else 12.5
    best = 1e9
    for a in RAMPS[v]:
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        for rb in (TIERS["milieu"][0], TIERS["bas"][0]):
            t = min(rb + hw, max(rb - hw, x * ca + y * sa))  # projection bornée sur le segment
            best = min(best, math.hypot(x - t * ca, y - t * sa))
    return best

def footprint(s, margin):
    """Boîtes / cercles occupés par une forme (pour les tests de chevauchement)."""
    t = s["t"]
    if t == "rect": return [("b", s["x"], s["y"], s["w"] / 2 + margin, s["h"] / 2 + margin)]
    if t == "circle": return [("c", s["x"], s["y"], s["r"] + margin)]
    if t == "houses": return [("c", s["zx"], s["zy"], s["zr"] + margin)]
    if t == "ring":
        if s.get("fill"): return [("c", s["x"], s["y"], s["r"] + s["w"] / 2 + margin)]
        return []  # rempart : géré par les rayons autorisés
    if t == "line":
        out = []; p = s["pts"]
        for i in range(len(p) - 1):
            (x1, y1), (x2, y2) = p[i], p[i + 1]; L_ = math.hypot(x2 - x1, y2 - y1); k = max(1, math.ceil(L_ / 4))
            for j in range(k + 1): out.append(("c", x1 + (x2 - x1) * j / k, y1 + (y2 - y1) * j / k, s["w"] / 2 + margin))
        return out
    return []

def hit(a, b):
    if a[0] == "b" and b[0] == "b": return abs(a[1] - b[1]) < a[3] + b[3] and abs(a[2] - b[2]) < a[4] + b[4]
    if a[0] == "c" and b[0] == "c": return math.hypot(a[1] - b[1], a[2] - b[2]) < a[3] + b[3]
    bx, cx = (a, b) if a[0] == "b" else (b, a)
    dx = max(abs(cx[1] - bx[1]) - bx[3], 0); dy = max(abs(cx[2] - bx[2]) - bx[4], 0)
    return math.hypot(dx, dy) < cx[3]

def ang_of(x, y): return math.degrees(math.atan2(y, x)) % 360

def place(v, tier, ang, rr, hidden, occupied, mk):
    """Position valide la plus proche de la cible (grille de 3 m) ; mk(x, y) construit la forme."""
    lo, hi = (HAUT_R0 if tier == "haut" else TIERS[tier][0] + 1), TIERS[tier][1] - (4 if tier == "bas" else 1)
    ok = lambda px, py: lo <= math.hypot(px, py) <= hi and ANG_MIN <= ang_of(px, py) <= ANG_MAX
    tx, ty = pol(rr if rr else (hi - 10 if hidden else (lo + hi) / 2), ang)
    cands = sorted((math.hypot(x - tx, y - ty), x, y) for x in range(-500, 501, 3) for y in range(-500, 1, 3) if ok(x, y))
    for _, x, y in cands:
        s = mk(x, y)
        if not all(ok(px, py) and ramp_dist(v, px, py) >= RAMPE_MARGE for px, py in pts(s)): continue
        if any(hit(f, o) for f in footprint(s, 4) for o in occupied): continue
        return s
    return None

all_docs = []
for v, cfg in VILLAGES.items():
    docs = []
    def add(code, group, name, fill, shape, desc, level, layer="surface", hidden=False, door=None):
        d = {"village": v, "code": code, "name": name, "group": group, "status": cfg["status"], "fill": fill,
             "desc": desc, "order": len(docs), "hidden": hidden, "shape": shape, "level": level, "layer": layer}
        if door: d["door"] = door
        docs.append(d)
    for code, group, name, fill, shape, level, desc in cfg["fixed"]: add(code, group, name, fill, shape, desc, level)
    # V6 fixée (arbitrage R1) ; la rue commerçante part de la grande porte et reste dégagée jusqu'à la frontière du niveau
    add("V6", "Vie du village", "Place d'apparition", "zone", C(*pol(420, 270), 30), "Les joueurs de " + v.capitalize() + " apparaissent ici.", "bas")
    add("R1", "Routes", "Rue commerçante", "road", L([(0, -499), (0, -345)], 20 if v == "konoha" else 16), "De la grande porte à la place.", "bas")
    occupied = [f for d in docs for f in footprint(d["shape"], 0)]
    for item in COMMON + cfg["extra"]:
        code, group, name, fill, size, tier, ang, rr, desc = item[:9]
        hidden = len(item) > 9 and item[9]
        if v == "kumo" and code == "U1": name, desc = "Poste de la garde", "Unité de garde (la grande caserne est à côté)."
        if isinstance(size, tuple): mk = lambda x, y, size=size: R(x, y, *size)
        elif code == "F3": mk = lambda x, y, size=size: RING(x, y, size, 6, 0, 360, True)
        else: mk = lambda x, y, size=size: C(x, y, size)
        s = place(v, tier, ang, rr, hidden, occupied, mk)
        if not s: print(f"!! {v} {code} {name} : pas de place"); continue
        occupied += footprint(s, 0)
        add(code, group, name, fill, s, desc, tier, hidden=hidden)
    # Quartiers d'habitation du niveau milieu (cercles de maisons ; Q pour ne pas confondre avec le passage caché H1)
    for i, (n, a) in enumerate(zip(cfg["houses"], (246, 270, 294))):
        s = place(v, "milieu", a, 255, False, occupied, lambda x, y: H(n, cfg["house_style"], x, y, 25))
        if not s: print(f"!! {v} Q{i + 1} : pas de place"); continue
        occupied += footprint(s, 0)
        add(f"Q{i + 1}", "Habitants", f"Quartier d'habitation {i + 1}", "built", s, "Maisons d'habitants.", "milieu")

    # ── Souterrains (formes en mètres locaux ; la profondeur est gérée en Task 3)
    by = {d["code"]: d for d in docs}
    def sous(code, name, shape, desc, level, door=None):
        add(code, "Souterrains", name, "built", shape, desc, level, "souterrain", True, door)
    def salle(over, code, name, desc, rule, domain=None):
        sh = by[over]["shape"]
        cx, cy = (sh["x"], sh["y"]) if "x" in sh else (sh["zx"], sh["zy"])
        w, h = (min(sh["w"], 20), min(sh["h"], 16)) if sh["t"] == "rect" else (14, 12)
        sous(code, name, R(cx, cy, w, h), desc, by[over]["level"], {"rule": rule, "domain": domain, "oneWay": False})
        return (cx, cy)
    p1 = salle("P1", "S1", "Archives interdites", "Sous le palais. Réservé au Kage.", "Kage")
    u3 = salle("U3", "S2", "Salle de l'unité spéciale", "Sous le QG de l'unité spéciale.", "SpecialUnit")
    salle("U2", "S3", "Cellules", "Cellules profondes sous le QG de la police.", "Police")
    for i, dom in enumerate(sorted(c for c in by if c.startswith("D"))):
        salle(dom, f"S{4 + i}", f"Refuge ({dom})", "Refuge de clan sous le domaine.", "Clan", f"{v}-{dom}")
    for k, src, rule, lvl in (("1", p1, "Kage", "haut"), ("2", u3, "SpecialUnit", "milieu")):
        sx, sy = pol(SORTIES["X" + k][1], SORTIES["X" + k][0])
        sous("T" + k, "Tunnel " + k, L([src, (sx, sy)], 3), "Tunnel secret vers une sortie cachée hors du rempart.", lvl,
             {"rule": rule, "domain": None, "oneWay": True})
        add("X" + k, "Souterrains", "Sortie cachée " + k, "built", C(sx, sy, 3), "Sortie du tunnel, dans le pays, hors du rempart.", "bas", hidden=True)
    sous("H1", "Passage caché", L([pol(325, 236), pol(355, 238)], 3), "Passage ouvert entre le niveau bas et le niveau milieu.", "milieu")
    all_docs += docs
    print(f"{v} : {len(docs)} éléments")

for f in os.listdir(out_dir):
    if f.endswith(".json"): os.remove(os.path.join(out_dir, f))
for d in all_docs:
    json.dump(d, open(os.path.join(out_dir, f'{d["village"]}-{d["code"]}.json'), "w", encoding="utf-8"), ensure_ascii=False)
print(len(all_docs), "documents")
