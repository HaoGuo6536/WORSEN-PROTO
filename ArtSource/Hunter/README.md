# Hunter sources — project roster and cake disguise (PLAN-017)

Owner request: 2026-10-01. Ram, Skip and Blinder now join the seven earlier
project bodies. No third-party geometry or downloads are used. Mimic retains
the project's own cake surface and packed palette images; the latest request
supersedes the earlier exposed jam/piping tell and breathing disguise.

## Inventory

Each row's source is `<Hunter>/WORSEN_Hunter<Hunter>.blend` in this folder.
Its runtime export and measured manifest are
`Assets/Art/Hunter/<Hunter>/WORSEN_Hunter<Hunter>.fbx` and `.manifest.json`.

| Hunter | Triangles (FBX re-import) | Used materials | Pass-2 modelling |
|---|---:|---:|---|
| Echo | 4952 | 3 | Pleated blue-grey cover, irregular hem, hanging tatters, hood welt and staggered recording scars; joint volumes replace floating limb ends. |
| Ram | 4112 | 4 | Low impact skull, broad shoulder shields, curved horn yoke, split hooves and hide folds; stamp, bounding charge and wall stagger. |
| Skip | 4884 | 4 | 1.2m crooked hood, wiry wrapped limbs, stitched coat and fingers; doorway crouch and silent intercept. |
| Blinder | 5368 | 4 | Stooped bound head, exposed throwing arm, powder-filled pouch, drawstring and dust-stained apron; draw-back and release. |
| Herald | 6408 | 3 | Curved tapered rib doors, contrasting bone, vertebrae, throat cords, jaw frame and small teeth; original chest-opening bones retained. |
| Mannequin | 7556 | 3 | Tapered shells, dark joint hardware, screw slots, segmented abdomen, fingers and draped dust sheet. |
| Mimic | 3462 | 3 | Original cake exterior, UVs and corner normals; no added exterior tells. Concealed teeth and palate unfold on the original jaw rig. |
| Stare | 5592 | 4 | Tall asymmetric pleated cover, long pale face, recessed dark orbits, fixed tiny eyes, nose and fingers. |
| Ticking | 7836 | 4 | Bevelled wooden case and dial, open toothed side gears, rivets, articulated brass fingers, pendulum and rear winding key. |
| Weaver | 7432 | 4 | Shaped shell plates and flutes, tapered three-segment legs, joint spurs, curved mandibles and six eyes. |

## Contract and provenance

- Blender 5.2.1 LTS headless; metre-scale FBX, -Z forward, Y up,
  `bake_space_transform=True`, importer `bakeAxisConversion=false`.
- Existing armature identities, bones/rest positions/hierarchy, sockets, six clip
  names/ranges/loop flags and 30 fps are retained for existing bodies. The three
  new bodies share the humanoid bone hierarchy and six-role contract. Mimic's
  idle/walk/run are exact closed holds; bite/recovery retain motion minima.
  Source/FBX motion agreement is independently measured by the validators.
- Each rigid mesh section has one normalized bone weight. Hardware, fingers,
  ribs, jaw and cloth sections use their original owning bones; no new rig or
  runtime animation controller is introduced. Cloth is segmented rigid prototype
  art, not simulated fabric.
- Existing `.meta` files are untouched. Unity import, material creation/remapping,
  prefab rebuild and in-game lighting acceptance belong to the coordinator.
- The owner-requested Stare face remains. The latest Mimic request restores an
  indistinguishable disguise: no jam tell or extra piping, no breathing. The cake
  has a cherry, not a candle; adding a candle only to Mimic would break parity.
  Imported split normals preserve the cream/cherry shading. Unity setup copies
  the actual cake material, configured elevation and three native Lumen layers.

## Reproduction (isolated worktree only)

Use the installed Blender executable with
`--background --factory-startup --python-exit-code 1 --python <script>`.
Do not open Unity or modify the shared Library junction.

1. Run `tools/blender/hunter_<lowercase hunter>.py` for each hunter.
2. Run `tools/blender/validate_hunter_<lowercase hunter>.py` for each hunter.
3. Run `tools/blender/validate_hunter_detail_contract.py` (9 boundary/mutation cases)
   and `tools/blender/validate_hunter_motion_regressions.py` (10 mutation cases).
4. Run `tools/blender/hunter_detail_review.py` to render all close/10m views and
   warm/cold six-frame strips for every clip. Append `-- HunterName` for one body.
5. Run that review script with `-- --distance-only` for true-10m warm/cold attacks.

The original validators continue to check the full motion minima, alternating
feet, floor clearance, roots, seams, bind poses and source/export agreement.
The shared detail gate now requires 3000–8000 triangles, 2–4 unique used materials,
no empty bodies and no missing/unused material slots. This is not a polygon-count
claim copied from a manifest.

## Review evidence

Current PNG boards and rendering settings are under
`Logs/AgentValidation/Art/HunterDetailPass2/<Hunter>/`:

- `warm-turntables.png`, `cold-turntables.png`: front, three-quarter, side;
  top row close, bottom row fixed 10m perspective.
- `warm-animations.png`, `cold-animations.png`: rows idle, walk, run, ready,
  attack, hit; six evaluated FBX poses per row. Attack samples 0/20/26.7/40/65/100%.
- `attack-10m.png`: warm row then cold row, same six attack phases at 10m.
- `review-settings.json`: camera and lighting values, not unspecified studio light.

`Hunter<Hunter>/validation.json` beside the pass folder contains counted contract
checks and motion metrics. Earlier failed geometry, buried-jam RED evidence and
superseded lighting renders are retained separately in the pass folder.

These are dim studio proxies, not a Unity scene, fog, occlusion or owner playtest.
A cake-sized Mimic is necessarily small at 10m. Tiny fasteners and eyes are
secondary details; silhouette and motion must carry recognition. The additional
`Mimic/review_disguise.py` renders the unmodified source and re-imported Mimic at
close, 3m and 6m distances into `HunterMimic/real-cake-vs-mimic.png`.

### Earlier pass-2 visual review (Mimic observations superseded below)

All five final boards per hunter were opened with vision: both turntables, both
six-clip boards and the warm/cold 10m attack board. Observations, not Unity claims:

- Echo: narrow hood, blank recess and vertical folds remain visible under both
  colours; the ragged lower edge and staggered chest marks add close detail.
  Walk/run move the legs and arms, and the attack holds a two-handed reach.
- Herald: pale curved ribs stand out around the red-black chest; jaw/throat and
  vertebrae read close up. Ready opens the rib doors; the scream spreads arms
  widely enough to change the silhouette at 10m.
- Mannequin: blank covered head, pale shells, dark mechanical joints and fingered
  hands read as an articulated dummy. Stiff steps and the abrupt two-arm lunge
  remain visible. Screw slots are close-up detail, not distant recognition cues.
- Stare: the long pale mask and off-centre lean distinguish it from Echo; its
  small fixed eyes stay attached while the unequal arms stalk and sweep. The
  cold-lit face contrasts more strongly than the dark robe and far-side arm.
- Ticking: round dial, pendulum and unequal arms make a compact walking clock;
  side views expose the gears and rear key. Warm brass reads more clearly than
  cold-side wood; walk/run differ and the long arm swings across the dial.
- Weaver: the three-quarter view separates all eight thin segmented legs;
  straight-on legs overlap. Pale joints and feet retain the wide spider outline
  in darkness. The warning lifts front legs; the attack visibly drops and rears.
- Mimic: cream bands, piped beads and cherry read as layered cake; two narrow jam
  drips are exposed on the sponge after a RED→GREEN burial check. The decorated
  lid follows the jaw, opens to reveal teeth and closes at contact. At 10m it is
  very small: the opening triangular lid is visible, but neither the tell nor
  individual teeth can be judged reliably. Full distance acceptance is deferred
  rather than inflating its size or zooming its review camera.

The first geometry review prompted joint-gap repairs and shorter Echo tatters
after run-floor penetration failed validation. A later review exposed the buried
jam drip; its measured exterior position and an explicit visibility check now
prevent that regression. No animation minima were reduced for either repair.

## Provisional authoring values (no runtime tunables added)

All new mesh coordinates, section proportions and palette values are provisional
art data in `tools/blender/hunter_detail_geometry.py` and each hunter generator.
Principal shared defaults are:

| Location | Default |
|---|---|
| `SculptBody.box` | Edge bevel 18% of minimum dimension, two segments; toes narrow by up to 18% and slope by up to 45%. |
| `SculptBody.link` / `tube` | Eight-sided five-ring tapered links; radius profile .65 / 1 / .94 / end / .65×end. Detail sweeps use 4, 6 or 8 sides as authored. |
| `SculptBody.cloth` | 24 radial samples, four samples per input interval, 6.5% pleat modulation, 12 folds, up to 35mm irregular hem. |
| `SculptCreature.shape` | Spheres 12×8; cylinders 32 sides; teeth 8 sides; bevel 16% of minimum dimension, one segment. |
| `humanoid_details` | Joint volumes .085×.09×.105m; finger radii .012/.010/.003m; Echo tatter tips .20/.218/.236m; other detail coordinates are explicit per-creature authoring. |
| `mimic_details` | 20 cream beads, .016×.021×.015m, z=.225m; cream sRGB (.87,.81,.67); jam radii .004/.003/.001m, just outside measured sponge x=±.07870057 at y=.02. |
| `hunter_stare.PALETTE` | Cloth (.19,.22,.25), rim (.32,.38,.43), mask (.62,.65,.61), eyes (.85,.88,.77); rim emission .12, eye emission .16. |
| `hunter_herald.PALETTE` | Added Bone (.76,.72,.59); existing cavity emission .3 unchanged. |
| `hunter_weaver.PALETTE` | Shell (.30,.22,.16), ridge (.46,.35,.23), limbs/underside (.17,.14,.12), tips/eyes (.76,.70,.53). |
| `hunter_ticking.PALETTE` | Combined wood (.38,.25,.15); existing brass, dial and dark values retained. |
| `hunter_detail_review` | Cycles CPU 16 samples, AgX Medium High Contrast, exposure 0, world strength .12; key sun 2.0 and rim 1.1; warm/cold RGB pairs explicitly in `LIGHTS`. Fixed-distance camera 50mm, 36mm sensor, 10m from target z=1.6m. |

Triangle/material thresholds are owner acceptance gates rather than gameplay
balance values. No C# Config, package, scene or prefab was edited.

## New-body / exact-disguise hand-off

The new generators use `SculptCreature` and the shared motion/export/review
pipeline. Run each new validator with `-- --render --detail` to regenerate the
six-clip strips and warm/cold boards. All four validators pass: Ram 126 checks,
Skip 153, Blinder 138, Mimic 103. Their measured heights are 1.62m, 1.20m, 1.50m
and 0.289091m respectively. Mimic's exterior surface error is 0.283mm; closed
bounds differ by under 0.00003mm, with matching UVs/textures and corner normals.

New provisional art data lives in each generator's `PALETTE`, `build` and
`motion` functions. No gameplay Config values were changed. The six take frame
ranges remain idle 1–61, walk 1–31, run 1–21, ready/hit 1–25, attack 1–31;
attack contact/release is frame 13. Ram's bounding run uses 0.18 stance duty,
stride 0.46×hip height, crouch 0.20×hip height, bob 0.035×hip height and lift
0.32×hip height. Measured walk/run speeds at 1× (m/s), also in manifests:

| Body | Walk | Run |
|---|---:|---:|
| Ram | 1.143072 | 6.762004 |
| Skip | 0.705600 | 1.517040 |
| Blinder | 0.940800 | 2.022719 |

The runtime owner must use these reference speeds: Ram at 18m/s requires about
2.662× playback, not the old generic/vendor reference. Phase mapping remains
ready=stamp/windup, run=charge, hit=wall stagger; Blinder attack=throw.

The coordinator owns the base-prefab collision fix. The closed cake bounds in
Unity axes are 0.266348 × 0.289091 × 0.352336m (width/height/depth). Use a matching
box where the motor permits it. A retained upright capsule cannot fit this
non-circular footprint exactly; radius 0.176168m and height 0.352336m remove the
1.8m pillar but remain an approximation. Center it on the displayed cake bounds,
including FloorDriverConfig.PickupHeight (currently 0.7m), not at 0.9m.

Setup order: existing HorrorRun/cake/Lumen and roster-profile setup first,
then `HunterRosterVisualSetup::Build`, then `HunterRosterVisualSetupUnityTests`,
`HunterFactoryRosterTests`, `HunterRosterScopeTests` and native animation tests.
Run visuals last after any full scene/base-body rebuild. RosterB profile setup
itself preserves an already bound prefab. Verify native Lumen initialization,
repeat-build stability, Mimic geometry and 18m/s playback.
No Unity execution, .meta authoring, prefab editing, staging or commit occurred.

Final boards were opened with vision. Ram's pale horn yoke and low skull remain
broad; run has distinct alternating bounds and hit rocks the shoulders back.
Skip's wrapped limbs, uneven hood and lowered posture read as a wiry ambusher,
with a visible reaching intercept. Blinder's pouch and dust marks remain attached
through the throw; the bare arm swings from behind the head to forward release.
Mimic's first three rows stay completely closed; ready exposes teeth and attack
unfolds the triangular lid before contact. The source/export pair now matches
the frosting/cherry shading even close up after repairing FBX normal damage.
These observations do not establish native Lumen parity or 18m/s timing.

Final offline compile `hunter-bodies-cake-003` has zero errors in all seven
assemblies and baseline-only warnings. `hunter-bodies-cake-pure-003` reports
43 passed, 0 failed, 48 environment, 0 skipped; 34 passes are the visual setup's
pure cases. Native-only fixtures have no pure coverage. `ast-grep scan` has
zero findings. The generated cake prefab/material folder is not present in this
isolated checkout; the comparison uses its unchanged authored source. The
coordinator must ensure `HorrorArtSetup.EnsureCake` output is imported before
native visual setup, not treat these offline results as a Unity pass.

`ArtSource/README.md` is outside this worker's ownership: coordinator should add
the three new source rows there; this owned inventory records them meanwhile.
