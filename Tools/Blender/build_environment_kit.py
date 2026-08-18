"""Builds the EchoRun modular environment kit.

Fifteen vertex-colored pieces (pylons, arch, halo, totems, island, city
cards) exported as individual FBX files into Assets/Resources/Art/Kit so
WorldStyler can swap its primitive placeholders for authored meshes at
runtime through EchoEnvironmentKit. Every piece follows the same vertex
color convention as the EchoRunner character: RGB is the base color and
the alpha channel marks the emissive glow mask for the EchoRun/VertexColor
shader.

Axis note: pieces are authored Blender-native (height along +Z, track
length along +Y). The FBX export maps Blender +Z to Unity +Y and Blender
-Y to Unity +Z, so a detail meant to face the runner's camera (Unity -Z)
is modeled on the Blender +Y side.

Run with: blender --background --python Tools/Blender/build_environment_kit.py
"""

import bpy
import json
import math
import os
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE_DIR = os.path.join(ROOT, "ArtSource", "EnvironmentKit")
KIT_DIR = os.path.join(ROOT, "Assets", "Resources", "Art", "Kit")
DOCS_DIR = os.path.join(ROOT, "docs", "ConceptArt")

BLEND_PATH = os.path.join(SOURCE_DIR, "EnvironmentKit_v1.blend")
PREVIEW_PATH = os.path.join(DOCS_DIR, "EnvironmentKit-v1.png")
STATS_PATH = os.path.join(SOURCE_DIR, "EnvironmentKit_v1-stats.json")

for path in (SOURCE_DIR, KIT_DIR, DOCS_DIR):
    os.makedirs(path, exist_ok=True)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.armatures,
                       bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def set_bsdf_input(bsdf, names, value):
    for name in names:
        socket = bsdf.inputs.get(name)
        if socket is not None:
            socket.default_value = value
            return


def material(name, color, metallic=0.0, roughness=0.65, emission=None):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        set_bsdf_input(bsdf, ["Emission Color", "Emission"], (*emission, 1.0))
        set_bsdf_input(bsdf, ["Emission Strength"], 3.2)
    return mat


def apply_transform(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.select_set(False)


def bevel(obj, width=0.025, segments=2):
    mod = obj.modifiers.new("Edge softening", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False)


def pbox(parts, name, location, scale, mat, bevel_width=0.03, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_transform(obj)
    if bevel_width:
        bevel(obj, bevel_width, 2)
    obj.data.materials.append(mat)
    parts.append(obj)
    return obj


def pcyl(parts, name, location, radius, depth, mat, vertices=18,
         bevel_width=0.0, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth,
        location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    if bevel_width:
        bevel(obj, bevel_width, 2)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.materials.append(mat)
    parts.append(obj)
    return obj


def pcone(parts, name, location, radius1, radius2, depth, mat,
          vertices=18, bevel_width=0.0):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices, radius1=radius1, radius2=radius2,
        depth=depth, location=location)
    obj = bpy.context.object
    obj.name = name
    if bevel_width:
        bevel(obj, bevel_width, 2)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.materials.append(mat)
    parts.append(obj)
    return obj


def pico(parts, name, location, scale, mat, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=subdivisions, radius=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_transform(obj)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj.data.materials.append(mat)
    parts.append(obj)
    return obj


def ptube(parts, name, points, radius, mat, sides=10):
    pts = [Vector(p) for p in points]
    vertices = []
    faces = []
    for i, point in enumerate(pts):
        if i == 0:
            tangent = pts[1] - pts[0]
        elif i == len(pts) - 1:
            tangent = pts[-1] - pts[-2]
        else:
            tangent = pts[i + 1] - pts[i - 1]
        tangent.normalize()
        reference = Vector((0, 0, 1)) if abs(tangent.z) < 0.92 else Vector((1, 0, 0))
        normal = tangent.cross(reference).normalized()
        binormal = tangent.cross(normal).normalized()
        for side in range(sides):
            angle = 2.0 * math.pi * side / sides
            vertex = point + radius * (
                math.cos(angle) * normal + math.sin(angle) * binormal)
            vertices.append(tuple(vertex))
    rings = len(pts)
    for ring in range(rings - 1):
        for side in range(sides):
            nxt = (side + 1) % sides
            a = ring * sides + side
            b = ring * sides + nxt
            c = (ring + 1) * sides + nxt
            d = (ring + 1) * sides + side
            faces.append((a, b, c, d))
    faces.append(tuple(reversed(range(sides))))
    top = (rings - 1) * sides
    faces.append(tuple(top + side for side in range(sides)))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj.data.materials.append(mat)
    parts.append(obj)
    return obj


def ploft(parts, name, rings, mat, sides=16):
    vertices = []
    faces = []
    for z, rx, ry, y_offset in rings:
        for index in range(sides):
            angle = (2.0 * math.pi * index / sides) + math.pi / sides
            vertices.append(
                (rx * math.cos(angle), y_offset + ry * math.sin(angle), z))
    for ring in range(len(rings) - 1):
        for index in range(sides):
            nxt = (index + 1) % sides
            a = ring * sides + index
            b = ring * sides + nxt
            c = (ring + 1) * sides + nxt
            d = (ring + 1) * sides + index
            faces.append((a, b, c, d))
    faces.append(tuple(reversed(range(sides))))
    top = (len(rings) - 1) * sides
    faces.append(tuple(top + i for i in range(sides)))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bevel(obj, 0.06, 2)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj.data.materials.append(mat)
    parts.append(obj)
    return obj


def join_piece(name, parts):
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


clear_scene()

# Shared palette, mirroring WorldStyler.BuildPalette.
structure = material("KitStructure", (0.22, 0.29, 0.39), metallic=0.1, roughness=0.55)
deep = material("KitDeep", (0.055, 0.09, 0.15), roughness=0.7)
cyan = material("KitCyan", (0.22, 0.84, 1.00), roughness=0.35, emission=(0.05, 0.55, 0.85))
coral = material("KitCoral", (1.00, 0.40, 0.35), roughness=0.45, emission=(0.75, 0.08, 0.04))
gold = material("KitGold", (0.94, 0.68, 0.24), metallic=0.25, roughness=0.4, emission=(0.55, 0.22, 0.02))

GLOW_MATERIAL_NAMES = {cyan.name, coral.name, gold.name}


def build_pylon(name, height, crown_mat, lens_mat):
    parts = []
    pbox(parts, "Foot", (0, 0, 0.14), (0.45, 0.45, 0.14), deep, 0.05)
    pcyl(parts, "FootRing", (0, 0, 0.31), 0.30, 0.10, deep, 20, 0.02)
    body_height = height - 0.95
    pcone(parts, "Body", (0, 0, 0.36 + body_height * 0.5),
          0.26, 0.17, body_height, structure, 18, 0.025)
    pcyl(parts, "WrapBand", (0, 0, 0.36 + body_height * 0.55),
         0.215, 0.09, deep, 18, 0.02)
    # Blender +Y faces Unity -Z (the camera), matching the primitive lens.
    pbox(parts, "Lens", (0, 0.245, 0.36 + body_height * 0.66),
         (0.055, 0.03, body_height * 0.16), lens_mat, 0.012)
    pbox(parts, "LensBack", (0, -0.245, 0.36 + body_height * 0.66),
         (0.04, 0.025, body_height * 0.10), lens_mat, 0.01)
    pcyl(parts, "CrownBase", (0, 0, height - 0.34), 0.24, 0.09, structure, 18, 0.02)
    pico(parts, "Crown", (0, 0, height - 0.10), (0.30, 0.30, 0.22), crown_mat, 2)
    return join_piece(name, parts)


def build_arch(name):
    parts = []
    half_width = 8.4
    height = 6.2
    opening = 3.4
    for sign in (-1, 1):
        points = []
        steps = 12
        for i in range(steps + 1):
            x = sign * (opening + (half_width - opening) * i / steps)
            z = height * math.sqrt(max(0.0, 1.0 - (x / half_width) ** 2))
            points.append((x, 0.0, z))
        ptube(parts, "Wing", points, 0.23, structure, 12)
        for t0, t1 in ((0.22, 0.30), (0.78, 0.86)):
            band = []
            for i in range(4):
                t = t0 + (t1 - t0) * i / 3.0
                x = sign * (opening + (half_width - opening) * t)
                z = height * math.sqrt(max(0.0, 1.0 - (x / half_width) ** 2))
                band.append((x, 0.0, z))
            ptube(parts, "AccentBand", band, 0.27, cyan, 10)
        # Keystone node and coral signal at the floating inner tip.
        tip_x = sign * opening
        tip_z = height * math.sqrt(max(0.0, 1.0 - (opening / half_width) ** 2))
        pico(parts, "Keystone", (tip_x, 0, tip_z), (0.36, 0.36, 0.36), gold, 2)
        signal_x = sign * 3.6
        signal_z = height * math.sqrt(max(0.0, 1.0 - (3.6 / half_width) ** 2)) - 0.12
        pbox(parts, "Signal", (signal_x, 0.28, signal_z),
             (0.42, 0.035, 0.08), coral, 0.02)
        pbox(parts, "Base", (sign * half_width, 0, 0.15), (0.7, 0.7, 0.15), deep, 0.05)
        pcone(parts, "BaseCollar", (sign * half_width, 0, 0.65),
              0.42, 0.30, 0.8, deep, 16, 0.03)
    return join_piece(name, parts)


def build_halo(name):
    parts = []
    bpy.ops.mesh.primitive_torus_add(
        major_radius=2.2, minor_radius=0.13, major_segments=48,
        minor_segments=10, location=(0, 0, 0), rotation=(math.pi / 2, 0, 0))
    ring = bpy.context.object
    ring.name = "Ring"
    for polygon in ring.data.polygons:
        polygon.use_smooth = True
    ring.data.materials.append(structure)
    parts.append(ring)
    for start_deg, end_deg, accent_mat in ((20, 62, gold), (200, 242, cyan)):
        arc = []
        for i in range(7):
            angle = math.radians(start_deg + (end_deg - start_deg) * i / 6.0)
            arc.append((math.cos(angle) * 2.2, 0.0, math.sin(angle) * 2.2))
        ptube(parts, "AccentArc", arc, 0.18, accent_mat, 10)
    for angle_deg in (45, 135, 225, 315):
        angle = math.radians(angle_deg)
        pico(parts, "Node", (math.cos(angle) * 2.2, 0, math.sin(angle) * 2.2),
             (0.17, 0.17, 0.17), structure, 1)
    return join_piece(name, parts)


def build_halo_pylon(name):
    parts = []
    pbox(parts, "Foot", (0, 0, 0.13), (0.5, 0.5, 0.13), deep, 0.05)
    pcone(parts, "Mast", (0, 0, 0.26 + 1.42), 0.30, 0.19, 2.84, deep, 16, 0.03)
    pcyl(parts, "GlowCollar", (0, 0, 2.55), 0.24, 0.10, cyan, 16, 0.02)
    pico(parts, "Emitter", (0, 0, 3.12), (0.20, 0.20, 0.20), cyan, 2)
    return join_piece(name, parts)


def build_totem(name, height, band_mat, band_z):
    parts = []
    pbox(parts, "Base", (0, 0, 0.18), (0.75, 0.75, 0.18), deep, 0.06)
    body_height = height - 0.9
    pcone(parts, "Body", (0, 0, 0.36 + body_height * 0.5),
          0.58, 0.42, body_height, structure, 20, 0.04)
    pcyl(parts, "DataBand", (0, 0, band_z), 0.52, 0.16, band_mat, 20, 0.03)
    pbox(parts, "GlowNotch", (0, 0.475, height * 0.55),
         (0.07, 0.03, 0.5), band_mat, 0.015)
    pcyl(parts, "CrownRing", (0, 0, height - 0.30), 0.40, 0.10, deep, 20, 0.02)
    pico(parts, "Crown", (0, 0, height - 0.12), (0.46, 0.46, 0.30), structure, 2)
    return join_piece(name, parts)


def build_island(name):
    parts = []
    # Tapered floating slab, 20m along Blender Y (Unity Z), origin at the
    # vertical center so it drops into the same pivot as the fallback capsule.
    ploft(parts, "Slab", (
        (-0.75, 2.2, 7.5, 0.0),
        (-0.20, 2.9, 9.6, 0.0),
        (0.45, 3.1, 10.0, 0.0),
        (0.70, 2.9, 9.7, 0.0),
    ), deep, 20)
    pbox(parts, "DeckPlate", (0, 0, 0.66), (2.55, 9.3, 0.10), structure, 0.05)
    pbox(parts, "EdgeGlowL", (-2.72, 0, 0.70), (0.06, 9.2, 0.045), cyan, 0.015)
    pbox(parts, "EdgeGlowR", (2.72, 0, 0.70), (0.06, 9.2, 0.045), cyan, 0.015)
    pbox(parts, "KeelFin", (0, 0, -1.15), (0.55, 2.6, 0.55), deep, 0.08)
    pbox(parts, "KeelFinFront", (0, 5.2, -1.0), (0.4, 1.6, 0.4), deep, 0.06)
    pbox(parts, "KeelFinBack", (0, -5.2, -1.0), (0.4, 1.6, 0.4), deep, 0.06)
    return join_piece(name, parts)


def build_citycard(name, towers, seed_offset):
    parts = []
    window_index = 0
    for tower_x, width, height in towers:
        pbox(parts, "Slab", (tower_x, 0, height * 0.5),
             (width * 0.5, 0.5, height * 0.5), deep, 0.08)
        pbox(parts, "Lip", (tower_x, 0, height - 0.12),
             (width * 0.5 + 0.15, 0.56, 0.12), structure, 0.04)
        cols = max(3, int(width / 1.1))
        rows = max(4, int(height / 1.35))
        for row in range(rows):
            for col in range(cols):
                if (row * 7 + col * 3 + seed_offset) % 5 >= 2:
                    continue
                wx = tower_x - width * 0.5 + (col + 0.5) * width / cols
                wz = 0.9 + row * (height - 1.8) / max(1, rows - 1)
                window_index += 1
                window_mat = gold if window_index % 7 == 0 else cyan
                pbox(parts, "Window", (wx, 0.52, wz),
                     (0.16, 0.03, 0.26), window_mat, 0.008)
    return join_piece(name, parts)


pieces = []
pieces.append(build_pylon("Pylon_S", 3.5, cyan, cyan))
pieces.append(build_pylon("Pylon_M", 6.0, cyan, cyan))
pieces.append(build_pylon("Pylon_L", 9.5, cyan, cyan))
pieces.append(build_pylon("PylonAccent_S", 3.5, coral, gold))
pieces.append(build_pylon("PylonAccent_M", 6.0, coral, gold))
pieces.append(build_pylon("PylonAccent_L", 9.5, coral, gold))
pieces.append(build_arch("Arch"))
pieces.append(build_halo("Halo"))
pieces.append(build_halo_pylon("HaloPylon"))
pieces.append(build_totem("Totem_A", 4.4, coral, 3.2))
pieces.append(build_totem("Totem_B", 6.4, cyan, 4.2))
pieces.append(build_island("Island"))
pieces.append(build_citycard("CityCard_A", ((0.0, 8.0, 14.0),), 1))
pieces.append(build_citycard("CityCard_B", ((-2.2, 6.0, 12.0), (3.0, 4.5, 8.5)), 2))
pieces.append(build_citycard("CityCard_C", ((0.0, 12.0, 9.0),), 3))

def new_color_layer(mesh):
    # Blender 3.2+ exposes color_attributes; older releases use the legacy
    # vertex_colors collection. Both share the same per-loop data interface.
    if hasattr(mesh, "color_attributes"):
        return mesh.color_attributes.new(
            name="Color", type="BYTE_COLOR", domain="CORNER")
    return mesh.vertex_colors.new(name="Color")


# Bake the shared vertex color convention into every piece: RGB from the
# source material diffuse, alpha 1.0 on glow materials for the emissive
# mask consumed by the EchoRun/VertexColor shader.
vertex_mat = bpy.data.materials.new("EchoKit_VertexColor")
vertex_mat.use_nodes = True
nodes = vertex_mat.node_tree.nodes
links = vertex_mat.node_tree.links
bsdf = nodes.get("Principled BSDF")
bsdf.inputs["Metallic"].default_value = 0.05
bsdf.inputs["Roughness"].default_value = 0.6
vertex_color = nodes.new("ShaderNodeVertexColor")
vertex_color.layer_name = "Color"
emission_strength = nodes.new("ShaderNodeMath")
emission_strength.operation = "MULTIPLY"
emission_strength.inputs[1].default_value = 3.0
links.new(vertex_color.outputs["Color"], bsdf.inputs["Base Color"])
emission_color_socket = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
emission_strength_socket = bsdf.inputs.get("Emission Strength")
links.new(vertex_color.outputs["Color"], emission_color_socket)
links.new(vertex_color.outputs["Alpha"], emission_strength.inputs[0])
links.new(emission_strength.outputs[0], emission_strength_socket)

for piece in pieces:
    color_layer = new_color_layer(piece.data)
    source_materials = list(piece.data.materials)
    for polygon in piece.data.polygons:
        source = source_materials[polygon.material_index]
        color = tuple(source.diffuse_color[:3])
        glow_mask = 1.0 if source.name in GLOW_MATERIAL_NAMES else 0.0
        for loop_index in polygon.loop_indices:
            color_layer.data[loop_index].color = (*color, glow_mask)
    for polygon in piece.data.polygons:
        polygon.material_index = 0
    piece.data.materials.clear()
    piece.data.materials.append(vertex_mat)


# Export one FBX per piece while every piece still sits at the origin.
for piece in pieces:
    bpy.ops.object.select_all(action="DESELECT")
    piece.select_set(True)
    bpy.context.view_layer.objects.active = piece
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(KIT_DIR, piece.name + ".fbx"),
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        path_mode="AUTO",
        embed_textures=False,
    )


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


# Arrange the exported pieces into a contact-sheet layout for the preview
# and for a readable .blend source file.
LAYOUT = {
    "Pylon_S": (-15.0, 0.0), "Pylon_M": (-11.5, 0.0), "Pylon_L": (-8.0, 0.0),
    "PylonAccent_S": (-4.5, 0.0), "PylonAccent_M": (-1.0, 0.0),
    "PylonAccent_L": (2.5, 0.0),
    "Arch": (-8.0, 10.0), "Halo": (5.0, 10.0), "HaloPylon": (8.5, 10.0),
    "Totem_A": (-14.0, 19.0), "Totem_B": (-10.0, 19.0),
    "Island": (2.0, 22.0),
    "CityCard_A": (-11.0, 36.0), "CityCard_B": (-1.0, 36.0),
    "CityCard_C": (10.0, 36.0),
}
# Pieces face the in-game camera (Blender +Y), so the contact sheet shows
# their backs unless rotated; turn the windowed and long pieces around.
PREVIEW_YAW = {
    "CityCard_A": 180.0, "CityCard_B": 180.0, "CityCard_C": 180.0,
    "Island": 90.0,
}
for piece in pieces:
    x, y = LAYOUT[piece.name]
    piece.location = (x, y, 0.0)
    piece.rotation_euler.z = math.radians(PREVIEW_YAW.get(piece.name, 0.0))

# Halo reads better in the sheet when lifted to its in-game center height.
bpy.data.objects["Halo"].location.z = 4.8

preview_objects = []
bpy.ops.mesh.primitive_plane_add(size=120, location=(0, 18, -0.02))
ground = bpy.context.object
ground.name = "Preview_Ground"
ground.data.materials.append(material("PreviewGround", (0.09, 0.11, 0.15), roughness=0.9))
preview_objects.append(ground)

world = bpy.context.scene.world or bpy.data.worlds.new("World")
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.05, 0.07, 0.10, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.45

for light_name, location, energy, size in (
    ("Key", (-16, -18, 26), 6000, 9.0),
    ("Fill", (18, -6, 18), 4200, 7.0),
    ("Rim", (0, 44, 28), 5500, 8.0),
):
    data = bpy.data.lights.new(light_name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(light_name, data)
    obj.location = location
    look_at(obj, (0, 16, 2))
    bpy.context.collection.objects.link(obj)
    preview_objects.append(obj)

camera_data = bpy.data.cameras.new("PreviewCamera")
camera = bpy.data.objects.new("PreviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (44, -30, 34)
camera_data.lens = 50
look_at(camera, (-1, 18, 3))
bpy.context.scene.camera = camera
preview_objects.append(camera)

# Persist the .blend source and stats before rendering so a headless GL
# failure can never take the exported FBX files down with it.
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

stats = {
    "kit": "EchoRun Environment Kit v1",
    "pieces": [],
    "total_vertices": 0,
    "total_triangles": 0,
    "blend": BLEND_PATH,
    "fbx_dir": KIT_DIR,
    "color_source": "FBX vertex colors (Color, alpha = glow mask)",
    "preview": PREVIEW_PATH,
}
for piece in pieces:
    piece.data.calc_loop_triangles()
    vertices = len(piece.data.vertices)
    triangles = len(piece.data.loop_triangles)
    stats["pieces"].append({
        "name": piece.name,
        "vertices": vertices,
        "triangles": triangles,
    })
    stats["total_vertices"] += vertices
    stats["total_triangles"] += triangles

with open(STATS_PATH, "w", encoding="utf-8") as handle:
    json.dump(stats, handle, ensure_ascii=False, indent=2)

print("ECHO_ENVIRONMENT_KIT_BUILD_OK")
print(json.dumps(stats, ensure_ascii=False))

scene = bpy.context.scene
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except Exception:
    scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1600
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = PREVIEW_PATH
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
except Exception:
    pass
try:
    bpy.ops.render.render(write_still=True)
    print("ECHO_ENVIRONMENT_KIT_PREVIEW_OK")
except Exception as exc:
    print("ECHO_ENVIRONMENT_KIT_PREVIEW_SKIPPED: " + str(exc))
