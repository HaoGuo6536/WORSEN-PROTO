// ============================================================================
// RunShrineWiringTests.cs
// ============================================================================
// PURPOSE:
//   Drives shrine sampling through the real Run and Progression boundaries.
//   Player movement, shielding and tick delivery are explicit rather than fabricated events.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Run.
// KEY RESPONSIBILITIES:
//   - Check activation fraction, single use, delayed noise, subscriptions and shield-only hits.
// DEPENDENCIES:
//   - Run/Progression, Domain Player/Shrine/Director, Core, NUnit and reflection.
// USAGE NOTES:
//   Isolated Edit Mode actors; no scene loads, procedural bake or persistent initialization.
//   Reset Player registrations and clear the owned canonical reference explicitly on teardown.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Shrine;
using Worsen.Domain.Director;
using Worsen.Session.Run;
using Worsen.Session.Progression;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Run
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RunShrineWiringTests
    {
        private readonly List<Object> owned = new List<Object>();
        private RunSessionManager run;
        private ProgressionSessionManager progression;
        private PlayerManager player;
        private ShrineManager shrine;
        private ShrineConfig config;
        private int generation;
        [SetUp]
        public void SetUp()
        {
            ResetRegistry();
            Assert.That(PlayerRegistry.Items, Is.Empty);
            Assert.That(ProgressionSessionManager.Instance, Is.Null);
            player = Component<PlayerManager>();
            var mover = Config<PlayerMoverDriverConfig>(); Set(mover, "_hunterBodyLayer", "Ignore Raycast");
            Set(player.GetComponent<PlayerDriver>(), "_config", mover);
            player.Initialize(Config<PlayerProfile>(), new EntityContext(new EntityId(701), new System.Random(3)));
            player.gameObject.SetActive(true);
            typeof(PlayerRegistry).GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { player });
            ((PlayerBehaviorState)player.ReadOnlyState).Velocity = Vector3.forward;
            run = Component<RunSessionManager>();
            var state = new RunSessionBehaviorState(3); var clock = new RunSessionController(state, new System.Random(3));
            clock.StartScene(SceneKey.HorrorRun); Set(run, "state", state); Set(run, "controller", clock);
            run.gameObject.SetActive(true); run.BindGameplay(null, null, null);
            progression = Component<ProgressionSessionManager>();
            var progressionState = new ProgressionSessionBehaviorState();
            var progressionConfig = Config<ProgressionConfig>();
            var controller = new ProgressionSessionController(progressionState, progressionConfig, new System.Random(3));
            controller.StartRun(3);
            while (controller.Snapshot().Phase != ProgressionPhase.Exploring)
            {
                var s = controller.Snapshot();
                if (s.Phase == ProgressionPhase.ChooseThreat) controller.ChooseThreat(s.Choices[0].Id, s.Revision);
                else if (s.Phase == ProgressionPhase.ChooseCurse) controller.ChooseCurse(s.Choices[0].Id, s.Revision);
                else if (s.Phase == ProgressionPhase.Generating) controller.ConfirmFloorReady(s.GenerationId);
                else Assert.Fail("Unexpected setup phase.");
            }
            generation = controller.Snapshot().GenerationId;
            Set(progression, "state", progressionState); Set(progression, "controller", controller); Set(progression, "config", progressionConfig);
            typeof(ProgressionSessionManager).GetProperty("Instance").SetValue(null, progression);
            shrine = Component<ShrineManager>(); shrine.gameObject.SetActive(true); config = Config<ShrineConfig>();
            Set(config, "_countFloors", new[] { 1 });
        }
        [TearDown]
        public void TearDown()
        {
            if (run != null) run.DetachGameplay();
            if (player != null) player.Teardown();
            if (ReferenceEquals(ProgressionSessionManager.Instance, progression))
                typeof(ProgressionSessionManager).GetProperty("Instance").SetValue(null, null);
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            ResetRegistry();
        }
        private static void ResetRegistry() => typeof(PlayerRegistry).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        private void Bind(ShrineKind kind)
        {
            Set(config, "_availability", new[] { new ShrineAvailability(kind, 1, FearAxis.None) });
            shrine.Assemble(new[] { new ShrineSite(player.ReadOnlyState.Position, 1) }, 1, config,
                Config<ShrineDriverConfig>(), new System.Random(3));
            run.BindShrines(shrine, progression, generation, player.Id, () => .25f);
        }
        [Test]
        public void ActivationUsesFractionOnceAndRebindDisableDetachPairSubscriptions()
        {
            Bind(ShrineKind.Purgatory); Bind(ShrineKind.Purgatory);
            var facts = new List<ShrineResolvedFact>(); progression.ShrineResolved += facts.Add;
            Call(run, "TickShrines", default(InputFrame), .5f, 1L);
            Call(run, "TickShrines", default(InputFrame), .5f, 2L);
            Assert.That(facts.Count, Is.EqualTo(1)); Assert.That(facts[0].YieldMultiplier, Is.EqualTo(1.75f));
            Assert.That(facts[0].GenerationId, Is.EqualTo(generation));
            Assert.That(Subscribers(shrine, "Activated"), Is.EqualTo(1));
            run.gameObject.SetActive(false); Call(run, "OnDisable"); Assert.That(Subscribers(shrine, "Activated"), Is.Zero);
            run.gameObject.SetActive(true); Call(run, "OnEnable"); Assert.That(Subscribers(shrine, "Activated"), Is.EqualTo(1));
            run.DetachGameplay(); Assert.That(Subscribers(shrine, "Activated"), Is.Zero);
            Assert.That(Subscribers(progression, "ShrineNoiseEmitted"), Is.Zero);
        }
        [Test]
        public void PacificationNoiseUsesDeliveryTickAndQueuesDirectorExactlyOnce()
        {
            var director = Component<DirectorManager>();
            director.Initialize(Config<DirectorConfig>(), new System.Random(3),
                new Worsen.Domain.Chase.ChaseBehaviorState(), new Worsen.Domain.Floor.FloorBehaviorState());
            run.BindGameplay(null, null, director); Bind(ShrineKind.Pacification);
            var noises = new List<NoiseEvent>(); progression.ShrineNoiseEmitted += noises.Add;
            for (long tick = 1; tick <= 5; tick++) Call(run, "TickShrines", default(InputFrame), .5f, tick);
            Assert.That(noises.Count, Is.EqualTo(1)); Assert.That(noises[0].Tick, Is.EqualTo(4));
            Assert.That(((DirectorBehaviorState)Get(Get(director, "_controller"), "_state")).Noises.Count, Is.EqualTo(1));
        }
        [Test]
        public void ShieldOnlyHitPublishesZeroLossAndStartsGraceOnce()
        {
            player.GrantShield(40f);
            var hits = new List<HunterHit>(); var grace = new List<GraceWindowFact>(); var telemetry = new List<TelemetrySample>();
            run.HitAccepted += hits.Add; run.OnGraceStarted += grace.Add; run.TelemetryPublished += telemetry.Add;
            var hit = new HunterHit(new EntityId(-1), player.Id, 10, 0, Vector3.back);
            Call(run, "ApplyAcceptedHit", hit); Call(run, "ApplyAcceptedHit", hit);
            Assert.That(player.ReadOnlyState.Health, Is.EqualTo(100f)); Assert.That(player.ReadOnlyShieldState.Shield, Is.EqualTo(30f));
            Assert.That(hits.Count, Is.EqualTo(1)); Assert.That(hits[0].Damage, Is.Zero);
            Assert.That(grace.Count, Is.EqualTo(1)); Assert.That(telemetry.Count, Is.EqualTo(1));
            Assert.That(telemetry[0].Value, Is.Zero);
        }
        private T Component<T>() where T : Component
        { var root = new GameObject(typeof(T).Name); owned.Add(root); root.SetActive(false); return root.AddComponent<T>(); }
        private T Config<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static int Subscribers(object target, string field) => (Get(target, field) as Delegate)?.GetInvocationList().Length ?? 0;
    }
}
