"""Run in Blender: --background --factory-startup --python this.py -- --output DIR.
Creates a simple bench, a preserved .blend, rendered preview, and CS2-ready static-object package.
Export settings follow nachocheesedog/cs2-mcp (MIT); see THIRD_PARTY_NOTICES.md.
"""
import argparse
import json
import math
from pathlib import Path
import sys
import uuid
import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
root = Path(args.output).resolve()
name = 'CD_Bench' + uuid.uuid4().hex
work = root / name
package = work / 'export' / name
package.mkdir(parents=True, exist_ok=False)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
parts = []

def box(label, position, size):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    ob = bpy.context.object
    ob.name = label
    ob.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    parts.append(ob)

box('Seat', (0, 0, .48), (2.4, .55, .12))
box('Back', (0, .25, .85), (2.4, .10, .60))
for x in (-.9, .9):
    box('Leg', (x, 0, .23), (.12, .42, .46))
    box('Arm', (x, 0, .72), (.10, .60, .08))
    box('ArmSupport', (x, -.15, .60), (.08, .08, .24))
bpy.ops.object.select_all(action='DESELECT')
for part in parts:
    part.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
mesh = bpy.context.object
mesh.name = name
bpy.context.scene.cursor.location = (0, 0, 0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project()
bpy.ops.object.mode_set(mode='OBJECT')
material = bpy.data.materials.new(name)
material.use_nodes = True
shader = material.node_tree.nodes.get('Principled BSDF')
roughness, metallic, coat = .7, 0.0, 0.0
shader.inputs['Metallic'].default_value = metallic
shader.inputs['Coat Weight'].default_value = coat
mesh.data.materials.clear()
mesh.data.materials.append(material)
textures = {}
# CS2 uses metallic/coat/unused/gloss, NOT the standard HDRP mask layout.
# Use the same exported images in Blender so the preview exercises the actual package.
for suffix, color in [('BaseColor', (.50, .30, .16, 1)), ('Normal', (.5, .5, 1, 1)),
                      ('MaskMap', (metallic, coat, 0, 1 - roughness))]:
    tex = bpy.data.images.new(name + '_' + suffix, width=512, height=512, alpha=True)
    tex.colorspace_settings.name = 'sRGB' if suffix == 'BaseColor' else 'Non-Color'
    tex.alpha_mode = 'CHANNEL_PACKED' if suffix == 'MaskMap' else 'STRAIGHT'
    tex.generated_color = color
    tex.filepath_raw = str(package / (name + '_' + suffix + '.png'))
    tex.file_format = 'PNG'
    tex.save()
    exported = bpy.data.images.load(tex.filepath_raw, check_existing=False)
    exported.colorspace_settings.name = tex.colorspace_settings.name
    exported.alpha_mode = tex.alpha_mode
    textures[suffix] = exported

nodes, links = material.node_tree.nodes, material.node_tree.links
def image_node(suffix):
    node = nodes.new('ShaderNodeTexImage')
    node.image = textures[suffix]
    return node
base, mask, normal = (image_node(suffix) for suffix in ('BaseColor', 'MaskMap', 'Normal'))
separate = nodes.new('ShaderNodeSeparateColor')
inverse = nodes.new('ShaderNodeMath')
inverse.operation = 'SUBTRACT'
inverse.inputs[0].default_value = 1
normal_map = nodes.new('ShaderNodeNormalMap')
links.new(base.outputs['Color'], shader.inputs['Base Color'])
links.new(mask.outputs['Color'], separate.inputs['Color'])
links.new(separate.outputs['Red'], shader.inputs['Metallic'])
links.new(separate.outputs['Green'], shader.inputs['Coat Weight'])
links.new(mask.outputs['Alpha'], inverse.inputs[1])
links.new(inverse.outputs[0], shader.inputs['Roughness'])
links.new(normal.outputs['Color'], normal_map.inputs['Color'])
links.new(normal_map.outputs['Normal'], shader.inputs['Normal'])
# Read the saved PNG back, including alpha: a structurally valid package can still be mirror-like.
mask_pixel = list(textures['MaskMap'].pixels[:4])
expected = (metallic, coat, 0, 1 - roughness)
assert all(abs(a - b) <= 1.5 / 255 for a, b in zip(mask_pixel, expected)), (mask_pixel, expected)
(work / 'material-check.json').write_text(json.dumps({
    'maskRGBA': mask_pixel, 'expectedMaskRGBA': expected, 'roughness': roughness,
    'previewUsesExportedTextures': True, 'scope': 'texture readback; in-game appearance needs separate verification'
}, indent=2), encoding='utf-8')
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.render.resolution_x = 640
scene.render.resolution_y = 400
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.world.color = (.3, .3, .3)
bpy.ops.object.camera_add(location=(3.3, -4.5, 2.5))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, 0, .55)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = camera
bpy.ops.object.light_add(type='AREA', location=(1, -2, 4))
bpy.context.object.data.energy = 450
bpy.context.object.data.shape = 'DISK'
bpy.context.object.data.size = 4
scene.render.filepath = str(work / 'preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(work / 'model.blend'))
bpy.ops.render.render(write_still=True)
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
bpy.context.view_layer.objects.active = mesh
bpy.ops.export_scene.fbx(filepath=str(package / (name + '.fbx')), use_selection=True, object_types={'MESH'},
    apply_unit_scale=True, global_scale=1.0, apply_scale_options='FBX_SCALE_NONE', axis_forward='-Z', axis_up='Y',
    bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False,
    path_mode='STRIP', embed_textures=False, use_custom_props=False)
result = {'assetId': name, 'directory': str(package), 'sourceBlend': str(work / 'model.blend'),
          'preview': str(work / 'preview.png'), 'recipe': str(Path(__file__).resolve()),
          'title': '扶手长椅 · 哑光木色', 'description': '2.4 米静态长椅；哑光木色，粗糙度 0.7、无金属与亮面涂层；不含居民坐下行为。',
          'parentAssetId': 'CD_Bench566f66474ef045f19af51c38bd93edfb'}
(work / 'asset.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print('CITY_DIRECTOR_ASSET=' + json.dumps(result, ensure_ascii=True))
