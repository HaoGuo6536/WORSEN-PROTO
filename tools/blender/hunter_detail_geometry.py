# ============================================================================
# hunter_detail_geometry.py
# PURPOSE:
#   Shape the seven authored hunter skins without changing their accepted rigs.
#   Faceted lofts, inset bevels and swept details replace construction primitives
#   while keeping every appendage attached to its original animation bone.
# ARCHITECTURAL ROLE:
#   Offline art geometry utility · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Build bounded bevels, tapered anatomical links and folded cloth lofts.
#   - Adapt the two existing rigid-skin builders without changing skeletons.
#   - Author silhouette-specific hardware, facial and organic secondary detail.
# DEPENDENCIES:
#   Blender 5.2 bpy/mathutils; hunter_humanoid_common and hunter_creature_common.
# USAGE NOTES:
#   All metre dimensions and tessellation values are provisional art authoring.
#   Rigid weights are deliberate for joint hardware and layered cutout cloth.
#   No Unity calls, downloads, external art or source skeleton changes.
# ============================================================================
import math
import bpy
from mathutils import Vector
from hunter_humanoid_common import Body
from hunter_creature_common import Creature, material


def mesh_object(name, vertices, faces):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return obj


def bevel(obj, amount, segments=2):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mod = obj.modifiers.new('CarvedEdges', 'BEVEL')
    mod.width, mod.segments = amount, segments
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def tube(name, path, radii, sides=8):
    vertices, faces = [], []
    path = [Vector(p) for p in path]
    for j, p in enumerate(path):
        tangent = path[min(j+1,len(path)-1)]-path[max(0,j-1)]
        basis = tangent.to_track_quat('Z','Y')
        for i in range(sides):
            a = i*math.tau/sides
            vertices.append(p + basis @ Vector((radii[j]*math.cos(a),radii[j]*math.sin(a),0)))
    faces.append(tuple(reversed(range(sides))))
    for j in range(len(path)-1):
        for i in range(sides):
            a, b = j*sides+i, j*sides+(i+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.append(tuple(range(len(vertices)-sides,len(vertices))))
    return mesh_object(name, vertices, faces)


class SculptBody(Body):
    def box(self, bone, center, size, material=0):
        if bone.endswith('Hand'):
            return self.egg(bone,center,size,material)
        bpy.ops.mesh.primitive_cube_add(size=1, location=center)
        obj = bpy.context.object
        obj.scale = size
        bevel(obj, min(size)*.18)
        if bone.endswith('Foot'):
            for vertex in obj.data.vertices:
                front = max(0,-vertex.co.y/(size[1]*.5))
                vertex.co.x *= 1-.18*front
                if vertex.co.z>0:
                    vertex.co.z *= 1-.45*front
        return self.part(obj,bone,material)

    def link(self, bone, start, end, radius, material=0, radius_end=None):
        a, b = Vector(start), Vector(end)
        end_radius = radius if radius_end is None else radius_end
        obj = tube('TaperedForm', [a,a.lerp(b,.14),a.lerp(b,.5),a.lerp(b,.86),b],
                   [radius*.65,radius,radius*.94,end_radius,end_radius*.65])
        return self.part(obj,bone,material)

    def sweep(self,bone,path,radii,material=0,sides=8):
        return self.part(tube('SculptedDetail',path,radii,sides),bone,material)

    def cloth(self,bone,rings,material=0,segments=24):
        # Intermediate rings let the pleats taper into shoulders rather than
        # reading as straight cones; the top and bottom authoring bounds stay put.
        dense = []
        for a,b in zip(rings,rings[1:]):
            for j in range(4):
                t = j/4
                dense.append(tuple(x+(y-x)*t for x,y in zip(a,b)))
        dense.append(rings[-1])
        vertices, faces = [], []
        for j,(cx,cy,z,rx,ry) in enumerate(dense):
            for i in range(segments):
                a = i*math.tau/segments
                pleat = 1 + .065*math.cos(a*12+.18*math.sin(j*.7))
                hem = .035*(.5+.5*math.sin(i*2.3)) if j==0 and z<1.6 else 0
                vertices.append((cx+rx*math.cos(a)*pleat,cy+ry*math.sin(a)*pleat,z+hem))
        faces.append(tuple(reversed(range(segments))))
        for j in range(len(dense)-1):
            for i in range(segments):
                a,b = j*segments+i,j*segments+(i+1)%segments
                faces.append((a,b,b+segments,a+segments))
        faces.append(tuple(range(len(vertices)-segments,len(vertices))))
        return self.part(mesh_object('PleatedCloth',vertices,faces),bone,material)


class SculptCreature(Creature):
    def shape(self,part,bone,center,size,mat,kind='box',rotation=(0,0,0)):
        if kind=='ico':
            bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,radius=1)
        elif kind=='box':
            bpy.ops.mesh.primitive_cube_add(size=1)
        elif kind=='cylinder':
            bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=1,depth=1)
        elif kind=='tooth':
            bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=1,radius2=0,depth=1)
        else:
            raise ValueError(kind)
        obj = bpy.context.object
        obj.dimensions = size
        if kind in {'box','cylinder'}:
            bevel(obj,min(size)*.16,1)
        obj.location, obj.rotation_euler = center,rotation
        obj.data.materials.append(self.materials[mat])
        return self.bind(obj,bone,part)

    def sweep(self,part,bone,path,radii,mat,sides=8):
        obj = tube(part,path,radii,sides)
        obj.data.materials.append(self.materials[mat])
        return self.bind(obj,bone,part)

    def bar(self,part,bone,start,end,width,mat):
        a,b = Vector(start),Vector(end)
        return self.sweep(part,bone,[a,a.lerp(b,.18),a.lerp(b,.65),b],
                          [width*.34,width*.57,width*.46,width*.28],mat)


def humanoid_details(body):
    heads = {n:Vector(p) for n,_,p in body.bones}
    # Cover the construction gaps with actual joint volumes; overlaps remain
    # connected when the child rotates. These are not floating limb segments.
    for side in ('Left','Right'):
        for suffix in ('Forearm','Hand','Shin','Foot'):
            if body.name=='Mannequin' and suffix in ('Forearm','Shin'):
                continue
            mat = 2 if body.name in {'Mannequin','Herald'} else 0
            p = heads[side+suffix]
            body.egg(side+suffix,p,(.085,.09,.105),mat)
    if body.name in {'Mannequin','Herald','Stare'}:
        # Separate tapered fingers retain Hand ownership, including the sweep.
        for side,sign in (('Left',1),('Right',-1)):
            p = heads[side+'Hand']
            for i in range(4):
                x = p.x+(i-1.5)*.023
                body.sweep(side+'Hand',[(x,-.035,p.z-.105),(x+sign*.008,-.065,p.z-.16),
                                       (x+sign*.012,-.088,p.z-.19+(i%2)*.015)], [.012,.010,.003],0,6)
    if body.name=='Echo':
        # Split hanging strips and staggered horizontal recording scars.
        for i in range(9):
            a = math.tau*i/9
            x,y = .21*math.cos(a),.15*math.sin(a)
            body.sweep('Hips',[(x*.85,y*.85,.66),(x,y,.32),(x*1.03,y*1.03,.20+.018*(i%3))],
                       [.024,.021,.003],2,6)
        for i in range(5):
            z = 1.12+i*.065
            body.sweep('Chest',[(-.14,-.12,z),(-.02,-.157,z+.006),(.13,-.125,z-.008)],
                       [.004,.006,.003],2,6)
        for sign in (-1,1):
            body.sweep('Head',[(sign*.045,-.07,1.78),(sign*.137,-.105,1.71),
                               (sign*.123,-.14,1.58),(sign*.075,-.105,1.49)], [.011]*4,2)
    elif body.name=='Mannequin':
        for side in ('Left','Right'):
            for suffix in ('Arm','Forearm','Thigh','Shin','Hand'):
                p = heads[side+suffix]
                body.link(side+suffix,p+Vector((0,-.068,0)),p+Vector((0,-.082,0)),.026,2)
                body.box(side+suffix,p+Vector((0,-.084,0)),(.032,.004,.005),0)
        # Segmented abdomen and split shell seam, no expressive facial features.
        for z in (1.04,1.10,1.16):
            body.egg('Chest',(0,0,z),(.23,.20,.08),2)
        body.sweep('Chest',[(0,-.125,1.10),(0,-.14,1.26),(0,-.11,1.42)],[.004]*3,2,6)
    elif body.name=='Stare':
        # Pale stretched mask, hollow orbits and small fixed forward pupils.
        body.egg('Head',(.254,-.099,2.104),(.164,.10,.32),2)
        for sign in (-1,1):
            body.egg('Head',(.254+sign*.045,-.141,2.168),(.058,.014,.045),0)
            body.egg('Head',(.254+sign*.045,-.149,2.168),(.016,.007,.015),3)
        body.sweep('Head',[(.254,-.15,2.15),(.252,-.175,2.09),(.249,-.157,2.063)], [.012,.016,.005],2)
        body.egg('Head',(.246,-.145,2.011),(.025,.013,.071),0)
        for i in range(6):
            x=.07+(i-2.5)*.036
            body.sweep('Chest',[(x*.8,-.095,1.2),(x+.025,-.13,1.5),(x+.07,-.10,1.79)], [.009,.008,.003],1,6)
    elif body.name=='Herald':
        # An open, elongated jaw frame and throat; Head bone contract is retained.
        body.egg('Head',(0,-.109,2.30),(.115,.025,.19),1)
        body.sweep('Head',[(-.074,-.08,2.38),(-.082,-.122,2.26),(0,-.132,2.20),
                           (.082,-.122,2.26),(.074,-.08,2.38)], [.019,.021,.025,.021,.019],2)
        for i in range(7):
            z=1.37+i*.095
            body.egg('Chest',(0,.075,z),(.095,.095,.055),2)
        for sign in (-1,1):
            body.sweep('Head',[(sign*.041,-.03,2.03),(sign*.059,-.05,2.17),(sign*.07,-.07,2.26)], [.015]*3,2)
            for i in range(4):
                x=sign*(.02+i*.014)
                body.sweep('Head',[(x,-.125,2.37),(x,-.13,2.34)],[.008,.002],2,6)


def weaver_details(m):
    for sign in (-1,1):
        for i in range(3):
            m.shape('Eye%d_%d'%(sign,i),'Body',(sign*(.06+i*.075),-.445+i*.023,.949-i*.015),
                    (.061,.05,.05),'JointTips','ico')
        m.sweep('Mandible'+str(sign),'Body',[(sign*.13,-.48,.86),(sign*.22,-.61,.79),
                (sign*.17,-.73,.76),(sign*.065,-.72,.80)],[.051,.04,.025,.003],'Limbs')
        for i in range(5):
            y=-.2+i*.13
            m.sweep('ShellFlute%d_%d'%(sign,i),'Body',[(sign*.05,y,1.02),(sign*.26,y+.05,.998),
                    (sign*.38,y+.07,.93)],[.018,.024,.009],'Ridge')
    for side in ('L','R'):
        for i in range(4):
            bone=side+str(i)+'Segment1'
            p=m.rig.data.bones[bone].head_local
            m.sweep('KneeSpur'+bone,bone,[p,p+Vector((0,.03,.08)),p+Vector((0,.055,.12))],
                    [.028,.022,.002],'Ridge',6)


def ticking_details(m):
    # Open toothed wheels on side panels, not painted circles.
    for side in (-1,1):
        for j in range(2):
            center=Vector((side*.312,.01,.64+j*.25))
            path=[center+Vector((0,math.cos(i*math.tau/24)*.087,math.sin(i*math.tau/24)*.087)) for i in range(25)]
            m.sweep('GearRing%d_%d'%(side,j),'Case',path,[.012]*25,'Brass',6)
            for i in range(12):
                a=i*math.tau/12
                radial=Vector((0,math.cos(a),math.sin(a)))
                m.sweep('GearTooth%d_%d_%d'%(side,j,i),'Case',[center+radial*.075,center+radial*.111],
                        [.015,.011],'Brass',4)
            for i in range(3):
                a=i*math.tau/3
                m.sweep('GearSpoke%d_%d_%d'%(side,j,i),'Case',[center,center+Vector((0,math.cos(a)*.08,math.sin(a)*.08))],
                        [.009,.009],'Brass',4)
    for x in (-.25,.25):
        for z in (.46,.70,1.045):
            m.shape('CaseRivet','Case',(x,-.227,z),(.027,.015,.027),'Brass','ico')
    for side,x,z in (('Long',.62,.25),('Short',-.40,.61)):
        for i in range(3):
            m.sweep(side+'Finger'+str(i),side+'Forearm',[(x+(i-1)*.05,-.14,z),
                     (x+(i-1)*.06,-.19,z-.06),(x+(i-1)*.05,-.22,z-.04)],[.018,.013,.005],'Brass',6)


def mimic_details(m):
    # A piped cream border and tiny jam leak are the owner-approved disguise tell.
    # Keep the original cake triangles/UVs intact; all additions use distinct names.
    m.materials['Frosting'] = material('M_HunterMimic_Frosting',(.87,.81,.67))
    for side in (-1,1):
        for i in range(10):
            y=-.113+i*.024
            x=side*(y+.168)*.34
            m.shape('Frosting_%d_%d'%(side,i),'Jaw',(x,y,.225),(.016,.021,.015),'Frosting','ico')
        # The measured sponge side at y=.02 is x=+/-.07870057. Keep the
        # drip just proud of that plane rather than hidden inside the sponge.
        m.sweep('JamTell'+str(side),'Root',[(side*.0809,.02,.133),(side*.081,.019,.118),
                (side*.0815,.021,.103)],[.004,.003,.001],'Mouth',6)
