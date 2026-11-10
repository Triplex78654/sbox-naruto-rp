# Tests du relief (executables sans dependance : python tools/test_relief.py)
import importlib.util, json, math, os, sys, tempfile
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from collections import deque
from carte import CORNERS, ECHELLE_VILLAGE, RAMPS, TIER_Z, TIERS, INNER, M, HALF, RAYON_REMPART_M, local_to_world, territoire
import relief
from relief import height_at

VILLAGES = ["konoha", "suna", "kumo"]
R_V = RAYON_REMPART_M          # rayon du rempart (m)
R_V6 = 420 * ECHELLE_VILLAGE   # rayon de la place d'apparition V6 (axe 270)
PAS = 10 * M                   # grille de 10 m
N_G = INNER // PAS             # indices de -N_G a N_G
TAN35 = math.tan(math.radians(35))


def pt(v, ang, r_m):
    a = math.radians(ang)
    return local_to_world(v, r_m * math.cos(a), r_m * math.sin(a))


def pente_deg(dh, dist_u):
    return math.degrees(math.atan2(abs(dh), dist_u))


def periph(d, n=400):
    # n points regulierement repartis sur le carre |x| ou |y| = d
    pts = []
    for k in range(n):
        t = 8 * d * k / n
        c, s = divmod(t, 2 * d)
        c, s = int(c), s - d
        pts.append([(s, -d), (d, s), (-s, d), (-d, -s)][c])
    return pts


def bord_infranchissable():
    for x, y in periph(INNER + 3000):
        assert height_at(x, y) >= 1900, (x, y, height_at(x, y))
    # montee : plateau atteint a +1200 u, pente moyenne >= 50 deg sur les 600 premieres u, partout
    # (dos des villages et abords des coins compris)
    pts = periph(INNER, 400)
    for cx, cy in CORNERS.values():
        for k in range(100):
            pts += [(cx + math.copysign(20000 * k / 99, -cx), cy), (cx, cy + math.copysign(20000 * k / 99, -cy))]
    for x, y in pts:
        k = 0 if abs(x) >= abs(y) else 1
        p, q, r = [x, y], [x, y], [x, y]
        q[k] += math.copysign(600, q[k])
        r[k] += math.copysign(1200, r[k])
        assert pente_deg(height_at(*q) - height_at(*p), 600) >= 50, (x, y)
        assert height_at(*r) >= 1900, (x, y)


def niveaux_village():
    for v in VILLAGES:
        bas, mil, haut = TIER_Z[v]
        # place d'apparition, et a 30 m des frontieres de niveau (bandes sans bosses)
        for ang, r, att in ((270, R_V6, bas), (270, TIERS["milieu"][0] + 30, mil), (240, TIERS["milieu"][0] - 30, haut)):
            h = height_at(*pt(v, ang, r))
            assert abs(h - att) <= 5, (v, ang, r, h, att)


def rampes_marchables():
    for v in VILLAGES:
        for a in RAMPS[v]:
            r0 = round(100 * ECHELLE_VILLAGE)
            prev = height_at(*pt(v, a, r0))
            for r in range(r0 + 1, TIERS["bas"][0] + 22):
                h = height_at(*pt(v, a, r))
                assert pente_deg(h - prev, M) <= 35, (v, a, r)
                prev = h
            h1, h2 = height_at(*pt(v, a + 8, TIERS["bas"][0] - 3)), height_at(*pt(v, a + 8, TIERS["bas"][0] + 3))
            assert pente_deg(h2 - h1, 6 * M) > 50, (v, a)


def apparitions_plates():
    for v in VILLAGES:
        cx, cy = pt(v, 270, R_V6)
        hs = [height_at(cx + dx, cy + dy) for dx, dy in ((500, 0), (-500, 0), (0, 500), (0, -500))]
        assert max(hs) - min(hs) < 10, (v, hs)


# ── Relief de fond, eaux, ponts, routes (grille de 10 m sur la zone jouable, calculee une fois)
_H = {}


def grille():
    if not _H:
        for j in range(-N_G, N_G + 1):
            for i in range(-N_G, N_G + 1):
                _H[(i, j)] = height_at(i * PAS, j * PAS)
    return _H


def dans_village(x, y, marge_m=0):
    return any(math.hypot(x - CORNERS[v][0], y - CORNERS[v][1]) <= (R_V + marge_m) * M for v in VILLAGES)


def dist_seg(px, py, a, b):
    (ax, ay), (bx, by) = a[:2], b[:2]
    l2 = (bx - ax) ** 2 + (by - ay) ** 2
    t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((px - ax) * (bx - ax) + (py - ay) * (by - ay)) / l2))
    return math.hypot(px - ax - t * (bx - ax), py - ay - t * (by - ay))


def dist_poly(x, y, pts):
    return min(dist_seg(x, y, a, b) for a, b in zip(pts, pts[1:]))


def cases_berges():
    # cases a <= larg/2 + 30 m d'une eau (berges et parois : pente libre)
    out = set()
    for e in relief.EAUX:
        rad = e["larg_u"] / 2 + 30 * M
        for a, b in zip(e["pts"], e["pts"][1:]):
            i0, i1 = math.floor((min(a[0], b[0]) - rad) / PAS), math.ceil((max(a[0], b[0]) + rad) / PAS)
            j0, j1 = math.floor((min(a[1], b[1]) - rad) / PAS), math.ceil((max(a[1], b[1]) + rad) / PAS)
            for j in range(j0, j1 + 1):
                for i in range(i0, i1 + 1):
                    if dist_seg(i * PAS, j * PAS, a, b) <= rad:
                        out.add((i, j))
    return out


def sur_tablier(x, y, p):
    a = math.radians(p["yaw"])
    dx, dy = x - p["x"], y - p["y"]
    return abs(dx * math.cos(a) + dy * math.sin(a)) <= p["long_u"] / 2 + 1 and abs(-dx * math.sin(a) + dy * math.cos(a)) <= p["larg_u"] / 2 + 1


def bouts_tablier(p):
    a = math.radians(p["yaw"])
    dx, dy = math.cos(a) * p["long_u"] / 2, math.sin(a) * p["long_u"] / 2
    return (p["x"] - dx, p["y"] - dy), (p["x"] + dx, p["y"] + dy)


def pont_pose(p):
    # les deux bouts du tablier arrivent au niveau du sol (on monte dessus et on en descend des deux cotes)
    return all(abs(height_at(*b) - p["z"]) <= 50 for b in bouts_tablier(p))


def ponts_poses():
    assert relief.PONTS
    for p in relief.PONTS:
        assert pont_pose(p), (p, [round(height_at(*b)) for b in bouts_tablier(p)])


def pentes_marchables():
    H, berges = grille(), cases_berges()
    pires = 0.0
    for (i, j), h in H.items():
        if (i, j) in berges or dans_village(i * PAS, j * PAS, 20):
            continue
        for q in ((i + 1, j), (i, j + 1)):
            if q in H and q not in berges and not dans_village(q[0] * PAS, q[1] * PAS, 20):
                d = abs(H[q] - h)
                pires = max(pires, d)
                assert d <= TAN35 * PAS, ((i * PAS, j * PAS), q, round(pente_deg(d, PAS), 1))
    print("   pente maxi", round(pente_deg(pires, PAS), 1), "deg")


def surface_route(x, y):
    for p in relief.PONTS:
        if sur_tablier(x, y, p):
            return p["z"]
    return height_at(x, y)


def routes():
    assert len(relief.ROUTES) == 3 and {r["v"] for r in relief.ROUTES} == set(VILLAGES)
    for rt in relief.ROUTES:
        prev = None
        for a, b in zip(rt["pts"], rt["pts"][1:]):
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            ux, uy = (b[0] - a[0]) / L, (b[1] - a[1]) / L
            for k in range(int(L // 200) + 1):
                x, y = a[0] + ux * 200 * k, a[1] + uy * 200 * k
                z = surface_route(x, y)
                if prev is not None:
                    assert pente_deg(z - prev, 200) <= 10, (rt["v"], round(x), round(y), z, prev)
                prev = z
                trav = [surface_route(x - uy * o, y + ux * o) for o in (-400, -200, 0, 200, 400)]
                assert max(trav) - min(trav) <= 5, (rt["v"], round(x), round(y), trav)
        # chaque croisement route x eau a son pont
        for a, b in zip(rt["pts"], rt["pts"][1:]):
            for e in relief.EAUX:
                for c, d in zip(e["pts"], e["pts"][1:]):
                    den = (b[0] - a[0]) * (d[1] - c[1]) - (b[1] - a[1]) * (d[0] - c[0])
                    if den == 0:
                        continue
                    t = ((c[0] - a[0]) * (d[1] - c[1]) - (c[1] - a[1]) * (d[0] - c[0])) / den
                    s = ((c[0] - a[0]) * (b[1] - a[1]) - (c[1] - a[1]) * (b[0] - a[0])) / den
                    if 0 <= t <= 1 and 0 <= s <= 1:
                        x, y = a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])
                        assert any(math.hypot(p["x"] - x, p["y"] - y) <= 20 * M for p in relief.PONTS), (rt["v"], e["nom"], round(x), round(y))


def _bfs(depart, voisin_ok, but):
    vu, f = {depart}, deque([depart])
    while f:
        c = f.popleft()
        if but(c):
            return c
        for q in ((c[0] + 1, c[1]), (c[0] - 1, c[1]), (c[0], c[1] + 1), (c[0], c[1] - 1)):
            if q not in vu and voisin_ok(c, q):
                vu.add(q)
                f.append(q)
    return None


def chemins():
    H = grille()
    def ok(c, q):
        if q not in H or dans_village(q[0] * PAS, q[1] * PAS):
            return False
        if abs(H[q] - H[c]) <= TAN35 * PAS:
            return True
        return any(sur_tablier(c[0] * PAS, c[1] * PAS, p) and sur_tablier(q[0] * PAS, q[1] * PAS, p) for p in ponts_ok)
    ponts_ok = [p for p in relief.PONTS if pont_pose(p)]
    portes = {}
    for v in VILLAGES:
        x, y = pt(v, 270, R_V + 30)
        portes[v] = (round(x / PAS), round(y / PAS))
    for v in VILLAGES:
        for but in [(0, 0)] + [portes[w] for w in VILLAGES if w != v]:
            assert _bfs(portes[v], ok, lambda c: c == but), (v, "->", but)


def eau_descend():
    assert relief.EAUX
    for e in relief.EAUX:
        zs = [p[2] for p in e["pts"]]
        assert all(a > b for a, b in zip(zs, zs[1:])), e["nom"]
        for x, y, z in e["pts"]:
            assert height_at(x, y) < z, (e["nom"], round(x), round(y))
        assert all(territoire(x, y) == "konoha" for x, y, _ in e["pts"]), e["nom"]
    assert 4 <= len(relief.CASCADES) <= 8, len(relief.CASCADES)
    for c in relief.CASCADES:
        assert c["z_haut"] - c["z_bas"] >= 120, c
        assert min(dist_poly(c["x"], c["y"], e["pts"]) for e in relief.EAUX) <= 5 * M, c


def ravins_sortie():
    # depuis le fond (les deux bouts et le milieu de chaque troncon entre cascades), on sort a pied de l'eau et des berges
    p5 = PAS / 2
    for e in relief.EAUX:
        rad = e["larg_u"] / 2 + 30 * M
        pts = e["pts"]
        coupes = [0] + [k for k in range(1, len(pts)) if pts[k - 1][2] - pts[k][2] >= 120] + [len(pts) - 1]
        departs = [0, len(pts) - 1] + [(a + b) // 2 for a, b in zip(coupes, coupes[1:])]
        assert len(departs) >= 4, e["nom"]
        for k in departs:
            x0, y0, _ = pts[k]
            P = lambda c: (x0 + c[0] * p5, y0 + c[1] * p5)
            hc = {}
            def h(c):
                if c not in hc:
                    hc[c] = height_at(*P(c))
                return hc[c]
            ok = lambda c, q: abs(q[0]) < 160 and abs(q[1]) < 160 and abs(h(q) - h(c)) <= TAN35 * p5
            assert _bfs((0, 0), ok, lambda c: dist_poly(*P(c), pts) > rad), (e["nom"], k, round(x0), round(y0))


def centre_plat():
    assert abs(height_at(0, 0)) < 1e-6
    H = grille()
    n = 0
    for (i, j), h in H.items():
        if territoire(i * PAS, j * PAS) != "centre":
            continue
        for q in ((i + 1, j), (i, j + 1)):
            if q in H and territoire(q[0] * PAS, q[1] * PAS) == "centre":
                n += 1
                assert pente_deg(H[q] - h, PAS) <= 5, (i, j)
    assert n > 1000


def relief_present():
    H = grille()
    routes_v = {r["v"]: r for r in relief.ROUTES}
    for v, mini in (("konoha", 600), ("suna", 600), ("kumo", 1600)):
        hs = [h for (i, j), h in H.items() if territoire(i * PAS, j * PAS) == v and not dans_village(i * PAS, j * PAS, 20)
              and dist_poly(i * PAS, j * PAS, routes_v[v]["pts"]) > routes_v[v]["larg_u"] / 2 + 10 * M]
        assert max(hs) - min(hs) >= mini, (v, round(max(hs) - min(hs)))


def _charge(nom, argv):
    sys.argv = argv
    spec = importlib.util.spec_from_file_location(nom, os.path.join(os.path.dirname(os.path.abspath(__file__)), nom.replace("_", "-") + ".py"))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def _generer():
    tmp = tempfile.mkdtemp()
    _charge("plans_villages_v3", ["plans-villages-v3.py", tmp])
    gen = _charge("plan_vers_blocage", ["plan-vers-blocage.py", tmp, os.path.join(tmp, "monde.cs.txt")])
    relief.charger_trous(tmp)
    relief.charger_emprises(tmp)
    return tmp, gen


def generation_stable():
    # generation dans ce processus (bosses deja chargees) == generation livree (scripts seuls, processus neufs)
    import subprocess
    a, _ = _generer()
    b = tempfile.mkdtemp()
    for script, extra in (("plans-villages-v3.py", []), ("plan-vers-blocage.py", [os.path.join(b, "monde.cs.txt")])):
        subprocess.run([sys.executable, os.path.join(os.path.dirname(os.path.abspath(__file__)), script), b] + extra,
                       check=True, stdout=subprocess.DEVNULL)
    for f in ("trous.holes", "souterrains.emprises", "monde.cs.txt"):
        assert open(os.path.join(a, f), encoding="utf-8").read() == open(os.path.join(b, f), encoding="utf-8").read(), f


def _codes_attendus(tmp):
    att = set()
    for f in os.listdir(tmp):
        if f.endswith(".json"):
            d = json.load(open(os.path.join(tmp, f), encoding="utf-8"))
            c = d["code"]
            if c[0] == "X" or (c[0] == "S" and d["layer"] == "souterrain"):
                att.add((d["village"], c))
            if c == "H1":
                att |= {(d["village"], "H1a"), (d["village"], "H1b")}
    return att


def trous_sur_sorties():
    tmp, gen = _generer()
    att = _codes_attendus(tmp)
    assert {(h["v"], h["code"]) for h in relief.HOLES} == att and len(relief.HOLES) == len(att), "trous != X* + S* + H1"
    _, holes, _, _ = relief.build()
    step = relief.SIZE / (relief.RES - 1)
    cells = [(k % relief.RES, k // relief.RES) for k, h in enumerate(holes) if h]
    assert cells
    for i, j in cells:  # aucun trou ailleurs que sur un puits
        x, y = -HALF + i * step, -HALF + j * step
        assert any(math.hypot(x - h["x"], y - h["y"]) < 400 for h in relief.HOLES), (x, y)
    for h in relief.HOLES:  # chaque puits a des cases trouees autour de son centre
        assert sum(1 for i, j in cells if math.hypot(-HALF + i * step - h["x"], -HALF + j * step - h["y"]) < 150) >= 4, h
    assert len(gen.SHAFTS) == len(att)
    for s in gen.SHAFTS:
        assert math.degrees(math.atan(relief.PUITS_PENTE)) <= 35
        assert abs(s["floor_end"] - relief.sol_puits(s["t_end"], s["zs"])) < 1e-6, s["code"]


def souterrains_accessibles():
    tmp, gen = _generer()
    _, holes, _, _ = relief.build()
    step = relief.SIZE / (relief.RES - 1)
    def dans_un_trou(h, t):
        # point a la distance t du centre du trou h, sur l'axe : la case de grille la plus proche est-elle trouee ?
        x, y = h["x"] + h["ux"] * t, h["y"] + h["uy"] * t
        i, j = round((x + HALF) / step), round((y + HALF) / step)
        return any(holes[(j + dj) * relief.RES + i + di] for di in (-1, 0, 1) for dj in (-1, 0, 1))
    par_code = {(s["v"], s["code"]): s for s in gen.SHAFTS}
    hole = {(h["v"], h["code"]): h for h in relief.HOLES}
    for (v, code), s in par_code.items():
        h = hole[(v, code)]
        assert dans_un_trou(h, 0) and dans_un_trou(h, relief.TROU_T1 - 30), (v, code)   # on sort bien dans le trou
        if code[0] == "X":   # sortie cachee hors rempart : le relief de fond reste a plat autour du trou
            for dx, dy in ((0, 0), (300, 0), (-300, 0), (0, 300), (0, -300)):
                assert abs(height_at(h["x"] + dx, h["y"] + dy) - h["zs"]) <= 5, (v, code, dx, dy)
        # polyligne : depart (fin du couloir) -> sol a z = surface sous le trou ; pente <= 35 deg
        x0, y0, z0 = s["start"]
        xt, yt = s["top"]
        zt = relief.sol_puits(relief.PUITS_FIN, s["zs"])
        run = math.hypot(xt - x0, yt - y0) + relief.PUITS_FIN
        assert math.degrees(math.atan2(zt - z0, run)) <= 35, (v, code)
        assert abs(zt - s["zs"]) < 1e-6
        if code[0] == "S":  # le depart est le bout du couloir de la salle
            nrm = lambda d: math.sqrt(sum(k * k for k in d))
            bouts = [(b["c"][0] + b["dir"][0] / nrm(b["dir"]) * b["s"][0] / 2, b["c"][1] + b["dir"][1] / nrm(b["dir"]) * b["s"][0] / 2) for b in gen.UNDER
                     if b["v"] == v and b["code"] == code and b["kind"] == "stub" and b["name"].endswith("sol")]
            assert any(math.hypot(bx - x0, by - y0) < 20 for bx, by in bouts), (v, code, "puits non raccorde au couloir")
        else:
            assert code[:2] in ("X1", "X2", "H1")
    for v in ("konoha", "suna", "kumo"):
        tiers = sorted(hole[(v, c)]["tier"] for c in ("H1a", "H1b"))
        assert tiers == ["bas", "milieu"], (v, tiers)
        assert not any(b["code"] == "H1" and "porte" in b["name"].lower() for b in gen.UNDER)


def _coins(b):
    # 8 coins monde-local (x, y, z) d'une boite G : centre, tailles, direction (dx, dy, dz)
    dx, dy, dz = b["dir"]
    n = math.sqrt(dx * dx + dy * dy + dz * dz)
    f = (dx / n, dy / n, dz / n)
    h = math.hypot(f[0], f[1])
    l = (-f[1] / h, f[0] / h, 0) if h > 1e-9 else (0, 1, 0)
    u = (f[1] * l[2] - f[2] * l[1], f[2] * l[0] - f[0] * l[2], f[0] * l[1] - f[1] * l[0])
    out = []
    for a in (-1, 1):
        for c in (-1, 1):
            for d in (-1, 1):
                out.append(tuple(b["c"][k] + a * b["s"][0] / 2 * f[k] + c * b["s"][1] / 2 * l[k] + d * b["s"][2] / 2 * u[k] for k in range(3)))
    return out


def souterrains_sous_terrain():
    tmp, gen = _generer()
    assert gen.UNDER
    pires = []
    for b in gen.UNDER:
        if b["kind"] == "puits":
            if "toit" in b["name"]:   # le toit du puits reste sous le relief (pas de dalle visible en surface)
                for x, y, z in _coins(b):
                    assert z <= height_at(*local_to_world(b["v"], x / M, y / M)) + 1, (b["v"], b["code"], round(z))
            continue
        v = b["v"]
        for x, y, z in _coins(b):
            h = height_at(*local_to_world(v, x / M, y / M))
            pires.append(h - 60 - z)
            assert z <= h - 60, (b["code"], b["kind"], round(z), round(h))
    # les tunnels restent marchables (<= 35 deg) et les pieces alignees avec leur tunnel
    for b in gen.UNDER:
        if b["kind"] == "tunnel":
            dx, dy, dz = b["dir"]
            assert math.degrees(math.atan2(abs(dz), math.hypot(dx, dy))) <= 35, b["code"]
    print("   marge mini", round(min(pires)), "u,", len(gen.UNDER), "boites souterraines")


def _axes(b):
    # axes (avant, gauche, haut) d'une boite G, comme _coins
    dx, dy, dz = b["dir"]
    n = math.sqrt(dx * dx + dy * dy + dz * dz)
    f = (dx / n, dy / n, dz / n)
    h = math.hypot(f[0], f[1])
    l = (-f[1] / h, f[0] / h, 0) if h > 1e-9 else (0, 1, 0)
    u = (f[1] * l[2] - f[2] * l[1], f[2] * l[0] - f[0] * l[2], f[0] * l[1] - f[1] * l[0])
    return [f, l, u]


def _obb_coupe(a, b, eps=2.0):
    """Test des axes separateurs (15 axes) : les deux boites se chevauchent-elles de plus de eps ?"""
    A, B = _axes(a), _axes(b)
    ha, hb = [k / 2 for k in a["s"]], [k / 2 for k in b["s"]]
    t = [b["c"][k] - a["c"][k] for k in range(3)]
    dot = lambda p, q: p[0] * q[0] + p[1] * q[1] + p[2] * q[2]
    cr = lambda p, q: (p[1] * q[2] - p[2] * q[1], p[2] * q[0] - p[0] * q[2], p[0] * q[1] - p[1] * q[0])
    for ax in A + B + [cr(p, q) for p in A for q in B]:
        n = math.sqrt(dot(ax, ax))
        if n < 1e-6: continue
        ax = tuple(k / n for k in ax)
        ra = sum(ha[i] * abs(dot(A[i], ax)) for i in range(3))
        rb = sum(hb[i] * abs(dot(B[i], ax)) for i in range(3))
        if abs(dot(t, ax)) > ra + rb - eps: return False
    return True


def souterrains_sans_intrusion():
    """Aucun couloir, tunnel, puits ou passage n'entre dans la boite d'une AUTRE salle : on n'entre dans une salle que par sa porte."""
    tmp, gen = _generer()
    tun_salle = {}   # (village, "T1") -> code de la salle de depart du tunnel
    salles = {}
    for f in os.listdir(tmp):
        if f.endswith(".json"):
            d = json.load(open(os.path.join(tmp, f), encoding="utf-8"))
            if d["layer"] == "souterrain" and d["code"][0] == "S":
                salles[(d["village"], (d["shape"]["x"], d["shape"]["y"]))] = d["code"]
    for f in os.listdir(tmp):
        if f.endswith(".json"):
            d = json.load(open(os.path.join(tmp, f), encoding="utf-8"))
            if d["code"][0] == "T" and d["layer"] == "souterrain":
                tun_salle[(d["village"], d["code"])] = salles[(d["village"], tuple(d["shape"]["pts"][0]))]
    def proprio(b):
        c = b["code"]
        if c[0] in "TX" and c[1:].isdigit(): return tun_salle.get((b["v"], "T" + c[1:]))
        return c
    # boite englobante de chaque salle (sol + plafond + murs)
    boites = {}
    for b in gen.UNDER:
        if b["kind"] != "salle": continue
        lo_hi = boites.setdefault((b["v"], b["code"]), [[1e18] * 3, [-1e18] * 3])
        for p in _coins(b):
            for k in range(3):
                lo_hi[0][k] = min(lo_hi[0][k], p[k]); lo_hi[1][k] = max(lo_hi[1][k], p[k])
    assert boites
    n = 0
    for (v, code), (lo, hi) in boites.items():
        rb = dict(c=[(lo[k] + hi[k]) / 2 for k in range(3)], s=[hi[k] - lo[k] for k in range(3)], dir=(1, 0, 0))
        for b in gen.UNDER:
            if b["v"] != v or b["kind"] == "salle" or proprio(b) == code: continue
            n += 1
            assert not _obb_coupe(rb, b), (v, code, b["code"], b["kind"], b["name"])
    print("  ", n, "paires boite / salle testees")


def _echantillons(s):
    # points (m locaux) sur et dans une forme des plans, tous les ~2 m
    t = s["t"]
    if t == "rect":
        nx, ny = math.ceil(s["w"] / 2), math.ceil(s["h"] / 2)
        return [(s["x"] - s["w"] / 2 + s["w"] * i / nx, s["y"] - s["h"] / 2 + s["h"] * j / ny) for i in range(nx + 1) for j in range(ny + 1)]
    if t == "line":
        out = []
        for (ax, ay), (bx, by) in zip(s["pts"], s["pts"][1:]):
            L = math.hypot(bx - ax, by - ay)
            n = math.ceil(L / 2)
            for k in range(n + 1):
                for o in (-s["w"] / 2, 0, s["w"] / 2):
                    out.append((ax + (bx - ax) * k / n - (by - ay) / L * o, ay + (by - ay) * k / n + (bx - ax) / L * o))
        return out
    x, y = (s["zx"], s["zy"]) if t == "houses" else (s["x"], s["y"])
    r = s["zr"] if t == "houses" else s["r"] + s.get("w", 0) / 2
    return [(x, y)] + [(x + f * r * math.cos(math.radians(a)), y + f * r * math.sin(math.radians(a))) for f in (0.33, 0.66, 1) for a in range(0, 360, 15)]


def village_bosses():
    """Bosses des niveaux : presentes (>= 1 m), marchables, et sol plat sous chaque element de surface des plans."""
    tmp, _ = _generer()
    n = 0
    for f in os.listdir(tmp):
        if not f.endswith(".json"):
            continue
        d = json.load(open(os.path.join(tmp, f), encoding="utf-8"))
        if d["layer"] != "surface" or d["code"] == "A1" or d["code"][0] == "X" or d["fill"] == "mountain":
            continue   # rempart, sorties hors village, falaise de Konoha (deborde du quart, sur le bord de carte)
        pts = _echantillons(d["shape"])
        if d["shape"]["t"] == "line":   # une rue peut finir sur une rampe (Suna, axe 270) : on ne garde que le plat
            hw = 22.5 if d["village"] == "kumo" else 12.5
            pts = [p for p in pts if min(abs(math.hypot(*p) - rb) for rb in (TIERS["milieu"][0], TIERS["bas"][0])) > hw + 3]
        hs = [height_at(*local_to_world(d["village"], x, y)) for x, y in pts]
        n += 1
        assert max(hs) - min(hs) <= 10, (d["village"], d["code"], round(max(hs) - min(hs)))
    pas = 2
    for v in VILLAGES:
        pire, maxi = 0.0, 0.0
        for i in range(-R_V // pas, R_V // pas):
            for j in range(-R_V // pas, 0):
                x, y = i * pas, j * pas
                b = relief._bosses(v, x, y)
                maxi = max(maxi, abs(b))
                if b or relief._bosses(v, x + pas, y) or relief._bosses(v, x, y + pas):
                    h = height_at(*local_to_world(v, x, y))
                    for q in ((x, y), (x + pas, y), (x, y + pas)):
                        if max(map(abs, local_to_world(v, *q))) > INNER:   # bord de carte : hors sujet
                            break
                    else:
                        for q in ((x + pas, y), (x, y + pas)):
                            pire = max(pire, pente_deg(height_at(*local_to_world(v, *q)) - h, pas * M))
        assert maxi >= M, (v, maxi)
        assert pire <= 35, (v, pire)
        print("  ", v, "bosse maxi", round(maxi / M, 1), "m, pente maxi", round(pire, 1), "deg")
    print("  ", n, "elements a plat")


def sols_biomes():
    # octet du sol : biome du territoire loin des frontieres, melange des deux biomes au milieu du fondu
    S = relief.sol_case
    def pur(x, y, b): return S(x, y) & 3 == b and S(x, y) >> 4 == 0   # base = b, sans melange
    assert pur(-30000, 30000, 0) and pur(30000, 30000, 1) and pur(30000, -30000, 2) and pur(-30000, -30000, 3) and pur(0, 0, 3)
    b = S(0, 30000)   # frontiere Konoha / Suna : foret et desert a 0,5
    assert {b & 3, b >> 2 & 3} == {0, 1} and 6 <= b >> 4 <= 8, b
    assert pur(-2000, 30000, 0)   # 50 m de la frontiere : hors du fondu de 80 m
    c = S(-1000, 30000)   # 25 m de la frontiere, cote foret : foret dominante, un peu de desert
    assert c & 3 == 0 and c >> 2 & 3 == 1 and 1 <= c >> 4 <= 6, c


if __name__ == "__main__":
    _generer()   # plans, trous et emprises (bosses des villages) charges pour tous les tests
    for f in (bord_infranchissable, niveaux_village, rampes_marchables, apparitions_plates,
              generation_stable, ponts_poses, pentes_marchables, routes, chemins, eau_descend, ravins_sortie, centre_plat, relief_present,
              trous_sur_sorties, souterrains_accessibles, souterrains_sous_terrain,
              souterrains_sans_intrusion, village_bosses, sols_biomes):
        f()
        print("OK", f.__name__)
