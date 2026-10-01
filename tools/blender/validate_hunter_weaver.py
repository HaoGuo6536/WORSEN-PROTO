# ============================================================================
# validate_hunter_weaver.py
# ============================================================================
# PURPOSE:
#   Re-imports Weaver's exported FBX into an empty Blender scene and measures it.
#   Independent shape and motion assertions reject a mislabeled generic body or
#   a manifest that disagrees with the actual rigid skin and baked actions.
# ARCHITECTURAL ROLE:
#   Offline art validator · no runtime layer · Hunter / PLAN-015.
# KEY RESPONSIBILITIES:
#   - Check dimensions, forward landmarks, eight articulated legs and exact bones.
#   - Verify skitter movement and the warned body-drop/rear attack sequence.
#   - Write fail-closed validation evidence and imported-content hashes.
# DEPENDENCIES:
#   Blender 5.2 and hunter_creature_common; generated FBX/manifest/previews.
# USAGE NOTES:
#   Never runs Unity. Raises on failure with --python-exit-code 1.
#   Generic rig integration and 15m fog readability still require owner review.
# ============================================================================
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import hunter_creature_common as c
from hunter_animation_review import validate_motion


def main():
    v = c.Validation("Weaver")
    try:
        expected = {"Root": None, "Body": "Root"}
        for side in ("L", "R"):
            for i in range(4):
                for j in range(3):
                    expected[side + str(i) + "Segment" + str(j)] = "Body" if j == 0 else side + str(i) + "Segment" + str(j - 1)
        v.shared(expected, (.95, 1.2), lambda a: c.center(a, "ForwardSpinner").y < -.40 and c.center(a, "RearPlate1").y > .3)
        v.sockets({"attack_origin": ("Body", (0, -.60, .84)), "head_or_top": ("Body", (0, .10, 1.035))})
        v.check("wide_flat_outline", 1.9 < v.report["width_m"] < 2.2 and abs(c.center(v, "Carapace").z - .9) < .025, {"width_m": v.report["width_m"], "body_z": c.center(v, "Carapace").z})
        v.report['motion'] = validate_motion('Weaver',v.rig,v.meshes,v.clips,
            dict(idle=.04,walk=.30,run=.45,ready=.35,attack=.60,hit=.25),v.check)
        c.pose(v.rig, v.clips["attack"], 1)
        start = (v.rig.matrix_world @ v.rig.pose.bones["Body"].head).z
        c.pose(v.rig, v.clips["attack"], 8)
        dropped = (v.rig.matrix_world @ v.rig.pose.bones["Body"].head).z
        c.pose(v.rig, v.clips["attack"], 21)
        reared = (v.rig.matrix_world @ v.rig.pose.bones["Body"].head).z
        v.check("attack_drops_then_rears", dropped < start - .15 and reared > start + .06, [start, dropped, reared])
        c.clear_pose(v.rig)
        v.source_motion()
    except Exception as exc:
        v.check("exception", False, repr(exc))
    v.finish()


if __name__ == "__main__":
    main()
