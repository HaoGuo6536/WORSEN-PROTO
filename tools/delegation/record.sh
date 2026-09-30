#!/usr/bin/env bash
# record.sh <slug> <n> <outcome> [note]
# Closes a run that was not accepted (rejected, superseded, stalled, killed) so the
# delegation ledger counts every launch. Finishes run.json usage if launch.sh could not.
set -euo pipefail
source "$(dirname "$0")/lib.sh"
SLUG="${1:?slug}"; N="${2:?n}"; OUTCOME="${3:?outcome}"; NOTE="${4:-}"
RUN="$RUNS/$SLUG-$N"; REC="$RUN/run.json"
[ -f "$REC" ] || die "no run.json for $SLUG-$N"
grep -q '"ended"' "$REC" || runrec "$REC" ended="$(now_iso)" exit=null
"$PY" "$(cygpath -m "$TOOLS/runrec.py")" usage "$(cygpath -m "$REC")" "$(cygpath -m "$RUN/usage.json")" || true
runrec "$REC" outcome="$OUTCOME" note="$NOTE"
"$PY" "$(cygpath -m "$TOOLS/runrec.py")" ledger "$(cygpath -m "$REC")" "$(cygpath -m "$MAIN/evidence/delegation-runs.jsonl")"
"$PY" "$(cygpath -m "$TOOLS/runrec.py")" show "$(cygpath -m "$REC")"
