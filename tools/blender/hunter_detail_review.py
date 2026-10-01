# ============================================================================
# hunter_detail_review.py
# PURPOSE:
#   Render shipped hunter FBX skins under dim warm and cold illumination.
#   Record three close views, fixed ten-metre views and six-frame strips for all
#   six clips without changing the exported asset, its source or Unity state.
# ARCHITECTURAL ROLE:
#   Offline art review tool · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Restore manifest shader colours/emission lost by FBX conversion.
#   - Render labelled close and fixed-distance silhouette review sheets.
#   - Render evaluated skin snapshots across each of the six animation clips.
#   - Save portable PNG boards and explicit lighting/camera metadata.
# DEPENDENCIES:
#   Blender 5.2 bpy, bundled numpy/mathutils; hunter_animation_review sampling.
# USAGE NOTES:
#   -- HunterName selects one body; otherwise renders all seven. Sources and
#   Library are never saved. Ten-metre shots use 50mm/36mm perspective, not zoom.
# ============================================================================
import json
import math
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector
sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from hunter_animation_review import sample, skin_points
from hunter_humanoid_common import linear

ROOT = Path(__file__).resolve().parents[2]
FOLDER = ROOT/'Logs/AgentValidation/Art/HunterDetailPass2'
NAMES = ('Echo','Herald','Mannequin','Mimic','Stare','Ticking','Weaver')
SIZE = 384
LIGHTS = {'warm':((1,.61,.32),(.24,.40,.68)),
          'cold':((.40,.64,1),(.67,.37,.22))}


def light(name, energy, color, rotation):
    data = bpy.data.lights.new(name,'SUN')
    data.energy, data.color, data.angle = energy,color,.18
    obj = bpy.data.objects.new(name,data)
    bpy.context.scene.collection.objects.link(obj)
    obj.rotation_euler = rotation
    return obj


def setup(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    folder = ROOT/f'Assets/Art/Hunter/{name}'
    bpy.ops.import_scene.fbx(filepath=str(folder/f'WORSEN_Hunter{name}.fbx'))
    rig = next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    meshes = [o for o in bpy.context.scene.objects if o.type=='MESH']
    clips = {a.name.split('|')[-1]:a for a in bpy.data.actions}
    manifest = json.loads((folder/f'WORSEN_Hunter{name}.manifest.json').read_text())
    for row in manifest['materials']:
        shader = bpy.data.materials[row['name']].node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = (*[linear(c) for c in row['base_color_srgb'][:3]],1)
        shader.inputs['Emission Color'].default_value = (*[linear(c) for c in row['emission_color_srgb'][:3]],1)
        shader.inputs['Emission Strength'].default_value = row['emission_strength']
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.samples=16
    scene.cycles.use_denoising=True
    scene.cycles.device='CPU'
    scene.render.threads_mode='FIXED'
    scene.render.threads=8
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='AgX'
    scene.view_settings.look='AgX - Medium High Contrast'
    scene.view_settings.exposure=0
    scene.world=bpy.data.worlds.new('DimRoom')
    scene.world.use_nodes=True
    scene.world.node_tree.nodes.get('Background').inputs['Color'].default_value=(.12,.15,.19,1)
    scene.world.node_tree.nodes.get('Background').inputs['Strength'].default_value=.12
    for prop in scene.render.bl_rna.properties:
        if prop.identifier.startswith('use_stamp') and prop.type=='BOOLEAN':
            setattr(scene.render,prop.identifier,False)
    key=light('DimKey',2.0,LIGHTS['warm'][0],(1.05,0,-.45))
    rim=light('DimRim',1.1,LIGHTS['warm'][1],(1.2,0,2.3))
    data=bpy.data.cameras.new('ReviewCamera')
    camera=bpy.data.objects.new('ReviewCamera',data)
    scene.collection.objects.link(camera)
    scene.camera=camera
    data.lens,data.sensor_width=50,36
    label=bpy.data.materials.new('LabelInk')
    label.use_nodes=True
    shader=label.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=(.7,.7,.7,1)
    shader.inputs['Emission Color'].default_value=(.7,.7,.7,1)
    shader.inputs['Emission Strength'].default_value=1
    return rig,meshes,clips,camera,key,rim,label,manifest


def label(text,position,rotation,size,material):
    data=bpy.data.curves.new('Caption','FONT')
    data.body=text
    data.align_x='CENTER'
    data.size=size
    data.materials.append(material)
    obj=bpy.data.objects.new('Caption',data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location,obj.rotation_euler=position,rotation.to_euler()
    return obj


def remove(objects):
    for obj in objects:
        data=obj.data
        bpy.data.objects.remove(obj,do_unlink=True)
        if isinstance(data,bpy.types.Mesh):
            bpy.data.meshes.remove(data)
        else:
            bpy.data.curves.remove(data)


def pixels(path,width,height):
    scene=bpy.context.scene
    scene.render.resolution_x,scene.render.resolution_y=width,height
    scene.render.filepath=str(path)
    bpy.ops.render.render(write_still=True)
    image=bpy.data.images.load(str(path),check_existing=False)
    values=np.empty(width*height*4,dtype=np.float32)
    image.pixels.foreach_get(values)
    bpy.data.images.remove(image)
    return values.reshape((height,width,4))


def board(path,rows):
    values=np.concatenate(list(reversed(rows)),axis=0)
    height,width,_=values.shape
    image=bpy.data.images.new('ReviewBoard',width=width,height=height)
    image.pixels.foreach_set(values.ravel())
    image.filepath_raw=str(path)
    image.file_format='PNG'
    image.save()
    bpy.data.images.remove(image)


def distant_attack(name, rig, clips, camera, key, rim, ink, folder):
    target=Vector((0,0,1.6))
    direction=Vector((.4,-math.sqrt(.84),0))
    rotation=(-direction).to_track_quat('-Z','Y')
    camera.location=target+direction*10
    camera.rotation_euler=rotation.to_euler()
    camera.data.type='PERSP'
    camera.data.sensor_fit='HORIZONTAL'
    rows=[]
    for lighting,colors in LIGHTS.items():
        key.data.color,rim.data.color=colors
        frames=[]
        clip=clips['attack']
        start,end=clip.frame_range
        for i,fraction in enumerate((0,.2,.267,.4,.65,1)):
            sample(rig,clip,start+(end-start)*fraction)
            text=label(f'{name} {lighting} 10m {fraction:.0%}',
                target+rotation@Vector((0,-3.25,0)),rotation,.22,ink)
            frames.append(pixels(folder/f'{lighting}-attack-10m-{i}.png',SIZE,SIZE))
            remove([text])
        rows.append(np.concatenate(frames,axis=1))
    board(folder/'attack-10m.png',rows)
    print('DISTANT_ATTACK_COMPLETE',name,flush=True)


def review(name, distance_only=False):
    rig,meshes,clips,camera,key,rim,ink,manifest=setup(name)
    folder=FOLDER/name
    folder.mkdir(parents=True,exist_ok=True)
    if distance_only:
        distant_attack(name,rig,clips,camera,key,rim,ink,folder)
        return
    all_points=[]
    for clip in clips.values():
        start,end=clip.frame_range
        for fraction in (0,.2,.267,.4,.6,.8,1):
            sample(rig,clip,start+(end-start)*fraction)
            all_points.extend(skin_points(meshes))
    height=max(p.z for p in all_points)
    width=max(max(p[i] for p in all_points)-min(p[i] for p in all_points) for i in (0,1))
    scale=max(height*1.30,width*1.36,.45)
    target=Vector((0,0,height*.47))
    direction=Vector((4,-6,2.0)).normalized()
    rotation=(-direction).to_track_quat('-Z','Y')
    right=rotation@Vector((1,0,0))
    for lighting,colors in LIGHTS.items():
        key.data.color,rim.data.color=colors
        rows=[]
        for distance in (False,True):
            views=[]
            for view,direction_view in (('front',(0,-1,.0)),('three-quarter',(.55,-.83,.12)),('side',(1,0,0))):
                sample(rig,clips['idle'],clips['idle'].frame_range[0])
                aim=Vector((0,0,1.6)) if distance else Vector((0,0,manifest['height_m']*.5))
                rot=(-Vector(direction_view)).to_track_quat('-Z','Y')
                camera.location=aim+Vector(direction_view).normalized()*10
                camera.rotation_euler=rot.to_euler()
                camera.data.type='PERSP' if distance else 'ORTHO'
                camera.data.sensor_fit='HORIZONTAL' if distance else 'VERTICAL'
                camera.data.ortho_scale=max(manifest['height_m'],manifest['width_m'])*1.3
                caption_scale=7.2 if distance else camera.data.ortho_scale
                text=label(f'{name} {lighting} {view}'+(' 10m' if distance else ''),
                    aim+rot@Vector((0,-caption_scale*.46,0)),rot,caption_scale*.034,ink)
                views.append(pixels(folder/f'{lighting}-{view}-{"10m" if distance else "close"}.png',SIZE*2,SIZE*2))
                remove([text])
            rows.append(np.concatenate(views,axis=1))
        board(folder/f'{lighting}-turntables.png',rows)
        rows=[]
        camera.location=target+direction*10
        camera.rotation_euler=rotation.to_euler()
        camera.data.type='ORTHO'
        camera.data.sensor_fit='VERTICAL'
        camera.data.ortho_scale=scale
        for role in ('idle','walk','run','ready','attack','hit'):
            clip=clips[role]
            start,end=clip.frame_range
            snapshots=[]
            fractions=(0,.2,.267,.4,.65,1) if role=='attack' else (0,.2,.4,.6,.8,1)
            for i,fraction in enumerate(fractions):
                sample(rig,clip,start+(end-start)*fraction)
                graph=bpy.context.evaluated_depsgraph_get()
                offset=right*((i-2.5)*scale)
                for mesh in meshes:
                    data=bpy.data.meshes.new_from_object(mesh.evaluated_get(graph),depsgraph=graph)
                    obj=bpy.data.objects.new('PoseSnapshot',data)
                    bpy.context.scene.collection.objects.link(obj)
                    obj.matrix_world=mesh.matrix_world.copy()
                    obj.location+=offset
                    snapshots.append(obj)
                snapshots.append(label(f'{name} {role} {fraction:.0%}',
                    target+offset+rotation@Vector((0,-scale*.46,0)),rotation,scale*.043,ink))
            for mesh in meshes:
                mesh.hide_render=True
            rows.append(pixels(folder/f'{lighting}-{role}.png',SIZE*6,SIZE))
            for mesh in meshes:
                mesh.hide_render=False
            remove(snapshots)
        board(folder/f'{lighting}-animations.png',rows)
    (folder/'review-settings.json').write_text(json.dumps({'hunter':name,'renderer':'Cycles CPU',
        'samples':16,'exposure':0,'world_strength':.12,'key_sun_energy':2.0,'rim_sun_energy':1.1,
        'lighting_colors':LIGHTS,'distance_m':10,'lens_mm':50,'sensor_mm':36,
        'distance_camera_target_m':[0,0,1.6],'clip_order':['idle','walk','run','ready','attack','hit'],
        'clip_scale_m':scale},indent=2)+'\n')
    print('DETAIL_REVIEW_COMPLETE',name,flush=True)


if __name__=='__main__':
    names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(NAMES)
    distance_only='--distance-only' in names
    if distance_only:
        names.remove('--distance-only')
    names=names or NAMES
    for name in names:
        if name not in NAMES:
            raise ValueError(name)
        review(name,distance_only)
