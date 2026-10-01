// ============================================================================
// MimicModuleManager.cs
// ============================================================================
// PURPOSE:
//   Registers Mimic's disguise rules and routes its specialised touch contacts.
//   Bite facts are drained only after the root publishes accepted hit intent,
//   preserving Session's same-tick damage pairing and teardown notifications.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Mimic.
// KEY RESPONSIBILITIES:
//   - Register rules and route pose touch probes and normal-bearing hits.
//   - Publish pose/bite facts after contacts, motion, catch and teardown.
// DEPENDENCIES:
//   - Local Mimic rules/config, parent Hunter contracts/driver and Core facts.
// USAGE NOTES:
//   Scene-owned; factory creates the facet, root owns tick and teardown.
//   No subscriptions or independent state. Teardown clears the Controller's life data.
//   IEntityHandle uses the root identity inherited through HunterArchetypeManager.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter.Archetypes.Mimic
{
    public sealed class MimicModuleManager : HunterArchetypeManager, IEntityHandle
    {
        private MimicController Controller => (MimicController)Rules;
        public static void Register(HunterArchetypeFactory factory)
            => factory.Register<MimicConfig, MimicModuleManager>((config, profile, random) => new MimicController(config, random));
        public override bool HandlesContact => true;
        public override void AfterContact() => PublishFacts();
        public override bool TryContact(EntityId target, Vector3 normal, out HunterHit hit)
        {
            hit = default;
            if (!Controller.Touch(target, out HunterHit bite)) return false;
            hit = new HunterHit(bite.Hunter, bite.Target, bite.Damage, bite.Tick, bite.HunterPosition,
                bite.Reason, bite.Severity, bite.Source, normal); return true;
        }
        public override void AfterPose(HunterTickResult result)
        {
            if (Controller.Posed && !Shared.CatchActive) Driver.ProbeMimicTouch(Controller.TouchRadius);
            PublishFacts();
        }
        public override void BeforeCatch() { Controller.Won(); PublishFacts(); }
        public override void PublishFacts() { while (Controller.TakeFact(out MimicFact fact)) Events.Publish(fact); }
        public override void TeardownModule() { Controller.Teardown(); PublishFacts(); }
    }
}
