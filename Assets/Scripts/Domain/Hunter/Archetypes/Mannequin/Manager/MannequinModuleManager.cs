// ============================================================================
// MannequinModuleManager.cs
// ============================================================================
// PURPOSE:
//   Registers the light-independent Mannequin observation rules.
//   The distinctive accepted catch is published only after the shared catch gate,
//   while ordinary module facts keep their original end-of-tick ordering.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Mannequin.
// KEY RESPONSIBILITIES:
//   - Register rules and supply authored observation height.
//   - Publish silence and accepted-catch facts; retain inert legacy light entry points.
// DEPENDENCIES:
//   - Local Mannequin rules/config, parent Hunter contracts and Core facts.
// USAGE NOTES:
//   Scene-owned; factory creates the facet, root owns tick and teardown.
//   No subscriptions, independent state or engine calls.
//   IEntityHandle uses the root identity inherited through HunterArchetypeManager.
// ============================================================================
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Mannequin
{
    public sealed class MannequinModuleManager : HunterArchetypeManager, IEntityHandle
    {
        private MannequinController Controller => (MannequinController)Rules;
        public static void Register(HunterArchetypeFactory factory)
            => factory.Register<MannequinConfig, MannequinModuleManager>((config, profile, random) => new MannequinController(config, random));
        public override float ObservationHeight => ((MannequinConfig)Profile.ArchetypeRules).ObservationHeight;
        public override void FinishTick() => PublishFacts();
        public override void SetEffects(IReadOnlyActiveEffects effects) => Controller.SetEffects(effects);
        public override float BeginAfterglow(int roomId) => Controller.BeginAfterglow(roomId);
        public override void BeginCatch()
        { if (Controller.TryCatch()) Events.PublishMannequinCatch(Id, Hunter.Position, Hunter.Tick); }
        public override void PublishFacts() { while (Controller.TakeFact(out MannequinFact fact)) Events.Publish(fact); }
    }
}
