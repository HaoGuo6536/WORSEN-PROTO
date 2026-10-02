#!/usr/bin/env python3
# ============================================================================
# select-tests.py
# ============================================================================
# PURPOSE:
#   Select conservative Unity fixtures from candidate source, not a stale index.
#   Preserve explanations and widen to the full suite when classification fails.
# ARCHITECTURAL ROLE: Integration tooling; offline test selection, no runtime layer.
# KEY RESPONSIBILITIES:
#   - Read immutable Git snapshots or an explicit working-tree change list.
#   - Discover fixtures and type references without third-party dependencies.
#   - Include system, one-hop consumer, wiring and mandatory smoke coverage.
#   - Emit deterministic, fail-closed JSON suitable for gate evidence.
# DEPENDENCIES: Python standard library, Git, project folder/namespace conventions.
# USAGE NOTES: This is a conservative lexical scan, not a C# compiler. Unsupported
#   fixture declarations force full. GitNexus is never used to narrow coverage.
# ============================================================================
import argparse
import json
import re
import subprocess
from pathlib import Path, PurePosixPath

TESTS = 'Assets/Editor/Tests/'
# Exact gate-owned overlay only, never a directory-wide infrastructure exemption.
GATE_INJECTED = frozenset({'Assets/Editor/Testing/NativeTestRunnerSetup.cs',
                           'Assets/Editor/Testing/NativeTestRunnerSetup.cs.meta'})
# Existing Environment coverage predates the general system name.
SYSTEM_FIXTURE_ALIASES = {'Environment': ('CastleEnvironment',)}
SMOKE = (
    'Worsen.Tests.Architecture.ArchitectureConformanceTests',
    'Worsen.Tests.Infrastructure.FixtureTimeSetUpTests',
    'Worsen.Tests.Run.RunFactRelayWiringTests',
)
# Keep strings in the reference scan (reflection and source-inspection tests),
# but mask them, along with comments, while discovering declarations/braces.
LEXICAL = re.compile(r'//[^\n]*|/\*.*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'', re.S)
TYPE = re.compile(r'\b(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+([A-Za-z_]\w*)\s*(?=[<{:(;])')
DELEGATE = re.compile(r'\bdelegate\s+[\w.<>,?\[\]\s]+?\s+([A-Za-z_]\w*)\s*(?:<[^;{}()]+>)?\s*\(')
CLASS = re.compile(r'\bclass\s+([A-Za-z_]\w*)([^{};]*)\{')
TEST_ATTRIBUTE = re.compile(r'(?:\[|,)\s*(?:(?:NUnit\.Framework|UnityEngine\.TestTools)\.)?(?:Test|TestCase|TestCaseSource|UnityTest|TestFixture|TestFixtureSource)(?:Attribute)?\b')


def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args], stderr=subprocess.PIPE)


def snapshot(root, ref=None):
    if ref is None:
        return {p.relative_to(root).as_posix(): p.read_text(encoding='utf-8-sig')
                for directory in ('Assets/Scripts', 'Assets/Editor')
                for p in (root / directory).rglob('*.cs')}
    tree = git(root, 'ls-tree', '-r', '-z', ref, '--', 'Assets/Scripts', 'Assets/Editor')
    entries = []
    for entry in tree.split(b'\0'):
        if not entry:
            continue
        info, path = entry.split(b'\t', 1)
        if path.endswith(b'.cs'):
            entries.append((path.decode('utf-8'), info.split()[2]))
    # One Git process, no checkout, no imports and no Library access.
    data = subprocess.run(['git', '-C', str(root), 'cat-file', '--batch'],
                          input=b'\n'.join(oid for _, oid in entries) + b'\n',
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True).stdout
    result, pos = {}, 0
    for path, _ in entries:
        end = data.index(b'\n', pos)
        size = int(data[pos:end].split()[2])
        pos = end + 1
        result[path] = data[pos:pos + size].decode('utf-8-sig')
        pos += size + 1
    return result


def code_only(text):
    return LEXICAL.sub(lambda m: ' ' * len(m.group()), text)


def declarations(text):
    code = code_only(text)
    return set(TYPE.findall(code)) | set(DELEGATE.findall(code))


def test_declarations(text):
    # External consumers of nested test helpers must name/import their enclosing
    # type. Seeding unqualified private helpers (Fixture, Player, Level) would
    # mistake unrelated namespace words for dependencies across the whole suite.
    code = code_only(text)
    namespace = re.search(r'\bnamespace\s+[\w.]+\s*([{;])', code)
    if not namespace:
        return declarations(text)
    depth = 1 if namespace.group(1) == '{' else 0
    return {m.group(1) for m in list(TYPE.finditer(code)) + list(DELEGATE.finditer(code))
            if code[:m.start()].count('{') - code[:m.start()].count('}') == depth}


def fixtures(path, text):
    """Discover ordinary top-level fixtures; reject ambiguous/nested/generic ones."""
    code = code_only(text)
    if re.search(r'\busing\s+\w+\s*=\s*(?:NUnit\.Framework|UnityEngine\.TestTools)', code) or '"""' in text:
        raise ValueError('unsupported test attribute alias/raw literal: ' + path)
    namespaces = re.findall(r'\bnamespace\s+([\w.]+)\s*[{;]', code)
    if len(namespaces) != 1 or not namespaces[0].startswith('Worsen.Tests.'):
        if TEST_ATTRIBUTE.search(code):
            raise ValueError('unsupported test namespace: ' + path)
        return []
    classes = []
    for match in CLASS.finditer(code):
        depth, end = 1, match.end()
        while end < len(code) and depth:
            depth += (code[end] == '{') - (code[end] == '}')
            end += 1
        if depth:
            raise ValueError('unbalanced class: ' + path)
        classes.append((match, end))
    found = []
    for match, end in classes:
        body = code[match.end():end - 1]
        for child, child_end in reversed(classes):
            if match.end() <= child.start() < end:
                start = child.start() - match.end()
                body = body[:start] + ' ' * (child_end - child.start()) + body[child_end - match.end():]
        # Include fixture attributes immediately before the class declaration.
        prefix = code[max(code.rfind('}', 0, match.start()), code.rfind(';', 0, match.start()),
                          code.rfind('{', 0, match.start())) + 1:match.start()]
        if not TEST_ATTRIBUTE.search(prefix + body):
            continue
        if '<' in match.group(2) or any(parent.start() < match.start() < parent_end
                                      for parent, parent_end in classes):
            raise ValueError('generic/nested fixture requires full discovery: ' + path)
        if re.search(r'\babstract\b', prefix):
            raise ValueError('inherited fixture requires full discovery: ' + path)
        found.append(namespaces[0] + '.' + match.group(1))
    if (TEST_ATTRIBUTE.search(code) or path.endswith('Tests.cs')) and not found:
        raise ValueError('unresolved test attributes: ' + path)
    return found


def system(path):
    parts = PurePosixPath(path).parts
    match = re.fullmatch(r'Assets/Scripts/Orchestrator/(\w+)Orchestrator\.cs', path)
    if match:
        return match.group(1)
    if path.startswith('Assets/Resources/ScriptableObjects/') and len(parts) >= 5:
        return parts[4]
    if path.startswith(TESTS) and len(parts) > 4:
        return parts[3]
    if path.startswith('Assets/Scripts/') and len(parts) > 4:
        return 'Core' if parts[2] == 'Core' else parts[3]
    if path.startswith('Assets/Editor/') and len(parts) > 3:
        return parts[2]
    return None


def wiring(path):
    return (path.startswith(('Assets/Scripts/Orchestrator/', 'Assets/Scripts/Session/',
                             'Assets/Scripts/Core/Definitions/', 'Assets/Resources/'))
            or (path.startswith('Assets/Editor/') and not path.startswith(TESTS))
            or path.endswith(('.unity', '.prefab')))


def select(changed, current, previous=None):
    previous = previous or {}
    reasons, full, by_file = {}, [], {}
    def widen(reason):
        if reason not in full:
            full.append(reason)
    for path, text in sorted(current.items()):
        if path.startswith(TESTS):
            try:
                by_file[path] = fixtures(path, text)
            except ValueError as error:
                widen(str(error))
    inventory = sorted({name for names in by_file.values() for name in names})
    fixture_types = {name.rsplit('.', 1)[-1] for name in inventory}
    for path, text in current.items():
        if path.startswith(TESTS):
            for declaration in CLASS.finditer(code_only(text)):
                if ':' in declaration.group(2) and set(re.findall(r'\w+', declaration.group(2))) & fixture_types:
                    widen('inherited fixture requires full discovery: ' + path)
    def add(names, reason):
        for name in names:
            reasons.setdefault(name, set()).add(reason)
    for name in SMOKE:
        if name not in inventory:
            widen('mandatory smoke fixture missing: ' + name)
        else:
            add([name], 'mandatory smoke')
    def add_system(owner, reason):
        owners = (owner,) + SYSTEM_FIXTURE_ALIASES.get(owner, ())
        names = [name for path, names in by_file.items() for name in names
                 if system(path) in owners or any(name.startswith('Worsen.Tests.' + o + '.') for o in owners)]
        add(names, reason)
        return bool(names)
    def add_wiring(reason):
        names = [name for name in inventory if
                 name.split('.')[2] in ('Expedition', 'Run', 'Scenes', 'HorrorRun', 'SceneFlow')
                 or re.search(r'Integration|Wiring|Routing|Scene|Setup', name.rsplit('.', 1)[-1])]
        add(names, reason)
        if not names:
            widen('no integration/wiring fixtures discovered')
    seeds = set()
    normalized = sorted(set(changed))
    for original in normalized:
        if (not original or '\\' in original or original.startswith('/') or ':' in original
                or '..' in original.split('/')):
            widen('invalid repository-relative path: ' + original)
            continue
        path = original[:-5] if original.endswith('.meta') else original
        # A folder's .meta (no extension on its final segment) only records the folder GUID;
        # it changes no behaviour, so it adds no native fixtures (batch 36: Assets/Prefabs.meta).
        if original.endswith('.meta') and path.startswith('Assets/') and '.' not in path.rsplit('/', 1)[-1]:
            continue
        if original in GATE_INJECTED:
            if path in current and path in previous and current[path] != previous[path]:
                widen('gate runner implementation changed: ' + original)
            continue
        # Gate and offline tooling never run inside Unity; their own pytest/PowerShell and pure-harness
        # checks cover them, so a tooling change adds no native fixtures (owner, 2026-10-01: gates too slow).
        if path.startswith(('ArtSource/', 'tools/blender/', 'PLANNING/', 'evidence/', 'tools/integration/',
                            'tools/offline-compile/')):
            continue
        if path.endswith('.md') and path != 'VENDOR.md':
            continue
        if (path.endswith(('.asmdef', '.asmref', '.dll', '.rsp'))
                or path.startswith(('Packages/', 'ProjectSettings/', TESTS + 'Infrastructure/',
                                    'Assets/Editor/Testing/'))
                or 'FixtureTimeSetUp' in path or path == 'VENDOR.md'
                or 'vendor' in path.lower() or 'Vendor' in path):
            widen('assembly, environment, vendor or test infrastructure: ' + original)
            continue
        asset_owner = next((owner for prefix, owner in (
            ('Assets/Art/Environment', 'Procedural'), ('Assets/Art/Hunter', 'Hunter'),
            # Collapse hands and the cake pickup are Floor content; the Suzume door is Floor's exit;
            # other horror art is wired by the Horror setup; the player model is Player content.
            ('Assets/Art/Horror/Collapse', 'Floor'), ('Assets/Art/Horror/Cake', 'Floor'), ('Assets/Art/Exit', 'Floor'),
            ('Assets/Art/Horror', 'Horror'), ('Assets/Art/Player', 'Player'),
            ('Assets/Art/Shrine', 'Shrine')) if path == prefix or path.startswith(prefix + '/')), None)
        if path.startswith('Assets/Resources/ScriptableObjects/'):
            asset_owner = system(path)
        if asset_owner:
            if not add_system(asset_owner, 'changed system asset: ' + original):
                widen('no conventional system fixtures: ' + original)
            if wiring(path):
                add_wiring('serialized wiring/config: ' + original)
            continue
        if path.endswith('.cs') and path in current.keys() | previous.keys():
            if path.startswith(TESTS):
                if path in current:
                    add(by_file.get(path, []), 'changed test file: ' + original)
                # Helpers and base classes can be consumed by other fixtures too.
            elif path.startswith(('Assets/Scripts/Domain/', 'Assets/Scripts/Presentation/',
                                  'Assets/Scripts/Session/', 'Assets/Scripts/Core/')):
                owner = system(path)
                if not owner or not add_system(owner, 'changed system: ' + original):
                    widen('no conventional system fixtures: ' + original)
                for text in (current.get(path, ''), previous.get(path, '')):
                    for namespace_owner in re.findall(r'\bnamespace\s+Worsen\.(?:Domain|Session|Presentation)\.([A-Za-z_]\w*)', code_only(text)):
                        add_system(namespace_owner, 'changed namespace system: ' + original)
            elif not path.startswith(('Assets/Scripts/Orchestrator/', 'Assets/Editor/')):
                widen('unclassified source: ' + original)
            else:
                owner = system(path)
                if owner:
                    if not add_system(owner, 'changed editor/orchestrator system: ' + original):
                        widen('no conventional system fixtures: ' + original)
            discover = test_declarations if path.startswith(TESTS) else declarations
            types = discover(current.get(path, '')) | discover(previous.get(path, ''))
            if not types:
                widen('no declared types: ' + original)
            seeds.update(types)

        elif path.endswith(('.unity', '.prefab')) or path.startswith('Assets/Resources/'):
            add_wiring('serialized wiring/config: ' + original)
        elif original.endswith('.meta') and any(p.startswith(path + '/') for p in current):
            add_wiring('source folder metadata: ' + original)
        elif path.endswith('.md') and not path.startswith('Assets/'):
            pass  # Explicit documentation-only classification, not a catch-all.
        else:
            widen('unclassified or unavailable path: ' + original)
        if wiring(path):
            add_wiring('cross-system wiring: ' + original)
    # Preserve strings for reflection/source assertions, but not prose comments.
    tokens = {p: set(re.findall(r'\b[A-Za-z_]\w*\b', LEXICAL.sub(
        lambda m: ' ' if m.group().startswith(('//', '/*')) else m.group(), text)))
        for p, text in current.items()}
    affected = set(seeds)
    for path in sorted(current):
        hits = tokens[path] & seeds
        if hits and not path.startswith(TESTS):
            affected.update(declarations(current[path]))  # one transitive production hop
            if wiring(path):
                add_wiring('affected wiring consumer: ' + path)
    # Follow helper references to closure within the test tree (cheap, avoids missing
    # fixtures whose shared test helper changed but whose own file did not).
    pending = True
    while pending:
        pending = False
        for path, names in sorted(by_file.items()):
            hits = tokens[path] & affected
            if hits:
                add(names, 'type reference (direct/one-hop/helper): ' + ', '.join(sorted(hits)))
                extra = test_declarations(current[path]) - affected
                if extra:
                    affected.update(extra)
                    pending = True
    if not inventory:
        widen('no fixture inventory')
    if full:
        reasons = {name: {'full-suite fallback (see full_reasons)'} for name in inventory}
    return {'schema_version': 1, 'scope': 'full' if full else 'selected',
            'fixtures': sorted(reasons),
            'fixture_reasons': {n: sorted(reasons[n]) for n in sorted(reasons)},
            'full_reasons': sorted(full), 'changed_files': normalized,
            'inventory_count': len(inventory), 'all_fixtures': inventory,
            'ignored_gate_files': sorted(set(changed) & GATE_INJECTED)}


def comparison_base(root, base, candidate, tested=None):
    """Only ancestry or exact tree equality qualifies; patch resemblance is not proof."""
    base = git(root, 'rev-parse', '--verify', base + '^{commit}').decode().strip()
    candidate = git(root, 'rev-parse', '--verify', candidate + '^{commit}').decode().strip()
    if not tested:
        return base, candidate, False, 'no complete full baseline'
    try:
        tested = git(root, 'rev-parse', '--verify', tested + '^{commit}').decode().strip()
        ancestry = subprocess.run(['git', '-C', str(root), 'merge-base', '--is-ancestor', tested, candidate],
                                  stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        if ancestry.returncode == 0:
            return tested, candidate, True, 'tested candidate is an ancestor (including self)'
        if ancestry.returncode != 1:
            raise ValueError('ancestry check failed')
        if git(root, 'rev-parse', tested + '^{tree}') == git(root, 'rev-parse', candidate + '^{tree}'):
            return tested, candidate, True, 'identical complete Git trees'
    except (ValueError, subprocess.SubprocessError):
        return base, candidate, False, 'tested candidate unavailable; main-based fallback'
    return base, candidate, False, 'tested candidate is neither ancestor nor identical tree; main-based fallback'


def read_paths(path):
    text = path.read_text(encoding='utf-8-sig')
    paths = json.loads(text) if text.lstrip().startswith('[') else text.splitlines()
    if not isinstance(paths, list) or not all(isinstance(p, str) for p in paths):
        raise ValueError('changed-files must be a list of strings')
    return paths


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--base')
    parser.add_argument('--candidate')
    parser.add_argument('--tested-candidate', help='Completed full-run candidate; ancestry/equality checked')
    parser.add_argument('--extra-changes', type=Path, help='Setup drift paths, unioned with the Git diff')
    parser.add_argument('--changed-files', type=Path, help='UTF-8 JSON string array or newline-separated paths')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    try:
        if bool(args.base) != bool(args.candidate) or bool(args.base) == bool(args.changed_files):
            raise ValueError('supply either --base and --candidate, or --changed-files')
        if args.changed_files:
            if args.tested_candidate or args.extra_changes:
                raise ValueError('tested-candidate/extra-changes require Git refs')
            changed = read_paths(args.changed_files)
            result = select(changed, snapshot(args.root))
        else:
            base, candidate, reused, rule = comparison_base(args.root, args.base, args.candidate, args.tested_candidate)
            changed = git(args.root, 'diff', '--no-renames', '--name-only', '-z', base, candidate).decode('utf-8').strip('\0').split('\0')
            if args.extra_changes:
                changed += read_paths(args.extra_changes)
            result = select([p for p in changed if p], snapshot(args.root, candidate), snapshot(args.root, base))
            result.update(base=base, candidate=candidate, baseline_reused=reused, baseline_rule=rule)
    except (ValueError, OSError, subprocess.SubprocessError, UnicodeError) as error:
        result = {'schema_version': 1, 'scope': 'full', 'fixtures': [], 'fixture_reasons': {},
                  'full_reasons': ['selector could not establish coverage: ' + str(error)], 'inventory_count': 0}
    output = json.dumps(result, indent=2, sort_keys=True) + '\n'
    if args.output:
        args.output.write_text(output, encoding='utf-8')
    else:
        print(output, end='')


if __name__ == '__main__':
    main()
