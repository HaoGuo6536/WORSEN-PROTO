"""Add [Worsen.Tests.Infrastructure.FixtureTimeGuard] to every test fixture class that lacks it.

Workers that run in parallel with a test-infrastructure change cannot know about the guard;
FixtureTimeSetUpTests.EveryDiscoveredFixtureHasAClockGuard then fails the candidate. Run this
on a coordinator branch cut from the merged candidate, review the diff and commit it.

  python tools/integration/add-fixture-guards.py [--root <checkout>] [--check]

--check lists unguarded fixtures and exits 1 if any exist, without editing.
Only classes named *Tests that contain test methods are touched; abstract classes and
helper classes are skipped. Indentation, line endings and BOMs are preserved.
"""
import argparse
import pathlib
import re
import sys

ATTRIBUTE = "[Worsen.Tests.Infrastructure.FixtureTimeGuard]"
DECL = re.compile(r"^([ \t]*)(?:public|internal)(?:\s+(?:sealed|static|partial))*\s+class\s+(\w+Tests)\b")
TESTS = re.compile(r"\[(Test|TestCase|UnityTest|TestCaseSource)\b")


def process(path, check):
    raw = path.read_bytes()
    text = raw.decode("utf-8-sig")
    if not TESTS.search(text):
        return []
    lines = text.splitlines(keepends=True)
    out, missing = [], []
    for i, line in enumerate(lines):
        m = DECL.match(line)
        if m and "abstract" not in line:
            j, attrs = i - 1, []
            while j >= 0 and lines[j].strip().startswith("["):
                attrs.append(lines[j])
                j -= 1
            if not any("FixtureTimeGuard" in a for a in attrs):
                missing.append(m.group(2))
                if not check:
                    out.append(m.group(1) + ATTRIBUTE + ("\r\n" if line.endswith("\r\n") else "\n"))
        out.append(line)
    if missing and not check:
        path.write_bytes((b"\xef\xbb\xbf" if raw.startswith(b"\xef\xbb\xbf") else b"") + "".join(out).encode("utf-8"))
    return missing


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=".")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    tests = pathlib.Path(args.root) / "Assets" / "Editor" / "Tests"
    found = []
    for p in sorted(tests.rglob("*Tests.cs")):
        for name in process(p, args.check):
            found.append(f"{p.relative_to(args.root)} {name}")
    for f in found:
        print(("MISSING " if args.check else "ADDED ") + f)
    print(f"fixtures {'missing' if args.check else 'guarded'}: {len(found)}")
    return 1 if (args.check and found) else 0


if __name__ == "__main__":
    sys.exit(main())
