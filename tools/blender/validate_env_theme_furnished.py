# ============================================================================
# validate_env_theme_furnished.py
# PURPOSE: Fail closed on unsupported, colliding or unusable furnished rooms.
# ARCHITECTURAL ROLE: Offline art validator · Environment; no runtime layer.
# KEY RESPONSIBILITIES:
#   - Measure floor/support and wall-back attachment from imported FBX vertices.
#   - Check prop separation, door throats, walking routes and gameplay anchors.
#   - Validate functional groups and dedicated shrine/puzzle space contracts.
#   - Reject deliberately damaged layouts and hash-bind render evidence.
# DEPENDENCIES: Python standard library; validate_env_theme_vaults collision math.
# USAGE NOTES: Called by every theme validator with independently imported meshes.
#   This does not establish Unity import, navigation or consumer schema admission.
# ============================================================================
import copy
import hashlib
import json
import math
from pathlib import Path
from validate_env_theme_vaults import WalkMap, boxes, intersects, rotate, socket, require, hub_sites

ROOT=Path(__file__).resolve().parents[2]
TOLERANCE=.002
WALL_TOLERANCE=.025
WALL_FURNITURE={
    'prop_bed','prop_cabinet','prop_scrub_sink','prop_bookcase','locker_bank_2m','prop_teacher_desk','prop_boiler',
    'prop_room_radiator','prop_room_linen_shelf','prop_room_washer','prop_room_reception_desk',
    'prop_room_coat_rack','prop_room_piano','prop_room_stores_shelf','prop_room_toolboard',
    'prop_room_pipe_riser','prop_room_service_locker','prop_room_bookcase','prop_room_hearth',
    'prop_room_kitchen_dresser','prop_room_sleeping_cot','prop_room_sideboard','prop_room_niche_panel',
}


def bounds(p,points):
    transformed=[]
    for x,y,z in points[p['id']]:
        x,z=rotate(x,z,p['rotY'])
        transformed.append((x+p['pos'][0],y+p['pos'][1],z+p['pos'][2]))
    return tuple(min(v[i] for v in transformed) for i in range(3)), tuple(max(v[i] for v in transformed) for i in range(3))


def overlap(a,b):
    return all(min(a[1][i],b[1][i])-max(a[0][i],b[0][i])>TOLERANCE for i in range(3))


def area_bounds(a):
    return tuple(a['center'][i]-a['size'][i]/2 for i in range(3)),tuple(a['center'][i]+a['size'][i]/2 for i in range(3))


class WideWalk(WalkMap):
    def clear(self,p,radius=.6,height=2):
        return super().clear(p,radius,height)

    def sweep(self,a,b):
        return not any(intersects(box,a,b,.6,2) for box in self.obstacles)


def validate_layout(t,rows,points):
    label=t['id']+': '
    require(t['furnishingVersion']==1,label+'version')
    require(t['kind'] in ('room','shrine','puzzle'),label+'kind')
    require(t['minimumWalkingWidth']==1.2,label+'walking contract')
    require(len(t['footprint'])==len(set(map(tuple,t['footprint']))),label+'duplicate cells')
    measured=[bounds(p,points) for p in t['pieces']]
    props=[i for i,p in enumerate(t['pieces']) if 'support' in p]
    require(all(p.get('support') for p in t['pieces'] if rows[p['id']]['kind'] not in ('floor','ceiling','wall','window','door')),
            label+'unclassified furnishing support')
    theme=t['id'].split('_')[0]
    inset={'castle':0,'hospital':.25,'school':.028,'basement':.006}[theme]
    for i in props:
        p=t['pieces'][i]; lo,hi=measured[i]; support=p['support']
        require(support in ('floor','floor-wall','wall','ceiling'),label+'support '+p['id'])
        if p['id'] in WALL_FURNITURE:
            require(support in ('floor-wall','wall') and 'backWall' in p,label+'wall-type furniture needs backing '+p['id'])
        if support.startswith('floor'):
            require(abs(lo[1])<TOLERANCE,label+'floating/buried '+p['id'])
        if support=='ceiling':
            require(abs(hi[1]-t['height'])<TOLERANCE,label+'ceiling support '+p['id'])
        if support in ('wall','floor-wall'):
            wall=p.get('backWall'); require(wall is not None,label+'wall mount declaration')
            side,line=wall['side'],wall['line']
            expected={'N':0,'E':90,'S':180,'W':270}[side]
            require(p['rotY']==expected,label+'back orientation '+p['id'])
            gap={'N':line-inset-hi[2],'E':line-inset-hi[0],
                 'S':lo[2]-line-inset,'W':lo[0]-line-inset}[side]
            require(-TOLERANCE<=gap<=WALL_TOLERANCE,label+'wall back gap '+p['id']+' '+str(gap))
            # Prove the declared support is a boundary, not a fabricated interior plane.
            x=(lo[0]+hi[0])/2; z=(lo[2]+hi[2])/2
            dx,dz={'N':(0,1),'E':(1,0),'S':(0,-1),'W':(-1,0)}[side]
            edge=(x,line) if side in 'NS' else (line,z)
            inside=(math.floor((edge[0]-dx*.01)/2),math.floor((edge[1]-dz*.01)/2))
            outside=(math.floor((edge[0]+dx*.01)/2),math.floor((edge[1]+dz*.01)/2))
            cells=set(map(tuple,t['footprint']))
            require(inside in cells and outside not in cells,label+'missing supporting wall '+p['id'])
        for j in props:
            if j>i:
                require(not overlap(measured[i],measured[j]),label+'prop overlap '+p['id']+' / '+t['pieces'][j]['id'])
        for area in t['reservedAreas']:
            require(not overlap(measured[i],area_bounds(area)),label+'reserved '+area['id']+' / '+p['id'])
        for j,q in enumerate(t['pieces']):
            if rows[q['id']]['kind'] in ('wall','window'):
                require(not overlap(measured[i],measured[j]),label+'wall intersection '+p['id']+' / '+q['id'])
            elif rows[q['id']]['kind']=='door':
                for cx,cy,cz,hx,hy,hz,yaw in boxes(dict(id=t['id'],pieces=[q]),rows):
                    ex=abs(math.cos(math.radians(yaw)))*hx+abs(math.sin(math.radians(yaw)))*hz
                    ez=abs(math.sin(math.radians(yaw)))*hx+abs(math.cos(math.radians(yaw)))*hz
                    require(not overlap(measured[i],((cx-ex,cy-hy,cz-ez),(cx+ex,cy+hy,cz+ez))),label+'door frame intersection '+p['id'])
        for door in t['doors']:
            (x,z),(dx,dz)=socket(door)
            throat=dict(center=[x-dx*.6,1.4,z-dz*.6],size=[1.2 if dx else 3.2,2.8,3.2 if dx else 1.2])
            require(not overlap(measured[i],area_bounds(throat)),label+'door blocked '+p['id'])
    world=WideWalk(t,rows)
    require(world.cells==set(map(tuple,t['footprint'])),label+'missing floor support')
    start,normal=socket(t['doors'][0]); start=[start[0]-normal[0],0,start[1]-normal[1]]
    seen=world.flood(start)
    require(bool(seen),label+'entry clearance')
    for door in t['doors']:
        (x,z),(dx,dz)=socket(door)
        require(world.reached([x-dx,0,z-dz],seen),label+'door route')
    for key,anchors in t['anchors'].items():
        if key=='light': continue
        for p in anchors:
            require(world.clear(p) and world.reached(p,seen),label+'anchor clearance '+key+' '+str(p))
    for p,radius in hub_sites(t,rows):
        require(world.clear(p,radius) and world.reached(p,seen),label+'selected player/exit clearance')
    for group in t['groups']:
        require(bool(group['members']),label+'empty functional group')
        target=group.get('target')
        if group['kind']=='boiler-train':
            boiler,pump=[t['pieces'][i] for i in group['members']]
            require(abs(boiler['pos'][0]-pump['pos'][0])<.01 and pump['pos'][2]<boiler['pos'][2],label+'pump must serve its boiler')
        if group['kind']=='workbench-tools':
            bench,tools=[t['pieces'][i] for i in group['members']]
            require(abs(bench['pos'][0]-tools['pos'][0])<.01 and tools['pos'][1]>=rows[bench['id']]['size'][1],label+'toolboard above bench')
        for index in group['members']:
            p=t['pieces'][index]
            if group['kind']=='ward-beds':
                require(p.get('backWall',{}).get('side') in ('E','W'),label+'ward bed alignment')
            if group['kind'] in ('desk-row','pews'):
                require(p['rotY']==180,label+'facing board/altar')
                require(t['pieces'][target]['pos'][2]>p['pos'][2],label+'target behind furniture')
            if group['kind']=='seating':
                q=t['pieces'][target]
                fx,fz=rotate(0,-1,p['rotY'])
                vx,vz=q['pos'][0]-p['pos'][0],q['pos'][2]-p['pos'][2]
                require(vx*fx+vz*fz>.25 and math.hypot(vx,vz)<1.7,label+'chair at table')
    if t['kind']=='shrine':
        require(len(t.get('shrineSockets',[]))==1,label+'exactly one shrine')
        s=t['shrineSockets'][0]; gap=t['passageGap']
        require(s['facing']==[1,0,0] and s['position'][1]==0,label+'shrine facing')
        require(world.reached(s['interactionCenter'],seen),label+'shrine interaction')
        require(gap['edge'][2]==s['position'][2]==gap['landing'][2] and gap['landing'][0]-gap['edge'][0]==4,label+'Passage alignment')
        require(gap['width']==2.4 and gap['sealedUntilActivated'] and gap['optionalOnly'],label+'Passage contract')
    else: require(not t.get('shrineSockets'),label+'shrine in ordinary room')
    if t['kind']=='puzzle':
        s=t['puzzleSockets']; origin=s['origin']
        require(s['laneLength']==8 and s['laneWidth']==1.6 and s['clearance']==.8,label+'puzzle dimensions')
        require(s['axis']==[0,0,1] and s['steps']==[[origin[0],0,origin[2]+z] for z in (-3,-1,1)],label+'puzzle step transform')
        require(s['reward']==[origin[0],0,origin[2]+3],label+'puzzle reward transform')
        for p in s['steps']+[s['reward']]: require(world.reached(p,seen),label+'puzzle socket clearance')
    # Future puzzle cage and shrine model cannot cut off ordinary door routes.
    for a in t['reservedAreas']:
        if a['id'] not in ('puzzle-envelope','shrine-model'): continue
        x,y,z=a['center']; w,h,d=a['size']; world.obstacles.append((x,y,z,w/2,h/2,d/2,0))
    world.cache.clear(); seen=world.flood(start)
    for door in t['doors']:
        (x,z),(dx,dz)=socket(door)
        require(world.reached([x-dx,0,z-dz],seen),label+'gameplay allocation blocks circulation')
    return dict(id=t['id'],props=len(props),groups=len(t['groups']),minimumWalkingWidth=1.2)


def validate_expansion(theme,rows,points,previews=True):
    path=ROOT/'Assets/Art/Environment'/theme.title()/'Rooms'/(theme.title()+'Rooms.expansion.manifest.json')
    manifest=json.loads(path.read_text(encoding='utf-8'))
    templates=manifest['templates']
    require(len(templates)==10 and len({t['id'] for t in templates})==10,theme+' expansion inventory')
    require(sum(t['kind']=='shrine' for t in templates)==1 and sum(t['kind']=='puzzle' for t in templates)==1,theme+' dedicated rooms')
    require(sum('transition' in t for t in templates)==2,theme+' transitions')
    details=[validate_layout(t,rows,points) for t in templates]
    for t in templates:
        if t['kind']!='shrine': continue
        gap=t['passageGap']
        pocket=next(p for p in templates if p['id']==gap['pocketTemplateId'])
        ox,oz=(v*2 for v in gap['pocketOffset'])
        require(gap['pocketTurns']==0 and gap['landing'][0]==ox,theme+' pocket transform/landing')
        local=[gap['landing'][0]-ox+1.4,0,gap['landing'][2]-oz]
        target=WideWalk(pocket,rows)
        for offset in (-gap['width']/2+.6,0,gap['width']/2-.6):
            require(target.clear([local[0],0,local[2]+offset]),theme+' supported Passage landing')
        seen=target.flood(local)
        for d in pocket['doors']:
            (x,z),(dx,dz)=socket(d)
            require(target.reached([x-dx,0,z-dz],seen),theme+' Passage pocket route')
    controls=[]
    for mutation in ('floating','wall-gap','overlap','door','anchor','shrine-count','puzzle-step','reserved','wall-classification','functional-facing'):
        t=copy.deepcopy(next(t for t in templates if t['kind']==('shrine' if mutation=='shrine-count' else 'puzzle' if mutation=='puzzle-step' else 'room')))
        p=next(p for p in t['pieces'] if p.get('support')=='floor-wall')
        if mutation=='floating': p['pos'][1]+=.2
        elif mutation=='wall-gap':
            dx,dz={'N':(0,-1),'S':(0,1),'E':(-1,0),'W':(1,0)}[p['backWall']['side']]
            p['pos'][0]+=dx*.4; p['pos'][2]+=dz*.4
        elif mutation=='overlap': t['pieces'].append(copy.deepcopy(p))
        elif mutation=='door':
            (x,z),(dx,dz)=socket(t['doors'][0]); p['pos']=[x-dx,0,z-dz]; p['support']='floor'
        elif mutation=='anchor': t['anchors']['cake'][0]=list(p['pos'])
        elif mutation=='shrine-count': t['shrineSockets']*=2
        elif mutation=='puzzle-step': t['puzzleSockets']['steps'][0][2]+=.25
        elif mutation=='wall-classification': p['support']='floor'; del p['backWall']
        elif mutation=='functional-facing':
            group=t['groups'][0]
            p=t['pieces'][group['members'][0]]
            if group['kind']=='boiler-train': p['pos'][0]+=.1
            else: p['rotY']=(p['rotY']+180)%360
        else: t['reservedAreas'].append(dict(id='blocked',center=[p['pos'][0],1,p['pos'][2]],size=[2,2,2]))
        try: validate_layout(t,rows,points)
        except AssertionError: controls.append(mutation)
        else: raise AssertionError(theme+' accepted negative control '+mutation)
    out=ROOT/'Logs/AgentValidation/Art/Rooms'/theme
    if previews:
        receipt=json.loads((out/'render-receipts.json').read_text())
        require(len(receipt['images'])==20,theme+' render count')
        for relative,digest in receipt['inputs'].items():
            require(hashlib.sha256((ROOT/relative).read_bytes()).hexdigest()==digest,theme+' stale render input '+relative)
        for item in receipt['images']:
            require(hashlib.sha256((out/item['image']).read_bytes()).hexdigest()==item['imageSha256'],theme+' image mismatch')
    import bpy
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'ArtSource/Environment'/theme.title()/'Rooms'/(theme.title()+'Rooms.blend')))
    for t in templates:
        scene=bpy.data.scenes.get('Furnished_'+t['id'])
        require(scene is not None,t['id']+' missing source scene')
        instances={o.get('placement_index'):o for o in scene.objects if 'placement_index' in o}
        require(len(instances)==len(t['pieces']),t['id']+' source placement count')
        for i,p in enumerate(t['pieces']):
            o=instances[i]
            require(o['piece_id']==p['id'],t['id']+' source piece id')
            expected=(p['pos'][0],-p['pos'][2],p['pos'][1])
            require(max(abs(a-b) for a,b in zip(o.location,expected))<.00001,t['id']+' source position')
            require(abs(math.degrees(o.rotation_euler.z)-p['rotY'])<.0001 and all(abs(v-1)<.00001 for v in o.scale),t['id']+' source transform')
            local=[(v.co.x,v.co.z,-v.co.y) for v in o.data.vertices]
            for axis in range(3):
                require(abs(min(v[axis] for v in local)-min(v[axis] for v in points[p['id']]))<.0001 and
                        abs(max(v[axis] for v in local)-max(v[axis] for v in points[p['id']]))<.0001,t['id']+' source/export bounds')
    result=dict(passed=True,theme=theme,templates=details,negativeControls=controls,previewsChecked=previews)
    out.mkdir(parents=True,exist_ok=True)
    (out/'validation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(f'PASS {theme} furnished expansion: {len(details)} rooms; grounded/flush/groups/doors/1.2m routes/anchors/shrine/puzzle; {len(controls)} negative controls rejected',flush=True)
    return result
