// ============================================================================
// WeaverModuleManager.cs
// ============================================================================
// PURPOSE:
//   Wires Weaver rules to the parent-owned swept-shot driver and outward facts.
//   Its hooks preserve the original pre-sense probe, post-reaction launch and
//   post-motion ceiling/nest ordering without exposing Weaver types to the root.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Weaver.
// KEY RESPONSIBILITIES:
//   - Register and configure per-life Weaver rules and shared sweep presentation.
//   - Resolve projectile contacts and sequence probes, launches and ceiling commands.
//   - Publish nest and hit facts at their original tick boundaries.
// DEPENDENCIES:
//   - Local Weaver rules/config, parent Hunter contracts/driver and Core identities.
// USAGE NOTES:
//   Scene-owned; factory creates the facet, root owns tick and teardown.
//   No event subscriptions or independent timers; random source comes from the factory.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Weaver
{
    public sealed class WeaverModuleManager : HunterArchetypeManager
    {
        private WeaverController Controller => (WeaverController)Rules;
        private WeaverConfig Config => (WeaverConfig)Profile.ArchetypeRules;
        public static void Register(HunterArchetypeFactory factory)
            => factory.Register<WeaverConfig, WeaverModuleManager>((config, profile, random) =>
                new WeaverController(new WeaverBehaviorState(), config, profile, random));
        protected override void Configure() => Driver.ConfigureWeaver(Config.DriverConfig);
        public override bool Hold => Controller.Hold;
        public override bool PrepareTick(float dt, long tick)
        {
            if (tick <= Controller.LastTick) return false;
            foreach (var contact in Driver.TickWebs(dt))
            {
                IEntityHandle handle = contact.Key.GetComponentInParent<IEntityHandle>();
                if (handle != null && Controller.TryHit(handle.Id, contact.Value, tick, out WebHitFact hit)) Events.Publish(hit);
            }
            Controller.Observe(Driver.ProbeWeaver(Controller.Aim(Driver.WeaverShotHeight),
                Controller.Warning ? Controller.WarnedRadius : Controller.Radius, Config.ShotRange, tick));
            return true;
        }
        public override void AfterReaction(HunterTickResult result)
        {
            if (result.HoldPosition || result.Phase != HunterLungePhase.None || Shared.CatchActive || Shared.PursuitSuppressed) Controller.SuspendAttack();
            Driver.SetWeaverCeiling(Controller.CeilingHeight, Controller.Ceiling && result.Phase == HunterLungePhase.None && !Shared.CatchActive);
            if (Controller.Fire) Controller.CommitLaunch(Driver.LaunchWeb(Controller.WarnedOrigin, Controller.WarnedTarget,
                Controller.WarnedRadius, Config.ProjectileSpeed, Config.ShotRange, Controller.Serial + 1));
        }
        public override void AfterPose(HunterTickResult result)
        {
            Driver.SetWeaverCeiling(Controller.CeilingHeight, Controller.Ceiling && result.Phase == HunterLungePhase.None && !Shared.CatchActive);
            while (Controller.TryTakeWeaverFact(out WeaverFact fact)) { Driver.AddWeaverNest(fact); Events.Publish(fact); }
        }
        public override void BeginCatch()
        { Controller.SuspendAttack(); Driver.SetWeaverCeiling(Controller.CeilingHeight, false); }
    }
}
