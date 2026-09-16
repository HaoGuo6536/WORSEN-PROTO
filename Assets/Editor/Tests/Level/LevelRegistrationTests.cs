// ============================================================================
// LevelRegistrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises marker lifecycle through the real Driver, Manager and Registry.
//   These focused scene-object tests verify that initial enable order and owner
//   re-enabling cannot lose markers or duplicate registrations.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Level.
// KEY RESPONSIBILITIES:
//   - Check initial reconciliation, enable/disable symmetry and repeated initialization.
//   - Exercise actual scene unload without explicit Level teardown, and retain
//     authoring diagnostics for an invalid room removal in a surviving scene.
// DEPENDENCIES:
//   - Domain Level components, Core marker records, UnityEditor serialization,
//     NUnit and Unity Test Framework Play Mode transition instructions.
// USAGE NOTES:
//   Editor-discovered integration tests enter Play Mode before creating components,
//   so Unity invokes their normal OnEnable/OnDisable callbacks. The installed Test
//   Framework supplies its isolated bootstrap scene and restores the previous scene
//   setup; this fixture only creates/destroys its own temporary root. The coordinator
//   runs these tests under the Unity lease, including the domain reload transitions.
//   Editor-runner null yields do not imply a player-loop frame; diagnostic checks
//   wait for observed frame advancement before restoring an invalidated marker.
//   The fixture temporarily enables background simulation after entering Play Mode
//   and restores the prior runtime setting in teardown; project settings are untouched.
//   Exact Level Error expectations and the runner's unexpected-error checks verify
//   diagnostics. Whole-editor silence is outside this fixture's contract: ordinary
//   bridge reload logs remain visible without becoming lifecycle assertion failures.
// ============================================================================

using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Level;

namespace Worsen.Tests.Level
{
    public sealed class LevelRegistrationTests
    {
        private GameObject _root;
        private LevelManager _manager;
        private LevelMarker _room;
        private LevelMarker _anchor;
        private bool _previousRunInBackground;
        private bool _restoreRunInBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True, "Level lifecycle tests require actual Play Mode callbacks.");
            _previousRunInBackground = Application.runInBackground;
            _restoreRunInBackground = true;
            Application.runInBackground = true;
            _root = new GameObject("Level registration test");
            _root.SetActive(false);
            _root.AddComponent<LevelDriver>();
            _root.AddComponent<LevelMarkerRegistry>();
            _manager = _root.AddComponent<LevelManager>();
            _room = AddMarker(1, LevelMarkerKind.Room);
            AddMarker(90, LevelMarkerKind.ExitMarker);
            _anchor = AddMarker(100, LevelMarkerKind.CakeAnchor);
            _root.SetActive(true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (_manager != null) _manager.Teardown();
                if (_root != null) Object.DestroyImmediate(_root);
                _root = null;
                _manager = null;
                _room = null;
                _anchor = null;
            }
            finally
            {
                if (_restoreRunInBackground) Application.runInBackground = _previousRunInBackground;
                _restoreRunInBackground = false;
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void InitializeReconcilesAlreadyEnabledMarkersAndIsRepeatable()
        {
            _manager.Initialize();
            _manager.Initialize();
            Assert.That(_manager.ReadOnlyState.IsReady, Is.True);
            Assert.That(_root.GetComponent<LevelMarkerRegistry>().Count, Is.EqualTo(3));
            Assert.That(_manager.ReadOnlyState.Graph.Anchors.Count, Is.EqualTo(1));
        }

        [Test]
        public void MarkerEnableDisableFlowsThroughOwnerWithoutDuplicates()
        {
            _manager.Initialize();
            _anchor.enabled = false;
            Assert.That(_manager.ReadOnlyState.Graph.Anchors.Count, Is.Zero);
            _anchor.enabled = true;
            Assert.That(_manager.ReadOnlyState.Graph.Anchors.Count, Is.EqualTo(1));
            Assert.That(_root.GetComponent<LevelMarkerRegistry>().Count, Is.EqualTo(3));
        }

        [Test]
        public void OwnerReenableReconcilesChangesMadeWhileItWasDisabled()
        {
            _manager.Initialize();
            _manager.enabled = false;
            Assert.That(_manager.ReadOnlyState.IsReady, Is.False);
            _anchor.enabled = false;
            _manager.enabled = true;
            Assert.That(_manager.ReadOnlyState.IsReady, Is.True);
            Assert.That(_manager.ReadOnlyState.Graph.Anchors.Count, Is.Zero);
            _anchor.enabled = true;
            Assert.That(_manager.ReadOnlyState.Graph.Anchors.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SceneUnloadWithoutExplicitTeardownDoesNotReportPartialRegistration()
        {
            var scene = SceneManager.CreateScene("Level registration unload regression");
            SceneManager.MoveGameObjectToScene(_root, scene);
            var state = _manager.Initialize();
            Assert.That(state.IsReady, Is.True);

            // Exercise real engine unload; do not call LevelManager.Teardown first.
            var unload = SceneManager.UnloadSceneAsync(scene);
            Assert.That(unload, Is.Not.Null);
            yield return unload;
            yield return null;

            Assert.That(_root == null, Is.True);
            Assert.That(state.IsReady, Is.False);
            Assert.That(state.Graph, Is.Null);
        }

        [UnityTest]
        public IEnumerator ExplicitRoomDisableInvalidatesImmediatelyAndReportsOnNextActiveFrame()
        {
            _manager.Initialize();
            LogAssert.Expect(LogType.Error,
                "Level marker registration is incomplete: Every marker must reference an enabled room.");
            _room.enabled = false;
            Assert.That(_manager.ReadOnlyState.IsReady, Is.False);
            Assert.That(_manager.ReadOnlyState.Graph, Is.Null);
            yield return WaitForActiveFrames(2);

            _room.enabled = true;
            Assert.That(_manager.ReadOnlyState.IsReady, Is.True);
            Assert.That(_manager.ReadOnlyState.Graph.Anchors.Count, Is.EqualTo(1));
            yield return WaitForActiveFrames(2);
        }

        private static IEnumerator WaitForActiveFrames(int count)
        {
            // The Edit Mode runner advances on EditorApplication.update even after
            // EnterPlayMode. Two observed frames guarantee an intervening LateUpdate.
            var targetFrame = Time.frameCount + count;
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Application.isPlaying && Time.frameCount < targetFrame && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Application.isPlaying, Is.True, "Play Mode ended before the diagnostic check.");
            Assert.That(Time.frameCount, Is.GreaterThanOrEqualTo(targetFrame), "The active player loop did not advance.");
        }

        private LevelMarker AddMarker(int id, LevelMarkerKind kind)
        {
            var child = new GameObject(kind.ToString());
            child.transform.SetParent(_root.transform, false);
            var marker = child.AddComponent<LevelMarker>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("_id").intValue = id;
            serialized.FindProperty("_kind").enumValueIndex = (int)kind;
            serialized.FindProperty("_roomId").intValue = 1;
            serialized.FindProperty("_size").vector3Value = Vector3.one;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }
    }
}
