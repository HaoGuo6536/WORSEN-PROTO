#!/usr/bin/env bash
# retire-worktrees.sh [--apply]
# Removes worker worktrees whose work is safely kept elsewhere. Dry run by default.
# A worktree is retired only when all hold:
#   - its branch wt/<slug> is an ancestor of main;
#   - origin/wt/<slug> equals the local branch (the branch is pushed);
#   - it has no uncommitted or untracked changes other than ignored files and known noise;
#   - its Library is a junction (never a real directory).
# Retirement: copy its Logs/ to <main>/Logs/AgentValidation/worktrees/<slug>/, remove the
# Library junction itself (rmdir on a junction never touches the target), then
# `git worktree remove`. Branches stay, locally and on origin. wt/integration is skipped.
set -uo pipefail
source "$(dirname "$0")/lib.sh"
APPLY=0; [ "${1:-}" = --apply ] && APPLY=1
git -C "$MAIN" fetch -q origin 'refs/heads/wt/*:refs/remotes/origin/wt/*' || die "fetch failed"
kept=0; retired=0
for WT in "$WT_ROOT"/*/; do
  WT="${WT%/}"; SLUG="$(basename "$WT")"
  [ "$SLUG" = integration ] && continue
  [ -e "$WT/.git" ] || continue
  BR="wt/$SLUG"; why=
  git -C "$MAIN" rev-parse -q --verify "refs/heads/$BR" > /dev/null || why="no branch $BR"
  [ -z "$why" ] && ! git -C "$MAIN" merge-base --is-ancestor "$BR" main && why="not merged into main"
  [ -z "$why" ] && [ "$(git -C "$MAIN" rev-parse -q --verify "refs/remotes/origin/$BR")" != "$(git -C "$MAIN" rev-parse "$BR")" ] && why="not pushed"
  if [ -z "$why" ]; then
    DIRTY="$(GIT_OPTIONAL_LOCKS=0 git -C "$WT" status --porcelain --untracked-files=all | grep -v "$KNOWN_NOISE" | head -3)"
    [ -n "$DIRTY" ] && why="dirty: $(echo "$DIRTY" | tr '\n' ' ')"
  fi
  if [ -z "$why" ] && [ -e "$WT/Library" ]; then
    TYPE="$(powershell -NoProfile -Command "(Get-Item -LiteralPath '$(cygpath -w "$WT/Library")' -Force).LinkType" 2>/dev/null | tr -d '\r')"
    [ "$TYPE" = Junction ] || why="Library is not a junction ($TYPE)"
  fi
  if [ -n "$why" ]; then echo "KEEP   $SLUG: $why"; kept=$((kept + 1)); continue; fi
  echo "RETIRE $SLUG"; retired=$((retired + 1))
  [ "$APPLY" = 1 ] || continue
  if [ -d "$WT/Logs" ]; then
    mkdir -p "$MAIN/Logs/AgentValidation/worktrees/$SLUG"
    cp -r "$WT/Logs/." "$MAIN/Logs/AgentValidation/worktrees/$SLUG/" || { echo "  copy failed; kept"; continue; }
  fi
  if [ -e "$WT/Library" ]; then cmd //c rmdir "$(cygpath -w "$WT/Library")" || { echo "  junction removal failed; kept"; continue; }; fi
  git -C "$MAIN" worktree remove --force "$WT" || echo "  worktree remove failed"
done
[ "$APPLY" = 1 ] && git -C "$MAIN" worktree prune
echo "retire=$retired keep=$kept apply=$APPLY"
