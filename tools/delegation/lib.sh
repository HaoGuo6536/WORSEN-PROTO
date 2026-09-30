#!/usr/bin/env bash
# Shared paths and helpers for the Hermes delegation scripts (Git Bash on Windows).
# Override any path with the matching environment variable.

MAIN="${WORSEN_MAIN:-/c/Users/Hao Guo/Documents/UnityProjects/WORSEN-PROTO}"
WT_ROOT="${WORSEN_WT_ROOT:-/c/Users/Hao Guo/Documents/UnityProjects/WORSEN-wt}"
RUNS="${WORSEN_RUNS:-$(cygpath -u "$USERPROFILE")/.claude/delegations}"
TOOLS="$MAIN/tools/delegation"
PY="${PYTHON:-python}"

die() { echo "ERROR: $*" >&2; exit 1; }
now_iso() { date -u +%Y-%m-%dT%H:%M:%SZ; }
# runrec <run.json> key=value ...   (values that parse as JSON are stored typed)
runrec() { "$PY" "$(cygpath -m "$TOOLS/runrec.py")" set "$(cygpath -m "$1")" "${@:2}"; }
# Known noise in every worktree: Git LFS pointer mismatch on a template icon.
KNOWN_NOISE='Assets/TutorialInfo/Icons/URP.png'
