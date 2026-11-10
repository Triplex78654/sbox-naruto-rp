# Tests du blocage des territoires (routes, ponts, postes-frontiere, eau, contenu replace) sur les boites generees
# par plan-vers-blocage.py (gen.TERR). Executable sans dependance : python tools/test_blocage.py
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import carte
from carte import CORNERS, M, territoire, world_to_local
import relief
from relief import height_at
from test_relief import _generer, sur_tablier, bouts_tablier, dist_poly, dist_seg

R_V = carte.VILLAGE_R
TMP, GEN = None, None
NOMS_TERR = {"konoha": "Konoha", "suna": "Suna", "kumo": "Kumo", "libre": "4e coin", "centre": "Centre"}


def gen():
    global TMP, GEN
    if GEN is None:
        TMP, GEN = _generer()
    return GEN


def script():
    return open(os.path.join(TMP, "monde.cs.txt"), encoding="utf-8").read()


def coins(b):
    # 4 coins de l'emprise au sol d'une boite (centre c, taille s, direction dir)
    (x, y, _), (sx, sy, _), (dx, dy, _) = b["c"], b["s"], b["dir"]
    n = math.hypot(dx, dy)
    ux, uy = dx / n, dy / n
    return [(x + ux * a * sx / 2 - uy * c * sy / 2, y + uy * a * sx / 2 + ux * c * sy / 2) for a in (-1, 1) for c in (-1, 1)]


def bouts(b):
    # deux bouts de l'axe (x, y, z du dessus) d'une boite orientee
    (x, y, z), (sx, _, sz), (dx, dy, dz) = b["c"], b["s"], b["dir"]
    n = math.sqrt(dx * dx + dy * dy + dz * dz)
    return [(x + k * dx / n * sx / 2, y + k * dy / n * sx / 2, z + k * dz / n * sx / 2 + sz / 2) for k in (-1, 1)]


def boites(pred):
    return [b for b in gen().TERR if pred(b)]


def ponts_construits():
    for i, p in enumerate(relief.PONTS):
        nom = f"Pont {i + 1}"
        tab = boites(lambda b: b["name"] == f"{nom} (tablier)")
        gc = boites(lambda b: b["name"] == f"{nom} (garde-corps)")
        assert tab and gc, nom
        a = math.radians(p["yaw"])
        lat = lambda q: -(q[0] - p["x"]) * math.sin(a) + (q[1] - p["y"]) * math.cos(a)
        lon = lambda q: (q[0] - p["x"]) * math.cos(a) + (q[1] - p["y"]) * math.sin(a)
        assert any(lat(b["c"]) > 0 for b in gc) and any(lat(b["c"]) < 0 for b in gc), nom
        # profil du dessus du tablier : (position le long du pont, z)
        prof = sorted({(round(lon(q), 1), q[2]) for b in tab for q in bouts(b)})
        assert prof[0][0] <= -p["long_u"] / 2 + 1 and prof[-1][0] >= p["long_u"] / 2 - 1, (nom, prof[0], prof[-1])
        for (s, z), bx in zip((prof[0], prof[-1]), bouts_tablier(p)):
            assert abs(z - height_at(*bx)) <= 20, (nom, "bout", round(z), round(height_at(*bx)))
        def dessus(s):
            for (s0, z0), (s1, z1) in zip(prof, prof[1:]):
                if s0 <= s <= s1:
                    return z0 + (z1 - z0) * (s - s0) / max(1e-9, s1 - s0)
            return None
        n = 0
        for e in relief.EAUX:
            for x, y, z in e["pts"]:
                if sur_tablier(x, y, p):
                    n += 1
                    assert dessus(lon((x, y))) >= z + 150, (nom, e["nom"], round(dessus(lon((x, y)))), round(z))
        print("  ", nom, "eau dessous :", n, "points")


def chaussees():
    return gen().CHAUSSEES   # {v: [(x, y), ...]} : route du village (ROUTES) prolongee jusqu'au centre


def postes_frontiere():
    spawns = gen().SPAWNS
    assert spawns
    larg = relief.ROUTE_LARG_U
    for v, pts in chaussees().items():
        # frontieres traversees par la route (pas de 1 m)
        trav, prev = [], None
        for a, b in zip(pts, pts[1:]):
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            for k in range(int(L // M) + 1):
                x, y = a[0] + (b[0] - a[0]) * k * M / L, a[1] + (b[1] - a[1]) * k * M / L
                t = territoire(x, y)
                if prev is not None and t != prev:
                    trav.append((x, y, prev, t))
                prev = t
        postes = [q for q in gen().POSTES if q["route"] == v]
        assert len(trav) >= 1 and len(postes) == len(trav), (v, trav, postes)
        for x, y, _, _ in trav:
            assert any(math.hypot(q["x"] - x, q["y"] - y) <= 60 * M for q in postes), (v, round(x), round(y))
    bx = boites(lambda b: b["name"].startswith("Poste-frontière"))
    assert bx
    for b in bx:
        for c in coins(b) + [b["c"][:2]]:
            for pts in chaussees().values():
                assert dist_poly(*c, pts) >= larg / 2 + 3 * M, (b["name"], round(dist_poly(*c, pts)))
            for s in spawns:
                assert math.hypot(c[0] - s[0], c[1] - s[1]) >= 10 * M, b["name"]


def routes_suivent_relief():
    segs = boites(lambda b: b["name"].startswith("Route"))
    assert segs
    for b in segs:
        x, y, z = b["c"]
        assert abs(z - height_at(x, y)) <= 20, (b["name"], round(x), round(y), round(z), round(height_at(x, y)))
        assert min(dist_poly(x, y, pts) for pts in chaussees().values()) <= 1, b["name"]
    # chaque route de ROUTES est couverte (segments + tabliers des ponts)
    for rt in relief.ROUTES:
        (ax, ay), (bx, by) = rt["pts"][0], rt["pts"][-1]
        L = math.hypot(bx - ax, by - ay)
        for k in range(int(L // 100) + 1):
            x, y = ax + (bx - ax) * k * 100 / L, ay + (by - ay) * k * 100 / L
            ok = any(sur_tablier(x, y, p) for p in relief.PONTS) or any(
                dist_seg(x, y, *[q[:2] for q in bouts(b)]) <= 5 for b in segs)
            assert ok, (rt["v"], round(x), round(y))


def bornes_disparues():
    assert '"Borne"' not in script()
    assert not hasattr(carte, "ANCIEN_PAYS_R")


def groupes_territoires():
    s = script()
    for t, nom in NOMS_TERR.items():
        assert f'"{nom} (territoire)"' in s, nom
    for b in gen().TERR:
        assert territoire(*b["c"][:2]) == b["terr"], (b["name"], b["terr"], territoire(*b["c"][:2]))
    # relancable : tout le contenu hors villages est detruit puis reconstruit (aucun deplacement de l'existant)
    assert "deplace-v3" not in s and '"Monde (blocage)"' in s


def _hors_eau_routes(b):
    for c in coins(b) + [b["c"][:2]]:
        for pts in chaussees().values():
            assert dist_poly(*c, pts) > relief.ROUTE_LARG_U / 2, (b["name"], "sur la route")
        for e in relief.EAUX:
            assert dist_poly(*c, e["pts"]) > e["larg_u"] / 2 + 8 * M, (b["name"], "dans l'eau")
        for p in relief.PONTS:
            assert not sur_tablier(*c, p), (b["name"], "sur un pont")


def contenu_replace():
    feu = boites(lambda b: b["grp"] in ("Pays du Feu - zone de Konoha", "Terres neutres de Konoha"))
    cen = boites(lambda b: b["grp"] == "Centre commun")
    assert len(feu) > 600 and len(cen) > 30, (len(feu), len(cen))
    trous = [(h["x"], h["y"]) for h in gen().HOLES_OUT if h["v"] == "konoha"]
    for b in feu:
        for c in coins(b):
            assert territoire(*c) == "konoha", b["name"]
            assert math.hypot(*world_to_local("konoha", *c)) > R_V + 15, b["name"]
            assert all(math.hypot(c[0] - hx, c[1] - hy) > 30 * M for hx, hy in trous), b["name"]
        _hors_eau_routes(b)
    for b in cen:
        for c in coins(b):
            assert territoire(*c) == "centre", b["name"]
        _hors_eau_routes(b)


def pose_au_sol():
    n = 0
    for b in boites(lambda b: b["sol"]):
        hs = [height_at(*c) for c in coins(b) + [b["c"][:2]]]
        base, haut = b["c"][2] - b["s"][2] / 2, b["c"][2] + b["s"][2] / 2
        assert base <= min(hs) + 1 and haut > max(hs) + 20, (b["name"], round(base), round(haut), [round(h) for h in hs])
        n += 1
    assert n > 700, n
    print("  ", n, "boites posees au sol")


def eau():
    eaux = boites(lambda b: b["grp"] == "Eau")
    assert eaux and all(not b["collide"] and b["alpha"] < 1 for b in eaux)
    assert len([b for b in eaux if b["name"].startswith("Chute")]) == len(relief.CASCADES)
    assert len([b for b in eaux if b["name"].startswith("Bassin")]) == len(relief.CASCADES)
    for e in relief.EAUX:
        surf = [b for b in eaux if b["name"] == f"Eau ({e['nom']})"]
        assert surf, e["nom"]
        for b in surf:
            x, y, z = b["c"]
            zo = min(e["pts"], key=lambda q: math.hypot(q[0] - x, q[1] - y))[2]
            assert dist_poly(x, y, e["pts"]) <= 20 and abs(z - zo) <= 20, (e["nom"], round(z), round(zo))


if __name__ == "__main__":
    gen()
    for f in (ponts_construits, postes_frontiere, routes_suivent_relief, bornes_disparues, groupes_territoires,
              contenu_replace, pose_au_sol, eau):
        f()
        print("OK", f.__name__)
