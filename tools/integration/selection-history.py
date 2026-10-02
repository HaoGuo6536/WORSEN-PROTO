#!/usr/bin/env python3
# ============================================================================
# selection-history.py
# ============================================================================
# PURPOSE: Reproduce historical selection without checking out candidates. Compare
#   a recorded full gate with tested-candidate previews and duration-based estimates;
#   preserve missing evidence rather than silently inventing measurements.
# ARCHITECTURAL ROLE: Integration evidence tooling; no runtime layer.
# KEY RESPONSIBILITIES:
#   - Resolve retained candidate refs or their uniquely named gate commits.
#   - Reconstruct the base before each contiguous first-parent worker merge train.
#   - Write repeatable JSON with selection reasons and estimate assumptions.
# DEPENDENCIES: Git history, Python standard library and select-tests.py.
# USAGE NOTES: --run-directory uses recorded NUnit fixture durations. The older
#   batch20-24 mode retains its explicitly uniform-cost estimate, not a benchmark.
# ============================================================================
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess
from datetime import datetime
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location('selector', Path(__file__).with_name('select-tests.py'))
SELECTOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SELECTOR)


def historical_candidate(root, label):
    ref = 'cand/' + label
    try:
        candidate = SELECTOR.git(root, 'rev-parse', '--verify', ref + '^{commit}').decode().strip()
        source = ref
    except subprocess.CalledProcessError:
        matches = SELECTOR.git(root, 'log', '--all', '--format=%H', '--fixed-strings',
                               '--grep=Gate ' + label + ': Unity meta files and setup outputs').decode().splitlines()
        if len(matches) != 1:
            raise ValueError(f'{ref} absent and gate commit is not unique: {matches}')
        candidate = matches[0]
        source = ref + ' absent; unique gate commit subject fallback'
    # integrate.ps1 reset to main, merged workers with --no-ff, then added one
    # setup/meta commit. The first non-merge parent is that gate's base main.
    rows = SELECTOR.git(root, 'log', '--first-parent', '--format=%H%x09%P%x09%s', candidate).decode().splitlines()
    for row in rows[1:]:
        commit, parents, subject = row.split('\t', 2)
        if len(parents.split()) == 2 and subject.startswith('Merge '):
            continue
        return candidate, commit, source
    raise ValueError('Could not reconstruct base for ' + label)


def recorded_run_report(root, directory, changed):
    before = json.loads((directory / 'test-selection.json').read_text(encoding='utf-8-sig'))
    candidate = before['candidate']
    base, target, reused, rule = SELECTOR.comparison_base(root, before['base'], candidate, candidate)
    if not reused:
        raise ValueError('Cannot establish self-comparison: ' + rule)
    sources = SELECTOR.snapshot(root, target)
    if any(path not in sources for path in changed):
        raise ValueError('Preview source path absent from the recorded candidate')
    completions = list(directory.glob('native-*/complete.json'))
    if len(completions) != 1:
        raise ValueError('Expected exactly one completed native run')
    complete = json.loads(completions[0].read_text(encoding='utf-8-sig'))
    request = json.loads((directory / ('request-' + complete['run_id'] + '.json')).read_text(encoding='utf-8-sig'))
    if complete['scope'] != 'full' or request['scope'] != 'full':
        raise ValueError('Duration evidence must be a full run')
    xml_text = (completions[0].parent / 'result-native.xml').read_text(encoding='utf-8-sig')
    # NUnit needs no DTD/entities. Reject them before the standard-library parser.
    if '<!DOCTYPE' in xml_text.upper() or '<!ENTITY' in xml_text.upper():
        raise ValueError('DTD/entity declarations are not supported')
    xml = ET.fromstring(xml_text)
    fixture_durations = {}
    for suite in xml.findall('.//test-suite[@type="TestFixture"]'):
        name = suite.attrib['fullname'].split('(', 1)[0]
        fixture_durations[name] = fixture_durations.get(name, 0) + float(suite.get('duration', '0'))
    # Unity's root duration resets across domain reloads; the leaf time span does not.
    leaves = xml.findall('.//test-case')
    starts = [datetime.fromisoformat(c.attrib['start-time'].replace('Z', '+00:00')) for c in leaves if 'start-time' in c.attrib]
    ends = [datetime.fromisoformat(c.attrib['end-time'].replace('Z', '+00:00')) for c in leaves if 'end-time' in c.attrib]
    elapsed = (max(ends) - min(starts)).total_seconds()
    recorded_sum = sum(fixture_durations.values())
    overhead = max(0, elapsed - recorded_sum)
    drift = json.loads((directory / 'setup-drift.json').read_text(encoding='utf-8-sig'))
    legacy_paths = drift['changed'] + drift['added'] + drift['removed']
    report = {'candidate': candidate, 'baseline_run_id': complete['run_id'], 'baseline_rule': rule,
              'before': {'scope': before['scope'], 'fixture_count': len(before['fixtures']),
                         'full_reason_count': len(before['full_reasons'])},
              'timing': {'root_seconds_unreliable_across_reload': float(xml.attrib['duration']),
                         'fixture_seconds': round(recorded_sum, 3), 'leaf_span_seconds': elapsed,
                         'unattributed_seconds': round(overhead, 3)},
              'estimate_method': 'sum selected recorded fixture durations; upper estimate retains ALL unattributed full-run time; excludes offline/import/setup; not a new benchmark',
              'scenarios': []}
    for label, paths in (('self, no setup change (hash parity assumed)', []),
                         ('Floor change, no setup change (hash parity assumed)', changed),
                         ('self, conservative legacy setup drift', legacy_paths),
                         ('Floor change, conservative legacy setup drift', changed + legacy_paths)):
        selection = SELECTOR.select(paths, sources, sources)
        # Match integrate.ps1's existing selected-only sweep policy.
        if selection['scope'] == 'selected':
            selection['fixtures'] = [f for f in selection['fixtures'] if not f.endswith('SweepTests')]
        missing = set(selection['fixtures']) - fixture_durations.keys()
        if missing:
            raise ValueError('Missing fixture durations: ' + repr(sorted(missing)))
        seconds = sum(fixture_durations[f] for f in selection['fixtures'])
        row = {'scenario': label, 'fixture_count': len(selection['fixtures']),
               'estimated_minutes': [round(seconds / 60, 2), round((seconds + overhead) / 60, 2)],
               'selection': selection}
        report['scenarios'].append(row)
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--run-directory', type=Path)
    parser.add_argument('--changed-path', action='append', default=[])
    args = parser.parse_args()
    if args.run_directory:
        report = recorded_run_report(args.root, args.run_directory, args.changed_path or ['Assets/Scripts/Domain/Floor/Controller/FloorController.cs'])
        args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + '\n', encoding='utf-8')
        print(json.dumps({k: v for k, v in report.items() if k != 'scenarios'}, indent=2))
        for row in report['scenarios']:
            print(f"{row['scenario']}: {row['selection']['scope']} {row['fixture_count']} fixtures; estimated minutes {row['estimated_minutes']}")
        return
    report = {'method': 'candidate Git snapshots; base is the first non-merge before the first-parent worker merge train',
              'estimate': 'uniform fixture-cost proxy, full suite 15-25 minutes (owner estimate); no Unity timings measured; excludes offline tier and fixed editor overhead',
              'runs': []}
    for number in range(20, 25):
        label = 'batch' + str(number)
        candidate, base, source = historical_candidate(args.root, label)
        changed = SELECTOR.git(args.root, 'diff', '--no-renames', '--name-only', '-z', base, candidate).decode().strip('\0').split('\0')
        selection = SELECTOR.select(changed, SELECTOR.snapshot(args.root, candidate), SELECTOR.snapshot(args.root, base))
        count, total = len(selection['fixtures']), selection['inventory_count']
        fraction = 0 if selection['scope'] == 'full' else 1 - count / total
        row = {'label': label, 'requested_ref': 'cand/' + label, 'candidate_source': source,
               'base': base, 'candidate': candidate, 'scope': selection['scope'], 'fixture_count': count,
               'inventory_count': total, 'estimated_minutes_saved': [round(15 * fraction, 2), round(25 * fraction, 2)],
               'selection': selection}
        report['runs'].append(row)
        print(f"{label}: {selection['scope']} {count}/{total} fixtures; estimated saved {row['estimated_minutes_saved']} min; base={base[:8]} candidate={candidate[:8]} ({source})")
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
