#!/usr/bin/env python3
# ============================================================================
# test_select_tests.py
# ============================================================================
# PURPOSE: Exercise conservative selection against actual repository snapshots
#   and small synthetic source graphs; never start Unity or alter Git state.
# ARCHITECTURAL ROLE: Integration tooling tests; no runtime layer.
# KEY RESPONSIBILITIES:
#   - Cover test-only, system, Core, assembly, wiring and mixed change sets.
#   - Prove direct/transitive references, deletions and uncertainty widen coverage.
#   - Compare historical Git input with the equivalent explicit path input.
# DEPENDENCIES: Python unittest and select-tests.py; repository Git history.
# USAGE NOTES: Run python -m unittest discover -s tools/integration -p test_*.py.
# ============================================================================
import importlib.util
import json
import subprocess
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('select_tests', Path(__file__).with_name('select-tests.py'))
SELECTOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SELECTOR)


def fixture(owner, name, body=''):
    return f'namespace Worsen.Tests.{owner} {{ public class {name} {{ [Test] public void Case() {{ {body} }} }} }}'


class SelectorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sources = SELECTOR.snapshot(ROOT)

    def choose(self, *paths):
        return SELECTOR.select(paths, self.sources)

    def test_test_only_history_change_is_small(self):
        # adc1efd: only Greedy Door guidance assertions changed.
        changed = SELECTOR.git(ROOT, 'diff-tree', '--no-commit-id', '--name-only', '-r', 'adc1efd').decode().splitlines()
        result = SELECTOR.select(changed, self.sources)
        self.assertEqual('selected', result['scope'])
        self.assertIn('Worsen.Tests.Floor.FloorGuidanceIntegrationTests', result['fixtures'])
        # These fixtures share the two changed fixtures' public helper types.
        expected = {'Worsen.Tests.Floor.' + name for name in (
            'FloorCakeIntegrationTests', 'FloorCakeRulesTests', 'FloorAllocationDeterminismTests',
            'FloorGuidanceControllerTests', 'FloorGuidanceIntegrationTests',
            'FloorRoutingConsumerTests', 'FloorThemeConsumerTests')}
        self.assertEqual(expected | set(SELECTOR.SMOKE), set(result['fixtures']))
        self.assertTrue(set(SELECTOR.SMOKE) <= set(result['fixtures']))

    def test_single_system_folder_and_reference_coverage(self):
        result = self.choose('Assets/Scripts/Presentation/HUD/Driver/HUDPresenter.cs')
        self.assertEqual('selected', result['scope'])
        expected = [name for path, text in self.sources.items() if path.startswith(SELECTOR.TESTS + 'HUD/')
                    for name in SELECTOR.fixtures(path, text)]
        self.assertTrue(set(expected) <= set(result['fixtures']))

    def test_core_definitions_select_wiring(self):
        result = self.choose('Assets/Scripts/Core/Definitions/EntityId.cs')
        self.assertEqual('selected', result['scope'])
        self.assertIn('Worsen.Tests.Expedition.HorrorRunIntegrationTests', result['fixtures'])
        self.assertIn('Worsen.Tests.Scenes.SharedSceneRootWiringTests', result['fixtures'])

    def test_asmdef_packages_settings_infrastructure_and_vendor_force_full(self):
        for path in ('Assets/Editor/Worsen.Editor.asmdef', 'Packages/manifest.json',
                     'ProjectSettings/TagManager.asset', 'Assets/Editor/Tests/FixtureTimeSetUp.cs',
                     'Assets/Editor/Tests/Infrastructure/FixtureTimeGuardAttribute.cs',
                     'Assets/Editor/Testing/NativeTestRunnerSetup.cs', 'tools/offline-compile/Compile-Staged.ps1',
                     'Assets/Synaptic AI Pro/Editor/TestRunner/NexusTestRunnerService.cs',
                     'Assets/Scripts/Domain/VendorReferences.cs', 'VENDOR.md'):
            with self.subTest(path=path):
                result = self.choose(path)
                self.assertEqual('full', result['scope'])
                self.assertTrue(result['full_reasons'])
                self.assertEqual(result['inventory_count'], len(result['fixtures']))

    def test_scene_prefab_resources_and_setup_include_integration(self):
        for path in ('Assets/Scenes/HorrorRun.unity', 'Assets/Prefabs/Player.prefab',
                     'Assets/Resources/ScriptableObjects/Domain/Player/PlayerConfig.asset',
                     'Assets/Editor/Scenes/HorrorRunSceneSetup.cs'):
            with self.subTest(path=path):
                result = self.choose(path)
                self.assertEqual('selected', result['scope'])
                self.assertIn('Worsen.Tests.Expedition.HorrorRunIntegrationTests', result['fixtures'])
                self.assertIn('Worsen.Tests.Run.RunFactRelayWiringTests', result['fixtures'])

    def test_mixed_batch_unions_and_unknown_widens(self):
        changes = ['Assets/Scripts/Presentation/HUD/Driver/HUDPresenter.cs',
                   'Assets/Editor/Tests/Audio/AudioMixerSetupTests.cs', 'Assets/Scenes/HorrorRun.unity']
        result = self.choose(*changes)
        self.assertEqual('selected', result['scope'])
        for path in changes:
            self.assertTrue(set(self.choose(path)['fixtures']) <= set(result['fixtures']))
        self.assertEqual('full', self.choose(*changes, 'Assets/Art/new.fbx')['scope'])

    def test_direct_one_hop_and_test_helper_closure(self):
        sources = {p: text for p, text in self.sources.items() if p.startswith(SELECTOR.TESTS) and any(n in text for n in ('class ArchitectureConformanceTests', 'class FixtureTimeSetUpTests', 'class RunFactRelayWiringTests'))}
        sources.update({
            'Assets/Scripts/Domain/Alpha/Controller/AlphaController.cs': 'namespace Worsen.Domain.Alpha { class AlphaController {} }',
            'Assets/Scripts/Domain/Beta/Controller/BetaController.cs': 'namespace Worsen.Domain.Beta { class BetaController { AlphaController value; } }',
            SELECTOR.TESTS + 'Alpha/AlphaTests.cs': fixture('Alpha', 'AlphaTests'),
            SELECTOR.TESTS + 'Other/DirectTests.cs': fixture('Other', 'DirectTests', 'AlphaController x;'),
            SELECTOR.TESTS + 'Other/HopTests.cs': fixture('Other', 'HopTests', 'BetaController x;'),
            SELECTOR.TESTS + 'Other/Helper.cs': 'namespace Worsen.Tests.Other { class Helper { BetaController x; } }',
            SELECTOR.TESTS + 'Other/HelperTests.cs': fixture('Other', 'HelperTests', 'Helper x;'),
        })
        result = SELECTOR.select(['Assets/Scripts/Domain/Alpha/Controller/AlphaController.cs'], sources)
        self.assertEqual('selected', result['scope'])
        for name in ('DirectTests', 'HopTests', 'HelperTests'):
            self.assertIn('Worsen.Tests.Other.' + name, result['fixtures'])

    def test_removed_type_is_scanned_from_base(self):
        path = 'Assets/Scripts/Presentation/HUD/Driver/HUDPresenter.cs'
        current = dict(self.sources)
        old = current.pop(path)
        result = SELECTOR.select([path], current, {path: old})
        self.assertEqual('selected', result['scope'])
        self.assertIn('Worsen.Tests.HUD.HUDPresenterTests', result['fixtures'])
        self.assertEqual('full', SELECTOR.select([path], current)['scope'])

    def test_multiple_fixtures_helper_and_comments(self):
        text = '// class Fake { [Test] void Nope() {} }\nnamespace Worsen.Tests.X { class A { [Test] void One() {} class Helper {} } class B { [TestCase(1)] void Two(int x) {} } }'
        self.assertEqual(['Worsen.Tests.X.A', 'Worsen.Tests.X.B'], SELECTOR.fixtures('a.cs', text))
        self.assertEqual(['Worsen.Tests.X.C'], SELECTOR.fixtures('c.cs', 'namespace Worsen.Tests.X; class C { [Timeout(30), Test] void T() {} }'))

    def test_unsupported_fixture_forces_full(self):
        for text in ('namespace Worsen.Tests.X { class Generic<T> { [Test] void T() {} } }',
                     'namespace Worsen.Tests.X { class Outer { class Nested { [Test] void T() {} } } }',
                     'namespace Worsen.Tests.X { abstract class Base { [Test] void T() {} } }'):
            current = dict(self.sources)
            current[SELECTOR.TESTS + 'X/Unknown.cs'] = text
            self.assertEqual('full', SELECTOR.select(['README.md'], current)['scope'])

    def test_alias_and_inherited_fixtures_widen(self):
        for text in ('using T = NUnit.Framework.TestAttribute; namespace Worsen.Tests.X { class X { [T] void T() {} } }',
                     'namespace Worsen.Tests.X { class Base { [Test] void T() {} } class Derived : Base {} }'):
            current = dict(self.sources)
            current[SELECTOR.TESTS + 'X/Unknown.cs'] = text
            self.assertEqual('full', SELECTOR.select(['README.md'], current)['scope'])

    def test_delegates_are_types_but_foreach_record_is_not(self):
        text = 'namespace N { delegate void Changed(int x); delegate T Convert<T>(T x); struct Payload {} class C { void M() { foreach (var record in items) {} } } }'
        self.assertEqual({'Changed', 'Convert', 'Payload', 'C'}, SELECTOR.declarations(text))

    def test_meta_and_bad_paths_fail_closed(self):
        self.assertEqual('selected', self.choose('Assets/Scenes/HorrorRun.unity.meta')['scope'])
        for path in ('../outside.cs', 'Assets/Missing.cs', 'Assets\\Scenes\\HorrorRun.unity', 'unknown.bin'):
            self.assertEqual('full', self.choose(path)['scope'])

    def test_all_selected_fixtures_have_reasons_and_output_is_deterministic(self):
        changes = ['Assets/Scenes/HorrorRun.unity', 'README.md']
        a, b = self.choose(*changes), self.choose(*reversed(changes))
        self.assertEqual(a, b)
        self.assertTrue(all(a['fixture_reasons'][name] for name in a['fixtures']))

    def test_cli_reads_candidate_not_worktree(self):
        result = json.loads(subprocess.check_output(['python', str(Path(__file__).with_name('select-tests.py')),
                            '--root', str(ROOT), '--base', 'adc1efd^', '--candidate', 'adc1efd']))
        self.assertEqual('selected', result['scope'])
        self.assertEqual(SELECTOR.git(ROOT, 'rev-parse', 'adc1efd').decode().strip(), result['candidate'])

    def test_unresolvable_refs_emit_full_json(self):
        result = json.loads(subprocess.check_output(['python', str(Path(__file__).with_name('select-tests.py')),
                            '--root', str(ROOT), '--base', 'not-a-ref', '--candidate', 'HEAD']))
        self.assertEqual('full', result['scope'])
        self.assertTrue(result['full_reasons'])


if __name__ == '__main__':
    unittest.main()
