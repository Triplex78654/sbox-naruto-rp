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


# Frontieres de territoire : segments sur x = 0 / y = 0 (hors des pays proches du centre) et arcs de PAYS_R.
# Chaque piece : (type, donnees, territoire A, territoire B). Calculees une fois.
_S = math.sqrt((PAYS_R * M) ** 2 - INNER ** 2)   # decalage entre le coin et les extremites d'un arc
_YE = INNER - _S                                   # extremites des arcs sur les axes : points triples
_EPS = 1e-6


def _pieces():
    res = []
    for sg in (1, -1):
        res.append(("seg", ((0, sg * _YE), (0, sg * INNER)),
                    territoire(-_EPS, sg * (_YE + INNER) / 2), territoire(_EPS, sg * (_YE + INNER) / 2)))
        res.append(("seg", ((sg * _YE, 0), (sg * INNER, 0)),
                    territoire(sg * (_YE + INNER) / 2, _EPS), territoire(sg * (_YE + INNER) / 2, -_EPS)))
    for v, (cx, cy) in CORNERS.items():
        res.append(("arc", (cx, cy, ((0, cy - math.copysign(_S, cy)), (cx - math.copysign(_S, cx), 0))), v, "centre"))
    return res


def _dist_seg(px, py, a, b):
    (ax, ay), (bx, by) = a, b
    t = max(0.0, min(1.0, ((px - ax) * (bx - ax) + (py - ay) * (by - ay)) / ((bx - ax) ** 2 + (by - ay) ** 2)))
    return math.hypot(px - ax - t * (bx - ax), py - ay - t * (by - ay)) / M


def _dist_arc(px, py, cx, cy, fins):
    r = math.hypot(px - cx, py - cy)
    if r > 0:
        qx, qy = cx + (px - cx) * PAYS_R * M / r, cy + (py - cy) * PAYS_R * M / r
        if qx * cx >= 0 and qy * cy >= 0:   # le point radial est dans le quart de plan du coin
            return abs(r / M - PAYS_R)
    return min(math.hypot(px - fx, py - fy) / M for fx, fy in fins)


def _distances(x_u, y_u):
    # (distance en m, territoire A, territoire B) pour chaque piece de frontiere
    return [(_dist_seg(x_u, y_u, *d) if k == "seg" else _dist_arc(x_u, y_u, *d), a, b)
            for k, d, a, b in _PIECES]


def dist_frontiere_m(x_u, y_u):
    return min(d for d, _, _ in _distances(x_u, y_u))


def biome_poids(x_u, y_u):
    # Score 1 pour le biome local ; chaque frontiere proche donne a ses deux biomes un score
    # 1 - d/(FONDU_M/2) (max par biome), puis normalisation. Distance a des segments / arcs :
    # continu partout, 0,5/0,5 sur une frontiere simple, 1/3 aux points triples.
    score = {"foret": 0.0, "desert": 0.0, "roche": 0.0, "neutre": 0.0}
    score[BIOME.get(territoire(x_u, y_u), "neutre")] = 1.0
    for d, a, b in _distances(x_u, y_u):
        if BIOME[a] != BIOME[b]:
            for t in (a, b):
                score[BIOME[t]] = max(score[BIOME[t]], 1 - d / (FONDU_M / 2))
    total = sum(score.values())
    return {k: v / total for k, v in score.items()}


_PIECES = _pieces()


def zone_of(x_u, y_u):
    t = territoire(x_u, y_u)
    if t == "centre":
        return "centre"
    if t == "bord":
        return "bord-" + coin_proche(x_u, y_u)
    cx, cy = CORNERS[t]
    return ("village-" if math.hypot(x_u - cx, y_u - cy) / M <= VILLAGE_R else "pays-") + t
