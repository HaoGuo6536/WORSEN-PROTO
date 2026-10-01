# ============================================================================
# env_theme_furnished.py
# PURPOSE:
#   Assemble furnished modular room expansions from each theme's own current kit.
#   Keep the new catalogue explicit while the coordinator extends the runtime's
#   closed kind schema and fixed-count tests. Never silently rename a shrine room.
# ARCHITECTURAL ROLE: Offline art generator · Environment; no Unity runtime layer.
# KEY RESPONSIBILITIES:
#   - Assemble existing theme shells on the 2m grid with measured prop back planes.
#   - Reserve functional groups, gameplay sockets and connected walking space.
#   - Publish expansion manifests without altering the runtime catalogue count.
#   - Render plan/perspective evidence and save exact editable room assemblies.
# DEPENDENCIES: Blender 5.2; theme shell builders; env_theme_room_layouts;
#   validate_env_theme_vaults for conservative standing-envelope clearance.
# USAGE NOTES:
#   All public coordinates are Unity XYZ metres. Main generators call publish
#   after their existing source save; expansion scenes join that same Rooms.blend.
#   No model from Assets/Art/Shrine is copied, invented or embedded in the kit.
# ============================================================================
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

sys.path.insert(0,str(Path(__file__).resolve().parent))
from validate_env_theme_vaults import WalkMap, boxes, intersects, socket, rotate

ROOT = Path(__file__).resolve().parents[2]
WALL_GAP = .008
WALK_WIDTH = 1.2
ANCHOR_RADIUS = .6
RENDER_SIZE = (1200, 1000)
RENDER_SAMPLES = 16
SIDES = {'N': (0, 1, 0), 'E': (1, 0, 90), 'S': (0, -1, 180), 'W': (-1, 0, 270)}


def rectangle(w, d):
    return {(x, z) for x in range(w//2) for z in range(d//2)}


def pos(piece, x, z, y=0, yaw=0, **metadata):
    return dict(id=piece, pos=[round(x, 6), round(y, 6), round(z, 6)], rotY=yaw, **metadata)


class FurnishedRoom:
    def __init__(self, theme, api, objects, rows, name, w, d, cells=None, doors=None, kind='room'):
        self.theme, self.api, self.objects, self.rows = theme, api, objects, rows
        self.w, self.d = w, d
        self.cells = cells if cells is not None else rectangle(w, d)
        doors = doors or [((1, 0), 'S'), ((w//2-1, 1), 'E')]
        shape = 'rect' if len(self.cells)*4 == w*d else 'L'
        if theme == 'castle':
            t = api['room'](name, self.cells, doors, shape=shape, low=True)
        elif theme == 'hospital':
            t = api['assemble_template'](name, self.cells, 'room', shape, doors, [])
        elif theme == 'school':
            t = api['make_room'](name, self.cells, doors, shape=shape)
        else:
            ds = [dict(cell=list(c), side=s) for c, s in doors]
            ps = api['shell'](self.cells, ds)
            for x, z in sorted(self.cells):
                ps.extend([pos('floor_2x2', 2*x+1, 2*z+1, -.1001),
                           pos('ceiling_2x2', 2*x+1, 2*z+1, api['HEIGHT']-.34)])
            n = len(self.cells)
            t = dict(id=theme+'_'+name, kind='room', shape=shape,
                     sizeClass='small' if n<=9 else 'medium' if n<=20 else 'large' if n<=40 else 'hall',
                     footprint=[list(c) for c in sorted(self.cells)], height=api['HEIGHT'],
                     doors=ds, anchors={}, gimmick='none', minRound=1, weight=1.0, pieces=ps)
        self.t = t
        # Keep structural kit geometry, not the old scatter dressing algorithm.
        t['pieces'] = [p for p in t['pieces'] if rows[p['id']]['kind'] in
                       ('wall', 'window', 'door', 'floor', 'ceiling')]
        t['kind'] = kind
        t['furnishingVersion'] = 1
        t['groups'], t['reservedAreas'] = [], []
        t['anchors'] = dict(cake=[], goldenCake=[], hunterSpawn=[], light=[])
        if theme == 'castle':
            for door in t['doors']:
                p = door['closedWith'][0]
                t['pieces'].append(pos('floor_portal_4m', p['pos'][0], p['pos'][2], -.16, p['rotY']))
        self.lights()

    def put(self, piece, x, z, yaw=0, y=0, support='floor'):
        p = pos(piece, x, z, y, yaw, support=support)
        self.t['pieces'].append(p)
        return len(self.t['pieces'])-1

    def wall(self, piece, side, along, y=0, line=None):
        dx, dz, yaw = SIDES[side]
        line = line if line is not None else {'N':self.d,'S':0,'E':self.w,'W':0}[side]
        # A cupboard/board needs solid backing, not a boarded window projecting
        # through its back. Keep all unrelated windows in the room shell.
        for p in self.t['pieces']:
            if self.rows[p['id']]['kind']=='window' and p['rotY']==yaw:
                tangent,plane=(p['pos'][0],p['pos'][2]) if side in 'NS' else (p['pos'][2],p['pos'][0])
                if abs(plane-line)<.01 and abs(tangent-along)<2:
                    p['id']='wall_2m'
        # These are the existing shell's actual interior-face relief datums.
        inset = {'castle':0, 'hospital':.25, 'school':.028, 'basement':.006}[self.theme]
        obj = self.objects[piece]
        back = max(-v.co.y for v in obj.data.vertices)
        offset = inset+back+WALL_GAP
        x, z = (along, line-dz*offset) if side in 'NS' else (line-dx*offset, along)
        index = self.put(piece, x, z, yaw, y, 'wall' if y else 'floor-wall')
        self.t['pieces'][index]['backWall'] = dict(side=side, line=line)
        return index

    def group(self, kind, members, target=None, facing=None):
        value = dict(kind=kind, members=members)
        if target is not None:
            value['target'] = target
        if facing is not None:
            value['facing'] = facing
        self.t['groups'].append(value)

    def reserve(self, name, center, size):
        self.t['reservedAreas'].append(dict(id=name, center=center, size=size))

    def lights(self):
        if self.theme == 'castle':
            for z in (2, self.d-2):
                self.wall('prop_torch_sconce', 'W', z, 3.3)
                self.t['anchors']['light'].append([.8,4.2,z])
        else:
            piece = {'hospital':'light_fluorescent_panel','school':'prop_fluorescent',
                     'basement':'cage_lamp'}[self.theme]
            h = self.rows[piece]['size'][1]
            positions=((3,3),(3,self.d-3)) if self.t['kind']=='puzzle' else ((3,3),(self.w-3,self.d-3))
            for x, z in positions:
                if (int(x//2), int(z//2)) in self.cells:
                    self.put(piece,x,z,y=self.t['height']-h,support='ceiling')
                    self.t['anchors']['light'].append([x,self.t['height']-h-.1,z])

    def finish(self):
        # Keep cakes out of reserved interaction/puzzle lanes, then put them in
        # orderly short runs along the walkable room perimeter, not random scatter.
        world = WalkMap(self.t, self.rows)
        start, normal = socket(self.t['doors'][0])
        seen = world.flood([start[0]-normal[0],0,start[1]-normal[1]])
        candidates = []
        for z in range(1, self.d):
            for x in range(1, self.w):
                p = [x,0,z]
                if not world.clear(p, ANCHOR_RADIUS) or not world.reached(p, seen):
                    continue
                if any(area_contains(a,p,ANCHOR_RADIUS) for a in self.t['reservedAreas']):
                    continue
                candidates.append(p)
        needed = max(2,(len(self.cells)*2+8)//9)
        chosen = []
        for p in candidates:
            if all(math.dist(p,q)>=1.4 for q in chosen):
                chosen.append(p)
        if len(chosen)<needed+1:
            raise AssertionError((self.t['id'],'insufficient clear anchors',len(chosen),needed))
        self.t['anchors']['cake'] = chosen[:needed]
        self.t['anchors']['hunterSpawn'] = [chosen[-1]]
        if self.t['kind']=='puzzle':
            self.t['anchors']['goldenCake'] = [self.t['puzzleSockets']['reward']]
        elif len(self.cells)>20:
            self.t['anchors']['goldenCake'] = [chosen[-2]]
        self.t['minimumWalkingWidth'] = WALK_WIDTH
        return self.t


def area_contains(area, p, pad=0):
    return all(abs(p[i]-area['center'][i]) < area['size'][i]/2+pad for i in (0,2))


def build_expansion(theme, api, objects, rows):
    from env_theme_room_layouts import layout_rooms
    lookup = {r['id']:r for r in rows}
    def create(name, w, d, **kw):
        return FurnishedRoom(theme,api,objects,lookup,name,w,d,**kw)
    templates = layout_rooms(theme,create)
    return dict(theme=theme,module=2.0,schemaVersion=2,
                activation='coordinator-required: merge templates after kind/socket admission and count-test update',
                templates=templates)


def publish_expansion(theme, api, objects, rows, skip=False):
    manifest = build_expansion(theme,api,objects,rows)
    art = ROOT/'Assets/Art/Environment'/theme.title()/'Rooms'
    path = art/(theme.title()+'Rooms.expansion.manifest.json')
    path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8',newline='\n')
    from validate_env_theme_furnished import validate_layout
    lookup = {r['id']:r for r in rows}
    points = {pid:[(v.co.x,v.co.z,-v.co.y) for v in obj.data.vertices] for pid,obj in objects.items()}
    for t in manifest['templates']:
        validate_layout(t,lookup,points)
    render_expansion(theme,manifest,objects,lookup,skip)
    print(f"FURNISHED {theme}: {len(manifest['templates'])} expansion templates; explicit activation required",flush=True)


def render_expansion(theme, manifest, objects, rows, skip):
    out = ROOT/'Logs/AgentValidation/Art/Rooms'/theme
    out.mkdir(parents=True,exist_ok=True)
    receipts=[]
    for t in manifest['templates']:
        scene=bpy.data.scenes.new('Furnished_'+t['id'])
        bpy.context.window.scene=scene
        scene.unit_settings.system='METRIC'
        scene['template_id']=t['id']
        scene.render.engine='CYCLES'
        scene.cycles.samples=RENDER_SAMPLES
        scene.cycles.use_denoising=True
        scene.cycles.seed=261001
        scene.render.resolution_x,scene.render.resolution_y=RENDER_SIZE
        scene.render.resolution_percentage=100
        scene.render.image_settings.file_format='PNG'
        scene.world=bpy.data.worlds.new(t['id']+'_review_world')
        scene.world.use_nodes=True
        scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.32,.36,.42,1)
        scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45
        scene.view_settings.view_transform='AgX'
        w=2*(max(c[0] for c in t['footprint'])+1)
        d=2*(max(c[1] for c in t['footprint'])+1)
        placed=[]
        for i,p in enumerate(t['pieces']):
            obj=bpy.data.objects.new(f"F_{t['id']}_{i:04d}",objects[p['id']].data)
            scene.collection.objects.link(obj)
            obj.location=(p['pos'][0],-p['pos'][2],p['pos'][1])
            obj.rotation_euler.z=math.radians(p['rotY'])
            obj['placement_index']=i
            obj['piece_id']=p['id']
            placed.append(obj)
        for label,location,power,size in (
                ('key',(w*.35,2,12),2600,9),('fill',(w+3,-d*.55,10),1800,8)):
            data=bpy.data.lights.new(t['id']+label,'AREA')
            data.energy=power; data.shape='DISK'; data.size=size
            light=bpy.data.objects.new(data.name,data); scene.collection.objects.link(light)
            light.location=location
            light.rotation_euler=(Vector((w/2,-d/2,0))-light.location).to_track_quat('-Z','Y').to_euler()
        camera=bpy.data.objects.new(t['id']+'_camera',bpy.data.cameras.new(t['id']+'_camera'))
        scene.collection.objects.link(camera); scene.camera=camera
        # Socket diagrams are review-only curves, never exported geometry or shrine models.
        annotations=[]
        for area in t['reservedAreas']:
            cx,_,cz=area['center']; sx,_,sz=area['size']
            curve=bpy.data.curves.new(area['id'],'CURVE'); curve.dimensions='3D'; curve.bevel_depth=.025
            line=curve.splines.new('POLY'); line.points.add(3); line.use_cyclic_u=True
            for point,(x,z) in zip(line.points,((cx-sx/2,cz-sz/2),(cx+sx/2,cz-sz/2),
                                               (cx+sx/2,cz+sz/2),(cx-sx/2,cz+sz/2))):
                point.co=(x,-z,.04,1)
            ob=bpy.data.objects.new('REVIEW_ONLY_'+area['id'],curve); scene.collection.objects.link(ob)
            material=bpy.data.materials.get('RoomSocketDiagram') or bpy.data.materials.new('RoomSocketDiagram')
            material.diffuse_color=(.13,.65,.75,1)
            material.use_nodes=True
            material.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.13,.65,.75,1)
            curve.materials.append(material); annotations.append(ob)
        for view in ('plan','perspective'):
            hidden=[]
            cutaways=[]
            for i,(p,obj) in enumerate(zip(t['pieces'],placed)):
                kind=rows[p['id']]['kind']
                roof=kind=='ceiling' or p.get('support')=='ceiling'
                near=kind in ('wall','window','door') and p['rotY'] in (180,90)
                obj.hide_render=roof or (view=='perspective' and near)
                if view=='plan' and kind in ('wall','window','door'):
                    # A real horizontal mesh cut reveals door apertures; merely
                    # scaling a wall would lower its lintel and conceal the door.
                    clipped=bpy.data.objects.new('REVIEW_ONLY_cut_'+str(i),obj.data.copy())
                    scene.collection.objects.link(clipped)
                    # New object world matrices have not yet been evaluated by
                    # Blender. Copy authored transforms, never stale identity.
                    clipped.location=obj.location.copy()
                    clipped.rotation_euler=obj.rotation_euler.copy()
                    mesh=bmesh.new(); mesh.from_mesh(clipped.data)
                    bmesh.ops.bisect_plane(mesh,geom=list(mesh.verts)+list(mesh.edges)+list(mesh.faces),
                                          plane_co=(0,0,1.1-p['pos'][1]),plane_no=(0,0,1),
                                          clear_outer=True,clear_inner=False)
                    mesh.to_mesh(clipped.data); mesh.free()
                    cutaways.append(clipped); obj.hide_render=True
                if obj.hide_render: hidden.append(i)
            for ob in annotations: ob.hide_render=view!='plan'
            camera.data.type='ORTHO' if view=='plan' else 'PERSP'
            if view=='plan':
                camera.location=(w/2,-d/2,26)
                camera.rotation_euler=(0,0,0)
                camera.data.ortho_scale=max(w+2,(d+2)*RENDER_SIZE[0]/RENDER_SIZE[1])*1.08
            else:
                camera.data.lens=39
                camera.location=(w*1.4,d*.50,max(w,d)*.90+4)
                target=Vector((w*.46,-d*.49,.60))
                camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
                # Keep the tall keep walls and all floor edges in the perspective,
                # not just the furniture. Test projected bounds, not a guessed lens.
                for attempt in range(12):
                    bpy.context.view_layer.update()
                    projected=[world_to_camera_view(scene,camera,obj.matrix_world@Vector(corner))
                               for obj in placed if not obj.hide_render for corner in obj.bound_box]
                    if all(.04<=p.x<=.96 and .04<=p.y<=.96 and p.z>0 for p in projected): break
                    camera.location=target+(camera.location-target)*1.12
                else: raise AssertionError(t['id']+' perspective framing')
            target=out/(t['id']+'-'+view+'.png')
            if not skip:
                scene.render.filepath=str(target)
                bpy.ops.render.render(write_still=True)
                receipts.append(dict(id=t['id'],view=view,image=target.name,
                                     imageSha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                                     hiddenPlacements=hidden,reviewOnly=True))
            for clipped in cutaways:
                data=clipped.data
                bpy.data.objects.remove(clipped,do_unlink=True)
                bpy.data.meshes.remove(data)
        scene['reviewInstructions']='Exact manifest meshes. Roof/near-wall visibility is review-only; blue plan outlines reserve sockets, not shrine art.'
    if not skip:
        inputs={}
        for p in (ROOT/'Assets/Art/Environment'/theme.title()/'Kit'/(theme.title()+'Kit.manifest.json'),
                  ROOT/'Assets/Art/Environment'/theme.title()/'Rooms'/(theme.title()+'Rooms.expansion.manifest.json'),
                  Path(__file__),ROOT/'tools/blender/env_theme_room_layouts.py',ROOT/'tools/blender/env_kit_furnishings.py'):
            inputs[str(p.relative_to(ROOT)).replace('\\','/')]=hashlib.sha256(p.read_bytes()).hexdigest()
        (out/'render-receipts.json').write_text(json.dumps(dict(inputs=inputs,images=receipts),indent=2)+'\n',encoding='utf-8')
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Environment'/theme.title()/'Rooms'/(theme.title()+'Rooms.blend')))


if __name__=='__main__':
    # Fast authoring check, no exports or source replacement. Final publishing
    # still uses the four theme entry points and validates imported FBX again.
    import importlib
    import sys
    sys.path.insert(0,str(Path(__file__).parent))
    theme=sys.argv[sys.argv.index('--')+1]
    api=vars(importlib.import_module('env_theme_'+theme))
    publish='--publish' in sys.argv
    area='Rooms' if publish else 'Kit'
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Environment'/theme.title()/area/(theme.title()+area+'.blend')))
    bpy.context.preferences.filepaths.save_version=0
    if publish:
        for scene in list(bpy.data.scenes):
            if scene.name.startswith('Furnished_'):
                for obj in list(scene.objects):
                    bpy.data.objects.remove(obj,do_unlink=True)
                bpy.data.scenes.remove(scene)
    rows=json.loads((ROOT/'Assets/Art/Environment'/theme.title()/'Kit'/(theme.title()+'Kit.manifest.json')).read_text())['pieces']
    objects={r['id']:bpy.data.objects[theme.title()+'_'+r['id']] for r in rows}
    manifest=build_expansion(theme,api,objects,rows)
    from validate_env_theme_furnished import validate_layout
    points={pid:[(v.co.x,v.co.z,-v.co.y) for v in obj.data.vertices] for pid,obj in objects.items()}
    failed=[]
    for t in manifest['templates']:
        try: validate_layout(t,{r['id']:r for r in rows},points)
        except AssertionError as e: failed.append(str(e))
    print('LAYOUT_CHECK',theme,json.dumps(failed),flush=True)
    if failed: raise AssertionError('Layout check failed')
    if publish: publish_expansion(theme,api,objects,rows)
