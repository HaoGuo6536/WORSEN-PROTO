// ============================================================================
// HunterArchetypeControllerTests.cs
// ============================================================================
// PURPOSE:
//   Protects the parent-owned neutral rule adapter used by sibling plug-ins.
//   Identity hooks must not alter observations or consume random draws. Derived
//   rules must not accidentally inherit compatibility-only curse permissions.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify neutral hooks, inert replay/facts and explicit legacy opt-in.
// DEPENDENCIES:
//   - Parent Hunter rule adapter, Core values and NUnit.
// USAGE NOTES:
//   Managed-only tests; no scene, assets or native engine calls.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterArchetypeControllerTests
    {
        private sealed class Extension : HunterArchetypeController { }
        [TestCase(true)] [TestCase(false)] public void NeutralHooksLeaveSharedDecisionsAlone(bool visible)
        {
            var rules = new HunterArchetypeController(); rules.Reset(default); rules.Tick(default); rules.CommitReplay(3, true);
            Assert.That(rules.FilterVisibility(visible, default, default), Is.EqualTo(visible));
            Assert.That(rules.GoalUtility(HunterGoal.LocatePrey, 7f), Is.EqualTo(7f));
            Assert.That(rules.TryMovement(out _, out _), Is.False); Assert.That(rules.ReplayPath, Is.Null);
            Assert.That(rules.TryTakeFact(out _), Is.False); Assert.That(rules.OwnsPursuit || rules.NeverLoses, Is.False);
        }
        [Test] public void DerivedRulesDoNotInheritLegacyTraitPermission()
        {
            Assert.That(new HunterArchetypeController().AllowsLegacyTraits, Is.True);
            Assert.That(new Extension().AllowsLegacyTraits, Is.False);
        }
    }
}
