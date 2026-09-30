# ============================================================================
# validate_hunter_ticking.py
# ============================================================================
# PURPOSE:
#   Measures the shipped clock FBX after a fresh import rather than trusting its
#   generator. It verifies the silhouette landmarks and continuous pendulum so a
#   static prop or forward-reversed clock cannot silently satisfy the hand-off.
# ARCHITECTURAL ROLE:
#   Offline art validator · no runtime layer · Hunter / PLAN-015.
# KEY RESPONSIBILITIES:
#   - Check the 1.2m asymmetric clock, rear key and exact rigid skeleton.
#   - Verify every take contains a swinging pendulum and the attack swings an arm.
#   - Report shared contract checks and reproducible imported semantic hashes.
# DEPENDENCIES:
#   Blender 5.2 and hunter_creature_common; generated art and manifests only.
# USAGE NOTES:
#   Run with factory startup and python-exit-code 1; no Unity is started.
#   Clock hands do not represent eyes or encode gameplay timing.
# ============================================================================
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import hunter_creature_common as c


def main():
    v = c.Validation("Ticking")
    try:
        expected = {"Root": None, "Case": "Root", "Pendulum": "Case", "Key": "Case",
                    "LongArm": "Case", "LongForearm": "LongArm", "ShortArm": "Case", "ShortForearm": "ShortArm",
                    "LeftLeg": "Root", "LeftShin": "LeftLeg", "RightLeg": "Root", "RightShin": "RightLeg"}
        v.shared(expected, (1.1999, 1.2001), lambda a: c.center(a, "ClockDial").y < -.2 and c.center(a, "KeyShaft").y > .3)
        v.sockets({"attack_origin": ("LongForearm", (.62, -.18, .25)), "head_or_top": ("Case", (-.065, .025, 1.20))})
        v.check("unequal_arms", c.center(v, "LongHand").z < c.center(v, "ShortHand").z - .25, {"long": list(c.center(v, "LongHand")), "short": list(c.center(v, "ShortHand"))})
        pendulum = {}
        for name, clip in v.clips.items():
            rotations = []
            for f in range(1, c.CLIPS[name] + 2):
                c.pose(v.rig, clip, f)
                p = v.rig.pose.bones["Pendulum"]
                rotations.append((p.parent.matrix.inverted() @ p.matrix).to_quaternion())
            swing = max(rotations[0].rotation_difference(q).angle for q in rotations)
            pendulum[name] = swing
            v.check(name + "_pendulum", swing > .25, swing)
        v.signature["pendulum_motion"] = pendulum
        c.pose(v.rig, v.clips["attack"], 1)
        initial = v.rig.pose.bones["LongArm"].matrix.to_quaternion()
        c.pose(v.rig, v.clips["attack"], 13)
        angle = initial.rotation_difference(v.rig.pose.bones["LongArm"].matrix.to_quaternion()).angle
        v.check("attack_arm_swing", angle > 1, angle)
        c.clear_pose(v.rig)
        v.source_motion()
    except Exception as exc:
        v.check("exception", False, repr(exc))
    v.finish()


if __name__ == "__main__":
    main()
