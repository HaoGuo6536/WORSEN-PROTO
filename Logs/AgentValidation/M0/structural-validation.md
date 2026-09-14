# M0 graph and structural validation

Observed 2026-09-14. Read-only source review; evidence writes only. No source, saved query, index, staging area, or commit was changed by this validation task.

## Result

- ast-grep scan --json: exit 0; complete output [] (zero findings).
- Saved query 1 (forbidden cross-layer references): zero rows.
- Saved query 2 (direct Domain system cycles): zero rows. Domain currently contains only its assembly definition and meta, so this does not exercise future Domain dependencies.
- Saved query 3 (Driver to game-system calls): zero rows.
- Saved query 4 (long Orchestrator methods): two indexed hits, manually reviewed below.
- Saved query 5 (pure classes without test references): one raw hit, RunSessionController. Follow-up confirms a saved-query false positive described below. The original saved query has not been edited or represented as passing.
- GitNexus check: status clean, enumeration complete, cycleCount 0, componentCount 0.

## Schema evidence

The MCP resource inventory did not expose the GitNexus schema URI. Live database introspection read show_tables, table_info('Method'), table_info('Class'), table_info('CodeRelation'), and actual relation types. The saved queries use existing labels and properties. The schema has Class, Method, Constructor and the shared CodeRelation table, with r.type discriminators and filePath/startLine/endLine properties. Initial MCP relation counts total 8,551; the final disk index has 8,560, including 56 IMPORTS. See schema-introspection.json and relationship-counts-cli.json.

Parent refreshed the index at 20:05:57 UTC with 4,066 nodes and 8,560 edges. The long-lived MCP server continued returning the older 8,551-edge snapshot and the old SceneRoot span. All five saved queries were therefore rerun through a fresh CLI process against the current disk index; those final results are in graph-query-results-cli.json. No index refresh was performed here. The project runner mishandles Windows quoting for Cypher, so the verified installed CLI entry point was invoked directly with Node for these reads.

## Query 4 review

1. TagArenaSceneRoot.Start: line 47, final indexed span 13 (the stale MCP snapshot reported 17). It explicitly initializes the four serialized service references (falling back to canonical instances only when the scene-local reference has already been destroyed), validates wiring, sends the no-player overlay handoff, and publishes readiness. This is allowed SceneRoot assembly/lifecycle work, not gameplay computation.
2. InputOrchestrator.OnEnable: indexed at line 35, span 14. It checks wiring, rejects a duplicate persistent input root, acquires canonical service references, and pairs event subscription with OnDisable. Event handlers route frames, readiness, and load facts. No gameplay math or stored gameplay state was found.

## Query 5 false positive

RunSessionControllerTests creates RunSessionController through its Create helper and exercises phase transitions, fixed ticks, buffered input, scene handoffs, invalid durations and deterministic replay. The graph records 24 test CALLS to the controller's Constructor and Methods. It does not record a direct incoming edge to the Class node from a test, which is the sole shape accepted by saved query 5.

The controller Class owns its Constructor and Methods through HAS_METHOD edges (verified in graph-member-ownership.json). A follow-up query preserving direct class-reference checks while also recognizing tests referencing those owned members returns zero untested types. Its exact query and result are in query5-member-coverage-followup.json. This does not claim tests passed; Unity test execution is separate.

## detect_changes scope limitation

The exact MCP detect_changes(scope=all, repo=WORSEN-PROTO) result reports changed_count 0, affected_count 0, changed_files 9, risk_level low, and no partial/truncated flags. The final fresh CLI check also exits 0 and prints "No changes detected." These outputs are not M0 blast-radius evidence: the runtime/test sources are currently untracked and therefore absent from the default git diff.

The full git status snapshot has 13,897 entries: 25 tracked dirty entries and 13,872 untracked entries. Of the latter, 13,686 are inside the preexisting Synaptic AI Pro vendor tree and 186 are elsewhere. The Assets/Scripts and Assets/Editor scope contains 37 untracked C# files. These counts include existing work and must not all be attributed to this task. No unrelated files were staged or changed to make detect_changes see them.

Source-wide ast-grep and the refreshed graph's conformance queries cover indexed source independently of the tracked-diff limitation. GitNexus does not establish Unity callback timing, serialized references, actual test execution, or scene/prefab wiring; parent owns live checks.

## Input update review

PlayerInputDriver.CaptureMouseLook subscribes once to InputSystem.onAfterUpdate, skips Editor and BeforeRender updates, and samples each bound DeltaControl once for gameplay input updates. This avoids summing Input System's cumulative intermediate mouse callbacks. Gate closure clears buffered state and disables the action map. Physical device behavior and scene lifecycle are verified by parent live tests, not by this graph review.

## Evidence

- ast-grep.json
- schema-introspection.json
- graph-query-results.json (all five unchanged saved queries and raw MCP results)
- graph-followup.json (24 test-to-controller-member calls, pure types, Domain inventory)
- graph-member-ownership.json
- query5-member-coverage-followup.json
- cycle-check.json
- detect-changes-all.json
- git-status-full.txt
- source-status.txt
- index-snapshot.json
- graph-query-results-cli.json and the five individual *.cli.json results (final disk index)
- relationship-counts-cli.json
- query5-member-coverage-cli.json
- cycle-check-cli.json
- detect-changes-all-cli.json
