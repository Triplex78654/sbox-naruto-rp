# Génère le script de blocage s&box (run_csharp) des trois villages sur le relief (Terrain) à partir des plans v3.
# Entrée : dossier des plans (tools/plans-villages-v3.py <dossier>), un JSON par élément (mètres, repère local du village).
# Sortie : docs/monde-blocage-v3.cs.txt (ou le 2e argument), à lancer avec konoha.scene ouverte, APRÈS tools/terrain-import.cs.txt :
#   sbox-cli --call run_csharp '{"path":".../docs/monde-blocage-v3.cs.txt"}'
# Usage : python tools/plan-vers-blocage.py <dossier plans> [sortie]
import json, math, os, sys, glob
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from carte import CORNERS, INNER, M, CENTRE_R, PAYS_R, TIERS, TIER_Z, RAMPS, yaw_of, local_to_world
from relief import height_at, RAMPE_LARG, PUITS_PENTE, PUITS_FIN, PUITS_LARG, sol_puits

src = sys.argv[1]
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "docs", "monde-blocage-v3.cs.txt")
els = {}
for f in sorted(glob.glob(os.path.join(src, "*.json"))):
    d = json.load(open(f, encoding="utf-8"))
    d = d.get("data", d)
    els.setdefault(d["village"], []).append(d)

BORDER = PAYS_R * M                              # rayon de la frontière (u)
DIST = math.hypot(*CORNERS["konoha"])            # coin -> centre du monde (u)

# Style de chaque village : couleurs (r, g, b) et hauteurs (unités).
STYLES = {
    "konoha": dict(rock=(0.5, 0.45, 0.4), built=(0.9, 0.86, 0.76), roof=(0.7, 0.2, 0.15),
                 roofs=[(0.7, 0.2, 0.15), (0.2, 0.35, 0.6), (0.25, 0.45, 0.25), (0.55, 0.3, 0.2)],
                 tower=(0.85, 0.5, 0.25), rampart_h=(650, 650), house_h=(260, 500)),
    "suna": dict(rock=(0.74, 0.56, 0.36), built=(0.9, 0.8, 0.62), roof=(0.78, 0.62, 0.42),
                 tower=(0.86, 0.68, 0.42), rampart_h=(900, 1200), house_h=(220, 340)),
    "kumo": dict(rock=(0.48, 0.48, 0.5), built=(0.82, 0.82, 0.8), roof=(0.3, 0.45, 0.62),
                 tower=(0.35, 0.5, 0.72), rampart_h=(1600, 2800), house_h=(260, 420)),
}
COMMON = dict(road=(0.55, 0.5, 0.42), water=(0.2, 0.45, 0.75), zone=(0.68, 0.58, 0.42), mark=(0.25, 0.55, 0.6),
              rock2=(0.62, 0.6, 0.55), park=(0.3, 0.55, 0.28), mountain=(0.5, 0.45, 0.4), wall=(0.78, 0.7, 0.55),
              domain=(0.82, 0.76, 0.66), leaf=(0.22, 0.45, 0.2), wood=(0.45, 0.3, 0.18))
SOUS = dict(mur=(0.3, 0.28, 0.26), sol=(0.38, 0.34, 0.3), plafond=(0.26, 0.24, 0.23), puits=(0.34, 0.3, 0.26))

def col(style, key):
    if key in ("rock", "built", "tower"): return style[key]
    return COMMON.get(key, style["built"])

lines = []
CUR = ["konoha"]
def hl(x, y, v=None):
    """z monde du relief au point (x, y) du repère local du village (unités)."""
    return height_at(*local_to_world(v or CUR[0], x / M, y / M))

def B(group, name, x, y, sx, sy, sz, c, z0=0.0, yaw=0.0, collide=True):
    lines.append(f'B({group},"{name}",{x:.0f}f,{y:.0f}f,{sx:.0f}f,{sy:.0f}f,{sz:g}f,new Color({c[0]}f,{c[1]}f,{c[2]}f),{z0:.0f}f,{yaw:.1f}f,{"true" if collide else "false"});')
def ZB(group, name, x, y, sx, sy, sz, c, yaw=0.0, collide=True, flat=False):
    """Boîte posée sur le relief : base à la hauteur du terrain au centre (enfoncée de 15 u pour les volumes)."""
    h = hl(x, y)
    if flat: B(group, name, x, y, sx, sy, sz, c, h, yaw, collide)
    else: B(group, name, x, y, sx, sy, sz + 15, c, h - 15, yaw, collide)
def S(group, name, x, y, z, d, c):
    lines.append(f'S({group},"{name}",{x:.0f}f,{y:.0f}f,{z:.0f}f,{d:.0f}f,new Color({c[0]}f,{c[1]}f,{c[2]}f));')
def disk(group, name, x, y, r, h, c, collide=True, n=None, flat=False):
    # Disque = bandes parallèles de longueur égale à la corde (des carrés tournés donnent une étoile).
    n = n or 14
    w = 2 * r / n
    for i in range(n):
        yy = -r + (i + 0.5) * w
        ZB(group, name, x, y + yy, 2 * math.sqrt(max(0, r * r - yy * yy)), w * 1.02, h, c, 0, collide, flat)
def ring(group, name, x, y, r, w, a0, a1, h, c, step_m=12, jag=None, flat=False):
    span = a1 - a0
    n = max(4, math.ceil(2 * math.pi * r * span / 360 / step_m))
    da = span / n
    seg = 2 * math.pi * r * da / 360 * 1.06
    for i in range(n):
        a = a0 + (i + 0.5) * da
        rad = math.radians(a)
        hh = h if jag is None else jag(i)
        ZB(group, f"{name} {i}", (x + math.cos(rad) * r) * M, (y + math.sin(rad) * r) * M, w * M, seg * M, hh, c, a, not flat, flat)

# ── Souterrains : boîtes centrées G (repère local du village, z monde), enregistrées dans UNDER pour les tests.
UNDER, SHAFTS, HOLES_OUT = [], [], []
T, TW, TH, RH = 40, 120, 220, 300     # épaisseur des parois, largeur / hauteur intérieures du tunnel, hauteur des salles
MARGE = 110                           # dessus du plafond (intérieur) sous le relief : les dalles de 40 u laissent >= 60 u
LTAN = math.tan(math.radians(10))     # pente max cherchée des tunnels (la limite de marche est 35 deg)

def G(grp, v, code, kind, name, c, s, d, color):
    c = tuple(round(k, 1) for k in c); s = tuple(round(k, 1) for k in s); d = tuple(round(k, 4) for k in d)
    UNDER.append(dict(v=v, code=code, kind=kind, name=name, c=c, s=s, dir=d))
    lines.append(f'G({grp},"{name}",{c[0]}f,{c[1]}f,{c[2]}f,{s[0]}f,{s[1]}f,{s[2]}f,{d[0]}f,{d[1]}f,{d[2]}f,new Color({color[0]}f,{color[1]}f,{color[2]}f));')

def galerie(grp, v, code, kind, pts, W=TW, H=TH):
    """Couloir (sol, 2 murs, plafond) le long de pts [(x, y, z du sol)], boîtes inclinées d'un nœud au suivant."""
    for (ax, ay, az), (bx, by, bz) in zip(pts, pts[1:]):
        d = (bx - ax, by - ay, bz - az)
        n3, nh = math.sqrt(sum(k * k for k in d)), math.hypot(d[0], d[1])
        if nh < 1: continue
        lat = (-d[1] / nh, d[0] / nh)
        m = ((ax + bx) / 2, (ay + by) / 2, (az + bz) / 2)
        ln = n3 * 1.03
        G(grp, v, code, kind, f"{code} sol", (m[0], m[1], m[2] - T / 2), (ln, W + 2 * T, T), d, SOUS["sol"])
        G(grp, v, code, kind, f"{code} plafond", (m[0], m[1], m[2] + H + T / 2), (ln, W + 2 * T, T), d, SOUS["plafond"])
        for sg in (-1, 1):
            G(grp, v, code, kind, f"{code} mur", (m[0] + lat[0] * sg * (W / 2 + T / 2), m[1] + lat[1] * sg * (W / 2 + T / 2), m[2] + H / 2),
              (ln, T, H + 2 * T), d, SOUS["mur"])

def hmin_rect(v, x0, y0, x1, y1, step=20):
    return min(hl(x0 + (x1 - x0) * i / max(1, round((x1 - x0) / step)), y0 + (y1 - y0) * j / max(1, round((y1 - y0) / step)), v)
               for i in range(max(1, round((x1 - x0) / step)) + 1) for j in range(max(1, round((y1 - y0) / step)) + 1))

def profil(v, x0, y0, ux, uy, s_end, extra0=None, sf=None):
    """Plafond intérieur c(s) d'un tunnel : toujours sous le relief (marge MARGE, sur tout le couloir), pente <= LTAN,
    plat jusqu'à sf. Enveloppe inférieure du relief sous le tunnel (la plus basse hauteur au passage d'un changement de niveau)."""
    lat = (-uy, ux)
    ss = [i * 10.0 for i in range(int(s_end // 10) + 1)] + [s_end]
    e = [min(hl(x0 + ux * s + lat[0] * o, y0 + uy * s + lat[1] * o, v) for o in (-110, -55, 0, 55, 110)) - MARGE for s in ss]
    if extra0 is not None: e[0] = min(e[0], extra0)
    c = e[:]
    for i in range(1, len(ss)): c[i] = min(c[i], c[i - 1] + LTAN * (ss[i] - ss[i - 1]))
    for i in range(len(ss) - 2, -1, -1): c[i] = min(c[i], c[i + 1] + LTAN * (ss[i + 1] - ss[i]))
    ks = [0.0] + ([sf] if sf else [])
    k = (sf or 0) + 800
    while k < s_end - 400: ks.append(float(k)); k += 800
    ks.append(float(s_end))
    ck = []
    for i, s0 in enumerate(ks):
        lo, hi = ks[max(0, i - 1)], ks[min(len(ks) - 1, i + 1)]
        ck.append(min(cv for sv, cv in zip(ss, c) if lo <= sv <= hi))
    if sf: ck[0] = ck[1] = min(ck[0], ck[1])
    return ks, ck

def cote(ux, uy, hw, hh):
    """(côté de la salle traversé par un rayon du centre vers (ux, uy), distance jusqu'à la paroi intérieure)."""
    dx = hw / abs(ux) if abs(ux) > 1e-9 else 1e18
    dy = hh / abs(uy) if abs(uy) > 1e-9 else 1e18
    return ("x", dx) if dx < dy else ("y", dy)

def salle(grp, v, code, cx, cy, w, h, top, floor_flat, ouv):
    """Salle rectangulaire (w x h intérieurs), dessus du plafond intérieur = top ; ouv : liste d'ouvertures
    (axe 'x'/'y', signe, position le long de la paroi, largeur) percées dans les murs."""
    z = top - RH
    G(grp, v, code, "salle", f"{code} sol", (cx, cy, z - T / 2), (w + 2 * T, h + 2 * T, T), (1, 0, 0), SOUS["sol"])
    G(grp, v, code, "salle", f"{code} plafond", (cx, cy, top + T / 2), (w + 2 * T, h + 2 * T, T), (1, 0, 0), SOUS["plafond"])
    for ax, sg in (("x", 1), ("x", -1), ("y", 1), ("y", -1)):
        half, perp = (w / 2, h / 2) if ax == "x" else (h / 2, w / 2)
        gaps = sorted((p - g / 2, p + g / 2) for a, s_, p, g in ouv if a == ax and s_ == sg)
        spans, cur = [], -perp - T
        for lo, hi in gaps:
            spans.append((cur, lo)); cur = hi
        spans.append((cur, perp + T))
        for a0, a1 in spans:
            if a1 - a0 < 1: continue
            mid = (a0 + a1) / 2
            ctr = (cx + sg * (half + T / 2), cy + mid) if ax == "x" else (cx + mid, cy + sg * (half + T / 2))
            sz = (T, a1 - a0) if ax == "x" else (a1 - a0, T)
            G(grp, v, code, "salle", f"{code} mur", (ctr[0], ctr[1], z + RH / 2), (sz[0], sz[1], RH + 2 * T), (1, 0, 0), SOUS["mur"])
    return z
# Même placement que la page des plans (graine fixe), pour retrouver les mêmes maisons.
HOUSES_CACHE = {}
def houses(lst, it):
    s = it["shape"]; n = max(0, min(400, int(s["n"])))
    wall = next((e for e in lst if e["shape"]["t"] == "ring" and e["code"] == "A1"), None)
    inner = wall["shape"]["r"] - wall["shape"]["w"] / 2 - 6 if wall else 200
    busy = []
    for e in lst:
        q = e["shape"]
        if e is it: continue
        if q["t"] == "houses":
            if e.get("order", 0) < it.get("order", 0):
                for h in HOUSES_CACHE.get(id(e), []): busy.append(("c", h[0], h[1], h[2] + 3))
            continue
        if q["t"] == "circle": busy.append(("c", q["x"], q["y"], q["r"] + 5))
        elif q["t"] == "ring" and q.get("fill"): busy.append(("c", q["x"], q["y"], q["r"] + q["w"] / 2 + 5))
        elif q["t"] == "ring" and q["a1"] - q["a0"] >= 360: busy.append(("r", q["x"], q["y"], q["r"], q["w"] / 2 + 5))
        elif q["t"] == "rect":
            if q.get("rot"): busy.append(("c", q["x"], q["y"], math.hypot(q["w"], q["h"]) / 2 + 5))
            else: busy.append(("b", q["x"], q["y"], q["w"] / 2 + 5, q["h"] / 2 + 5))
        elif q["t"] == "line":
            p = q["pts"]
            for i in range(len(p) - 1):
                a, b = p[i], p[i + 1]; L = math.hypot(b[0] - a[0], b[1] - a[1]); k = max(1, math.ceil(L / 6))
                for j in range(k + 1): busy.append(("c", a[0] + (b[0] - a[0]) * j / k, a[1] + (b[1] - a[1]) * j / k, q["w"] / 2 + 5))
    seed = 7
    def rnd():
        nonlocal seed
        seed = (seed * 16807) % 2147483647
        return seed / 2147483647
    out = []
    zr = s.get("zr") or inner
    zx, zy = (s["zx"], s["zy"]) if s.get("zr") else (0, 0)
    k = 0
    while k < 8000 and len(out) < n:
        k += 1
        x = zx + (rnd() * 2 - 1) * zr; y = zy + (rnd() * 2 - 1) * zr
        r = 5 + rnd() * 3 if s.get("style") == "dome" else 5 + rnd() * 2.5
        if math.hypot(x, y) > inner - r or math.hypot(x - zx, y - zy) > zr: continue
        def hit(b):
            if b[0] == "c": return math.hypot(x - b[1], y - b[2]) < b[3] + r
            if b[0] == "r": return abs(math.hypot(x - b[1], y - b[2]) - b[3]) < b[4] + r
            return abs(x - b[1]) < b[3] + r and abs(y - b[2]) < b[4] + r
        if any(hit(b) for b in busy): continue
        if any(math.hypot(x - o[0], y - o[1]) < o[2] + r + 3 for o in out): continue
        out.append((x, y, r))
    HOUSES_CACHE[id(it)] = out
    return out

HEIGHT = dict(built=400, tower=1600, mark=900, rock2=320, wall=650, mountain=2400)
FLAT = dict(road=5, zone=6, water=4, park=3)

def libre(lst, x, y, marge=4):
    """Vrai si (x, y) (mètres locaux) n'est dans l'emprise d'aucun élément de surface (pour poser les ponts)."""
    for e in lst:
        if e["layer"] != "surface" or e["code"][0] == "X": continue
        q = e["shape"]
        if q["t"] == "rect" and abs(x - q["x"]) < q["w"] / 2 + marge and abs(y - q["y"]) < q["h"] / 2 + marge: return False
        if q["t"] == "circle" and math.hypot(x - q["x"], y - q["y"]) < q["r"] + marge: return False
        if q["t"] == "houses" and math.hypot(x - q["zx"], y - q["zy"]) < q["zr"] + marge: return False
        if q["t"] == "ring" and q.get("fill") and math.hypot(x - q["x"], y - q["y"]) < q["r"] + q["w"] / 2 + marge: return False
    return True

def ponts_kumo(g, lst):
    """2 ponts de pierre du niveau milieu au niveau haut : tablier incliné (27 deg) au-dessus du sol du milieu, sur piles."""
    bas, mil, haut = TIER_Z["kumo"]
    r0, r1 = TIERS["milieu"][0] + 45, TIERS["milieu"][0] - 4   # départ sur le milieu, arrivée sur le haut
    pris = []
    for cible in (240, 300):
        for da in sorted(range(-12, 13), key=abs):
            a = cible + da
            if any(abs(a - b) < 20 for b in pris) or a < 230 or a > 310: continue
            if min(abs(((a - ra + 180) % 360) - 180) for ra in RAMPS["kumo"]) < 12: continue
            if all(libre(lst, (r0 + (r1 - r0) * k / 12) * math.cos(math.radians(a)) + lx, (r0 + (r1 - r0) * k / 12) * math.sin(math.radians(a)) + ly, 4)
                   for k in range(13) for lx, ly in ((0, 0), (3, 0), (-3, 0), (0, 3), (0, -3))):
                pris.append(a); break
    for i, a in enumerate(pris):
        ca, sa = math.cos(math.radians(a)) * M, math.sin(math.radians(a)) * M
        p0, p1 = (r0 * ca, r0 * sa, mil), (r1 * ca, r1 * sa, haut)
        d = (p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2] + 5)
        n3 = math.sqrt(sum(k * k for k in d))
        mid = tuple((p0[k] + p1[k]) / 2 for k in range(3))
        lines.append(f'G({g},"Pont {i + 1} (tablier)",{mid[0]:.1f}f,{mid[1]:.1f}f,{mid[2] - 10:.1f}f,{n3:.1f}f,200f,30f,{d[0]:.1f}f,{d[1]:.1f}f,{d[2]:.1f}f,new Color(0.6f,0.6f,0.62f));')
        for k in (1, 2, 3, 4):
            f = k / 5
            px, py, pz = p0[0] + (p1[0] - p0[0]) * f, p0[1] + (p1[1] - p0[1]) * f, p0[2] + (p1[2] - p0[2]) * f - 25
            B(g, f"Pont {i + 1} (pile)", px, py, 110, 110, pz - mil + 15, (0.5, 0.5, 0.52), mil - 15, a)

def levres_suna(g):
    """Murets de roche au bord haut de chaque terrasse (pas sur les rampes)."""
    for r0 in (TIERS["milieu"][0], TIERS["bas"][0]):
        r = r0 - 4.5
        n = math.ceil(2 * math.pi * r * 90 / 360 / 6)
        for i in range(n):
            a = 225 + (i + 0.5) * 90 / n
            if any(r * abs(math.sin(math.radians(a - ra))) < RAMPE_LARG[1] + 3 and math.cos(math.radians(a - ra)) > 0 for ra in RAMPS["suna"]): continue
            rad = math.radians(a)
            ZB(g, "Muret de terrasse", math.cos(rad) * r * M, math.sin(rad) * r * M, 100, 6 * M * 1.08, 90, STYLES["suna"]["rock"], a)

def ramp_dist(v, x, y):
    """Distance (m) à l'axe de la rampe la plus proche (comme plans-villages-v3)."""
    hw = 22.5 if v == "kumo" else 12.5
    best = 1e9
    for a in RAMPS[v]:
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        for rb in (TIERS["milieu"][0], TIERS["bas"][0]):
            t = min(rb + hw, max(rb - hw, x * ca + y * sa))
            best = min(best, math.hypot(x - t * ca, y - t * sa))
    return best

def niveau(r): return "haut" if r < TIERS["milieu"][0] else "milieu" if r < TIERS["bas"][0] else "bas"

def libre_ext(lst, x, y, marge):
    """libre() + routes (lignes) : (x, y) en mètres locaux."""
    if not libre(lst, x, y, marge): return False
    for e in lst:
        q = e["shape"]
        if e["layer"] != "surface" or q["t"] != "line": continue
        p = q["pts"]
        for (x1, y1), (x2, y2) in zip(p, p[1:]):
            L2 = (x2 - x1) ** 2 + (y2 - y1) ** 2
            t = max(0, min(1, ((x - x1) * (x2 - x1) + (y - y1) * (y2 - y1)) / L2))
            if math.hypot(x - x1 - t * (x2 - x1), y - y1 - t * (y2 - y1)) < q["w"] / 2 + marge: return False
    return True

def puits_ok(v, lst, px, py, ux, uy, floor0, niv, ext=0):
    """Un puits de (px, py) (unités) selon (ux, uy), sol à floor0, peut-il sortir à la surface du niveau niv ?
    Renvoie (L, zs) ou None : sortie libre (hors emprises, routes, rampes), niveau uniforme, terrain toujours >= 110 u au-dessus du sol."""
    zs = hl(px, py)
    for _ in range(3):
        L = (zs - floor0) / PUITS_PENTE - PUITS_FIN
        zs = hl(px + ux * L, py + uy * L)
    L = (zs - floor0) / PUITS_PENTE - PUITS_FIN
    if L < 300: return None
    xm, ym = (px + ux * L) / M, (py + uy * L) / M
    r = math.hypot(xm, ym); a = math.degrees(math.atan2(ym, xm)) % 360
    if r > 490 or niveau(r) != niv or not 226 <= a <= 314: return None
    if ramp_dist(v, xm, ym) < 20: return None
    if not all(libre_ext(lst, xm + dx, ym + dy, 2.5) for dx in (-3, 0, 3) for dy in (-3, 0, 3)): return None
    lat = (-uy, ux)
    for i in range(1, int(ext / 40) + 1):   # couloir allongé avant le puits : son plafond (+ dalle) doit rester >= 60 u sous le relief
        for o in (-110, 0, 110):
            if hl(px - ux * i * 40.0 + lat[0] * o, py - uy * i * 40.0 + lat[1] * o) < floor0 + TH + T + 62: return None
    for i in range(int((L + PUITS_FIN + 120) / 40) + 1):
        s = i * 40.0
        floor = floor0 + s * PUITS_PENTE if s <= L + PUITS_FIN else zs
        for o in (-100, 0, 100):
            h = hl(px + ux * s + lat[0] * o, py + uy * s + lat[1] * o)
            if s - L >= -170:   # sous le trou et au-delà : terrain plat, c'est le trou qui ouvre
                if s - L <= 120 and abs(h - zs) > 8: return None
            elif h - floor < 110: return None
    return L, zs

def rect_bouche(r, sx_, sy_, stub):
    """Emprise (x0, y0, x1, y1) de la bouche de longueur stub sortant du côté (sx_, sy_) d'une salle."""
    if sx_:
        a, b = r["cx"] + sx_ * r["w"] / 2, r["cx"] + sx_ * (r["w"] / 2 + T + stub)
        return (min(a, b), r["cy"] - r["h"] / 2, max(a, b), r["cy"] + r["h"] / 2)
    a, b = r["cy"] + sy_ * r["h"] / 2, r["cy"] + sy_ * (r["h"] / 2 + T + stub)
    return (r["cx"] - r["w"] / 2, min(a, b), r["cx"] + r["w"] / 2, max(a, b))

def empiete(rooms, code, r, ax, p, nx, ny, sx_, sy_, Ls):
    """La bouche + le puits d'une salle (emprise au sol, toutes hauteurs) touchent-ils une AUTRE salle ? On n'entre que par sa porte."""
    ox, oy = (0, p) if ax == "x" else (p, 0)
    a = (r["cx"] + ox, r["cy"] + oy)
    b = (sx_ + nx * (Ls + PUITS_FIN + 180), sy_ + ny * (Ls + PUITS_FIN + 180))
    hw = PUITS_LARG / 2 + T
    x0, x1 = min(a[0], b[0]) - (hw if ax == "y" else 0), max(a[0], b[0]) + (hw if ax == "y" else 0)
    y0, y1 = min(a[1], b[1]) - (hw if ax == "x" else 0), max(a[1], b[1]) + (hw if ax == "x" else 0)
    return any(x0 < o["cx"] + o["w"] / 2 + T + 20 and o["cx"] - o["w"] / 2 - T - 20 < x1 and y0 < o["cy"] + o["h"] / 2 + T + 20 and o["cy"] - o["h"] / 2 - T - 20 < y1
               for c, o in rooms.items() if c != code)

def souterrains(v, lst, grp, g):
    CUR[0] = v
    by = {e["code"]: e for e in lst}
    rooms = {}
    for e in lst:
        if e["layer"] == "souterrain" and e["code"][0] == "S":
            s = e["shape"]
            rooms[e["code"]] = dict(e=e, cx=s["x"] * M, cy=s["y"] * M, w=s["w"] * M, h=s["h"] * M, ouv=[], tun=None)
    def sale_de(p): return next(r for r in rooms.values() if abs(r["cx"] - p[0] * M) < 1 and abs(r["cy"] - p[1] * M) < 1)
    STUB = 400
    # 1) tunnels vers les sorties
    for k in ("1", "2"):
        t = by["T" + k]
        (ax, ay), (bx, by_) = t["shape"]["pts"]
        r = sale_de((ax, ay))
        x0, y0, x1, y1 = ax * M, ay * M, bx * M, by_ * M
        L = math.hypot(x1 - x0, y1 - y0); ux, uy = (x1 - x0) / L, (y1 - y0) / L
        side, dint = cote(ux, uy, r["w"] / 2, r["h"] / 2)
        comp = abs(ux) if side == "x" else abs(uy)
        sg = (1 if ux > 0 else -1) if side == "x" else (1 if uy > 0 else -1)
        p = uy * dint if side == "x" else ux * dint
        r["ouv"].append((side, sg, p, min(TW / comp + 20, (r["h"] if side == "x" else r["w"]) - 40)))
        floor_end = -MARGE - TH
        r["tun"] = dict(k=k, side=(side, sg), x0=x0, y0=y0, ux=ux, uy=uy, L=L, s_end=L + PUITS_FIN + floor_end / PUITS_PENTE,
                        floor_end=floor_end, s_d=dint + T / 2 / comp, dint=dint, door=t["door"])
    # 2) emprise : salle + les 4 bouches possibles (le plafond reste sous le relief quel que soit le côté choisi)
    for r in rooms.values():
        hw, hh = r["w"] / 2 + T, r["h"] / 2 + T
        rects = [(r["cx"] - hw, r["cy"] - hh, r["cx"] + hw, r["cy"] + hh)] + [rect_bouche(r, sx_, sy_, STUB) for sx_, sy_ in ((1, 0), (-1, 0), (0, 1), (0, -1))]
        r["hmin"] = min(hmin_rect(v, *q) for q in rects)
    v6 = next(q for q in lst if q["code"] == "V6")["shape"]
    # 3) construire
    for code, r in sorted(rooms.items()):
        e = r["e"]; tn = r["tun"]
        if tn:
            ks, ck = profil(v, tn["x0"], tn["y0"], tn["ux"], tn["uy"], tn["s_end"], r["hmin"] - MARGE - 80, tn["s_d"] + 200)
            top = ck[0] + 80
        else:
            top = r["hmin"] - MARGE
        z = top - RH
        # entrée de surface : côté face à la place d'apparition d'abord, puis décalages le long du mur ; premier puits valide
        cand = [c for c in (("x", 1), ("x", -1), ("y", 1), ("y", -1)) if not (tn and tn["side"] == c)]
        vx, vy = v6["x"] * M - r["cx"], v6["y"] * M - r["cy"]
        cand.sort(key=lambda c: -(vx * c[1] if c[0] == "x" else vy * c[1]))
        choix = None
        for ext in (0, 800, 1600, 2400, 3200):
            for ax, sg in cand:
                half, perp = (r["w"] / 2, r["h"] / 2) if ax == "x" else (r["h"] / 2, r["w"] / 2)
                nx, ny = (sg, 0) if ax == "x" else (0, sg)
                for frac in (0, 0.5, -0.5, 1, -1):
                    p = frac * (perp - TW)
                    sx_, sy_ = (r["cx"] + nx * (half + T + STUB + ext), r["cy"] + p) if ax == "x" else (r["cx"] + p, r["cy"] + ny * (half + T + STUB + ext))
                    res = puits_ok(v, lst, sx_, sy_, nx, ny, z, e["level"], ext)
                    if res and empiete(rooms, code, r, ax, p, nx, ny, sx_, sy_, res[0]): res = None
                    if res: choix = (ax, sg, p, nx, ny, sx_, sy_, res); break
                if choix: break
            if choix: break
        assert choix, f"{v} {code} : pas d'entrée de surface"
        ax, sg, p, nx, ny, sx_, sy_, (Ls, zs) = choix
        r["ouv"].append((ax, sg, p, TW + 20))
        salle(grp, v, code, r["cx"], r["cy"], r["w"], r["h"], top, None, r["ouv"])
        door = e["door"]
        half = r["w"] / 2 if ax == "x" else r["h"] / 2
        ox, oy = (0, p) if ax == "x" else (p, 0)   # décalage de la bouche le long du mur
        galerie(grp, v, code, "stub", [(r["cx"] + ox + nx * half, r["cy"] + oy + ny * half, z), (sx_, sy_, z)])
        lines.append(f'D({grp},"Porte {code}",{r["cx"] + ox + nx * (half + T / 2):.1f}f,{r["cy"] + oy + ny * (half + T / 2):.1f}f,{z:.1f}f,{math.degrees(math.atan2(-ny, -nx)):.1f}f,{TW}f,{TH}f,NarutoRp.World.DoorRule.{door["rule"]},"{v}","{door["domain"] or ""}",{"true" if door["oneWay"] else "false"});')
        X = puits(grp, v, code, sx_, sy_, nx, ny, z, zs)
        kiosque(g, X[0], X[1], nx, ny, zs, code)
        if tn:
            fl = [ck_ - TH for ck_ in ck]
            pts = [(tn["x0"] + tn["ux"] * s, tn["y0"] + tn["uy"] * s, f) for s, f in zip([tn["dint"]] + ks[1:], fl)]
            galerie(grp, v, "T" + tn["k"], "tunnel", pts)
            assert abs(fl[-1] - tn["floor_end"]) < 1e-6, (v, tn["k"], fl[-1])
            sd = tn["s_d"]
            lines.append(f'D({grp},"Porte T{tn["k"]}",{tn["x0"] + tn["ux"] * sd:.1f}f,{tn["y0"] + tn["uy"] * sd:.1f}f,{fl[0]:.1f}f,{math.degrees(math.atan2(-tn["uy"], -tn["ux"])):.1f}f,{TW}f,{TH}f,NarutoRp.World.DoorRule.{tn["door"]["rule"]},"{v}","",true);')
            puits(grp, v, "X" + tn["k"], tn["x0"] + tn["ux"] * tn["s_end"], tn["y0"] + tn["uy"] * tn["s_end"], tn["ux"], tn["uy"], tn["floor_end"], 0.0)
    # 4) passage caché H1 : ouvert aux deux bouts, un puits côté milieu, un côté bas, sans porte ni kiosque
    h1 = by.get("H1")
    if h1:
        (ax, ay), (bx, by_) = h1["shape"]["pts"]
        x0, y0, x1, y1 = ax * M, ay * M, bx * M, by_ * M
        L = math.hypot(x1 - x0, y1 - y0); ux, uy = (x1 - x0) / L, (y1 - y0) / L
        ks, ck = profil(v, x0, y0, ux, uy, L)
        fl = [c - TH for c in ck]
        galerie(grp, v, "H1", "passage", [(x0 + ux * s, y0 + uy * s, f) for s, f in zip(ks, fl)])
        for suf, (px, py, dx, dy, f0, niv) in (("a", (x0, y0, -ux, -uy, fl[0], "milieu")), ("b", (x1, y1, ux, uy, fl[-1], "bas"))):
            res = puits_ok(v, lst, px, py, dx, dy, f0, niv)
            assert res, f"{v} H1{suf} : pas de sortie"
            puits(grp, v, "H1" + suf, px, py, dx, dy, f0, res[1])

def kiosque(g, x, y, ux, uy, zs, code):
    """Petit auvent sur 2 poteaux au-dessus de l'escalier d'une salle."""
    yaw = math.degrees(math.atan2(uy, ux)); lat = (-uy, ux)
    for sg in (-1, 1):
        B(g, f"Entrée {code} (poteau)", x + lat[0] * sg * 130 - ux * 20, y + lat[1] * sg * 130 - uy * 20, 40, 40, 260, COMMON["wood"], zs - 5, yaw)
    B(g, f"Entrée {code} (auvent)", x - ux * 20, y - uy * 20, 300, 340, 30, (0.55, 0.3, 0.2), zs + 260, yaw)

def puits(grp, v, code, px, py, ux, uy, floor0, zs):
    """Puits : sol incliné (25 deg) de (px, py) (sol à floor0) jusqu'à la surface zs sous un trou ; murets, mur de tête, palier.
    Renvoie le centre (x, y) du trou (unités locales) ; enregistre le trou dans HOLES_OUT."""
    L = (zs - floor0) / PUITS_PENTE - PUITS_FIN
    lat = (-uy, ux)
    P = lambda s, o=0: (px + ux * s + lat[0] * o, py + uy * s + lat[1] * o)
    X = P(L)
    s_top = L + PUITS_FIN
    wx, wy = local_to_world(v, X[0] / M, X[1] / M)
    wx2, wy2 = local_to_world(v, (X[0] + ux * 100) / M, (X[1] + uy * 100) / M)
    n_ = math.hypot(wx2 - wx, wy2 - wy)
    SHAFTS.append(dict(v=v, code=code, floor_end=floor0, t_end=-L, zs=zs, start=(round(px, 1), round(py, 1), floor0), top=(round(X[0], 1), round(X[1], 1))))
    HOLES_OUT.append(dict(v=v, code=code, x=wx, y=wy, ux=(wx2 - wx) / n_, uy=(wy2 - wy) / n_, zs=zs, tier=niveau(math.hypot(X[0], X[1]) / M)))
    d = (ux * s_top, uy * s_top, zs - floor0)
    n3 = math.sqrt(sum(k * k for k in d)); m = P(s_top / 2)
    W2 = PUITS_LARG + 2 * T
    G(grp, v, code, "puits", f"{code} puits (sol)", (m[0], m[1], (floor0 + zs) / 2 - T / 2), (n3 * 1.03, W2, T), d, SOUS["puits"])
    m = P(s_top + 80)
    G(grp, v, code, "puits", f"{code} palier", (m[0], m[1], zs - 2 - T / 2), (200, W2, T), (ux, uy, 0), SOUS["puits"])
    n = math.ceil((s_top - T) / 200); ln = (s_top - T) / n
    for i in range(n):
        s = T + (i + 0.5) * ln
        zf = floor0 + s * PUITS_PENTE
        zlo, zhi = zf - ln * PUITS_PENTE / 2 - T, zs - 1
        for sg in (-1, 1):
            m = P(s, sg * (PUITS_LARG / 2 + T / 2))
            G(grp, v, code, "puits", f"{code} muret", (m[0], m[1], (zlo + zhi) / 2), (ln * 1.03, T, zhi - zlo), (ux, uy, 0), SOUS["mur"])
    # Toit : le relief (heightfield) n'arrête rien par en dessous (vérifié en jeu, T6) ; dalle parallèle au sol, aussi haute que le relief le permet.
    # Arrêtée là où il reste < 90 u sous le relief (près du trou) : sortir par là, c'est sortir à côté du trou.
    RT = 20
    for i in range(n):
        s0, s1 = T + i * ln, T + (i + 1) * ln
        c = min(hl(*P(s0 + k * (s1 - s0) / 10, o)) - (floor0 + (s0 + k * (s1 - s0) / 10) * PUITS_PENTE)
                for k in range(11) for o in (-W2 / 2, 0, W2 / 2))
        b = min(c - RT - 2, TH)
        if b < 90: break
        m = P((s0 + s1) / 2)
        G(grp, v, code, "puits", f"{code} toit du puits", (m[0], m[1], floor0 + (s0 + s1) / 2 * PUITS_PENTE + b + RT / 2),
          (ln * math.sqrt(1 + PUITS_PENTE ** 2) * 1.03, W2, RT), d, SOUS["plafond"])
    zlo = floor0 - T
    side = (W2 - TW) / 2
    for sg in (-1, 1):
        m = P(T / 2, sg * (TW / 2 + side / 2))
        G(grp, v, code, "puits", f"{code} mur de tête", (m[0], m[1], (zlo + zs - 1) / 2), (T, side, zs - 1 - zlo), (ux, uy, 0), SOUS["mur"])
    m = P(T / 2)
    G(grp, v, code, "puits", f"{code} linteau", (m[0], m[1], (floor0 + TH + zs - 1) / 2), (T, TW, zs - 1 - floor0 - TH), (ux, uy, 0), SOUS["mur"])
    return X

# ── Villages
SPAWN_OFFS = ((-500, 500), (500, 500), (-500, -500), (500, -500))
for v, style in STYLES.items():
    lst = sorted(els.get(v, []), key=lambda e: e.get("order", 999))
    if not lst: continue
    CUR[0] = v
    cx, cy = CORNERS[v]
    name = v.capitalize()
    lines.append(f'// ═══ {name.upper()} ═══')
    lines.append(f'var {v} = new GameObject( true, "{name} (blocage)" ); {v}.WorldPosition = new Vector3({cx}f,{cy}f,0); {v}.WorldRotation = Rotation.FromYaw({yaw_of(v):.4f}f);')
    g = v
    rnd_h = __import__("random").Random(v)
    surf = [e for e in lst if e["layer"] == "surface"]
    for e in surf:
        s, f, nm = e["shape"], e["fill"], e["name"].replace('"', "'")
        if e["code"][0] == "X": continue   # sortie cachée : un simple trou dans le relief (puits dans les souterrains)
        c = col(style, f)
        tag = f'{e["code"]} {nm}'
        if s["t"] == "houses":
            for i, (x, y, r) in enumerate(houses(lst, e)):
                h = rnd_h.randint(*style["house_h"])
                ZB(g, f"Maison {i + 1}", x * M, y * M, 2 * r * M * 0.9, 2 * r * M * 0.9, h, style["built"])
                hz = hl(x * M, y * M)
                if s.get("style") == "dome": S(g, f"Dôme {i + 1}", x * M, y * M, hz + h, 2 * r * M * 0.9, style["roof"])
                else: B(g, f"Toit {i + 1}", x * M, y * M, 2 * r * M, 2 * r * M, 30, rnd_h.choice(style.get("roofs", [style["roof"]])), hz + h)
        elif s["t"] == "ring":
            if e["code"] == "A1":
                lo, hi = style["rampart_h"]; rr = __import__("random").Random(v + "A1")
                ring(g, tag, s["x"], s["y"], s["r"], s["w"], s["a0"], s["a1"], hi, c, 12, lambda i: rr.randint(lo, hi))
            elif f in FLAT:
                ring(g, tag, s["x"], s["y"], s["r"], s["w"], s["a0"], s["a1"], FLAT[f], c, 10, flat=True)
            else:
                if s.get("fill"): disk(g, f"{tag} (sol)", s["x"] * M, s["y"] * M, s["r"] * M, 4, COMMON["zone"], False, flat=True)
                ring(g, tag, s["x"], s["y"], s["r"], s["w"], s["a0"], s["a1"], HEIGHT.get(f, 400), c, 8)
        elif s["t"] == "line":
            p = s["pts"]
            for i in range(len(p) - 1):
                (x1, y1), (x2, y2) = p[i], p[i + 1]
                L = math.hypot(x2 - x1, y2 - y1); k = max(1, math.ceil(L * M / 600))
                for j in range(k):
                    t0, t1 = j / k, (j + 1) / k
                    ZB(g, tag, (x1 + (x2 - x1) * (t0 + t1) / 2) * M, (y1 + (y2 - y1) * (t0 + t1) / 2) * M, L / k * 1.04 * M, s["w"] * M, FLAT.get(f, 6), c,
                       math.degrees(math.atan2(y2 - y1, x2 - x1)), False, True)
        elif s["t"] == "circle":
            if f in FLAT: disk(g, tag, s["x"] * M, s["y"] * M, s["r"] * M, FLAT[f], c, False, flat=True)
            if f == "park":
                tr = __import__("random").Random(tag)
                for i in range(int(s["r"] * s["r"] / 60)):
                    ang, rad = tr.uniform(0, 2 * math.pi), s["r"] * math.sqrt(tr.random()) * 0.9
                    tx, ty = (s["x"] + math.cos(ang) * rad) * M, (s["y"] + math.sin(ang) * rad) * M
                    hz = hl(tx, ty)
                    B(g, "Tronc", tx, ty, 50, 50, 450, COMMON["wood"], hz); S(g, "Feuillage", tx, ty, hz + 560, 380, COMMON["leaf"])
            elif f not in FLAT:
                h = HEIGHT.get(f, 400)
                disk(g, tag, s["x"] * M, s["y"] * M, s["r"] * M, h, c, True)
                if f == "tower": S(g, f"{tag} (dôme)", s["x"] * M, s["y"] * M, hl(s["x"] * M, s["y"] * M) + h, s["r"] * 2 * M * 0.85, style["roof"])
        elif s["t"] == "rect" and f == "domain":
            x, y, w, h = s["x"] * M, s["y"] * M, s["w"] * M, s["h"] * M
            wall_h = 520 if e.get("hidden") else 300
            t, gap = 40, 300
            if abs(s["y"]) >= abs(s["x"]): gate_side = "S" if s["y"] > 0 else "N"
            else: gate_side = "W" if s["x"] > 0 else "E"
            sides = {"N": (x, y + h / 2, w, t), "S": (x, y - h / 2, w, t), "E": (x + w / 2, y, t, h), "W": (x - w / 2, y, t, h)}
            for side, (bx, by, bw, bh) in sides.items():
                if side != gate_side:
                    ZB(g, f"{tag} (mur)", bx, by, bw, bh, wall_h, COMMON["wall"]); continue
                if bw > bh:
                    half = (w - gap) / 2
                    ZB(g, f"{tag} (mur)", bx - (gap + half) / 2, by, half, t, wall_h, COMMON["wall"])
                    ZB(g, f"{tag} (mur)", bx + (gap + half) / 2, by, half, t, wall_h, COMMON["wall"])
                else:
                    half = (h - gap) / 2
                    ZB(g, f"{tag} (mur)", bx, by - (gap + half) / 2, t, half, wall_h, COMMON["wall"])
                    ZB(g, f"{tag} (mur)", bx, by + (gap + half) / 2, t, half, wall_h, COMMON["wall"])
            ZB(g, f"{tag} (jardin)", x, y, w - 2 * t, h - 2 * t, 3, COMMON["park"], 0, False, True)
            hz = hl(x, y)
            B(g, f"{tag} (demeure)", x, y, w * 0.55, h * 0.5, 420, COMMON["domain"], hz)
            B(g, f"{tag} (toit)", x, y, w * 0.55 + 80, h * 0.5 + 80, 40, style["roof"], hz + 420)
        elif s["t"] == "rect":
            flat = f in FLAT
            ZB(g, tag, s["x"] * M, s["y"] * M, s["w"] * M, s["h"] * M, FLAT[f] if flat else HEIGHT.get(f, 400), c, s.get("rot", 0), not flat, flat)
            if not flat and f == "built":
                B(g, f"{tag} (toit)", s["x"] * M, s["y"] * M, s["w"] * M + 60, s["h"] * M + 60, 40, style["roof"], hl(s["x"] * M, s["y"] * M) + HEIGHT.get(f, 400), s.get("rot", 0))
    # Styles : murets de terrasse (Suna), ponts (Kumo)
    if v == "suna": levres_suna(g)
    if v == "kumo": ponts_kumo(g, surf)
    # Apparition : 4 points sur la place V6
    sp = next(e for e in lst if e["code"] == "V6")
    for dx, dy in SPAWN_OFFS:
        px, py = sp["shape"]["x"] * M + dx, sp["shape"]["y"] * M + dy
        lines.append(f'{{ var s = new GameObject({g}, true, "Spawn"); s.LocalPosition = new Vector3({px:.0f}f,{py:.0f}f,{hl(px, py) + 20:.0f}f); s.LocalRotation = Rotation.FromYaw(-90); var sc = s.Components.Create<SpawnPoint>(); sc.Enabled = true; }}')
    # Frontière : bornes tous les 5° sur l'arc à 900 m, poste-frontière sur la route
    for i in range(1, 18):
        ang = 225 + i * 5
        if ang == 270: continue
        r = math.radians(ang)
        bx, by = math.cos(r) * (BORDER - 150), math.sin(r) * (BORDER - 150)
        ZB(g, "Borne", bx, by, 90, 90, 360, COMMON["rock2"])
    ZB(g, "Poste-frontière (tour de guet)", 900, -BORDER, 700, 700, 1200, style["rock"])
    ZB(g, "Poste-frontière (pilier)", -650, -BORDER, 160, 160, 700, style["rock"])
    ZB(g, "Poste-frontière (pilier)", 650, -BORDER, 160, 160, 700, style["rock"])
    B(g, "Poste-frontière (arche)", 0, -BORDER, 1500, 200, 120, style["roof"], hl(0, -BORDER) + 700)
    # Route : de la porte au bord du centre commun
    gate = next((e for e in lst if e["code"] == "A2"), None)
    gy = (gate["shape"]["y"] - gate["shape"]["h"] / 2) * M if gate else -20000
    end = -(DIST - CENTRE_R)
    B(g, "Route vers le centre", 0, (gy + end) / 2, 640, gy - end, 5, COMMON["road"], hl(0, (gy + end) / 2), 0, False)
    # Souterrains (repère local du village, z monde)
    lines.append(f'var {v}Sous = new GameObject( {v}, true, "Souterrains" );')
    souterrains(v, lst, f"{v}Sous", g)

# ── Murs invisibles du monde
E = INNER + 2400
for nm, x, y, sx, sy in (("nord", 0, E + 100, 2 * E + 400, 200), ("sud", 0, -E - 100, 2 * E + 400, 200), ("est", E + 100, 0, 200, 2 * E + 400), ("ouest", -E - 100, 0, 200, 2 * E + 400)):
    lines.append(f'W("Mur invisible ({nm})",{x}f,{y}f,{sx}f,{sy}f,20000f);')

header = '''// GÉNÉRÉ par tools/plan-vers-blocage.py à partir des plans v3 (ne pas modifier à la main).
// Blocage des trois villages posé sur le relief (composant Terrain « Relief », voir tools/terrain-import.cs.txt, à lancer avant) :
// niveaux, murets, ponts, souterrains (salles, tunnels, puits) et portes. Nécessite NarutoRp.World.LockedDoor (Task 4).
// Lancer avec konoha.scene ouverte, puis écrire la scène (docs/sbox-reference.md §11, piège de sauvegarde). Relançable.
var scene = Editor.SceneEditorSession.Active.Scene;
var box = Model.Load( "models/dev/box.vmdl" );
var sphere = Model.Load( "models/dev/sphere.vmdl" );
int count = 0;
void B( GameObject parent, string name, float x, float y, float sx, float sy, float sz, Color tint, float z0, float yaw, bool collide )
{
	var g = new GameObject( parent, true, name );
	g.LocalPosition = new Vector3( x, y, z0 + sz / 2f );
	g.LocalRotation = Rotation.FromYaw( yaw );
	g.LocalScale = new Vector3( sx / 50f, sy / 50f, sz / 50f );
	var r = g.Components.Create<ModelRenderer>(); r.Model = box; r.Tint = tint;
	if ( collide ) g.Components.Create<BoxCollider>().Scale = new Vector3( 50, 50, 50 );
	count++;
}
// Boîte centrée en (x, y, z), orientée selon la direction (dx, dy, dz) : x = longueur, y = largeur, z = épaisseur.
void G( GameObject parent, string name, float x, float y, float z, float sx, float sy, float sz, float dx, float dy, float dz, Color tint )
{
	var g = new GameObject( parent, true, name );
	g.LocalPosition = new Vector3( x, y, z );
	g.LocalRotation = Rotation.LookAt( new Vector3( dx, dy, dz ), Vector3.Up );
	g.LocalScale = new Vector3( sx / 50f, sy / 50f, sz / 50f );
	var r = g.Components.Create<ModelRenderer>(); r.Model = box; r.Tint = tint;
	g.Components.Create<BoxCollider>().Scale = new Vector3( 50, 50, 50 );
	count++;
}
void S( GameObject parent, string name, float x, float y, float z, float d, Color tint )
{
	var g = new GameObject( parent, true, name );
	g.LocalPosition = new Vector3( x, y, z );
	g.LocalScale = new Vector3( d / 64f, d / 64f, d / 128f );
	var r = g.Components.Create<ModelRenderer>(); r.Model = sphere; r.Tint = tint;
	count++;
}
// Porte : objet « Porte X » (Forward = côté autorisé, vers la salle) + enfant « Battant » (le vantail qui bloque l'ouverture).
void D( GameObject parent, string name, float x, float y, float z, float yaw, float width, float height, NarutoRp.World.DoorRule rule, string village, string domain, bool oneWay )
{
	var d = new GameObject( parent, true, name );
	d.LocalPosition = new Vector3( x, y, z );
	d.LocalRotation = Rotation.FromYaw( yaw );
	d.NetworkMode = NetworkMode.Object; // objet réseau : IsOpen synchronisé et RequestOpen appelable par les clients
	var lk = d.Components.Create<NarutoRp.World.LockedDoor>();
	lk.Rule = rule; lk.Village = village; lk.DomainId = domain; lk.OneWay = oneWay;
	var b = new GameObject( d, true, "Battant" );
	b.LocalPosition = new Vector3( 0, 0, height / 2f );
	b.LocalScale = new Vector3( 20f / 50f, width / 50f, height / 50f );
	var r = b.Components.Create<ModelRenderer>(); r.Model = box; r.Tint = new Color( 0.5f, 0.35f, 0.2f );
	b.Components.Create<BoxCollider>().Scale = new Vector3( 50, 50, 50 );
	count++;
}
using ( scene.Push() )
{
	// (a) Nettoyage : anciens groupes, frontière de Konoha, grand sol du monde, sols plats devenus inutiles (le relief fait le sol).
	foreach ( var old in scene.GetAllObjects( true ).Where( g => g.Name is "Konoha (blocage)" or "Suna (blocage)" or "Kumo (blocage)" or "Vue d'ensemble (blocage)" or "Frontière du Pays du Feu" or "Limites du monde" ).ToList() )
		old.Destroy();
	foreach ( var g in scene.GetAllObjects( true ).Where( g => g.Name == "Sol" && ( g.WorldScale.x > 1000 || g.Parent?.Name == "Centre commun" ) ).ToList() ) g.Destroy();

	// (b) « Pays du Feu - zone de Konoha » et « Terres neutres de Konoha » : de l'ancien repère (village en (-60000, 0, 0), centre vers +x) au coin de Konoha.
	// Les enfants sont en coordonnées monde (groupes à l'origine).
	// - Terres neutres : déplacement rigide (elles restent entre le pays et le centre).
	// - Pays du Feu : l'ancienne forêt faisait un anneau complet de 250 à 500 m autour du village ; le nouveau pays est un quart d'anneau
	//   de 520 à 875 m depuis le coin. Chaque objet (ou chaque clairière, d'un bloc) : angle / 4 autour de l'axe du centre, rayon étiré.
	var corner = new Vector3( %CX%f, %CY%f, 0 );
	var rotD = Rotation.FromYaw( %YAW%f - 90f );
	var oldPos = new Vector3( -60000f, 0, 0 );
	var axis = ( Vector3.Zero - corner ); var axisLen = axis.Length; axis /= axisLen;
	var sorties = new Vector3[] { %HOLES% };
	bool Bad( Vector3 p )
	{
		var d = new Vector3( p.x - corner.x, p.y - corner.y, 0 );
		if ( d.Length < %VR% ) return true;                                           // dans le village
		if ( MathF.Max( MathF.Abs( p.x ), MathF.Abs( p.y ) ) > %INNER%f - 1500f ) return true;   // bande de relief ou hors carte
		var along = d.x * axis.x + d.y * axis.y; var lateral = MathF.Abs( d.x * axis.y - d.y * axis.x );
		if ( along > 0 && lateral < 1200f ) return true;                              // sur la route du centre
		foreach ( var h in sorties ) if ( ( p.WithZ( 0 ) - h ).Length < 700f ) return true;   // sur un puits de sortie (trou du relief)
		return false;
	}
	// Ancienne position -> nouvelle (et rotation à appliquer).
	Vector3 MapPays( Vector3 p, out Rotation turn )
	{
		var d = p - oldPos;
		var r = MathF.Sqrt( d.x * d.x + d.y * d.y );
		var a = MathF.Atan2( d.y, d.x ) * 180f / MathF.PI;                            // 0 = vers le centre
		var na = %YAW%f - 90f + a * 0.21f;                                            // ± 38 deg autour de l'axe du centre
		var nr = 20800f + 1200f + ( r - 9900f ) * ( 35000f - 22000f ) / ( 20100f - 9900f );
		turn = Rotation.FromYaw( na - a );
		return corner + new Vector3( MathF.Cos( na * MathF.PI / 180f ), MathF.Sin( na * MathF.PI / 180f ), 0 ) * nr + Vector3.Up * p.z;
	}
	foreach ( var gname in new[] { "Pays du Feu - zone de Konoha", "Terres neutres de Konoha" } )
	{
		var grp = scene.GetAllObjects( true ).FirstOrDefault( g => g.Name == gname );
		if ( grp is null || grp.Tags.Has( "deplace-v3" ) ) continue;
		grp.Tags.Add( "deplace-v3" );
		var kill = new System.Collections.Generic.List<GameObject>();
		if ( gname.StartsWith( "Terres" ) )
		{
			grp.WorldPosition = corner - rotD * oldPos;
			grp.WorldRotation = rotD;
		}
		else
		{
			foreach ( var c in grp.Children.ToList() )
			{
				if ( c.Children.Count == 0 )
				{
					var np = MapPays( c.WorldPosition, out var turn );
					c.WorldPosition = np; c.WorldRotation = turn * c.WorldRotation;
					continue;
				}
				// clairière : bloc rigide autour du centre de ses objets
				var ctr = Vector3.Zero; foreach ( var l in c.Children ) ctr += l.WorldPosition; ctr /= c.Children.Count;
				var nc = MapPays( ctr.WithZ( 0 ), out var t2 );
				c.WorldRotation = t2 * c.WorldRotation;
				c.WorldPosition = nc - t2 * ctr.WithZ( 0 );   // le groupe est à l'origine : ses enfants (coordonnées monde) suivent
			}
		}
		void Walk( GameObject o )
		{
			foreach ( var c in o.Children )
			{
				if ( c.Name == "Sol neutre" || c.Name.StartsWith( "Route" ) || ( c.Children.Count == 0 && Bad( c.WorldPosition ) ) ) kill.Add( c );
				else Walk( c );
			}
		}
		Walk( grp );
		foreach ( var c in kill ) c.Destroy();
	}

	// (c) Centre commun : routes et panneaux de l'ancien tracé tournés vers les nouveaux coins (Konoha 180 -> 135 deg, Suna 60 -> 45, Kumo -60 -> -45).
	foreach ( var (nom, da) in new[] { ("Konoha", -45f), ("Suna", -15f), ("Kumo", 15f) } )
		foreach ( var g in scene.GetAllObjects( true ).Where( g => g.Parent?.Name == "Centre commun" && ( g.Name.StartsWith( "Route vers " + nom ) || g.Name.StartsWith( "Panneau " + nom ) ) && !g.Tags.Has( "deplace-v3" ) ).ToList() )
		{
			var rc = Rotation.FromYaw( da );
			g.WorldPosition = rc * g.WorldPosition; g.WorldRotation = rc * g.WorldRotation; g.Tags.Add( "deplace-v3" );
		}

	// (g) Murs invisibles autour de la zone jouable : collision seule, de 0 à 20 000 u de haut.
	var limites = new GameObject( true, "Limites du monde" );
	void W( string name, float x, float y, float sx, float sy, float sz )
	{
		var g = new GameObject( limites, true, name );
		g.WorldPosition = new Vector3( x, y, sz / 2f );
		g.Components.Create<BoxCollider>().Scale = new Vector3( sx, sy, sz );
		count++;
	}
'''.replace("%CX%", str(CORNERS["konoha"][0])).replace("%CY%", str(CORNERS["konoha"][1])).replace("%YAW%", f'{yaw_of("konoha"):.4f}') \
   .replace("%HOLES%", ", ".join(f'new Vector3({h["x"]:.0f}f,{h["y"]:.0f}f,0)' for h in HOLES_OUT if h["v"] == "konoha"))    .replace("%VR%", "20800f").replace("%INNER%", str(INNER))
footer = '''
	var cam = scene.GetAllComponents<CameraComponent>().FirstOrDefault();
	if ( cam is not null ) cam.ZFar = 120000;
}
return $"{count} volumes";
'''
open(OUT, "w", encoding="utf-8").write(header + "\n".join("\t" + l for l in "\n".join(lines).split("\n")) + footer)
json.dump(HOLES_OUT, open(os.path.join(src, "trous.holes"), "w"), indent=0)   # lu par tools/relief.py (extension != .json : pas pris pour un plan)
print(f"{len(lines)} lignes, {len(UNDER)} boîtes souterraines -> {os.path.abspath(OUT)}")
