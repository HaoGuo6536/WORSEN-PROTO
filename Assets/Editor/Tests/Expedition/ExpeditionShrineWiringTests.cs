// ============================================================================
// ExpeditionShrineWiringTests.cs
// ============================================================================
// PURPOSE:
//   Freezes shrine floor ownership, shield transfer and reversible Wick state.
//   Small injected floors exercise world routing without procedural generation or baking.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Expedition.
// KEY RESPONSIBILITIES:
//   - Test physical gold fractions, duplicate resolutions, shield reset and lamp restoration.
//   - Check producer-facing pocket resolution and deterministic per-floor shrine placement.
// DEPENDENCIES:
//   - Expedition, Shrine, Level/Procedural, Core, NUnit and test-only reflection.
// USAGE NOTES:
//   Coordinator runs Edit Mode. No scene files, native navigation bake or asset writes.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Domain.Procedural;
using Worsen.Domain.Shrine;
using Worsen.Session.Expedition;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Expedition
{
    public sealed class ExpeditionShrineWiringTests
    {
        private static ExpeditionSessionController Ready(out ExpeditionSessionBehaviorState state)
        {
            state = new ExpeditionSessionBehaviorState(); var c = new ExpeditionSessionController(state);
            c.Bind(SceneKey.HorrorRun); c.Queue(new ProgressionGenerationRequest(1, 17, 8, false,
                new ProgressionEffects(1, 1, 1, 1, 100, 100, 0)));
            c.Begin(1); c.RecordPlayer(new EntityId(1)); c.Ready(); return c;
        }
        [Test]
        public void ShieldSurvivesReplacementAndShelterButNotDeathOrNewRun()
        {
            var c = Ready(out _); c.AdmitShieldTransfer(); c.CaptureShield(true, 37f);
            c.ReleaseActors(); Assert.That(c.CarriedShield, Is.EqualTo(37f));
            c.CaptureShield(true, c.CarriedShield); Assert.That(c.CarriedShield, Is.EqualTo(37f));
            c.CaptureShield(false, 37f); Assert.That(c.CarriedShield, Is.Zero);
            c.CaptureShield(true, 22f); c.ResetRun(); c.CaptureShield(true, 22f);
            Assert.That(c.CarriedShield, Is.Zero, "Old living actor must not refill the transfer after StartRun.");
        }
        [TestCase(false)] [TestCase(true)]
        public void FractionCountsPhysicalGoldNotBlindFaithExitCredit(bool blind)
        {
            var c = Ready(out _);
            c.BeginCollection(new[] { new LevelAnchor(1, 1, default, Vector3.zero), new LevelAnchor(2, 1, default, Vector3.one) }, blind);
            c.ObservePickup(new PickupCollectedFact(new EntityId(1), 1, PickupKind.Cake, 1, 0, 1), blind);
            if (!blind) c.ObservePickup(new PickupCollectedFact(new EntityId(1), 2, PickupKind.Cake, 2, 0, 2), true);
            Assert.That(c.CollectedFraction, Is.Zero);
            var gold = new PickupCollectedFact(new EntityId(1), 1, PickupKind.GoldenCake, 2, 1, 3);
            c.ObservePickup(gold, true); c.ObservePickup(gold, true);
            c.ObservePickup(new PickupCollectedFact(new EntityId(1), 99, PickupKind.Cake, 3, 1, 4), true);
            Assert.That(c.CollectedFraction, Is.EqualTo(blind ? 1f : .5f));
        }
        [Test]
        public void WickOverlapsPreserveOriginalStatesAndSkipActivationTick()
        {
            var c = Ready(out _);
            c.BeginWick(2f, 4, new[] { Lamp(11, false), Lamp(12, true) });
            Assert.That(c.TickWick(1f, 4), Is.False);
            c.BeginWick(3f, 4, new[] { Lamp(11, true), Lamp(12, true) });
            Assert.That(c.TickWick(2f, 5), Is.False); Assert.That(c.TickWick(2f, 5), Is.False);
            Assert.That(c.TickWick(1f, 6), Is.True);
            var previous = c.EndWick(); Assert.That(previous[11], Is.False); Assert.That(previous[12], Is.True);
            Assert.That(c.EndWick(), Is.Empty);
        }
        [Test]
        public void WickManagerLightsEveryLampThenRestoresOnExpiryAndRelease()
        {
            var root = new GameObject("expedition fixture"); root.SetActive(false);
            var levelRoot = new GameObject("level fixture");
            var c = Ready(out var state); var manager = root.AddComponent<ExpeditionSessionManager>();
            var level = levelRoot.AddComponent<LevelManager>();
            try
            {
                level.InitializeGenerated(Graph(), new[] { Lamp(11, false), Lamp(12, true) });
                Set(manager, "_state", state); Set(manager, "_controller", c); Set(manager, "_level", level);
                var fact = new ShrineResolvedFact(1, new ShrineActivatedFact(9, ShrineKind.Wick, 1, Vector3.zero, 0), ShrineKind.Wick, false, wickSeconds: 1f);
                Call(manager, "HandleShrineResolved", fact); Call(manager, "HandleShrineResolved", fact);
                Assert.That(level.Interactables.TryGet(11, out var lamp), Is.True); Assert.That(lamp.Value, Is.EqualTo(InteractableStateValue.Lit));
                Assert.That(manager.WickActive, Is.True);
                Call(manager, "HandleTick", default(InputFrame), 1f, 1L);
                Assert.That(manager.WickActive, Is.False);
                level.Interactables.TryGet(11, out lamp); Assert.That(lamp.Value, Is.EqualTo(InteractableStateValue.Inactive));
                level.Interactables.TryGet(12, out lamp); Assert.That(lamp.Value, Is.EqualTo(InteractableStateValue.Lit));
                Call(manager, "HandleShrineResolved", new ShrineResolvedFact(1,
                    new ShrineActivatedFact(10, ShrineKind.Wick, 1, Vector3.zero, 2), ShrineKind.Wick, false, wickSeconds: 2f));
                Call(manager, "ReleaseFloor"); Assert.That(manager.WickActive, Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(levelRoot); }
        }
        [Test]
        public void GapFacingSelectsPocketNotSourceOrUnrelatedRoom()
        {
            var site = new ShrineSite(Vector3.zero, 1, true);
            Assert.That(ExpeditionSessionController.PassagePocket(Graph(), site, Vector3.right), Is.EqualTo(2));
            Assert.That(ExpeditionSessionController.PassagePocket(Graph(), site, Vector3.left), Is.Zero);
            Assert.That(ExpeditionSessionController.PassagePocket(Graph(), new ShrineSite(Vector3.zero, 1), Vector3.right), Is.Zero);
        }
        [Test]
        public void LateHuntersAreOwnedAndShrineResolutionsAreGenerationGuarded()
        {
            var c = Ready(out var state); c.RecordHunter(new EntityId(-1));
            var fact = new ShrineResolvedFact(1, new ShrineActivatedFact(1, ShrineKind.Purgatory, 1, Vector3.zero, 1), ShrineKind.Purgatory, false);
            Assert.That(c.AcceptShrine(fact), Is.True); Assert.That(c.AcceptShrine(fact), Is.False);
            Assert.That(c.AcceptShrine(new ShrineResolvedFact(2, fact.Activation, ShrineKind.Purgatory, false)), Is.False);
            c.ReleaseActors(); Assert.That(state.Player.IsValid, Is.False);
        }
        [Test]
        public void PlacementStreamReplaysAndWorldTeardownRemovesObjects()
        {
            var config = ScriptableObject.CreateInstance<ShrineConfig>(); var visual = ScriptableObject.CreateInstance<ShrineDriverConfig>();
            var a = new GameObject("shrines a"); var b = new GameObject("shrines b");
            try
            {
                var sites = new[] { new ShrineSite(Vector3.zero, 1), new ShrineSite(Vector3.right * 4, 1), new ShrineSite(Vector3.forward * 4, 1) };
                var first = a.AddComponent<ShrineManager>(); var second = b.AddComponent<ShrineManager>();
                var x = first.Assemble(sites, 8, config, visual, new System.Random(ExpeditionSessionController.ShrineSeed(17, 8)));
                var y = second.Assemble(sites, 8, config, visual, new System.Random(ExpeditionSessionController.ShrineSeed(17, 8)));
                Assert.That(x, Is.EqualTo(y)); Assert.That(x.Count, Is.GreaterThan(0));
                first.Teardown(); Assert.That(a.GetComponentsInChildren<TextMesh>(), Is.Empty);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(config); Object.DestroyImmediate(visual); }
        }
        private static InteractableState Lamp(int id, bool lit) => new InteractableState(id, InteractableKind.Light, 1, Vector3.zero,
            lit ? InteractableStateValue.Lit : InteractableStateValue.Inactive);
        private static LevelGraph Graph() => new LevelGraph(new[] {
            new LevelRoom(1, Vector3.up * 2, new Vector3(8, 4, 8)),
            new LevelRoom(2, new Vector3(16, 2, 0), new Vector3(8, 4, 8), pocket: true) },
            Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
