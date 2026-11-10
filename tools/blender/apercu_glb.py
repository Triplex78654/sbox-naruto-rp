# Aperçu d'un modèle GLB : 3 rendus (face, 3/4, dos) + nombre de triangles.
# Usage : blender --background --factory-startup --python apercu_glb.py -- <modele.glb> <dossier de sortie>
import bpy, sys, math, os
from mathutils import Vector

glb, out = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete()
bpy.ops.import_scene.gltf(filepath=glb)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector((min(p[i] for p in pts) for i in range(3))); hi = Vector((max(p[i] for p in pts) for i in range(3)))
centre, taille = (lo + hi) / 2, max(hi - lo)
print("TRIS", sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons), "TAILLE", tuple(round(v, 3) for v in hi - lo))

sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 1000, 1000
sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.57, 0.6, 1)
sc.world.node_tree.nodes["Background"].inputs[1].default_value = 1.2
sun = bpy.data.objects.new("s", bpy.data.lights.new("s", "SUN")); sun.data.energy = 3
sun.rotation_euler = (math.radians(50), 0, math.radians(30)); sc.collection.objects.link(sun)
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
nom = os.path.splitext(os.path.basename(glb))[0]
for vue, ang in (("face", -90), ("34", -50), ("dos", 90)):
    a = math.radians(ang)
    cam.location = centre + Vector((math.cos(a), math.sin(a), 0.35)) * taille * 2.1
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(out, f"{nom}_{vue}.png")
    bpy.ops.render.render(write_still=True)
