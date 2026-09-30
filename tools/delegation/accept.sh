#!/usr/bin/env bash
# accept.sh <slug> <n> <subject> <body-file> [--allow GLOB]... [--outcome accepted|partial]
# Commits a reviewed worker's changes on its own branch (never on main).
#   - refuses if HEAD moved since prepare, or the branch is not wt/<slug>;
#   - refuses any changed path outside owned-paths.txt, or on the forbidden list
#     (owned_paths.py), unless the coordinator passes --allow GLOB; each allowance is
#     recorded in the commit as a Scope-Exception trailer;
#   - stages exactly the changed files (no blanket `git add -A`), so the pre-commit
#     hook (ast-grep) gates the commit;
#   - adds provenance trailers (Worker-Model, Worker-Run, Plan) and finishes run.json,
#     appending a compact record to evidence/delegation-runs.jsonl in the main checkout.
set -euo pipefail
source "$(dirname "$0")/lib.sh"
SLUG="${1:?slug}"; N="${2:?n}"; SUBJECT="${3:?subject}"; BODY="${4:-}"
shift 4 || true
ALLOW=(); OUTCOME=accepted
while [ $# -gt 0 ]; do
  case "$1" in
    --allow) ALLOW+=("$2"); shift 2;;
    --outcome) OUTCOME="$2"; shift 2;;
    *) die "unknown option $1";;
  esac
done
RUN="$RUNS/$SLUG-$N"; WT="$WT_ROOT/$SLUG"; REC="$RUN/run.json"
BASE="$(head -1 "$RUN/baseline.txt")"
[ "$(git -C "$WT" rev-parse HEAD)" = "$BASE" ] || die "HEAD moved in $SLUG since prepare; refusing"
[ "$(git -C "$WT" rev-parse --abbrev-ref HEAD)" = "wt/$SLUG" ] || die "unexpected branch in $SLUG"

mapfile -t CHANGED < <(GIT_OPTIONAL_LOCKS=0 git -C "$WT" -c core.quotepath=off status --porcelain --untracked-files=all \
  | sed -E 's/^.. //; s/^"(.*)"$/\1/; s/.* -> //' | grep -v -x "$KNOWN_NOISE" || true)
[ "${#CHANGED[@]}" -gt 0 ] || die "nothing to commit"
ARGS=(); for g in "${ALLOW[@]}"; do ARGS+=(--allow "$g"); done
if ! printf '%s\n' "${CHANGED[@]}" | "$PY" "$(cygpath -m "$TOOLS/owned_paths.py")" check "$(cygpath -m "$RUN/owned-paths.txt")" "${ARGS[@]}"; then
  die "scope check failed (above). Revert those paths, or pass --allow GLOB with a reason in the body"
fi

git -C "$WT" add -- "${CHANGED[@]}"
MODEL="$("$PY" -c "import json;d=json.load(open(r'$(cygpath -m "$REC")'));print(d.get('model','?')+'/'+d.get('effort','?'))")"
PLAN="$("$PY" -c "import json;print(json.load(open(r'$(cygpath -m "$REC")')).get('plan') or '')")"
MSG="$SUBJECT"
[ -n "$BODY" ] && [ -f "$BODY" ] && MSG="$MSG"$'\n\n'"$(cat "$BODY")"
MSG="$MSG"$'\n\n'"Worker-Model: $MODEL"$'\n'"Worker-Run: $SLUG-$N"
[ -n "$PLAN" ] && MSG="$MSG"$'\n'"Plan: $PLAN"
for g in "${ALLOW[@]}"; do MSG="$MSG"$'\n'"Scope-Exception: $g"; done
MSG="$MSG"$'\n'"Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git -C "$WT" commit -q -m "$MSG"
SHA="$(git -C "$WT" rev-parse HEAD)"
runrec "$REC" outcome="$OUTCOME" commit="$SHA" accepted="$(now_iso)"
"$PY" "$(cygpath -m "$TOOLS/runrec.py")" ledger "$(cygpath -m "$REC")" "$(cygpath -m "$MAIN/evidence/delegation-runs.jsonl")"
git -C "$WT" log --oneline -1
git -C "$WT" show --stat --format= HEAD | tail -1
