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
//   - Resolve owned references, initialize once, and pair enable/disable lifecycle.
//   - Forward summary display commands and republish one accepted restart click.
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

namespace Worsen.Presentation.Results
{
    public sealed class ResultsManager : MonoBehaviour
    {
        [SerializeField] private ResultsDriverConfig _config;
        [SerializeField] private ResultsDriver _driver;
        private bool _initialized;

        public event Action RestartRequested;

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
        public void Hide() { if (_driver != null) _driver.Hide(); }

        private void Awake() => Initialize();

        private void OnEnable()
        {
            Initialize();
            if (_driver == null) return;
            _driver.enabled = true;
            _driver.RestartClicked -= OnRestartClicked;
            _driver.RestartClicked += OnRestartClicked;
        }

        private void OnDisable()
        {
            if (_driver == null) return;
            _driver.RestartClicked -= OnRestartClicked;
            _driver.enabled = false;
        }

        private void OnDestroy()
        {
            if (_driver != null) _driver.Teardown();
        }

        private void OnRestartClicked() => RestartRequested?.Invoke();
    }
}

