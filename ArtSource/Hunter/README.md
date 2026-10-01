# Hunter sources — detailed prototype pass 2 (PLAN-017)

Owner request: 2026-10-01. These are revisions of the seven already inventoried
sources, not new source assets. No third-party geometry or downloads are used.
The Mimic retains the project's own cake surface and packed palette images.

## Inventory

Each row's source is `<Hunter>/WORSEN_Hunter<Hunter>.blend` in this folder.
Its runtime export and measured manifest are
`Assets/Art/Hunter/<Hunter>/WORSEN_Hunter<Hunter>.fbx` and `.manifest.json`.

| Hunter | Triangles (FBX re-import) | Used materials | Pass-2 modelling |
|---|---:|---:|---|
| Echo | 4952 | 3 | Pleated blue-grey cover, irregular hem, hanging tatters, hood welt and staggered recording scars; joint volumes replace floating limb ends. |
| Herald | 6408 | 3 | Curved tapered rib doors, contrasting bone, vertebrae, throat cords, jaw frame and small teeth; original chest-opening bones retained. |
| Mannequin | 7556 | 3 | Tapered shells, dark joint hardware, screw slots, segmented abdomen, fingers and draped dust sheet. |
| Mimic | 6214 | 4 | Original layered cake and palette UVs, additional cream piping, exposed jam drips and finer concealed teeth. |
| Stare | 5592 | 4 | Tall asymmetric pleated cover, long pale face, recessed dark orbits, fixed tiny eyes, nose and fingers. |
| Ticking | 7836 | 4 | Bevelled wooden case and dial, open toothed side gears, rivets, articulated brass fingers, pendulum and rear winding key. |
| Weaver | 7432 | 4 | Shaped shell plates and flutes, tapered three-segment legs, joint spurs, curved mandibles and six eyes. |

## Contract and provenance

- Blender 5.2.1 LTS headless; metre-scale FBX, -Z forward, Y up,
  `bake_space_transform=True`, importer `bakeAxisConversion=false`.
- Existing armature identities, bones/rest positions/hierarchy, sockets, six clip
  names/ranges/loop flags and 30 fps are retained. Animation authoring functions
  are unchanged from accepted pass 1. Source/FBX motion agreement is independently
  measured by the original validators.
- Each rigid mesh section has one normalized bone weight. Hardware, fingers,
  ribs, jaw and cloth sections use their original owning bones; no new rig or
  runtime animation controller is introduced. Cloth is segmented rigid prototype
  art, not simulated fabric.
- Existing `.meta` files are untouched. Unity import, material creation/remapping,
  prefab rebuild and in-game lighting acceptance belong to the coordinator.
- The owner-requested Stare face and Mimic tell supersede the older non-facial /
  perfectly indistinguishable descriptions. Mimic's original CakeBase/CakeTop
  geometry, UVs, textures and overall closed bounds remain exact; added decoration
  is separately bounded and the jam drip must actually stand proud of the sponge.

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
A cake-sized Mimic is necessarily small at 10m; its frosting/jam tell is close-range
information, not a promised readable distant face. Tiny fasteners and eyes are
secondary details; silhouette and motion must carry recognition.

### Observed visual review

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
