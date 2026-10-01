// ============================================================================
// ResultsManager.cs
// ============================================================================
//
// PURPOSE:
//   Provides the scene-owned entry point for authoritative run results and restart interactions.
//   It owns the Results Driver lifecycle and forwards supplied facts without querying
//   gameplay systems or manipulating visual elements.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Results (Service system).
//
// KEY RESPONSIBILITIES:
//   - Forward no-floor seed facts and republish paired title navigation intent.
//   - Forward catch identity and completion; DriverState owns the pending summary.
//   - Resolve owned references, initialize once, and pair enable/disable lifecycle.
//   - Forward summary display commands and republish one accepted restart click.
//   - Forward persisted best depth and republish an optional fixed next-run seed.
//
// DEPENDENCIES:
//   - Worsen.Core RunSummary; no Domain, Session or sibling Presentation systems.
//
// USAGE NOTES:
//   - Scene-owned tier (§8); no singleton and no persistence across scene loads.
//   - Awake performs only internal initialization; scene assembly may call Initialize explicitly.
//   - Serialized config first, mirrored Resources fallback second; ResultsSetup restores wiring.
//
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Results
{
    public sealed class ResultsManager : MonoBehaviour
    {
        [SerializeField] private ResultsDriverConfig _config;
        [SerializeField] private ResultsDriver _driver;
        private bool _initialized;

        public event Action RestartRequested;
        public event Action ReturnToTitleRequested;
        public event Action<bool, int> RestartWithSeedRequested;

        public void Initialize()
        {
            if (_initialized) return;
            if (_config == null) _config = Resources.Load<ResultsDriverConfig>("ScriptableObjects/Presentation/Results/ResultsDriverConfig");
            if (_driver == null) _driver = GetComponent<ResultsDriver>();
            if (_driver == null) _driver = gameObject.AddComponent<ResultsDriver>();
            _driver.Initialize(_config);
            _driver.enabled = isActiveAndEnabled;
            _initialized = _config != null;
        }

        public void Show(RunSummary summary) { if (_driver != null) _driver.Show(summary); }
        public void ShowNoFloor(int seed) { if (_driver != null) _driver.ShowNoFloor(seed); }
        public void SetGenerationSeed(int seed) { if (_driver != null) _driver.SetGenerationSeed(seed); }
        public void SetBestDepth(int depth) { if (_driver != null) _driver.SetBestDepth(depth); }
        public void PrepareCatch(EntityId player) { if (_driver != null) _driver.PrepareCatch(player); }
        public void EndCatch(EntityId player) { if (_driver != null) _driver.EndCatch(player); }
        public void Hide() { if (_driver != null) _driver.Hide(); }

        private void Awake() => Initialize();

        private void OnEnable()
        {
            Initialize();
            if (_driver == null) return;
            _driver.enabled = true;
            _driver.RestartClicked -= OnRestartClicked;
            _driver.RestartClicked += OnRestartClicked;
            _driver.RestartWithSeedClicked -= OnRestartWithSeedClicked;
            _driver.RestartWithSeedClicked += OnRestartWithSeedClicked;
            _driver.ReturnToTitleClicked -= OnReturnToTitleClicked;
            _driver.ReturnToTitleClicked += OnReturnToTitleClicked;
        }

        private void OnDisable()
        {
            if (_driver == null) return;
            _driver.RestartClicked -= OnRestartClicked;
            _driver.enabled = false;
            _driver.RestartWithSeedClicked -= OnRestartWithSeedClicked;
            _driver.ReturnToTitleClicked -= OnReturnToTitleClicked;
        }

        private void OnDestroy()
        {
            if (_driver != null) _driver.Teardown();
        }

        private void OnRestartClicked() => RestartRequested?.Invoke();
        private void OnReturnToTitleClicked() => ReturnToTitleRequested?.Invoke();
        private void OnRestartWithSeedClicked(bool fixedSeed, int seed) => RestartWithSeedRequested?.Invoke(fixedSeed, seed);
    }
}

