# Plans v2 des villages (Konoha, Suna, Kumo) : bâtiments RP, routes, quartiers aux 4 coins, domaines (dont cachés).
# Écrit un JSON par élément dans <sortie>/ (format de la collection `elements` de la page des plans).
# Les éléments « fixes » ont une position donnée ; les autres sont placés automatiquement sans chevauchement,
# au plus près d'un angle préféré (90° = fond du village, 270° = grande porte).
# Usage : python tools/plans-villages-v2.py <sortie>
import json, math, os, sys

out_dir = sys.argv[1]
os.makedirs(out_dir, exist_ok=True)

def pol(r, a):
    return (round(r * math.cos(math.radians(a)), 1), round(r * math.sin(math.radians(a)), 1))
def C(x, y, r): return {"t": "circle", "x": x, "y": y, "r": r}
def R(x, y, w, h, rot=0): return {"t": "rect", "x": x, "y": y, "w": w, "h": h, "rot": rot}
def RING(x, y, r, w, a0, a1, fill=False): return {"t": "ring", "x": x, "y": y, "r": r, "w": w, "a0": a0, "a1": a1, "fill": fill}
def L(pts, w): return {"t": "line", "pts": [list(p) for p in pts], "w": w}
def H(n, style, zx, zy, zr): return {"t": "houses", "n": n, "style": style, "zx": zx, "zy": zy, "zr": zr}

# ── Bâtiments RP communs : code, groupe, nom, couleur, largeur, profondeur, zone ("in" = dans le boulevard, "out" = entre boulevard et rempart), angle, description
COMMON = [
    ("P2", "Pouvoir", "Salle du conseil", "built", 34, 22, "in", 120, "Réunions du Kage, du conseil et des chefs d'unité."),
    ("P3", "Pouvoir", "Bureau des missions", "built", 30, 20, "in", 60, "PNJ des missions D et C, tableau des missions B à S données en RP."),
    ("P4", "Pouvoir", "Archives et bibliothèque", "built", 26, 20, "in", 155, "Parchemins, registres des ninjas, savoir du village."),
    ("U1", "Unités", "Caserne de la garde", "built", 36, 24, "out", 245, "Unité de garde : relèves aux portes et rondes."),
    ("U2", "Unités", "QG de la police et prison", "built", 38, 26, "out", 295, "Police du village, cellules pour les arrestations RP."),
    ("U3", "Unités", "QG de l'unité spéciale (caché)", "built", 24, 18, "out", 140, "Entrée discrète au fond d'une ruelle. Réservé à l'unité spéciale.", True),
    ("U4", "Unités", "Hôpital (corps médical)", "built", 38, 28, "in", 20, "Soins, médic-nin, relève des blessés."),
    ("F1", "Formation", "Académie", "built", 40, 28, "out", 20, "Formation des élèves, cour intérieure."),
    ("F2", "Formation", "Terrain d'entraînement", "zone", 42, 34, "out", 340, "Poteaux, cibles, espace pour les duels d'entraînement."),
    ("M1", "Métiers", "Boutique officielle du village", "built", 20, 16, "in", 245, "La boutique du village (prix fixés par le staff)."),
    ("M2", "Métiers", "Forge et armurerie", "built", 24, 18, "in", 290, "Fabrication et réparation d'armes (V2)."),
    ("M3", "Métiers", "Atelier de parchemins", "built", 20, 16, "in", 200, "Fabrication de parchemins et sceaux (V2)."),
    ("M4", "Métiers", "Herboristerie", "built", 18, 14, "in", 40, "Ingrédients médicaux, plantes récoltées dans le pays."),
    ("M5", "Métiers", "Comptoir des ressources", "built", 24, 18, "in", 330, "Achat et vente des ressources récoltées (minerai, bois, plantes)."),
    ("M6", "Métiers", "Banque du village", "built", 20, 18, "in", 85, "Coffres des habitants. Lieu sensible pour le RP."),
    ("V1", "Vie du village", "Maison de thé", "built", 16, 14, "in", 220, "Lieu de rencontre et de discussion RP."),
    ("V2", "Vie du village", "Auberge des voyageurs", "built", 26, 18, "out", 222, "Pour les étrangers et les voyageurs de passage."),
    ("V4", "Vie du village", "Volière des messagers", "built", 16, 16, "out", 75, "Oiseaux messagers vers les autres villages."),
    ("V5", "Vie du village", "Mémorial et cimetière", "zone", 34, 24, "out", 105, "Noms des ninjas tombés. Cérémonies RP."),
    ("D1", "Domaines", "Domaine (est)", "domain", 32, 26, "out", 35, "Grande propriété murée pour un clan ou une famille importante."),
    ("D2", "Domaines", "Domaine (ouest)", "domain", 32, 26, "out", 165, "Grande propriété murée pour un clan ou une famille importante."),
    ("D3", "Domaines", "Domaine caché (nord)", "domain", 28, 22, "out", 118, "Derrière les autres bâtiments, contre le rempart, accès par une seule ruelle.", True),
    ("D4", "Domaines", "Domaine (sud-est)", "domain", 30, 24, "out", 318, "Grande propriété murée, près de la porte."),
    ("D5", "Domaines", "Domaine caché (sud-ouest)", "domain", 26, 20, "out", 203, "Coincé entre le rempart et l'arène, peu visible depuis les rues.", True),
]

VILLAGES = {
    "konoha": dict(status="fait", inner=212, ring_r=150, house_style="square", houses=(32, 30, 28, 28), house_angles=(55, 125, 235, 305),
        skip=set(), fixed=[
        ("A1", "Défense", "Enceinte", "wall", RING(0, 0, 225, 3, -80, 260), "Mur rond de 16 m, ouvert seulement à la grande porte."),
        ("A2", "Défense", "Grande porte", "tower", R(0, -225, 47.5, 6), "Grande porte face au centre du monde."),
        ("A3", "Défense", "Poste de garde", "built", R(37.5, -197.5, 22.5, 12.5), "Contrôle des entrées."),
        ("P1", "Pouvoir", "Tour du Hokage", "tower", C(130, 0, 19), "Bureau du Hokage."),
        ("P5", "Pouvoir", "Falaise des Kage", "mountain", R(267.5, 0, 85, 350), "Grande falaise derrière la tour, hors de l'enceinte."),
        ("P6", "Pouvoir", "Emplacements des visages", "mark", R(228, 0, 6, 237.5), "5 visages vides pour les Kage élus par les joueurs."),
        ("V6", "Vie du village", "Place centrale (apparition)", "zone", R(0, 0, 70, 70), "Les joueurs de Konoha apparaissent ici."),
        ("R1", "Routes", "Rue commerçante", "road", L([(0, -224), (0, -35)], 20), "De la grande porte à la place."),
        ("R3", "Routes", "Avenue de la tour", "road", L([(35, 0), (111, 0)], 18), "De la place à la tour du Hokage."),
        ("W1", "Routes", "Rivière", "water", L([(-87.5, -222), (-87.5, 222)], 16), "Traverse le village, ponts aux croisements."),
        ("M7", "Métiers", "Échoppes (côté gauche)", "built", R(-21, -125, 12, 140), "Petites boutiques le long de la rue commerçante."),
        ("M8", "Métiers", "Échoppes (côté droit)", "built", R(21, -125, 12, 140), "Petites boutiques le long de la rue commerçante."),
        ("V3", "Vie du village", "Échoppe de ramen", "tower", R(-21, -45, 12, 10), "Le restaurant préféré des ninjas."),
        ("V7", "Vie du village", "Parc", "park", C(-45, 140, 30), "Arbres et bancs."),
        ("F3", "Formation", "Arène des examens chûnin", "rock2", RING(-150, -70, 34, 6, -80, 260, True), "Examens de grade et duels encadrés."),
    ]),
    "suna": dict(status="propose", inner=213, ring_r=140, house_style="dome", houses=(18, 18, 16, 16), house_angles=(58, 122, 238, 302),
        skip=set(), fixed=[
        ("A1", "Défense", "Rempart de falaises", "rock", RING(0, 0, 240, 30, -85, 265), "Falaises naturelles en cratère, ouvertes par la faille."),
        ("A2", "Défense", "Faille d'entrée (grande porte)", "road", R(0, -240, 22, 44), "Passage étroit dans la falaise, face au centre du monde."),
        ("A3", "Défense", "Poste de garde", "built", R(30, -202, 18, 12), "Contrôle des entrées."),
        ("P1", "Pouvoir", "Tour du Kazekage", "tower", C(0, 88, 28), "Grand bâtiment rond à dôme."),
        ("P5", "Pouvoir", "Mur des Kazekage", "mark", RING(0, 0, 218, 10, 72, 108), "5 emblèmes vides pour les Kazekage élus."),
        ("V6", "Vie du village", "Place du Vent (apparition)", "zone", C(0, 0, 30), "Les joueurs de Suna apparaissent ici."),
        ("R1", "Routes", "Avenue principale", "road", L([(0, -262), (0, -30)], 16), "De la faille à la place."),
        ("R3", "Routes", "Allée du Kazekage", "road", L([(0, 30), (0, 60)], 12), "De la place à la tour."),
        ("M7", "Métiers", "Marché couvert (gauche)", "built", R(-19, -95, 12, 95), "Échoppes sous toiles le long de l'avenue."),
        ("M8", "Métiers", "Marché couvert (droite)", "built", R(19, -95, 12, 95), "Échoppes sous toiles le long de l'avenue."),
        ("V8", "Vie du village", "Réservoir d'eau", "water", C(-178, 40, 22), "La réserve d'eau du village (l'eau est rare)."),
        ("M9", "Métiers", "Ateliers de marionnettistes", "built", R(160, 95, 28, 20), "Fabrication de marionnettes de combat."),
        ("F3", "Formation", "Arène des examens", "rock2", RING(-160, -80, 28, 6, 0, 360, True), "Examens de grade et duels encadrés."),
    ]),
    "kumo": dict(status="propose", inner=205, ring_r=130, house_style="square", houses=(16, 16, 15, 15), house_angles=(58, 122, 238, 302),
        skip=set(), fixed=[
        ("A1", "Défense", "Pics rocheux", "mountain", RING(0, 0, 235, 40, -82, 262), "Pics de montagne qui forment un rempart naturel."),
        ("A2", "Défense", "Col d'entrée (grande porte)", "road", R(0, -235, 18, 48), "Seul passage entre les pics, pont de pierre."),
        ("A3", "Défense", "Poste de garde", "built", R(28, -196, 18, 12), "Contrôle des entrées."),
        ("P1", "Pouvoir", "Tour du Raikage", "tower", C(0, 85, 26), "Sur le pic le plus haut."),
        ("P5", "Pouvoir", "Sommet des Raikage", "mark", R(0, 196, 80, 12), "5 socles de statues vides pour les Raikage élus."),
        ("V6", "Vie du village", "Esplanade (apparition)", "zone", C(0, 0, 28), "Les joueurs de Kumo apparaissent ici."),
        ("R1", "Routes", "Grand escalier", "road", L([(0, -258), (-22, -185), (18, -115), (0, -30)], 12), "Escalier en lacets du col à l'esplanade."),
        ("R3", "Routes", "Montée du Raikage", "road", L([(0, 28), (0, 59)], 10), "De l'esplanade à la tour."),
        ("U5", "Unités", "Grande caserne", "built", R(150, -60, 34, 44), "Village militaire : dortoirs et salle de briefing des unités."),
        ("V3", "Vie du village", "Bains chauds", "water", C(120, -140, 20), "Source chaude, lieu de repos et de RP."),
        ("F3", "Formation", "Arène des examens", "rock2", RING(-150, -100, 28, 6, 0, 360, True), "Examens de grade et duels encadrés."),
    ]),
}

def footprint(s, margin):
    """Boîtes / cercles occupés par une forme (pour les tests de chevauchement)."""
    t = s["t"]
    if t == "rect":
        if s.get("rot"):
            r = math.hypot(s["w"], s["h"]) / 2 + margin
            return [("c", s["x"], s["y"], r)]
        return [("b", s["x"], s["y"], s["w"] / 2 + margin, s["h"] / 2 + margin)]
    if t == "circle": return [("c", s["x"], s["y"], s["r"] + margin)]
    if t == "ring":
        if s.get("fill") or s["a1"] - s["a0"] >= 360: return [("c", s["x"], s["y"], s["r"] + s["w"] / 2 + margin)]
        return []  # rempart / boulevard : géré par les rayons autorisés
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

all_docs = []
for v, cfg in VILLAGES.items():
    docs = []
    def add(code, group, name, fill, shape, desc, hidden=False):
        docs.append({"village": v, "code": code, "name": name, "group": group, "status": cfg["status"], "fill": fill,
                     "desc": desc, "order": len(docs), "hidden": hidden, "shape": shape})
    for f in cfg["fixed"]: add(*f)
    rr, inner = cfg["ring_r"], cfg["inner"]
    # Routes : boulevard circulaire, 4 rues en diagonale, 4 ruelles vers les coins, ruelles cachées vers les lieux cachés.
    add("R2", "Routes", "Boulevard circulaire", "road", RING(0, 0, rr, 10, 0, 360), "Fait le tour du centre du village.")
    for i, a in enumerate((45, 135, 225, 315)):
        add(f"R{4 + i}", "Routes", f"Rue diagonale {('nord-est', 'nord-ouest', 'sud-ouest', 'sud-est')[i]}", "road",
            L([pol(36, a), pol(rr, a)], 9), "Relie la place au boulevard.")
        add(f"R{8 + i}", "Routes", f"Ruelle vers le quartier {('nord-est', 'nord-ouest', 'sud-ouest', 'sud-est')[i]}", "road",
            L([pol(rr + 5, a), pol(inner - 10, a)], 6), "Dessert les maisons du coin.")
    occupied = []
    for d in docs:
        if d["code"] in ("R2",): continue
        occupied += footprint(d["shape"], 0)
    ring_band = (rr - 5, rr + 5)

    # Placement automatique des bâtiments communs
    for item in COMMON:
        code, group, name, fill, w, h, zone, ang, desc = item[:9]
        hidden = len(item) > 9 and item[9]
        if code in cfg["skip"]: continue
        if v == "kumo" and code == "U1": name, desc = "Poste de la garde", "Unité de garde (la grande caserne est à côté)."
        lo, hi = (34, ring_band[0] - 4) if zone == "in" else (ring_band[1] + 4, inner)
        target_r = (lo + hi) / 2 if not hidden else hi - 10
        tx, ty = pol(target_r, ang)
        best = None
        for gx in range(-int(inner), int(inner) + 1, 2):
            for gy in range(-int(inner), int(inner) + 1, 2):
                # tous les coins dans l'anneau autorisé
                corners = [math.hypot(gx + sx * w / 2, gy + sy * h / 2) for sx in (-1, 1) for sy in (-1, 1)]
                near = min(math.hypot(gx + sx * w / 2, gy) for sx in (-1, 1)) if False else None
                if max(corners) > hi or min(corners) < lo: continue
                # pas sur le boulevard
                if zone == "in" and max(corners) > ring_band[0] - 2: continue
                dist = math.hypot(gx - tx, gy - ty)
                if best and dist >= best[0]: continue
                fp = ("b", gx, gy, w / 2 + 4, h / 2 + 4)
                if any(hit(fp, o) for o in occupied): continue
                best = (dist, gx, gy)
        if not best:
            print(f"!! {v} {code} {name} : pas de place"); continue
        shape = R(best[1], best[2], w, h)
        add(code, group, name, fill, shape, desc, hidden)
        occupied += footprint(shape, 0)
        if hidden:  # ruelle cachée : du bâtiment vers le boulevard
            a = math.degrees(math.atan2(best[2], best[1]))
            add(f"{code}r", "Routes", f"Ruelle cachée ({name.split(' (')[0].lower()})", "road",
                L([pol(rr + 5, a), pol(math.hypot(best[1], best[2]) - max(w, h) / 2, a)], 4), "Passage étroit, peu visible.")
    # Quartiers d'habitation aux 4 coins (+ quelques maisons isolées au centre)
    names = ("nord-est", "nord-ouest", "sud-ouest", "sud-est")
    for i, (n, a) in enumerate(zip(cfg["houses"], cfg["house_angles"])):
        zx, zy = pol((rr + inner) / 2 + 3, a)
        add(f"H{i + 1}", "Habitants", f"Quartier {names[i]}", "built", H(n, cfg["house_style"], zx, zy, (inner - rr) / 2 + 22), "Maisons d'habitants, réparties dans l'espace libre du coin.")
    add("H5", "Habitants", "Maisons du centre", "built", H(10, cfg["house_style"], 0, 0, rr - 8), "Quelques maisons entre les bâtiments du centre.")
    all_docs += docs
    print(f"{v} : {len(docs)} éléments")

for f in os.listdir(out_dir):
    if f.endswith(".json"): os.remove(os.path.join(out_dir, f))
for d in all_docs:
    json.dump(d, open(os.path.join(out_dir, f'{d["village"]}-{d["code"]}.json'), "w", encoding="utf-8"), ensure_ascii=False)
print(len(all_docs), "documents")
