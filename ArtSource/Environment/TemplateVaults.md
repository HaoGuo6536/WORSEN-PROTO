# PLAN-026 template vault art handoff

## Summary

The four generators now add collision-bearing, explicitly marked optional vault obstacles. This is an art/manifest handoff, NOT a working-player-vault claim: the current runtime parser drops traversal metadata and the template geometry consumer creates untagged solid props. The coordinator/Procedural owner must implement the requests below before the owner playtest.

| Theme | Templates | Obstacles | Height | New kit piece | Final validator |
|---|---:|---:|---:|---|---|
| Hospital | 14 | 15 | 0.90 m | prop_vault_partition | PASS |
| School | 12 | 15 | 0.80 m | prop_vault_low_locker | PASS |
| Basement | 13 | 14 | 0.65 m | prop_vault_low_duct | PASS |
| Castle | 15 | 18 | 0.90 m | prop_vault_low_wall | PASS |

Density: one per non-closet template, two when the footprint has more than 20 two-metre cells; one closet per theme is exempt. Hospital has 11 single/2 double placements, School 7/4, Basement 10/2, Castle 10/4. All eligible hallways participate. These are catalogue counts, not measured counts on a generated Unity floor. Placement prioritizes central door-to-door routes with ordinary walking bypasses. The Castle guard-room table and one School science-lab chair are replaced to free shortcut lanes; gameplay anchors and door socket records are unchanged. The collapsed crossing avoids its missing/broken floor tiles.

## Files changed

- `tools/blender/env_theme_vaults.py`: shared themed meshes and deterministic placement search.
- `tools/blender/validate_env_theme_vaults.py`: measured bounds, metadata, floor support, run-up/landing/headroom, anchor/hub reservation, full door throats, walking bypass, connectivity and density checks; malformed-input controls.
- `tools/blender/env_theme_hospital.py`: create the partition and place it before manifest/source export; permit this isolated worktree.
- `tools/blender/env_theme_school.py`: create/place the low locker.
- `tools/blender/env_theme_basement.py`: create/place the low duct.
- `tools/blender/env_theme_castle.py`: create/place the low masonry wall.
- `tools/blender/validate_env_theme_hospital.py`: invoke shared gate and include per-template vault details in its report.
- `tools/blender/validate_env_theme_school.py`: invoke shared gate.
- `tools/blender/validate_env_theme_basement.py`: invoke shared gate.
- `tools/blender/validate_env_theme_castle.py`: invoke shared gate.
- All four `Assets/Art/Environment/<Theme>/Kit/<Theme>Kit.manifest.json`: add the themed prop with traversal/collision metadata.
- All four `Assets/Art/Environment/<Theme>/Rooms/<Theme>Rooms.manifest.json`: add validated placements and paired endpoints.
- All four kit FBX sets: regenerated with Blender 5.2.1 LTS, including the four new vault meshes. Existing FBX paths were overwritten, not deleted/recreated; existing `.meta` files were not changed.
- All four `ArtSource/Environment/<Theme>/Kit/<Theme>Kit.blend` and `Rooms/<Theme>Rooms.blend`: regenerated editable kit/room assemblies; no new blend source or inventory entry is needed.
- This file: evidence, provisional settings and blocking runtime integration contract. Blender generation/validation logs remain under `ArtSource/Environment/*-vault-*.log`; PNG review evidence remains in the generators' existing ignored `Logs/AgentValidation/Art/Env<Theme>/` output folders.

## Impact analysis

The eight existing generator/validator `main` symbols were queried upstream from the main project with file disambiguation. GitNexus reports CRITICAL for each, one direct caller: that script's module-level entry point. School initially returned UNKNOWN/ambiguous; a subsequent `--uid Function:tools/blender/env_theme_school.py:main` lookup returned exact/CRITICAL. Source search confirms the entry points and script invocation; cross-theme Python imports use existing support/render functions, which were not changed. Broad indexed C# process associations are not treated as proof of Python runtime calls. Scope remained strictly the owned art and Blender files; no C# changes.

## Tests

No NUnit fixture or assertion was changed. The new shared gate is exercised by every theme validator, with seven rejected malformed inputs per theme (height, landing blocker, anchor overlap, doorway relocation, disabled collision metadata, missing density, obstructed bypass). These controls establish rejection, not an isolated error diagnosis for every mutation.

All four complete Blender validators passed after the final placement/support changes, including imported-FBX dimensions, editable source parity, existing enclosure/support checks and current preview checks:

- Hospital: `hospital-vault-validation-003.log`
- School: `school-vault-validation-002.log`
- Basement: `basement-vault-validation-002.log`
- Castle: `castle-vault-validation-002.log`

Final headless run: `template-vaults-pure-003`, compile input `template-vaults-002`.
Filter: `^Worsen[.]Tests[.]Procedural[.]`
Result: `PURE_RESULT passed=250 failed=0 environment=268 skipped=0`

Required coverage: `ProceduralTemplateAnchorTests` 55 passed (including `EveryRequiredAnchorHasSupportedClearConnectedFloor` across all catalogues); `ProceduralTemplateConsumerTests` 38 passed; `ProceduralTemplateValidationUtilityTests` 59 passed/8 environment (including all four `CheckedInThemeManifestsPassProductionAdmission` cases passing); `ProceduralExitHubUtilityTests` 13 passed.

The first pure selection failed because Hospital lost its usable start hub. Placement now reserves the existing pre-vault player/exit pair; both subsequent full runs passed. The first Castle validator rejected a vault over the collapsed crossing's missing floor. The final placement map excludes missing/broken tiles and its successor validator passed. Failed logs/results were retained, not relabelled as passes.

Environment-dependent cases require Unity, not skips or assertion changes. Counts by fixture (all under `Worsen.Tests.Procedural`, suffix `Tests`): CastlePresenter 34; Controller 32; Driver 19; FootprintUtility 18; FreezeUtility 1; GenerationController 1; GeometryPresenter 4; GimmickUtility 2; InteractablePresenter 6; KitAssetSetup 5; NavFallbackIntegration 3; NavigationPresenter 6; OrganicShellPresenter 2; OrganicUtility 2; PassageNavigation 4; PassagePresenter 15; PuzzleLayoutPresenter 1; RoomBudget 48; RoutePresenter 2; ShrineSitePresenter 11; SpawnUtility 6; StoreyNavigation 5; StoreyPresenter 1; StoreyUtility 10; TemplateController 6; TemplateGeometryPresenter 9; TemplateValidationUtility 8; ThemeUtility 2; WorldObject 5. Fixtures with zero passes have no pure coverage in this run. Full case names and reasons are in `Logs/AgentValidation/PLAN-002/offline-compile/template-vaults-pure-003/summary.json`.

## Provisional values

No runtime config or profile was changed. New authoring/check defaults:

- `env_theme_vaults.py`, `HEIGHTS`/`build_vault`: Hospital/Castle 0.90 m, School 0.80 m, Basement 0.65 m; all outer envelopes 1.20 m wide x 0.50 m deep. Fixed mesh detail positions, relief sizes and material slots are authored in `build_vault`, not exposed gameplay tunables.
- `validate_env_theme_vaults.py`, `density`: closets 0; footprints <=20 cells 1; >20 cells 2. This density needs owner playtest acceptance.
- `env_theme_vaults.py`, `add_vaults`: candidate grid 0.25 m, starting at 1 m; yaw 0/90 degrees; minimum separation 3 m; central-distance score plus door-route penalty weight 2 and perpendicular-axis penalty weight 3. Endpoints sit 0.70 m beyond each depth face (0.95 m from centre for these props).
- `validate_env_theme_vaults.py`: standing navigation envelope radius 0.50 m, height 2 m; flood resolution 0.25 m; authored anchor clearance 0.60 m; additional run-up 0.50 m beyond either endpoint. Lifted-player envelope radius 0.30 m/height 1.80 m at obstacle top +0.08 m, sampled at nine positions. These are conservative offline checks, not PhysX evidence.
- Gate bounds: 0.35-1.20 m height; 0.30-0.60 m depth; minimum width 1 m. Full 3.20 m door opening retained for its first interior metre; five transverse samples spaced 0.80 m, with 0.05 m padding. Float tolerances: 1e-4 m bounds/endpoints/intersection padding, 1e-5 m base datum, 1e-8 parallel-ray threshold; floor-top support tolerance 0.05 m. Castle arc collision approximated by four segments, thickness 0.82 m.
- `hub_sites` mirrors checked-in pure hub selection defaults: player radius 0.60 m/height 2.80 m, exit radius 1.60 m/height 3 m, separation 3.20 m, cake distances 1 m/player and 1.50 m/exit. Reservations add 0.001 m padding. These values must stay aligned with future Procedural configuration changes; they do not select runtime shrine sites.

Verified player contract: `PlayerProfile.asset` height limits 0.35/1.20 m, mantle 2 m, vault duration 0.25 s, maximum design speed 14 m/s. Crucially, `PlayerDriver.cs:106-118` requires `ITraversalSurface.Kind == Vault` and a clear selected endpoint; untagged low props are NOT ground-vault candidates in this checkout.

## Requests to other owners

Blocking runtime request (no edits made):

1. `Assets/Scripts/Domain/Procedural/Definitions/ProceduralTemplateDefinitions.cs`, `ProceduralKitPiece` and `ProceduralTemplatePiece`: retain optional traversal/collision metadata and paired endpoints. Contract: kit rows keep `kind: "prop"`, add `traversal: "vault"`, `collision: true`; room placements repeat traversal/collision and add `endpointA`/`endpointB` as room-local Unity XYZ coordinates. They already include the piece yaw; do not rotate them a second time by `placement.RotY`.
2. `Assets/Editor/Procedural/ProceduralRoomManifestSetup.cs`, `Parse`/`Placement`: parse those optional fields, preserve legacy untagged props, reject malformed vault endpoints/metadata rather than silently dropping them.
3. `Assets/Scripts/Domain/Procedural/Driver/ProceduralTemplateGeometryPresenter.cs`, `Build`: for marked placements emit a collision-bearing vault `ProceduralBlock`, deterministic unique nonzero `SurfaceId`, `TraversalSurfaceKind.Vault`, paired endpoints transformed by the room's translation/rotation through `World`, and the existing kit ID/pivot/rotation. Current `kind: prop` already produces collision, but no traversal tag. `ProceduralDriver.CreateBlock` already attaches `ProceduralTraversalSurface` when SurfaceId is nonzero; no name-based ceiling-vault heuristic should be added.
4. `Assets/Scripts/Domain/Procedural/Controller/ProceduralTemplateValidationUtility.cs`, `Validate`: extend admission to the optional traversal contract, reject invalid vault dimensions/endpoints/collision flags, and add parser/geometry tests in the Procedural owner's fixtures, including rotated rooms and untagged legacy props.
5. Coordinator: import the four new FBXs to create their `.meta` files in Unity, keeping `bakeAxisConversion = false`; re-publish the catalogue through `ProceduralContentSetup.Build`. No metadata was hand-written here.

## Verification

Compile `template-vaults-002`:

    Worsen.Core: exit=0 sources=58 errors=0 warnings=0 (baseline 0)
    Worsen.Domain: exit=0 sources=233 errors=0 warnings=41 (baseline 41)
    Worsen.Session: exit=0 sources=56 errors=0 warnings=0 (baseline 0)
    Worsen.Presentation: exit=0 sources=137 errors=0 warnings=34 (baseline 34)
    Worsen.Orchestrator: exit=0 sources=18 errors=0 warnings=70 (baseline 70)
    Worsen.Editor: exit=0 sources=64 errors=0 warnings=0 (baseline 0)
    Worsen.Tests: exit=0 sources=358 errors=0 warnings=0 (baseline 0)

Blender 5.2.1 LTS generated all kit FBXs/manifests and existing kit/room blend sources, with previews. All four complete theme validators passed. Compile and pure evidence are under `Logs/AgentValidation/PLAN-002/offline-compile/`. Final `ast-grep scan`: 0 findings, exit 0. `git diff --check`: clean. Scope audit: 200 changed/untracked paths, all owned; no changed `.meta` files. No commits or staging.

## Open issues

- Runtime tagging/parser requests above are blocking: these obstacles currently import as ordinary solid props, not functional vault spots. No working traversal or owner-playtest completion is claimed.
- Coordinator-only Unity checks: catalogue import for all four themes; generated floor screenshot per theme showing the new obstacles; both-direction jump-press traversal at walking/sprinting approach; normal bypass and doorway navigation; selected player/exit/shrine/hunter clearance; all room rotations; asset and primitive-fallback collision parity. No Unity, Synaptic, lease or Library operations were performed.
- Highest-risk Unity fixtures: `ProceduralTemplateGeometryPresenterTests`, `ProceduralTemplateControllerTests`, `ProceduralKitAssetSetupTests`, `ProceduralNavigationPresenterTests`, `ProceduralShrineSitePresenterTests`, and the native cases in `ProceduralTemplateValidationUtilityTests`. The current pure tests do not assert new vault tagging, so their green result must not be mistaken for traversal integration evidence.
- Density and the two furnishing replacements remain provisional owner judgements. Offline route/connectivity checks are not a guarantee of native NavMesh or gameplay feel.

## Diff Report

`ArtSource/Environment/template-vaults-diff.log` contains only raw `git status --short`, `git diff --stat`, and new-file `wc -l` output. It includes regenerated FBX/source LFS changes; untracked files are not included by `git diff --stat`.
