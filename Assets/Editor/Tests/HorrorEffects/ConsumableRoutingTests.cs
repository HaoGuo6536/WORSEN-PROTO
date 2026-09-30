// ============================================================================
// ConsumableRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Verifies real Session routing from consumable presses to inventory and Player.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Exercise real timed web cleansing and Level-backed Doorstop admission/expiry.
//   - Reject duplicate ticks; pause moving heals; clear slows; admit one revival.
// DEPENDENCIES:
//   Core, Player, Level, HorrorEffects, Progression, NUnit and transient Unity objects.
// USAGE NOTES:
//   Edit Mode. Reflection supplies isolated state and restores the service singleton.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using Worsen.Session.HorrorEffects;
using Worsen.Session.Progression;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.HorrorEffects
{
    public sealed class ConsumableRoutingTests
    {
        private readonly List<Object> owned = new List<Object>();
        private PlayerManager player;
        private PlayerBehaviorState motion;
        private HorrorEffectsManager effects;
        private ProgressionSessionController progression;
        private ProgressionSessionManager service;
        private int generation;

        [TearDown]
        public void Cleanup()
        {
            if (effects != null) effects.Suspend();
            if (player != null) Registry("Unregister");
            typeof(ProgressionSessionManager).GetProperty("Instance").SetValue(null, null);
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [TestCase("firecracker", 1)]
        [TestCase("gauze", 0)]
        [TestCase("smelling-salts", 0)]
        [TestCase("wax-ward", 0)]
        [TestCase("oil-flask", 0)]
        [TestCase("adrenaline", 0)]
        public void MovingUseConsumesOnceAndDuplicateTickCannotRepeat(string id, int remaining)
        {
            Setup(id, EffectKind.Consumable);
            Property(motion, "Health", 20f); Property(motion, "Velocity", Vector3.forward * 3f);
            var frame = UseFrame;
            effects.Tick(frame, .02f, 1); effects.Tick(frame, .02f, 1);
            Assert.That(progression.Consumables().RemainingUses[0], Is.EqualTo(remaining));
            Assert.That(motion.Velocity, Is.EqualTo(Vector3.forward * 3f));
            if (id == "wax-ward") Assert.That(progression.Snapshot().Effects.WaxWardCharges, Is.EqualTo(1));
        }

        [Test]
        public void SaltsPublishesOneCleanseAndClearsBothSlowKinds()
        {
            Setup("smelling-salts", EffectKind.Consumable);
            player.SetTrapSpeedMultiplier(.4f); player.ApplyWebSlow(new WebHitFact(new EntityId(-1), player.Id, 0, 1, .3f, 3f, 1f));
            int count = 0; effects.SensesCleansed += fact => { Assert.That(fact.PlayerId, Is.EqualTo(player.Id)); count++; };
            effects.Tick(UseFrame, .02f, 1);
            Assert.That(count, Is.EqualTo(1)); Assert.That(motion.TrapSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(motion.WebSpeedMultiplier, Is.EqualTo(1f)); Assert.That(motion.WebSlowRemaining, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void DoorstopSelectsDoorAndRejectsOpenWithoutPublishingUntilExpiryOrBreak(bool broken)
        {
            Setup("doorstop", EffectKind.Consumable);
            var level = Component<LevelManager>();
            level.gameObject.SetActive(true);
            var graph = LevelGraphUtility.Build(new[] {
                new LevelRoom(1, Vector3.up * 2, new Vector3(12, 4, 12)),
                new LevelRoom(2, new Vector3(12, 2, 0), new Vector3(12, 4, 12)) },
                new[] { new LevelEdge(11, 1, 2, true, TraversalAccess.All) }, Array.Empty<LevelAnchor>(), 1, Vector3.zero);
            level.InitializeGenerated(graph, new[] { new InteractableState(101, InteractableKind.Door, 1,
                Vector3.back, InteractableStateValue.Open, 11) });
            effects.ConfigureHazards(service, null, levelService: level);
            effects.Tick(UseFrame, .02f, 1);
            Assert.That(effects.IsDoorJammed(101), Is.True);
            int changes = 0, playerOpens = 0;
            level.InteractableChanged += (a, b) => changes++;
            level.DoorOpened += (door, byPlayer) => { if (byPlayer) playerOpens++; };
            Assert.That(level.OpenDoor(101, openedByPlayer: true), Is.False);
            Assert.That(changes, Is.Zero); Assert.That(playerOpens, Is.Zero);
            Assert.That(level.ClosedDoors[11], Is.True);
            if (broken)
            {
                Assert.That(effects.CompleteDoorBreak(101), Is.True);
                Assert.That(effects.IsDoorJammed(101), Is.False); Assert.That(level.ClosedDoors[11], Is.False);
            }
            else
            {
                effects.Tick(default, 1000f, 2);
                Assert.That(effects.IsDoorJammed(101), Is.False);
                Assert.That(level.OpenDoor(101, openedByPlayer: true), Is.True);
                Assert.That(playerOpens, Is.EqualTo(1));
            }
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void GauzeActuallyHealsOnlyWhileMoving()
        {
            Setup("gauze", EffectKind.Consumable); Property(motion, "Health", 40f);
            effects.Tick(UseFrame, .02f, 1);
            effects.Tick(default, 2f, 2); Assert.That(motion.Health, Is.EqualTo(40f));
            Property(motion, "Velocity", Vector3.forward);
            effects.Tick(default, 4f, 3); Assert.That(motion.Health, Is.EqualTo(75f).Within(.001f));
        }

        [Test]
        public void NoncriticalAdrenalineDoesNotSpendSlot()
        {
            Setup("adrenaline", EffectKind.Consumable);
            effects.Tick(UseFrame, .02f, 1);
            Assert.That(progression.Consumables().Inventory[0].Id, Is.EqualTo("adrenaline"));
        }

        [Test]
        public void RevivalIsSpentOnceAndReturnsToFloorStartWithoutChangingGeneration()
        {
            Setup("extra-life", EffectKind.Upgrade);
            Vector3 start = motion.Position;
            motion.Health = 0f; motion.Position = Vector3.right * 20f;
            Assert.That(effects.TryBeginRevival(player.Id), Is.True);
            Assert.That(motion.IsAlive, Is.False, "The catch must finish before revival.");
            effects.Tick(default, 1f, 10);
            Assert.That(progression.Snapshot().GenerationId, Is.EqualTo(generation));
            Assert.That(progression.Snapshot().Phase, Is.EqualTo(ProgressionPhase.Exploring));
            Assert.That(effects.CompleteRevival(player.Id), Is.True);
            Assert.That(motion.Position, Is.EqualTo(start)); Assert.That(motion.Health, Is.EqualTo(motion.MaxHealth * .5f));
            motion.Health = 0f;
            Assert.That(effects.TryBeginRevival(player.Id), Is.False);
        }

        private static InputFrame UseFrame => new InputFrame(Vector2.up, Vector2.zero, InputButtons.UseConsumable, InputButtons.UseConsumable, InputButtons.None);
        private void Open()
        {
            if (progression.Snapshot().Phase == ProgressionPhase.ChooseThreat) progression.ChooseThreat(progression.Snapshot().Choices[0].Id, progression.Snapshot().Revision);
            if (progression.Snapshot().Phase == ProgressionPhase.ChooseCurse) progression.ChooseCurse(progression.Snapshot().Choices[0].Id, progression.Snapshot().Revision);
            Assert.That(progression.ConfirmFloorReady(progression.Snapshot().GenerationId), Is.True);
        }
        private void Setup(string id, EffectKind kind)
        {
            Assert.That(PlayerRegistry.Items, Is.Empty); Assert.That(ProgressionSessionManager.Instance, Is.Null);
            player = Component<PlayerManager>();
            var mover = Config<PlayerMoverDriverConfig>(); Set(mover, "_hunterBodyLayer", "Ignore Raycast");
            Set(player.GetComponent<PlayerDriver>(), "_config", mover);
            player.Initialize(Config<PlayerProfile>(), new EntityContext(new EntityId(1), new System.Random(7)));
            motion = (PlayerBehaviorState)player.ReadOnlyState; Registry("Register"); player.gameObject.SetActive(true);
            var catalogue = Config<EffectCatalogueConfig>();
            Set(catalogue, "_entries", new[] { new EffectCatalogueEntry(id, kind, FearAxis.Agency, id, "Test purchase.", price: 0) });
            var config = Config<ProgressionConfig>(); Set(config, "_effectCatalogue", catalogue);
            var state = new ProgressionSessionBehaviorState(); progression = new ProgressionSessionController(state, config, new System.Random(7));
            progression.StartRun(7);
            for (int i = 0; i < 2; i++) { Open(); progression.CompleteFloor(progression.Snapshot().GenerationId); }
            progression.ConfirmFloorReady(progression.Snapshot().GenerationId);
            Assert.That(progression.Purchase(id, progression.Snapshot().Revision), Is.True);
            progression.ContinueShop(progression.Snapshot().Revision); Open(); generation = progression.Snapshot().GenerationId;
            service = Component<ProgressionSessionManager>(); Set(service, "state", state); Set(service, "controller", progression);
            typeof(ProgressionSessionManager).GetProperty("Instance").SetValue(null, service);
            effects = Component<HorrorEffectsManager>(); var tuning = Config<HorrorEffectsConfig>();
            Set(effects, "controller", new HorrorEffectsController(new HorrorEffectsBehaviorState(), tuning));
            Set(effects, "consumables", new ConsumableController(new ConsumableBehaviorState(), tuning));
            Set(effects, "config", tuning); Set(effects, "driver", effects.gameObject.AddComponent<HorrorEffectsDriver>());
            effects.gameObject.SetActive(true); effects.BeginFloor(generation, default); effects.ConfigureHazards(service, null);
            effects.ObserveAim(new FlashlightSample(player.Id, 0, true, Vector3.up, Vector3.forward, 10f, 45f));
        }

        private T Component<T>() where T : Component
        { var go = new GameObject(typeof(T).Name + " item test"); go.SetActive(false); owned.Add(go); return go.AddComponent<T>(); }
        private T Config<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
        private void Registry(string name) => typeof(PlayerRegistry).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { player });
        private static void Set(object value, string name, object data) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);
        private static void Property(object value, string name, object data) => value.GetType().GetProperty(name).SetValue(value, data);
    }
}
