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
UNDER, SHAFTS = [], []
T, TW, TH, RH = 40, 120, 220, 300     # épaisseur des parois, largeur / hauteur intérieures du tunnel, hauteur des salles
MARGE = 110                           # dessus du plafond (intérieur) sous le relief : les dalles de 40 u laissent >= 60 u
LTAN = math.tan(math.radians(10))     # pente max cherchée des tunnels (la limite de marche est 35 deg)

def G(grp, v, code, kind, name, c, s, d, color):
    c = tuple(round(k, 1) for k in c); s = tuple(round(k, 1) for k in s); d = tuple(round(k, 4) for k in d)
    UNDER.append(dict(v=v, code=code, kind=kind, c=c, s=s, dir=d))
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

def souterrains(v, lst, grp):
    CUR[0] = v
    by = {e["code"]: e for e in lst}
    rooms = {}
    for e in lst:
        if e["layer"] == "souterrain" and e["code"][0] == "S":
            s = e["shape"]
            rooms[e["code"]] = dict(e=e, cx=s["x"] * M, cy=s["y"] * M, w=s["w"] * M, h=s["h"] * M, ouv=[], tun=None)
    def sale_de(p): return next(r for r in rooms.values() if abs(r["cx"] - p[0] * M) < 1 and abs(r["cy"] - p[1] * M) < 1)
    # 1) tunnels : profil sous le relief, puits jusqu'à la sortie
    for k in ("1", "2"):
        t, x = by["T" + k], by["X" + k]
        (ax, ay), (bx, by_) = t["shape"]["pts"]
        r = sale_de((ax, ay))
        x0, y0, x1, y1 = ax * M, ay * M, bx * M, by_ * M
        L = math.hypot(x1 - x0, y1 - y0); ux, uy = (x1 - x0) / L, (y1 - y0) / L
        side, dint = cote(ux, uy, r["w"] / 2, r["h"] / 2)
        comp = abs(ux) if side == "x" else abs(uy)
        s_d = dint + T / 2 / comp
        sg = (1 if ux > 0 else -1) if side == "x" else (1 if uy > 0 else -1)
        p = uy * dint if side == "x" else ux * dint
        r["ouv"].append((side, sg, p, min(TW / comp + 20, (r["h"] if side == "x" else r["w"]) - 40)))
        r["tun"] = dict(k=k, side=(side, sg))
        floor_end = -MARGE - TH
        s_end = L + PUITS_FIN + floor_end / PUITS_PENTE
        r["stub_side"] = None
        r["tun"].update(x0=x0, y0=y0, ux=ux, uy=uy, L=L, s_end=s_end, floor_end=floor_end, s_d=s_d, dint=dint, door=t["door"])
    # 2) côté et emprise de la bouche (stub) de chaque salle : face au centre du village, hors côté du tunnel
    for code, r in rooms.items():
        d0 = math.hypot(r["cx"], r["cy"]) or 1
        cand = [(("x", 1), 1, 0), (("x", -1), -1, 0), (("y", 1), 0, 1), (("y", -1), 0, -1)]
        cand = [c for c in cand if not (r["tun"] and r["tun"]["side"] == c[0])]
        (ax, sg), nx, ny = max(cand, key=lambda c: -(r["cx"] * c[1] + r["cy"] * c[2]) / d0)
        r["stub"] = dict(ax=ax, sg=sg, n=(nx, ny))
        half = r["w"] / 2 if ax == "x" else r["h"] / 2
        ex = half + T + 600
        xa, xb = sorted((r["cx"] + nx * (half), r["cx"] + nx * ex)); ya, yb = sorted((r["cy"] + ny * half, r["cy"] + ny * ex))
        if ax == "x": ya, yb = r["cy"] - TW / 2 - T, r["cy"] + TW / 2 + T
        else: xa, xb = r["cx"] - TW / 2 - T, r["cx"] + TW / 2 + T
        r["hmin"] = min(hmin_rect(v, r["cx"] - r["w"] / 2 - T, r["cy"] - r["h"] / 2 - T, r["cx"] + r["w"] / 2 + T, r["cy"] + r["h"] / 2 + T),
                        hmin_rect(v, xa, ya, xb, yb))
    # 3) construire
    for code, r in sorted(rooms.items()):
        e = r["e"]; tn = r["tun"]
        if tn:
            ks, ck = profil(v, tn["x0"], tn["y0"], tn["ux"], tn["uy"], tn["s_end"], r["hmin"] - MARGE - 80, tn["s_d"] + 200)
            top = ck[0] + 80
        else:
            top = r["hmin"] - MARGE
        ax, sg = r["stub"]["ax"], r["stub"]["sg"]
        r["ouv"].append((ax, sg, 0, TW + 20))
        z = salle(grp, v, code, r["cx"], r["cy"], r["w"], r["h"], top, None, r["ouv"])
        door = e["door"]
        nx, ny = r["stub"]["n"]
        half = r["w"] / 2 if ax == "x" else r["h"] / 2
        galerie(grp, v, code, "stub", [(r["cx"] + nx * half, r["cy"] + ny * half, z), (r["cx"] + nx * (half + T + 600), r["cy"] + ny * (half + T + 600), z)])
        G(grp, v, code, "stub", f"{code} fond", (r["cx"] + nx * (half + T + 600 + T / 2), r["cy"] + ny * (half + T + 600 + T / 2), z + TH / 2), (T, TW + 2 * T, TH + 2 * T), (nx, ny, 0), SOUS["mur"])
        lines.append(f'D({grp},"Porte {code}",{r["cx"] + nx * (half + T / 2):.1f}f,{r["cy"] + ny * (half + T / 2):.1f}f,{z:.1f}f,{math.degrees(math.atan2(-ny, -nx)):.1f}f,{TW}f,{TH}f,NarutoRp.World.DoorRule.{door["rule"]},"{v}","{door["domain"] or ""}",{"true" if door["oneWay"] else "false"});')
        if tn:
            fl = [ck_ - TH for ck_ in ck]
            pts = [(tn["x0"] + tn["ux"] * s, tn["y0"] + tn["uy"] * s, f) for s, f in zip([tn["dint"]] + ks[1:], fl)]
            galerie(grp, v, "T" + tn["k"], "tunnel", pts)
            assert abs(fl[-1] - tn["floor_end"]) < 1e-6, (v, tn["k"], fl[-1])
            sd = tn["s_d"]
            lines.append(f'D({grp},"Porte T{tn["k"]}",{tn["x0"] + tn["ux"] * sd:.1f}f,{tn["y0"] + tn["uy"] * sd:.1f}f,{fl[0]:.1f}f,{math.degrees(math.atan2(-tn["uy"], -tn["ux"])):.1f}f,{TW}f,{TH}f,NarutoRp.World.DoorRule.{tn["door"]["rule"]},"{v}","",true);')
            puits(grp, v, "X" + tn["k"], tn["x0"], tn["y0"], tn["ux"], tn["uy"], tn["L"], tn["s_end"], tn["floor_end"])
    # 4) passage caché H1 (sans porte, sous la marche entre le bas et le milieu ; culs-de-sac à ses deux bouts)
    h1 = by.get("H1")
    if h1:
        (ax, ay), (bx, by_) = h1["shape"]["pts"]
        x0, y0, x1, y1 = ax * M, ay * M, bx * M, by_ * M
        L = math.hypot(x1 - x0, y1 - y0); ux, uy = (x1 - x0) / L, (y1 - y0) / L
        ks, ck = profil(v, x0, y0, ux, uy, L)
        galerie(grp, v, "H1", "passage", [(x0 + ux * s, y0 + uy * s, c - TH) for s, c in zip(ks, ck)])
        for s, c in ((0, ck[0]), (L, ck[-1])):
            G(grp, v, "H1", "passage", "H1 fond", (x0 + ux * s, y0 + uy * s, c - TH / 2), (T, TW + 2 * T, TH + 2 * T), (ux, uy, 0), SOUS["mur"])

def puits(grp, v, code, x0, y0, ux, uy, L, s_end, floor_end):
    """Puits d'une sortie : sol incliné (25 deg) de la fin du tunnel jusqu'à z = 0 sous le trou, murets, mur de tête, palier."""
    SHAFTS.append(dict(v=v, code=code, floor_end=floor_end, t_end=s_end - L))
    lat = (-uy, ux)
    P = lambda s, o=0: (x0 + ux * s + lat[0] * o, y0 + uy * s + lat[1] * o)
    s_top = L + PUITS_FIN
    d = (ux * (s_top - s_end), uy * (s_top - s_end), -floor_end)
    n3 = math.sqrt(sum(k * k for k in d)); m = P((s_end + s_top) / 2)
    W2 = PUITS_LARG + 2 * T
    G(grp, v, code, "puits", f"{code} puits (sol)", (m[0], m[1], floor_end / 2 - T / 2), (n3 * 1.03, W2, T), d, SOUS["puits"])
    m = P(s_top + 80)
    G(grp, v, code, "puits", f"{code} palier", (m[0], m[1], -2 - T / 2), (200, W2, T), (ux, uy, 0), SOUS["puits"])
    n = math.ceil((s_top - s_end - T) / 200); ln = (s_top - s_end - T) / n
    for i in range(n):
        s = s_end + T + (i + 0.5) * ln
        zf = sol_puits(s - L)
        zlo, zhi = zf - ln * PUITS_PENTE / 2 - T, -1
        for sg in (-1, 1):
            m = P(s, sg * (PUITS_LARG / 2 + T / 2))
            G(grp, v, code, "puits", f"{code} muret", (m[0], m[1], (zlo + zhi) / 2), (ln * 1.03, T, zhi - zlo), (ux, uy, 0), SOUS["mur"])
    zlo = floor_end - T
    side = (W2 - TW) / 2
    for sg in (-1, 1):
        m = P(s_end + T / 2, sg * (TW / 2 + side / 2))
        G(grp, v, code, "puits", f"{code} mur de tête", (m[0], m[1], (zlo - 1) / 2), (T, side, -1 - zlo), (ux, uy, 0), SOUS["mur"])
    m = P(s_end + T / 2)
    G(grp, v, code, "puits", f"{code} linteau", (m[0], m[1], (floor_end + TH - 1) / 2), (T, TW, -1 - floor_end - TH), (ux, uy, 0), SOUS["mur"])

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
    souterrains(v, lst, f"{v}Sous")

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

	// (b) « Pays du Feu - zone de Konoha » et « Terres neutres de Konoha » : de l'ancien repère (position (-60000, 0, 0), lacet 90) au coin de Konoha.
	// Le groupe est à l'origine et ses enfants sont en coordonnées monde : un déplacement rigide du groupe les emporte tous.
	var corner = new Vector3( %CX%f, %CY%f, 0 );
	var rotD = Rotation.FromYaw( %YAW%f - 90f );
	var oldPos = new Vector3( -60000f, 0, 0 );
	var axis = ( Vector3.Zero - corner ); var axisLen = axis.Length; axis /= axisLen;
	bool Bad( Vector3 p )
	{
		var d = new Vector3( p.x - corner.x, p.y - corner.y, 0 );
		if ( d.Length < %VR% ) return true;                                           // dans le village
		if ( MathF.Max( MathF.Abs( p.x ), MathF.Abs( p.y ) ) > %INNER%f ) return true;   // bande de relief ou hors carte
		var along = d.x * axis.x + d.y * axis.y; var lateral = MathF.Abs( d.x * axis.y - d.y * axis.x );
		return along > 0 && lateral < 1200f;                                          // sur la route du centre
	}
	foreach ( var gname in new[] { "Pays du Feu - zone de Konoha", "Terres neutres de Konoha" } )
	{
		var grp = scene.GetAllObjects( true ).FirstOrDefault( g => g.Name == gname );
		if ( grp is null || grp.Tags.Has( "deplace-v3" ) ) continue;
		grp.WorldPosition = corner - rotD * oldPos;
		grp.WorldRotation = rotD;
		grp.Tags.Add( "deplace-v3" );
		var kill = new System.Collections.Generic.List<GameObject>();
		void Walk( GameObject o )
		{
			foreach ( var c in o.Children )
			{
				if ( c.Name == "Sol neutre" || c.Name.StartsWith( "Route" ) || ( c.Components.Get<ModelRenderer>() is not null && Bad( c.WorldPosition ) ) ) kill.Add( c );
				else Walk( c );
			}
		}
		Walk( grp );
		foreach ( var c in kill ) c.Destroy();
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
   .replace("%VR%", "20800f").replace("%INNER%", str(INNER))
footer = '''
	var cam = scene.GetAllComponents<CameraComponent>().FirstOrDefault();
	if ( cam is not null ) cam.ZFar = 120000;
}
return $"{count} volumes";
'''
open(OUT, "w", encoding="utf-8").write(header + "\n".join("\t" + l for l in "\n".join(lines).split("\n")) + footer)
print(f"{len(lines)} lignes, {len(UNDER)} boîtes souterraines -> {os.path.abspath(OUT)}")
