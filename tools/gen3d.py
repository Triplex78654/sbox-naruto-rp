# Génération gratuite d'un modèle 3D : image (FLUX.1-schnell) -> modèle GLB (TRELLIS.2), via les Spaces Hugging Face.
# Usage : python tools/gen3d.py <nom> "<prompt image>"            (génère l'image puis le modèle)
#         python tools/gen3d.py <nom> --image <fichier.png>        (part d'une image existante)
# Sortie : <dossier>/<nom>.png et <dossier>/<nom>.glb  (dossier = variable GEN3D_OUT, sinon tools/out/gen3d)
# Jeton facultatif : variable d'environnement HF_TOKEN (compte Hugging Face gratuit) pour avoir plus de quota.
# shortcut: une seule tentative par étape, pas de reprise automatique sur « GPU occupé / quota » ; relancer à la main.
import os, shutil, sys
from gradio_client import Client, handle_file

nom = sys.argv[1]
out = os.environ.get("GEN3D_OUT") or os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "gen3d")
os.makedirs(out, exist_ok=True)
tok = os.environ.get("HF_TOKEN")
kw = {"token": tok} if tok else {}
png = os.path.join(out, nom + ".png")

if sys.argv[2] == "--image":
    if os.path.abspath(sys.argv[3]) != os.path.abspath(png): shutil.copy(sys.argv[3], png)
else:
    r, _ = Client("black-forest-labs/FLUX.1-schnell", verbose=False, **kw).predict(
        prompt=sys.argv[2], seed=0, randomize_seed=True, width=1024, height=1024, num_inference_steps=4, api_name="/infer")
    shutil.copy(r["path"] if isinstance(r, dict) else r, png)
print("image :", png)
if "--image-only" in sys.argv: sys.exit(0)

c = Client("microsoft/TRELLIS.2", verbose=False, **kw)
c.predict(api_name="/start_session")
img = c.predict(handle_file(png), api_name="/preprocess_image")
c.predict(image=handle_file(img if isinstance(img, str) else img["path"]), seed=0, resolution="1024", api_name="/image_to_3d")
glb = c.predict(decimation_target=100000, texture_size=2048, api_name="/extract_glb")
src = glb[1] if isinstance(glb, (list, tuple)) else glb
dst = os.path.join(out, nom + ".glb")
shutil.copy(src if isinstance(src, str) else src["path"], dst)
print("modele :", dst, os.path.getsize(dst), "octets")
