# Hermes delegation tools

The coordinator (a Claude Code session) delegates implementation to one-shot Hermes workers on GPT-6 Astra and GPT-6.1 Sol. Each worker gets one isolated worktree, a written brief, and an owned-file list. Workers never run Unity; the coordinator integrates their branches through the [integration gate](../integration/README.md).

Run records live outside the repository in `%USERPROFILE%/.claude/delegations/<slug>-<n>/`: `task.md`, `brief.md`, `owned-paths.txt`, `baseline.txt`, `run.json`, `usage.json`, `child-output.txt`, `watchdog.log`. A compact line per finished run is appended to the tracked [evidence/delegation-runs.jsonl](../../evidence/delegation-runs.jsonl).

## Lifecycle

| Step | Command (Git Bash) | What it guarantees |
|---|---|---|
| 1. Write the task | `<runs>/<slug>-<n>/task.md` | Required sections: goal, `## Owned files` (backtick `WORKTREE/...` globs), tests. Optional `Plan:` and `Requires:` lines. |
| 2. Prepare | `tools/delegation/prepare.sh <slug> <n> [base_ref]` | Owned files present; every `Requires:` ref is in the base; existing worktree clean; brief assembled from the versioned [common rules](common-rules.md). |
| 3. Launch | `tools/delegation/launch.sh <slug> <n> <model> <effort> [task_class]`, as a session-bound background command | `run.json` records model, effort, base, brief hash, timing, exit and usage. Stall guard flags at 20 idle / 90 wall minutes and stops at 35 / 150. At most three launches per slug. |
| 4. Review | `compile.ps1`, `ast-grep scan`, `git diff` in the worktree | The coordinator reads the diff; the worker's report is a pointer, not evidence. |
| 5. Accept | `tools/delegation/accept.sh <slug> <n> "<subject>" <body> [--allow GLOB]` | HEAD unchanged; every changed path inside the owned list (exceptions recorded as `Scope-Exception:`); exact-file staging; the pre-commit lint runs; `Worker-Model`, `Worker-Run` and `Plan` trailers. |
| 5b. Close otherwise | `tools/delegation/record.sh <slug> <n> rejected\|superseded\|stalled [note]` | Every launch reaches the ledger. |
| 6. Integrate | `tools/integration/integrate.ps1` | Fail-closed candidate gate; see its README. |
| 7. Retire | `tools/delegation/retire-worktrees.sh [--apply]` | Only merged, pushed, clean worktrees; logs archived; Library junction removed safely. |

## Routing

| Task class (`task_class`) | Model / effort |
|---|---|
| `contained` — pure logic or a narrow fix, spec at most ~4 KB, testable without Unity | `gpt-6.1-sol` high |
| `wiring` — cross-system wiring, fixtures, Orchestrator or SceneRoot edits | `gpt-6-astra` high |
| `fix` — known-cause test or regression fixes | `gpt-6-astra` high |
| `design` — design-heavy, multi-system features; unknown-cause regressions | `gpt-6-astra` xhigh; split the task if it nears context compression |
| `diagnosis` — read-only investigation | `gpt-6-astra` high, `terminal,file` toolsets, `## Read-only` task |
| `art` — Blender generators under `tools/blender/` | `gpt-6-astra` high |

**Effort levels:** only `high` and `xhigh` are used. The medium A/B trial was stopped by the owner on 2026-09-30, and `medium` is no longer an option.

## Rules of thumb

- Keep briefs short and specific: exact files, symbols, owner decisions quoted verbatim, and the tests to add. The common rules are appended automatically.
- Base workers on the newest candidate that contains their prerequisites, and list those commits under `Requires:`.
- A worker that needs one line outside its scope should report it, not stop. The coordinator applies it or passes `--allow`.
- A stalled worker's worktree is still reviewable: compile and read it before relaunching, and relaunch with a narrower brief and a new run number.
