# Emblèmes de village gravés sur la plaque du bandeau.
# Chaque village = UNE fonction qui renvoie des traits (polyline en mm, largeur en mm), centrés sur (0, 0),
# x vers la droite, y vers le haut, vus de face. Pour changer un emblème : ne modifier que sa fonction.
# bake() rasterise les traits en texture couleur (rainure foncée) + normal map (rainure creusée).
import math

import numpy as np


def _arc(cx, cy, r, a0, a1, n=24):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * k / n)),
             cy + r * math.sin(math.radians(a0 + (a1 - a0) * k / n))) for k in range(n + 1)]


def konoha():
    """Feuille stylisée : spirale + pointe de feuille en haut à droite + tige en bas à gauche."""
    W = 2.1
    turns, r0, r1, a_end = 2.25, 1.0, 10.0, 45.0
    a0 = a_end - 360 * turns
    pol = lambda a, r: (r * math.cos(math.radians(a)), r * math.sin(math.radians(a)))
    rad = lambda a: r0 + (r1 - r0) * (a - a0) / (a_end - a0)
    spiral = [pol(a0 + (a_end - a0) * k / 140, rad(a0 + (a_end - a0) * k / 140)) for k in range(141)]
    tip = pol(45, 18.5)
    back = pol(-20, rad(-20))  # retour de la pointe sur le dernier tour, côté droit
    stem = [pol(-135, rad(-135)), pol(-140, 14.0), pol(-146, 17.5)]  # tige en bas à gauche
    return [(spiral, W), ([spiral[-1], tip, back], W), (stem, W)]


def suna():
    """Sablier : barres haut/bas, flancs qui se resserrent, un point de chaque côté de la taille."""
    W, H = 2.2, 15.0
    side = [(-(2.6 + 7.0 * (abs(y) / H) ** 1.6), y) for y in np.linspace(H, -H, 31)]
    return [([(-11.5, H), (11.5, H)], W), ([(-11.5, -H), (11.5, -H)], W),
            (side, W), ([(-x, y) for x, y in side], W),
            ([(-9.0, 0.0)], 3.4), ([(9.0, 0.0)], 3.4)]


def kumo():
    """Nuage : contour à trois bosses sur une base plate, plus une volute intérieure."""
    W = 2.1
    circles = [(-10.5, -1.0, 8.0), (0.0, 4.0, 10.0), (10.5, -1.0, 8.0)]
    base_y = -7.0
    ymin = [base_y, 0.0, base_y]  # la bosse du milieu ne garde que son arc haut
    strokes = []
    for i, (cx, cy, r) in enumerate(circles):
        arc = []
        for p in _arc(cx, cy, r, 0, 360, 96):
            inside_other = any(math.hypot(p[0] - ox, p[1] - oy) < orr - 0.05
                               for j, (ox, oy, orr) in enumerate(circles) if j != i)
            under_side = i != 1 and p[1] < cy and abs(p[0]) < abs(cx)  # pas d'arc intérieur en bas
            if p[1] >= ymin[i] and not inside_other and not under_side:
                arc.append(p)
            elif arc:
                strokes.append((arc, W))
                arc = []
        if arc:
            strokes.append((arc, W))
    xb = 10.5 + math.sqrt(8.0 ** 2 - (base_y + 1.0) ** 2)
    strokes.append(([(-xb, base_y), (xb, base_y)], W))
    # volute (marque de nuage) à l'intérieur
    vol = [(0.5 + (0.8 + 4.2 * k / 60) * math.cos(math.radians(200 + 400 * k / 60)),
            1.5 + (0.8 + 4.2 * k / 60) * math.sin(math.radians(200 + 400 * k / 60))) for k in range(61)]
    strokes.append((vol, 1.8))
    strokes.append(([(-14.0, -3.0), (-7.0, -3.0)], 1.8))
    strokes.append(([(7.0, -3.0), (14.0, -3.0)], 1.8))
    return strokes


# village -> (emblème, couleur de tissu par défaut)
VILLAGES = {
    "konoha": (konoha, (0.07, 0.09, 0.26)),   # bleu foncé
    "suna": (suna, (0.19, 0.08, 0.04)),       # sable foncé / brun-rouge
    "kumo": (kumo, (0.74, 0.74, 0.71)),       # blanc cassé / gris
    "none": (None, (0.10, 0.13, 0.32)),
}


def _sdf(strokes, X, Y):
    """Distance signée (mm) à l'union des traits épais."""
    sd = np.full(X.shape, 1e9, dtype=np.float32)
    for pts, w in strokes:
        pts = pts if len(pts) > 1 else pts * 2
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            dx, dy = bx - ax, by - ay
            L2 = dx * dx + dy * dy
            t = np.clip(((X - ax) * dx + (Y - ay) * dy) / L2, 0, 1) if L2 > 0 else 0.0
            d = np.hypot(X - ax - t * dx, Y - ay - t * dy) - w / 2
            np.minimum(sd, d, out=sd)
    return sd


def bake(strokes, w_mm, h_mm, metal_rgb, res=(1024, 512), depth=0.45):
    """Renvoie (couleur RGBA, normale RGBA) en float32 (lignes de bas en haut, comme bpy)."""
    nx, ny = res
    px = w_mm / nx
    X, Y = np.meshgrid((np.arange(nx) + 0.5) / nx * w_mm - w_mm / 2,
                       (np.arange(ny) + 0.5) / ny * h_mm - h_mm / 2)
    sd = _sdf(strokes, X, Y)
    cov = np.clip(0.5 - sd / px, 0, 1)                                  # remplissage anti-aliasé
    wall = np.clip((0.15 - sd) / 0.4, 0, 1)
    h = -depth * wall * wall * (3 - 2 * wall)                           # profil de rainure (smoothstep)
    gy, gx = np.gradient(h, h_mm / ny, w_mm / nx)
    n = np.dstack([-gx, -gy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    dark = np.array([0.07, 0.07, 0.08])
    col = np.array(metal_rgb)[None, None] * (1 - cov[..., None]) + dark * cov[..., None]
    a = np.ones((ny, nx, 1), np.float32)
    return (np.concatenate([col, a], 2).astype(np.float32),
            np.concatenate([n * 0.5 + 0.5, a], 2).astype(np.float32))


if __name__ == "__main__":
    # auto-test : chaque emblème tient dans la plaque et laisse une marge
    for v, (fn, _) in VILLAGES.items():
        if fn:
            xs = [p[0] for pts, _ in fn() for p in pts]
            ys = [p[1] for pts, _ in fn() for p in pts]
            assert max(map(abs, xs)) < 30 and max(map(abs, ys)) < 19, v
    print("emblemes OK")
