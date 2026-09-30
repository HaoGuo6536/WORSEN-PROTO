// ============================================================================
// HunterRosterScopeTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that one type-wide curse snapshot reaches every retained instance.
//   Real modules expose changed rule values without sharing mutable life state.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Cover all ten modules, duplicate indices and removal of type-wide effects.
//   - Check that a foreign type's curse cannot change the selected rule.
// DEPENDENCIES:
//   - Hunter modules, Core effects, injected Player/Level fixtures and NUnit.
// USAGE NOTES:
//   No scene queries. Temporary configs retain their authored defaults.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
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
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterRosterScopeTests
    {
        [TestCase("echo", "echo-shorter-delay")]
        [TestCase("weaver", "weaver-stickier-webs")]
        [TestCase("ticking", "ticking-runs-faster")]
        [TestCase("ram", "ram-longer-charge")]
        [TestCase("skip", "skip-quicker-learner")]
        [TestCase("mimic", "mimic-faithless-arrow")]
        [TestCase("blinder", "blinder-longer-dark")]
        [TestCase("herald", "herald-wider-scream")]
        [TestCase("mannequin", "mannequin-longer-strides")]
        [TestCase("stare", "stare-shorter-window")]
        public void EveryDuplicateConsumesAndRemovesTheSameTypeCurse(string key, string effect)
        {
            var profile = ScriptableObject.CreateInstance<HunterProfile>();
            var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8 };
            var world = new EchoControllerTests.World();
            var effects = new ActiveEffects(new[] { new ActiveEffect(new EffectId(effect), EffectKind.Curse, 2) });
            float? expected = null;
            try
            {
                for (int duplicate = 0; duplicate < 4; duplicate++)
                {
                    HunterArchetypeConfig config = null;
                    try
                    {
                        IHunterArchetypeController module = Create(key, profile, out config, out Func<float> value);
                        var state = new HunterBehaviorState();
                        var shared = new HunterController(state, profile, new System.Random(17), player, world, module);
                        shared.Reset(new EntityId(-1 - duplicate), Vector3.zero, Vector3.forward);
                        typeof(HunterBehaviorState).GetProperty("DuplicateIndex").SetValue(state, duplicate);
                        shared.Tick(default, .1f, 1); float neutral = value();
                        shared.SetActiveEffects(effects); shared.Tick(default, .1f, 2);
                        float cursed = value(); Assert.That(cursed, Is.Not.EqualTo(neutral), key);
                        if (expected.HasValue) Assert.That(cursed, Is.EqualTo(expected.Value), "duplicate " + duplicate);
                        expected = cursed;
                        shared.SetActiveEffects(default(ActiveEffects)); shared.Tick(default, .1f, 3);
                        Assert.That(value(), Is.EqualTo(neutral));
                        string foreign = key == "echo" ? "stare-shorter-window" : "echo-shorter-delay";
                        shared.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(new EffectId(foreign), EffectKind.Curse, 3) }));
                        shared.Tick(default, .1f, 4); Assert.That(value(), Is.EqualTo(neutral));
                    }
                    finally { if (config != null) UnityEngine.Object.DestroyImmediate(config); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }
        private static IHunterArchetypeController Create(string key, HunterProfile profile,
            out HunterArchetypeConfig config, out Func<float> value)
        {
            var random = new System.Random(19);
            switch (key)
            {
                case "echo":
                    var echo = ScriptableObject.CreateInstance<EchoConfig>(); config = echo;
                    var e = new EchoController(echo); value = () => e.EffectiveDelay; return e;
                case "weaver":
                    var weaver = ScriptableObject.CreateInstance<WeaverConfig>(); config = weaver;
                    var w = new WeaverController(new WeaverBehaviorState(), weaver, profile, random); value = () => w.SlowSeconds; return w;
                case "ticking":
                    var ticking = ScriptableObject.CreateInstance<TickingConfig>(); config = ticking;
                    var t = new TickingController(ticking, random); value = () => t.EffectiveSpringSeconds; return t;
                case "ram":
                    var ram = ScriptableObject.CreateInstance<RamConfig>(); config = ram;
                    var r = new RamController(ram, profile); value = () => r.ChargeDistance; return r;
                case "skip":
                    var skip = ScriptableObject.CreateInstance<SkipConfig>(); config = skip;
                    var s = new SkipController(skip, profile, random); value = () => s.Threshold; return s;
                case "mimic":
                    var mimic = ScriptableObject.CreateInstance<MimicConfig>(); config = mimic;
                    var m = new MimicController(mimic, random); value = () => m.FaithlessEnabled ? 1f : 0f; return m;
                case "blinder":
                    var blinder = ScriptableObject.CreateInstance<BlinderConfig>(); config = blinder;
                    var b = new BlinderController(new BlinderBehaviorState(), blinder, profile); value = () => b.TrapPolicy.Duration; return b;
                case "herald":
                    var herald = ScriptableObject.CreateInstance<HeraldConfig>(); config = herald;
                    var h = new HeraldController(new HeraldBehaviorState(), herald, random); value = () => h.Radius; return h;
                case "mannequin":
                    var mannequin = ScriptableObject.CreateInstance<MannequinConfig>(); config = mannequin;
                    var n = new MannequinController(mannequin, random); value = () => n.SpeedMultiplier; return n;
                case "stare":
                    var stare = ScriptableObject.CreateInstance<StareConfig>(); config = stare;
                    var a = new StareController(stare, random); value = () => a.WindowSeconds; return a;
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }
    }
}
