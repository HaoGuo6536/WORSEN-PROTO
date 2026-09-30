// ============================================================================
// ExitCollapseLightingTests.cs
// ============================================================================
// PURPOSE:
//   Verifies collapse budgets cannot starve eligible exit-room lights.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Reserve exit lights under zero density and competing nearest lights.
//   - Keep range/cap and authoritative non-exit light gating intact.
// DEPENDENCIES:
//   NUnit, Core light values and pure EnvironmentPresenter/state.
// USAGE NOTES:
//   Edit Mode; no Lumen objects or scene mutation.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Environment;

namespace Worsen.Tests.CastleEnvironment
{
    public sealed class ExitCollapseLightingTests
    {
        [TestCase(0f)] [TestCase(.4f)] [TestCase(1f)]
        public void ExitRoomLightsSurviveDensityAndTakePriorityWithinCap(float multiplier)
        {
            var state = new EnvironmentDriverState { TorchCountMultiplier = multiplier, ExitLightIndex = 2 };
            for (int i = 0; i < 3; i++)
            {
                state.Positions.Add(Vector3.right * i); state.Available.Add(true);
                state.Flames.Add(new EnvironmentFlameDriverState { RoomId = i == 0 ? 1 : 2, Exit = i == 2,
                    SocketPosition = Vector3.right * i });
            }
            Assert.That(EnvironmentPresenter.BudgetedLights(state, 2, 20f), Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(EnvironmentPresenter.BudgetedLights(state, 1, 20f), Is.EquivalentTo(new[] { 1 }));
            Assert.That(EnvironmentPresenter.BudgetedLights(state, 0, 20f), Is.Empty);
            Assert.That(EnvironmentPresenter.BudgetedLights(state, 3, .5f), Has.No.Member(1));
            var off = new InteractableState(1, InteractableKind.Light, 2, Vector3.right,
                InteractableStateValue.Inactive);
            Assert.That(EnvironmentPresenter.ApplyLight(state, off), Is.False);
            Assert.That(state.Available[1], Is.True);
        }
    }
}
