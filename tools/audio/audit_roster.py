# ============================================================================
# audit_roster.py
# PURPOSE:
#   Resolve serialized audio GUIDs and link a path-only selection to MOSS evidence.
# ARCHITECTURAL ROLE:
#   Offline audio tooling utility; no Unity invocation or asset mutation.
# KEY RESPONSIBILITIES:
#   - Snapshot actual serialized banks/bindings and their read-only vendor paths.
#   - Verify selected whole-file hashes and report every cue with model outputs.
# DEPENDENCIES:
#   Python standard library and moss_paths.
# USAGE NOTES:
#   Writes a new JSON report under checkout Logs; never copies audio or metadata.
# ============================================================================
import argparse
import hashlib
import json
from pathlib import Path
import re

from moss_paths import ROOT


def sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def audit(source, runs):
    paths = {}
    for area in ('Assets/External', 'Assets/Audio', 'Assets/Resources/Audio'):
        for meta in (source / area).rglob('*.meta'):
            match = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(encoding='utf-8-sig'), re.M)
            if match:
                paths[match[1]] = meta.relative_to(source).as_posix()[:-5]
    enum = (ROOT / 'Assets/Scripts/Core/Definitions/CueId.cs').read_text()
    names = [s.strip() for s in re.search(r'enum CueId\s*{([^}]+)}', enum).group(1).split(',')]
    snapshots = {}
    for name in ('AudioSoundscapeDriverConfig', 'AudioDriverConfig'):
        path = ROOT / f'Assets/Resources/ScriptableObjects/Presentation/Audio/{name}.asset'
        text = path.read_text()
        banks = []
        for match in re.finditer(r'^  - (Cue|_cue|Id): ([^\n]+)\n(.*?)(?=^  - |^  _|\Z)', text, re.M | re.S):
            kind, key, body = match.groups()
            guids = re.findall(r'guid: ([a-f0-9]{32})', body)
            banks.append({'id': key if kind == 'Id' else names[int(key)], 'kind': kind,
                          'clips': [paths.get(guid, 'UNRESOLVED:' + guid) for guid in guids]})
        snapshots[name] = {'sha256': sha256(path), 'records': banks}
    results = {}
    counts = {}
    for run in runs:
        status = json.loads((run / 'inference-status.json').read_text())
        data = json.loads((run / 'inference-results.json').read_text())
        expected = json.loads((run / 'candidates.json').read_text())['candidates']
        if status['state'] != 'completed' or status['completed'] != len(data['results']) or len(expected) != len(data['results']):
            raise ValueError('Incomplete inference: ' + str(run))
        counts[run.name] = len(data['results'])
        for record in data['results']:
            if sha256(Path(record['absolute_path'])) != record['sha256']:
                raise ValueError('Input changed: ' + record['id'])
            results.setdefault(record['relative_path'], []).append({
                'run': run.name, 'id': record['id'], 'sha256': record['sha256'],
                'prompt': record['prompt'], 'model_output': record['model_output'],
                'human_listened': record['human_listened']})
    clips, cues = {}, []
    selection = ROOT / 'Assets/Audio/HunterRoster/SELECTION.md'
    for line in selection.read_text(encoding='utf-8').splitlines():
        cells = [s.strip() for s in line.split('|')]
        if len(cells) == 8 and cells[1].startswith('clip:'):
            clips[cells[1][5:]] = cells[2]
        if len(cells) == 8 and cells[1].startswith('cue:'):
            cues.append(cells)
    report = []
    for row in cues:
        chosen = []
        if row[4] not in ('silence', 'missing'):
            for key in (row[4] + ',' + row[5]).split(','):
                if key.strip() == '-':
                    continue
                path = clips[key.strip()]
                if not path.startswith('Assets/External/') or path not in results:
                    raise ValueError('Selected clip has no vendor/model provenance: ' + path)
                chosen.append({'path': path, 'evidence': results[path]})
        report.append({'cue': row[1][4:], 'bank': row[2], 'gain': float(row[3]),
                       'status': row[4] if not chosen else 'pending owner listening', 'clips': chosen})
    return {'serialized_snapshot_not_live_playback': snapshots, 'completed_runs': counts,
            'unique_analyzed_paths': len(results), 'selection_sha256': sha256(selection),
            'cues': report, 'human_listening': 'pending owner',
            'limitation': 'MOSS prompt sensitivity is not perceptual acceptance; raw conflicts are retained.'}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source-root', type=Path, required=True)
    parser.add_argument('--runs', nargs='+', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if not args.output.resolve().is_relative_to(ROOT / 'Logs') or args.output.exists():
        raise ValueError('Use a new checkout-local Logs report.')
    result = audit(args.source_root, args.runs)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps({'completed_runs': result['completed_runs'], 'unique_analyzed_paths': result['unique_analyzed_paths'],
                      'cues': len(result['cues']), 'output': str(args.output)}, indent=2))
    for name, snapshot in result['serialized_snapshot_not_live_playback'].items():
        for row in snapshot['records']:
            if row['id'] in ('Presence', 'Detection', 'Chase', 'Death', 'EnemyWindup', 'EnemyScream', 'hunter.turn', 'hunter.cake-reaction'):
                print(name, row['id'], row['clips'])


if __name__ == '__main__':
    main()
