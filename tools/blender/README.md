# Original blocky Player art

This generator uses only Blender primitives and authored numbers. No asset is
fetched, copied or referenced. Blender 5.2.1 LTS runs headless; Unity is not used.
All paths resolve from this script's worktree, never from another checkout's
`ArtSource/` files. Do not run against the open main checkout during another worker's
Unity lease. Do not copy the worktree's junctioned Library. Placement and naming of
authoring `.blend` files follow [ArtSource/README.md](../../ArtSource/README.md).

## Regenerate and validate

From `C:/Users/Hao Guo/Documents/UnityProjects/WORSEN-wt/blocky-character`, Git Bash:

```sh
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/blocky_character.py
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/validate_blocky_character.py
powershell -NoProfile -File "C:/Users/Hao Guo/.claude/delegations/_shared/compile.ps1" -Worktree "C:/Users/Hao Guo/Documents/UnityProjects/WORSEN-wt/blocky-character" -RunName blocky-owner-001
ast-grep scan
```

Use a new compile run name each time. `--python-exit-code 1` makes a Python
exception fail the Blender process, including validation failures. Validation
re-imports each FBX into a fresh scene, independently checks its hierarchy and
weights, and writes `validation.txt` / `validation.json` beside the previews.
The JSON includes content hashes of imported geometry, weights, hierarchy,
materials and sampled animations for repeat-generation comparison. FBX creation
timestamps and Blender session metadata need not be byte-identical.

Authoring source in `ArtSource/Player/BlockyCharacter/` (outside `Assets/`, so
Unity never imports it):

- `BlockyCharacter.blend`: both models, all four actions, full-character IK controls.

Runtime outputs in `Assets/Art/Player/BlockyCharacter/`:

- `BlockyCharacter.fbx`: Body (180 triangles), Arms (72), one 21-bone rig.
- `BlockyArmsFP.fbx`: LeftArm (36 triangles), RightArm (36), one 9-bone rig.

Previews in `Logs/AgentValidation/Art/BlockyCharacter/`:
`front.png`, `side.png`, `three-quarter.png`, `walk-mid-pose.png`, `first-person.png`.
Workbench uses flat polygon normals, studio lighting and a neutral background.
The first-person preview is the source Hold rest pose at a 75-degree lens, not a
Unity screenshot. Validation checks image dimensions/content, not artistic taste.

## Source and export contract

Metres, total height 1.8, Blender Z-up, feet at Z=0, facing -Y. The validator
reports height/ground in Unity's Y-up convention after Blender restores Z-up.
Two disconnected rigidly weighted mesh objects share the character armature.
Every vertex has one weight of 1; split elbow/knee boxes have no smoothing or
bevels. Two small shoe-coloured eye boxes make the front unambiguous.

Full hierarchy (Left and Right have identical topology):

```text
Hips
  Spine
    Chest
      Neck
        Head
      LeftShoulder -> LeftUpperArm -> LeftLowerArm -> LeftHand
      RightShoulder -> RightUpperArm -> RightLowerArm -> RightHand
  LeftUpperLeg -> LeftLowerLeg -> LeftFoot -> LeftToes
  RightUpperLeg -> RightLowerLeg -> RightFoot -> RightToes
```

The separate first-person rig has `Root`, then the two shoulder-to-hand chains.
`Idle` is 2 seconds, `Walk` is 1 second, `Hold` is a constant 1-second take, and
`Sway` is 2 seconds, all sampled at 30 fps with matching loop endpoints. Walking
is in-place; arm lowering belongs to the action, never to the Humanoid rest pose.
NLA export isolates each model's own takes, preventing cross-rig action leakage.

FBX: scale 1, metre units, FBX_SCALE_ALL, forward -Z, up Y, apply transform,
deform bones only, no leaf bones, triangulated faces, baked NLA actions, no curve
simplification. The validator checks bind-pose deformation after re-import since
Blender's apply-transform option is a known area requiring rig verification.

The .blend's `Controls (IK influence initially zero)` collection contains four
IK targets and four poles, all non-deforming. Enable the desired lower-limb IK
constraint influence for interactive posing. It defaults to zero to preserve FK
actions and the T-pose. Controls are added after FBX export and cannot leak into
runtime files. The FP rig is hidden in the saved authoring scene; unhide it to edit.

## Coordinator-only Unity integration

No Unity operation has been performed by this task. After integrating and
acquiring the Unity lease, invoke `Worsen/Player/Import Blocky Character`.
This configures both importers and runs the existing Player asset generator.
It does not modify scenes or project settings. Unity must generate the .meta files.

The full FBX gets an explicit Humanoid mapping using HumanTrait names, including
LeftShoulder and RightShoulder. The setup rejects an invalid avatar instead of
silently falling back. Check shoulders, forward axis, 1.8m height and T-pose in
Unity's Avatar inspector. The FP FBX stays Generic with Root as motion node,
preserved hierarchy and no optimized-away transforms. Clips are renamed to
Idle/Walk/Hold/Sway, with Hold non-looping and the others looping.

Embedded flat colours are remapped to shared URP/Lit materials in the art's
`Materials/` folder. Missing materials are created; existing material identities
and tuning are retained on repeat setup. No textures are needed. The authoring
.blend lives in `ArtSource/`, so Unity does not import it through its installed
Blender association (which may point to Blender 4.5); only the FBXs are imported.

The Player builder creates separate Left Hand/Right Hand roots at the imported
wrist pivots. It reparents each shoulder chain and its skinned mesh intact, strips
colliders/Animator, and sets `_showHands` true. This uses the authored Hold rest
pose; it does NOT automatically play Sway. The source FBX retains both original
animation clips and paths. Moving them onto the split runtime rig would require
a separately designed Player animation path, not reattaching an Animator with
broken paths. Missing FBX means hidden capsules and `_showHands` false; a present
but invalid FBX is an error, not a silent capsule fallback.

The existing PlayerMoverDriverConfig.HandOffset is reused unchanged. The limb
presenter projects every descendant renderer bound relative to its wrist root,
so off-centre arms, camera pitch/roll and look-back respect the current near plane.
No new runtime tunables or assembly dependencies are introduced.

Run PlayerLimbPresenterTests, PlayerDriverTests, BlockyCharacterSetupTests and
ArchitectureConformanceTests in Unity, then the full suite. The new fixture
checks missing-art replacement, clip/avatar settings, repeated real-arm wiring,
collider removal, skin references, camera-space vertices and teardown. Tests
have been authored for the coordinator; offline compilation does not execute them.
Re-run setup twice and verify material GUIDs, no duplicate limbs, and the actual
lower-corner composition in HorrorRun before owner acceptance. Graph refresh and
conformance are also a coordinator hand-off because the main index is read-only
and stale for this worktree.

## Provisional art values

These are authored asset values in `blocky_character.py`, not runtime logic
constants. Runtime view tuning remains in the existing PlayerMoverDriverConfig.

- COLORS (linear RGBA): Skin (.58,.32,.19,1), Shirt (.045,.075,.085,1),
  Trousers (.17,.205,.22,1), Shoes (.022,.027,.032,1); roughness 1.
  Newly created Unity materials use smoothness 0; existing materials are retained.
- Shared arm length/width/thickness: upper (.28,.17,.17), fore (.26,.135,.135),
  hand (.16,.145,.10) m. Longitudinal box lengths leave a .006m joint gap.
- Body widths/depths/heights: hips (.38,.25,.13), belly (.36,.24,.18),
  chest (.46,.27,.29), neck (.14,.14,.09), head (.28,.255,.26) m.
  The body centres and skeleton joint coordinates are listed directly in
  full_character; they define the 1.8m T-pose, not runtime tuning knobs.
- Leg spacing +/- .115m; thigh (.18,.21,.37), shin (.155,.18,.37),
  foot (.185,.24,.15), toe (.185,.13,.10) m. Eyes (.027,.008,.027) m.
- FP palm centres (+/-.32,-.52,-.25) m in Blender camera coordinates. Unit
  directions are normalized from (+/-.10,-.30,.95), (+/-.22,-.75,.624),
  (+/-.12,-.98,.12), with X signs toward the centre. Shoulder stub .1m
  outward and .04m down; Root length .10m.
- Idle chest sway 1.2 degrees and spine scale amplitude .008; Walk arm drop
  82 degrees, arm swing 20 degrees, elbow bend 9 degrees, leg swing 25 degrees,
  knee bend 30 degrees; Sway shoulder rotation 1.5 degrees. Durations above.
- Authoring controls .12m long, arm pole displacement (0,.4,-.2)m,
  leg pole displacement (0,-.5,0)m, influence 0 by default, chain length 2,
  no stretch. Preview orthographic scale 2.35m, perspective lens 75 degrees,
  near plane .05m, background (.19,.19,.19); camera locations live in main().

# SPEC-005 creature hunter prototypes

Weaver (a wide, jointed six-limbed body carried at 0.9 m), Ticking (an asymmetric clock case with a rear key and a pendulum) and Mimic (the cake pickup split into a hinged, toothed jaw) are original prototypes. They follow the same contract as the humanoid set: six actions, a rigid-skinned rig and a manifest. The shared helper is `hunter_creature_common.py`.

```sh
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/hunter_<name>.py
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/validate_hunter_<name>.py
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python-exit-code 1 --python tools/blender/hunter_mimic.py -- --lineup
```

- **Mimic source:** reads `ArtSource/Horror/Cake/WORSEN_CakePickup.blend` read-only. Its validator checks that the closed pose matches the cake's surface within 5 mm and that the packed palette bytes are unchanged.
- **Hashes:** semantic hashes exclude binary timestamps.
- **Authored numbers:** the full list of provisional authored numbers is written to `Logs/AgentValidation/Art/HunterProvisionalValues-hunter-art-b.json`.

