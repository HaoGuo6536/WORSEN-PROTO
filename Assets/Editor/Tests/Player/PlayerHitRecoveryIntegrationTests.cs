// ============================================================================
// PlayerHitRecoveryIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the Player-owned hit routing and grace collision boundary using
//   temporary actors and geometry. These tests distinguish damage protection
//   from actual pass-through and verify restoration without global physics edits.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player integration.
// KEY RESPONSIBILITIES:
//   - Verify Manager effect delivery waits for Tick and publishes maximum-only health changes.
//   - Verify floor health hand-off and regeneration publish the live effective maximum.
//   - Check Core grace facts, source-independent absorption and symmetric cleanup.
//   - Check hunter-only capsule/ground filtering and the visible missing-layer fallback.
// DEPENDENCIES:
//   Core, Player, NUnit, UnityEditor serialization and Unity Test Framework logging.
// USAGE NOTES:
//   Coordinator runs these Edit Mode engine tests in Unity. The built-in Ignore
//   Raycast layer stands in for HunterBody; no project layers or matrix are edited.
//   The warning test restores the session latch so fixture order cannot suppress it.
//   Runtime-only MonoBehaviour callbacks are explicitly invoked in Edit Mode lifecycle tests.
// ============================================================================
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Player
{
    public sealed class PlayerHitRecoveryIntegrationTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly Vector3 _origin = new Vector3(4000f, 0f, 0f);
        private PlayerProfile _profile;
        private PlayerMoverDriverConfig _config;
        private PlayerManager _player;
        private PlayerDriver _driver;
        private CapsuleCollider _capsule;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<PlayerProfile>();
            _config = ScriptableObject.CreateInstance<PlayerMoverDriverConfig>();
            SetLayer("Ignore Raycast");
            var actor = Make("Player", Vector3.zero);
            _driver = actor.AddComponent<PlayerDriver>();
            var driverData = new SerializedObject(_driver);
            driverData.FindProperty("_config").objectReferenceValue = _config;
            driverData.ApplyModifiedPropertiesWithoutUndo();
            _capsule = actor.GetComponent<CapsuleCollider>();
            _capsule.excludeLayers = 1 << 4;
            _player = actor.AddComponent<PlayerManager>();
            _player.Initialize(_profile, new EntityContext(new EntityId(1), new System.Random(1)));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject value in _objects) if (value != null) Object.DestroyImmediate(value);
            _objects.Clear();
            Object.DestroyImmediate(_profile);
            Object.DestroyImmediate(_config);
        }

        [TestCase(HitSource.Lunge, HitSeverity.Heavy)]
        [TestCase(HitSource.Hand, HitSeverity.Light)]
        [TestCase(HitSource.Projectile, HitSeverity.Heavy)]
        [TestCase(HitSource.Scream, HitSeverity.Light)]
        public void EverySourcePublishesGraceAndAbsorbsUntilEnd(HitSource source, HitSeverity severity)
        {
            var starts = new List<GraceWindowFact>();
            var ends = new List<GraceWindowFact>();
            int absorbed = 0, healthChanges = 0;
            _player.OnGraceStarted += starts.Add;
            _player.OnGraceEnded += ends.Add;
            _player.OnHealthChanged += (id, health, maximum) => healthChanges++;
            _player.OnHitAbsorbedByGrace += (id, hitSeverity, hitSource) =>
            { Assert.That(hitSource, Is.EqualTo(source)); absorbed++; };
            Assert.That(_player.ApplyHit(10f, _origin, severity, source), Is.True);
            Assert.That(_player.ApplyHit(100f, _origin, severity, source), Is.False);
            Assert.That(starts.Count, Is.EqualTo(1));
            Assert.That(absorbed, Is.EqualTo(1));
            Assert.That(healthChanges, Is.EqualTo(1));
            Assert.That(_player.ReadOnlyState.Health, Is.EqualTo(90f));
            Assert.That(_capsule.excludeLayers.value, Is.EqualTo((1 << 4) | (1 << 2)));
            _player.AdvanceRecovery(starts[0].EndTick);
            _player.AdvanceRecovery(starts[0].EndTick);
            Assert.That(ends.Count, Is.EqualTo(1));
            Assert.That(ends[0], Is.EqualTo(starts[0]));
            Assert.That(_capsule.excludeLayers.value, Is.EqualTo(1 << 4));
            Assert.That(_player.ApplyHit(10f, _origin, severity, source), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisableOrTeardownEndsGraceOnceAndRestoresContacts(bool teardown)
        {
            int ended = 0;
            _player.OnGraceEnded += fact => ended++;
            _player.ApplyHit(1f, _origin);
            if (teardown) _player.Teardown();
            else
            {
                _player.gameObject.SetActive(false);
                // This fixture never enters Play Mode: SetActive alone does not
                // dispatch OnDisable for a runtime-only MonoBehaviour here.
                typeof(PlayerManager).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_player, null);
            }
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(_capsule.excludeLayers.value, Is.EqualTo(1 << 4));
            _player.Teardown();
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void GracePassesThroughHunterButNotWorldAndRestoresBlocking()
        {
            Box("Hunter", new Vector3(0f, 1f, 1f), new Vector3(2f, 2f, 0.5f), 2);
            Box("World", new Vector3(0f, 1f, 3f), new Vector3(2f, 2f, 0.5f), 0);
            Physics.SyncTransforms();
            var blocked = _driver.Move(Vector3.forward * 4f, Vector3.forward, false, 0f, 1f);
            Assert.That(blocked.Position.z, Is.LessThan(_origin.z + 0.75f));
            _player.ApplyHit(1f, _origin);
            var passed = _driver.Move(Vector3.forward * 4f, Vector3.forward, false, 0f, 1f);
            Assert.That(passed.Position.z, Is.InRange(_origin.z + 1.5f, _origin.z + 2.75f));
            _driver.SetGraceActive(false);
            var restored = _driver.Move(Vector3.back * 4f, Vector3.back, false, 0f, 1f);
            Assert.That(restored.Position.z, Is.GreaterThan(_origin.z + 1.25f));
        }

        [Test]
        public void GraceGroundQueriesDoNotUseHunterBodiesAsSupport()
        {
            Box("Hunter support", new Vector3(0f, -0.25f, 0f), new Vector3(4f, 0.5f, 4f), 2);
            Physics.SyncTransforms();
            Assert.That(_driver.Probe().Grounded, Is.True);
            _player.ApplyHit(1f, _origin);
            Assert.That(_driver.Probe().Grounded, Is.False);
            _driver.SetGraceActive(false);
            Assert.That(_driver.Probe().Grounded, Is.True);
        }

        [Test]
        public void MissingLayerWarnsOnceButDamageGraceStillWorks()
        {
            var warnings = (PlayerDriverState)typeof(PlayerDriver).GetField("SessionWarnings", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            bool previous = warnings.MissingHunterLayerWarned;
            try
            {
                warnings.MissingHunterLayerWarned = false;
                SetLayer("MissingPlan013HunterBody");
                _player.Initialize(_profile, new EntityContext(new EntityId(2), new System.Random(2)));
                LogAssert.Expect(LogType.Warning, "Player hunter-body layer 'MissingPlan013HunterBody' is missing; hit grace still blocks damage, but hunter pass-through is disabled for this session.");
                Assert.That(_player.ApplyHit(1f, _origin), Is.True);
                Assert.That(_player.ApplyHit(100f, _origin), Is.False);
                _player.AdvanceRecovery(1);
                _driver.SetGraceActive(true);
                Assert.That(_capsule.excludeLayers.value, Is.EqualTo(1 << 4));
                LogAssert.NoUnexpectedReceived();
            }
            finally { warnings.MissingHunterLayerWarned = previous; }
        }

        private void SetLayer(string name)
        {
            var data = new SerializedObject(_config);
            data.FindProperty("_hunterBodyLayer").stringValue = name;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void ActiveEffectsWaitForTickAndPublishMaximumOnlyChanges()
        {
            var effects = ScriptableObject.CreateInstance<PlayerEffectConfig>();
            try
            {
                var data = new SerializedObject(_player);
                data.FindProperty("_effectConfig").objectReferenceValue = effects;
                data.ApplyModifiedPropertiesWithoutUndo();
                _player.Initialize(_profile, new EntityContext(new EntityId(1), new System.Random(1)));
                _player.ApplyRunModifiers(20f, 100f, 1f);
                _player.SetHealthRecoveryEffects(0f);
                int changes = 0;
                _player.OnHealthChanged += (id, health, maximum) => changes++;
                _player.SetActiveEffects(PlayerEffectUtilityTests.Effects("thin-skin"));
                Assert.That(_player.ReadOnlyState.MaxHealth, Is.EqualTo(100f));
                Assert.That(changes, Is.Zero);
                _player.Tick(default, 1f / 60f, 1);
                Assert.That(_player.ReadOnlyState.Health, Is.EqualTo(20f));
                Assert.That(_player.ReadOnlyState.MaxHealth, Is.EqualTo(75f));
                Assert.That(changes, Is.EqualTo(1));
                _player.SetActiveEffects(null);
                _player.Tick(default, 1f / 60f, 2);
                Assert.That(_player.ReadOnlyState.MaxHealth, Is.EqualTo(100f));
                Assert.That(changes, Is.EqualTo(2));
                Assert.That(_player.IsUngrabbable, Is.False);
            }
            finally { _player.Teardown(); Object.DestroyImmediate(effects); }
        }

        [Test]
        public void FloorHealthAndRegenerationPublishChangesWithoutRevivingTheDead()
        {
            int changes = 0;
            float publishedHealth = 0f, publishedMaximum = 0f;
            _player.OnHealthChanged += (id, health, maximum) =>
            {
                Assert.That(id, Is.EqualTo(_player.Id));
                changes++;
                publishedHealth = health;
                publishedMaximum = maximum;
            };
            _player.BeginFloorHealth(80f, 1f);
            Assert.That(publishedHealth, Is.EqualTo(80f));
            Assert.That(publishedMaximum, Is.EqualTo(80f));
            Assert.That(changes, Is.EqualTo(1));
            _player.ApplyHit(10f, _origin);
            _player.Tick(default, 4f, 240);
            Assert.That(changes, Is.EqualTo(2), "The exact delay boundary does not heal.");
            _player.Tick(default, 1f, 300);
            Assert.That(changes, Is.EqualTo(3));
            Assert.That(publishedHealth, Is.EqualTo(71.5f));
            Assert.That(publishedMaximum, Is.EqualTo(80f));
            _player.ApplyHit(100f, _origin);
            _player.Tick(default, 10f, 900);
            Assert.That(changes, Is.EqualTo(4));
            Assert.That(publishedHealth, Is.Zero);
            _player.SetHealthRecoveryEffects(0f, 0.5f);
            _player.BeginFloorHealth(60f, 1f);
            Assert.That(publishedHealth, Is.EqualTo(30f));
            Assert.That(publishedMaximum, Is.EqualTo(60f));
            _player.Tick(default, 10f, 1500);
            Assert.That(changes, Is.EqualTo(5), "Disabled regeneration emits no spurious health changes.");
        }

        private GameObject Make(string name, Vector3 offset)
        {
            var value = new GameObject("[Test] " + name);
            value.transform.position = _origin + offset;
            _objects.Add(value);
            return value;
        }

        private void Box(string name, Vector3 offset, Vector3 size, int layer)
        {
            GameObject value = Make(name, offset);
            value.layer = layer;
            value.AddComponent<BoxCollider>().size = size;
        }
    }
}
