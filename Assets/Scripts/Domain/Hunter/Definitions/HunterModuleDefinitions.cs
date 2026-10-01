// ============================================================================
// HunterModuleDefinitions.cs
// ============================================================================
// PURPOSE:
//   Defines the lifecycle seam between the shared Hunter and an installed module.
//   Modules own their rule construction and specialised driver commands. The
//   shared tick preserves ordering without knowing any concrete archetype.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Describe module lifecycle, movement and contact hooks.
//   - Expose optional external inputs and typed outward fact delivery.
//   - Keep clock events and placement drivers behind shared contracts.
// DEPENDENCIES:
//   - Core values, Hunter contracts and the injected Player read-only view.
// USAGE NOTES:
//   Interfaces contain no implementation. Fact delivery is synchronous; modules
//   are scene-owned and factories reuse their component while replacing life state.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public interface IHunterDormancyRules { bool Dormant { get; } }
    public interface IHunterIndependentAttackRules { bool AllowSharedAttack { get; } }
    public interface IHunterLegacyRules { bool AllowsLegacyTraits { get; } }
    public interface IHunterTickingModule
    {
        event Action<TickingSoundFact> OnSound;
        event Action<TickingGuidanceFact> OnGuidance;
        event Action<TickingNoiseFact> OnNoise;
        void PublishTick();
    }
    public interface IHunterPlacementDriver
    {
        void Initialize();
        void SetPresent(bool present);
        bool Probe(Vector3 candidate, Vector3 player, HunterPlayerView view, HunterMotorDriverConfig config, out Vector3 point);
        void Teardown();
    }
    public interface IHunterArchetypeModule
    {
        IHunterArchetypeController Rules { get; }
        IHunterTickingModule Ticking { get; }
        float ObservationHeight { get; }
        bool Hold { get; }
        bool OwnsDecisionMotion { get; }
        void InitializeModule(IHunterArchetypeController rules, HunterProfile profile, HunterDriver driver,
            HunterController shared, IReadOnlyHunterState state, IReadOnlyPlayerState player, IHunterModuleEvents events);
        bool PrepareTick(float dt, long tick);
        void AfterSensing(HunterTickResult result);
        void AfterReaction(HunterTickResult result);
        bool Move(float dt);
        void AfterPose(HunterTickResult result);
        void PublishFacts();
        void FinishTick();
        bool HandlesContact { get; }
        bool TryContact(EntityId target, Vector3 normal, out HunterHit hit);
        void AfterContact();
        void BeforeCatch();
        void BeginCatch();
        void SetEffects(IReadOnlyActiveEffects effects);
        bool BeginFloor(long generation);
        bool RecordUse(SkipTraversalUse use);
        float BeginAfterglow(int roomId);
        void ReportTrapTick(Vector3 position, long tick);
        bool TryGetTrapPolicy(out BlinderTrapPolicyFact fact);
        void TeardownModule();
    }
    public interface IHunterModuleEvents
    {
        void Publish(WebHitFact fact);
        void Publish(WeaverFact fact);
        void Publish(RamFact fact);
        void Publish(SkipFact fact);
        void Publish(MimicFact fact);
        void Publish(BlinderHitFact fact);
        void Publish(BlinderThrowFact fact);
        void Publish(BlinderSoundFact fact);
        void Publish(BlinderTrapPolicyFact fact);
        void Publish(HeraldScreamFact fact);
        void Publish(HeraldBreathFact fact);
        void Publish(HeraldDeafenFact fact);
        void Publish(MannequinFact fact);
        void Publish(StareFact fact);
        void PublishMannequinCatch(EntityId hunter, Vector3 position, long tick);
    }
}
