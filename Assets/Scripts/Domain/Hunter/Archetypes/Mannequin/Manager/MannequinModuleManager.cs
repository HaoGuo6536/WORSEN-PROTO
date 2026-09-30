// ============================================================================
// MannequinModuleManager.cs
// ============================================================================
// PURPOSE:
//   Registers Mannequin observation rules and forwards its light-safety inputs.
//   The distinctive accepted catch is published only after the shared catch gate,
//   while ordinary module facts keep their original end-of-tick ordering.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Mannequin.
// KEY RESPONSIBILITIES:
//   - Register rules and supply authored observation height.
//   - Forward effects/afterglow and publish light and accepted-catch facts.
// DEPENDENCIES:
//   - Local Mannequin rules/config, parent Hunter contracts and Core facts.
// USAGE NOTES:
//   Scene-owned; factory creates the facet, root owns tick and teardown.
//   No subscriptions, independent state or engine calls.
// ============================================================================
using Worsen.Core;
namespace Worsen.Domain.Hunter.Archetypes.Mannequin
{
    public sealed class MannequinModuleManager : HunterArchetypeManager
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
