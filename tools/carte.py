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
VILLAGE_R = 500    # rayon du village (m)
PAYS_R = 900       # rayon du pays (m)


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


def zone_of(x_u, y_u):
    if max(abs(x_u), abs(y_u)) > INNER:
        return "bord-" + coin_proche(x_u, y_u)
    if math.hypot(x_u, y_u) < CENTRE_R:
        return "centre"
    v = coin_proche(x_u, y_u)
    if v != "libre":
        d = math.hypot(x_u - CORNERS[v][0], y_u - CORNERS[v][1]) / M
        if d <= VILLAGE_R:
            return "village-" + v
        if d <= PAYS_R:
            return "pays-" + v
    return "neutre"
