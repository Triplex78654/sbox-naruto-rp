# Carte de hauteurs : relief des villages (3 etages, buttes et creux doux), relief de fond par biome (rivieres, cascades, ponts,
# routes aplanies), bord infranchissable.
# Ecrit tools/out/relief.u16, holes.u8, sols.u8 (biome du sol), relief.json pour le composant Terrain de s&box.
# Usage : python tools/relief.py [<dossier plans>] ; ordre : plans-villages-v3.py <dossier> -> plan-vers-blocage.py <dossier>
# -> relief.py <dossier> (lit trous.holes et souterrains.emprises de CE dossier ; sans dossier, il refait les deux etapes).
# Decor pose dans un village hors des emprises des plans : appeler charger_emprises(<dossier>) avant height_at (bosses).
import json
import math
import os
import sys
from array import array

from carte import (BIOME, CORNERS, FONDU_M, HALF, INNER, M, PAYS_R, RAMPS, RAYON_REMPART_M, SORTIES, TIER_Z, TIERS,
                   biome_poids, local_to_world, territoire, world_to_local)

RES = 2048
TERRAIN_Z = -2000      # le terrain est pose a z = -2000 : hauteur stockee 2000 = z monde 0
TERRAIN_HEIGHT = 40000
SIZE = 2 * HALF

# Trous du terrain : un rectangle le long de chaque puits (sorties X1 / X2, entrees des salles S*, bouts de H1).
# {v, code, x, y (monde, centre), ux, uy (direction monde de la montee)} ; remplis par charger_trous().
HOLES = []
PUITS_PENTE = math.tan(math.radians(25))  # pente du puits (marchable : <= 35 deg)
PUITS_FIN = 100     # le sol du puits atteint z = 0 a 100 u apres le centre de la sortie
PUITS_LARG = 300    # largeur interieure du puits (u) : plus large que le trou, qui est decoupe par cases de 55 u
TROU_LARG = 160     # largeur du trou (u)
TROU_T0, TROU_T1 = -170, 110  # etendue du trou le long du puits (u, 0 = centre de la sortie)


def sol_puits(t, zs=0.0):
    # z du sol du puits a la distance t (u) du centre du trou, vers l'exterieur ; zs = z monde de la surface au trou
    return zs + (t - PUITS_FIN) * PUITS_PENTE


def charger_emprises(plans_dir):
    """Active les bosses des villages : sol plat sous les elements de surface des plans v3 (hors rempart A1 et sorties X*)
    et au-dessus des souterrains (souterrains.emprises, ecrit par plan-vers-blocage.py). Distance a la plus proche
    emprise precalculee sur une grille de BOSSE_PAS m (repere local du village)."""
    formes = {}
    for f in os.listdir(plans_dir):
        if f.endswith(".json"):
            d = json.load(open(os.path.join(plans_dir, f), encoding="utf-8"))
            if d["layer"] == "surface" and d["code"] != "A1" and d["code"][0] != "X":
                formes.setdefault(d["village"], []).append(d["shape"])
    for v, lst in json.load(open(os.path.join(plans_dir, "souterrains.emprises"))).items():
        formes.setdefault(v, []).extend(lst)
    _DIST_BOSSE.clear()
    portee = BOSSE_MARGE_M + BOSSE_FONDU_M
    for v, lst in formes.items():
        g = array("d", [portee] * (_NX * _NY))
        for s in lst:
            x0, y0, x1, y1 = _bbox(s)
            for j in range(max(0, int((y0 - portee + R_V) / BOSSE_PAS)), min(_NY, int((y1 + portee + R_V) / BOSSE_PAS) + 2)):
                for i in range(max(0, int((x0 - portee + R_V) / BOSSE_PAS)), min(_NX, int((x1 + portee + R_V) / BOSSE_PAS) + 2)):
                    k = j * _NX + i
                    d = _dist_emprise(s, i * BOSSE_PAS - R_V, j * BOSSE_PAS - R_V)
                    if d < g[k]:
                        g[k] = d
        _DIST_BOSSE[v] = g


def oublier_emprises():
    # retour au relief sans bosses (celui sur lequel plan-vers-blocage.py calcule)
    _DIST_BOSSE.clear()


def charger_trous(plans_dir):
    # trous ecrits par plan-vers-blocage.py (sorties X*, entrees des salles S*, bouts de H1) : {v, code, x, y, ux, uy, zs, tier}
    HOLES.clear()
    HOLES.extend(json.load(open(os.path.join(plans_dir, "trous.holes"))))


BORD_Z = {"konoha": 2400, "suna": 2000, "kumo": 6000, "libre": 2000}  # sommet du bord (z monde)
BORD_MONTEE = 1200     # montee du bord : 30 m
TAN_BORD = math.tan(math.radians(55))
BORD_LISSAGE = 3000    # (u) douceur du changement de style entre deux coins
RAMPE_LARG = (4.0, 6.0)  # demi-largeur pleine / nulle d'une rampe (m)
VILLAGES = [v for v in CORNERS if v in TIER_Z]
RMAX = TIERS["bas"][0] + 25  # au-dela, village forcement a l'etage bas (marge Kumo 22.5 m) ; suit TIERS (tache 3)


def _lisse(t):
    t = 0.0 if t < 0 else 1.0 if t > 1 else t
    return t * t * (3 - 2 * t)


def _hauteur_village(v, x, y):
    # hauteur d'etage a (x, y) monde, pour un des trois villages
    bas, mil, haut = TIER_Z[v]
    cx, cy = CORNERS[v]
    dx, dy = (x - cx) / M, (y - cy) / M
    if dx * dx + dy * dy > RMAX * RMAX:
        return bas
    xm, ym = world_to_local(v, x, y)
    r = math.hypot(xm, ym)
    ang = math.degrees(math.atan2(ym, xm)) % 360
    # distance laterale a la rampe la plus proche
    lat = 1e9
    for a in RAMPS[v]:
        d = math.radians(ang - a)
        lat = min(lat, r * abs(math.sin(d)) if math.cos(d) > 0 else r)
    m = 1 - _lisse((lat - RAMPE_LARG[0]) / (RAMPE_LARG[1] - RAMPE_LARG[0]))
    hw = 22.5 if v == "kumo" else 12.5   # rampe de 45 m (Kumo) ou 25 m
    h = bas
    for r0, dz in ((TIERS["bas"][0], mil - bas), (TIERS["milieu"][0], haut - mil)):
        marche = _lisse((r0 + 3 - r) / 6)                   # marche lissee sur 6 m
        rampe = min(1.0, max(0.0, (r0 + hw - r) / (2 * hw)))  # rampe lineaire
        h += dz * (marche + m * (rampe - marche))
    return h


# ── Buttes et creux doux dans les niveaux des villages : +/- BOSSE_M, nuls sous les emprises des plans et au-dessus des
# souterrains (marge BOSSE_MARGE_M), de part et d'autre des frontieres de niveau (marches, rampes, ponts de Kumo, murets
# de Suna) et pres du rempart. Sans charger_emprises, pas de bosses : plan-vers-blocage.py travaille ainsi (tout ce qu'il
# pose est sur une emprise ou dans une bande plate) ; tout autre appelant qui pose quelque chose dans un village les charge.
_DIST_BOSSE = {}       # village -> grille des distances (m) a la plus proche emprise (charger_emprises)
BOSSE_PAS = 2          # pas de la grille (m) : un point dans une emprise a ses 4 noeuds a < 3 m d'elle
BOSSE_M = 3.0          # amplitude max (m)
BOSSE_MARGE_M = 6      # sol plat autour de chaque emprise
BOSSE_FONDU_M = 20     # la bosse monte sur 20 m au-dela de la marge (pente < 35 deg)
BOSSE_BANDE_M = 50     # sol plat a moins de 50 m d'une frontiere de niveau ou du rempart


def _dist_emprise(s, x, y):
    # distance (m) de (x, y) a une forme des plans (0 dedans) ; rectangles sans rotation (aucun plan v3 n'en a)
    t = s["t"]
    if t == "rect":
        return math.hypot(max(abs(x - s["x"]) - s["w"] / 2, 0), max(abs(y - s["y"]) - s["h"] / 2, 0))
    if t == "circle":
        return max(0.0, math.hypot(x - s["x"], y - s["y"]) - s["r"])
    if t == "houses":
        return max(0.0, math.hypot(x - s["zx"], y - s["zy"]) - s["zr"])
    if t == "ring":   # anneau plein (arenes) : disque
        return max(0.0, math.hypot(x - s["x"], y - s["y"]) - s["r"] - s["w"] / 2)
    if t == "line":
        d = 1e9
        for (ax, ay), (bx, by) in zip(s["pts"], s["pts"][1:]):
            l2 = (bx - ax) ** 2 + (by - ay) ** 2
            k = 0.0 if l2 == 0 else max(0.0, min(1.0, ((x - ax) * (bx - ax) + (y - ay) * (by - ay)) / l2))
            d = min(d, math.hypot(x - ax - k * (bx - ax), y - ay - k * (by - ay)))
        return max(0.0, d - s["w"] / 2)
    return 1e9


def _bbox(s):
    t = s["t"]
    if t == "rect":
        return s["x"] - s["w"] / 2, s["y"] - s["h"] / 2, s["x"] + s["w"] / 2, s["y"] + s["h"] / 2
    if t == "line":
        xs, ys = [p[0] for p in s["pts"]], [p[1] for p in s["pts"]]
        return min(xs) - s["w"] / 2, min(ys) - s["w"] / 2, max(xs) + s["w"] / 2, max(ys) + s["w"] / 2
    x, y = (s["zx"], s["zy"]) if t == "houses" else (s["x"], s["y"])
    r = s["zr"] if t == "houses" else s["r"] + s.get("w", 0) / 2
    return x - r, y - r, x + r, y + r


def _bosses(v, xm, ym):
    """Bosse (u) au point local (xm, ym) m du village v."""
    g = _DIST_BOSSE.get(v)
    if g is None:
        return 0.0
    r = math.hypot(xm, ym)
    d = min(abs(r - TIERS["milieu"][0]), abs(r - TIERS["bas"][0]), R_V - r)
    m = _lisse((d - BOSSE_BANDE_M) / BOSSE_FONDU_M)
    if m == 0.0:
        return 0.0
    fx, fy = (xm + R_V) / BOSSE_PAS, (ym + R_V) / BOSSE_PAS
    i, j = min(int(fx), _NX - 2), min(int(fy), _NY - 2)
    fx, fy = fx - i, fy - j
    k = j * _NX + i
    d = (g[k] * (1 - fx) + g[k + 1] * fx) * (1 - fy) + (g[k + _NX] * (1 - fx) + g[k + _NX + 1] * fx) * fy
    m *= _lisse((d - BOSSE_MARGE_M) / BOSSE_FONDU_M)
    if m == 0.0:
        return 0.0
    ph = VILLAGES.index(v) * 2.3
    return BOSSE_M * M * m * (2 * _sinus3(3.5 * xm, 3.5 * ym, ph) - 1)   # longueurs d'onde ~ 90, 60, 40 m


def _cible_bord(x, y):
    # sommet du bord : melange doux des styles des coins (le plus proche domine)
    ds = {v: math.hypot(x - cx, y - cy) for v, (cx, cy) in CORNERS.items()}
    dmin = min(ds.values())
    ws = {v: math.exp(-(d - dmin) / BORD_LISSAGE) for v, d in ds.items()}
    return sum(BORD_Z[v] * w for v, w in ws.items()) / sum(ws.values())


# ── Relief de fond (hors villages, en metres) : un relief par biome, pondere par biome_poids.
# Nul dans les villages, sur les routes, autour des sorties cachees X* et au centre ; toujours >= 0 hors des eaux
# (les tunnels des sorties X* passent dessous : un creux leur enleverait de la couverture).
R_V = RAYON_REMPART_M                     # rayon du rempart (m)
_NX, _NY = 2 * R_V // BOSSE_PAS + 1, R_V // BOSSE_PAS + 1   # grille des bosses : x local de -R_V a R_V, y de -R_V a 0
FONDU_VILLAGE_M = 60                      # le fond s'efface sur 60 m autour du rempart...
MONTEE_M = 200                            # ... et son amplitude ne monte que sur 200 m (pentes < 35 deg)
TAPER_M = (PAYS_R - 300, PAYS_R - 50)     # amplitude ramenee a 0 avant l'arc du pays : frontieres douces, centre plat
ROUTE_LARG_U = 640
ROUTE_PLAT_M = ROUTE_LARG_U / M / 2 + 4   # chaussee + 4 m de chaque cote : plat a z = 0
ROUTE_FONDU_M = 150
X_PLAT_M, X_FONDU_M = 25, 100     # plat : trou, puits et bout du tunnel (le generateur y suppose z = 0)
PAYS_DU_BIOME = {"foret": "konoha", "desert": "suna", "roche": "kumo", "neutre": "libre"}


def _pol(a, r):
    return (r * math.cos(math.radians(a)), r * math.sin(math.radians(a)))


_X_LOC = [_pol(a, r) for a, r in SORTIES.values()]   # sorties cachees X1 / X2 (carte.SORTIES)


def _sinus3(X, Y, ph):
    # somme de 3 produits de sinus (longueurs d'onde ~320, 210, 140 m), ramenee dans [0, 1]
    s = (math.sin(0.0196 * X + ph) * math.sin(0.0171 * Y + 0.4 + ph)
         + 0.5 * math.sin(0.024 * X - 0.018 * Y + 2.1) * math.sin(0.018 * X + 0.024 * Y + 0.7 + ph)
         + 0.25 * math.sin(0.040 * X + 0.020 * Y + 4.0 + ph) * math.sin(-0.020 * X + 0.040 * Y + 1.9))
    return (s + 1.75) / 3.5


def _loi(b, X, Y):
    # amplitude selon la distance r (m) au coin du pays du biome : monte depuis le rempart, retombe avant l'arc
    cx, cy = CORNERS[PAYS_DU_BIOME[b]]
    r = math.hypot(X - cx / M, Y - cy / M)
    L = _lisse((TAPER_M[1] - r) / (TAPER_M[1] - TAPER_M[0]))
    if PAYS_DU_BIOME[b] in VILLAGES:
        L *= _lisse((r - R_V) / MONTEE_M)
    return L, r


def _relief_biome(b, X, Y):
    L, r = _loi(b, X, Y)
    if b == "foret":    # collines arrondies et vallons
        return 22 * L * _sinus3(X, Y, 0.0)
    if b == "neutre":   # ondulations faibles
        return 4 * L * _sinus3(0.8 * X, 0.8 * Y, 3.1)
    if b == "desert":   # dunes allongees (cretes ~ perpendiculaires au vent) + plateaux a bords raides
        u, w = 0.866 * X + 0.5 * Y, -0.5 * X + 0.866 * Y
        c = 0.5 + 0.5 * math.sin(0.0483 * u + 1.2 * math.sin(0.015 * w))
        h = 8 * L * c * c * (0.6 + 0.4 * math.sin(0.0105 * w + 1.0))
        for px, py, rp, hp in _PLATEAUX:
            d = math.hypot(X - px, Y - py)
            if d < rp + PLATEAU_BORD_M:
                h = max(h, hp * _lisse((rp + PLATEAU_BORD_M - d) / PLATEAU_BORD_M))
        return h
    # roche : grosses bosses, plus hautes vers le coin, et pics
    t = min(1.0, max(0.0, (r - R_V) / (PAYS_R - R_V)))
    h = 26 * (1.2 - 0.5 * t) * L * _sinus3(0.8 * X, 0.8 * Y, 1.7) ** 1.2
    for px, py, rk, z in PICS_KUMO:
        d = math.hypot(X - px / M, Y - py / M)
        if d < rk / M:
            h = max(h, z / M * _lisse((rk / M - d) / (rk / M - PIC_SOMMET_M)))
    return h


def _fond(x, y):
    """Relief de fond (u) en (x, y) monde dans la zone jouable, avant les eaux."""
    t = territoire(x, y)
    if t == "centre":
        return 0.0
    env = 1.0
    if t in VILLAGES:
        xm, ym = world_to_local(t, x, y)
        r = math.hypot(xm, ym)
        if r <= R_V:
            return 0.0
        env = _lisse((r - R_V) / FONDU_VILLAGE_M)
        # route : axe local x = 0 (angle 270), de la porte a l'arc du pays
        d = abs(xm) if -PAYS_R <= ym <= -R_V else min(math.hypot(xm, ym + R_V), math.hypot(xm, ym + PAYS_R))
        env *= _lisse((d - ROUTE_PLAT_M) / ROUTE_FONDU_M)
        for sx, sy in _X_LOC:
            env *= _lisse((math.hypot(xm - sx, ym - sy) - X_PLAT_M) / X_FONDU_M)
        if env == 0.0:
            return 0.0
    X, Y = x / M, y / M
    if _loin_frontiere(X, Y):
        h = _relief_biome(BIOME[t], X, Y)
    else:
        h = sum(w * _relief_biome(b, X, Y) for b, w in biome_poids(x, y).items() if w > 0)
    return env * h * M


def _loin_frontiere(X, Y):
    """Vrai si (X, Y) (m) est hors du fondu des biomes : minorant de la distance aux frontieres (axes, cercles des pays)."""
    return min(abs(X), abs(Y), *(abs(math.hypot(X - cx / M, Y - cy / M) - PAYS_R) for cx, cy in CORNERS.values())) >= FONDU_M / 2


SOL_IDX = {"foret": 0, "desert": 1, "roche": 2, "neutre": 3}   # ordre des materiaux dans tools/terrain-import.cs.txt


def sol_case(x, y):
    """Octet du sol en (x, y) monde : base (bits 0-1) | dessus (bits 2-3) | melange 0..15 (bits 4-7), les deux biomes les plus
    lourds de biome_poids (fondu de FONDU_M aux frontieres) ; lu par terrain-import.cs.txt (CompactTerrainMaterial)."""
    if _loin_frontiere(x / M, y / M):
        b = SOL_IDX[BIOME.get(territoire(x, y), "neutre")]
        return b | b << 2
    (b1, w1), (b2, w2) = sorted(biome_poids(x, y).items(), key=lambda kv: -kv[1])[:2]
    return SOL_IDX[b1] | SOL_IDX[b2] << 2 | round(15 * w2 / (w1 + w2)) << 4


# ── Donnees du relief (lues par les taches 4 a 6). Positions en repere local du village : (angle deg, rayon m).
PIC_SOMMET_M = 12          # plateau sommital des pics (m)
PLATEAU_BORD_M = 45        # largeur du bord d'un plateau de Suna (pente ~28 deg)
PICS_KUMO = [(*local_to_world("kumo", *_pol(a, r)), 140 * M, 55 * M) for a, r in ((254, 1180), (246, 1180))]   # (x, y, r_u, z_sommet)
_PLATEAUX = [(*(k / M for k in local_to_world("suna", *_pol(a, r))), 50, 16) for a, r in ((250, 1150), (300, 1100))]  # (X, Y, r_m, h_m)

ROUTES = [dict(v=v, pts=[local_to_world(v, 0, -R_V), local_to_world(v, 0, -PAYS_R)], larg_u=ROUTE_LARG_U) for v in VILLAGES]

# Rivieres de Konoha : points de controle (angle local, rayon m) de l'amont vers l'aval, angle monotone ;
# cascades a ces fractions de la longueur. Le niveau de l'eau suit le relief (toujours en dessous) et descend toujours.
RIVIERES = [
    dict(nom="Riviere de Konoha", ctrl=[(302, 1150), (267.5, 1150)], larg_m=12, cascades=(0.3, 0.6)),
    dict(nom="Torrent du ravin", ctrl=[(228, 1120), (240, 1080), (254, 990), (267, 950)], larg_m=8, cascades=(0.35, 0.65)),
]
EAU_PAS_M = 5
EAU_REVANCHE_M = 2.5       # eau sous le plus bas des terrains voisins (berges)
EAU_PROF_U = 48            # lit 1,2 m sous l'eau
EAU_PENTE_U = 1            # descente mini de l'eau tous les 5 m
CHUTE_U = 130              # hauteur d'une cascade (3,25 m) : le terrain ne descend pas assez pour plus (bouts peu profonds)
BERGE_M = 30               # paroi / berge entre le bord de l'eau et le relief de fond


def _r_de(ctrl, a):
    for (a0, r0), (a1, r1) in zip(ctrl, ctrl[1:]):
        if min(a0, a1) <= a <= max(a0, a1):
            return r0 + (r1 - r0) * (a - a0) / (a1 - a0)
    return ctrl[0][1] if abs(a - ctrl[0][0]) < abs(a - ctrl[-1][0]) else ctrl[-1][1]


def _riviere(d):
    a0, a1 = d["ctrl"][0][0], d["ctrl"][-1][0]
    n = max(2, round(abs(math.radians(a1 - a0)) * sum(r for _, r in d["ctrl"]) / len(d["ctrl"]) / EAU_PAS_M))
    loc = [_pol(a0 + (a1 - a0) * k / n, _r_de(d["ctrl"], a0 + (a1 - a0) * k / n)) for k in range(n + 1)]
    pts = [local_to_world("konoha", *p) for p in loc]
    casc = {round(f * n) for f in d["cascades"]}
    o = (d["larg_m"] / 2 + 15) * M
    z = []
    for k, (x, y) in enumerate(pts):
        lim = min(_fond(x + dx, y + dy) for dx, dy in ((0, 0), (o, 0), (-o, 0), (0, o), (0, -o))) - EAU_REVANCHE_M * M
        z.append(lim if k == 0 else min(z[-1] - EAU_PENTE_U - (CHUTE_U if k in casc else 0), lim))
    rs = [math.hypot(*p) for p in loc]
    zone = d["larg_m"] / 2 + BERGE_M
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    return dict(nom=d["nom"], a0=a0, a1=a1, n=n, ctrl=d["ctrl"], loc=loc, pts=pts, z=z, casc=sorted(casc), larg_m=d["larg_m"],
                zone=zone, rmin=min(rs) - zone, rmax=max(rs) + zone,
                bbox=(min(xs) - zone * M, min(ys) - zone * M, max(xs) + zone * M, max(ys) + zone * M))


def _creuse(x, y, h):
    """Lit et berges des rivieres : du lit (plat sur la largeur de l'eau) au relief de fond sur BERGE_M."""
    for e in _RIV:
        x0, y0, x1, y1 = e["bbox"]
        if not (x0 <= x <= x1 and y0 <= y <= y1):
            continue
        xm, ym = world_to_local("konoha", x, y)
        r = math.hypot(xm, ym)
        if not e["rmin"] <= r <= e["rmax"]:
            continue
        a = math.degrees(math.atan2(ym, xm)) % 360
        f = (a - e["a0"]) / (e["a1"] - e["a0"])
        if 0 <= f <= 1:
            d = abs(r - _r_de(e["ctrl"], a))    # >= distance vraie a l'axe : la berge reste dans la zone d'eau
            k = f * e["n"]
            i = min(int(k), e["n"] - 1)
            zw = e["z"][i] + (e["z"][i + 1] - e["z"][i]) * (k - i)
        else:
            i = 0 if f < 0 else -1
            d = math.hypot(xm - e["loc"][i][0], ym - e["loc"][i][1])
            zw = e["z"][i]
        if d < e["zone"]:
            lit = zw - EAU_PROF_U
            h = lit + (h - lit) * _lisse((d - e["larg_m"] / 2) / BERGE_M)
    return h


_RIV = [_riviere(d) for d in RIVIERES]
EAUX = [dict(nom=e["nom"], pts=[(x, y, z) for (x, y), z in zip(e["pts"], e["z"])], larg_u=e["larg_m"] * M) for e in _RIV]


def _yaw(a, b):
    return math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))


CASCADES = [dict(x=(e["pts"][k - 1][0] + e["pts"][k][0]) / 2, y=(e["pts"][k - 1][1] + e["pts"][k][1]) / 2,
                 z_haut=e["z"][k - 1], z_bas=e["z"][k], larg_u=e["larg_m"] * M, yaw=_yaw(e["pts"][k - 1], e["pts"][k]))
            for e in _RIV for k in e["casc"]]


def _ponts():
    out = []
    # route x riviere : la route (axe local angle 270 du village) croise une riviere de Konoha qui passe par 270 deg
    rt = next(r for r in ROUTES if r["v"] == "konoha")
    for e in _RIV:
        if min(e["a0"], e["a1"]) <= 270 <= max(e["a0"], e["a1"]):
            x, y = local_to_world("konoha", 0, -_r_de(e["ctrl"], 270))
            out.append(dict(x=x, y=y, yaw=_yaw(*rt["pts"]), long_u=2 * (e["zone"] + 6) * M, larg_u=ROUTE_LARG_U + 160, z=0.0))
    # passerelle sur le torrent, entre ses cascades, la ou les deux rives sont le plus a la meme hauteur
    e = _RIV[1]
    lg = 2 * (e["zone"] + 5) * M
    def bouts(k):
        yaw = _yaw(e["pts"][k - 1], e["pts"][k + 1]) + 90
        c, s = math.cos(math.radians(yaw)) * lg / 2, math.sin(math.radians(yaw)) * lg / 2
        x, y = e["pts"][k]
        return yaw, x, y, _fond(x + c, y + s), _fond(x - c, y - s)
    ks = [k for k in range(e["n"] // 5, e["n"] * 4 // 5) if all(abs(k - c) > 10 for c in e["casc"])]
    yaw, x, y, z1, z2 = min((bouts(k) for k in ks), key=lambda b: abs(b[3] - b[4]))
    out.append(dict(x=x, y=y, yaw=yaw, long_u=lg, larg_u=200, z=(z1 + z2) / 2))
    # pont entre les deux pics de Kumo (d'un sommet a l'autre)
    (ax, ay, _, z), (bx, by, _, _) = PICS_KUMO
    out.append(dict(x=(ax + bx) / 2, y=(ay + by) / 2, yaw=_yaw((ax, ay), (bx, by)),
                    long_u=math.hypot(bx - ax, by - ay) - 2 * (PIC_SOMMET_M - 6) * M, larg_u=240, z=z))
    return out


PONTS = _ponts()


def _hauteur(x, y):
    d = max(abs(x), abs(y)) - INNER
    xc = max(-INNER, min(INNER, x))
    yc = max(-INNER, min(INNER, y))
    h = 0.0
    for v in VILLAGES:
        if abs(xc - CORNERS[v][0]) < R_V * M and abs(yc - CORNERS[v][1]) < R_V * M:
            h = _hauteur_village(v, xc, yc) + _bosses(v, *world_to_local(v, xc, yc))
            break
    h = _creuse(xc, yc, h + _fond(xc, yc))
    if d > 0:
        cible = _cible_bord(x, y)
        # montee raccourcie quand l'ecart est faible : pente toujours >= 55 deg (> 45 = infranchissable)
        L = min(BORD_MONTEE, max(1.0, abs(cible - h) / TAN_BORD))
        h += (cible - h) * min(1.0, d / L)
    return h


def height_at(x_u, y_u):
    # z monde exact (fonction analytique ; la grille en est l'echantillonnage)
    return _hauteur(x_u, y_u)


def build():
    step = SIZE / (RES - 1)
    heights = array("H", bytes(2 * RES * RES))
    k = 0
    echelle = 65535 / TERRAIN_HEIGHT
    for j in range(RES):
        y = -HALF + j * step
        for i in range(RES):
            z = _hauteur(-HALF + i * step, y)
            v = round((z - TERRAIN_Z) * echelle)
            heights[k] = 0 if v < 0 else 65535 if v > 65535 else v
            k += 1
    holes = [False] * (RES * RES)
    for h in HOLES:
        r = TROU_T1 + TROU_LARG
        i0, i1 = int((h["x"] - r + HALF) / step), int((h["x"] + r + HALF) / step) + 1
        j0, j1 = int((h["y"] - r + HALF) / step), int((h["y"] + r + HALF) / step) + 1
        for j in range(max(0, j0), min(RES, j1 + 1)):
            for i in range(max(0, i0), min(RES, i1 + 1)):
                dx, dy = -HALF + i * step - h["x"], -HALF + j * step - h["y"]
                t, n = dx * h["ux"] + dy * h["uy"], -dx * h["uy"] + dy * h["ux"]
                if TROU_T0 <= t <= TROU_T1 and abs(n) <= TROU_LARG / 2:
                    holes[j * RES + i] = True
    sols = bytes(sol_case(-HALF + (k % RES) * step, -HALF + (k // RES) * step) for k in range(RES * RES))
    meta = {"res": RES, "size": SIZE, "height": TERRAIN_HEIGHT, "z": TERRAIN_Z, "holes": len(HOLES)}
    return heights, holes, sols, meta


def vue_dessus(heights, chemin):
    """Vue du dessus pour la relecture (facultatif, Pillow) : hauteurs ombrees, routes, eaux, ponts, pics."""
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        return
    n, k = RES // 4, 4
    z = [[heights[(j * k) * RES + i * k] for i in range(n)] for j in range(n)]
    img = Image.new("RGB", (n, n))
    px = img.load()
    for j in range(n):
        for i in range(n):
            g = min(255, max(0, 70 + (z[j][i] - 3277) // 40))   # 3277 = z monde 0
            ombre = (z[j][i] - z[max(0, j - 1)][max(0, i - 1)]) // 4
            g = min(255, max(0, g + ombre))
            px[i, n - 1 - j] = (g, g, g)   # y monde vers le haut
    d = ImageDraw.Draw(img)
    P = lambda x, y: ((x + HALF) / SIZE * n, n - 1 - (y + HALF) / SIZE * n)
    for r in ROUTES:
        d.line([P(*p) for p in r["pts"]], fill=(200, 170, 110), width=2)
    for e in EAUX:
        d.line([P(x, y) for x, y, _ in e["pts"]], fill=(60, 120, 230), width=2)
    for c in CASCADES:
        x, y = P(c["x"], c["y"])
        d.ellipse((x - 2, y - 2, x + 2, y + 2), fill=(150, 220, 255))
    for p in PONTS:
        a = math.radians(p["yaw"])
        dx, dy = math.cos(a) * p["long_u"] / 2, math.sin(a) * p["long_u"] / 2
        d.line([P(p["x"] - dx, p["y"] - dy), P(p["x"] + dx, p["y"] + dy)], fill=(220, 40, 40), width=3)
    img.save(chemin)


def main():
    if len(sys.argv) > 1:
        charger_emprises(sys.argv[1])
        charger_trous(sys.argv[1])
    else:  # sans dossier : on genere les plans v3 dans un dossier temporaire
        import subprocess, tempfile
        tmp = tempfile.mkdtemp()
        here = os.path.dirname(os.path.abspath(__file__))
        for script, extra in (("plans-villages-v3.py", []), ("plan-vers-blocage.py", [os.path.join(tmp, "monde.cs.txt")])):
            subprocess.run([sys.executable, os.path.join(here, script), tmp] + extra, check=True, stdout=subprocess.DEVNULL)
        charger_emprises(tmp)
        charger_trous(tmp)
    heights, holes, sols, meta = build()
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
    os.makedirs(out, exist_ok=True)
    vue_dessus(heights, os.path.join(out, "vue-dessus.png"))
    if sys.byteorder == "big":
        heights.byteswap()
    with open(os.path.join(out, "relief.u16"), "wb") as f:
        heights.tofile(f)
    with open(os.path.join(out, "holes.u8"), "wb") as f:
        f.write(bytes(holes))
    with open(os.path.join(out, "sols.u8"), "wb") as f:
        f.write(sols)
    with open(os.path.join(out, "relief.json"), "w") as f:
        json.dump(meta, f)
    print("ecrit", out, meta)


if __name__ == "__main__":
    main()
