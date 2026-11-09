# Génère le script de blocage s&box (run_csharp) des villages Suna et Kumo à partir des plans 2D.
# Entrée : dossier d'export des plans (ArtifactData list ... out_dir), un JSON par élément (mètres, repère local du village).
# Sortie : docs/monde-suna-kumo.cs.txt, à lancer avec konoha.scene ouverte :
#   sbox-cli --call run_csharp '{"path":".../docs/monde-suna-kumo.cs.txt"}'
# Usage : python tools/plan-vers-blocage.py <dossier elements>
import json, math, os, sys, glob

M = 40                 # unités par mètre
VILLAGE_DIST = 60000   # village -> centre du monde
BORDER = 21000         # rayon de la frontière (comme Konoha)
CENTER_R = 15000       # rayon du centre commun

src = sys.argv[1]
els = {}
for f in glob.glob(os.path.join(src, "*.json")):
    d = json.load(open(f, encoding="utf-8"))
    d = d.get("data", d)
    els.setdefault(d["village"], []).append(d)

# Style de chaque village : couleurs (r, g, b) et hauteurs (unités).
STYLES = {
    "suna": dict(angle=60, ground=(0.86, 0.74, 0.5), rock=(0.74, 0.56, 0.36), built=(0.9, 0.8, 0.62), roof=(0.78, 0.62, 0.42),
                 tower=(0.86, 0.68, 0.42), rampart_h=(900, 1200), house_h=(220, 340)),
    "kumo": dict(angle=-60, ground=(0.56, 0.55, 0.52), rock=(0.48, 0.48, 0.5), built=(0.82, 0.82, 0.8), roof=(0.3, 0.45, 0.62),
                 tower=(0.35, 0.5, 0.72), rampart_h=(1600, 2800), house_h=(260, 420)),
}
COMMON = dict(road=(0.55, 0.5, 0.42), water=(0.2, 0.45, 0.75), zone=(0.68, 0.58, 0.42), mark=(0.25, 0.55, 0.6),
              rock2=(0.62, 0.6, 0.55), park=(0.3, 0.55, 0.28), mountain=(0.5, 0.45, 0.4), wall=(0.78, 0.7, 0.55))

def col(style, key):
    if key in ("rock", "built", "tower"): return style[key]
    return COMMON.get(key, style["built"])

lines = []
def B(group, name, x, y, sx, sy, sz, c, z0=0.0, yaw=0.0, collide=True):
    lines.append(f'B({group},"{name}",{x:.0f}f,{y:.0f}f,{sx:.0f}f,{sy:.0f}f,{sz:g}f,new Color({c[0]}f,{c[1]}f,{c[2]}f),{z0:g}f,{yaw:.1f}f,{"true" if collide else "false"});')
def S(group, name, x, y, z, d, c):
    lines.append(f'S({group},"{name}",{x:.0f}f,{y:.0f}f,{z:.0f}f,{d:.0f}f,new Color({c[0]}f,{c[1]}f,{c[2]}f));')
def disk(group, name, x, y, r, h, c, z0=0.0, collide=True, n=None):
    # Disque = bandes parallèles de longueur égale à la corde (des carrés tournés donnent une étoile).
    n = n or (48 if r > 5000 else 14)
    w = 2 * r / n
    for i in range(n):
        yy = -r + (i + 0.5) * w
        B(group, name, x, y + yy, 2 * math.sqrt(max(0, r * r - yy * yy)), w * 1.02, h, c, z0, 0, collide)
def ring(group, name, x, y, r, w, a0, a1, h, c, step_m=12, jag=None):
    span = a1 - a0
    n = max(4, math.ceil(2 * math.pi * r * span / 360 / step_m))
    da = span / n
    seg = 2 * math.pi * r * da / 360 * 1.06
    for i in range(n):
        a = a0 + (i + 0.5) * da
        rad = math.radians(a)
        hh = h if jag is None else jag(i)
        B(group, f"{name} {i}", (x + math.cos(rad) * r) * M, (y + math.sin(rad) * r) * M, w * M, seg * M, hh, c, 0, a)

# Même placement que la page des plans (graine fixe), pour retrouver les mêmes maisons.
def houses(lst, it):
    s = it["shape"]; n = max(0, min(400, int(s["n"])))
    wall = next((e for e in lst if e["shape"]["t"] == "ring" and e["code"] == "A1"), None)
    inner = wall["shape"]["r"] - wall["shape"]["w"] / 2 - 6 if wall else 200
    busy = []
    for e in lst:
        q = e["shape"]
        if e is it or q["t"] == "houses": continue
        if q["t"] == "circle": busy.append(("c", q["x"], q["y"], q["r"] + 5))
        elif q["t"] == "ring" and (q.get("fill") or q["a1"] - q["a0"] >= 360): busy.append(("c", q["x"], q["y"], q["r"] + q["w"] / 2 + 5))
        elif q["t"] == "rect": busy.append(("b", q["x"], q["y"], math.hypot(q["w"], q["h"]) / 2 + 5))
        elif q["t"] == "line":
            p = q["pts"]
            for i in range(len(p) - 1):
                a, b = p[i], p[i + 1]; L = math.hypot(b[0] - a[0], b[1] - a[1]); k = max(1, math.ceil(L / 6))
                for j in range(k + 1): busy.append(("c", a[0] + (b[0] - a[0]) * j / k, a[1] + (b[1] - a[1]) * j / k, q["w"] / 2 + 5))
    seed = 7
    def rnd():
        nonlocal seed
        seed = (seed * 16807) % 2147483647
        return seed / 2147483647
    out = []
    k = 0
    while k < 8000 and len(out) < n:
        k += 1
        x = (rnd() * 2 - 1) * inner; y = (rnd() * 2 - 1) * inner
        r = 5 + rnd() * 3 if s.get("style") == "dome" else 5 + rnd() * 2.5
        if math.hypot(x, y) > inner - r: continue
        def hit(b):
            if b[0] == "c": return math.hypot(x - b[1], y - b[2]) < b[3] + r
            return abs(x - b[1]) < b[3] + r and abs(y - b[2]) < b[3] + r and math.hypot(x - b[1], y - b[2]) < b[3] + r
        if any(hit(b) for b in busy): continue
        if any(math.hypot(x - o[0], y - o[1]) < o[2] + r + 3 for o in out): continue
        out.append((x, y, r))
    return out

HEIGHT = dict(built=400, tower=1600, mark=900, rock2=320, wall=650, mountain=2400)
FLAT = dict(road=5, zone=6, water=4, park=3)

for v, style in STYLES.items():
    lst = sorted(els.get(v, []), key=lambda e: e.get("order", 999))
    if not lst: continue
    a = math.radians(style["angle"])
    cx, cy = math.cos(a) * VILLAGE_DIST, math.sin(a) * VILLAGE_DIST
    yaw = style["angle"] - 90  # -Y local vers le centre du monde
    name = v.capitalize()
    lines.append(f'// ═══ {name.upper()} ═══')
    lines.append(f'var {v} = new GameObject( true, "{name} (blocage)" ); {v}.WorldPosition = new Vector3({cx:.1f}f,{cy:.1f}f,0); {v}.WorldRotation = Rotation.FromYaw({yaw}f);')
    g = v
    disk(g, "Sol du pays", 0, 0, BORDER, 1.5, style["ground"], 1, False)
    rnd_h = __import__("random").Random(v)
    for e in lst:
        s, f, nm = e["shape"], e["fill"], e["name"].replace('"', "'")
        c = col(style, f)
        tag = f'{e["code"]} {nm}'
        if s["t"] == "houses":
            for i, (x, y, r) in enumerate(houses(lst, e)):
                h = rnd_h.randint(*style["house_h"])
                B(g, f"Maison {i + 1}", x * M, y * M, 2 * r * M * 0.9, 2 * r * M * 0.9, h, style["built"])
                if s.get("style") == "dome": S(g, f"Dôme {i + 1}", x * M, y * M, h, 2 * r * M * 0.9, style["roof"])
                else: B(g, f"Toit {i + 1}", x * M, y * M, 2 * r * M, 2 * r * M, 30, style["roof"], h)
        elif s["t"] == "ring":
            if e["code"] == "A1":
                lo, hi = style["rampart_h"]; rr = __import__("random").Random(v + "A1")
                ring(g, tag, s["x"], s["y"], s["r"], s["w"], s["a0"], s["a1"], hi, c, 12, lambda i: rr.randint(lo, hi))
            else:
                if s.get("fill"): disk(g, f"{tag} (sol)", s["x"] * M, s["y"] * M, s["r"] * M, 4, COMMON["zone"], 0, False)
                ring(g, tag, s["x"], s["y"], s["r"], s["w"], s["a0"], s["a1"], HEIGHT.get(f, 400), c, 8)
        elif s["t"] == "line":
            p = s["pts"]
            for i in range(len(p) - 1):
                (x1, y1), (x2, y2) = p[i], p[i + 1]
                L = math.hypot(x2 - x1, y2 - y1)
                B(g, tag, (x1 + x2) / 2 * M, (y1 + y2) / 2 * M, (L + s["w"]) * M, s["w"] * M, FLAT.get(f, 6), c, 0, math.degrees(math.atan2(y2 - y1, x2 - x1)), False)
        elif s["t"] == "circle":
            if f in FLAT: disk(g, tag, s["x"] * M, s["y"] * M, s["r"] * M, FLAT[f], c, 0, False)
            else:
                h = HEIGHT.get(f, 400)
                disk(g, tag, s["x"] * M, s["y"] * M, s["r"] * M, h, c, 0, True)
                if f == "tower": S(g, f"{tag} (dôme)", s["x"] * M, s["y"] * M, h, s["r"] * 2 * M * 0.85, style["roof"])
        elif s["t"] == "rect":
            flat = f in FLAT
            B(g, tag, s["x"] * M, s["y"] * M, s["w"] * M, s["h"] * M, FLAT[f] if flat else HEIGHT.get(f, 400), c, 0, s.get("rot", 0), not flat)
            if not flat and f == "built": B(g, f"{tag} (toit)", s["x"] * M, s["y"] * M, s["w"] * M + 60, s["h"] * M + 60, 40, style["roof"], HEIGHT.get(f, 400), s.get("rot", 0))
    # Apparition : sur la place B2, face à la porte
    sp = next((e for e in lst if e["code"] == "B2"), None)
    if sp:
        for dx, dy in ((-500, 500), (500, 500), (-500, -500), (500, -500)):
            lines.append(f'{{ var s = new GameObject({g}, true, "Spawn"); s.LocalPosition = new Vector3({sp["shape"]["x"] * M + dx:.0f}f,{sp["shape"]["y"] * M + dy:.0f}f,20); s.LocalRotation = Rotation.FromYaw(-90); s.Enabled = false; s.Components.Create<SpawnPoint>(); }}')
    # Frontière : bornes tous les 10°, sauf sur la route ; poste-frontière sur la route
    for i in range(36):
        ang = 270 + i * 10
        if i == 0: continue
        r = math.radians(ang)
        B(g, "Borne", math.cos(r) * (BORDER - 150), math.sin(r) * (BORDER - 150), 90, 90, 360, COMMON["rock2"])
    B(g, "Poste-frontière (tour de guet)", 900, -BORDER, 700, 700, 1200, style["rock"])
    B(g, "Poste-frontière (arche)", 0, -BORDER, 1500, 200, 120, style["roof"], 700)
    B(g, "Poste-frontière (pilier)", -650, -BORDER, 160, 160, 700, style["rock"])
    B(g, "Poste-frontière (pilier)", 650, -BORDER, 160, 160, 700, style["rock"])
    # Route : de la porte au bord du centre commun (la route du centre s'arrête à CENTER_R)
    gate = next((e for e in lst if e["code"] == "A2"), None)
    gy = (gate["shape"]["y"] - gate["shape"]["h"] / 2) * M if gate else -10000
    end = -(VILLAGE_DIST - CENTER_R)
    B(g, "Route vers le centre", 0, (gy + end) / 2, 640, gy - end, 5, COMMON["road"], 0, 0, False)

body = "\n".join(lines)
header = '''// GÉNÉRÉ par tools/plan-vers-blocage.py à partir des plans 2D (ne pas modifier à la main).
// Blocage de Suna et Kumo + ajustements de la vue d'ensemble (sol neutre, disque vert de Konoha, centre rond).
// Lancer avec konoha.scene ouverte, puis écrire la scène (docs/sbox-reference.md §11, piège de sauvegarde).
var scene = Editor.SceneEditorSession.Active.Scene;
var box = Model.Load( "models/dev/box.vmdl" );
var sphere = Model.Load( "models/dev/sphere.vmdl" );
int count = 0;
void B( GameObject parent, string name, float x, float y, float sx, float sy, float sz, Color tint, float z0, float yaw, bool collide )
{
	var g = new GameObject( parent, true, name );
	g.LocalPosition = new Vector3( x, y, z0 + sz / 2f );
	g.LocalRotation = Rotation.FromYaw( yaw );
	g.LocalScale = new Vector3( sx / 50f, sy / 50f, sz / 50f );
	var r = g.Components.Create<ModelRenderer>(); r.Model = box; r.Tint = tint;
	if ( collide ) g.Components.Create<BoxCollider>().Scale = new Vector3( 50, 50, 50 );
	count++;
}
void S( GameObject parent, string name, float x, float y, float z, float d, Color tint )
{
	var g = new GameObject( parent, true, name );
	g.LocalPosition = new Vector3( x, y, z );
	g.LocalScale = new Vector3( d / 64f, d / 64f, d / 128f );
	var r = g.Components.Create<ModelRenderer>(); r.Model = sphere; r.Tint = tint;
	count++;
}
using ( scene.Push() )
{
	foreach ( var old in scene.GetAllObjects( true ).Where( g => g.Name is "Suna (blocage)" or "Kumo (blocage)" or "Vue d'ensemble (blocage)" ).ToList() )
		old.Destroy();

	// Vue d'ensemble : sol du monde neutre, terres neutres de Konoha sans rectangle, zone de Konoha en disque vert, centre rond.
	var worldGround = scene.GetAllObjects( true ).FirstOrDefault( g => g.Name == "Sol" && g.WorldScale.x > 1000 );
	if ( worldGround?.Components.Get<ModelRenderer>() is { } wr ) wr.Tint = new Color( 0.62f, 0.56f, 0.42f );
	foreach ( var g in scene.GetAllObjects( true ).Where( g => g.Name == "Sol neutre" ).ToList() ) g.Destroy();
	var centerSquare = scene.GetAllObjects( true ).FirstOrDefault( g => g.Name == "Sol" && g.Parent?.Name == "Centre commun" );
	centerSquare?.Destroy();
	var overview = new GameObject( true, "Vue d'ensemble (blocage)" );
'''
_mark = len(lines)
disk("overview", "Sol du Pays du Feu", -VILLAGE_DIST, 0, BORDER, 1.5, (0.36, 0.55, 0.3), 1, False)
disk("overview", "Sol du centre commun", 0, 0, CENTER_R, 2, (0.6, 0.58, 0.42), 1, False)
overview = "\n".join("\t" + l for l in lines[_mark:]) + "\n"
del lines[_mark:]
footer = '''
}
return $"{count} volumes";
'''
out = os.path.join(os.path.dirname(__file__), "..", "docs", "monde-suna-kumo.cs.txt")
open(out, "w", encoding="utf-8").write(header + overview + "\n".join("\t" + l for l in body.split("\n")) + footer)
print(f"{len(lines)} lignes -> {os.path.abspath(out)}")
