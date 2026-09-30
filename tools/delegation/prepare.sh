#!/usr/bin/env bash
# prepare.sh <slug> <n> [base_ref]
# Prepares one Hermes worker run from <runs>/<slug>-<n>/task.md:
#   - checks prerequisites: task.md has "## Owned files"; every ref on a "Requires:" line
#     is an ancestor of the base; an existing worktree is clean and on wt/<slug>;
#   - creates worktree <wt_root>/<slug> on branch wt/<slug> from base_ref (default main)
#     and junctions its Library to the main checkout's Library;
#   - writes brief.md (task.md + tools/delegation/common-rules.md), owned-paths.txt,
#     baseline.txt and the initial run.json.
set -euo pipefail
source "$(dirname "$0")/lib.sh"
SLUG="${1:?slug}"; N="${2:?run number}"; BASE_REF="${3:-main}"
RUN="$RUNS/$SLUG-$N"; WT="$WT_ROOT/$SLUG"; TASK="$RUN/task.md"
[ -f "$TASK" ] || die "missing $TASK"
[ ! -f "$RUN/run.json" ] || ! grep -q '"started"' "$RUN/run.json" || die "$SLUG-$N already launched; use a new run number"

# ---- prerequisites ----
BASE_SHA="$(git -C "$MAIN" rev-parse --verify "$BASE_REF^{commit}")" || die "unknown base $BASE_REF"
"$PY" "$(cygpath -m "$TOOLS/owned_paths.py")" extract "$(cygpath -m "$TASK")" > "$RUN/owned-paths.txt"
if [ ! -s "$RUN/owned-paths.txt" ] && ! grep -qi '^## Read-only' "$TASK"; then
  die "task.md has no '## Owned files' backtick WORKTREE/ paths (or mark the task '## Read-only')"
fi
REQ="$(grep -m1 -i '^Requires:' "$TASK" | sed 's/^[Rr]equires:[[:space:]]*//' | tr ',' ' ' || true)"
for ref in $REQ; do
  sha="$(git -C "$MAIN" rev-parse --verify -q "$ref^{commit}")" || die "Requires: unknown ref $ref"
  git -C "$MAIN" merge-base --is-ancestor "$sha" "$BASE_SHA" || die "Requires: $ref is not in base $BASE_REF"
done

# ---- worktree ----
if [ ! -d "$WT" ]; then
  git -C "$MAIN" worktree add -q -b "wt/$SLUG" "$WT" "$BASE_SHA"
else
  BR="$(git -C "$WT" rev-parse --abbrev-ref HEAD)"
  [ "$BR" = "wt/$SLUG" ] || die "worktree on $BR, expected wt/$SLUG"
  DIRTY="$(GIT_OPTIONAL_LOCKS=0 git -C "$WT" status --porcelain --untracked-files=all | grep -v "$KNOWN_NOISE" || true)"
  [ -z "$DIRTY" ] || die "worktree $SLUG is dirty; accept or clean it first"
  if ! git -C "$WT" merge-base --is-ancestor "$BASE_SHA" HEAD; then
    echo "note: base $BASE_REF is not in wt/$SLUG; merge it first if the task needs it" >&2
  fi
fi
if [ ! -e "$WT/Library" ]; then
  cmd //c mklink //J "$(cygpath -w "$WT/Library")" "$(cygpath -w "$MAIN/Library")" > /dev/null
fi

# ---- brief and records ----
WTM="$(cygpath -m "$WT")"
{ sed -e "s#^WORKTREE:#@@ROOT@@:#" -e "s#WORKTREE#$WTM#g" -e "s#@@ROOT@@#Execution root#" "$TASK"
  printf '\n\n'
  sed "s#WORKTREE#$WTM#g" "$TOOLS/common-rules.md"; } > "$RUN/brief.md"
HEAD_SHA="$(git -C "$WT" rev-parse HEAD)"
{ echo "$HEAD_SHA"; echo "wt/$SLUG"; GIT_OPTIONAL_LOCKS=0 git -C "$WT" status --short --untracked-files=all; } > "$RUN/baseline.txt"
PLAN="$(grep -m1 -i '^Plan:' "$TASK" | sed 's/^[Pp]lan:[[:space:]]*//' || true)"
runrec "$RUN/run.json" run="$SLUG-$N" slug="$SLUG" base_ref="$BASE_REF" base_sha="$BASE_SHA" head_sha="$HEAD_SHA" \
  brief_sha256="$(sha256sum "$RUN/brief.md" | cut -d' ' -f1)" brief_bytes="$(wc -c < "$RUN/brief.md")" \
  owned_globs="$(wc -l < "$RUN/owned-paths.txt")" plan="$PLAN" prepared="$(now_iso)"
echo "prepared $SLUG-$N: wt=$WTM head=${HEAD_SHA:0:7} brief=$(wc -c < "$RUN/brief.md")B owned=$(wc -l < "$RUN/owned-paths.txt") globs"
