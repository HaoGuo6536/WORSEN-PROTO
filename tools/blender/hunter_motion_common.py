# ============================================================================
# hunter_motion_common.py
# PURPOSE:
#   Author grounded, in-place motion for the custom hunter skeletons. Analytic
#   leg placement gives the baked clips real stance and swing phases without
#   adding runtime IK, root motion, constraints or renamed socket bones.
# ARCHITECTURAL ROLE:
#   Offline art utility · no runtime layer · Hunter / PLAN-017.
# KEY RESPONSIBILITIES:
#   - Apply rest-basis rotations and translations in source metres.
#   - Solve biped stance/swing cycles with level soles and counter-swing.
#   - Solve articulated creature limbs against explicit foot targets.
# DEPENDENCIES:
#   Blender 5.2 bpy/mathutils and Python math; no Unity systems.
# USAGE NOTES:
#   All angles are degrees at the public pose boundary. Phase is injected.
#   Authoring amplitudes are provisional; validators measure exported results.
# ============================================================================
import math
import bpy
from mathutils import Euler, Matrix, Vector


def pose_delta(rig, name, angles=(0, 0, 0), offset=(0, 0, 0)):
    bone = rig.pose.bones[name]
    basis = bone.bone.matrix_local.to_3x3()
    rotation = Euler(tuple(math.radians(v) for v in angles)).to_matrix()
    bone.rotation_quaternion = (basis.inverted() @ rotation @ basis).to_quaternion()
    bone.location = basis.inverted() @ Vector(offset)


def step_phase(t, hold=.65, steps=8):
    phase = (t % 1) * steps
    cell = math.floor(phase)
    fraction = max(0, (phase - cell - hold) / (1 - hold))
    return (cell + fraction) / steps


def biped_gait(rig, phase, sprint=False, stride_scale=1, arm_scale=1, stiff=False):
    # Half the cycle is a level planted sole moving rearward relative to the
    # motor; the other half returns forward with positive ground clearance.
    h = rig.data.bones['Hips'].head_local.z
    stride = h * (.43 if sprint else .30) * stride_scale
    crouch = h * (.17 if sprint else .11)
    bob = h * (.035 if sprint else .018) * (1 - math.cos(4 * math.pi * phase))
    pose_delta(rig, 'Hips', offset=(0, 0, -crouch + bob))
    for side, shift in (('Left', 0), ('Right', .5)):
        p = (phase + shift) % 1
        y = stride * (-1 + 4*p) if p < .5 else stride * (3 - 4*p)
        lift = h * (.26 if sprint else .15) * max(0, -math.sin(2*math.pi*p))
        upper = rig.data.bones[side+'Thigh'].head_local.z - rig.data.bones[side+'Shin'].head_local.z
        lower = rig.data.bones[side+'Shin'].head_local.z - rig.data.bones[side+'Foot'].head_local.z
        down = upper + lower - crouch + bob - lift
        distance = math.hypot(y, down)
        assert distance < upper + lower, 'Gait target beyond leg reach'
        knee = math.acos(max(-1, min(1, (distance*distance-upper*upper-lower*lower)/(2*upper*lower))))
        thigh = math.atan2(y, down) - math.atan2(lower*math.sin(knee), upper+lower*math.cos(knee))
        pose_delta(rig, side+'Thigh', (math.degrees(thigh), 0, 0))
        pose_delta(rig, side+'Shin', (math.degrees(knee), 0, 0))
        pose_delta(rig, side+'Foot', (-math.degrees(thigh+knee), 0, 0))
        swing = -(.95 if sprint else .7) * math.degrees(math.atan2(y, down)) * arm_scale
        pose_delta(rig, side+'Arm', (swing, 0, 0))
        pose_delta(rig, side+'Forearm', (-(8 if stiff else 24 if sprint else 12), 0, 0))


def aim_bone(rig, name, head, rest_vector, posed_vector):
    bone = rig.pose.bones[name]
    rotation = Vector(rest_vector).rotation_difference(Vector(posed_vector)).to_matrix()
    matrix = (rotation @ bone.bone.matrix_local.to_3x3()).to_4x4()
    matrix.translation = head
    bone.matrix = matrix
    bpy.context.view_layer.update()


def spider_leg(rig, key, tip, target, yaw):
    # The proximal link sweeps; the two distal links solve onto the explicit
    # tip trajectory. No constraints survive baking and stance tips stay down.
    names = [key+'Segment'+str(j) for j in range(3)]
    rest = [rig.data.bones[n].head_local.copy() for n in names] + [Vector(tip)]
    parent = rig.pose.bones['Body'].matrix @ rig.data.bones['Body'].matrix_local.inverted()
    hip = parent @ rest[0]
    v0 = Euler((0, 0, yaw)).to_matrix() @ (rest[1]-rest[0])
    end = Vector(target)
    a, b = (rest[2]-rest[1]).length, (rest[3]-rest[2]).length
    # Pitch the proximal joint toward far targets before the distal solve.
    # This retains all segment lengths during planted-body drops and rears.
    if (end-hip-v0).length > a+b-.025:
        toward = (end-hip).normalized()*v0.length
        low, high = 0., 1.
        for _ in range(20):
            blend = (low+high)/2
            candidate = v0.lerp(toward,blend).normalized()*v0.length
            if (end-hip-candidate).length > a+b-.025:
                low = blend
            else:
                high = blend
        v0 = v0.lerp(toward,high).normalized()*v0.length
    knee = hip + v0
    aim_bone(rig, names[0], hip, rest[1]-rest[0], v0)
    direction = end-knee
    distance = direction.length
    axis = direction.normalized()
    assert abs(a-b) < distance < a+b, f'Spider {key} target beyond limb reach: {distance:.4f} not in ({abs(a-b):.4f}, {a+b:.4f})'
    along = (a*a-b*b+distance*distance)/(2*distance)
    pole = Vector((1 if tip[0] > 0 else -1, 0, .3))
    pole = (pole-axis*pole.dot(axis)).normalized()
    ankle = knee + axis*along + pole*math.sqrt(max(0, a*a-along*along))
    aim_bone(rig, names[1], knee, rest[2]-rest[1], ankle-knee)
    aim_bone(rig, names[2], ankle, rest[3]-rest[2], end-ankle)
