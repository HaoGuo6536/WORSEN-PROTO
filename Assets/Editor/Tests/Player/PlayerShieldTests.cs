// ============================================================================
// PlayerShieldTests.cs
// ============================================================================
// PURPOSE:
//   Tests shield absorption separately from health regeneration and floor refills.
//   Explicit transfer models the assembly owner's destruction and replacement of Player.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Player.
// KEY RESPONSIBILITIES:
//   - Verify shield-first damage, grace, non-regeneration, transfer and new-life reset.
// DEPENDENCIES:
//   - Domain Player, Core, NUnit and temporary PlayerProfile allocation.
// USAGE NOTES:
//   Pure Controller tests; actual floor-to-floor transfer must be wired by Expedition.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Player
{
    public sealed class PlayerShieldTests
    {
        private PlayerProfile profile;
        private PlayerBehaviorState state;
        private PlayerController controller;
        [SetUp] public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<PlayerProfile>(); state = new PlayerBehaviorState();
            controller = new PlayerController(state, profile, new System.Random(1));
            controller.Reset(new EntityId(1), Vector3.zero, 0f);
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(profile);
        [Test]
        public void ShieldAbsorbsBeforeHealthAndGraceDoesNotSpendIt()
        {
            Assert.That(controller.GrantShield(40f), Is.True);
            Assert.That(controller.ApplyHit(25f).Changed, Is.True);
            Assert.That(state.Shield, Is.EqualTo(15f)); Assert.That(state.Health, Is.EqualTo(profile.MaximumHealth));
            Assert.That(controller.ApplyHit(25f).AbsorbedByGrace, Is.True); Assert.That(state.Shield, Is.EqualTo(15f));
            controller.AdvanceRecovery(state.GraceWindow.EndTick);
            controller.ApplyHit(25f);
            Assert.That(state.Shield, Is.Zero); Assert.That(state.Health, Is.EqualTo(profile.MaximumHealth - 10f));
        }
        [Test]
        public void ShieldNeverRegeneratesOrRefillsAtFloorStartAndTransfersToReplacement()
        {
            controller.GrantShield(40f); controller.ApplyHit(25f);
            controller.Tick(default, new MovementProbe(true, Vector3.up), 30f, 10000);
            Assert.That(state.Shield, Is.EqualTo(15f));
            controller.BeginFloorHealth(profile.MaximumHealth, 1f); Assert.That(state.Shield, Is.EqualTo(15f));
            float transfer = ((IReadOnlyPlayerShieldState)state).Shield;
            controller.Reset(new EntityId(2), Vector3.zero, 0f); Assert.That(state.Shield, Is.Zero);
            Assert.That(controller.RestoreShield(transfer), Is.True);
            controller.BeginFloorHealth(profile.MaximumHealth, 1f); Assert.That(state.Shield, Is.EqualTo(transfer));
            controller.Reset(new EntityId(3), Vector3.zero, 0f); Assert.That(state.Shield, Is.Zero);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)] [TestCase(0f)]
        public void InvalidGrantsCannotAlterShield(float value)
        { Assert.That(controller.GrantShield(value), Is.False); Assert.That(state.Shield, Is.Zero); }
        [Test]
        public void ShieldCanStackButCannotResurrectThePlayer()
        {
            controller.GrantShield(40f); controller.GrantShield(80f); Assert.That(state.Shield, Is.EqualTo(120f));
            controller.ApplyHit(1000f); Assert.That(state.IsAlive, Is.False);
            Assert.That(controller.GrantShield(40f), Is.False); Assert.That(controller.RestoreShield(40f), Is.False);
        }
    }
}
