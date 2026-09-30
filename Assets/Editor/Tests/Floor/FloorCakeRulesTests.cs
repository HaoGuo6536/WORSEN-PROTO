// ============================================================================
// FloorCakeRulesTests.cs
// ============================================================================
// PURPOSE:
//   Exercises trap selection, guidance and optional cake rules without scene objects.
//   Explicit seeds and clocks cover the new rules and keep defaults compatible.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Verify optional-only traps, counts, one-shot contacts, tell timing and hooks.
//   - Verify Greedy Door quotas, collapse safety and typed guidance identities.
//   - Require the published guidance snapshot to retain white-first ordering and clear stale targets.
// DEPENDENCIES:
//   NUnit, Core, Floor and a read-only Player fixture; managed config field setup.
// USAGE NOTES:
//   No engine calls. Uninitialized configs are populated through reflection.
//   These helpers also supply immutable graph/player inputs to engine integration tests.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Floor
{
    public sealed class FloorCakeRulesTests
    {
        [Test]
        public void HooksDefaultOffAndNoOptionalSpawnCanEverBecomeATrap()
        {
            var hooks = default(FloorCakeHooks);
            Assert.That(hooks.SweetTooth || hooks.BlindFaith || hooks.GoldenSense || hooks.MoreTraps || hooks.SilentTraps || hooks.GreedyDoor, Is.False);
            var config = Config(); Set(config, "_requiredCakeCount", 18);
            Assert.That(Start(config: config, hooks: new FloorCakeHooks(moreTraps: true)).State.Traps, Is.Empty);
        }

        [Test]
        public void TrapsCannotTurnIntoGoldenCakesOrCakeLossesAndClosedTrapsCannotSpring()
        {
            var f = Start(); CollectRequired(f); f.Controller.Tick(10000f, 1);
            var losses = f.Controller.DrainCakeLosses();
            foreach (var trap in f.State.Traps)
            {
                Assert.That(losses.Any(l => l.AnchorId == trap.Anchor.Id), Is.False);
                Assert.That(f.Controller.Collect(new EntityId(1), trap.Anchor.Id, PickupKind.GoldenCake, 2, out _), Is.False);
                if (trap.Anchor.RoomId != f.Graph.ExitRoomId)
                    Assert.That(f.Controller.SpringTrap(new EntityId(1), trap.Anchor.Id, 2, out _, out _), Is.False);
            }
        }

        [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 2)]
        public void TrapsRespectRoundAndOptionalShare(int round, int expected)
        {
            var f = Start(round: round);
            Assert.That(f.State.Traps.Count, Is.EqualTo(expected));
            Assert.That(f.State.Traps.All(t => f.State.ActiveCakeAnchors.All(a => a.Id != t.Anchor.Id)), Is.True);
            Assert.That(f.State.RequiredCakeCount, Is.EqualTo(6));
            int accepted = f.Graph.Anchors.Count(a => f.Controller.Collect(new EntityId(1), a.Id, PickupKind.Cake, 1, out _));
            Assert.That(accepted, Is.EqualTo(18 - expected));
            Assert.That(f.State.CakeCount, Is.EqualTo(6));
            Assert.That(f.State.GoldenCakeCount, Is.Zero);
        }

        [Test]
        public void SelectionIsSeededBoundedAndDoesNotPerturbRequiredSubset()
        {
            var variants = new HashSet<string>();
            for (int seed = 0; seed < 20; seed++)
            {
                var a = Start(seed: seed); var b = Start(seed: seed); var earlier = Start(seed: seed, round: 2);
                Assert.That(a.State.Traps, Is.EqualTo(b.State.Traps));
                Assert.That(a.State.ActiveCakeAnchors, Is.EqualTo(earlier.State.ActiveCakeAnchors));
                Assert.That(a.State.Traps.Count, Is.LessThanOrEqualTo(3));
                variants.Add(string.Join(",", a.State.Traps.Select(t => t.Anchor.Id + ":" + t.Kind)));
            }
            Assert.That(variants.Count, Is.GreaterThan(1));
            var capped = Config(); Set(capped, "_roomsPerTrap", 1); Set(capped, "_optionalTrapShare", 1f);
            Assert.That(Start(config: capped).State.Traps.Count, Is.EqualTo(3));
        }

        [Test]
        public void TrapContactsRejectForeignPlayersSpringOnceAndNeverScoreOrGuide()
        {
            var kinds = new HashSet<FloorTrapKind>();
            for (int seed = 0; seed < 20; seed++)
            {
                var f = Start(seed: seed);
                foreach (var trap in f.State.Traps)
                {
                    kinds.Add(trap.Kind);
                    Assert.That(f.Controller.SpringTrap(new EntityId(99), trap.Anchor.Id, 8, out _, out _), Is.False);
                    Assert.That(f.Controller.SelectCue(new[] { new FloorPathCandidate(trap.Anchor.Id, 1f, Vector3.right) }).HasCue, Is.False);
                    Assert.That(f.Controller.TryWhiteGuidance(false, out _), Is.False);
                    Assert.That(f.Controller.Collect(new EntityId(1), trap.Anchor.Id, PickupKind.Cake, 8, out _), Is.False);
                    Assert.That(f.Controller.SpringTrap(new EntityId(1), trap.Anchor.Id, 9, out var fact, out var noise), Is.True);
                    Assert.That(fact.TrapId, Is.EqualTo(trap.Anchor.Id)); Assert.That(fact.Kind, Is.EqualTo(trap.Kind));
                    Assert.That(fact.RoomId, Is.EqualTo(trap.Anchor.RoomId)); Assert.That(fact.Position, Is.EqualTo(trap.Anchor.Position));
                    Assert.That(fact.Tick, Is.EqualTo(9)); Assert.That(fact.PlayerId, Is.EqualTo(new EntityId(1)));
                    if (trap.Kind == FloorTrapKind.Announce)
                    {
                        Assert.That(noise.SourceKind, Is.EqualTo(NoiseSourceKind.Trap)); Assert.That(noise.Tick, Is.EqualTo(9));
                        Assert.That(noise.Position, Is.EqualTo(trap.Anchor.Position)); Assert.That(noise.Loudness, Is.EqualTo(1f));
                    }
                    else Assert.That(noise.Loudness, Is.Zero);
                    Assert.That(f.Controller.SpringTrap(new EntityId(1), trap.Anchor.Id, 10, out _, out _), Is.False);
                }
                Assert.That(f.State.CakeCount, Is.Zero); Assert.That(f.State.GoldenCakeCount, Is.Zero);
            }
            Assert.That(kinds.Count, Is.EqualTo(3));
        }

        [Test]
        public void SweetToothRestoresExactlyOneOptionalCakeAndMoreTrapsAddsOnlyBlinders()
        {
            var normal = Start(); var sweet = Start(hooks: new FloorCakeHooks(sweetTooth: true));
            var extra = Start(hooks: new FloorCakeHooks(moreTraps: true));
            Assert.That(sweet.State.Traps.Count, Is.EqualTo(normal.State.Traps.Count - 1));
            int restored = normal.State.Traps[0].Anchor.Id;
            Assert.That(sweet.Controller.Collect(new EntityId(1), restored, PickupKind.Cake, 1, out _), Is.True);
            Assert.That(sweet.State.CakeCount, Is.Zero);
            Assert.That(extra.State.Traps.Take(normal.State.Traps.Count), Is.EqualTo(normal.State.Traps));
            Assert.That(extra.State.Traps.Skip(normal.State.Traps.Count).Select(t => t.Kind), Is.EqualTo(new[] { FloorTrapKind.Blind, FloorTrapKind.Blind }));
            Assert.That(Start(round: 2, hooks: new FloorCakeHooks(moreTraps: true)).State.Traps, Is.Empty);
        }

        [Test]
        public void BlinderTellUsesInjectedCadenceStopsAfterContactAndSilentHookSuppressesIt()
        {
            var f = Start(hooks: new FloorCakeHooks(moreTraps: true));
            Assert.That(f.Controller.TickTraps(1.99f), Is.Empty);
            var ticks = f.Controller.TickTraps(0.02f);
            Assert.That(ticks.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(ticks.All(t => t.Kind == FloorTrapKind.Blind), Is.True);
            foreach (var t in ticks) f.Controller.SpringTrap(new EntityId(1), t.Anchor.Id, 1, out _, out _);
            Assert.That(f.Controller.TickTraps(2f), Is.Empty);
            var silent = Start(hooks: new FloorCakeHooks(moreTraps: true, silentTraps: true));
            Assert.That(silent.State.Traps.Count, Is.GreaterThan(0)); Assert.That(silent.Controller.TickTraps(10f), Is.Empty);
        }

        [Test]
        public void BlindFaithDoublesOptionalCakeCreditAndSuppressesBothArrows()
        {
            var f = Start(round: 1, hooks: new FloorCakeHooks(blindFaith: true, goldenSense: true));
            var optional = f.Graph.Anchors.Where(a => f.State.ActiveCakeAnchors.All(r => r.Id != a.Id)).Take(3).ToArray();
            foreach (var a in optional) Assert.That(f.Controller.Collect(new EntityId(1), a.Id, PickupKind.Cake, 1, out _), Is.True);
            Assert.That(f.State.CakeCount, Is.EqualTo(6)); Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Open));
            Assert.That(f.Controller.SelectCue(new[] { new FloorPathCandidate(0, 1f, Vector3.right) }).HasCue, Is.False);
            Assert.That(f.Controller.TryWhiteGuidance(false, out _), Is.False);
            Assert.That(f.Controller.TryGoldenTarget(Vector3.zero, out _), Is.False);
        }

        [Test]
        public void DefaultsKeepWhiteArrowAndGoldenSenseIsSecondTargetOnlyDuringCollapse()
        {
            var f = Start(); var first = f.State.ActiveCakeAnchors[0];
            f.Controller.SelectCue(new[] { new FloorPathCandidate(first.Id, 1f, Vector3.right) });
            Assert.That(f.Controller.TryWhiteGuidance(true, out var white), Is.True);
            Assert.That(white.Kind, Is.EqualTo(GuidanceKind.WhiteArrow)); Assert.That(white.AnchorId, Is.EqualTo(first.Id));
            Assert.That(white.TargetPosition, Is.EqualTo(first.Position)); Assert.That(white.IsFallback, Is.True);
            Assert.That(f.Controller.TryGoldenTarget(Vector3.zero, out _), Is.False);
            CollectRequired(f); Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Open));
            Assert.That(f.Controller.TryGoldenTarget(Vector3.zero, out _), Is.False);
            var sense = Start(hooks: new FloorCakeHooks(goldenSense: true));
            var required = sense.State.ActiveCakeAnchors.OrderBy(a => a.Position.sqrMagnitude).ThenBy(a => a.Id).ToArray();
            Assert.That(sense.Controller.TryGoldenTarget(Vector3.zero, out _), Is.False);
            CollectRequired(sense);
            Assert.That(sense.Controller.TryGoldenTarget(Vector3.zero, out var nearest), Is.True);
            Assert.That(nearest.Id, Is.EqualTo(required[0].Id));
            sense.Controller.Collect(new EntityId(1), nearest.Id, PickupKind.GoldenCake, 2, out _);
            sense.Controller.TryGoldenTarget(Vector3.zero, out nearest); Assert.That(nearest.Id, Is.EqualTo(required[1].Id));
            sense.Controller.SelectCue(new[] { new FloorPathCandidate(0, 2f, Vector3.back), new FloorPathCandidate(nearest.Id, 1f, Vector3.right) });
            sense.Controller.TryWhiteGuidance(false, out white); Assert.That(white.AnchorId, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void GuidanceSnapshotKeepsWhiteBeforeGoldWhileGreedyDoorIsLocked(bool fallback)
        {
            var f = Start(hooks: new FloorCakeHooks(greedyDoor: true, goldenSense: true));
            CollectRequired(f);
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            f.Controller.SelectCue(new[] { new FloorPathCandidate(0, 2f, Vector3.back) });
            Assert.That(f.Controller.TryGoldenTarget(Vector3.zero, out var nearest), Is.True);
            var golden = new GuidanceTarget(GuidanceKind.GoldenSense, Vector3.right, nearest.Position, nearest.Id, isFallback: fallback);
            var targets = f.Controller.GuidanceTargets(fallback, golden);
            Assert.That(targets.Select(target => target.Kind), Is.EqualTo(new[] { GuidanceKind.WhiteArrow, GuidanceKind.GoldenSense }));
            Assert.That(targets[0].AnchorId, Is.Zero);
            Assert.That(targets[0].TargetPosition, Is.EqualTo(f.Graph.ExitPosition));
            Assert.That(targets[0].IsFallback, Is.EqualTo(fallback));
            Assert.That(targets[1], Is.EqualTo(golden));
            Assert.That(f.Controller.GuidanceTargets(false).Select(target => target.Kind), Is.EqualTo(new[] { GuidanceKind.WhiteArrow }));
            f.Controller.SelectCue(null);
            Assert.That(f.Controller.GuidanceTargets(false), Is.Empty);
            Assert.That(targets.Count, Is.EqualTo(2), "Later publications cannot mutate an earlier snapshot.");
        }

        [Test]
        public void GreedyDoorOpensAtFortyPercentAndAllowsGoldWhileLocked()
        {
            var config = Config(); Set(config, "_requiredCakeCount", 5);
            var f = Start(config: config, hooks: new FloorCakeHooks(greedyDoor: true));
            var required = f.State.ActiveCakeAnchors.ToArray(); CollectRequired(f);
            Assert.That(f.State.CollapseStarted, Is.True); Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            Assert.That(f.Controller.Collect(new EntityId(1), required[0].Id, PickupKind.GoldenCake, 2, out _), Is.True);
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            Assert.That(f.Controller.Collect(new EntityId(1), required[1].Id, PickupKind.GoldenCake, 3, out _), Is.True);
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Open));
        }

        [TestCase(false)] [TestCase(true)]
        public void CompletedCollapseAlwaysOpensGreedyDoorAndNeverClosesExitRoom(bool shuffled)
        {
            var f = Start(hooks: new FloorCakeHooks(greedyDoor: true), shuffled: shuffled); CollectRequired(f);
            Assert.That(f.Controller.Tick(0f, 1).All(x => x.RoomId != 6), Is.True);
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            var facts = f.Controller.Tick(10000f, 2);
            Assert.That(facts.Any(x => x.RoomId == 6), Is.False); Assert.That(f.State.RoomPhases[6], Is.EqualTo(RoomPhase.Open));
            Assert.That(f.State.GoldenCakeCount, Is.Zero); Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Open));
            Assert.That(f.Controller.ContactExit(new EntityId(1), 3, out _), Is.True);
        }

        [Test]
        public void GreedyQuotaShrinksWithLostGoldAndResetClearsHooksAndTraps()
        {
            var f = Start(hooks: new FloorCakeHooks(greedyDoor: true, moreTraps: true, silentTraps: true));
            var required = f.State.ActiveCakeAnchors.OrderByDescending(a => a.RoomId).ToArray(); CollectRequired(f);
            f.Controller.Collect(new EntityId(1), required[0].Id, PickupKind.GoldenCake, 1, out _);
            Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Locked));
            for (int tick = 1; tick < 5; tick++)
            {
                f.Controller.Tick(14f, tick);
                int remaining = required.Count(a => a.Id != required[0].Id && f.State.RoomPhases[a.RoomId] != RoomPhase.Closed);
                Assert.That(f.State.ExitState, Is.EqualTo(remaining <= 1 ? ExitState.Open : ExitState.Locked));
            }
            f.Controller.Initialize(f.Graph, new[] { new Player() });
            Assert.That(f.State.Traps, Is.Empty); Assert.That(f.State.CollapseStarted, Is.False);
            CollectRequired(f); Assert.That(f.State.ExitState, Is.EqualTo(ExitState.Open));
        }

        internal static Fixture Start(int seed = 7, int round = 3, FloorCakeHooks hooks = default, FloorConfig config = null, bool shuffled = false)
        {
            var f = new Fixture { Graph = Graph(), State = new FloorBehaviorState() };
            f.Controller = new FloorController(f.State, config ?? Config(), new System.Random(seed));
            f.Controller.Initialize(f.Graph, new[] { new Player() }, round: round, cakeHooks: hooks, shuffledCollapse: shuffled);
            return f;
        }
        internal static void CollectRequired(Fixture f)
        { foreach (var a in f.State.ActiveCakeAnchors.ToArray()) Assert.That(f.Controller.Collect(new EntityId(1), a.Id, PickupKind.Cake, 1, out _), Is.True); }
        internal static FloorConfig Config()
        {
            var c = (FloorConfig)FormatterServices.GetUninitializedObject(typeof(FloorConfig));
            Set(c, "_requiredCakeCount", 6); Set(c, "_flowWeight", 1f); Set(c, "_collapseInterval", 12f);
            Set(c, "_telegraphDuration", 6f); Set(c, "_directionCueInterval", 0.5f); Set(c, "_earlyBailHoldDuration", 1f);
            Set(c, "_trapStartRound", 3); Set(c, "_optionalTrapShare", 0.33333334f); Set(c, "_roomsPerTrap", 3);
            Set(c, "_maximumTraps", 3); Set(c, "_extraBlinderTraps", 2); Set(c, "_trapTickInterval", 2f);
            Set(c, "_trapAnnounceLoudness", 1f); Set(c, "_greedyDoorShare", 0.4f); return c;
        }
        internal static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        internal static LevelGraph Graph() => LevelGraphUtility.Build(
            Enumerable.Range(1, 6).Select(i => new LevelRoom(i, new Vector3(i * 10f, 2f, 0f), new Vector3(8f, 4f, 8f))).ToArray(),
            Enumerable.Range(1, 5).Select(i => new LevelEdge(i, i, i + 1, true)).ToArray(),
            Enumerable.Range(1, 6).SelectMany(i => Enumerable.Range(1, 3).Select(j => new LevelAnchor(i * 10 + j, i, CakeAnchorType.Flow, new Vector3(i * 10f, 0f, j)))).ToArray(),
            6, new Vector3(60f, 0f, 0f));
        internal sealed class Fixture { public LevelGraph Graph; public FloorBehaviorState State; public FloorController Controller; }
        internal sealed class Player : IReadOnlyPlayerState
        {
            public EntityId Id => new EntityId(1); public Vector3 Position => new Vector3(60f, 0f, 0f);
            public Vector3 Velocity => Vector3.zero; public Vector3 Forward => Vector3.forward;
            public float HeadingDegrees => 0f; public float SprintSpeed => 8f; public float MaxDesignSpeed => 12f;
            public float Health => 100f; public float MaxHealth => 100f; public bool IsAlive => true; public bool LookBack => false;
            public MovementState MovementState => MovementState.Ground; public long Tick => 0;
            public IReadOnlyList<NoiseEvent> RecentNoises => Array.Empty<NoiseEvent>(); public InventorySnapshot Inventory => default;
        }
    }
}
