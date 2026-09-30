// ============================================================================
// MenuPauseOwnershipTests.cs
// ============================================================================
// PURPOSE:
//   Exercises pause ownership through the real Menu driver and manager boundaries.
//   Teardown must restore a usable engine clock even with stale or overlapping owners.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Menu.
// KEY RESPONSIBILITIES:
//   - Cover resume, disable, destroy, invalid baselines, ownership transfer and editor exit.
// DEPENDENCIES:
//   NUnit, UnityEngine, UnityEditor, Menu runtime and editor safety tooling.
// USAGE NOTES:
//   Transient Edit Mode objects; invoke lifecycle by reflection, never SendMessage.
//   Native scene unload and Play Mode notifications remain coordinator checks.
//   Finally restores time and Session singletons even when assertions fail.
// ============================================================================
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Editor.Menu;
using Worsen.Presentation.Menu;
namespace Worsen.Tests.Menu
{
    public sealed class MenuPauseOwnershipTests
    {
        private readonly List<GameObject> _owned = new List<GameObject>();
        [TearDown]
        public void TearDown()
        {
            try
            {
                foreach (var owner in _owned)
                {
                    if (owner == null) continue;
                    owner.GetComponent<MenuDriver>().Teardown();
                    Object.DestroyImmediate(owner);
                }
                _owned.Clear();
            }
            finally { PauseFixtureCleanup.Restore(); }
        }
        [TestCase("OnDisable")]
        [TestCase("OnDestroy")]
        [TestCase("Teardown")]
        public void OwnerLifecycleRestoresTimeScaleExactlyOnce(string callback)
        {
            var driver = Driver(); Time.timeScale = .75f;
            driver.SetRunState(true, true); driver.SetRunState(true, true);
            Assert.That(Time.timeScale, Is.Zero);
            if (callback == "Teardown") driver.Teardown(); else Invoke(driver, callback);
            Assert.That(Time.timeScale, Is.EqualTo(.75f));
            Time.timeScale = .5f; driver.Teardown();
            Assert.That(Time.timeScale, Is.EqualTo(.5f), "An already released owner must not write global time.");
        }
        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidRecordedBaselineRestoresNormalTime(float previous)
        {
            var driver = Driver(); Time.timeScale = 1f; driver.SetRunState(true, true);
            State(driver).PreviousTimeScale = previous;
            driver.SetRunState(true, false);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
        [Test]
        public void StaleZeroIsNeverCapturedAsTheRestoreBaseline()
        {
            var driver = Driver(); Time.timeScale = 0f;
            driver.SetRunState(true, true);
            Assert.That(State(driver).PreviousTimeScale, Is.EqualTo(1f));
            driver.SetRunState(true, false);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
        [Test]
        public void NewOwnerInheritsTheUnpausedBaselineAndOldTeardownCannotReleaseIt()
        {
            var old = Driver(); var next = Driver(); Time.timeScale = .75f;
            old.SetRunState(true, true); next.SetRunState(true, true);
            Assert.That(State(next).PreviousTimeScale, Is.EqualTo(.75f));
            old.Teardown(); Assert.That(Time.timeScale, Is.Zero);
            next.Teardown(); Assert.That(Time.timeScale, Is.EqualTo(.75f));
        }
        [Test]
        public void ManagerDisableExplicitlyReleasesItsDriver()
        {
            var driver = Driver(); var manager = driver.gameObject.AddComponent<MenuManager>();
            typeof(MenuManager).GetField("_driver", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, driver);
            Time.timeScale = .75f; driver.SetRunState(true, true);
            Invoke(manager, "OnDisable");
            Assert.That(Time.timeScale, Is.EqualTo(.75f));
        }
        [Test]
        public void EditorExitReleasesOwnershipAndLogsOnlyOnce()
        {
            var driver = Driver(); Time.timeScale = .75f; driver.SetRunState(true, true);
            var hook = typeof(MenuPauseSafety).GetMethod("OnPlayModeStateChanged", BindingFlags.Static | BindingFlags.NonPublic);
            LogAssert.Expect(LogType.Log, "Menu pause released on Play Mode exit; Time.timeScale restored to 1.");
            hook.Invoke(null, new object[] { PlayModeStateChange.ExitingPlayMode });
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            driver.Teardown();
            hook.Invoke(null, new object[] { PlayModeStateChange.EnteredEditMode });
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            LogAssert.NoUnexpectedReceived();
        }
        private MenuDriver Driver()
        {
            var owner = new GameObject("Pause ownership test"); owner.SetActive(false);
            _owned.Add(owner); return owner.AddComponent<MenuDriver>();
        }
        private static MenuDriverState State(MenuDriver driver) => (MenuDriverState)typeof(MenuDriver)
            .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
        private static void Invoke(object target, string name) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
