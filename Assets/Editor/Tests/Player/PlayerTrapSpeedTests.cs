// ============================================================================
// PlayerTrapSpeedTests.cs
// ============================================================================
// PURPOSE:
//   Protects independent trap/grab speed composition in the locomotion controller.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Player.
// KEY RESPONSIBILITIES:
//   - Verify walking, sprinting, independent release and reset to neutral factors.
// DEPENDENCIES:
//   Core input/probe values, Player pure logic, NUnit and transient PlayerProfile.
// USAGE NOTES:
//   Edit Mode. Inputs, ground probe, elapsed time and random seed are explicit.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PlayerTrapSpeedTests
    {
        [TestCase(false)] [TestCase(true)]
        public void TrapAndGrabMultiplyAndReleaseIndependently(bool sprint)
        {
            var profile = ScriptableObject.CreateInstance<PlayerProfile>();
            try
            {
                var state = new PlayerBehaviorState(); var c = new PlayerController(state, profile, new System.Random(7));
                c.Reset(new EntityId(1), Vector3.zero, 0f);
                c.SetGrabSpeedMultiplier(.5f); c.SetTrapSpeedMultiplier(.6f);
                var frame = new InputFrame(Vector2.up, Vector2.zero, sprint ? InputButtons.Sprint : InputButtons.None, InputButtons.None, InputButtons.None);
                var ground = new MovementProbe(true, Vector3.up);
                float baseline = sprint ? profile.SprintSpeed : profile.WalkSpeed;
                for (int i = 1; i <= 30; i++) c.Tick(frame, ground, .1f, i);
                Assert.That(state.Velocity.z, Is.EqualTo(baseline * .5f * .6f).Within(.001f));
                c.SetTrapSpeedMultiplier(1f);
                for (int i = 31; i <= 60; i++) c.Tick(frame, ground, .1f, i);
                Assert.That(state.Velocity.z, Is.EqualTo(baseline * .5f).Within(.001f));
                c.SetTrapSpeedMultiplier(.6f); c.SetGrabSpeedMultiplier(1f);
                for (int i = 61; i <= 90; i++) c.Tick(frame, ground, .1f, i);
                Assert.That(state.Velocity.z, Is.EqualTo(baseline * .6f).Within(.001f));
                c.Reset(new EntityId(1), Vector3.zero, 0f);
                Assert.That(state.TrapSpeedMultiplier, Is.EqualTo(1f)); Assert.That(state.GrabSpeedMultiplier, Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
