# ============================================================================
# hunter_ticking.py
# ============================================================================
# PURPOSE:
#   Builds a compact asymmetric walking clock with a rear-pocket winding key.
#   Its round dial has clock hands, not eyes; the unequal arms, crooked supports
#   and continuously swinging pendulum carry its identity without facial cues.
# ARCHITECTURAL ROLE:
#   Offline art generator · no runtime layer · Hunter, SPEC-005 §2.4 / PLAN-015.
# KEY RESPONSIBILITIES:
#   - Author original wooden case, brass clockwork and asymmetric rigid rig.
#   - Bake waddle, arm-swing attack and pendulum motion in every action.
#   - Export the editable source, FBX, manifest and neutral preview evidence.
# DEPENDENCIES:
#   Blender 5.2 and hunter_creature_common; no external assets or game systems.
# USAGE NOTES:
#   Run headlessly with factory startup and python-exit-code 1 in this worktree.
#   Palette, geometry and motion values below are provisional presentation data.
# ============================================================================
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import hunter_creature_common as c


PALETTE = {"Wood": (.24, .13, .075), "WoodEdge": (.38, .22, .11),
           "Brass": (.70, .51, .23), "Dial": (.88, .82, .64), "Dark": (.07, .055, .045)}


def build():
    defs = [("Case", "Root", (0, 0, .63)), ("Pendulum", "Case", (-.055, -.253, .73)),
            ("LongArm", "Case", (.30, 0, .96)), ("LongForearm", "LongArm", (.49, -.03, .66)),
            ("ShortArm", "Case", (-.30, 0, .90)), ("ShortForearm", "ShortArm", (-.44, -.02, .73)),
            ("LeftLeg", "Root", (-.19, 0, .44)), ("LeftShin", "LeftLeg", (-.26, .025, .22)),
            ("RightLeg", "Root", (.17, 0, .44)), ("RightShin", "RightLeg", (.24, -.045, .24)),
            ("Key", "Case", (.08, .24, .80))]
    m = c.Creature("Ticking", PALETTE, defs)
    m.shape("WoodenCase", "Case", (0, .025, .76), (.60, .38, .70), "Wood")
    m.shape("CrookedCrown", "Case", (-.065, .025, 1.15), (.62, .41, .10), "WoodEdge", "ico")
    m.shape("LeftRail", "Case", (-.277, -.185, .74), (.075, .075, .69), "WoodEdge")
    m.shape("RightRail", "Case", (.277, -.185, .78), (.075, .075, .65), "WoodEdge")
    m.shape("BaseTrim", "Case", (.025, -.015, .42), (.66, .45, .08), "Brass")
    m.shape("PendulumRecess", "Case", (-.055, -.172, .54), (.32, .032, .25), "Dark")
    m.shape("DialRim", "Case", (-.025, -.205, .90), (.51, .51, .045), "Brass", "cylinder", (math.pi / 2, 0, 0))
    m.shape("ClockDial", "Case", (-.025, -.233, .90), (.445, .445, .016), "Dial", "cylinder", (math.pi / 2, 0, 0))
    for i in range(12):
        a = 2 * math.pi * i / 12
        m.shape("HourMark%02d" % i, "Case", (-.025 + .192 * math.sin(a), -.245, .90 + .192 * math.cos(a)),
                (.015, .008, .040 if i % 3 == 0 else .023), "Dark", rotation=(0, a, 0))
    m.bar("MinuteHand", "Case", (-.025, -.255, .90), (-.025, -.255, 1.064), .019, "Dark")
    m.bar("HourHand", "Case", (-.025, -.260, .90), (.073, -.260, .863), .023, "Dark")
    m.shape("DialAxle", "Case", (-.025, -.267, .90), (.035, .035, .018), "Brass", "cylinder", (math.pi / 2, 0, 0))
    m.bar("PendulumRod", "Pendulum", (-.055, -.253, .73), (-.055, -.253, .46), .019, "Brass")
    m.shape("PendulumBob", "Pendulum", (-.055, -.253, .46), (.13, .13, .040), "Brass", "cylinder", (math.pi / 2, 0, 0))
    for name, a, b, width in (("LongArm", (.30, 0, .96), (.49, -.03, .66), .12),
                              ("LongForearm", (.49, -.03, .66), (.62, -.10, .29), .105),
                              ("ShortArm", (-.30, 0, .90), (-.44, -.02, .73), .105),
                              ("ShortForearm", (-.44, -.02, .73), (-.40, -.12, .62), .09),
                              ("LeftLeg", (-.19, 0, .44), (-.26, .025, .22), .115),
                              ("LeftShin", (-.26, .025, .22), (-.21, -.065, .07), .095),
                              ("RightLeg", (.17, 0, .44), (.24, -.045, .24), .115),
                              ("RightShin", (.24, -.045, .24), (.18, -.065, .07), .095)):
        m.bar(name + "Wood", name, a, b, width, "WoodEdge")
        m.shape(name + "Hinge", name, a, (.12, .12, .12), "Brass", "ico")
    m.shape("LongHand", "LongForearm", (.62, -.10, .25), (.16, .16, .16), "Wood")
    m.shape("ShortHand", "ShortForearm", (-.40, -.12, .61), (.13, .14, .12), "Wood")
    for side, x in (("Left", -.21), ("Right", .18)):
        m.shape(side + "Shoe", side + "Shin", (x, -.09, .05), (.18, .28, .10), "Dark")
    m.shape("RearPocket", "Case", (.08, .255, .70), (.31, .16, .20), "WoodEdge")
    m.shape("RearPocketInset", "Case", (.08, .340, .76), (.245, .015, .065), "Dark")
    m.bar("KeyShaft", "Key", (.08, .245, .80), (.08, .44, .80), .04, "Brass")
    for sign in (-1, 1):
        x = .08 + sign * .09
        for j, (a, b) in enumerate((((x - .055, .44, .80), (x - .055, .44, .96)),
                                     ((x + .055, .44, .80), (x + .055, .44, .96)),
                                     ((x - .055, .44, .96), (x + .055, .44, .96)),
                                     ((x - .055, .44, .80), (x + .055, .44, .80)))):
            m.bar("KeyLoop%d_%d" % (sign, j), "Key", a, b, .025, "Brass")
    return m


def motion(rig, name, t):
    wave = math.sin(2 * math.pi * t)
    c.delta(rig, "Pendulum", (0, .30 * wave, 0))
    if name in {"walk", "run"}:
        stride = .50 if name == "walk" else .78
        c.delta(rig, "Case", (.12 if name=='run' else .04, .14 * wave, 0), (0, 0, .025 * (1 - math.cos(4 * math.pi * t))))
        for side, sign in (("Left", -1), ("Right", 1)):
            c.delta(rig, side + "Leg", (sign * stride * wave, 0, 0))
            c.delta(rig, side + "Shin", (.30 * max(0, -sign * wave), 0, 0))
            import bpy
            from mathutils import Vector
            bpy.context.view_layer.update()
            bone = rig.pose.bones[side+'Shin']
            matrix = bone.matrix @ bone.bone.matrix_local.inverted()
            x = -.21 if side=='Left' else .18
            floor = min((matrix @ Vector((x+dx,-.09+dy,.05+dz))).z
                        for dx in (-.09,.09) for dy in (-.14,.14) for dz in (-.05,.05))
            lift = (.14 if name=='run' else .08)*max(0,sign*math.cos(2*math.pi*t))
            c.delta(rig,side+'Leg',(sign*stride*wave,0,0),(0,0,lift-floor))
        c.delta(rig, "LongArm", (stride * wave, 0, .08 * wave))
        c.delta(rig, "ShortArm", (-stride * wave, 0, 0))
        c.delta(rig, "Key", (0, .07 * wave, 0))
    elif name == "idle":
        c.delta(rig, "Case", (0, .055*wave, 0))
        c.delta(rig, "Key", (0, .04 * wave, 0))
    elif name == "ready":
        c.delta(rig, "LongArm", (.45 * t, -.65 * t, -.25 * t))
        c.delta(rig, "LongForearm", (-.25 * t, 0, 0))
        c.delta(rig, "Case", (-.14*t, -.12 * t, .18 * t))
    elif name == "attack":
        swing = c.envelope(t, [(0, .45), (.23, .60), (.4, -1.75), (.60, -1.85), (1, 0)])
        c.delta(rig, "LongArm", (swing, -.20 * math.sin(math.pi * t), 0))
        c.delta(rig, "LongForearm", (-.35 * math.sin(math.pi * t), 0, 0))
        c.delta(rig, "Case", (.10 * math.sin(math.pi * t), 0, -.14 * math.sin(math.pi * t)))
    elif name == "hit":
        recoil = math.sin(math.pi*t)
        c.delta(rig, "Case", (-.30*recoil, .25*recoil, 0), (0,.04*recoil,.04*recoil))
        c.delta(rig, "LongArm", (.35 * math.sin(math.pi * t), 0, 0))


def main():
    c.reset()
    m = build()
    clips = c.animate(m, motion)
    manifest = c.save_model(m, clips, {"attack_origin": c.socket(m.rig, "LongForearm", (.62, -.18, .25)),
                                      "head_or_top": c.socket(m.rig, "Case", (-.065, .025, 1.20))})
    c.previews(m, clips, manifest)


if __name__ == "__main__":
    main()
