// ============================================================================
// StareModuleManager.cs
// ============================================================================
// PURPOSE:
//   Registers Stare rules and injects its placement driver through a parent port.
//   Post-reaction placement and presence commands retain the original bounded
//   candidate order, keeping concrete Stare types outside the shared Hunter stack.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Stare.
// KEY RESPONSIBILITIES:
//   - Register rules and wire the owned placement sub-driver.
//   - Sequence placement admission, visibility and attack/catch cues.
//   - Publish Stare facts at the original delivery boundary.
// DEPENDENCIES:
//   - Local Stare stack, parent Hunter contracts/driver and Core facts.
// USAGE NOTES:
//   Scene-owned; factory creates the facet, root owns tick and teardown.
//   No subscriptions or independent state; HunterDriver owns sub-driver lifetime.
//   IEntityHandle uses the root identity inherited through HunterArchetypeManager.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Stare
{
    public sealed class StareModuleManager : HunterArchetypeManager, IEntityHandle
    {
        private StareController Controller => (StareController)Rules;
        public static void Register(HunterArchetypeFactory factory)
            => factory.Register<StareConfig, StareModuleManager>((config, profile, random) => new StareController(config, random));
        public override float ObservationHeight => ((StareConfig)Profile.ArchetypeRules).ObservationHeight;
        public override void FinishTick() => PublishFacts();
        protected override void Configure()
        {
            var driver = GetComponent<StareDriver>();
            if (driver == null) driver = gameObject.AddComponent<StareDriver>();
            Driver.ConfigureStare(driver);
        }
        public override void AfterReaction(HunterTickResult result)
        {
            if (!Shared.ReactionHeld && Controller.NeedsPlacement)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    bool valid = Driver.ProbeStare(Controller.Candidate(attempt), Player.Position, Shared.PlayerView, out Vector3 point);
                    if (!Controller.AcceptPlacement(point, valid, valid && Driver.PlayerViewClear(Shared.PlayerView, point, ObservationHeight))) continue;
                    Driver.PlaceStare(point); placed = true; break;
                }
                if (!placed) Controller.PlacementFailed();
            }
            Driver.SetStarePresent(Controller.Present);
            if (result.BeginLunge) Controller.AttackCue();
        }
        public override void BeginCatch() { Controller.CatchCue(); PublishFacts(); }
        public override void PublishFacts() { while (Controller.TakeFact(out StareFact fact)) Events.Publish(fact); }
    }
}
