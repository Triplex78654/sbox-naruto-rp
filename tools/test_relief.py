# Tests du relief (executables sans dependance : python tools/test_relief.py)
import importlib.util, json, math, os, sys, tempfile
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from carte import CORNERS, RAMPS, TIER_Z, INNER, M, HALF, local_to_world
import relief
from relief import height_at

VILLAGES = ["konoha", "suna", "kumo"]


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
        for ang, r, att in ((270, 420, bas), (270, 250, mil), (240, 80, haut)):
            h = height_at(*pt(v, ang, r))
            assert abs(h - att) <= 5, (v, ang, r, h, att)


def rampes_marchables():
    for v in VILLAGES:
        for a in RAMPS[v]:
            prev = height_at(*pt(v, a, 100))
            for r in range(101, 361):
                h = height_at(*pt(v, a, r))
                assert pente_deg(h - prev, M) <= 35, (v, a, r)
                prev = h
            h1, h2 = height_at(*pt(v, a + 8, 337)), height_at(*pt(v, a + 8, 343))
            assert pente_deg(h2 - h1, 6 * M) > 50, (v, a)


def centre_et_routes_plats():
    assert abs(height_at(0, 0)) < 1e-6
    for v in VILLAGES:
        gx, gy = pt(v, 270, 500)
        L = math.hypot(gx, gy)
        e = (L - 15000) / L
        for k in range(50):
            f = e * k / 49
            assert abs(height_at(gx * (1 - f), gy * (1 - f))) <= 5, (v, k)


def apparitions_plates():
    for v in VILLAGES:
        cx, cy = pt(v, 270, 420)
        hs = [height_at(cx + dx, cy + dy) for dx, dy in ((500, 0), (-500, 0), (0, 500), (0, -500))]
        assert max(hs) - min(hs) < 10, (v, hs)


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
    return tmp, gen


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
    _, holes, _ = relief.build()
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
    _, holes, _ = relief.build()
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


if __name__ == "__main__":
    for f in (bord_infranchissable, niveaux_village, rampes_marchables,
              centre_et_routes_plats, apparitions_plates, trous_sur_sorties, souterrains_accessibles, souterrains_sous_terrain):
        f()
        print("OK", f.__name__)
