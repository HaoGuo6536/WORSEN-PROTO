#!/usr/bin/env python3
# ============================================================================
# test_architecture.py
# ============================================================================
# PURPOSE:
#   Exercise the offline architecture gate with isolated synthetic checkouts.
#   Boundaries and adversarial negative cases keep waivers and alarms measurable
#   without invoking Unity or changing any production source.
# ARCHITECTURAL ROLE:
#   Editor tool (§10) · Tests (§11, §13) · offline architecture enforcement.
# KEY RESPONSIBILITIES:
#   - Test layer references and GUID resolution, including malformed inputs.
#   - Test Session ordering, cycles and dependency evidence.
#   - Test each responsibility alarm at its boundary.
#   - Test waiver expiry, exact matching and the CLI exit/JSON contract.
# DEPENDENCIES:
#   - Python standard library, architecture.py and fixtures/cases.json.
# USAGE NOTES:
#   Temporary checkouts live under this owned tooling directory and are removed.
#   Fixture source strings are not Unity assets; no editor or lease is used.
# ============================================================================

import datetime as dt
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import architecture as arch

HERE = Path(__file__).resolve().parent
FIXTURES = json.loads((HERE / 'fixtures/cases.json').read_text(encoding='utf-8'))
PATH = 'Assets/Scripts/Session/Alpha/Controller/ExampleController.cs'
TODAY = dt.date(2026, 9, 30)


class ArchitectureTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='.architecture-test-', dir=HERE)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'Assets/Scripts').mkdir(parents=True)
        (self.root / 'Assets/Editor').mkdir(parents=True)
        self.put(arch.CONFIG, json.dumps({'sessionOrder': ['Beta', 'Alpha']}))
        self.put(arch.SPEC, FIXTURES['waiverHeading'])

    def put(self, path, content):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding='utf-8')
        return target

    def asm(self, layer, refs, **extra):
        return self.put(f'Assets/Scripts/{layer}/Worsen.{layer}.asmdef', json.dumps(
            dict(name='Worsen.' + layer, references=refs, autoReferenced=False, **extra)))

    def alarms(self, code):
        return arch.script_findings(PATH, code)

    def test_responsibilities_threshold(self):
        self.assertEqual(self.alarms(FIXTURES['header'])[0]['measured'], 6)
        self.assertEqual(self.alarms(FIXTURES['header'].replace('//   - Six\n', '')), [])

    def test_type_tests_in_single_method(self):
        result = self.alarms(FIXTURES['typeSwitch'])
        self.assertEqual([(f['alarm'], f['measured']) for f in result], [('type-switch', 3)])
        self.assertEqual(self.alarms(FIXTURES['typeSwitch'].replace('x is BazProfile', 'true')), [])

    def test_method_boundaries(self):
        code = 'class C { public bool A(object x) { return x is FooConfig || x is BarState; } public bool B(object x) => x is BazProfile; }'
        self.assertEqual(self.alarms(code), [])

    def test_implicit_private_method(self):
        self.assertEqual(self.alarms(FIXTURES['typeSwitch'].replace('public ', ''))[0]['measured'], 3)

    def test_comment_and_literal_noise(self):
        self.assertEqual(self.alarms(FIXTURES['literalNoise']), [])
        self.assertEqual(len(arch.code_only(FIXTURES['literalNoise'])), len(FIXTURES['literalNoise']))

    def test_fanout_distinct_excludes_own_core(self):
        code = 'using Worsen.Core.Definitions; using Worsen.Session.Alpha; ' + ''.join(
            f'using Worsen.Domain.System{i}; ' for i in range(6)) + 'class C {}'
        self.assertEqual(self.alarms(code)[0]['measured'], 6)
        self.assertEqual(self.alarms(code.replace('using Worsen.Domain.System5;', '')), [])

    def test_qualified_fanout(self):
        code = 'class C {' + ''.join(f'Worsen.Domain.System{i}.Type field{i};' for i in range(6)) + '}'
        self.assertEqual(self.alarms(code)[0]['measured'], 6)

    def test_public_event_threshold_and_multi_declarations(self):
        code = 'class ExampleManager { public event Action ' + ','.join('E' + str(i) for i in range(16)) + '; }'
        self.assertEqual(self.alarms(code)[0]['measured']['publicEvents'], 16)
        self.assertEqual(self.alarms(code.replace(',E15', '')), [])

    def test_relay_only_and_working_handler(self):
        self.assertEqual(self.alarms(FIXTURES['relay'])[0]['measured']['relayHandlers'], 1)
        self.assertEqual(self.alarms(FIXTURES['mixedHandler']), [])

    def test_half_relays_is_not_alarm(self):
        code = FIXTURES['relay'].replace('source.Changed += Handle;', 'source.Changed += Handle; source.Other += Work;').replace('private void Handle()', 'private void Work() { UpdateState(); } private void Handle()')
        self.assertEqual(self.alarms(code), [])

    def test_guarded_relay(self):
        self.assertTrue(arch.pure_relay('if (!IsPaused) Changed?.Invoke(fact);', ['Changed']))
        self.assertFalse(arch.pure_relay('if (Update()) Changed?.Invoke(fact);', ['Changed']))
        self.assertFalse(arch.pure_relay('Changed?.Invoke(Convert(fact));', ['Changed']))

    def test_assembly_layer_matrix(self):
        for layer in arch.LAYERS:
            self.asm(layer, sorted(arch.ALLOWED[layer]))
        self.assertEqual(arch.assembly_findings(self.root), [])
        self.asm('Core', ['Worsen.Domain'])
        self.assertEqual(arch.assembly_findings(self.root)[0]['measured'], 'Worsen.Domain')

    def test_vendor_references_are_pinned(self):
        self.asm('Presentation', sorted(arch.PRESENTATION_VENDOR) + ['Unapproved.Renderer'])
        self.assertEqual([f['measured'] for f in arch.assembly_findings(self.root)], ['Unapproved.Renderer'])

    def test_guid_reference(self):
        self.asm('Core', [])
        self.put('Assets/Scripts/Core/Worsen.Core.asmdef.meta', 'guid: abc123\n')
        self.asm('Session', ['GUID:abc123'])
        self.assertEqual(arch.assembly_findings(self.root), [])
        self.asm('Session', ['GUID:missing'])
        self.assertEqual(len(arch.assembly_findings(self.root)), 1)

    def test_auto_reference_warning_fails_gate(self):
        path = 'Assets/Scripts/Core/Worsen.Core.asmdef'
        self.put(path, json.dumps({'name': 'Worsen.Core', 'references': [], 'autoReferenced': True}))
        result = arch.check(self.root, TODAY)
        self.assertEqual(result['findings'][0]['severity'], 'warning')
        self.assertFalse(result['findings'][0]['waived'])

    def test_editor_and_tests_unrestricted(self):
        for tree in ('Assets/Editor', 'Assets/Editor/Tests', 'Assets/Tests'):
            self.put(tree + '/Test.asmdef', json.dumps({'name': 'Test', 'references': ['Anything'], 'autoReferenced': True}))
        self.assertEqual(arch.assembly_findings(self.root), [])

    def test_bad_asmdef(self):
        path = 'Assets/Scripts/Core/Worsen.Core.asmdef'
        for bad in ('{', '[]', '{"name":"Worsen.Core","references":"Worsen.Domain"}'):
            with self.subTest(bad=bad):
                self.put(path, bad)
                self.assertTrue(arch.assembly_findings(self.root))

    def test_session_edges_order_and_cycle(self):
        sources = {PATH: 'using Worsen.Session.Beta; namespace Worsen.Session.Alpha { class C {} }'}
        issues, edges, proposed = arch.session_graph(sources, ['Beta', 'Alpha'])
        self.assertEqual(issues, [])
        self.assertEqual((edges[0]['source'], edges[0]['target']), ('Alpha', 'Beta'))
        self.assertEqual(proposed, ['Beta', 'Alpha'])
        self.assertTrue(arch.session_graph(sources, ['Alpha', 'Beta'])[0])
        sources['Assets/Scripts/Session/Beta/Controller/C.cs'] = 'Worsen.Session.Alpha.Type x;'
        self.assertIsNone(arch.session_graph(sources, ['Beta', 'Alpha'])[2])

    def test_session_missing_and_noise(self):
        result = arch.session_graph({PATH: FIXTURES['literalNoise']}, [])
        self.assertEqual(result[1], [])
        self.assertEqual(result[0][0]['measured'], 'Alpha')

    def test_exact_waiver_and_expiry_boundary(self):
        self.put(PATH, FIXTURES['header'])
        for date, waived, expired in [('2026-09-29', 0, 1), ('2026-09-30', 1, 0), ('2026-10-01', 1, 0)]:
            self.put(arch.SPEC, FIXTURES['waiverHeading'] + f'| A-01 | {PATH} | responsibilities | coordinator | Split | {date} |\n')
            result = arch.check(self.root, TODAY)
            self.assertEqual((result['waived'], len(result['expired'])), (waived, expired))
        self.put(arch.SPEC, FIXTURES['waiverHeading'] + f'| A-01 | {PATH}x | responsibilities | coordinator | Split | 2026-10-01 |\n')
        self.assertEqual(arch.check(self.root, TODAY)['waived'], 0)

    def test_bad_waiver_and_wrong_heading(self):
        text = FIXTURES['waiverHeading'] + f'| A-01 | {PATH} | responsibilities | owner | Split | invalid |\n'
        self.assertTrue(arch.waivers(text, TODAY)[2])
        self.assertEqual(arch.waivers(text.replace('## Appendix A', '## Appendix B'), TODAY), ([], [], []))

    def test_invalid_config_and_missing_root(self):
        self.put(arch.CONFIG, '{"sessionOrder": ["Alpha", "Alpha"]}')
        self.assertTrue(any(f['alarm'] == 'input' for f in arch.check(self.root, TODAY)['findings']))
        self.assertTrue(arch.check(self.root / 'missing', TODAY)['findings'])

    def test_cli_json_exit_codes(self):
        output = self.root / 'out.json'
        table = self.root / 'report.md'
        command = [sys.executable, str(HERE / 'architecture.py'), '--root', str(self.root), '--json', str(output), '--markdown', str(table)]
        clean = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(clean.returncode, 0, clean.stderr)
        self.assertIn('ARCH_RESULT findings=0 waived=0 expired=0', clean.stdout)
        self.put(PATH, FIXTURES['header'])
        dirty = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(dirty.returncode, 1, dirty.stderr)
        self.assertEqual(len(json.loads(output.read_text())['findings']), 1)
        self.assertIn(f'| {PATH} | responsibilities | 6 |', table.read_text())


if __name__ == '__main__':
    unittest.main(verbosity=2)
