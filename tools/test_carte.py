# Tests des territoires et biomes (executables sans dependance : python tools/test_carte.py)
import math, os, random, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from carte import territoire, biome_poids, dist_frontiere_m, zone_of, M, INNER


def test_territoire():
    cas = [((-30000, 30000), "konoha"), ((30000, 30000), "suna"), ((30000, -30000), "kumo"),
           ((-30000, -30000), "libre"), ((0, 0), "centre"), ((-8000, 8000), "konoha"),
           ((-6000, 6000), "centre"), ((-100, 20000), "konoha"), ((100, 20000), "suna"),
           ((-100, 15000), "centre"), ((-52000, 0), "bord"), ((0, 30000), "suna")]
    for p, v in cas:
        assert territoire(*p) == v, (p, territoire(*p), v)


def test_zone_of():
    assert zone_of(-50000, 50000) == "village-konoha"
    assert zone_of(-30000, 30000) == "pays-konoha"
    assert zone_of(0, 0) == "centre"
    assert zone_of(-52000, 0) == "bord-konoha"


def test_biome_poids():
    assert biome_poids(-30000, 30000)["foret"] == 1.0
    assert biome_poids(-2000, 30000)["foret"] == 1.0
    p = biome_poids(0, 30000)
    assert abs(p["foret"] - 0.5) <= 0.05 and abs(p["desert"] - 0.5) <= 0.05, p
    rnd = random.Random(1)
    for _ in range(2000):
        p = biome_poids(rnd.uniform(-INNER, INNER), rnd.uniform(-INNER, INNER))
        assert abs(sum(p.values()) - 1) < 1e-9 and all(0 <= w <= 1 for w in p.values()), p


def test_continuite():
    # lignes qui traversent chaque frontiere (x=0, y=0, arc de 1500 m), pas de 2 m = 80 u
    lignes = [((-3000, 30000), (3000, 30000)), ((30000, -3000), (30000, 3000)),
              ((-3000, -30000), (3000, -30000)), ((-50000, 0), (-30000, 0)),
              ((-50000, 50000), (0, 0)), ((50000, 50000), (0, 0)), ((50000, -50000), (0, 0))]
    for (x0, y0), (x1, y1) in lignes:
        n = int(math.hypot(x1 - x0, y1 - y0) / 80)
        prec = None
        for k in range(n + 1):
            t = k / n
            p = biome_poids(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t)
            if prec:
                assert all(abs(p[b] - prec[b]) <= 0.1 for b in p), ((x0, y0), k, prec, p)
            prec = p


def test_dist_frontiere():
    assert abs(dist_frontiere_m(-2000, 30000) - 50) < 1e-6
    assert dist_frontiere_m(0, 30000) < 1e-3


if __name__ == "__main__":
    for nom, f in list(globals().items()):
        if nom.startswith("test_"):
            f()
            print("OK", nom)
