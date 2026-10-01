# ============================================================================
# hunter_weaver.py
# ============================================================================
# PURPOSE:
#   Authors the wide, eight-limbed Weaver instead of reusing the legacy werewolf.
#   A flattened carapace and pale joints disclose its overhead skitter without
#   losing the eight-legged outline. Eyes and mandibles frame the warned release.
# ARCHITECTURAL ROLE:
#   Offline art generator · no runtime layer · Hunter, SPEC-005 §2.3 / PLAN-015.
# KEY RESPONSIBILITIES:
#   - Define shaped shell plates, segmented limbs, mandibles and clustered eyes.
#   - Author in-place skitter, warning, drop/rear attack and recoil actions.
#   - Produce source, FBX, manifest and Workbench previews via the shared pipeline.
# DEPENDENCIES:
#   Blender 5.2, mathutils and hunter_creature_common; no Unity or vendor art.
# USAGE NOTES:
#   Blender --background --factory-startup --python-exit-code 1 --python this-file.
#   Dimensions and motion amplitudes below are provisional art values, not rules.
# ============================================================================
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import hunter_creature_common as c
from hunter_motion_common import spider_leg
from hunter_detail_geometry import SculptCreature, weaver_details

PALETTE = {"Carapace": (.30, .22, .16), "Ridge": (.46, .35, .23),
           "Limbs": (.17, .14, .12), "JointTips": (.76, .70, .53)}


def build():
    definitions = [("Body", "Root", (0, 0, .9))]
    limbs = []
    for side, sign in (("L", -1), ("R", 1)):
        for i, y in enumerate((-.34, -.12, .12, .34)):
            key = side + str(i)
            fan = i / 1.5 - 1
            reach_y = y + fan * .23
            p = [(sign * .27, y, .89), (sign * .66, reach_y, 1.03),
                 (sign * .98, reach_y + fan * .12, .50),
                 (sign * .89, reach_y + fan * .20, .035)]
            for j in range(3):
                definitions.append((key + "Segment" + str(j), "Body" if j == 0 else key + "Segment" + str(j - 1), p[j]))
            limbs.append((key, p))
    m = SculptCreature("Weaver", PALETTE, definitions)
    m.shape("Carapace", "Body", (0, .02, .9), (.84, 1.02, .25), "Carapace", "ico")
    m.shape("DorsalRidge", "Body", (0, .10, .998), (.25, .77, .075), "Ridge", "ico")
    m.shape("Underside", "Body", (0, .02, .805), (.61, .81, .10), "Limbs", "ico")
    m.shape("ForwardSpinner", "Body", (0, -.49, .84), (.29, .20, .135), "JointTips", "ico")
    # Rear fan and lateral shell plates keep the outline readable from below.
    for sign in (-1, 1):
        m.shape("RearPlate" + str(sign), "Body", (sign * .19, .40, .94), (.30, .33, .11), "Ridge", "ico")
        m.bar("FrontProng" + str(sign), "Body", (sign * .09, -.49, .84), (sign * .18, -.64, .79), .055, "Limbs")
    for key, p in limbs:
        for j in range(3):
            bone = key + "Segment" + str(j)
            m.bar(key + "Link" + str(j), bone, p[j], p[j + 1], (.09, .063, .036)[j], "Limbs")
            if j < 2:
                m.shape(key + "Joint" + str(j), bone, p[j], (.12, .12, .105), "JointTips", "ico")
        m.shape(key + "FootTip", key + "Segment2", p[3], (.085, .105, .07), "JointTips")
    weaver_details(m)
    return m


def motion(rig, name, t):
    wave = math.sin(2 * math.pi * t)
    if name in {"walk", "run"}:
        sprint = name == 'run'
        stride, lift = (.31, .25) if sprint else (.21, .14)
        c.delta(rig, 'Body', (.08 if sprint else 0, .04*wave, 0),
                (0, 0, -.22+.025*(1-math.cos(4*math.pi*t))))
        import bpy
        bpy.context.view_layer.update()
        for side, sign in (("L", -1), ("R", 1)):
            for i, y in enumerate((-.34, -.12, .12, .34)):
                key = side + str(i)
                phase = (t + .5*((i+(sign>0))%2)) % 1
                travel = stride*(-1+4*phase) if phase<.5 else stride*(3-4*phase)
                rise = lift*max(0,-math.sin(2*math.pi*phase))
                tip = (sign*.89, y+(i/1.5-1)*.43, .035)
                target = [tip[0], tip[1]+travel, .06+rise]
                yaw = sign*(.46 if sprint else .30)*math.cos(2*math.pi*phase)
                # Compensate the rigid cube's tilted sole, not merely its centre.
                for _ in range(4):
                    spider_leg(rig,key,tip,target,yaw)
                    row = rig.pose.bones[key+'Segment2'].matrix.to_3x3() @ rig.data.bones[key+'Segment2'].matrix_local.to_3x3().inverted()
                    target[2] = sum(abs(row[2][j])*size for j,size in enumerate((.0425,.0525,.035))) + rise + .002
    elif name == "idle":
        c.delta(rig, "Body", (.08 * wave, .05*wave, 0))
    elif name == "ready":
        lift = .95 * t
        for side, sign in (("L", -1), ("R", 1)):
            c.delta(rig, side + "0Segment0", (-lift, sign * .14 * t, 0))
            c.delta(rig, side + "0Segment1", (-.45 * t, 0, 0))
        c.delta(rig, "Body", (-.18 * t, 0, 0))
    elif name == "attack":
        drop = c.envelope(t, [(0, 0), (.23, -.20), (.4, -.15), (.67, .10), (1, 0)])
        rear = c.envelope(t, [(0, -.18), (.23, .18), (.4, -.38), (.67, -.60), (1, 0)])
        c.delta(rig, "Body", (rear, 0, 0), (0, 0, drop))
        for side in ("L", "R"):
            c.delta(rig, side + "0Segment0", (c.envelope(t, [(0, -.95), (.4, .35), (.67, -1.15), (1, 0)]), 0, 0))
    elif name == "hit":
        recoil = math.sin(math.pi * t)
        c.delta(rig, "Body", (.30 * recoil, .28 * recoil, 0), (0, .10 * recoil, -.10 * recoil))
    if name not in {'walk','run'}:
        import bpy
        bpy.context.view_layer.update()
        for side, sign in (('L',-1),('R',1)):
            for i,y in enumerate((-.34,-.12,.12,.34)):
                key = side+str(i)
                tip = (sign*.89,y+(i/1.5-1)*.43,.035)
                lift, reach = 0, 0
                if i==0 and name=='ready':
                    lift, reach = .60*t, -.28*t
                elif i==0 and name=='attack':
                    lift = c.envelope(t,[(0,.60),(.23,.12),(.4,.06),(.67,.75),(1,0)])
                    reach = c.envelope(t,[(0,-.28),(.23,0),(.4,-.48),(.67,-.20),(1,0)])
                target = [tip[0],tip[1]+reach,.06+lift]
                for _ in range(4):
                    spider_leg(rig,key,tip,target,0)
                    rotation = rig.pose.bones[key+'Segment2'].matrix.to_3x3() @ rig.data.bones[key+'Segment2'].matrix_local.to_3x3().inverted()
                    target[2] = sum(abs(rotation[2][j])*size for j,size in enumerate((.0425,.0525,.035)))+lift+.002


def main():
    c.reset()
    m = build()
    clips = c.animate(m, motion)
    manifest = c.save_model(m, clips, {"attack_origin": c.socket(m.rig, "Body", (0, -.60, .84)),
                                      "head_or_top": c.socket(m.rig, "Body", (0, .10, 1.035))})
    c.previews(m, clips, manifest)


if __name__ == "__main__":
    main()
