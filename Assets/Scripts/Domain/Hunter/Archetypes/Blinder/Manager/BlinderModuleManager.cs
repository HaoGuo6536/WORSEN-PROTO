// ============================================================================
// BlinderModuleManager.cs
// ============================================================================
// PURPOSE:
//   Wires Blinder's independent weapon to shared sweep presentation and facts.
//   Probe, launch, trap-policy and accepted-catch ordering stays local to this
//   plug-in rather than adding another concrete branch to HunterManager.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Blinder.
// KEY RESPONSIBILITIES:
//   - Register rules and validate the authored sweep configuration.
//   - Resolve projectile contacts and route probes, launches and throw poses.
//   - Forward effects, trap observations and typed outward facts.
// DEPENDENCIES:
//   - Local Blinder rules/config, parent Hunter contracts/driver and Core values.
// USAGE NOTES:
//   Scene-owned; factory creates the facet, root owns tick and teardown.
//   No subscriptions or independent time/random source; shared driver owns effects.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Blinder
{
    public sealed class BlinderModuleManager : HunterArchetypeManager
    {
        private BlinderController Controller => (BlinderController)Rules;
        private BlinderConfig Config => (BlinderConfig)Profile.ArchetypeRules;
        public static void Register(HunterArchetypeFactory factory)
            => factory.Register<BlinderConfig, BlinderModuleManager>((config, profile, random) =>
                new BlinderController(new BlinderBehaviorState(), config, profile));
        protected override void Configure()
        {
            if (Config.SweepConfig == null) throw new ArgumentException("Build Blinder profile and sweep config first.");
            Driver.ConfigureWeaver(Config.SweepConfig);
        }
        public override bool Hold => Controller.Hold;
        public override HunterAnimationPhase AnimationPhase => Controller.Warning ? HunterAnimationPhase.Ready : HunterAnimationPhase.None;
        public override bool PrepareTick(float dt, long tick)
        {
            if (tick <= Controller.LastTick) return false;
            foreach (var contact in Driver.TickWebs(dt))
            {
                IEntityHandle handle = contact.Key.GetComponentInParent<IEntityHandle>();
                if (handle != null && Controller.TryHit(handle.Id, contact.Value, tick, out BlinderHitFact hit)) Events.Publish(hit);
            }
            Controller.Observe(Driver.ProbeWeaver(Controller.Aim(Driver.WeaverShotHeight), Controller.Radius, Config.Range, tick));
            return true;
        }
        public override void AfterSensing(HunterTickResult result)
        {
            if (result.Phase != HunterLungePhase.None || Shared.CatchActive || Shared.PursuitSuppressed) Controller.SuspendAttack();
            if (Controller.Fire) Controller.CommitLaunch(Driver.LaunchWeb(Controller.Origin, Controller.Target,
                Controller.Radius, Config.ProjectileSpeed, Config.Range, Controller.Serial + 1));
        }
        public override void FinishTick() => PublishFacts();
        public override void PublishFacts()
        {
            while (Controller.TakeSound(out BlinderSoundFact sound)) Events.Publish(sound);
            while (Controller.TakeTrapPolicy(out BlinderTrapPolicyFact policy)) Events.Publish(policy);
            while (Controller.TakeThrow(out BlinderThrowFact shot))
            { Driver.TriggerAnimation(HunterAnimationPhase.Attack); Events.Publish(shot); }
        }
        public override void BeginCatch() { Controller.BeginCatch(); PublishFacts(); }
        public override void SetEffects(IReadOnlyActiveEffects effects) => Controller.SetEffects(effects);
        public override void ReportTrapTick(Vector3 position, long tick) { Controller.ReportTrapTick(position, tick); PublishFacts(); }
        public override bool TryGetTrapPolicy(out BlinderTrapPolicyFact fact) { fact = Controller.TrapPolicy; return true; }
    }
}
