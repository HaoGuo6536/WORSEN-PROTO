// ============================================================================
// HunterArchetypeRegistry.cs
// ============================================================================
// PURPOSE:
//   Lists the built-in Hunter plug-ins at one explicit composition boundary.
//   Adding a module requires its own folder and one registration line here, not
//   edits to the shared tick, factory dispatch or another archetype's rules.
// ARCHITECTURAL ROLE:
//   Registry (§8) · Domain · Hunter archetype composition.
// KEY RESPONSIBILITIES:
//   - Populate the factory with the approved built-in module factories.
// DEPENDENCIES:
//   - Parent Hunter factory and each archetype's registration entry point.
// USAGE NOTES:
//   No discovery, reflection, engine calls or mutable global subscriptions.
//   This composition table is the only code that enumerates concrete archetypes.
// ============================================================================
namespace Worsen.Domain.Hunter.Archetypes
{
    public static class HunterArchetypeRegistry
    {
        public static HunterArchetypeFactory Build()
        {
            var factory = new HunterArchetypeFactory();
            Default.DefaultModuleManager.Register(factory);
            Echo.EchoModuleManager.Register(factory);
            Weaver.WeaverModuleManager.Register(factory);
            Ticking.TickingManager.Register(factory);
            Ram.RamModuleManager.Register(factory);
            Skip.SkipModuleManager.Register(factory);
            Mimic.MimicModuleManager.Register(factory);
            Blinder.BlinderModuleManager.Register(factory);
            Herald.HeraldModuleManager.Register(factory);
            Mannequin.MannequinModuleManager.Register(factory);
            Stare.StareModuleManager.Register(factory);
            return factory;
        }
    }
}
