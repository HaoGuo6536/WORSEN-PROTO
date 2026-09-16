// ============================================================================
// ResultsDriverTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies the results Driver-to-Manager fact relay with a real UI Toolkit document.
//   It exercises disable/re-enable and document replacement, where stale callbacks
//   could otherwise cause duplicate requests or accept clicks on a hidden display.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Results.
//
// KEY RESPONSIBILITIES:
//   - Verify one restart fact per display cycle and none while disabled or hidden.
//   - Recreate the document and confirm restored summary text and current-root binding.
//   - Distinguish lost retained state from an unfinished document rebind.
//
// DEPENDENCIES:
//   - Worsen.Core, Worsen.Presentation.Results, NUnit and Unity UI Toolkit.
//
// USAGE NOTES:
//   - Editor-only fixture enters Play Mode before creating transient objects and configs.
//   - Test Framework provides/restores its isolated bootstrap scene. UnityTearDown
//     destroys temporary objects and exits Play Mode even after assertion failures.
//   - Requires published Results.uxml/ResultsTheme.tss. Coordinator runs under Unity lease.
//   - Temporarily enables background execution for remote Editor frames and restores
//     the captured value before leaving Play Mode; no project settings are changed.
//   - Invokes the Driver click handler to isolate routing; pointer hit testing remains a live acceptance gate.
//   - Binding waits only observe real callbacks; they never refresh, re-show, or invoke lifecycle methods.
//   - Diagnostic trace preserves assertion/yield order and records identities before
//     waiter completion and caller resumption, plus real root detach/attach stacks.
//
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Presentation.Results;

namespace Worsen.Tests.Results
{
    public sealed class ResultsDriverTests
    {
        private GameObject _owner;
        private ResultsDriverConfig _config;
        private PanelSettings _panel;
        private UIDocument _document;
        private ResultsDriver _driver;
        private ResultsManager _manager;
        private int _requests;
        private bool _previousRunInBackground;
        private bool _backgroundSnapshotTaken;
        private readonly List<string> _bindingTrace = new List<string>();
        private readonly List<VisualElement> _observedRoots = new List<VisualElement>();
        private string _bindingCheckpoint, _lastBindingSignature;
        private int _omittedTraceEntries;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            _previousRunInBackground = Application.runInBackground;
            _backgroundSnapshotTaken = true;
            Application.runInBackground = true;
            // Allocate after domain reload; runtime lifecycle initializes the Manager.
            Assert.That(Object.FindObjectsByType<ResultsManager>(FindObjectsInactive.Include,
                FindObjectsSortMode.None), Is.Empty, "Requires the Test Framework isolated scene.");
            _requests = 0;
            _bindingTrace.Clear();
            _observedRoots.Clear();
            _lastBindingSignature = null;
            _omittedTraceEntries = 0;
            _bindingCheckpoint = "setup";
            _owner = new GameObject("Results routing test");
            _owner.SetActive(false);
            _config = ScriptableObject.CreateInstance<ResultsDriverConfig>();
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            var tree = Resources.Load<VisualTreeAsset>("UI/Presentation/Results/Results");
            var theme = Resources.Load<ThemeStyleSheet>("UI/Presentation/Results/ResultsTheme");
            Assert.That(tree, Is.Not.Null);
            Assert.That(theme, Is.Not.Null);
            _panel.themeStyleSheet = theme;
            _document = _owner.AddComponent<UIDocument>();
            _document.panelSettings = _panel;
            _document.visualTreeAsset = tree;
            _driver = _owner.AddComponent<ResultsDriver>();
            _manager = _owner.AddComponent<ResultsManager>();
            SetField(_manager, "_driver", _driver);
            SetField(_manager, "_config", _config);
            _manager.RestartRequested += OnRestartRequested;
            _owner.SetActive(true);
            UnityEditor.EditorApplication.update += OnDiagnosticEditorUpdate;
            Application.onBeforeRender += OnDiagnosticBeforeRender;
            TraceBinding("setup activated", true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CurrentDocumentRelaySurvivesDisableAndRejectsDuplicateOrHiddenClicks()
        {
            var summary = new RunSummary(62, 3, 1, 2, 1, 18, RunEndReason.Escaped);
            _manager.Show(summary);
            TraceBinding("initial Show", true);
            var retainedState = ReadField<ResultsDriverState>(_driver, "_state");
            AssertRetainedSummary(retainedState, "initial Show");
            yield return WaitForCurrentSummaryBinding(retainedState, "initial Show");
            TraceBinding("parent resumed: initial Show", true);
            Assert.That(_document.rootVisualElement.Q<Label>("run-time").text, Is.EqualTo("01:02"), BindingTrace());
            Invoke(_driver, "OnRestartClicked");
            Invoke(_driver, "OnRestartClicked");
            Assert.That(_requests, Is.EqualTo(1));
            Assert.That(_document.rootVisualElement.Q<Button>("restart-button").enabledSelf, Is.False);

            _manager.Hide();
            TraceBinding("explicit Hide", true);
            Assert.That(_document.rootVisualElement.Q<VisualElement>("results").style.display.value,
                Is.EqualTo(DisplayStyle.None));
            Invoke(_driver, "OnRestartClicked");
            Assert.That(_requests, Is.EqualTo(1));
            _manager.Show(summary);
            TraceBinding("second Show", true);
            AssertRetainedSummary(retainedState, "second Show before disable");
            _manager.enabled = false;
            TraceBinding("manager disabled", true);
            AssertRetainedSummary(retainedState, "manager disabled");
            Assert.That(_driver.enabled, Is.False, "Manager OnDisable must disable its owned Driver.");
            Assert.That(_document.rootVisualElement.style.display.value, Is.EqualTo(DisplayStyle.None));
            Invoke(_driver, "OnRestartClicked");
            Assert.That(_requests, Is.EqualTo(1));
            _manager.enabled = true;
            TraceBinding("manager re-enabled synchronously", true);
            AssertRetainedSummary(retainedState, "manager re-enabled");
            yield return WaitForCurrentSummaryBinding(retainedState, "manager re-enabled");
            TraceBinding("parent resumed: manager re-enabled (original line119)", true);
            Assert.That(_driver.enabled, Is.True);
            Assert.That(_document.rootVisualElement.Q<Label>("run-time").text, Is.EqualTo("01:02"), BindingTrace());

            var oldRoot = _document.rootVisualElement;
            var oldButton = oldRoot.Q<Button>("restart-button");
            _document.enabled = false;
            TraceBinding("document disabled", true);
            yield return null;
            Assert.That(oldRoot.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(oldButton.panel, Is.Null, "Disabled document must detach its old button.");
            Invoke(_driver, "OnRestartClicked");
            Assert.That(_requests, Is.EqualTo(1));
            _document.enabled = true;
            TraceBinding("document re-enabled synchronously", true);
            AssertRetainedSummary(retainedState, "document re-enabled");
            yield return WaitForCurrentSummaryBinding(retainedState, "document re-enabled");
            TraceBinding("parent resumed: document re-enabled", true);
            Assert.That(_document.rootVisualElement, Is.Not.SameAs(oldRoot), "UIDocument should create a new root.");
            Assert.That(_document.rootVisualElement.Q<Label>("run-time").text, Is.EqualTo("01:02"), BindingTrace());
            Assert.That(_document.rootVisualElement.Q<Button>("restart-button"), Is.Not.SameAs(oldButton));
            Invoke(_driver, "OnRestartClicked");
            Invoke(_driver, "OnRestartClicked");
            Assert.That(_requests, Is.EqualTo(2));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                // Diagnostic output must never prevent the original fixture cleanup.
                try
                {
                    TraceBinding("teardown before cleanup", true);
                    TestContext.WriteLine(BindingTrace());
                }
                catch (System.Exception error)
                {
                    UnityEngine.Debug.LogWarning("Results binding diagnostic output failed: " + error);
                }
                UnityEditor.EditorApplication.update -= OnDiagnosticEditorUpdate;
                Application.onBeforeRender -= OnDiagnosticBeforeRender;
                foreach (var root in _observedRoots)
                {
                    root.UnregisterCallback<AttachToPanelEvent>(OnDiagnosticAttach);
                    root.UnregisterCallback<DetachFromPanelEvent>(OnDiagnosticDetach);
                }
                if (_manager != null) _manager.RestartRequested -= OnRestartRequested;
                if (_owner != null) Object.DestroyImmediate(_owner);
                if (_panel != null) Object.DestroyImmediate(_panel);
                if (_config != null) Object.DestroyImmediate(_config);
            }
            finally
            {
                if (_backgroundSnapshotTaken)
                {
                    Application.runInBackground = _previousRunInBackground;
                    _backgroundSnapshotTaken = false;
                }
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        private void OnRestartRequested() => _requests++;

        private void AssertRetainedSummary(ResultsDriverState expected, string checkpoint)
        {
            Assert.That(expected, Is.Not.Null, checkpoint + ": Show must create the presentation state.");
            Assert.That(ReadField<ResultsDriverState>(_driver, "_state"), Is.SameAs(expected),
                checkpoint + ": toggling a view must retain its presentation state.");
            Assert.That(expected.Visible, Is.True, checkpoint + ": the supplied summary must remain visible.");
            Assert.That(expected.RunTime, Is.EqualTo("01:02"), checkpoint + ": the retained summary must not be reset.");
        }

        private IEnumerator WaitForCurrentSummaryBinding(ResultsDriverState expected, string checkpoint)
        {
            TraceBinding("wait entered: " + checkpoint, true);
            float deadline = Time.realtimeSinceStartup + 5f;
            int firstFrame = Time.frameCount;
            // Allow callback scheduling, then observe readiness; a yield count does not prove frame progress.
            yield return null;
            for (int observation = 0; observation < 120; observation++)
            {
                AssertRetainedSummary(expected, checkpoint);
                var currentRoot = _document.rootVisualElement;
                var currentLabel = currentRoot?.Q<Label>("run-time");
                if (currentRoot != null && currentLabel != null &&
                    ReferenceEquals(ReadField<VisualElement>(_driver, "_boundRoot"), currentRoot) &&
                    ReferenceEquals(ReadField<Label>(_driver, "_runTime"), currentLabel) &&
                    currentLabel.text == "01:02")
                {
                    TraceBinding("wait ready before yield-break: " + checkpoint, true);
                    yield break;
                }
                TraceBinding("wait observing: " + checkpoint, false);
                if (Time.realtimeSinceStartup >= deadline) break;
                yield return null;
            }

            var root = _document.rootVisualElement;
            var label = root?.Q<Label>("run-time");
            var boundLabel = ReadField<Label>(_driver, "_runTime");
            Assert.Fail(checkpoint + ": current document did not bind the retained summary within 120 observations / 5 seconds. " +
                "Frames=" + firstFrame + "->" + Time.frameCount +
                ", managerEnabled=" + _manager.isActiveAndEnabled + ", driverEnabled=" + _driver.isActiveAndEnabled +
                ", documentEnabled=" + _document.isActiveAndEnabled + ", panelAttached=" + (root?.panel != null) +
                ", boundRootIsCurrent=" + ReferenceEquals(ReadField<VisualElement>(_driver, "_boundRoot"), root) +
                ", storedLabelIsCurrent=" + ReferenceEquals(boundLabel, label) +
                ", stateText=" + expected.RunTime + ", currentText=" + (label?.text ?? "<missing>") +
                ", storedText=" + (boundLabel?.text ?? "<missing>"));
        }

        private void OnDiagnosticEditorUpdate() => TraceBinding("editor update", false, false);
        private void OnDiagnosticBeforeRender() => TraceBinding("before render", false, false);
        private void OnDiagnosticAttach(AttachToPanelEvent evt)
        {
            if (!ReferenceEquals(evt.target, evt.currentTarget)) return;
            TraceBinding("root attached id=" + Identity(evt.target), true, false);
        }
        private void OnDiagnosticDetach(DetachFromPanelEvent evt)
        {
            if (!ReferenceEquals(evt.target, evt.currentTarget)) return;
            TraceBinding("root detached id=" + Identity(evt.target), true, false);
            AppendTrace("Detach stack: " + new System.Diagnostics.StackTrace(1, true));
        }
        private void TraceBinding(string observation, bool force, bool changeCheckpoint = true)
        {
            if (_driver == null || _document == null) return;
            if (changeCheckpoint) _bindingCheckpoint = observation;
            var root = _document.rootVisualElement;
            if (root != null && !_observedRoots.Contains(root))
            {
                _observedRoots.Add(root);
                root.RegisterCallback<AttachToPanelEvent>(OnDiagnosticAttach);
                root.RegisterCallback<DetachFromPanelEvent>(OnDiagnosticDetach);
            }
            var label = root?.Q<Label>("run-time");
            var button = root?.Q<Button>("restart-button");
            var boundRoot = ReadField<VisualElement>(_driver, "_boundRoot");
            var boundLabel = ReadField<Label>(_driver, "_runTime");
            var boundButton = ReadField<Button>(_driver, "_restart");
            var state = ReadField<ResultsDriverState>(_driver, "_state");
            string signature = "state=" + Identity(state) + "/" + (state?.Visible.ToString() ?? "null") + "/" + state?.RunTime +
                "; roots=" + Identity(root) + "/" + Identity(boundRoot) + "; labels=" + Identity(label) + "/" + Identity(boundLabel) +
                "; text=" + (label?.text ?? "<null>") + "/" + (boundLabel?.text ?? "<null>") +
                "; buttons=" + Identity(button) + "/" + Identity(boundButton) + "; children=" + (root?.childCount ?? -1) +
                "; panels=" + Identity(root?.panel) + "/" + Identity(label?.panel) + "/" + Identity(boundLabel?.panel) +
                "; enabled=" + (_manager != null && _manager.isActiveAndEnabled) + "/" + _driver.isActiveAndEnabled + "/" + _document.isActiveAndEnabled +
                "; tree=" + (_document.visualTreeAsset != null ? _document.visualTreeAsset.GetInstanceID() : 0);
            if (force || signature != _lastBindingSignature)
                AppendTrace("checkpoint=" + _bindingCheckpoint + "; observation=" + observation +
                    "; frame=" + Time.frameCount + "; realtime=" + Time.realtimeSinceStartupAsDouble.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "; " + signature);
            _lastBindingSignature = signature;
        }
        private void AppendTrace(string value)
        {
            if (_bindingTrace.Count < 100) _bindingTrace.Add(value);
            else _omittedTraceEntries++;
        }
        private string BindingTrace()
        {
            var text = new StringBuilder("RESULTS_BINDING_TRACE (observational; assertion and yield order retained)\n");
            foreach (string entry in _bindingTrace) text.AppendLine(entry);
            text.Append("Omitted entries: ").Append(_omittedTraceEntries);
            return text.ToString();
        }
        private static string Identity(object value) => value == null ? "null" : RuntimeHelpers.GetHashCode(value).ToString("X8");

        private static T ReadField<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void Invoke(object target, string name)
        {
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }
    }
}
