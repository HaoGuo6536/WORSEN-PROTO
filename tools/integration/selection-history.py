#!/usr/bin/env python3
# ============================================================================
# selection-history.py
# ============================================================================
# PURPOSE: Reproduce selection for five historical gates without checking them
#   out. Report conservative fixture counts and explicitly modelled, not measured,
#   time savings; preserve missing refs rather than silently inventing them.
# ARCHITECTURAL ROLE: Integration evidence tooling; no runtime layer.
# KEY RESPONSIBILITIES:
#   - Resolve retained candidate refs or their uniquely named gate commits.
#   - Reconstruct the base before each contiguous first-parent worker merge train.
#   - Write repeatable JSON with selection reasons and estimate assumptions.
# DEPENDENCIES: Git history, Python standard library and select-tests.py.
# USAGE NOTES: Estimates assume uniform fixture cost over the owner's 15-25 minute
#   full-suite range. They are not benchmarks; reload-heavy fixtures violate it.
# ============================================================================
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess

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


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
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
