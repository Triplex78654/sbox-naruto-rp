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


# ── Décor (tâche 5) : végétation, arches de Suna, cascades, budget d'objets
HA_PAYS = math.pi / 4 * (carte.PAYS_R ** 2 - R_V ** 2) / 1e4   # surface (ha) d'un pays hors village


def dist_boite(x, y, b):
    # distance au sol (u) de (x, y) a l'emprise de la boite b (0 dedans)
    (cx, cy, _), (sx, sy, _), (dx, dy, _) = b["c"], b["s"], b["dir"]
    n = math.hypot(dx, dy)
    lx, ly = ((x - cx) * dx + (y - cy) * dy) / n, (-(x - cx) * dy + (y - cy) * dx) / n
    return math.hypot(max(abs(lx) - sx / 2, 0), max(abs(ly) - sy / 2, 0))


def local_to_world_u(v, x_u, y_u):
    return carte.local_to_world(v, x_u / M, y_u / M)


def troncs(t=None):
    return boites(lambda b: b["name"] == "Tronc" and (t is None or b["terr"] == t))


def _degage(nom, pts, batiments):
    # pts : points de l'emprise au sol (monde) d'un arbre ou d'un rocher
    g = gen()
    trous = [(h["x"], h["y"]) for h in g.HOLES_OUT if h["code"][0] == "X"]
    for x, y in pts:
        for c in chaussees().values():
            assert dist_poly(x, y, c) >= relief.ROUTE_LARG_U / 2 + 8 * M, (nom, "route", round(x), round(y))
        for e in relief.EAUX:
            assert dist_poly(x, y, e["pts"]) >= e["larg_u"] / 2 + 8 * M, (nom, "eau")
        for p in relief.PONTS:
            assert not g.sur_pont(x, y, p, 8 * M), (nom, "pont")
        for sx, sy in g.SPAWNS + trous:
            assert math.hypot(x - sx, y - sy) >= 8 * M, (nom, "apparition / sortie")
        for b in batiments:
            assert dist_boite(x, y, b) >= 8 * M, (nom, "batiment", b["name"])


def vegetation_degagee():
    g = gen()
    bat = boites(lambda b: b["grp"].startswith("Poste-frontière") or "(cabane)" in b["name"])
    pieces = troncs() + boites(lambda b: b["grp"] == "Arches (Suna)" and "pilier" in b["name"])
    for b in pieces:
        _degage(b["name"], coins(b), bat)
    # village : arbres a 8 m des batiments, des rampes, des apparitions, et poses sur le relief final (avec bosses)
    for a in g.VILLAGE_ARBRES:
        v = a["v"]
        pts = [local_to_world_u(v, a["x"] + i * a["demi"], a["y"] + j * a["demi"]) for i in (-1, 1) for j in (-1, 1)]
        _degage("arbre du village", pts, [])
        xm, ym = a["x"] / M, a["y"] / M
        lst = [e for e in g.els[v] if e["layer"] == "surface"]
        assert g.dist_batiments(lst, xm, ym) - a["demi"] * 1.5 / M >= 8, ("arbre du village", "batiment", round(xm), round(ym))
        assert g.ramp_dist(v, xm, ym) >= 6 + 8, ("arbre du village", "rampe")
        z = height_at(*local_to_world_u(v, a["x"], a["y"]))
        assert abs(z - a["z0"]) <= 5, ("arbre du village", "pas au sol", round(z), round(a["z0"]))
    print("  ", len(pieces), "arbres / piliers du pays,", len(g.VILLAGE_ARBRES), "arbres de village degages")


def densite_vegetation():
    g = gen()
    d = {t: len(troncs(t)) / HA_PAYS for t in ("konoha", "suna", "kumo")}
    print("   arbres / ha :", {t: round(k, 1) for t, k in d.items()})
    assert d["suna"] == 0 and d["kumo"] > 0 and d["konoha"] >= 4 * d["kumo"], d
    assert d["konoha"] >= 15, d   # foret dense (l'ancienne foret seule : 4,7 / ha)
    assert all(b["grp"] in ("Végétation (Konoha)", "Végétation (Kumo)", "Pays du Feu - zone de Konoha") for b in troncs())
    # village de Konoha : des arbres dans les parcs et le long des rues
    lst = [e for e in g.els["konoha"] if e["layer"] == "surface"]
    parcs = [e["shape"] for e in lst if e["fill"] == "park" and e["shape"]["t"] == "circle"]
    rues = [e["shape"] for e in lst if e["fill"] == "road" and e["shape"]["t"] == "line"]
    ak = [a for a in g.VILLAGE_ARBRES if a["v"] == "konoha"]
    dans_parc = [a for a in ak if any(math.hypot(a["x"] / M - p["x"], a["y"] / M - p["y"]) < p["r"] for p in parcs)]
    bord_rue = [a for a in ak if any(dist_poly(a["x"] / M, a["y"] / M, r["pts"]) <= r["w"] / 2 + 6 for r in rues)]
    print("   village de Konoha :", len(dans_parc), "arbres dans les parcs,", len(bord_rue), "le long des rues")
    assert len(dans_parc) >= 5 and len(bord_rue) >= 10, (len(dans_parc), len(bord_rue))
    s = script()
    for nom in ("Konoha", "Kumo"):
        assert f'new GameObject( terr_{nom.lower()}, true, "Végétation ({nom})" )' in s, nom
    assert '"Végétation (Suna)"' not in s


def arches_suna():
    arch = boites(lambda b: b["grp"] == "Arches (Suna)")
    assert arch and all(b["terr"] == "suna" for b in arch)
    assert 'new GameObject( terr_suna, true, "Arches (Suna)" )' in script()
    noms = sorted({b["name"].split(" (")[0] for b in arch})
    assert 3 <= len(noms) <= 6, noms
    vues = 0
    for n in noms:
        pil = [b for b in arch if b["name"] == f"{n} (pilier)"]
        lin = [b for b in arch if b["name"] == f"{n} (linteau)"]
        assert len(pil) == 2 and len(lin) == 1, n
        lin = lin[0]
        assert all(b["sol"] and b["collide"] for b in pil), n   # posees au sol des deux cotes (pose_au_sol les verifie)
        (ax, ay, _), (bx, by, _) = pil[0]["c"], pil[1]["c"]
        L = math.hypot(bx - ax, by - ay); ux, uy = (bx - ax) / L, (by - ay) / L
        larg = L - pil[0]["s"][0] / 2 - pil[1]["s"][0] / 2   # entre les faces interieures des piliers
        assert larg >= 6 * M, (n, larg)
        # dessous du linteau au-dessus du sol le plus haut du passage, sur toute la profondeur de l'arche
        dessous = lin["c"][2] - lin["s"][2] / 2
        prof = pil[0]["s"][1]
        hmax = max(height_at((ax + bx) / 2 + ux * i * larg / 8 - uy * j * prof / 8, (ay + by) / 2 + uy * i * larg / 8 + ux * j * prof / 8)
                   for i in range(-4, 5) for j in range(-4, 5))
        assert dessous - hmax >= 4 * M, (n, round(dessous - hmax))
        # le linteau repose sur les deux piliers
        assert all(dist_boite(*b["c"][:2], lin) == 0 and b["c"][2] + b["s"][2] / 2 >= dessous for b in pil), n
        if min(dist_poly((ax + bx) / 2, (ay + by) / 2, c) for c in chaussees().values()) <= 60 * M:
            vues += 1
        print("  ", n, ": passage", round(larg / M, 1), "m x", round((dessous - hmax) / M, 1), "m")
    assert vues >= 1   # au moins une arche a moins de 60 m d'une route


def cascades():
    chutes = boites(lambda b: b["name"].startswith("Chute"))
    bassins = boites(lambda b: b["name"].startswith("Bassin"))
    n = len(relief.CASCADES) + len(gen().CASCADES_DECOR)
    assert 4 <= n <= 8 and len(bassins) == n, (n, len(bassins))
    assert all(not b["collide"] and b["alpha"] < 1 for b in chutes + bassins)
    for c in gen().CASCADES_DECOR:
        # vraie marche du relief ; la nappe suit la pente (ne flotte pas, ne s'enfonce pas)
        assert abs(c["z_haut"] - height_at(c["x_haut"], c["y_haut"])) <= 1 and abs(c["z_bas"] - height_at(c["x_bas"], c["y_bas"])) <= 1
        assert c["z_haut"] - c["z_bas"] >= 15 * M, c
        nappe = [b for b in chutes if b["name"] == f"Chute ({c['nom']})"]
        assert nappe, c["nom"]
        for b in nappe:
            for k in range(11):
                (x0, y0, z0), (x1, y1, z1) = bouts(b)
                x, y, z = x0 + (x1 - x0) * k / 10, y0 + (y1 - y0) * k / 10, z0 + (z1 - z0) * k / 10
                assert 0 <= z - height_at(x, y) <= 150, (c["nom"], round(z - height_at(x, y)))
        bas = [b for b in bassins if b["name"] == f"Bassin ({c['nom']})"]
        assert len(bas) == 1 and dist_boite(c["x_bas"], c["y_bas"], bas[0]) == 0, c["nom"]
    print("  ", n, "cascades dont", len(gen().CASCADES_DECOR), "hautes")


def budget_objets():
    import re
    s = script()
    n = int(re.search(r"// Objets créés : (\d+)", s).group(1))
    vol = len(re.findall(r"^\t*(?:B|G|S|W)\(", s, re.M)) + len(re.findall(r"^\t*D\(", s, re.M))
    assert vol < n <= 15000, (n, vol)
    print("  ", n, "objets dont", vol, "volumes")


def _dist_segments(a, b, c, d):
    """Distance entre les segments [a, b] et [c, d] du plan (0 s'ils se croisent)."""
    def cote(p, q, r): return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    if cote(a, b, c) * cote(a, b, d) < 0 and cote(c, d, a) * cote(c, d, b) < 0: return 0.0
    return min(dist_seg(*a[:2], c, d), dist_seg(*b[:2], c, d), dist_seg(*c[:2], a, b), dist_seg(*d[:2], a, b))


def _couloirs(v=None):
    """Axe au sol de chaque puits (repere local du village) : du pied du puits au bout du palier, (v, code, a, b, sortie)."""
    g = gen(); out = []
    for sh in g.SHAFTS:
        if v and sh["v"] != v: continue
        (x0, y0, _), (x1, y1) = sh["start"], sh["top"]
        n = math.hypot(x1 - x0, y1 - y0); ux, uy = (x1 - x0) / n, (y1 - y0) / n
        fin = g.PUITS_FIN + 180
        out.append((sh["v"], sh["code"], (x0, y0), (x1 + ux * fin, y1 + uy * fin), (x1, y1)))
    return out


def puits_separes():
    # deux puits d'un meme village ne se croisent pas (vu en jeu T6 : Konoha S6 / S8 se bouchaient l'un l'autre)
    g = gen(); larg = g.PUITS_LARG + 2 * g.T
    cs = _couloirs(); n = 0
    for i, (v, c1, a, b, _) in enumerate(cs):
        for v2, c2, c, d, _ in cs[i + 1:]:
            if v2 != v: continue
            n += 1
            assert _dist_segments(a, b, c, d) >= larg, (v, c1, c2, round(_dist_segments(a, b, c, d)))
    print("  ", n, "paires de puits")


def murets_hors_sorties():
    # aucun « Muret de terrasse » de Suna sur le couloir d'un puits (vu en jeu T6 : un muret fermait la sortie de S8)
    import re
    g = gen(); hw = g.PUITS_LARG / 2 + g.T
    murets = re.findall(r'B\(\w+,"Muret de terrasse",(-?[\d.]+)f,(-?[\d.]+)f,(-?[\d.]+)f,(-?[\d.]+)f,[^,]+f,new Color\([^)]*\),-?[\d.]+f,(-?[\d.]+)f', script())
    assert murets, "aucun muret de terrasse"
    cs = _couloirs("suna")
    for x, y, sx, sy, yaw in murets:
        x, y, sx, sy, a = float(x), float(y), float(sx), float(sy), math.radians(float(yaw))
        lx, ly = -math.sin(a) * sy / 2, math.cos(a) * sy / 2   # demi-longueur du muret (perpendiculaire a son yaw)
        for _, code, p, q, top in cs:   # partie a l'air libre : du bord amont du trou (170 u avant son centre) au bout du palier
            n = math.hypot(q[0] - p[0], q[1] - p[1])
            debut = (top[0] - (q[0] - p[0]) / n * 170, top[1] - (q[1] - p[1]) / n * 170)
            d = _dist_segments((x - lx, y - ly), (x + lx, y + ly), debut, q)
            assert d >= hw + sx / 2, ("suna", code, round(x), round(y), round(d))
    print("  ", len(murets), "murets,", len(cs), "puits")


if __name__ == "__main__":
    gen()
    for f in (ponts_construits, postes_frontiere, routes_suivent_relief, bornes_disparues, groupes_territoires,
              contenu_replace, pose_au_sol, eau, vegetation_degagee, densite_vegetation, arches_suna, cascades,
              budget_objets, puits_separes, murets_hors_sorties):
        f()
        print("OK", f.__name__)
