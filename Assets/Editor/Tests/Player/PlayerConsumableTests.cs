// ============================================================================
// PlayerConsumableTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the small Player APIs used by Session consumables and Extra Life.
//   Explicit healing seconds and speed factors keep effect lifetimes outside Player.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Player.
// KEY RESPONSIBILITIES:
//   - Use the sole timed web contact API and ensure cleansing cancels its lifetime.
//   - Cover healing clamps, speed composition, cleansing and half-health floor respawn.
// DEPENDENCIES:
//   - Player pure controller/state/profile, Core values and NUnit.
// USAGE NOTES:
//   Edit Mode; profile objects are temporary and never saved.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Player
{
    public sealed class PlayerConsumableTests
    {
        private PlayerProfile profile;
        private PlayerController player;
        private PlayerBehaviorState state;
        [SetUp] public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<PlayerProfile>(); state = new PlayerBehaviorState();
            player = new PlayerController(state, profile, new System.Random(1));
            player.Reset(new EntityId(1), new Vector3(2f, 0f, 3f), 45f);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(profile);
        [Test]
        public void HealingUsesMovingSecondsClampsAndCannotResurrect()
        {
            player.ApplyRunModifiers(50f, 100f, 1f);
            Assert.That(player.HealOverTime(8.75f, 0f), Is.False); Assert.That(state.Health, Is.EqualTo(50f));
            Assert.That(player.HealOverTime(8.75f, 4f), Is.True); Assert.That(state.Health, Is.EqualTo(85f));
            player.HealOverTime(100f, 1f); Assert.That(state.Health, Is.EqualTo(100f));
            player.ApplyRunModifiers(0f, 100f, 1f);
            Assert.That(player.HealOverTime(100f, 1f), Is.False); Assert.That(state.IsAlive, Is.False);
        }
        [Test]
        public void BurstMultipliesSpeedWhileSaltsClearWebAndTrapButNotGrab()
        {
            float baseline = player.MaximumMovementSpeed;
            player.SetTrapSpeedMultiplier(.5f); player.ApplyWebSlow(new WebHitFact(new EntityId(-1), state.Id, 0, 1, .5f, 3f, 1f)); player.SetGrabSpeedMultiplier(.5f);
            player.SetConsumableSpeedMultiplier(1.5f);
            Assert.That(player.MaximumMovementSpeed, Is.EqualTo(baseline * .5f * .5f * .5f * 1.5f).Within(.001f));
            player.ClearSlows(); Assert.That(player.MaximumMovementSpeed, Is.EqualTo(baseline * .5f * 1.5f).Within(.001f));
            Assert.That(state.WebSlowRemaining, Is.Zero); Assert.That(state.WebSpeedMultiplier, Is.EqualTo(1f));
            player.ApplyWebSlow(new WebHitFact(new EntityId(-1), state.Id, 1, 2, .8f, .1f, 1f));
            Assert.That(state.WebSpeedMultiplier, Is.EqualTo(.8f));
            player.Tick(default, default, .2f, 2);
            Assert.That(state.WebSlowRemaining, Is.Zero); Assert.That(state.WebSpeedMultiplier, Is.EqualTo(1f));
            player.SetConsumableSpeedMultiplier(1f); Assert.That(player.MaximumMovementSpeed, Is.EqualTo(baseline * .5f).Within(.001f));
        }
        [Test]
        public void RevivalRestoresFloorStartAtHalfEffectiveMaximumWithoutStartingANewFloor()
        {
            Vector3 start = state.Position;
            player.ApplyRunModifiers(0f, 120f, 1.2f); state.Position = new Vector3(20f, 0f, 30f); state.Tick = 900;
            Assert.That(player.RespawnAtFloorStart(.5f), Is.True);
            Assert.That(state.Position, Is.EqualTo(start)); Assert.That(state.Health, Is.EqualTo(60f));
            Assert.That(state.MaxHealth, Is.EqualTo(120f)); Assert.That(state.HeadingDegrees, Is.EqualTo(45f));
            Assert.That(state.Tick, Is.EqualTo(900)); Assert.That(state.IsAlive, Is.True);
            Assert.That(player.RespawnAtFloorStart(.5f), Is.False); Assert.That(state.Health, Is.EqualTo(60f));
        }
    }
}
