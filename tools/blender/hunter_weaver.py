# ============================================================================
# hunter_weaver.py
# ============================================================================
# PURPOSE:
#   Authors the wide, six-limbed Weaver instead of reusing the legacy werewolf.
#   A flattened carapace and pale joints disclose its overhead skitter without
#   eyes or a face. Its warned attack drops the shell before rearing to release.
# ARCHITECTURAL ROLE:
#   Offline art generator · no runtime layer · Hunter, SPEC-005 §2.3 / PLAN-015.
# KEY RESPONSIBILITIES:
#   - Define original primitive geometry, palette and three-segment limb rig.
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

PALETTE = {"Carapace": (.24, .135, .085), "Ridge": (.36, .215, .125),
           "Limbs": (.16, .105, .072), "JointTips": (.76, .70, .53), "Underside": (.095, .065, .052)}


def build():
    definitions = [("Body", "Root", (0, 0, .9))]
    limbs = []
    for side, sign in (("L", -1), ("R", 1)):
        for i, y in enumerate((-.34, .02, .34)):
            key = side + str(i)
            reach_y = y + (i - 1) * .23
            p = [(sign * .27, y, .89), (sign * .66, reach_y, 1.03),
                 (sign * .98, reach_y + (i - 1) * .12, .50),
                 (sign * .89, reach_y + (i - 1) * .20, .035)]
            for j in range(3):
                definitions.append((key + "Segment" + str(j), "Body" if j == 0 else key + "Segment" + str(j - 1), p[j]))
            limbs.append((key, p))
    m = c.Creature("Weaver", PALETTE, definitions)
    m.shape("Carapace", "Body", (0, .02, .9), (.84, 1.02, .25), "Carapace", "ico")
    m.shape("DorsalRidge", "Body", (0, .10, .998), (.25, .77, .075), "Ridge", "ico")
    m.shape("Underside", "Body", (0, .02, .805), (.61, .81, .10), "Underside", "ico")
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
    return m


def motion(rig, name, t):
    wave = math.sin(2 * math.pi * t)
    if name in {"walk", "run"}:
        amount = .23 if name == "walk" else .36
        for side, sign in (("L", -1), ("R", 1)):
            for i in range(3):
                w = wave * (-1 if (i + (sign > 0)) % 2 else 1)
                key = side + str(i)
                c.delta(rig, key + "Segment0", (0, sign * .12 * max(0, w), amount * w))
                c.delta(rig, key + "Segment1", (0, -sign * .18 * max(0, w), -.10 * w))
                c.delta(rig, key + "Segment2", (.10 * w, sign * .10 * max(0, w), 0))
        c.delta(rig, "Body", (0, .025 * wave, 0))
    elif name == "idle":
        c.delta(rig, "Body", (.018 * wave, 0, 0))
    elif name == "ready":
        lift = .55 * t
        for side, sign in (("L", -1), ("R", 1)):
            c.delta(rig, side + "0Segment0", (-lift, sign * .14 * t, 0))
            c.delta(rig, side + "0Segment1", (-.23 * t, 0, 0))
        c.delta(rig, "Body", (-.08 * t, 0, 0))
    elif name == "attack":
        drop = c.envelope(t, [(0, 0), (.23, -.20), (.4, -.15), (.67, .10), (1, 0)])
        rear = c.envelope(t, [(0, -.08), (.23, .10), (.4, -.23), (.67, -.39), (1, 0)])
        c.delta(rig, "Body", (rear, 0, 0), (0, 0, drop))
        for side in ("L", "R"):
            c.delta(rig, side + "0Segment0", (c.envelope(t, [(0, -.55), (.4, -.12), (.67, -.75), (1, 0)]), 0, 0))
    elif name == "hit":
        recoil = math.sin(math.pi * t)
        c.delta(rig, "Body", (.18 * recoil, .20 * recoil, 0), (0, .045 * recoil, -.10 * recoil))


def main():
    c.reset()
    m = build()
    clips = c.animate(m, motion)
    manifest = c.save_model(m, clips, {"attack_origin": c.socket(m.rig, "Body", (0, -.60, .84)),
                                      "head_or_top": c.socket(m.rig, "Body", (0, .10, 1.035))})
    c.previews(m, clips, manifest)


if __name__ == "__main__":
    main()
