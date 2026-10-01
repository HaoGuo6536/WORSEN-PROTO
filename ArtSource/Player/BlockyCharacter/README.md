# Relaxed first-person arms

This note supersedes the Hold-pivot description in `tools/blender/README.md` for
first-person arms only. The existing inventory entry in `ArtSource/README.md`
continues to identify `BlockyCharacter.blend`; no new Blender source is added.

## Source and regeneration

- `BlockyCharacter.blend`: unchanged full-body design plus the relaxed FP rig.
- `RelaxedArms.geometry.json`: generator-authored shoulder-relative vertices in
  Unity coordinates (-Blender X, Blender Z, -Blender Y), consumed by pure tests.
  The validator independently compares these samples with imported FBX vertices.
- `Assets/Art/Player/BlockyCharacter/BlockyArmsFP.fbx`: relaxed 9-bone rigid skin.
  Hold (constant) and Sway remain as unused compatibility takes on this new rest
  pose; the runtime still strips Animator. They do not implement walking swing.
- The generator leaves an existing `BlockyCharacter.fbx` byte-for-byte intact.
  Only a missing full-body export is regenerated from the unchanged body code.
  The validator checks both the shipped file hash and its semantic content hash.

Run from this worktree, using Blender 5.2 headless:

    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/blocky_character.py
    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/validate_blocky_character.py

The frozen full-body content hash is
`31e548c370b6f52d98c00317177d90481d4f8ba5b88dfdfe96fce4fbdae2319f`.
The frozen file SHA-256 is
`4a59ee5948290f449e3613394015148339f198af92b54326bbd9f01b9f63c6fb`.

## Runtime contract and provisional values

`PlayerMoverDriverConfig` owns the following defaults, all provisional:

| Serialized field | Default | Meaning |
| --- | --- | --- |
| `_handOffset` | (+/-0.24, -0.22, +0.08) metres | Shoulder, not wrist, relative to final eye in camera-yaw space |
| `_armSwingDegrees` | 6 degrees | Maximum at reference speed; Presenter enforces owner ceiling of 8 degrees |
| `_armSwingReferenceSpeed` | 4 metres/second | Horizontal speed producing full amplitude |
| `_armSwingFrequency` | 1.3 cycles/second | Full left/right swing cycle |
| `_armSwingEaseSeconds` | 0.2 seconds | Time to ramp an entire amplitude envelope |

`PlayerPrefabGenerator.EnsureAssets` migrates the existing config once, writes
`_relaxedArmsVersion = 1`, and saves it at its existing asset path. Subsequent
setup retains designer changes and GUIDs. `_handOffset` retains its serialized
name to preserve callers; its meaning changes along with the prefab pivot.
The asset itself must be saved by the coordinator's Unity setup, not by hand.

New authored art values in `blocky_character.py`: upper arm 12 degrees out from
vertical, elbow flexion 20 degrees forward, hand aligned with forearm, palm
thickness normal toward the torso, and unweighted shoulder stub 0.04 m outward.
Unchanged segment lengths are upper 0.28 m, forearm 0.26 m, hand 0.16 m.
At a 1.6 m eye, the default palm centre is about 0.794 m above the feet.

`PlayerLimbStandIn` anchors the shoulder, not the wrist, with yaw only. Swing is
about the shoulder's lateral axis, exactly opposite for the two arms. Time,
speed, crouch, and config arrive through `Apply`. A stopped/crouched/sliding/
vaulting/airborne/stumbling state sets the swing target to zero immediately;
existing motion eases to exactly zero within the configured 0.2 s instead of
snapping. Render callbacks never advance phase. Teardown resets phase/envelope.

Near-plane clearance projects each current world-space renderer AABB. It applies
only the necessary translation; upward-view corrections are horizontal so they
do not lift the shoulder. Bounds wholly below the original view disable their
renderer before clearance, preventing hidden arms from being pushed onscreen.
These are visual transforms only; no colliders are added or moved.

## Previews and scope

All evidence is in `Logs/AgentValidation/Art/BlockyCharacter/`:

- `fp-relaxed-pitch0.png`: no arms visible, satisfying the 'at most hands and
  forearm ends' limit and keeping the central 60% completely clear.
- `fp-relaxed-pitch-down45.png`: both arms hang beside the player.
- `fp-relaxed-pitch-up30.png`: arms out of view.
- `fp-relaxed-side.png`: relaxed arms beside the full-size body (T-pose body arms
  omitted for readability); eye height is 1.6 m.
- `fp-old-hold.png`: frozen original Hold rest pose for comparison.
- `relaxed-preview-math.json`: lens, bounds culling and clearance measurements.
- `validation.json` / `validation.txt`: imported FBX checks, pose angles, palm
  normals, body hashes and clipped-polygon composition at swing -8/0/+8 degrees.

First-person previews use 1280x720, a verified 75-degree VERTICAL FOV and 0.05 m
near plane. They read the C# default shoulder offset that setup will write,
not the currently unmigrated asset. The pitch-zero and upward images are
intentionally empty. These are Blender renders, not Unity screenshots.

## Coordinator integration

1. In `PlayerDriver.ShowMovement`, append to the existing `Apply` call:
   `new Vector2(_state.Velocity.x, _state.Velocity.z).magnitude, _state.Height < _config.Height, _state.LastStepDuration, _config`.
   Existing initialization/teardown calls remain valid. Without this owner change
   the rest pose works after rebuilding but the optional walking swing stays zero.
2. Run `Worsen/Player/Import Blocky Character` twice under the coordinator's lease.
   This reuses BlockyCharacterSetup and the existing config/material identities.
3. Run `PlayerArmsSetupTests`, `BlockyCharacterSetupTests`, `PlayerDriverTests`,
   architecture checks and the traversal integration fixture in Unity.
   `BlockyCharacterSetupTests.RealArmsRebuildAndClearTheFinalCameraPlane` still
   expects wrist roots in the viewport at every pitch. Its owner must replace
   only those obsolete root viewport assertions with shoulder-relative/gravity
   assertions, retaining all real vertex near-plane, skin, and teardown checks.
4. Owner reviews the pose and swing in HorrorRun. Confirm Unity's handedness,
   dynamic skinned bounds, culling transitions and config migration; headless math
   cannot prove those engine behaviours. Unity generates the new test .meta files.
5. Update the shared `tools/blender/README.md` Hold-pivot/default-offset paragraphs
   from this note; that shared file was outside this worker's ownership.
