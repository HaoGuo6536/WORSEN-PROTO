# Evidence

Tracked, curated verification records. Raw logs, compiler snapshots and NUnit XML stay in the git-ignored `Logs/`, which exists only on the machine that produced it. Plans should cite these ledgers, or a short summary here, rather than a `Logs/` path.

| File | Written by | One line per |
|---|---|---|
| `gate-ledger.jsonl` | `tools/integration/integrate.ps1` | integration gate run (candidate, test totals, failing names, verdict, promotion) |
| `build-ledger.jsonl` | `tools/integration/build-smoke.ps1` | player build and headless smoke run |
| `delegation-runs.jsonl` | `tools/delegation/accept.sh`, `record.sh` | Hermes worker run (model, effort, task class, tokens, calls, compressions, wall time, outcome, commit) |

The first `gate-ledger.jsonl` entry is a bootstrap baseline taken from the batch 11 results, which ran before the fail-closed gate existed.
