# Tests du relief (executables sans dependance : python tools/test_relief.py)
import math
from carte import CORNERS, RAMPS, TIER_Z, INNER, M, local_to_world
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
    # pente moyenne vers l'interieur > 45 deg sur [INNER, INNER+1200] (hors zones de village)
    for x, y in periph(INNER, 400):
        if any(math.hypot(x - cx, y - cy) < 500 * M for cx, cy in CORNERS.values()):
            continue
        k = 0 if abs(x) >= abs(y) else 1
        p, q = [x, y], [x, y]
        for u, w in ((p, 0), (q, 1200)):
            u[k] += math.copysign(w, u[k])
        assert pente_deg(height_at(*q) - height_at(*p), 1200) > 45, (x, y)


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


if __name__ == "__main__":
    for f in (bord_infranchissable, niveaux_village, rampes_marchables,
              centre_et_routes_plats, apparitions_plates):
        f()
        print("OK", f.__name__)
