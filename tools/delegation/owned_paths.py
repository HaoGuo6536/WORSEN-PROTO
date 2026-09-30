"""Owned-path contract for delegated workers (tools/delegation).

A task.md lists the worker's owned files under a "## Owned files" heading as
backtick paths starting with WORKTREE/, with optional {a,b} alternatives and
** globs. accept.sh refuses a change outside that list unless the coordinator
passes an explicit --allow glob, which is then recorded in the commit.

  owned_paths.py extract <task.md>                 print one glob per line
  owned_paths.py check <owned.txt> [--allow G]...  read changed paths on stdin,
                                                   print violations, exit 3 if any
"""
import re
import sys

# Never accepted from a worker, whatever the brief says; the coordinator owns these.
FORBIDDEN = [
    "ProjectSettings/**", "Packages/**", "Library/**", "AGENTS.md", "CLAUDE.md",
    ".github/**", "tools/hooks/**", "tools/integration/**", "tools/delegation/**",
    "Assets/**/*.blend", "*.blend",
]


def expand_braces(pattern):
    m = re.search(r"\{([^{}]*)\}", pattern)
    if not m:
        return [pattern]
    out = []
    for alt in m.group(1).split(","):
        out.extend(expand_braces(pattern[:m.start()] + alt + pattern[m.end():]))
    return out


def glob_regex(glob):
    i, out = 0, []
    while i < len(glob):
        if glob.startswith("**/", i):
            out.append("(?:.*/)?")
            i += 3
        elif glob.startswith("**", i):
            out.append(".*")
            i += 2
        elif glob[i] == "*":
            out.append("[^/]*")
            i += 1
        elif glob[i] == "?":
            out.append("[^/]")
            i += 1
        else:
            out.append(re.escape(glob[i]))
            i += 1
    return re.compile("^" + "".join(out) + "$")


def extract(task_path):
    text = open(task_path, encoding="utf-8").read()
    section = re.search(r"^##\s+Owned files\s*$(.*?)(?=^##\s|\Z)", text, re.M | re.S)
    if not section:
        return []
    globs = []
    for raw in re.findall(r"`([^`]+)`", section.group(1)):
        raw = raw.strip()
        if not raw.startswith("WORKTREE/"):
            continue
        for g in expand_braces(raw[len("WORKTREE/"):]):
            # A folder-level ownership also covers Unity's sibling .meta for that folder.
            globs.append(g)
            if g.endswith("/**"):
                globs.append(g[:-3] + ".meta")
    return sorted(set(globs))


def check(owned_file, allows, paths):
    owned = [l.strip() for l in open(owned_file, encoding="utf-8") if l.strip()]
    allowed = [glob_regex(g) for g in owned + allows]
    forbidden = [glob_regex(g) for g in FORBIDDEN]
    bad = []
    for p in paths:
        p = p.strip().strip('"').replace("\\", "/")
        if not p:
            continue
        if any(f.match(p) for f in forbidden):
            bad.append(("forbidden", p))
        elif not any(a.match(p) for a in allowed):
            bad.append(("outside-owned", p))
    return bad


def main(argv):
    if len(argv) >= 3 and argv[1] == "extract":
        for g in extract(argv[2]):
            print(g)
        return 0
    if len(argv) >= 3 and argv[1] == "check":
        allows, rest = [], argv[3:]
        while rest:
            if rest[0] == "--allow" and len(rest) > 1:
                allows.append(rest[1])
                rest = rest[2:]
            else:
                sys.exit("unknown argument " + rest[0])
        bad = check(argv[2], allows, sys.stdin.read().splitlines())
        for kind, p in bad:
            print(f"{kind}: {p}")
        return 3 if bad else 0
    sys.exit(__doc__)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
