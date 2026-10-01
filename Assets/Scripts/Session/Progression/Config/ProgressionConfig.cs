// ============================================================================
// ProgressionConfig.cs
// ============================================================================
// PURPOSE:
//   Defines the approved run roster and independent selection/shop cadences.
//   Designers can bind the shop, tune curse economy bonuses and effect traits
//   without changing wallet, selection or round-transition code.
// ARCHITECTURAL ROLE:
//   Config (§4) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Tune progression events and bind immutable Hunter profiles for mutation-pool reads.
//   - Delegate offers and economy tuning to EffectCatalogueConfig and ShopConfig.
//   - Optionally bind provisional shrine rules; unbound assets use documented defaults.
//   - Tune curse economy multipliers and independent selection/shop clocks.
//   - Describe run hunters and a small catalogue-free general-curse fallback.
// DEPENDENCIES:
//   - Domain Hunter profiles supply archetype mutation data, never runtime entity state.
//   - Unity serialization, Core traits, catalogue and the delegated ShopConfig.
// USAGE NOTES:
//   Mirrored asset: ScriptableObjects/Session/Progression/ProgressionConfig.
//   Runtime code only reads this asset. Counts, purchases and health live in State.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression.Shop;
using Worsen.Domain.Hunter;

namespace Worsen.Session.Progression
{
    [Serializable]
    public sealed class ProgressionEntryConfig
    {
        [SerializeField] private string _id;
        [SerializeField] private string _title;
        [SerializeField, TextArea] private string _description;
        [SerializeField, TextArea] private string _descriptionAtThreatCap;
        [SerializeField, Min(0)] private int _price;
        [SerializeField, Min(0.01f)] private float _movementSpeedMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float _hunterSpeedMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float _fogDensityMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float _flashlightRangeMultiplier = 1f;
        [SerializeField] private float _maximumHealthDelta;
        [SerializeField, Min(0f)] private float _healing;
        [SerializeField] private ProgressionTraits _traits;
        [SerializeField] private bool _repeatable;
        [SerializeField, Min(1)] private int _stockPerVisit = 1;
        [SerializeField] private bool _grantsWaxWard;
        [SerializeField] private string _requiredThreatId;

        public ProgressionEntryConfig(string id, string title, string description, int price = 0,
            float movementSpeedMultiplier = 1f, float hunterSpeedMultiplier = 1f,
            float fogDensityMultiplier = 1f, float flashlightRangeMultiplier = 1f,
            float maximumHealthDelta = 0f, float healing = 0f, string descriptionAtThreatCap = null,
            ProgressionTraits traits = ProgressionTraits.None, bool repeatable = false, int stockPerVisit = 1,
            bool grantsWaxWard = false, string requiredThreatId = null)
        {
            _id = id; _title = title; _description = description; _price = price;
            _movementSpeedMultiplier = movementSpeedMultiplier; _hunterSpeedMultiplier = hunterSpeedMultiplier;
            _fogDensityMultiplier = fogDensityMultiplier; _flashlightRangeMultiplier = flashlightRangeMultiplier;
            _maximumHealthDelta = maximumHealthDelta; _healing = healing;
            _descriptionAtThreatCap = descriptionAtThreatCap;
            _traits = traits; _repeatable = repeatable; _stockPerVisit = stockPerVisit; _grantsWaxWard = grantsWaxWard; _requiredThreatId = requiredThreatId;
        }
        public string Id => _id;
        public string Title => _title;
        public string Description => _description;
        public string DescriptionAtThreatCap => _descriptionAtThreatCap;
        public int Price => _price;
        public float MovementSpeedMultiplier => _movementSpeedMultiplier;
        public float HunterSpeedMultiplier => _hunterSpeedMultiplier;
        public float FogDensityMultiplier => _fogDensityMultiplier;
        public float FlashlightRangeMultiplier => _flashlightRangeMultiplier;
        public float MaximumHealthDelta => _maximumHealthDelta;
        public float Healing => _healing;
        public ProgressionTraits Traits => _traits;
        public bool Repeatable => _repeatable;
        public int StockPerVisit => _stockPerVisit;
        public bool GrantsWaxWard => _grantsWaxWard;
        public string RequiredThreatId => _requiredThreatId;
    }

    [CreateAssetMenu(menuName = "Worsen/Progression/Progression Config")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        public const float DefaultNothingShopPriceMultiplier = 0.85f;
        [SerializeField] private EffectCatalogueConfig _effectCatalogue = null;
        [SerializeField] private ShopConfig _shopConfig = null;
        [SerializeField] private ShrineProgressionConfig _shrineConfig = null;
        [Header("Progression events (provisional)")]
        [SerializeField, Min(8)] private int _firstEventRound = 8;
        [SerializeField, Min(2)] private int _eventInterval = 8;
        [SerializeField, Min(0)] private int _eventJitter = 1;
        [SerializeField, Min(1)] private int _eventHazardFloors = 3;
        [SerializeField, Min(0.01f)] private float _maximumMutationSpeedMultiplier = 4f;
        [SerializeField] private ProgressionEventKind[] _eventPool = {
            ProgressionEventKind.EnvironmentalHazard, ProgressionEventKind.HunterUpgrade,
            ProgressionEventKind.ExtraHunter, ProgressionEventKind.Random, ProgressionEventKind.HiddenMutation };
        [SerializeField] private HunterProfile[] _mutationProfiles = Array.Empty<HunterProfile>();
        public int FirstEventRound => _firstEventRound;
        public int EventInterval => _eventInterval;
        public int EventJitter => _eventJitter;
        public int EventHazardFloors => _eventHazardFloors;
        public float MaximumMutationSpeedMultiplier => _maximumMutationSpeedMultiplier;
        public IReadOnlyList<ProgressionEventKind> EventPool => Array.AsReadOnly(_eventPool ?? Array.Empty<ProgressionEventKind>());
        public IReadOnlyList<HunterProfile> MutationProfiles => Array.AsReadOnly(_mutationProfiles ?? Array.Empty<HunterProfile>());
        [Header("Selection and shops")]
        [SerializeField, Min(1)] private int _selectionInterval = 2;
        [SerializeField, Min(2)] private int _shopInterval = 2;
        [SerializeField, Min(1)] private int _maximumActiveThreats = 5;
        [SerializeField, Min(1)] private int _goldenCakeValue = 1;
        [Header("Curse economy (provisional)")]
        [SerializeField, Min(1f)] private float _fasterCollapseGoldenCakeMultiplier = 1.15f;
        [SerializeField, Range(0f, 1f)] private float _nothingShopPriceMultiplier = DefaultNothingShopPriceMultiplier;
        [SerializeField, Min(1f)] private float _initialMaximumHealth = 100f;
        [SerializeField, Min(1f)] private float _minimumMaximumHealth = 30f;
        [SerializeField, Min(1f)] private float _maximumMaximumHealth = 200f;
        [SerializeField, Min(0.01f)] private float _minimumMultiplier = 0.4f;
        [SerializeField, Min(1f)] private float _maximumMovementMultiplier = 1.6f;
        [SerializeField, Min(1f)] private float _maximumHunterMultiplier = 1.5f;
        [SerializeField, Min(1f)] private float _maximumFogMultiplier = 2f;
        [SerializeField, Min(1f)] private float _maximumFlashlightMultiplier = 2f;
        [SerializeField] private ProgressionEntryConfig[] _threats =
        {
            new ProgressionEntryConfig("echo", "THE ECHO", "Removes safe backtracking by replaying your path."),
            new ProgressionEntryConfig("weaver", "THE WEAVER", "Adds a ceiling hunter whose webs block safe routes."),
            new ProgressionEntryConfig("ticking", "THE TICKING", "Adds a moving clock; collect keys before it winds down."),
            new ProgressionEntryConfig("ram", "THE RAM", "Adds a charging hunter that commits to a straight line."),
            new ProgressionEntryConfig("mannequin", "THE MANNEQUIN", "Adds a hunter that moves whenever unseen and freezes while observed."),
            new ProgressionEntryConfig("mimic", "THE MIMIC", "Adds false cakes that punish careless collection."),
            new ProgressionEntryConfig("blinder", "THE BLINDER", "Adds traps and projectiles that remove vision."),
            new ProgressionEntryConfig("skip", "THE SKIP", "Adds a hunter that intercepts reused routes."),
            new ProgressionEntryConfig("herald", "THE HERALD", "Adds a screaming hunter that exposes your position."),
            new ProgressionEntryConfig("stare", "THE STARE", "Adds a threat you must find and stare down.")
        };
        [SerializeField] private ProgressionEntryConfig[] _curses =
        {
            new ProgressionEntryConfig("no-look-back", "NO LOOK-BACK", "Removes the look-back snap."),
            new ProgressionEntryConfig("hidden-count", "HIDDEN COUNT", "Hides the cake counter during a floor."),
            new ProgressionEntryConfig("short-grace", "SHORT GRACE", "Shortens the grace window after a hit.")
        };
        // Retained for serialized/source compatibility, never used to draw or purchase offers.
        [SerializeField] private ProgressionEntryConfig[] _offers = Array.Empty<ProgressionEntryConfig>();
        public EffectCatalogueConfig EffectCatalogue => _effectCatalogue;
        public ShopConfig ShopConfig => _shopConfig;
        public ShrineProgressionConfig ShrineConfig => _shrineConfig;
        // Combat floors 1, 1+interval, ... select; shop visits never advance this clock.
        public int SelectionInterval => _selectionInterval;
        public int ShopInterval => _shopInterval;
        // Retained for serialized compatibility only; selection no longer applies a body cap.
        public int MaximumActiveThreats => _maximumActiveThreats;
        public int GoldenCakeValue => _goldenCakeValue;
        public float FasterCollapseGoldenCakeMultiplier => _fasterCollapseGoldenCakeMultiplier;
        public float NothingShopPriceMultiplier => _nothingShopPriceMultiplier;
        public float InitialMaximumHealth => _initialMaximumHealth;
        public float MinimumMaximumHealth => _minimumMaximumHealth;
        public float MaximumMaximumHealth => _maximumMaximumHealth;
        public float MinimumMultiplier => _minimumMultiplier;
        public float MaximumMovementMultiplier => _maximumMovementMultiplier;
        public float MaximumHunterMultiplier => _maximumHunterMultiplier;
        public float MaximumFogMultiplier => _maximumFogMultiplier;
        public float MaximumFlashlightMultiplier => _maximumFlashlightMultiplier;
        public IReadOnlyList<ProgressionEntryConfig> Threats => _threats;
        public IReadOnlyList<ProgressionEntryConfig> Curses => _curses;
        public IReadOnlyList<ProgressionEntryConfig> Offers => _offers;
    }
}
