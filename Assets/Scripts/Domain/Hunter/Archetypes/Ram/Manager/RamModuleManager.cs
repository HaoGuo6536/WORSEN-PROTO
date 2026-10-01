// ============================================================================
// RamModuleManager.cs
// ============================================================================
// PURPOSE:
//   Registers Ram's committed charge and routes its physical motion acknowledgement.
//   Contact normals and breakable identities stop at this Manager boundary before
//   reaching rules; the shared root no longer needs a concrete Ram reference.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Ram.
// KEY RESPONSIBILITIES:
//   - Register per-life charge rules and sequence charge motion.
//   - Resolve breakable identities and preserve normal-aware charge contacts.
//   - Publish charge facts before catch and after committed movement.
// DEPENDENCIES:
//   - Local Ram rules/config, parent Hunter contracts/driver and Core identities.
// USAGE NOTES:
//   Scene-owned; factory creates the facet, root owns tick and teardown.
//   No subscriptions or independent state. Controller owns charge admission.
//   IEntityHandle uses the root identity inherited through HunterArchetypeManager.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter.Archetypes.Ram
{
    public sealed class RamModuleManager : HunterArchetypeManager, IEntityHandle
    {
        private RamController Controller => (RamController)Rules;
        public static void Register(HunterArchetypeFactory factory)
            => factory.Register<RamConfig, RamModuleManager>((config, profile, random) => new RamController(config, profile));
        public override bool OwnsDecisionMotion => Controller.OwnsPursuit;
        public override bool HandlesContact => true;
        public override bool Move(float dt)
        {
            if (!Controller.OwnsPursuit || Shared.CatchActive || !Hunter.IsActive) return false;
            bool blocked = Driver.MoveCharge(Controller.Motion, Controller.Direction, dt, out Collider blocker);
            IRamBreakableHandle partition = blocker != null ? blocker.GetComponentInParent<IRamBreakableHandle>() : null;
            Controller.CommitMotion(Driver.Position, blocked, partition?.RamBreakableId ?? -1);
            return true;
        }
        public override bool TryContact(EntityId target, Vector3 normal, out HunterHit hit) => Controller.TryHit(target, normal, out hit);
        public override void BeforeCatch() { Controller.Won(); PublishFacts(); }
        public override void AfterPose(HunterTickResult result) => PublishFacts();
        public override void PublishFacts() { while (Controller.TakeFact(out RamFact fact)) Events.Publish(fact); }
    }
}
