# Helper partagé pour les scripts Blender de génération d'équipement ninja (spike).
# Usage : importé par bandeau_frontal.py / kunai.py, lancés via
#   blender.exe --background --factory-startup --python <script>.py -- [args]
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

SBOX_HUMAN = "D:/SteamLibrary/steamapps/common/sbox/addons/citizen/Assets/models/citizen_human"
REF_MALE = SBOX_HUMAN + "/citizen_human_male_REF.fbx"
REF_FEMALE = SBOX_HUMAN + "/citizen_human_female_REF.fbx"
PROJECT = "C:/Users/Estam/Documents/sbox-naruto-rp/nrp"
SCRATCH = "C:/Users/Estam/AppData/Local/Temp/claude/90d6aa067d8f8955/7ad55e10-0ee3-4cb2-bcd1-864ff66fea98/scratchpad/blender"


def args():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_ref(path=REF_MALE):
    """Importe un FBX de référence s&box ; renvoie (armature, {nom: mesh})."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == "ARMATURE")
    meshes = {o.name: o for o in new if o.type == "MESH"}
    return arm, meshes


def world_bvh(obj):
    """BVH du mesh évalué (pose de repos) en coordonnées monde."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.transform(obj.matrix_world)
    tree = BVHTree.FromBMesh(bm)
    bm.free()
    ev.to_mesh_clear()
    return tree


def material(name, rgb, metallic=0.0, rough=0.6):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*rgb, 1)
    p.inputs["Metallic"].default_value = metallic
    p.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*rgb, 1)  # utilisé par Workbench
    m.metallic, m.roughness = metallic, rough
    return m


def mesh_obj(name, bm, mat=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    if mat:
        me.materials.append(mat)
    return o


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = o.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    return o


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def export_fbx(path, objs):
    """Export FBX comme les REF s&box : Y-up, UnitScaleFactor=1 (cm), pas de leaf bones ni d'anim."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
        add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE",
        use_armature_deform_only=False, path_mode="STRIP")


def setup_render(res=(1280, 720)):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sh = sc.display.shading
    sh.light, sh.color_type = "STUDIO", "MATERIAL"
    sh.show_cavity = True
    sh.background_type = "VIEWPORT"
    sh.background_color = (0.32, 0.33, 0.35)
    sc.display_settings.display_device = "sRGB"
    sc.view_settings.view_transform = "Standard"


def setup_eevee(res=(1280, 720)):
    """EEVEE (montre textures + normal maps) : fond gris neutre + un soleil doux."""
    setup_render(res)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    w = sc.world or bpy.data.worlds.new("world")
    sc.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.32, 0.33, 0.35, 1)
    bg.inputs["Strength"].default_value = 1.0
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 2.5
    sun.data.angle = math.radians(10)
    sun.rotation_euler = (math.radians(55), 0, math.radians(-25))
    sc.collection.objects.link(sun)
    fill = bpy.data.objects.new("fill", bpy.data.lights.new("fill", "SUN"))
    fill.data.energy = 1.0
    fill.rotation_euler = (math.radians(-50), 0, math.radians(20))  # contre-jour doux pour les vues de dos
    sc.collection.objects.link(fill)


def ribbon(name, pts, wdirs, widths, thick, mat, closed=False):
    """Ruban (tissu) le long d'un chemin : largeur selon wdirs, épaisseur par solidify."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rows = [(bm.verts.new(p - d * (w / 2)), bm.verts.new(p + d * (w / 2))) for p, d, w in zip(pts, wdirs, widths)]
    n = len(rows)
    for k in range(n if closed else n - 1):
        a, b = rows[k], rows[(k + 1) % n]
        f = bm.faces.new((a[0], a[1], b[1], b[0]))
        for l, (x, y) in zip(f.loops, ((0, k), (1, k), (1, k + 1), (0, k + 1))):
            l[uv].uv = (0.1 * x, 0.1 + 0.9 * y / n)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=thick)
    return mesh_obj(name, bm, mat)


def save_png(path, rgba, non_color=False):
    """Écrit un tableau float (h, w, 4), lignes de bas en haut, en PNG ; renvoie l'image bpy."""
    h, w = rgba.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=False)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


def render_view(path, target, direction, dist, lens=50):
    """Rend depuis target + direction*dist, caméra visant target."""
    sc = bpy.context.scene
    cam = sc.camera
    if cam is None:
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
        sc.collection.objects.link(cam)
        sc.camera = cam
    cam.data.lens = lens
    cam.data.clip_start = 0.01
    d = Vector(direction).normalized()
    cam.location = Vector(target) + d * dist
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def signed_dist(tree, p):
    """Distance signée de p à la surface (positive = dehors, d'après la normale)."""
    loc, nrm, _, d = tree.find_nearest(p)
    return d if (p - loc).dot(nrm) >= 0 else -d


def save_blend(name):
    os.makedirs(SCRATCH, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=f"{SCRATCH}/{name}.blend")


def ring_dir(theta):
    """Direction horizontale ; theta=0 vers l'avant du perso (-Y dans Blender)."""
    return Vector((math.sin(theta), -math.cos(theta), 0))
