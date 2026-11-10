# Carte de hauteurs : relief des villages (3 etages), bord infranchissable, terrain plat ailleurs.
# Ecrit tools/out/relief.u16, holes.u8, relief.json pour le composant Terrain de s&box.
import json
import math
import os
import sys
from array import array

from carte import CORNERS, HALF, INNER, M, RAMPS, TIER_Z, TIERS, world_to_local

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
RMAX = 340 + 25  # au-dela, village forcement a l'etage bas (marge Kumo 22.5 m)


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


def _cible_bord(x, y):
    # sommet du bord : melange doux des styles des coins (le plus proche domine)
    ds = {v: math.hypot(x - cx, y - cy) for v, (cx, cy) in CORNERS.items()}
    dmin = min(ds.values())
    ws = {v: math.exp(-(d - dmin) / BORD_LISSAGE) for v, d in ds.items()}
    return sum(BORD_Z[v] * w for v, w in ws.items()) / sum(ws.values())


def _hauteur(x, y):
    d = max(abs(x), abs(y)) - INNER
    xc = max(-INNER, min(INNER, x))
    yc = max(-INNER, min(INNER, y))
    h = 0.0
    for v in VILLAGES:
        if abs(xc - CORNERS[v][0]) < RMAX * M and abs(yc - CORNERS[v][1]) < RMAX * M:
            h = _hauteur_village(v, xc, yc)
            break
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
    meta = {"res": RES, "size": SIZE, "height": TERRAIN_HEIGHT, "z": TERRAIN_Z, "holes": len(HOLES)}
    return heights, holes, meta


def main():
    if len(sys.argv) > 1:
        charger_trous(sys.argv[1])
    else:  # sans dossier : on genere les plans v3 dans un dossier temporaire
        import subprocess, tempfile
        tmp = tempfile.mkdtemp()
        here = os.path.dirname(os.path.abspath(__file__))
        for script, extra in (("plans-villages-v3.py", []), ("plan-vers-blocage.py", [os.path.join(tmp, "monde.cs.txt")])):
            subprocess.run([sys.executable, os.path.join(here, script), tmp] + extra, check=True, stdout=subprocess.DEVNULL)
        charger_trous(tmp)
    heights, holes, meta = build()
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
    os.makedirs(out, exist_ok=True)
    if sys.byteorder == "big":
        heights.byteswap()
    with open(os.path.join(out, "relief.u16"), "wb") as f:
        heights.tofile(f)
    with open(os.path.join(out, "holes.u8"), "wb") as f:
        f.write(bytes(holes))
    with open(os.path.join(out, "relief.json"), "w") as f:
        json.dump(meta, f)
    print("ecrit", out, meta)


if __name__ == "__main__":
    main()
