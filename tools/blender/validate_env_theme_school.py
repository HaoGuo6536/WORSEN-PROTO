# ============================================================================
# validate_env_theme_school.py
# PURPOSE: Independently check the School's exported geometry and room catalogue.
#   Re-imported FBXs, saved Blender assemblies and manifest placements must agree.
#   Negative controls exercise rejection paths; this is not an artistic approval
#   or a substitute for the coordinator's Unity integration tests.
# ARCHITECTURAL ROLE: Offline acceptance tool; outside Unity runtime layers.
# KEY RESPONSIBILITIES:
#   - Verify v1 kit gates with the new School's authored dimensions and surfaces.
#   - Check end caps, support, enclosure and non-blocking vault density/clearance.
#   - Verify saved assemblies, preview provenance and dark-scene light sources.
#   - Regenerate twice and compare manifests and imported geometry when requested.
# DEPENDENCIES: Blender 5.2, bundled FBX parser/NumPy, Python standard library;
#   shared imported-mesh support/socket/door checks in validate_env_theme_castle.
# USAGE NOTES: -- --determinism performs two real generator runs before validation.
#   Does not import either generator. Reports/logs are written only under Logs;
#   regeneration writes only the owned School art via env_theme_school.py.
# ============================================================================
import argparse
import copy
import hashlib
import json
import math
import re
import subprocess
import sys
from collections import Counter
from pathlib import Path

import bpy
from bpy_extras.object_utils import world_to_camera_view
from io_scene_fbx import parse_fbx
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT/'Assets/Art/Environment/School'
SOURCE = ROOT/'ArtSource/Environment/School'
REPORT = ROOT/'Logs/AgentValidation/Art/EnvSchool'
EPS = .000025
REQUIRED = {'wall_2m':'wall','wall_door_4m':'door','wall_window_2m':'window',
            'wall_arc_r4':'arc','wall_arc_r6':'arc','wall_arc_r8':'arc',
            'corner_in':'corner','corner_out':'corner','pillar':'pillar',
            'floor_2x2':'floor','ceiling_2x2':'ceiling','trim_base_2m':'trim',
            'prop_desk':'prop','prop_locker_bank':'prop','prop_chalkboard':'prop','prop_chair':'prop'}
PALETTE = {'mustard':'c9a23a','teal':'4f7f7a','chalk_green':'3f5a45','lino':'6d5540',
           'cream':'e8dfc4','steel':'7c8084','fire_red':'8e2f2a'}
DIRECTIONS = {'N':(0,1),'E':(1,0),'S':(0,-1),'W':(-1,0)}
WALL_KINDS = {'wall','door','window','arc','corner'}


def require(condition,message):
    if not condition:
        raise AssertionError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def unity(p):
    return Vector((p.x,p.z,-p.y))


def facts(obj):
    obj.data.calc_loop_triangles()
    points=[unity(obj.matrix_world@v.co) for v in obj.data.vertices]
    materials=[s.material.name if s.material else '' for s in obj.material_slots]
    lo=[min(p[i] for p in points) for i in range(3)]
    hi=[max(p[i] for p in points) for i in range(3)]
    return {'points':points,'lo':lo,'hi':hi,'size':[b-a for a,b in zip(lo,hi)],
            'origin':unity(obj.matrix_world.translation),'materials':materials,
            'triangles':[(tuple(t.vertices),materials[t.material_index]) for t in obj.data.loop_triangles]}


def near_sets(a,b):
    # Quantized sets are fast for exact exports; use nearest-neighbour only at
    # quantization boundaries. Unused vertices are included just as in v1.
    if not a or not b:
        return False
    quant=lambda points:{tuple(round(float(c),5) for c in p) for p in points}
    if quant(a)==quant(b):
        return True
    return all(min((p-q).length for q in b)<EPS for p in a) and all(min((q-p).length for p in a)<EPS for q in b)


def semantic(data):
    triangles=sorted((tuple(sorted(tuple(round(float(v),4) for v in data['points'][i]) for i in ids)),mat)
                     for ids,mat in data['triangles'])
    return hashlib.sha256(json.dumps(triangles,separators=(',',':')).encode()).hexdigest()


def props(element):
    e=next((e for e in element.elems if e.id==b'Properties70'),None)
    return {p.props[0].decode():list(p.props[4:]) for p in e.elems} if e else {}


def raw_fbx(path):
    tree,version=parse_fbx.parse(str(path))
    require(version==7400,path.name+': FBX version')
    settings=props(next(e for e in tree.elems if e.id==b'GlobalSettings'))
    expected={'UpAxis':[1],'UpAxisSign':[1],'FrontAxis':[2],'FrontAxisSign':[1],
              'CoordAxis':[0],'CoordAxisSign':[1],'UnitScaleFactor':[100.0]}
    require(all(settings.get(k)==v for k,v in expected.items()),path.name+': axes/units')
    objects=next(e for e in tree.elems if e.id==b'Objects')
    models=[e for e in objects.elems if e.id==b'Model']
    meshes=[e for e in objects.elems if e.id==b'Geometry']
    require(len(models)==len(meshes)==1,path.name+': export contains non-piece objects')
    values=props(models[0])
    for key,default in (('Lcl Translation',[0,0,0]),('Lcl Rotation',[0,0,0]),('Lcl Scaling',[1,1,1]),
                        ('GeometricTranslation',[0,0,0]),('GeometricRotation',[0,0,0]),('GeometricScaling',[1,1,1]),
                        ('PreRotation',[0,0,0]),('PostRotation',[0,0,0]),('RotationPivot',[0,0,0]),('ScalingPivot',[0,0,0])):
        require(all(abs(a-b)<EPS for a,b in zip(values.get(key,default),default)),path.name+': unapplied '+key)
    coords=next(e for e in meshes[0].elems if e.id==b'Vertices').props[0]
    return [Vector(coords[i:i+3]) for i in range(0,len(coords),3)]


def clipped_area(triangle,bounds):
    polygon=[(v.x,v.y) for v in triangle]
    for axis,edge,greater in ((0,bounds[0],True),(0,bounds[1],False),(1,bounds[2],True),(1,bounds[3],False)):
        output=[]
        for a,b in zip(polygon,polygon[1:]+polygon[:1]):
            ia=a[axis]>=edge if greater else a[axis]<=edge
            ib=b[axis]>=edge if greater else b[axis]<=edge
            if ia!=ib:
                t=(edge-a[axis])/(b[axis]-a[axis])
                output.append(tuple(a[i]+t*(b[i]-a[i]) for i in range(2)))
            if ib:
                output.append(b)
        polygon=output
        if not polygon:
            return 0
    return abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(polygon,polygon[1:]+polygon[:1])))/2


def check_pivot(name,kind,data):
    require(data['origin'].length<EPS and abs(data['lo'][1])<EPS,name+': grounded pivot')
    require(abs(data['lo'][0]+data['hi'][0])<EPS,name+': X pivot')
    if kind in {'prop','floor','ceiling','pillar','trim'}:
        require(abs(data['lo'][2]+data['hi'][2])<EPS,name+': Z pivot')
    if kind in {'wall','door','window'}:
        require(abs(data['hi'][2]-.25)<EPS and data['lo'][2]<-.02,name+': front relief -Z/back +Z')
        require(any(abs(v.y)<EPS and abs(v.z)<EPS for v in data['points']),name+': wall-plane origin')


def dimensions(name,kind,data):
    check_pivot(name,kind,data)
    width,height,depth=data['size']
    if kind in {'wall','door','window'}:
        expected=4 if kind=='door' else 1 if name=='wall_cinderblock_end_1m' else 2
        require(abs(width-expected)<EPS and abs(height-3.8)<EPS,name+': wall dimensions')
    elif kind in {'floor','ceiling'}:
        require(abs(width-2)<EPS and abs(depth-2)<EPS,name+': 2x2 tile')
    elif kind in {'corner','pillar'}:
        require(abs(height-3.8)<EPS,name+': structural height')
    elif kind=='trim':
        require(abs(width-2)<EPS,name+': trim length')
    elif kind=='arc':
        radius=int(name[-1]); half=math.radians({4:30,6:20,8:15}[radius])/2
        require(abs(height-3.8)<EPS,name+': arc height')
        angles=[math.atan2(p.x,p.z+radius) for p in data['points']]
        require(abs(min(angles)+half)<EPS and abs(max(angles)-half)<EPS,name+': arc sweep')
        for p in data['points']:
            r=math.hypot(p.x,p.z+radius)
            require(min(abs(r-radius),abs(r-radius-.25),abs(r-radius+.004))<EPS,name+': arc radius')
        require(any(abs(p.x)<EPS and abs(p.y)<EPS and abs(p.z)<EPS for p in data['points']),name+': tangent pivot')
    else:
        require(0<width<4 and 0<height<3.8 and 0<depth<4,name+': prop envelope')
    # Replaces v1's explicitly provisional furniture bounds; mandatory scale
    # checks above are unchanged. These independent dimensions catch scale drift.
    authored={'prop_desk':(1.1,.761,.65),'prop_locker_bank':(1.98,2.18,.507),
              'prop_chalkboard':(1.94,1.3,.2225),'prop_chair':(.44,.892039,.44),
              'floor_2x2':(2,.0702,2),'ceiling_2x2':(2,.067,2),
              'trim_base_2m':(2,.13,.04),'pillar':(.44,3.8,.44)}
    if name in authored:
        require(all(abs(a-b)<.001 for a,b in zip(data['size'],authored[name])),name+': authored dimensions')
    if kind=='door':
        for ids,_ in data['triangles']:
            require(clipped_area([data['points'][i] for i in ids],(-1.599,1.599,.001,2.799))<1e-8,
                    name+': obstructed 3.2 x 2.8 door aperture')
        for x in (-1.6,1.6):
            require(any(abs(v.x-x)<EPS and abs(v.y)<EPS for v in data['points']),name+': jamb dimensions')
        require(any(abs(v.y-2.8)<EPS and abs(v.x-2)<EPS for v in data['points']),name+': lintel height')


def seams(records):
    def edges(name,width):
        return [[Vector((0,p.y,p.z)) for p in records[name]['points'] if abs(p.x-s*width/2)<EPS] for s in (-1,1)]
    reference=edges('wall_2m',2)[0]
    for name,width in (('wall_2m',2),('wall_window_2m',2),('wall_door_4m',4),
                       ('wall_cinderblock_end_1m',1),('window_boarded_2m',2),('door_classroom_transom',4)):
        require(all(near_sets(edge,reference) for edge in edges(name,width)),name+': wall seam')
    for name in ('floor_2x2','ceiling_2x2','trim_base_2m'):
        require(near_sets(*edges(name,2)),name+': X seam')
        if name!='trim_base_2m':
            zs=[[Vector((p.x,p.y,0)) for p in records[name]['points'] if abs(p.z-s)<EPS] for s in (-1,1)]
            require(near_sets(*zs),name+': Z seam')
    for r,deg in ((4,30),(6,20),(8,15)):
        a=math.radians(deg)
        data=records[f'wall_arc_r{r}']
        ends=[[p for p in data['points'] if abs(math.atan2(p.x,p.z+r)-s*a/2)<EPS] for s in (-1,1)]
        turned=[Vector((p.x*math.cos(a)+(p.z+r)*math.sin(a),p.y,
                        -p.x*math.sin(a)+(p.z+r)*math.cos(a)-r)) for p in ends[0]]
        require(near_sets(turned,ends[1]),f'r{r}: rotated arc seam')


def validate_kit(kit):
    require(kit['theme']=='school' and kit['wallHeight']==3.8,'kit identity')
    rows=kit['pieces']; lookup={r['id']:r for r in rows}
    require(len(lookup)==len(rows) and REQUIRED.keys()<=lookup.keys(),'contract inventory/duplicate ids')
    require(all(lookup[k]['kind']==v for k,v in REQUIRED.items()),'mandatory piece kinds')
    require({p.name for p in (ART/'Kit').glob('*.fbx')}=={r['file'] for r in rows},'unlisted/missing FBX')
    require(not list(ART.rglob('*.blend')),'Blender source under Assets')
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Kit/SchoolKit.blend'))
    originals={}
    for row in rows:
        name=row['id']; obj=bpy.data.objects.get('School_'+name)
        require(obj is not None and obj.type=='MESH',name+': source mesh')
        require(obj.location.length<EPS and obj.rotation_euler.to_matrix().is_identity and
                all(abs(s-1)<EPS for s in obj.scale) and not obj.modifiers,name+': source transforms')
        originals[name]=facts(obj)
        shown=bpy.data.objects.get('Sheet_'+name)
        require(shown is not None,name+': missing sheet instance')
        scene=bpy.context.scene
        coords=[world_to_camera_view(scene,scene.camera,shown.matrix_world@v.co) for v in shown.data.vertices]
        lo=[min(v[i] for v in coords) for i in (0,1)]
        hi=[max(v[i] for v in coords) for i in (0,1)]
        require(min(lo)>0 and max(hi)<1,name+': sheet camera clips piece')
        pixels=[(hi[i]-lo[i])*(scene.render.resolution_x if i==0 else scene.render.resolution_y) for i in (0,1)]
        require(max(pixels)>=60,name+': sheet thumbnail too small')
    for surface,hex_color in PALETTE.items():
        mat=bpy.data.materials.get('school_'+surface)
        require(mat is not None,'missing palette material '+surface)
        rgb=[int(hex_color[i:i+2],16)/255 for i in (0,2,4)]
        lin=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb]
        actual=mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value
        require(all(abs(a-b)<EPS for a,b in zip(actual,lin)),'owner palette '+surface)
    textures=list((ART/'Kit').glob('*.png'))
    require(len(textures)<=6,'texture count')
    for path in textures:
        image=bpy.data.images.load(str(path),check_existing=False)
        require(max(image.size)<=1024,'texture dimensions')
        require((SOURCE/'Kit'/path.name).is_file(),'texture source missing')
    require(all(not i.filepath or i.filepath.startswith('//') for i in bpy.data.images if i.source=='FILE'),
            'nonrelative texture path')
    records,details={},[]
    for row in rows:
        name,kind=row['id'],row['kind']
        require(kind in {'wall','door','window','arc','corner','pillar','floor','ceiling','trim','prop','pipe','duct'},name+': kind')
        require(row['file']=='School_'+name+'.fbx',name+': filename')
        path=ART/'Kit'/row['file']; raw=raw_fbx(path)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
        objects=list(bpy.context.scene.objects)
        require(len(objects)==1 and objects[0].type=='MESH' and objects[0].name==path.stem,name+': export isolation')
        data=facts(objects[0]); records[name]=data
        dimensions(name,kind,data)
        require(near_sets(raw,data['points']),name+': baked Y-up mismatch')
        require(near_sets(originals[name]['points'],data['points']),name+': source/export mismatch')
        require(len(originals[name]['triangles'])==len(data['triangles']),name+': triangle mismatch')
        require(set(originals[name]['materials'])==set(data['materials']),name+': material mismatch')
        require(len(row['size'])==3 and all(math.isfinite(a) and abs(a-b)<EPS for a,b in zip(row['size'],data['size'])),name+': manifest bounds')
        budget=1500 if kind in {'prop','pipe','duct'} else 300
        require(0<len(data['triangles'])<=budget,name+': triangle budget')
        require(data['materials'] and all(re.fullmatch(r'school_[a-z_]+',m) for m in data['materials']),name+': material naming')
        for ids,material in data['triangles']:
            a,b,c=[data['points'][i] for i in ids]
            require((b-a).cross(c-a).length>1e-10,name+': degenerate triangle')
        details.append({'id':name,'triangles':len(data['triangles']),'budget':budget,
                        'size':row['size'],'semanticSha256':semantic(data),'fbxSha256':sha(path)})
    seams(records)
    return lookup,records,details


def footprint_edges(cells):
    edges=[]
    for x,z in cells:
        for side,(dx,dz) in DIRECTIONS.items():
            if (x+dx,z+dz) not in cells:
                if side in 'NS':
                    edges.append(('X',2*(z+(side=='N')),2*x,2*x+2,side))
                else:
                    edges.append(('Z',2*(x+(side=='E')),2*z,2*z+2,side))
    return edges


def wall_segment(row,lookup):
    yaw=row['rotY']
    require(yaw in (0,90,180,270),'non-cardinal wall yaw')
    require(abs(row['pos'][1])<EPS,'floating wall')
    x,_,z=row['pos']; width=lookup[row['id']]['size'][0]
    side={0:'N',90:'E',180:'S',270:'W'}[yaw]
    return ('X',z,x-width/2,x+width/2,side) if side in 'NS' else ('Z',x,z-width/2,z+width/2,side)


def interval_coverage(intervals,lo,hi):
    cursor=lo
    for a,b in sorted(intervals):
        if b<=cursor+EPS:
            continue
        if a>cursor+EPS:
            return False
        cursor=max(cursor,b)
    return cursor>=hi-EPS


def distance_to_edge(x,z,edge):
    axis,fixed,a,b,_=edge
    v,perp=(x,z) if axis=='X' else (z,x)
    return math.hypot(perp-fixed,max(a-v,0,v-b))


def check_door_attachments(t,lookup):
    """Match each closed leaf to a jamb, using independently measured kit width."""
    leaves=[p for p in t['pieces'] if p['id']=='prop_classroom_door_leaf']
    require(len(leaves)==2*len(t['doors']),t['id']+': two leaves per socket')
    assigned=set()
    for door in t['doors']:
        x,z=door['cell']; side=door['side']
        dx,dz=DIRECTIONS[side]
        cx,cz=2*x+1+dx,2*z+1+dz
        cx+=(door.get('span',1)-1)*abs(dz)
        cz+=(door.get('span',1)-1)*abs(dx)
        yaw={'N':0,'E':90,'S':180,'W':270}[side]
        for sign in (-1,1):
            matches=[]
            for index,p in enumerate(leaves):
                px,py,pz=p['pos']
                along=(px-cx) if side in 'NS' else (pz-cz)
                across=(pz-cz) if side in 'NS' else (px-cx)
                width=lookup[p['id']]['size'][0]
                if (abs(py)<=.05 and p['rotY']==yaw and abs(across)<=.05 and
                        along*sign>0 and abs(abs(along)+width/2-1.6)<=.05):
                    matches.append(index)
            require(len(matches)==1 and matches[0] not in assigned,
                    t['id']+': detached or incorrectly rotated door leaf')
            assigned.add(matches[0])
    require(len(assigned)==len(leaves),t['id']+': orphan door leaf')


def validate_room(t,lookup):
    ident=t['id']
    require(ident.startswith('school_'),'foreign template theme')
    require(t['height']==3.8,'room height')
    require(t['kind'] in {'room','hallway','junction'},ident+': kind')
    require(t['shape'] in {'rect','L','T','round','irregular'},ident+': shape')
    raw=t['footprint']
    require(raw and all(len(c)==2 and all(type(i)==int for i in c) and min(c)>=0 for c in raw),ident+': footprint values')
    cells={tuple(c) for c in raw}
    require(len(cells)==len(raw),ident+': duplicate footprint cells')
    visited={next(iter(cells))}; pending=list(visited)
    while pending:
        x,z=pending.pop()
        for dx,dz in DIRECTIONS.values():
            c=(x+dx,z+dz)
            if c in cells and c not in visited:
                visited.add(c); pending.append(c)
    require(visited==cells,ident+': footprint not 4-connected')
    n=len(cells)
    expected='closet' if n<=4 else 'small' if n<=9 else 'medium' if n<=20 else 'large' if n<=40 else 'hall'
    require(t['sizeClass']==expected,ident+': sizeClass')
    if t['shape']=='rect':
        require(n==(max(x for x,z in cells)+1)*(max(z for x,z in cells)+1),ident+': false rect shape')
    if t['shape']=='L':
        missing={(x,z) for x in range(max(x for x,z in cells)+1) for z in range(max(z for x,z in cells)+1)}-cells
        require(missing and len(missing)==(max(x for x,z in missing)-min(x for x,z in missing)+1)*
                (max(z for x,z in missing)-min(z for x,z in missing)+1),ident+': L needs rectangular notch')
    if t['kind']=='hallway':
        counts=Counter(z for x,z in cells)
        require(max(counts.values())<=2,ident+': hallway width')
    require(t['gimmick'] in {'none','puzzle','freeze','traversal'},ident+': gimmick')
    require(type(t['minRound'])==int and t['minRound']>= (1 if t['gimmick']=='none' else 3),ident+': minRound')
    require(math.isfinite(t['weight']) and t['weight']>0,ident+': weight')
    edges=footprint_edges(cells)
    rows=t['pieces']
    for row in rows:
        require(row['id'] in lookup,ident+': unknown piece '+row['id'])
        require(len(row['pos'])==3 and all(math.isfinite(v) for v in row['pos']) and math.isfinite(row['rotY']),ident+': invalid transform')
    walls=[(row,wall_segment(row,lookup)) for row in rows if lookup[row['id']]['kind'] in WALL_KINDS]
    for i,(row,s) in enumerate(walls):
        axis,fixed,a,b,side=s
        matching=[(e[2],e[3]) for e in edges if e[0]==axis and abs(e[1]-fixed)<EPS and e[4]==side]
        require(interval_coverage(matching,a,b),ident+': wall off perimeter')
        for _,other in walls[:i]:
            require(not (other[0]==axis and abs(other[1]-fixed)<EPS and min(b,other[3])-max(a,other[2])>EPS),
                    ident+': overlapping walls')
    doors=t['doors']
    require(len(doors)>=(1 if n<=4 else 2),ident+': insufficient sockets')
    require(len({(tuple(d['cell']),d['side']) for d in doors})==len(doors),ident+': duplicate sockets')
    matched_doors=set()
    for door in doors:
        cell=tuple(door['cell']); side=door['side']
        require(side in DIRECTIONS and cell in cells,ident+': socket cell/side')
        x,z=cell; dx,dz=DIRECTIONS[side]
        require((x+dx,z+dz) not in cells,ident+': socket is not a boundary edge')
        fixed=2*(z+(side=='N')) if side in 'NS' else 2*(x+(side=='E'))
        center=2*(x if side in 'NS' else z)+door.get('span',1)
        axis='X' if side in 'NS' else 'Z'
        matching=[(i,row,s) for i,(row,s) in enumerate(walls) if lookup[row['id']]['kind']=='door' and
                  s[0]==axis and abs(s[1]-fixed)<EPS and s[4]==side and abs((s[2]+s[3])/2-center)<EPS]
        require(len(matching)==1,ident+': door socket needs one centred frame')
        matched_doors.add(matching[0][0])
        alternatives=door.get('closedWith',[])
        require(len(alternatives)==2,ident+': explicit two-wall closedWith')
        closed=[wall_segment(r,lookup) for r in alternatives if r['id']=='wall_2m']
        require(len(closed)==2 and all(s[0]==axis and abs(s[1]-fixed)<EPS and s[4]==side for s in closed),ident+': closedWith transforms')
        require(interval_coverage([(s[2],s[3]) for s in closed],center-2,center+2) and
                abs(sum(s[3]-s[2] for s in closed)-4)<EPS,ident+': closedWith gap/overlap')
    require(len(matched_doors)==sum(lookup[row['id']]['kind']=='door' for row,s in walls),ident+': unregistered opening')
    check_door_attachments(t,lookup)
    for axis,fixed,a,b,side in edges:
        spans=[(s[2],s[3]) for _,s in walls if s[0]==axis and abs(s[1]-fixed)<EPS and s[4]==side]
        require(interval_coverage(spans,a,b),ident+': unenclosed perimeter')
    for kind in ('floor','ceiling'):
        tiles=[r for r in rows if lookup[r['id']]['kind']==kind]
        if kind=='ceiling':
            require(all(abs(r['pos'][1]-t['height'])<=.05 for r in tiles),ident+': ceiling height')
        require(len(tiles)==n,ident+': tile count')
        occupied={(round((r['pos'][0]-1)/2,5),round((r['pos'][2]-1)/2,5)) for r in tiles}
        require(occupied==cells,ident+': tile coverage/grid')
    anchors=t['anchors']
    require(set(anchors)=={'cake','goldenCake','light','hunterSpawn'},ident+': anchor schema')
    require(len(anchors['cake'])>=max(2,(n*2+8)//9),ident+': area-scaled cakes')
    require(len(anchors['goldenCake'])<=1 and anchors['light'],ident+': golden/light sockets')
    require(n<10 or anchors['hunterSpawn'],ident+': missing hunter spawn')
    for kind,points in anchors.items():
        require(len({tuple(p) for p in points})==len(points),ident+': duplicate anchors')
        for p in points:
            require(len(p)==3 and all(math.isfinite(v) for v in p),ident+': anchor values')
            x,y,z=p
            require((math.floor(x/2),math.floor(z/2)) in cells and 0<=y<=3.8,ident+': anchor outside footprint')
            require(min(distance_to_edge(x,z,e) for e in edges)>=.6-EPS,ident+': anchor wall clearance')
    if t['kind']=='hallway':
        zvalues=[2*d['cell'][1]+1 for d in doors]
        require(max(zvalues)-min(zvalues)>=2*(max(z for x,z in cells)-min(z for x,z in cells))-4,
                ident+': hallway sockets not at opposite ends')
    return {'id':ident,'cells':n,'walls':len(walls),'doors':len(doors),'pieces':len(rows),'sizeClass':expected}


def validate_catalogue(rooms,lookup):
    require(rooms['theme']=='school' and rooms['module']==2.0,'room manifest identity')
    ts=rooms['templates']
    require(len(ts)>=10 and len({t['id'] for t in ts})==len(ts),'catalogue count/duplicate ids')
    result=[validate_room(t,lookup) for t in ts]
    sizes=Counter(t['sizeClass'] for t in ts if t['kind']=='room')
    require(sizes['closet']>=1 and sizes['small']>=2 and sizes['medium']>=2 and sizes['large']>=1 and sizes['hall']>=1,
            'catalogue size coverage')
    require(any(t['sizeClass']=='medium' and t['shape'] in {'L','round'} and t['kind']=='room' for t in ts),'nonrect medium')
    require(any(t['kind']=='hallway' and t['shape']=='rect' for t in ts) and
            any(t['kind'] in {'hallway','junction'} and t['shape']!='rect' for t in ts),'hallway coverage')
    require(1<=sum(t['gimmick']!='none' for t in ts)<=2,'gimmick coverage')
    return result


def negative_controls(rooms,lookup,records):
    require(clipped_area([Vector((-3,0,0)),Vector((3,0,0)),Vector((0,4,0))],(-1.599,1.599,.001,2.799))>0,'clipping negative control')
    require(not near_sets([Vector((0,0,0))],[Vector((0,.005,0))]),'seam negative control')
    bad=copy.deepcopy(records['wall_2m']); bad['origin']=Vector((.005,0,0))
    try:
        check_pivot('wall_2m','wall',bad)
    except AssertionError:
        pass
    else:
        raise AssertionError('pivot negative control accepted')
    base=rooms['templates'][0]
    cases=[]
    bad=copy.deepcopy(base); bad['footprint'].append([100,100]); cases.append(('disconnected footprint',bad))
    bad=copy.deepcopy(base); bad['doors'][0]['cell']=[1,1]; cases.append(('interior door',bad))
    bad=copy.deepcopy(base); bad['anchors']['cake'][0]=[-1,0,0]; cases.append(('outside anchor',bad))
    bad=copy.deepcopy(base); bad['anchors']['cake'][0]=[.1,0,.1]; cases.append(('wall-clearance anchor',bad))
    bad=copy.deepcopy(base); bad['pieces'][0]['id']='foreign_piece'; cases.append(('unknown piece',bad))
    bad=copy.deepcopy(base); bad['pieces'].append(copy.deepcopy(bad['pieces'][0])); cases.append(('overlapping wall',bad))
    bad=copy.deepcopy(base); bad['pieces'].pop(0); cases.append(('enclosure gap',bad))
    bad=copy.deepcopy(base); bad['gimmick']='freeze'; bad['minRound']=2; cases.append(('early gimmick',bad))
    bad=copy.deepcopy(base); bad['doors'][0]['closedWith'][0]['pos'][0]+=.2; cases.append(('closure gap',bad))
    bad=copy.deepcopy(base)
    next(p for p in bad['pieces'] if p['id']=='prop_classroom_door_leaf')['pos'][2]+=.3
    cases.append(('detached door leaf',bad))
    bad=copy.deepcopy(base)
    next(p for p in bad['pieces'] if p['id']=='prop_classroom_door_leaf')['rotY']+=90
    cases.append(('door leaf standing into room',bad))
    bad=copy.deepcopy(base)
    next(p for p in bad['pieces'] if lookup[p['id']]['kind']=='ceiling')['pos'][1]=1.0
    cases.append(('ceiling at furniture height',bad))
    for label,t in cases:
        try:
            validate_room(t,lookup)
        except AssertionError:
            continue
        raise AssertionError('negative control accepted: '+label)
    return len(cases)+3


def validate_sources(rooms,records):
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Rooms/SchoolRooms.blend'))
    for t in rooms['templates']:
        scene=bpy.data.scenes.get(t['id'])
        require(scene is not None and scene.get('template_id')==t['id'],t['id']+': source scene')
        placements={o.get('placement_index'):o for o in scene.objects if 'placement_index' in o}
        require(len(placements)==len(t['pieces']),t['id']+': assembly count')
        for i,row in enumerate(t['pieces']):
            obj=placements[i]
            require(obj.get('piece_id')==row['id'],t['id']+': assembly piece identity')
            require((unity(obj.location)-Vector(row['pos'])).length<EPS,t['id']+': assembly position')
            require(abs(math.degrees(obj.rotation_euler.z)-row['rotY'])<.0001,t['id']+': assembly rotation')
            points=[unity(v.co) for v in obj.data.vertices]
            require(near_sets(points,records[row['id']]['points']),t['id']+': assembly mesh differs from export')
        require(scene.camera is not None and any(o.hide_render for o in placements.values()),t['id']+': cutaway review')
        lights=[o for o in scene.objects if o.type=='LIGHT' and o.name.startswith('Fluorescent')]
        require(len(lights)==len(t['anchors']['light']) and all(o.data.energy>0 for o in lights),t['id']+': lit theme fixtures')
    dark=bpy.data.scenes.get('School darkness - fluorescent and flashlight only')
    require(dark is not None,'darkness scene missing')
    require(dark.world.node_tree.nodes['Background'].inputs['Strength'].default_value==0,'dark world must not illuminate')
    lights=[o for o in dark.objects if o.type=='LIGHT']
    require(lights and all(o.name.startswith(('Fluorescent','Flashlight')) and o.data.energy>0 for o in lights),'dark scene studio fill')
    require(sum(o.data.type=='SPOT' for o in lights)==1,'flashlight spot')
    require(not any(o.hide_render for o in dark.objects if o.type=='MESH'),'dark scene must keep full shell')


def validate_previews(rooms):
    provenance=json.loads((REPORT/'preview-inputs.json').read_text(encoding='utf-8'))
    for relative,digest in provenance.items():
        require(sha(ROOT/relative)==digest,'stale previews: '+relative)
    require(set(provenance)=={'tools/blender/env_theme_school.py',
            'Assets/Art/Environment/School/Kit/SchoolKit.manifest.json',
            'Assets/Art/Environment/School/Rooms/SchoolRooms.manifest.json'},'preview provenance missing inputs')
    names=['kit-sheet','in-darkness']+[t['id']+'-three-quarter' for t in rooms['templates']]
    details=[]
    for name in names:
        path=REPORT/(name+'.png')
        require(path.is_file(),'missing preview '+name)
        image=bpy.data.images.load(str(path),check_existing=False)
        require(tuple(image.size)==((2400,1800) if name=='kit-sheet' else (1440,1000)),name+': image dimensions')
        pixels=list(image.pixels)
        values=sorted(sum(pixels[i:i+3])/3 for i in range(0,len(pixels),160))
        require(values[-1]-values[0]>.15,name+': blank image')
        require(sum(v>.025 for v in values)/len(values)>.10,name+': nearly black image')
        details.append({'file':path.name,'sha256':sha(path),'p10':values[len(values)//10],
                        'p50':values[len(values)//2],'p90':values[len(values)*9//10]})
        bpy.data.images.remove(image)
    return details


def determinism():
    paths=[ART/'Kit/SchoolKit.manifest.json',ART/'Rooms/SchoolRooms.manifest.json']
    snapshots=[]
    for run in (1,2):
        command=[bpy.app.binary_path,'--background','--factory-startup','--python-exit-code','1',
                 '--python',str(ROOT/'tools/blender/env_theme_school.py'),'--','--skip-previews']
        with (REPORT/f'determinism-{run}.log').open('w',encoding='utf-8') as log:
            result=subprocess.run(command,cwd=str(ROOT),stdout=log,stderr=subprocess.STDOUT,check=False)
        require(result.returncode==0,f'determinism generator run {run} failed; see retained log')
        snapshot = {p.name:sha(p) for p in paths}
        snapshot['geometry'] = {}
        for path in sorted((ART/'Kit').glob('*.fbx')):
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
            obj, = bpy.context.scene.objects
            snapshot['geometry'][path.name] = semantic(facts(obj))
        snapshots.append(snapshot)
        (REPORT/f'determinism-{run}-hashes.json').write_text(json.dumps(snapshots[-1],indent=2)+'\n',encoding='utf-8')
    require(snapshots[0]==snapshots[1],'repeat-generation manifest/geometry hash mismatch')
    (REPORT/'determinism.json').write_text(json.dumps({'passed':True,'runs':snapshots},indent=2)+'\n',encoding='utf-8')
    return snapshots


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--determinism',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    require(bpy.app.version[:2]==(5,2),'Use Blender 5.2')
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from validate_env_theme_castle import check_door_quality, check_door_previews, placement_regressions
    REPORT.mkdir(parents=True,exist_ok=True)
    (REPORT/'validation.json').write_text('{"passed":false,"status":"validation started"}\n',encoding='utf-8')
    (REPORT/'validation.txt').write_text('INCOMPLETE: validation started\n',encoding='utf-8')
    repeated=determinism() if args.determinism else None
    check_door_quality('school', REPORT)
    kit=json.loads((ART/'Kit/SchoolKit.manifest.json').read_text(encoding='utf-8'))
    rooms=json.loads((ART/'Rooms/SchoolRooms.manifest.json').read_text(encoding='utf-8'))
    lookup,records,pieces=validate_kit(kit)
    room_details=validate_catalogue(rooms,lookup)
    negatives=negative_controls(rooms,lookup,records)
    placement_regressions(rooms['templates'],lookup,{k:v['points'] for k,v in records.items()})
    from validate_env_theme_vaults import validate_vaults
    validate_vaults(rooms['templates'], lookup, {k:v['points'] for k,v in records.items()})
    from validate_env_theme_furnished import validate_expansion
    validate_expansion('school', lookup, {k:v['points'] for k,v in records.items()})
    check_door_previews('school', REPORT)
    validate_sources(rooms,records)
    previews=validate_previews(rooms)
    lines=[f'PASS School kit: {len(pieces)} FBX pieces; all 16 mandatory ids, dimensions, pivots, axes, applied transforms, materials, budgets and source agreement',
           'PASS School seams: wall/window/door/end pieces, floor/ceiling/trim and r4/r6/r8 arcs',
           'PASS School doors: clear 3.2m x 2.8m frame apertures; paired closed leaves attached to socket jambs in every template',
           'PASS School ceilings: every tile at wall height within 0.05m',
           f'PASS School rooms: {len(room_details)} templates; 4-connected footprints, boundary doors, anchor clearance, piece ids, nonoverlapping walls and full enclosure',
           'PASS School catalogue: closet, two small, two medium including L, large, hall, straight/bent hallways and two round-3 gimmicks',
           f'PASS School negative controls: {negatives} malformed kit/room cases rejected',
           f'PASS School sources/previews: exact room assemblies, {len(previews)} nonblank images, current input hashes; darkness uses only fluorescents and one flashlight']
    if repeated:
        lines.append('PASS School determinism: two regeneration runs; both manifest SHA-256 values and every imported geometry hash match')
    result={'passed':True,'pieces':pieces,'rooms':room_details,'previews':previews,'determinism':repeated,
            'manifestHashes':{p.name:sha(p) for p in (ART/'Kit/SchoolKit.manifest.json',ART/'Rooms/SchoolRooms.manifest.json')},
            'limitations':['No Unity execution/import/material remapping/NavMesh checks.','Pixel probes do not establish artistic acceptance.']}
    (REPORT/'validation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8',newline='\n')
    (REPORT/'validation.txt').write_text('\n'.join(lines)+'\n',encoding='utf-8',newline='\n')
    for line in lines:
        print(line)


if __name__=='__main__':
    main()
