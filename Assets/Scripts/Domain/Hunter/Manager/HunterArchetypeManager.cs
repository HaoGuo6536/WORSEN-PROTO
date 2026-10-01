// ============================================================================
// HunterArchetypeManager.cs
// ============================================================================
// PURPOSE:
//   Provides the lifecycle adapter shared by scene-owned Hunter plug-ins.
//   The root sequences this interface at fixed points in its tick; each module
//   overrides only the specialised wiring it needs and keeps rules in Controllers.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Retain injected controller, driver, read-only views and publication ports.
//   - Supply neutral lifecycle hooks for modules with no specialised engine work.
// DEPENDENCIES:
//   - Hunter stack, Core values and injected Player read-only state.
// USAGE NOTES:
//   Scene-owned; factory creates components, root owns tick and teardown. This
//   adapter has no Update or subscriptions and owns no independent gameplay data.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public abstract class HunterArchetypeManager : MonoBehaviour, IHunterArchetypeModule, IEntityHandle
    {
        public IHunterArchetypeController Rules { get; private set; }
        protected HunterProfile Profile { get; private set; }
        protected HunterDriver Driver { get; private set; }
        protected HunterController Shared { get; private set; }
        protected IReadOnlyHunterState Hunter { get; private set; }
        protected IReadOnlyPlayerState Player { get; private set; }
        protected IHunterModuleEvents Events { get; private set; }
        public virtual EntityId Id => Hunter?.Id ?? EntityId.None;
        public virtual IHunterTickingModule Ticking => null;
        public virtual float ObservationHeight => 0f;
        public virtual bool Hold => false;
        public virtual bool OwnsDecisionMotion => false;
        public virtual bool HandlesContact => false;
        public void InitializeModule(IHunterArchetypeController rules, HunterProfile profile, HunterDriver driver,
            HunterController shared, IReadOnlyHunterState state, IReadOnlyPlayerState player, IHunterModuleEvents events)
        {
            Rules = rules; Profile = profile; Driver = driver; Shared = shared;
            Hunter = state; Player = player; Events = events; Configure();
        }
        protected virtual void Configure() { }
        public virtual bool PrepareTick(float dt, long tick) => true;
        public virtual void AfterSensing(HunterTickResult result) { }
        public virtual void AfterReaction(HunterTickResult result) { }
        public virtual bool Move(float dt) => false;
        public virtual void AfterPose(HunterTickResult result) { }
        public virtual void PublishFacts() { }
        public virtual void FinishTick() { }
        public virtual bool TryContact(EntityId target, Vector3 normal, out HunterHit hit) { hit = default; return false; }
        public virtual void BeforeCatch() { }
        public virtual void AfterContact() { }
        public virtual void BeginCatch() { }
        public virtual void SetEffects(IReadOnlyActiveEffects effects) { }
        public virtual bool BeginFloor(long generation) => false;
        public virtual bool RecordUse(SkipTraversalUse use) => false;
        public virtual float BeginAfterglow(int roomId) => 0f;
        public virtual void ReportTrapTick(Vector3 position, long tick) { }
        public virtual bool TryGetTrapPolicy(out BlinderTrapPolicyFact fact) { fact = default; return false; }
        public virtual void TeardownModule() { }
    }
}
