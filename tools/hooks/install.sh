#!/usr/bin/env bash
# Points every checkout and worktree of this repository at these hooks (absolute path, so
# worktrees created from older commits use them too). The LFS hooks here replace .git/hooks.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
git -C "$ROOT" config core.hooksPath "$(cygpath -m "$ROOT/tools/hooks" 2>/dev/null || echo "$ROOT/tools/hooks")"
echo "core.hooksPath=$(git -C "$ROOT" config --get core.hooksPath)"
