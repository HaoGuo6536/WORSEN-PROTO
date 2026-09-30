# PLAN-011 enforcement verification and hand-off

Execution root: `C:/Users/Hao Guo/Documents/UnityProjects/WORSEN-wt/enforcement`

Branch/base verified: `wt/enforcement`, `7e7c4bd96bfaca213e9ca607183470efb4846a79`. Initial working tree was clean. No product source, assembly definition, Unity asset, planning document, integration script, hook, or main-checkout file was changed. No Unity process or lease was used.

## Current findings — initial Appendix A evidence

The complete table (path, alarm, measured value, symbol and status) is [current-findings.md](current-findings.md). The JSON evidence is [current-findings.json](current-findings.json). Programmatic read-back verified all **242 finding rows**, representing **149 distinct path/alarm pairs**. Multiple classes in a file share its using directives and therefore can produce separate fan-out findings. No Editor/Tests exemption was invented.

```text
ARCH_RESULT findings=242 waived=0 expired=0
```

Checker exit: **1**, intentionally: the existing Appendix A is the old three-column table and supplies no approved waivers.

| Alarm | Findings |
|---|---:|
| fan-out | 138 |
| responsibilities | 88 |
| asmdef | 5 |
| relay-surface | 10 |
| type-switch | 1 |

All five asmdef findings are `autoReferenced=true` warnings. There are no forbidden-reference or Session-order findings in this checkout.

```text
SESSION_EDGE Expedition -> HorrorEffects
SESSION_EDGE Expedition -> Progression
SESSION_EDGE Expedition -> Run
SESSION_EDGE HorrorEffects -> Progression
SESSION_EDGE Run -> Progression
SESSION_ORDER_PROPOSED ["Progression", "SceneFlow", "Settings", "HorrorEffects", "Run", "Expedition"]
```

The order above is recorded in `architecture.json`, not silently written to SPEC-001. Source paths for each edge appear in both reports.

## ast-grep

Installed version: **0.44.1**. Every existing/new rule has valid/invalid snippets and a snapshot. Initial plain tests correctly rejected absent snapshots; after generating and examining diagnostics, plain `ast-grep test` passed. The initial expanded scan found four violations (below); only the requested exact-path debt ignores were added.

Final result lines:

```text
Running 14 tests
PASS driver-no-instance-read  ..
PASS instantiate-only-in-allowed-types  ...
PASS lower-layer-no-orchestrator-ref  ..
PASS manager-fixed-time-tick-owner  ...
PASS manager-no-manager-creation  .....
PASS manager-resources-own-config  ..........
PASS menu-under-project-root  ...
PASS pure-layer-no-engine-calls  ..................................
PASS pure-layer-not-monobehaviour  ...
PASS scene-load-only-in-sceneflow  ...
PASS so-no-public-setter  ....
PASS state-declares-no-events  ...
PASS static-event-has-reset  ...
PASS subscribe-in-onenable-only  ...
test result: ok. 14 passed; 0 failed;
```

`ast-grep scan --json=compact`: `[]`, exit 0. `git diff --check`: exit 0.

| Path | Rule/alarm for Appendix A | Measured value | Owning symbol |
|---|---|---|---|
| Assets/Scripts/Domain/Hunter/Manager/HunterManager.cs | manager-no-manager-creation | 1 AddComponent call creating TickingManager, line 167 | Initialize |
| Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs | manager-no-manager-creation | 1 AddComponent call creating ShrineManager, line 454 | AssembleShrines |
| Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs | manager-resources-own-config | 2 foreign loads, ShrineConfig and ShrineDriverConfig, lines 449–450 | AssembleShrines |

The Resources rule checks ownership using current namespace/type declarations, not just a Config/Profile suffix. Its conservative allowlist must be extended with tests for future owned types. Qualified calls and whitespace are tested; aliases and file-scoped namespace allowances require follow-up if adopted. Existing ast-grep rules retain their prior scope/semantics except the requested purity widening.

## Python self-tests

```text
Ran 23 tests
OK
```

Coverage includes each alarm and threshold boundary, implicit-private/expression-bodied methods, sibling-method isolation, literal/comment masking, named/guarded relays, multi-event declarations, layer/vendor allowlists, GUID resolution, malformed asmdefs, Session edges/reversed order/cycles/missing systems, exact waivers, expiry-day boundaries, malformed waivers/configuration, missing inputs, CLI exit codes and JSON/Markdown outputs. Fixtures and temporary synthetic checkouts remain under owned tools/checks paths; production assets are untouched.

## Offline compilation

Required coordinator harness, run name `enforcement-001`:

```text
Worsen.Core: exit=0 sources=53 errors=0 warnings=0 (baseline 0)
Worsen.Domain: exit=0 sources=181 errors=0 warnings=41 (baseline 41)
Worsen.Session: exit=0 sources=44 errors=0 warnings=0 (baseline 0)
Worsen.Presentation: exit=0 sources=118 errors=0 warnings=34 (baseline 34)
Worsen.Orchestrator: exit=0 sources=17 errors=0 warnings=70 (baseline 70)
Worsen.Editor: exit=0 sources=51 errors=0 warnings=0 (baseline 0)
Worsen.Tests: exit=0 sources=247 errors=0 warnings=0 (baseline 0)
```

Exit 0, zero new warnings. Evidence: `Logs/AgentValidation/PLAN-002/offline-compile/enforcement-001/summary.json`. The harness reads the shared Library for references but writes only its worktree validation directory; it never launches Unity.

## Impact and dependent tests

No existing C# or Python class/method/function was modified: Python checker/test symbols are new. The existing changes are YAML/configuration. The read-only main-root GitNexus query for `tools/ast-grep/rules/pure-layer-no-engine-calls.yml` resolved to the **SPEC-001 documentation section**, returning LOW, direct=0, processes=0. This is not an indexed executable-rule target and is not treated as proof of no impact. Text search confirmed lint consumers in `tools/hooks/pre-commit`, `tools/integration/integrate.ps1`, and `tools/offline-compile/Lint-Snapshot.ps1`; none was edited.

`ArchitectureConformanceTests.cs` was read in full. No existing public product symbol changed behavior, so no dependent NUnit expectations were changed. No new Unity-test failure is predicted from this tools-only change. Unity execution and graph refresh are deferred to the coordinator under the user boundary; the saved runtime graph queries do not establish these new Python/YAML checks.

## Requests to other owners

1. SPEC-001: publish the approved A1–A4 text, adopt the observed Session order, replace the old Appendix A with the six-column schema, and assign owner/exit plan/review date to accepted debts from the complete measured table plus the three ast-grep debt rows above. No waiver approval or date was invented here.
2. Hunter owner: `Assets/Scripts/Domain/Hunter/Manager/HunterManager.cs`, `Initialize`: move creation of TickingManager to the authorized assembly owner, then remove this rule's exact-path ignore.
3. Session/scene owner: `Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs`, `AssembleShrines`: obtain ShrineManager and its two configs from their authorized owner instead of creating/loading them here, then remove the two exact-path ignores.
4. Coordinator: run Unity conformance/regression checks after integration. Existing test expectations remain unchanged.

## Provisional values and limits

No gameplay tunables were introduced. Alarm thresholds are exactly the owner-approved constants. The initial Session order is evidence-based and awaits its spec-side adoption. Lexical-analysis boundaries, whole-file debt-ignore scope and non-expiring static ast-grep ignores are documented in README.md; Python responsibility waivers do expire. No semantic-C# or Unity-wiring coverage is claimed.
