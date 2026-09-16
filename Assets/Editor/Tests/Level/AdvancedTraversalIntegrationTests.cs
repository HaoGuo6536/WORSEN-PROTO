// ============================================================================
// AdvancedTraversalIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Covers higher-ledge clearance and ground transitions missing from the authored
//   traversal trial, using default Player configuration and actual physics casts.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Level physical integration.
// KEY RESPONSIBILITIES:
//   - Compare a reachable mantle with the same physically obstructed landing.
//   - Exercise a small step, shallow slope, snap-sized drop and unsupported edge.
//   - Attach sampled actual motion and capsule contacts to ground-route failures.
// DEPENDENCIES:
//   - Core, Player, Level marker metadata, Hunter inspection, Run/Input and TagArena.
//   - NUnit, Unity Test Framework, UnityEditor serialization and Unity physics.
// USAGE NOTES:
//   Coordinator owns the Unity lease. Test Framework isolates/restores the scene.
//   Labeled temporary primitive arrangements at x=1000 are physics benchmarks, not
//   shipped Level authoring. Initial runtime poses are explicit; all subsequent
//   movement uses synthetic normal Run input. No fake probes/resolutions or tuning.
//   The fixture owns background simulation until teardown and restores its value.
//   Post-entry setup uses a fresh iterator so captured scene-load state is created
//   after the Test Framework resumes across the EnterPlayMode domain reload.
//   Hunters are disabled for the test scene only. Geometry and subscriptions are
//   removed during cleanup. Look-back/hardware/chased traversal remain separate.
// ============================================================================

using System;
using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Orchestrator;
using Worsen.Presentation.Input;
using Worsen.Session.Run;

namespace Worsen.Tests.Level
{
    public sealed class AdvancedTraversalIntegrationTests
    {
        private const string ArenaPath = "Assets/Scenes/TagArena.unity";
        private static readonly Vector3 Origin = new Vector3(1000f, 0f, 0f);
        private RunSessionManager _run;
        private InputManager _input;
        private PlayerManager _player;
        private PlayerProfile _profile;
        private PlayerMoverDriverConfig _config;
        private CapsuleCollider _capsule;
        private GameObject _arrangement;
        private bool _previousBackground, _restoreBackground;
        private Hash128 _profileHash, _configHash;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            yield return SetUpArena();
        }

        private IEnumerator SetUpArena()
        {
            _previousBackground = Application.runInBackground;
            _restoreBackground = true;
            Application.runInBackground = true;
            Assert.That(UnityEngine.Object.FindObjectsByType<TagArenaSceneRoot>(FindObjectsSortMode.None), Is.Empty);
            var load = SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return Until(() => load.isDone && RunSessionManager.Instance != null &&
                RunSessionManager.Instance.Scene == SceneKey.TagArena && RunSessionManager.Instance.Tick >= 3,
                "TagArena did not become ready for the physical arrangement.");
            _run = RunSessionManager.Instance;
            _input = InputManager.Instance;
            Assert.That(_input, Is.Not.Null);
            _input.SetInputEnabled(false);
            foreach (var hunter in UnityEngine.Object.FindObjectsByType<HunterManager>(FindObjectsSortMode.None))
                hunter.gameObject.SetActive(false);
            Assert.That(HunterRegistry.Items.Count, Is.Zero);
            _player = One<PlayerManager>();
            _capsule = _player.GetComponent<CapsuleCollider>();
            _profile = new SerializedObject(One<TagArenaSceneRoot>()).FindProperty("_playerProfile").objectReferenceValue as PlayerProfile;
            _config = new SerializedObject(_player.GetComponent<PlayerDriver>()).FindProperty("_config").objectReferenceValue as PlayerMoverDriverConfig;
            Assert.That(_profile, Is.Not.Null); Assert.That(_config, Is.Not.Null); Assert.That(_capsule, Is.Not.Null);
            _profileHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(_profile));
            _configHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(_config));
            _arrangement = new GameObject("[Test arrangement] Advanced Player traversal; not shipped Level content");
            _arrangement.transform.position = Origin;
            Box("Base floor", new Vector3(8f, -0.25f, 0f), new Vector3(20f, 0.5f, 6f));
        }

        [UnityTest]
        public IEnumerator DefaultCapsuleMantlesHigherLedgeAndRejectsItsBlockedLanding()
        {
            Assert.That(_profile.VaultMaximumHeight, Is.LessThan(1.6f));
            Assert.That(_profile.MantleMaximumHeight, Is.GreaterThanOrEqualTo(1.6f));
            var ledge = Box("1.6m mantle ledge", new Vector3(3f, 0.8f, 0f), new Vector3(2f, 1.6f, 3f));
            var target = Origin + new Vector3(3f, 1.6f, 0f);
            var marker = ledge.gameObject.AddComponent<LevelMarker>();
            var fields = new SerializedObject(marker);
            fields.FindProperty("_id").intValue = 90001;
            fields.FindProperty("_kind").enumValueIndex = (int)LevelMarkerKind.VaultSurface;
            fields.FindProperty("_roomId").intValue = 2;
            fields.FindProperty("_size").vector3Value = ledge.bounds.size;
            fields.FindProperty("_targetPosition").vector3Value = target;
            fields.ApplyModifiedPropertiesWithoutUndo();
            for (int trial = 0; trial < 2; trial++)
            {
                bool blocked = trial == 1;
                PlaceInitialPose(new Vector3(1f, 0.05f, 0f));
                if (blocked) Box("Blocked landing overhead", new Vector3(3f, 2.4f, 0f), new Vector3(1f, 0.3f, 2f));
                Physics.SyncTransforms();
                yield return DriveTicks(10, _ => default, null);
                var probe = _player.LastProbeRecord.Probe;
                Assert.That(probe.Grounded && probe.VaultCandidate, Is.True, "The real forward cast did not find the higher ledge.");
                Assert.That(probe.VaultHeight, Is.EqualTo(1.6f).Within(0.03f));
                Assert.That(probe.VaultTarget, Is.EqualTo(target));
                Assert.That(probe.VaultClearance > 0f, Is.EqualTo(!blocked));
                int succeeded = 0, failed = 0;
                bool traversed = false, penetrated = false;
                Vector3 outcome = Vector3.zero;
                float duration = 0f;
                yield return DriveTicks(80, tick => new InputFrame(Vector2.zero, Vector2.zero,
                    tick == 0 ? InputButtons.Jump : InputButtons.None,
                    tick == 0 ? InputButtons.Jump : InputButtons.None,
                    tick == 1 ? InputButtons.Jump : InputButtons.None), record =>
                {
                    traversed |= _player.LastMovementSample.MovementState == MovementState.Vault;
                    penetrated |= PenetratesArrangement();
                    foreach (var fact in _player.LastTraversalFacts.Where(fact => fact.Kind == TraversalKind.Mantle))
                    {
                        if (fact.Succeeded) succeeded++; else failed++;
                        outcome = record.Resolution.Position;
                        duration = fact.Duration;
                    }
                });
                Assert.That(penetrated, Is.False, "The real capsule penetrated the mantle arrangement beyond its skin tolerance.");
                Assert.That(succeeded, Is.EqualTo(blocked ? 0 : 1));
                Assert.That(failed, Is.EqualTo(blocked ? 1 : 0));
                Assert.That(traversed, Is.EqualTo(!blocked));
                Assert.That(duration, Is.EqualTo(_profile.MantleDuration).Within(0.0001f));
                if (!blocked)
                {
                    Assert.That(Vector3.Distance(outcome, target), Is.LessThanOrEqualTo(_profile.VaultCompletionTolerance + 0.001f));
                    Assert.That(_player.LastProbeRecord.Resolution.Grounded, Is.True);
                    Assert.That(_player.ReadOnlyState.Position.y, Is.EqualTo(1.6f).Within(0.05f));
                }
                else
                    Assert.That(_player.ReadOnlyState.Position.x, Is.LessThan(ledge.bounds.min.x),
                        "A rejected mantle still moved into its obstructed destination.");
                TestContext.Progress.WriteLine("Runtime mantle arrangement: blocked=" + blocked + "; succeeded=" + succeeded +
                    "; failed=" + failed + "; outcome=" + outcome + "; duration=" + duration);
            }
            AssertUnchangedAssets();
        }

        [UnityTest]
        public IEnumerator DefaultCapsuleStepsClimbsSlopeSnapsSmallDropAndFallsFromEdge()
        {
            Assert.That(_config.StepHeight, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(_config.GroundSnapDistance, Is.EqualTo(0.2f).Within(0.001f));
            Box("0.2m ordinary step", new Vector3(3f, 0.1f, 0f), new Vector3(2f, 0.2f, 3f));
            var normal = new Vector3(-1f, 4f, 0f).normalized;
            var ramp = Box("1:4 ordinary ramp", new Vector3(6f, 0.7f, 0f) - normal * 0.1f,
                new Vector3(Mathf.Sqrt(17f), 0.2f, 3f));
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(1f, 4f) * Mathf.Rad2Deg);
            Box("Upper plateau", new Vector3(9.5f, 0.6f, 0f), new Vector3(3f, 1.2f, 3f));
            Box("0.15m snap-down then unsupported edge", new Vector3(11.75f, 0.525f, 0f), new Vector3(1.5f, 1.05f, 3f));
            PlaceInitialPose(new Vector3(0f, 0.05f, 0f));
            yield return DriveTicks(10, _ => default, null);
            bool step = false, slope = false, plateau = false, snapped = false, airborne = false, landed = false;
            bool snapLostGround = false, invalid = false, penetrated = false;
            int jumpFacts = 0;
            int observedTicks = 0;
            var trace = new StringBuilder();
            float largestRise = 0f, previousY = _player.ReadOnlyState.Position.y;
            float furthestX = _player.ReadOnlyState.Position.x - Origin.x;
            yield return DriveTicks(300, _ => new InputFrame(
                _player.ReadOnlyState.Position.x < Origin.x + 15f ? Vector2.up : Vector2.zero,
                Vector2.zero, InputButtons.None, InputButtons.None, InputButtons.None), record =>
            {
                var position = record.Resolution.Position - Origin;
                bool grounded = record.Resolution.Grounded;
                furthestX = Mathf.Max(furthestX, position.x);
                if (observedTicks < 3 || observedTicks % 30 == 0 || observedTicks == 299)
                    trace.AppendLine("tick=" + record.Tick + "; input=" + record.Input.Move + "/" + record.Input.Held +
                        "; dt=" + record.DeltaTime + "; pos=" + position.ToString("F3") +
                        "; velocity=" + record.Resolution.Velocity.ToString("F3") +
                        "; ground=" + record.Probe.Grounded + "/" + grounded +
                        "; normal=" + record.Probe.GroundNormal.ToString("F3") +
                        "; state=" + _player.LastMovementSample.MovementState);
                observedTicks++;
                invalid |= !record.Resolution.Present || float.IsNaN(position.sqrMagnitude) || float.IsInfinity(position.sqrMagnitude) ||
                    position.y < -0.05f || Mathf.Abs(position.z) > 0.15f;
                penetrated |= PenetratesArrangement();
                largestRise = Mathf.Max(largestRise, position.y - previousY);
                previousY = position.y;
                jumpFacts += _player.LastTraversalFacts.Count(fact => fact.Kind == TraversalKind.Jump);
                step |= position.x > 2.4f && position.x < 3.7f && grounded && Mathf.Abs(position.y - 0.2f) < 0.05f;
                slope |= position.x > 4.5f && position.x < 7.5f && grounded && record.Probe.GroundNormal.x < -0.1f &&
                    Vector3.Angle(record.Probe.GroundNormal, Vector3.up) < _config.SlopeLimitDegrees &&
                    Mathf.Abs(position.y - (0.2f + (position.x - 4f) * 0.25f)) < 0.08f;
                plateau |= position.x > 8.4f && position.x < 10.7f && grounded && Mathf.Abs(position.y - 1.2f) < 0.05f;
                snapped |= position.x > 11.4f && position.x < 12.1f && grounded && Mathf.Abs(position.y - 1.05f) < 0.05f;
                snapLostGround |= position.x > 10.7f && position.x < 12.1f && !grounded;
                airborne |= position.x > 12.5f && position.y > 0.2f && !record.Probe.Grounded && !grounded;
                landed |= airborne && position.x > 12.5f && grounded && Mathf.Abs(position.y) < 0.05f &&
                    _player.LastTraversalFacts.Any(fact => fact.Kind == TraversalKind.Land && fact.Succeeded);
            });
            string diagnostic = "\nGround route observations=" + observedTicks + "; furthest x=" + furthestX +
                "; largest rise=" + largestRise + "; walk speed=" + _profile.WalkSpeed +
                "; capsule=" + _capsule.height + "/" + _capsule.radius + "\n" + trace + DescribeGroundRouteContacts();
            Assert.That(invalid || penetrated, Is.False, "Invalid or penetrating real capsule motion through the ground-transition route." + diagnostic);
            Assert.That(step && slope && plateau && snapped, Is.True,
                "Missing committed ground transition: step=" + step + "; slope=" + slope + "; plateau=" + plateau + "; snapped=" + snapped + diagnostic);
            Assert.That(snapLostGround, Is.False, "The snap-sized drop lost committed ground contact.");
            Assert.That(airborne && landed, Is.True, "The larger unsupported edge did not produce a real airborne descent and landing.");
            Assert.That(jumpFacts, Is.Zero, "The route requires ordinary movement only.");
            Assert.That(largestRise, Is.LessThanOrEqualTo(_config.StepHeight + 0.03f));
            Assert.That(_player.ReadOnlyState.Position.x, Is.GreaterThan(Origin.x + 14f));
            Assert.That(_player.LastProbeRecord.Resolution.Grounded, Is.True);
            AssertUnchangedAssets();
            TestContext.Progress.WriteLine("Runtime ground route: step/slope/snap/edge verified; largest tick rise=" + largestRise +
                "; final position=" + (_player.ReadOnlyState.Position - Origin));
        }

        private BoxCollider Box(string name, Vector3 position, Vector3 size)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "[Test] " + name;
            item.transform.SetParent(_arrangement.transform, false);
            item.transform.localPosition = position;
            item.transform.localScale = size;
            return item.GetComponent<BoxCollider>();
        }

        private string DescribeGroundRouteContacts()
        {
            Vector3 feet = _player.ReadOnlyState.Position;
            Vector3 forward = _player.transform.forward;
            Vector3 raised = feet + Vector3.up * _config.StepHeight;
            float advance = _profile.WalkSpeed * _player.LastProbeRecord.DeltaTime;
            new PlayerMoverPresenter().Capsule(raised, _capsule.height, _config.Radius, out Vector3 bottom, out Vector3 top);
            string overlaps = string.Join(",", Physics.OverlapCapsule(bottom, top,
                Mathf.Max(0.001f, _config.Radius - _config.SkinWidth), _config.CollisionMask, QueryTriggerInteraction.Ignore)
                .Where(item => !item.transform.IsChildOf(_player.transform)).Select(item => item.name));
            return "Final real capsule contacts: forward=" + DescribeCapsuleCast(feet, forward, advance + _config.SkinWidth) +
                "; up=" + DescribeCapsuleCast(feet, Vector3.up, _config.StepHeight) +
                "; raised overlaps=[" + overlaps + "]" +
                "; raised forward=" + DescribeCapsuleCast(raised, forward, advance + _config.SkinWidth) +
                "; down after one-tick advance=" + DescribeCapsuleCast(raised + forward * advance, Vector3.down,
                    _config.StepHeight + _config.GroundProbeDistance) +
                "; down after radius advance=" + DescribeCapsuleCast(raised + forward * _config.Radius, Vector3.down,
                    _config.StepHeight + _config.GroundProbeDistance);
        }

        private string DescribeCapsuleCast(Vector3 feet, Vector3 direction, float distance)
        {
            new PlayerMoverPresenter().Capsule(feet, _capsule.height, _config.Radius, out Vector3 bottom, out Vector3 top);
            var hits = Physics.CapsuleCastAll(bottom, top, Mathf.Max(0.001f, _config.Radius - _config.SkinWidth),
                direction, distance, _config.CollisionMask, QueryTriggerInteraction.Ignore)
                .Where(hit => hit.collider != null && !hit.collider.transform.IsChildOf(_player.transform))
                .OrderBy(hit => hit.distance).Take(3);
            return "[" + string.Join(";", hits.Select(hit => hit.collider.name + ":distance=" + hit.distance.ToString("F4") +
                ":normal=" + hit.normal.ToString("F3") + ":angle=" + Vector3.Angle(hit.normal, Vector3.up).ToString("F2"))) + "]";
        }

        private void PlaceInitialPose(Vector3 position)
        {
            var id = _player.Id;
            _player.transform.SetPositionAndRotation(Origin + position, Quaternion.Euler(0f, 90f, 0f));
            _player.Initialize(_profile, new EntityContext(id, _run.RandomSource));
            Physics.SyncTransforms();
        }

        private bool PenetratesArrangement() => _arrangement.GetComponentsInChildren<Collider>().Any(other =>
            Physics.ComputePenetration(_capsule, _capsule.transform.position, _capsule.transform.rotation,
                other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > _config.SkinWidth + 0.01f);

        private IEnumerator DriveTicks(int required, Func<int, InputFrame> next, Action<InputProbeRecord> observe)
        {
            int ticks = 0;
            Action before = () => _run.ReceiveInput(ticks < required ? next(ticks) : default);
            Action<InputProbeRecord> record = value => { if (ticks >= required) return; observe?.Invoke(value); ticks++; };
            _run.BeforeTick += before; _run.PlayerProbeRecorded += record;
            try { yield return Until(() => ticks >= required, "Normal Run ticks did not complete the physical trial: " + ticks + "/" + required); }
            finally { _run.BeforeTick -= before; _run.PlayerProbeRecorded -= record; }
        }

        private void AssertUnchangedAssets()
        {
            Assert.That(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(_profile)), Is.EqualTo(_profileHash));
            Assert.That(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(_config)), Is.EqualTo(_configHash));
        }

        private static T One<T>() where T : Component
        {
            var items = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.That(items.Length, Is.EqualTo(1), typeof(T).Name + " must have one active instance.");
            return items[0];
        }

        private static IEnumerator Until(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            for (int frame = 0; frame < 5400 && !condition() && Time.realtimeSinceStartup < deadline; frame++) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (_input != null) _input.SetInputEnabled(false);
                if (_arrangement != null) UnityEngine.Object.DestroyImmediate(_arrangement);
            }
            finally
            {
                if (_restoreBackground) Application.runInBackground = _previousBackground;
                _restoreBackground = false;
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
