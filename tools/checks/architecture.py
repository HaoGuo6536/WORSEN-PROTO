#!/usr/bin/env python3
# ============================================================================
# architecture.py
# ============================================================================
# PURPOSE:
#   Make the assembly graph, Session ordering and responsibility alarms executable
#   without Unity or third-party Python packages. Report measured source evidence
#   and apply only dated, path-specific waivers from the architecture authority.
# ARCHITECTURAL ROLE:
#   Editor tool (§10, §13) · offline enforcement · project-wide, outside Assets.
# KEY RESPONSIBILITIES:
#   - Validate project assembly references against the fixed layer graph.
#   - Measure Session dependencies and propose a dependency-first order.
#   - Detect the four responsibility alarms without using file length.
#   - Apply the exact Appendix A waiver table and reject expired exemptions.
#   - Emit deterministic human-readable and JSON evidence with a failing exit gate.
# DEPENDENCIES:
#   - Python standard library, architecture.json, SPEC-001 and project source files.
# USAGE NOTES:
#   Read-only with respect to source; --json writes only the requested report.
#   This is a lexical checker, not a C# compiler. See README.md for its boundaries.
# ============================================================================

import argparse
import datetime as dt
import json
import re
from pathlib import Path

SPEC = 'PLANNING/specs/SPEC-001-project-architecture-guidelines.md'
CONFIG = 'tools/checks/architecture.json'
LAYERS = ('Core', 'Domain', 'Session', 'Presentation', 'Orchestrator')
PRESENTATION_VENDOR = {
    'Unity.InputSystem', 'Unity.Cinemachine',
    'Unity.RenderPipelines.Core.Runtime', 'Unity.RenderPipelines.Universal.Runtime',
    'FronkonGames.Weird.DitherFog', 'DistantLands.Lumen.Runtime',
}
ALLOWED = {
    'Core': set(),
    'Domain': {'Worsen.Core', 'DistantLands.Lumen.Runtime'},
    'Session': {'Worsen.Core', 'Worsen.Domain'},
    'Presentation': {'Worsen.Core'} | PRESENTATION_VENDOR,
    'Orchestrator': {'Worsen.' + layer for layer in LAYERS if layer != 'Orchestrator'},
}
SYSTEM = re.compile(r'\bWorsen\.(Core|Domain|Session|Presentation|Orchestrator|Editor|Tests)\.(\w+)')
# Preserve offsets and newlines while removing comments and literal contents.
LITERALS = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|\$*"{3,}[\s\S]*?"{3,}|(?:\$@|@\$|@)"(?:""|[^"])*"|\$?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')
METHOD = re.compile(
    r'\b(?:(?:public|private|protected|internal|static|virtual|override|sealed|async|unsafe|new|partial|extern)\s+)*'
    r'(?!(?:return|throw|new|await|else)\b)[\w.<>?,\[\]]+\s+(\w+)(?:<[^;{}()]+>)?\s*\([^;{}]*\)\s*'
    r'(?:where\s+[^{};=]+)?(?P<body>\{|=>)')


def code_only(text):
    return LITERALS.sub(lambda m: ''.join('\n' if c == '\n' else ' ' for c in m[0]), text)


def close_brace(code, start):
    depth = 0
    for pos in range(start, len(code)):
        if code[pos] == '{':
            depth += 1
        elif code[pos] == '}':
            depth -= 1
            if depth == 0:
                return pos
    raise ValueError('unbalanced C# braces')


def methods(code):
    for match in METHOD.finditer(code):
        start = match.end()
        end = close_brace(code, start - 1) if match['body'] == '{' else code.find(';', start)
        if end < 0:
            raise ValueError('unterminated expression-bodied method')
        yield match[1], code[start:end], match.start()


def finding(path, alarm, measured, detail, severity='error', **extra):
    return dict(path=path, alarm=alarm, measured=measured, detail=detail,
                severity=severity, waived=False, **extra)


def source_paths(root, suffix):
    return sorted({p for tree in ('Assets/Scripts', 'Assets/Editor', 'Assets/Tests')
                   for p in (root / tree).rglob('*' + suffix)})


def assembly_findings(root):
    result, definitions, guids = [], {}, {}
    project_paths = set(source_paths(root, '.asmdef'))
    # Resolve GUID references to names, including vendor asmdefs, but never Library.
    for path in sorted((root / 'Assets').rglob('*.asmdef')):
        try:
            data = json.loads(path.read_text(encoding='utf-8-sig'))
            if not isinstance(data, dict) or not isinstance(data.get('name'), str):
                raise ValueError('Assembly definition requires a name')
            definitions[path] = data
            meta = path.with_suffix('.asmdef.meta')
            if meta.exists():
                match = re.search(r'^guid:\s*(\w+)', meta.read_text(encoding='utf-8-sig'), re.M)
                if match:
                    guids[match[1].lower()] = data['name']
        except (ValueError, KeyError):
            if path in project_paths:
                result.append(finding(path.relative_to(root).as_posix(), 'asmdef', 'invalid JSON/name', 'Cannot parse assembly definition'))
    for path in source_paths(root, '.asmdef'):
        if path not in definitions:
            continue
        rel, data = path.relative_to(root).as_posix(), definitions[path]
        if not isinstance(data, dict):
            result.append(finding(rel, 'asmdef', 'invalid object', 'Expected a JSON object'))
            continue
        if rel.startswith(('Assets/Editor/', 'Assets/Tests/')):
            continue
        layer = rel.split('/')[2]
        if layer not in ALLOWED:
            result.append(finding(rel, 'asmdef', layer, 'Unrecognized runtime layer'))
            continue
        refs = data.get('references', [])
        if not isinstance(refs, list) or any(not isinstance(ref, str) for ref in refs):
            result.append(finding(rel, 'asmdef', 'invalid references', 'Expected an array of assembly names or GUIDs'))
            continue
        for ref in refs:
            resolved = guids.get(ref[5:].lower(), ref) if ref.startswith('GUID:') else ref
            if resolved not in ALLOWED[layer]:
                result.append(finding(rel, 'asmdef', resolved, f'{layer} cannot reference {resolved} (§9, §13a)'))
        if data.get('autoReferenced', True) is True:
            result.append(finding(rel, 'asmdef', 'autoReferenced=true', 'Layer assembly is automatically referenced (§13a)', 'warning'))
    return result


def header_responsibilities(text):
    lines = []
    for line in text.lstrip('\ufeff\r\n ').splitlines():
        if not line.lstrip().startswith('//'):
            break
        lines.append(re.sub(r'^\s*//\s?', '', line))
    header = '\n'.join(lines)
    match = re.search(r'KEY RESPONSIBILITIES:\s*\n(.*?)(?=\n\s*(?:DEPENDENCIES:|USAGE NOTES:|ARCHITECTURAL ROLE:|PURPOSE:|====)|\Z)', header, re.S)
    return len(re.findall(r'^\s*[-*]\s+', match[1], re.M)) if match else 0


def pure_relay(body, events):
    # Allow side-effect-free guards (pause/null checks), but not computations,
    # other calls, assignments, loops, multiple raises or argument construction.
    compact = re.sub(r'\s+', '', body)
    compact = compact.replace('{', '').replace('}', '')
    compact = re.sub(r'if\([^()]*\)return;', '', compact)
    compact = re.sub(r'^if\([^()]*\)', '', compact)
    for event in events:
        pattern = r'(?:this\.)?' + re.escape(event) + r'(?:\?\.Invoke|\.Invoke)?\(([^()]*)\);?'
        match = re.fullmatch(pattern, compact)
        if match and re.fullmatch(r'[\w.,\s]*', match[1]):
            return True
    return False


def script_findings(path, text):
    result, code = [], code_only(text)
    count = header_responsibilities(text)
    if count > 5:
        result.append(finding(path, 'responsibilities', count, 'KEY RESPONSIBILITIES bullets > 5 (§0; A3)'))
    parts = path.split('/')
    own = (parts[2], parts[3]) if path.startswith('Assets/Scripts/') and len(parts) > 4 else None
    if path.startswith('Assets/Editor/Tests/') and len(parts) > 4:
        own = ('Tests', parts[3])
    elif path.startswith('Assets/Editor/') and len(parts) > 3:
        own = ('Editor', parts[2])
    # A using directive counts as dependency evidence even if currently unused.
    imports = '\n'.join(re.findall(r'\b(?:global\s+)?using\s+[^;]+;', code))
    for match in re.finditer(r'\bclass\s+(\w+)[^{;]*\{', code):
        name = match[1]
        end = close_brace(code, match.end() - 1)
        body = code[match.end():end]
        systems = sorted({m.groups() for m in SYSTEM.finditer(imports + '\n' + body)
                          if m[1] != 'Core' and m.groups() != own})
        if len(systems) > 5:
            result.append(finding(path, 'fan-out', len(systems), ', '.join('.'.join(s) for s in systems), symbol=name))
        if name.endswith('Manager'):
            events = []
            public_events = []
            for event in re.finditer(r'\b(?P<mods>(?:(?:public|private|protected|internal|static|new|virtual|override)\s+)*)event\s+[\w.]+(?:<[^;{}]+?>)?\s+([^;{}]+)[;{]', body):
                names = [v.strip().split('=')[0].strip() for v in event[2].split(',')]
                events.extend(names)
                if 'public' in event['mods'].split():
                    public_events.extend(names)
            subscribed = set(re.findall(r'\+=\s*(?:this\.)?(\w+)\s*;', body))
            handlers = [(n, b) for n, b, _ in methods(body) if n in subscribed]
            relays = [n for n, b in handlers if pure_relay(b, events)]
            if len(public_events) > 15 or (handlers and len(relays) * 2 > len(handlers)):
                measured = dict(publicEvents=len(public_events), relayHandlers=len(relays), handlers=len(handlers))
                result.append(finding(path, 'relay-surface', measured, 'public events > 15 or relay handlers > half (§1, §9; A3)', symbol=name, relayNames=relays))
    for name, body, pos in methods(code):
        count = len(re.findall(r'\bis\s+(?:[\w]+\.)*\w+(?:Config|State|Profile)\b', body))
        if count >= 3:
            result.append(finding(path, 'type-switch', count, 'Config/State/Profile type tests in one method >= 3 (A3)', symbol=name, line=code[:pos].count('\n') + 1))
    return result


def session_graph(sources, order):
    edges, systems = {}, set()
    for path, text in sources.items():
        if not path.startswith('Assets/Scripts/Session/'):
            continue
        own = path.split('/')[3]
        systems.add(own)
        # Exclude namespace declarations, comments and string literals.
        code = re.sub(r'\bnamespace\s+[\w.]+', '', code_only(text))
        for target in re.findall(r'\bWorsen\.Session\.(\w+)', code):
            if target != own:
                edges.setdefault((own, target), []).append(path)
                systems.add(target)
    result = []
    rank = {name: pos for pos, name in enumerate(order)}
    for system in sorted(systems - rank.keys()):
        result.append(finding(CONFIG, 'session-order', system, 'Session system missing from sessionOrder (§9; A1)'))
    for (source, target), paths in sorted(edges.items()):
        if source not in rank or target not in rank or rank[target] >= rank[source]:
            for path in sorted(set(paths)):
                result.append(finding(path, 'session-order', f'{source} -> {target}', 'Dependency must be earlier in sessionOrder (§9; A1)'))
    proposed, remaining = [], set(systems)
    while remaining:
        ready = sorted(s for s in remaining if not any(a == s and b in remaining for a, b in edges))
        if not ready:
            result.append(finding(CONFIG, 'session-order', sorted(remaining), 'Cycle: no consistent order exists'))
            break
        proposed.extend(ready)
        remaining.difference_update(ready)
    return result, [dict(source=a, target=b, paths=sorted(set(paths))) for (a, b), paths in sorted(edges.items())], proposed if not remaining else None


def waivers(text, today):
    heading = '## Appendix A — Known Debt (migrate when touched)'
    if heading not in text:
        return [], [], []
    section = text.split(heading, 1)[1].split('\n## ', 1)[0]
    expected = ['ID', 'Path', 'Alarm', 'Owner', 'Exit plan', 'Review by']
    active, expired, errors, table = [], [], [], False
    for line in section.splitlines():
        if not line.strip().startswith('|'):
            table = False
            continue
        cells = [c.strip().strip('`') for c in line.strip().strip('|').split('|')]
        if cells == expected:
            table = True
            continue
        if not table or all(re.fullmatch(r':?-+:?', c) for c in cells):
            continue
        try:
            if len(cells) != 6 or not all(cells):
                raise ValueError('all six waiver fields are required')
            ident, path, alarm, owner, exit_plan, review = cells
            if '\\' in path or path.startswith('/') or '..' in path.split('/') or '*' in path:
                raise ValueError('waiver path must be exact checkout-relative POSIX path')
            deadline = dt.date.fromisoformat(review)
            row = dict(id=ident, path=path, alarm=alarm, owner=owner, exitPlan=exit_plan, reviewBy=review)
            (expired if deadline < today else active).append(row)
        except ValueError as exc:
            errors.append(finding(SPEC, 'waiver', line, str(exc)))
    return active, expired, errors


def check(root, today=None):
    root = Path(root).resolve()
    today = today or dt.date.today()
    findings = []
    for required in ('Assets/Scripts', 'Assets/Editor', CONFIG, SPEC):
        if not (root / required).exists():
            findings.append(finding(required, 'input', 'missing', 'Required checker input is missing'))
    try:
        config = json.loads((root / CONFIG).read_text(encoding='utf-8-sig'))
        order = config['sessionOrder']
        if not isinstance(order, list) or any(not isinstance(s, str) or not s for s in order) or len(order) != len(set(order)):
            raise ValueError('sessionOrder must contain unique nonempty system names')
    except (OSError, ValueError, KeyError, TypeError) as exc:
        findings.append(finding(CONFIG, 'input', 'invalid', str(exc)))
        order = []
    sources = {p.relative_to(root).as_posix(): p.read_text(encoding='utf-8-sig') for p in source_paths(root, '.cs')}
    findings.extend(assembly_findings(root))
    for path, text in sources.items():
        try:
            findings.extend(script_findings(path, text))
        except ValueError as exc:
            findings.append(finding(path, 'input', 'unparseable source', str(exc)))
    session, edges, proposed = session_graph(sources, order)
    findings.extend(session)
    spec = (root / SPEC).read_text(encoding='utf-8-sig') if (root / SPEC).exists() else ''
    active, expired, errors = waivers(spec, today)
    findings.extend(errors)
    for item in findings:
        matches = [w for w in active if (w['path'], w['alarm']) == (item['path'], item['alarm'])]
        if matches and item['alarm'] not in ('input', 'waiver'):
            item.update(waived=True, waiver=matches[0]['id'])
    findings.sort(key=lambda f: (f['path'], f['alarm'], f.get('symbol', ''), str(f['measured'])))
    return dict(date=today.isoformat(), findings=findings, waived=sum(f['waived'] for f in findings),
                expired=expired, sessionEdges=edges, sessionOrder=order, proposedSessionOrder=proposed)


def markdown_report(report):
    def cell(value):
        text = json.dumps(value, sort_keys=True) if not isinstance(value, str) else value
        return text.replace('|', '\\|').replace('\n', ' ')

    rows = ['# Architecture findings report', '',
            f"ARCH_RESULT findings={len(report['findings'])} waived={report['waived']} expired={len(report['expired'])}",
            '', 'One row per finding; symbols distinguish classes sharing a file. '
            'This is evidence, not an approved waiver ledger.', '',
            '| Path | Alarm | Measured value | Symbol | Status |',
            '|---|---|---|---|---|']
    for item in report['findings']:
        values = (item['path'], item['alarm'], item['measured'], item.get('symbol', ''),
                  'waived' if item['waived'] else item['severity'])
        rows.append('| ' + ' | '.join(cell(v) for v in values) + ' |')
    rows.extend(['', '## Session edges', '', '| Source | Dependency | Evidence paths |', '|---|---|---|'])
    for edge in report['sessionEdges']:
        rows.append('| ' + ' | '.join((edge['source'], edge['target'], ', '.join(edge['paths']))) + ' |')
    rows.extend(['', 'Proposed dependency-first order: ' + cell(report['proposedSessionOrder']), ''])
    return '\n'.join(rows)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', required=True, type=Path)
    parser.add_argument('--json', type=Path, dest='json_path')
    parser.add_argument('--markdown', type=Path, help='Optional complete measured findings table')
    args = parser.parse_args(argv)
    try:
        report = check(args.root)
    except (OSError, ValueError, TypeError) as exc:
        report = dict(findings=[finding(str(args.root), 'input', 'read/parse error', str(exc))], waived=0, expired=[], sessionEdges=[], proposedSessionOrder=None)
    print(f"ARCH_RESULT findings={len(report['findings'])} waived={report['waived']} expired={len(report['expired'])}")
    for item in report['findings']:
        status = 'waived' if item['waived'] else item['severity']
        print(f"{item['path']} | {item['alarm']} | {json.dumps(item['measured'], sort_keys=True)} | {status} | {item['detail']}")
    for edge in report['sessionEdges']:
        print(f"SESSION_EDGE {edge['source']} -> {edge['target']}")
    print('SESSION_ORDER_PROPOSED ' + json.dumps(report['proposedSessionOrder']))
    for row in report['expired']:
        print(f"EXPIRED {row['id']} {row['path']} {row['alarm']} review-by={row['reviewBy']}")
    if args.json_path:
        args.json_path.parent.mkdir(parents=True, exist_ok=True)
        args.json_path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    if args.markdown:
        args.markdown.parent.mkdir(parents=True, exist_ok=True)
        args.markdown.write_text(markdown_report(report), encoding='utf-8')
    return int(any(not f['waived'] for f in report['findings']))


if __name__ == '__main__':
    raise SystemExit(main())
