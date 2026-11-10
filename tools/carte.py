# Constantes et reperes de la carte (unites : 40 u = 1 m ; donnees de village en metres)
import math

HALF = 56000   # demi-cote de la carte (u)
BAND = 6000    # bande de relief du bord (u)
INNER = 50000  # limite de la zone jouable (u)
M = 40         # unites par metre

CORNERS = {"konoha": (-50000, 50000), "suna": (50000, 50000),
           "kumo": (50000, -50000), "libre": (-50000, -50000)}
TIERS = {"haut": (0, 170), "milieu": (170, 340), "bas": (340, 500)}  # rayons (m)
TIER_Z = {"konoha": (0, 320, 800), "suna": (0, 320, 800), "kumo": (0, 640, 1600)}  # bas, milieu, haut (u)
RAMPS = {"konoha": [250, 290], "suna": [245, 270, 295], "kumo": [260, 280]}  # angles locaux (deg)

CENTRE_R = 15000   # disque du centre commun (u)
PAYS_R = 1500      # rayon du pays, quart de cercle (m)
VILLAGE_R = 700    # rayon du village (m)
ECHELLE_VILLAGE = 1.4
FONDU_M = 80       # largeur du fondu de biome, centre sur la frontiere (m)
# Temporaire jusqu'a la tache 3 : geometrie actuelle des villages (500 m) et de l'ancien pays (900 m).
ANCIEN_VILLAGE_R = 500
ANCIEN_PAYS_R = 900
RAYON_REMPART_M = ANCIEN_VILLAGE_R   # rayon de rempart actuel (la tache 3 le passera a VILLAGE_R)
BIOME = {"konoha": "foret", "suna": "desert", "kumo": "roche", "libre": "neutre", "centre": "neutre"}


def yaw_of(v):
    cx, cy = CORNERS[v]
    return math.degrees(math.atan2(-cx, cy))


def local_to_world(v, x_m, y_m):
    cx, cy = CORNERS[v]
    t = math.radians(yaw_of(v))
    c, s = math.cos(t), math.sin(t)
    return (cx + (x_m * c - y_m * s) * M, cy + (x_m * s + y_m * c) * M)


def world_to_local(v, x_u, y_u):
    cx, cy = CORNERS[v]
    t = math.radians(yaw_of(v))
    c, s = math.cos(t), math.sin(t)
    dx, dy = (x_u - cx) / M, (y_u - cy) / M
    return (dx * c + dy * s, -dx * s + dy * c)


def coin_proche(x_u, y_u):
    return min(CORNERS, key=lambda v: math.hypot(x_u - CORNERS[v][0], y_u - CORNERS[v][1]))


def territoire(x_u, y_u):
    if max(abs(x_u), abs(y_u)) > INNER:
        return "bord"
    # quart de plan du coin ; sur x = 0 cote est, sur y = 0 cote nord
    v = {(0, 1): "konoha", (1, 1): "suna", (1, 0): "kumo", (0, 0): "libre"}[(x_u >= 0, y_u >= 0)]
    cx, cy = CORNERS[v]
    return v if math.hypot(x_u - cx, y_u - cy) / M <= PAYS_R else "centre"


def _candidats(x_u, y_u):
    # (distance en m, territoire de l'autre cote) pour chaque frontiere possible
    e = 1e-6   # depassement pour tomber franchement de l'autre cote (cas x = 0, y = 0)
    t0 = territoire(x_u, y_u)
    res = [(abs(x_u) / M, territoire(-x_u if x_u < 0 else -x_u - e, y_u)),
           (abs(y_u) / M, territoire(x_u, -y_u if y_u < 0 else -y_u - e))]
    for cx, cy in CORNERS.values():
        r = math.hypot(x_u - cx, y_u - cy)
        if r > 0:
            k = (2 * PAYS_R * M - r) / r + (e if r > PAYS_R * M else -e) / r
            res.append((abs(r / M - PAYS_R), territoire(cx + (x_u - cx) * k, cy + (y_u - cy) * k)))
    return t0, [(d, t) for d, t in res if t != t0]


def dist_frontiere_m(x_u, y_u):
    return min((d for d, _ in _candidats(x_u, y_u)[1]), default=math.inf)


def biome_poids(x_u, y_u):
    t0, cands = _candidats(x_u, y_u)
    poids = {"foret": 0.0, "desert": 0.0, "roche": 0.0, "neutre": 0.0}
    b0 = BIOME.get(t0, "neutre")
    w = 0.0
    if cands:
        d, t1 = min(cands, key=lambda c: c[0])
        b1 = BIOME.get(t1, "neutre")
        if b1 != b0:
            w = max(0.0, 0.5 * (1 - d / (FONDU_M / 2)))
            poids[b1] = w
    poids[b0] += 1 - w
    return poids


def zone_of(x_u, y_u):
    t = territoire(x_u, y_u)
    if t == "centre":
        return "centre"
    if t == "bord":
        return "bord-" + coin_proche(x_u, y_u)
    cx, cy = CORNERS[t]
    return ("village-" if math.hypot(x_u - cx, y_u - cy) / M <= VILLAGE_R else "pays-") + t
