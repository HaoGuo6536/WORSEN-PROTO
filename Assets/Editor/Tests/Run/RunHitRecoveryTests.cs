// ============================================================================
// RunHitRecoveryTests.cs
// ============================================================================
// PURPOSE:
//   Exercises Session hit routing through a real Player manager and its recovery
//   controller. Explicit clocks distinguish accepted damage from grace absorption
//   without requiring a scene, a Hunter attack, or a persistent Play Mode service.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Session · Run/HorrorEffects integration.
// KEY RESPONSIBILITIES:
//   - Verify severity/source forwarding, end-exclusive timing and accepted-hit guards.
//   - Verify grace relays bind once and detach on disable, rebind and destruction.
//   - Verify light/hand routing through the Floor event subscription outside Player ticks.
// DEPENDENCIES:
//   Core facts, Domain Player/Floor, Session Run/HorrorEffects, NUnit and UnityEngine.
// USAGE NOTES:
//   Edit Mode boundary fixtures. Reflection installs real Session controllers to
//   avoid DontDestroyOnLoad, registers only the owned Player, and injects queued
//   hit/Floor facts. No fake Player health or grace events are raised. Temporary
//   fixed delta time is restored; Ignore Raycast substitutes for HunterBody.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Player;
using Worsen.Session.HorrorEffects;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Run
{
    public sealed class RunHitRecoveryTests
    {
        private const float Dt = 1f / 60f;
        private readonly List<Object> owned = new List<Object>();
        private readonly List<GraceWindowFact> starts = new List<GraceWindowFact>();
        private readonly List<GraceWindowFact> ends = new List<GraceWindowFact>();
        private readonly List<HunterHit> accepted = new List<HunterHit>();
        private readonly List<TelemetrySample> telemetry = new List<TelemetrySample>();
        private RunSessionManager run;
        private RunSessionController clock;
        private PlayerManager player;
        private HorrorEffectsManager effects;
        private FloorManager floor;
        private float previousFixedDelta;

        [SetUp]
        public void SetUp()
        {
            previousFixedDelta = Time.fixedDeltaTime;
            Assert.That(PlayerRegistry.Items, Is.Empty, "Requires isolated Edit Mode actors.");
            Time.fixedDeltaTime = Dt;
            var profile = Config<PlayerProfile>();
            var mover = Config<PlayerMoverDriverConfig>();
            Set(mover, "_hunterBodyLayer", "Ignore Raycast");
            player = Component<PlayerManager>();
            Set(player.GetComponent<PlayerDriver>(), "_config", mover);
            player.Initialize(profile, new EntityContext(new EntityId(1), new System.Random(13)));
            Registry("Register");
            player.gameObject.SetActive(true);
            run = Component<RunSessionManager>();
            var state = new RunSessionBehaviorState(13);
            clock = new RunSessionController(state, new System.Random(13));
            clock.StartScene(SceneKey.HorrorRun);
            Set(run, "state", state); Set(run, "controller", clock);
            run.gameObject.SetActive(true);
            run.BindGameplay(null, null, null);
            run.OnGraceStarted += starts.Add; run.OnGraceEnded += ends.Add;
            run.HitAccepted += accepted.Add; run.TelemetryPublished += telemetry.Add;
            floor = Component<FloorManager>();
            effects = Component<HorrorEffectsManager>();
            Set(effects, "controller", new HorrorEffectsController(new HorrorEffectsBehaviorState(), Config<HorrorEffectsConfig>()));
            effects.gameObject.SetActive(true);
            effects.BeginFloor(1, default);
            effects.ConfigureHazards(null, floor);
        }

        [TearDown]
        public void TearDown()
        {
            if (run != null) run.DetachGameplay();
            if (effects != null) effects.ClearHazards();
            if (player != null) Registry("Unregister");
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear(); starts.Clear(); ends.Clear(); accepted.Clear(); telemetry.Clear();
            Time.fixedDeltaTime = previousFixedDelta;
        }

        [TestCase(HitSeverity.Light, HitSource.Scream)]
        [TestCase(HitSeverity.Heavy, HitSource.Projectile)]
        public void QueuedHitsForwardMetadataAndOnlyAcceptedDamageIsPublished(HitSeverity severity, HitSource source)
        {
            int absorbed = 0;
            player.OnHitAbsorbedByGrace += (id, actualSeverity, actualSource) =>
            {
                Assert.That(id, Is.EqualTo(player.Id));
                Assert.That(actualSeverity, Is.EqualTo(severity));
                Assert.That(actualSource, Is.EqualTo(source));
                absorbed++;
            };
            HunterHit hit = Hit(0, severity, source);
            Route(hit); Route(hit);
            Assert.That(player.ReadOnlyState.Health, Is.EqualTo(90f));
            Assert.That(starts.Count, Is.EqualTo(1));
            Assert.That(starts[0].PlayerId, Is.EqualTo(player.Id));
            Assert.That(starts[0].Severity, Is.EqualTo(severity));
            Assert.That(starts[0].StartTick, Is.Zero);
            Assert.That(starts[0].EndTick, Is.EqualTo(72));
            Assert.That(absorbed, Is.EqualTo(1));
            Assert.That(accepted, Is.EqualTo(new[] { hit }));
            Assert.That(telemetry.Count, Is.EqualTo(1));
            Assert.That(telemetry[0].Kind, Is.EqualTo(TelemetrySampleKind.AcceptedHit));
        }

        [Test]
        public void ProcessingClockExpiresGraceBeforePlayerTickAndIgnoresHitTimestamp()
        {
            Route(Hit(0));
            AdvanceRun(71);
            Route(Hit(999));
            Assert.That(player.ReadOnlyState.Tick, Is.EqualTo(71));
            Assert.That(accepted.Count, Is.EqualTo(1), "A future fact timestamp cannot expire grace.");
            Assert.That(ends, Is.Empty);
            AdvanceRun(72);
            Route(Hit(0));
            Assert.That(player.ReadOnlyState.Health, Is.EqualTo(80f));
            Assert.That(ends, Is.EqualTo(new[] { starts[0] }));
            Assert.That(starts[1].StartTick, Is.EqualTo(72), "Use processing time, not the queued historical tick.");
            Assert.That(starts[1].EndTick, Is.EqualTo(144));
            Assert.That(accepted.Count, Is.EqualTo(2));
            Assert.That(telemetry.Count, Is.EqualTo(2));
        }

        [Test]
        public void OlderRunClockDoesNotRewindPlayerRecovery()
        {
            player.AdvanceRecovery(100);
            Route(Hit(0));
            Assert.That(player.ReadOnlyState.Tick, Is.EqualTo(100));
            Assert.That(starts[0].StartTick, Is.EqualTo(100));
            Assert.That(starts[0].EndTick, Is.EqualTo(172));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidDamageDoesNotPublishAcceptance(int damage)
        {
            Route(new HunterHit(new EntityId(-1), player.Id, damage, 0, Vector3.zero));
            Assert.That(player.ReadOnlyState.Health, Is.EqualTo(100f));
            Assert.That(starts, Is.Empty);
            Assert.That(accepted, Is.Empty);
            Assert.That(telemetry, Is.Empty);
        }

        [Test]
        public void GraceRelaysPairAcrossRebindDisableDetachAndDestroy()
        {
            run.BindGameplay(null, null, null);
            AssertSubscribers(1);
            Route(Hit(0));
            player.AdvanceRecovery(72);
            Assert.That(starts.Count, Is.EqualTo(1));
            Assert.That(ends, Is.EqualTo(starts));
            run.gameObject.SetActive(false);
            AssertSubscribers(0);
            player.ApplyHit(1f, Vector3.zero);
            player.AdvanceRecovery(144);
            Assert.That(starts.Count, Is.EqualTo(1));
            Assert.That(ends.Count, Is.EqualTo(1));
            run.gameObject.SetActive(true);
            AssertSubscribers(1);
            player.ApplyHit(1f, Vector3.zero);
            player.AdvanceRecovery(216);
            Assert.That(starts.Count, Is.EqualTo(2));
            Assert.That(ends, Is.EqualTo(starts));
            run.DetachGameplay();
            AssertSubscribers(0);
            run.BindGameplay(null, null, null);
            AssertSubscribers(1);
            Object.DestroyImmediate(run.gameObject);
            AssertSubscribers(0);
        }

        [TestCase(71, false)]
        [TestCase(72, true)]
        public void FloorLethalRoutingUsesProcessingRecoveryClock(int tick, bool lethal)
        {
            Route(Hit(0));
            AdvanceRun(tick);
            Invoke(run, "HandleLethal", new FloorLethalContactFact(player.Id, 1, tick));
            Assert.That(player.ReadOnlyState.IsAlive, Is.EqualTo(!lethal));
            Assert.That(player.ReadOnlyState.Tick, Is.EqualTo(tick));
        }

        [Test]
        public void HandEventsForwardLightHandAndExpireGraceOutsidePlayerTicks()
        {
            int absorbed = 0;
            player.OnHitAbsorbedByGrace += (id, severity, source) =>
            {
                Assert.That(id, Is.EqualTo(player.Id));
                Assert.That(severity, Is.EqualTo(HitSeverity.Light));
                Assert.That(source, Is.EqualTo(HitSource.Hand));
                absorbed++;
            };
            Hand(10); Hand(81);
            Assert.That(player.ReadOnlyState.Health, Is.EqualTo(90f));
            Assert.That(absorbed, Is.EqualTo(1));
            Assert.That(starts[0].Severity, Is.EqualTo(HitSeverity.Light));
            Assert.That(starts[0].StartTick, Is.EqualTo(10));
            Assert.That(starts[0].EndTick, Is.EqualTo(82));
            Hand(82);
            Assert.That(player.ReadOnlyState.Health, Is.EqualTo(80f));
            Assert.That(player.ReadOnlyState.Tick, Is.EqualTo(82));
            Assert.That(ends, Is.EqualTo(new[] { starts[0] }));
            Assert.That(starts[1].StartTick, Is.EqualTo(82));
            Assert.That(accepted, Is.Empty, "Hand facts are not Hunter catch telemetry.");
        }

        [Test]
        public void OlderHandFactDoesNotRewindPlayerRecovery()
        {
            player.AdvanceRecovery(100);
            Hand(10);
            Assert.That(player.ReadOnlyState.Tick, Is.EqualTo(100));
            Assert.That(starts[0].StartTick, Is.EqualTo(100));
            Assert.That(starts[0].EndTick, Is.EqualTo(172));
        }

        private HunterHit Hit(long tick, HitSeverity severity = HitSeverity.Heavy, HitSource source = HitSource.Lunge)
            => new HunterHit(new EntityId(-1), player.Id, 10, tick, Vector3.back, severity: severity, source: source);
        private void Route(HunterHit hit) { Invoke(run, "QueueHit", hit); Invoke(run, "DrainPendingHits"); }
        private void Hand(long tick) => ((Action<CollapseHandFact>)Field(floor, "OnCollapseHand").GetValue(floor))
            .Invoke(new CollapseHandFact(player.Id, 1, CollapseHandEventKind.Hit, Vector3.back, 1f, 10f, tick));
        private void AdvanceRun(long tick)
        { while (run.Tick < tick) Assert.That(clock.TryTick(Dt, out _), Is.True); }
        private void AssertSubscribers(int count)
        {
            foreach (string name in new[] { "OnGraceStarted", "OnGraceEnded" })
                Assert.That((Field(player, name).GetValue(player) as Delegate)?.GetInvocationList().Length ?? 0, Is.EqualTo(count), name);
        }
        private void Registry(string method) => typeof(PlayerRegistry).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { player });
        private T Component<T>() where T : Component
        {
            var owner = new GameObject(typeof(T).Name + " recovery test");
            owned.Add(owner); owner.SetActive(false); return owner.AddComponent<T>();
        }
        private T Config<T>() where T : ScriptableObject
        { T value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static void Invoke(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
