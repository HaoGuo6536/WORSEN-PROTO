// ============================================================================
// HunterArchetypeFactoryTests.cs
// ============================================================================
// PURPOSE:
//   Checks the complete built-in plug-in table and fail-closed registration.
//   Type resolution is managed-only; separate native cases prove real configs
//   construct fresh rules and reuse only the correct scene-owned module component.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Resolve every config and the null default to its rules and module.
//   - Reject unknown, missing and duplicate entries and admit explicit extensions.
// DEPENDENCIES:
//   - Hunter factory and archetypes, NUnit and Unity for native construction cases.
// USAGE NOTES:
//   No authored assets or loaded scenes are changed. Managed type cases do not
//   create ScriptableObjects or GameObjects and run in the offline harness.
// ============================================================================
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Default;
using Worsen.Domain.Hunter.Archetypes.Echo;
using Worsen.Domain.Hunter.Archetypes.Weaver;
using Worsen.Domain.Hunter.Archetypes.Ticking;
using Worsen.Domain.Hunter.Archetypes.Ram;
using Worsen.Domain.Hunter.Archetypes.Skip;
using Worsen.Domain.Hunter.Archetypes.Mimic;
using Worsen.Domain.Hunter.Archetypes.Blinder;
using Worsen.Domain.Hunter.Archetypes.Herald;
using Worsen.Domain.Hunter.Archetypes.Mannequin;
using Worsen.Domain.Hunter.Archetypes.Stare;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterArchetypeFactoryTests
    {
        public static IEnumerable Registrations
        {
            get
            {
                yield return new object[] { null, typeof(DefaultModuleManager), typeof(DefaultHunterController) };
                yield return new object[] { typeof(EchoConfig), typeof(EchoModuleManager), typeof(EchoController) };
                yield return new object[] { typeof(WeaverConfig), typeof(WeaverModuleManager), typeof(WeaverController) };
                yield return new object[] { typeof(TickingConfig), typeof(TickingManager), typeof(TickingController) };
                yield return new object[] { typeof(RamConfig), typeof(RamModuleManager), typeof(RamController) };
                yield return new object[] { typeof(SkipConfig), typeof(SkipModuleManager), typeof(SkipController) };
                yield return new object[] { typeof(MimicConfig), typeof(MimicModuleManager), typeof(MimicController) };
                yield return new object[] { typeof(BlinderConfig), typeof(BlinderModuleManager), typeof(BlinderController) };
                yield return new object[] { typeof(HeraldConfig), typeof(HeraldModuleManager), typeof(HeraldController) };
                yield return new object[] { typeof(MannequinConfig), typeof(MannequinModuleManager), typeof(MannequinController) };
                yield return new object[] { typeof(StareConfig), typeof(StareModuleManager), typeof(StareController) };
            }
        }
        [TestCaseSource(nameof(Registrations))]
        public void EveryConfigResolvesToItsModule(Type config, Type module, Type rules)
        {
            Assert.That(HunterArchetypeFactory.BuiltIn.ModuleType(config), Is.EqualTo(module));
            Assert.That(typeof(Worsen.Core.IEntityHandle).IsAssignableFrom(module), Is.True,
                "Every entity facet must expose its root's identity, including inherited implementations.");
        }
        [TestCaseSource(nameof(Registrations))]
        public void EveryConfigCreatesFreshRulesAndReusesItsComponent(Type config, Type module, Type rules)
        {
            var profile = ScriptableObject.CreateInstance<HunterProfile>();
            var asset = config == null ? null : (HunterArchetypeConfig)ScriptableObject.CreateInstance(config);
            var root = new GameObject("Hunter factory fixture"); root.SetActive(false);
            try
            {
                EchoControllerTests.Tune(profile, "_archetypeRules", asset);
                var factory = HunterArchetypeFactory.BuiltIn;
                var first = factory.CreateRules(profile, new System.Random(9));
                var second = factory.CreateRules(profile, new System.Random(9));
                Assert.That(first.GetType(), Is.EqualTo(rules)); Assert.That(second, Is.Not.SameAs(first));
                var component = factory.CreateModule(root, asset);
                Assert.That(component.GetType(), Is.EqualTo(module));
                Assert.That(factory.CreateModule(root, asset), Is.SameAs(component));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(profile); if (asset != null) Object.DestroyImmediate(asset); }
        }
        [Test] public void UnknownConfigFailsLoudlyInsteadOfFallingBack()
        {
            var error = Assert.Throws<ArgumentException>(() => HunterArchetypeFactory.BuiltIn.ModuleType(typeof(UnregisteredHunterConfig)));
            Assert.That(error.Message, Does.Contain(nameof(UnregisteredHunterConfig)));
        }
        [Test] public void EmptyFactoryHasNoImplicitDefaultOrKnownConfig()
        {
            var factory = new HunterArchetypeFactory();
            Assert.Throws<ArgumentException>(() => factory.ModuleType(null));
            Assert.Throws<ArgumentException>(() => factory.ModuleType(typeof(EchoConfig)));
        }
        [Test] public void DuplicateRegistrationsFailAndIndependentTablesDoNotLeak()
        {
            var factory = new HunterArchetypeFactory(); DefaultModuleManager.Register(factory); EchoModuleManager.Register(factory);
            Assert.Throws<InvalidOperationException>(() => DefaultModuleManager.Register(factory));
            Assert.Throws<ArgumentException>(() => EchoModuleManager.Register(factory));
            Assert.Throws<ArgumentException>(() => new HunterArchetypeFactory().ModuleType(typeof(EchoConfig)));
        }
        [Test] public void ExtensionNeedsOnlyAnExplicitRegistration()
        {
            var factory = new HunterArchetypeFactory();
            factory.Register<UnregisteredHunterConfig, DefaultModuleManager>((config, profile, random) => new HunterArchetypeController());
            Assert.That(factory.ModuleType(typeof(UnregisteredHunterConfig)), Is.EqualTo(typeof(DefaultModuleManager)));
            Assert.Throws<ArgumentException>(() => HunterArchetypeFactory.BuiltIn.ModuleType(typeof(UnregisteredHunterConfig)));
        }
    }
    public sealed class UnregisteredHunterConfig : HunterArchetypeConfig { }
}
