#!/usr/bin/env bash
# launch.sh <slug> <n> <model> <effort> [task_class] [toolsets]
# Runs one prepared Hermes worker in the foreground (start it as a session-bound background
# command) and always finishes run.json: model, effort, timing, exit, outcome and usage.
#
# Stall guard (no model turns spent on polling):
#   - flags in watchdog.log after FLAG_IDLE_MIN (20) minutes without a file change in the
#     worktree, or FLAG_WALL_MIN (90) minutes of wall time;
#   - stops the worker after KILL_IDLE_MIN (35) idle minutes or KILL_WALL_MIN (150) wall
#     minutes and records outcome=stalled. The worktree is left untouched for review.
# Retry cap: refuses a fourth launch of one slug unless ALLOW_RETRY=1.
set -uo pipefail
source "$(dirname "$0")/lib.sh"
SLUG="${1:?slug}"; N="${2:?n}"; MODEL="${3:?model}"; EFFORT="${4:?effort}"
CLASS="${5:-unclassified}"; TOOLSETS="${6:-terminal,file}"
RUN="$RUNS/$SLUG-$N"; WT="$WT_ROOT/$SLUG"; REC="$RUN/run.json"
FLAG_IDLE_MIN="${FLAG_IDLE_MIN:-20}"; FLAG_WALL_MIN="${FLAG_WALL_MIN:-90}"
KILL_IDLE_MIN="${KILL_IDLE_MIN:-35}"; KILL_WALL_MIN="${KILL_WALL_MIN:-150}"
[ -f "$RUN/brief.md" ] && [ -f "$REC" ] || die "run $SLUG-$N is not prepared (tools/delegation/prepare.sh)"
grep -q '"started"' "$REC" && die "$SLUG-$N was already launched; prepare a new run number"
LAUNCHES="$(grep -l '"started"' "$RUNS/$SLUG"-*/run.json 2>/dev/null | wc -l)"
[ "$LAUNCHES" -lt 3 ] || [ "${ALLOW_RETRY:-0}" = 1 ] || die "$SLUG already launched $LAUNCHES times; split the task or set ALLOW_RETRY=1"
case "$EFFORT" in low|medium|high|xhigh) ;; *) die "effort must be low|medium|high|xhigh";; esac

START_EPOCH="$(date +%s)"
runrec "$REC" model="$MODEL" provider=openai-codex effort="$EFFORT" task_class="$CLASS" toolsets="$TOOLSETS" \
  started="$(now_iso)" outcome=running
OUTCOME=exited; EXIT=
finish() {
  local end; end="$(date +%s)"
  runrec "$REC" ended="$(now_iso)" wall_minutes="$(( (end - START_EPOCH) / 60 ))" exit="${EXIT:-null}" \
    outcome="$OUTCOME" output_bytes="$(wc -c < "$RUN/child-output.txt" 2>/dev/null || echo 0)"
  "$PY" "$(cygpath -m "$TOOLS/runrec.py")" usage "$(cygpath -m "$REC")" "$(cygpath -m "$RUN/usage.json")" || true
  echo "$SLUG-$N exit=${EXIT:-?} outcome=$OUTCOME $("$PY" "$(cygpath -m "$TOOLS/runrec.py")" show "$(cygpath -m "$REC")")"
}
trap finish EXIT

cd "$WT" || die "missing worktree $WT"
hermes -z "$(cat "$RUN/brief.md")" --usage-file "$(cygpath -m "$RUN/usage.json")" --model "$MODEL" \
  --provider openai-codex --reasoning "$EFFORT" -t "$TOOLSETS" > "$RUN/child-output.txt" 2> "$RUN/child-stderr.txt" &
HP=$!
WINPID="$(cat /proc/$HP/winpid 2>/dev/null || echo)"
runrec "$REC" pid="$HP" winpid="${WINPID:-null}"

flagged=
while kill -0 "$HP" 2>/dev/null; do
  sleep 60
  kill -0 "$HP" 2>/dev/null || break
  wall=$(( ($(date +%s) - START_EPOCH) / 60 ))
  # Any file touched in the worktree (sources, compile evidence) counts as activity.
  if find "$WT" \( -path "$WT/Library" -o -path "$WT/.git" \) -prune -o -type f -mmin "-$KILL_IDLE_MIN" -print -quit | grep -q .; then
    recent_kill=1; else recent_kill=0; fi
  if find "$WT" \( -path "$WT/Library" -o -path "$WT/.git" \) -prune -o -type f -mmin "-$FLAG_IDLE_MIN" -print -quit | grep -q .; then
    recent_flag=1; else recent_flag=0; fi
  if [ -z "$flagged" ] && { [ "$recent_flag" = 0 ] || [ "$wall" -ge "$FLAG_WALL_MIN" ]; }; then
    echo "$(now_iso) FLAG wall=${wall}m idle>=${FLAG_IDLE_MIN}m:$((1 - recent_flag))" >> "$RUN/watchdog.log"; flagged=1
  fi
  if [ "$recent_kill" = 0 ] || [ "$wall" -ge "$KILL_WALL_MIN" ]; then
    echo "$(now_iso) STOP wall=${wall}m no-file-change>=${KILL_IDLE_MIN}m:$((1 - recent_kill))" >> "$RUN/watchdog.log"
    OUTCOME=stalled
    [ -n "$WINPID" ] && taskkill //F //T //PID "$WINPID" > /dev/null 2>&1
    kill "$HP" 2>/dev/null
    break
  fi
done
wait "$HP"; EXIT=$?
[ "$OUTCOME" = stalled ] || { [ "$EXIT" = 0 ] && OUTCOME=exited-ok || OUTCOME=exited-error; }
