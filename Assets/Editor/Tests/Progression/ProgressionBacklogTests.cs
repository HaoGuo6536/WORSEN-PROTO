// ============================================================================
// ProgressionBacklogTests.cs
// ============================================================================
// PURPOSE:
//   Exercises WP-P economy, roster, shrine and type-wide effect regressions.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Verify real exit transactions remove unused items only with Spent Pockets.
//   - Verify Golden Greed yield, Chance caps and once-per-run Extra Life admission.
//   - Check roster gates and a shared curse view on separate hunter instances.
// DEPENDENCIES:
//   NUnit, Core, Progression/Shop, Hunter pure controllers and transient configs.
// USAGE NOTES:
//   Edit Mode. No assets or scenes are saved; actual spawns/tells need integration proof.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression;
using Worsen.Session.Progression.Shop;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Ram;
using Worsen.Domain.Player;
using Worsen.Tests.Hunter;
using Object = UnityEngine.Object;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Progression
{
    public sealed class ProgressionBacklogTests
    {
        private readonly List<Object> owned = new List<Object>();
        private T Config<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
        [TearDown] public void TearDown() { foreach (var value in owned) Object.DestroyImmediate(value); owned.Clear(); }
        private static void Set(object value, string field, object data) => value.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);
        private static void Open(ProgressionSessionController session)
        {
            if (session.Snapshot().Phase == ProgressionPhase.ChooseThreat)
                Assert.That(session.ChooseThreat(session.Snapshot().Choices[0].Id, session.Snapshot().Revision), Is.True);
            if (session.Snapshot().Phase == ProgressionPhase.ChooseCurse)
                Assert.That(session.ChooseCurse(session.Snapshot().Choices[0].Id, session.Snapshot().Revision), Is.True);
            Assert.That(session.ConfirmFloorReady(session.Snapshot().GenerationId), Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void ExitRemovesUnusedItemsOnlyWithSpentPockets(bool spent)
        {
            var config = Config<ProgressionConfig>(); var catalogue = Config<EffectCatalogueConfig>();
            Set(config, "_effectCatalogue", catalogue); Set(config, "_eventPool", Array.Empty<ProgressionEventKind>());
            Set(config, "_curses", new[] { new ProgressionEntryConfig(spent ? "spent-pockets" : "hidden-count", "Curse", "Removes safety.") });
            Set(catalogue, "_entries", new[] { new EffectCatalogueEntry("firecracker", EffectKind.Consumable,
                FearAxis.Information, "Firecracker", "Draws hunters.", price: 0) });
            var session = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(73));
            session.StartRun(73);
            for (int floor = 0; floor < 2; floor++) { Open(session); Assert.That(session.CompleteFloor(session.Snapshot().GenerationId), Is.True); }
            Open(session); Assert.That(session.Purchase("firecracker", session.Snapshot().Revision), Is.True);
            Assert.That(session.ContinueShop(session.Snapshot().Revision), Is.True); Open(session);
            Assert.That(session.TryConsumeSelected(session.Snapshot().GenerationId, session.Snapshot().Revision, "firecracker"), Is.True);
            Assert.That(session.Consumables().RemainingUses[0], Is.EqualTo(1));
            var frozen = session.Consumables();
            Assert.That(session.CompleteFloor(session.Snapshot().GenerationId - 1), Is.False);
            Assert.That(session.Consumables().RemainingUses[0], Is.EqualTo(1));
            Assert.That(session.CompleteFloor(session.Snapshot().GenerationId), Is.True);
            Assert.That(session.EffectsSnapshot().ActiveEffects.Has(new EffectId("firecracker")), Is.EqualTo(!spent));
            Assert.That(session.Consumables().Inventory.Any(slot => slot.Id == "firecracker"), Is.EqualTo(!spent));
            Assert.That(frozen.RemainingUses[0], Is.EqualTo(1));
        }
        [Test]
        public void GoldenGreedDoublesYieldAndOverflowDoesNotPay()
        {
            var catalogue = Config<EffectCatalogueConfig>();
            var shop = new ShopController(new ShopBehaviorState(), new ShopRules(), catalogue, new System.Random(1));
            var active = new ActiveEffects(new[] { new ActiveEffect(new EffectId("gilded-greed"), EffectKind.Upgrade, 1),
                new ActiveEffect(new EffectId("golden-touch"), EffectKind.Upgrade, 1) });
            Assert.That(shop.GoldenCredit(0, 1, active, out int credit), Is.True); Assert.That(credit, Is.EqualTo(4));
            Assert.That(shop.GoldenCredit(int.MaxValue - 3, 1, active, out _), Is.False);
            Assert.That(shop.GoldenCredit(0, 1, active, out credit, 1.5f), Is.True); Assert.That(credit, Is.EqualTo(6));
        }
        [Test]
        public void ChanceCannotRedrawSpentExtraLifeAndDoesNotDoubleCountItsOwnStacks()
        {
            var catalogue = Config<EffectCatalogueConfig>();
            Set(catalogue, "_entries", new[] { new EffectCatalogueEntry("extra-life", EffectKind.Upgrade,
                FearAxis.Stakes, "Life", "Revives once.") });
            var shrine = new ShrineProgressionController(new ShrineProgressionBehaviorState(), new ShrineProgressionRules(), catalogue, new System.Random(1));
            var fact = new ShrineActivatedFact(1, ShrineKind.Chance, 1, default, 1);
            Assert.That(shrine.Resolve(1, 20, fact, 0, 0f, 0f, default(ActiveEffects), out _, true), Is.False);
            Set(catalogue, "_entries", new[] { new EffectCatalogueEntry("test-upgrade", EffectKind.Upgrade,
                FearAxis.Agency, "Test", "Adds a rule.", cap: 3) });
            var config = Config<ProgressionConfig>(); Set(config, "_effectCatalogue", catalogue);
            var session = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(1));
            session.StartRun(1); Open(session);
            for (int id = 1; id <= 3; id++)
            {
                Assert.That(session.ActivateShrine(session.Snapshot().GenerationId,
                    new ShrineActivatedFact(id, ShrineKind.Chance, 1, default, id), 0f, 0f, out _), Is.True);
                Assert.That(session.EffectsSnapshot().ActiveEffects.Stacks(new EffectId("test-upgrade")), Is.EqualTo(id));
            }
            Assert.That(session.ActivateShrine(session.Snapshot().GenerationId,
                new ShrineActivatedFact(4, ShrineKind.Chance, 1, default, 4), 0f, 0f, out _), Is.False);
        }
        [Test]
        public void SharedTypeCurseChangesBothInstancesWithoutMutatingTheirConfig()
        {
            var config = Config<RamConfig>(); var profile = Config<HunterProfile>();
            var effects = new ActiveEffects(new[] { new ActiveEffect(RamController.ShorterWindup, EffectKind.Curse, 3) });
            var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, Position = Vector3.forward * 10 };
            var world = new EchoControllerTests.World();
            foreach (int id in new[] { -1, -2 })
            {
                var hunter = new RosterBTestHunter { Id = new EntityId(id), IsActive = true, Forward = Vector3.forward };
                var ram = new RamController(config, profile);
                ram.Reset(new HunterArchetypeContext(hunter, player, world, world, world.Doors, world, effects, 0f, 1, true, 1f));
                Assert.That(ram.WindupSeconds, Is.EqualTo(config.WindupSeconds * Mathf.Pow(config.ShorterWindupMultiplier, 3)).Within(.0001f));
            }
            Assert.That(config.WindupSeconds, Is.EqualTo(1f));
        }
        [TestCase(1)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void ActualOffersRespectRoundGatesAndExposeEveryEligibleHunter(int round)
        {
            var config = Config<ProgressionConfig>(); Set(config, "_eventPool", Array.Empty<ProgressionEventKind>());
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 64; seed++)
            {
                var state = new ProgressionSessionBehaviorState();
                var session = new ProgressionSessionController(state, config, new System.Random(seed)); session.StartRun(seed);
                typeof(ProgressionSessionBehaviorState).GetProperty("Round").SetValue(state, round);
                typeof(ProgressionSessionController).GetMethod("BuildThreatChoices", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
                foreach (var choice in session.Snapshot().Choices)
                { Assert.That(ProgressionRosterUtility.Admits(choice.Id, round), Is.True); seen.Add(choice.Id); }
            }
            Assert.That(seen, Is.EquivalentTo(config.Threats.Where(t => ProgressionRosterUtility.Admits(t.Id, round)).Select(t => t.Id)));
        }
    }
}
