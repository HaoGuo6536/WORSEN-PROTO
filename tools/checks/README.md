# Offline architecture gates

Authority: [SPEC-001](../../PLANNING/specs/SPEC-001-project-architecture-guidelines.md), with the owner-approved PLAN-011 amendments A1–A4. The coordinator owns the spec update. These tools do not modify product code, run Unity, inspect its Library, or acquire a Unity lease.

## Run

From the checkout root, with Python 3 (standard library only) and the installed ast-grep:

```text
ast-grep test
ast-grep scan
python tools/checks/test_architecture.py
python tools/checks/architecture.py --root . --json tools/checks/current-findings.json --markdown tools/checks/current-findings.md
```

`--json` and `--markdown` are optional output files. All paths in findings are checkout-relative with forward slashes. The summary is `ARCH_RESULT findings=N waived=N expired=N`: findings counts all findings, waived is the subset covered by active waivers, and expired counts expired ledger rows (even if their debt has since disappeared). Exit 1 means at least one unwaived finding, including warning-level findings; exit 0 means none. Input errors cannot be waived. An expired row stops applying on the day after its Review-by date, using the local calendar date. The API accepts an injected date for deterministic self-tests.

## Coverage

- **asmdef:** parse project definitions in `Assets/Scripts`, `Assets/Editor` (including Tests), and `Assets/Tests`. Runtime layer is determined by the owning folder, not a user-controlled assembly name. Core references nothing; Domain permits Core and `DistantLands.Lumen.Runtime`; Session permits Core and Domain; Presentation permits Core plus the pinned vendor list below; Orchestrator permits the other runtime layers. Editor/Tests references are unrestricted. Explicit or default `autoReferenced: true` produces a warning that still fails the gate unless waived. GUID references are resolved through `.asmdef.meta` files under Assets; unresolved GUIDs fail closed. Vendor assets are only read for this identity lookup, not linted. No Library traversal.
- **session-order:** `architecture.json:sessionOrder` is dependency-first. References through `using` (including aliases/static imports) and fully qualified `Worsen.Session.B` names produce A → B edges. Own-system references and namespace declarations are excluded. Missing systems, reversed edges and cycles fail. Actual edge paths and a deterministic topological proposal are reported; cycles have no proposed order. Nested namespaces such as Progression.Shop belong to the top-level Progression system.
- **responsibilities:** more than five bullets in the leading `//` header's KEY RESPONSIBILITIES section. Continued lines and other header sections do not count. This does not replace the full §0 header conformance test; short headers are not authorized.
- **fan-out:** more than five distinct other `Worsen.<Layer>.<System>` namespaces from using directives and class-body qualified names, excluding Core and the owning system. Each class is reported separately. File-level imports apply to every class in that file, including nested fixture classes. Editor and Tests are included: A3 grants them no exemption.
- **relay-surface:** a `*Manager` class with more than 15 public events, or more than half its subscribed named handlers doing only an event raise. Method-group subscriptions identify handlers; event counts include multiple declarators and custom accessors. Straight raises, null-conditional Invoke, expression-bodied handlers and simple pause/null guards are recognized. Handlers doing extra work are not pure relays. Measurements show the public-event count, relay count, denominator and relay names.
- **type-switch:** at least three `is NameConfig/NameState/NameProfile` tests in one block- or expression-bodied method, including implicit-private methods and qualified type names. No aggregation across sibling methods.

Presentation's existing non-Core references, pinned in `architecture.py` rather than learned from whatever an edited asmdef happens to permit:

- `Unity.InputSystem`
- `Unity.Cinemachine`
- `Unity.RenderPipelines.Core.Runtime`
- `Unity.RenderPipelines.Universal.Runtime`
- `FronkonGames.Weird.DitherFog`
- `DistantLands.Lumen.Runtime`

## Waivers

Only the table under this exact heading in SPEC-001 is authoritative:

```markdown
## Appendix A — Known Debt (migrate when touched)

| ID | Path | Alarm | Owner | Exit plan | Review by |
|---|---|---|---|---|---|
| A-01 | Assets/Scripts/Session/Run/Manager/RunSessionManager.cs | relay-surface | coordinator | Split the Run fact relay (L1-03) | 2026-11-30 |
```

Paths and alarm names match exactly; no globs or inherited exemptions. A row covers all matching class/method findings in its file. Invalid rows fail closed. The legacy three-column debt table does not grant waivers. The checked-in findings report is evidence for the coordinator, not an automatically approved ledger. Owners, exit plans and review dates must be assigned by the coordinator; the checker never invents them.

## ast-grep rules and debt

Every rule has a valid/invalid test file and a reviewed snapshot baseline under `tools/ast-grep/rule-tests`. After intentionally changing diagnostic ranges, use `ast-grep test --update-all`, inspect the changed snapshots, then run plain `ast-grep test` again. The test runner does not exercise file globs or ignores; `ast-grep scan` does.

The existing pure-layer rule now covers unqualified Random calls/properties, object lookup variants, GameObject.Find*, DateTime.Now/UtcNow and Stopwatch. Its file scope is unchanged.

The three Manager rules enforce Config/Profile ownership, fixed-time tick ownership and no Manager creation. `manager-resources-own-config.yml` uses a fail-closed namespace/type allowlist derived from current Config declarations: ast-grep alone cannot resolve unqualified C# types across files. Add a namespace/type pair and a valid/invalid test when adding an owned Config/Profile type. Nested systems use their top-level system identity, consistently with A3. Aliases and file-scoped namespaces are not currently an allowance path. A new unknown type is rejected rather than accepted merely because its name ends in Config. Structural lint is not a replacement for semantic type resolution.

Current debt ignores (whole-file, rule-specific, explicitly requested for migration):

| Path | Rule/alarm | Measured violation |
|---|---|---|
| Assets/Scripts/Domain/Hunter/Manager/HunterManager.cs | manager-no-manager-creation | 1 call: Initialize creates Archetypes.Ticking.TickingManager, line 167 |
| Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs | manager-no-manager-creation | 1 call: AssembleShrines creates ShrineManager, line 454 |
| Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs | manager-resources-own-config | 2 calls: AssembleShrines loads ShrineConfig and ShrineDriverConfig, lines 449–450 |

Each ignore has `# debt: SPEC-001 Appendix A`. No pure-layer or fixed-time offender was found. The fixed-time rule's single RunSessionManager exclusion is authorized policy, not debt. These static ast-grep ignores do not automatically expire; the Python checker's date-aware Appendix A waivers apply to its findings. Remove the matching ignore when resolving a debt item. The coordinator must add the three rows above to Appendix A.

## Limits and interpretation

This is a dependency/alarm checker, not a C# parser or type checker. It masks comments and literals and balances method braces. Conditional compilation branches are both inspected. It does not resolve arbitrary namespace aliases, inheritance, partial-class aggregation, cross-file event subscriptions, lambda handlers, reflection, or C# semantic type identity. Interpolated string contents are masked, including interpolation expressions. Relay classification is intentionally conservative for complex bodies; no guessed handler is added to the denominator. Generated Unity wiring still needs the coordinator's Unity tests. Existing architecture NUnit tests are unchanged.

No line-count alarm exists. Thresholds are the approved constants (5 responsibilities, 5 foreign systems, 15 public events, strictly more than half relay handlers, 3 type tests), not game tunables. The Session order is the initial observed topological order and requires coordinator adoption in SPEC-001.

## Evidence

- [Full measured findings table](current-findings.md)
- [Machine-readable findings, waiver status and edge paths](current-findings.json)
- [Verification and owner hand-off](verification.md)
