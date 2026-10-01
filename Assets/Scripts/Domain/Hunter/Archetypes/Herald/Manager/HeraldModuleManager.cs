// ============================================================================
// HeraldModuleManager.cs
// ============================================================================
// PURPOSE:
//   Registers Herald rules and resolves its attack after shared sensing.
//   Sound, breath and deafen queues are delivered at the original end-of-tick
//   boundary while the shared root knows only the module interface.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Herald.
// KEY RESPONSIBILITIES:
//   - Register per-life rules and preserve duplicate-tick admission.
//   - Sequence post-sense resolution, catch suspension and outward facts.
// DEPENDENCIES:
//   - Local Herald rules/config, parent Hunter contracts and Core facts.
// USAGE NOTES:
//   Scene-owned; factory creates the facet, root owns tick and teardown.
//   No subscriptions, independent state or engine operations.
// ============================================================================
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Herald
{
    public sealed class HeraldModuleManager : HunterArchetypeManager
    {
        private HeraldController Controller => (HeraldController)Rules;
        public static void Register(HunterArchetypeFactory factory)
            => factory.Register<HeraldConfig, HeraldModuleManager>((config, profile, random) =>
                new HeraldController(new HeraldBehaviorState(), config, random));
        public override bool Hold => Controller.Hold;
        public override bool PrepareTick(float dt, long tick) => tick > Controller.LastTick;
        public override void AfterSensing(HunterTickResult result) => Controller.ResolveAfterSensing();
        public override void BeginCatch() => Controller.SuspendAttack();
        public override void FinishTick() => PublishFacts();
        public override void PublishFacts()
        {
            while (Controller.TakeScream(out HeraldScreamFact scream)) Events.Publish(scream);
            while (Controller.TakeBreath(out HeraldBreathFact breath)) Events.Publish(breath);
            while (Controller.TakeHit(out HeraldDeafenFact hit)) Events.Publish(hit);
        }
    }
}
