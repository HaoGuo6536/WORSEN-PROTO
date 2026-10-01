# ============================================================================
# review_disguise.py
# PURPOSE:
#   Compare the shipped Mimic disguise against the unchanged authored cake.
#   Use identical material settings and cameras, with close and play-distance rows.
# ARCHITECTURAL ROLE:
#   Offline art review · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Load the actual exported Mimic and the real project's editable cake source.
#   - Render paired material/geometry views without inventing native Lumen evidence.
# DEPENDENCIES:
#   Blender 5.2; existing hunter_detail_review and hunter_animation_review.
# USAGE NOTES:
#   Lumen cannot run in Blender. Native saved-glow tests verify its setup separately.
#   The available source has a cherry, not a candle; do not fabricate a reference.
# ============================================================================
import sys
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'tools/blender'))
import bpy
import numpy as np
from mathutils import Vector
import hunter_detail_review as review
from hunter_animation_review import sample

rig,meshes,clips,camera,key,rim,ink,manifest=review.setup('Mimic')
sample(rig,clips['idle'],1)
with bpy.data.libraries.load(str(ROOT/'ArtSource/Horror/Cake/WORSEN_CakePickup.blend'),link=False) as (src,dst):
    dst.objects=['WORSEN_CakePickup']
reference=dst.objects[0]
bpy.context.scene.collection.objects.link(reference)
# Match HorrorArtSetup.EnsureCake's white tint, 0.5 emission and 0.42 smoothness.
for material in {m for o in meshes+[reference] for m in o.data.materials}:
    if 'Cake' not in material.name:
        continue
    shader=material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Emission Strength'].default_value=.5
    shader.inputs['Roughness'].default_value=.58
folder=ROOT/'Logs/AgentValidation/Art/HunterMimic'
rows=[]
for distance in (0,3,6):
    frames=[]
    for is_mimic in (False,True):
        reference.hide_render=is_mimic
        for o in meshes:
            o.hide_render=not is_mimic
        target=Vector((0,0,.145))
        direction=Vector((.45,-.8,.40)).normalized()
        camera.location=target+direction*(distance or 2)
        camera.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler()
        camera.data.type='PERSP' if distance else 'ORTHO'
        camera.data.ortho_scale=.48
        camera.data.sensor_fit='HORIZONTAL'
        kind='mimic' if is_mimic else 'real-source'
        frames.append(review.pixels(folder/f'compare-{distance}m-{kind}.png',640,640))
    rows.append(np.concatenate(frames,axis=1))
review.board(folder/'real-cake-vs-mimic.png',rows)
print('DISGUISE_REVIEW_COMPLETE: left=real authored source; right=exported Mimic; rows=close,3m,6m; Lumen not simulated')
