// ============================================================================
// HunterArchetypeFactory.cs
// ============================================================================
// PURPOSE:
//   Resolves Hunter configs through an explicit, fail-closed registration table.
//   Rule construction is available without engine objects; component creation is
//   kept here so HunterManager never constructs another Manager or type-switches.
// ARCHITECTURAL ROLE:
//   Factory (§1c) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Register exact config types and reject duplicate or unknown registrations.
//   - Construct per-life rules and create or reuse the corresponding module facet.
// DEPENDENCIES:
//   - Hunter contracts and the archetype composition registry only.
// USAGE NOTES:
//   Scene-owned module components share their entity root and are not pooled
//   independently. InitializeModule replaces life references after root reset.
//   The null config has its own default entry, never an unknown-config fallback.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterArchetypeFactory
    {
        private readonly Dictionary<Type, Registration> _registrations = new Dictionary<Type, Registration>();
        private Registration _default;
        private sealed class Registration
        {
            public Type ModuleType;
            public Func<HunterArchetypeConfig, HunterProfile, System.Random, IHunterArchetypeController> Rules;
            public Func<GameObject, IHunterArchetypeModule> Module;
        }
        public static HunterArchetypeFactory BuiltIn { get; } = Archetypes.HunterArchetypeRegistry.Build();
        public void Register<TConfig, TModule>(Func<TConfig, HunterProfile, System.Random, IHunterArchetypeController> create)
            where TConfig : HunterArchetypeConfig where TModule : Component, IHunterArchetypeModule
        {
            if (create == null) throw new ArgumentNullException(nameof(create));
            _registrations.Add(typeof(TConfig), Entry<TModule>((config, profile, random) => create((TConfig)config, profile, random)));
        }
        public void RegisterDefault<TModule>(Func<HunterProfile, System.Random, IHunterArchetypeController> create)
            where TModule : Component, IHunterArchetypeModule
        {
            if (create == null) throw new ArgumentNullException(nameof(create));
            if (_default != null) throw new InvalidOperationException("Default Hunter module already registered.");
            _default = Entry<TModule>((config, profile, random) => create(profile, random));
        }
        private static Registration Entry<TModule>(Func<HunterArchetypeConfig, HunterProfile, System.Random, IHunterArchetypeController> create)
            where TModule : Component, IHunterArchetypeModule
            => new Registration { ModuleType = typeof(TModule), Rules = create, Module = root =>
            {
                TModule module = root.GetComponent<TModule>();
                return module != null ? module : root.AddComponent<TModule>();
            } };
        private Registration Resolve(Type configType)
        {
            if (configType == null && _default != null) return _default;
            if (configType != null && _registrations.TryGetValue(configType, out Registration entry)) return entry;
            throw new ArgumentException("Unregistered Hunter rules config: " + (configType?.FullName ?? "<default>"));
        }
        public Type ModuleType(Type configType) => Resolve(configType).ModuleType;
        public IHunterArchetypeController CreateRules(HunterProfile profile, System.Random random)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (random == null) throw new ArgumentNullException(nameof(random));
            HunterArchetypeConfig config = profile.ArchetypeRules;
            return Resolve(config != null ? config.GetType() : null).Rules(config, profile, random);
        }
        public IHunterArchetypeModule CreateModule(GameObject root, HunterArchetypeConfig config)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            return Resolve(config != null ? config.GetType() : null).Module(root);
        }
    }
}
