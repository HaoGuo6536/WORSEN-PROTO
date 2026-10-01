// ============================================================================
// SkipModuleManager.cs
// ============================================================================
// PURPOSE:
//   Registers Skip's route-learning rules and acknowledges atomic teleports.
//   Floor generations and committed traversal uses stay explicit inputs, while
//   driver placement remains the authority for a successful interception.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Skip.
// KEY RESPONSIBILITIES:
//   - Register rules and forward generation/traversal inputs.
//   - Route teleport requests, acknowledgements and post-motion facts.
// DEPENDENCIES:
//   - Local Skip rules/config, parent Hunter contracts/driver and Core facts.
// USAGE NOTES:
//   Scene-owned; factory creates the facet, root owns tick and teardown.
//   No subscriptions, independent state or time source. Shared contacts are retained.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Skip
{
    public sealed class SkipModuleManager : HunterArchetypeManager
    {
        private SkipController Controller => (SkipController)Rules;
        public static void Register(HunterArchetypeFactory factory)
            => factory.Register<SkipConfig, SkipModuleManager>((config, profile, random) => new SkipController(config, profile, random));
        public override bool BeginFloor(long generation) => Controller.BeginFloor(generation);
        public override void BeforeCatch() => PublishFacts();
        public override bool RecordUse(SkipTraversalUse use) => Controller.RecordUse(use);
        public override bool Move(float dt)
        {
            if (!Controller.TryTeleport(out Vector3 arrival)) return false;
            Controller.CommitTeleport(Driver.TryTeleport(arrival), arrival); return true;
        }
        public override void AfterPose(HunterTickResult result) => PublishFacts();
        public override void PublishFacts() { while (Controller.TakeFact(out SkipFact fact)) Events.Publish(fact); }
    }
}
