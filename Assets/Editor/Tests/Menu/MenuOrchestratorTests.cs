// ============================================================================
// MenuOrchestratorTests.cs
// ============================================================================
// PURPOSE:
//   Exercises title and preferences through the real Menu and Settings boundaries.
//   Inactive objects isolate routing from native UI, disk IO and scene loading.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Menu routing.
// KEY RESPONSIBILITIES:
//   - Verify one title start, settings acknowledgements, history and paired subscriptions.
// DEPENDENCIES:
//   NUnit, Core, Menu, Settings, Progression, scene composition and fake Settings IO.
// USAGE NOTES:
//   Edit Mode boundary tests; the coordinator runs them in Unity. Reflection injects
//   owned state and invokes lifecycle/click boundaries, never replaces routing logic.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Orchestrator;
using Worsen.Presentation.Menu;
using Worsen.Presentation.Input;
using Worsen.Session.Progression;
using Worsen.Session.Settings;
using Worsen.Tests.Settings;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Menu
{
    public sealed class MenuOrchestratorTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private MenuManager _menu;
        private MenuDriver _driver;
        private MenuDriverState _menuState;
        private SettingsManager _settings;
        private FakeSettingsFileDriver _files;
        private SettingsConfig _defaults;
        private ProgressionConfig _progressionConfig;
        private ProgressionSessionManager _progression;
        private MenuOrchestrator _route;
        private HorrorRunSceneRoot _root;
        private InputManager _input;
        private Action _start;

        [SetUp]
        public void SetUp()
        {
            Assert.That(ProgressionSessionManager.Instance, Is.Null);
            _defaults = ScriptableObject.CreateInstance<SettingsConfig>();
            _progressionConfig = ScriptableObject.CreateInstance<ProgressionConfig>();
            _settings = Component<SettingsManager>();
            _files = _settings.gameObject.AddComponent<FakeSettingsFileDriver>();
            var settingsState = new SettingsBehaviorState();
            Set(_settings, "_state", settingsState);
            Set(_settings, "_controller", new SettingsController(settingsState, _defaults.Defaults));
            Set(_settings, "_driver", _files);
            _menu = Component<MenuManager>();
            _driver = _menu.gameObject.AddComponent<MenuDriver>();
            _menuState = (MenuDriverState)Field(_driver, "_state").GetValue(_driver);
            Set(_menu, "_driver", _driver); Set(_menu, "_initialized", true);
            Invoke(_menu, "OnEnable");
            _progression = Component<ProgressionSessionManager>();
            var state = new ProgressionSessionBehaviorState();
            Set(_progression, "state", state); Set(_progression, "config", _progressionConfig);
            Set(_progression, "controller", new ProgressionSessionController(state, _progressionConfig, new System.Random(731)));
            typeof(ProgressionSessionManager).GetProperty("Instance").SetValue(null, _progression);
            _root = Component<HorrorRunSceneRoot>();
            Set(_root, "_progression", _progression); Set(_root, "_seed", 731);
            _start = (Action)Delegate.CreateDelegate(typeof(Action), _root, Method(_root, "StartFromTitle"));
            _input = Component<InputManager>();
            Set(_input, "_driver", _input.GetComponent<PlayerInputDriver>()); Set(_input, "_initialized", true);
            Invoke(_input, "OnEnable");
            _route = Component<MenuOrchestrator>();
            _route.Configure(_menu, _settings, _start, _progression, _input);
            Invoke(_route, "OnEnable");
        }
        [TearDown]
        public void TearDown()
        {
            if (_route != null) Invoke(_route, "OnDisable");
            if (_menu != null) Invoke(_menu, "OnDisable");
            if (_input != null) Invoke(_input, "OnDisable");
            for (int i = _objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            typeof(ProgressionSessionManager).GetProperty("Instance").SetValue(null, null);
            Object.DestroyImmediate(_defaults); Object.DestroyImmediate(_progressionConfig);
        }
        [Test]
        public void TitleClickStartsProgressionOnceWithSceneSeedAndAcknowledgesMenu()
        {
            int starts = 0;
            _progression.TransactionCommitted += (before, after, operation, choice) => starts++;
            _menu.ShowTitle();
            Assert.That(_progression.Snapshot.Revision, Is.Zero);
            Invoke(_driver, "OnStart"); Invoke(_driver, "OnStart");
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(_progression.Snapshot.Seed, Is.EqualTo(731));
            Assert.That(_progression.Snapshot.Phase, Is.EqualTo(ProgressionPhase.ChooseThreat));
            Assert.That(_menuState.TitleVisible || _menuState.Pending, Is.False);
            Assert.That(_settings.History.LifetimeRuns, Is.Zero, "History counts expedition ends, not starts.");
        }
        [TestCase(false)]
        [TestCase(true)]
        public void SettingsRoundTripPublishesRuntimeCopyAndActualSaveOutcome(bool fail)
        {
            string configBefore = JsonUtility.ToJson(_defaults);
            _settings.PublishCurrent();
            Assert.That(_menuState.Settings, Is.EqualTo(_defaults.Defaults));
            _menu.SetRunState(true, true);
            var value = new PlayerSettingsRecord(1, .27f, true, 107, false, true, false, .8f, .6f, .4f);
            Assert.That(new MenuPresenter().EditSettings(_menuState, value), Is.True);
            _files.FailWrites = fail;
            Invoke(_driver, "OnApply");
            Assert.That(_settings.Current, Is.EqualTo(value));
            Assert.That(_menuState.Settings, Is.EqualTo(value));
            Assert.That(JsonUtility.ToJson(_defaults), Is.EqualTo(configBefore));
            if (fail)
            {
                Assert.That(_menuState.Message, Does.Contain("not saved").And.Contain("Injected disk failure"));
                Assert.That(_files.Files, Is.Empty);
            }
            else
            {
                Assert.That(_menuState.Message, Is.EqualTo("Settings saved."));
                Assert.That(_files.LoadSettings(_defaults.Defaults), Is.EqualTo(value));
            }
        }
        [Test]
        public void EndHistoryCountsOnceAndPersistsBestDepth()
        {
            _progression.StartRun(731);
            var snapshot = _progression.Snapshot;
            Assert.That(_progression.ChooseThreat(snapshot.Choices[0].Id, snapshot.Revision), Is.True);
            snapshot = _progression.Snapshot;
            Assert.That(_progression.ChooseCurse(snapshot.Choices[0].Id, snapshot.Revision), Is.True);
            int generation = _progression.Snapshot.GenerationId;
            Assert.That(_progression.ConfirmFloorReady(generation), Is.True);
            Assert.That(_progression.EndRun(generation), Is.True);
            Assert.That(_progression.EndRun(generation), Is.False);
            Assert.That(_settings.History.LifetimeRuns, Is.EqualTo(1));
            Assert.That(_settings.History.BestDepth, Is.EqualTo(_progression.Snapshot.Round));
            var saved = _files.LoadHistory();
            Assert.That(saved.LifetimeRuns, Is.EqualTo(1));
            Assert.That(saved.BestDepth, Is.EqualTo(_settings.History.BestDepth));
        }
        [Test]
        public void IndependentInputPauseReachesMenuIntentButDoesNotAcknowledgeSimulation()
        {
            _menu.SetRunState(true, false);
            int requests = 0;
            _menu.PauseSelected += pause => { Assert.That(pause, Is.True); requests++; };
            var driver = _input.GetComponent<PlayerInputDriver>();
            ((Action)Field(driver, "PausePressed").GetValue(driver))();
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(_menuState.Pending, Is.True);
            Assert.That(_menuState.Paused, Is.False, "Only the simulation owner may acknowledge pause.");
        }
        [Test]
        public void ReconfigureDoesNotDuplicateAndDisableUnpairsEverySubscription()
        {
            _route.Configure(_menu, _settings, _start, _progression, _input);
            Invoke(_route, "OnEnable"); Invoke(_route, "OnEnable");
            Assert.That(((Delegate)Field(_menu, "StartClicked").GetValue(_menu)).GetInvocationList().Length, Is.EqualTo(1));
            Invoke(_route, "OnDisable");
            foreach (string name in new[] { "StartClicked", "QuitClicked", "SettingsApplied" })
                Assert.That(Field(_menu, name).GetValue(_menu), Is.Null, name);
            foreach (string name in new[] { "SettingsChanged", "SaveCompleted" })
                Assert.That(Field(_settings, name).GetValue(_settings), Is.Null, name);
            Assert.That(Field(_progression, "TransactionCommitted").GetValue(_progression), Is.Null);
            Assert.That(Field(_input, "PausePressed").GetValue(_input), Is.Null);
            _settings.PublishCurrent();
            Assert.That(_menuState.SettingsReady, Is.False);
        }
        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " routing test");
            owner.SetActive(false); _objects.Add(owner); return owner.AddComponent<T>();
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static MethodInfo Method(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static void Invoke(object target, string name) => Method(target, name).Invoke(target, null);
    }
}
