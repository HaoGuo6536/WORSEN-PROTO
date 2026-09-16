// ============================================================================
// ProgressionConfig.cs
// ============================================================================
// PURPOSE:
//   Defines the expedition's unique curse and upgrade catalog, consumable stock and caps.
//   Designers can tune prices and effect traits
//   without changing wallet, selection or round-transition code.
// ARCHITECTURAL ROLE:
//   Config (§4) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Keep all balance values and offer descriptions in designer-owned data.
//   - Supply safe defaults for a shop after every two completed combat floors.
//   - Describe five hunter identities and hunter-dependent plus general curse traits.
// DEPENDENCIES:
//   - Unity ScriptableObject serialization and System collection interfaces.
// USAGE NOTES:
//   Mirrored asset: ScriptableObjects/Session/Progression/ProgressionConfig.
//   Runtime code only reads this asset. Counts, purchases and health live in State.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

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
        [SerializeField, Min(2)] private int _shopInterval = 2;
        [SerializeField, Min(1)] private int _maximumActiveThreats = 5;
        [SerializeField, Min(1)] private int _goldenCakeValue = 1;
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
            new ProgressionEntryConfig("watcher", "THE WATCHER", "A patient Satyr follows remembered light and tries to cut off your route."),
            new ProgressionEntryConfig("rusher", "THE RUSHER", "A Werewolf commits to aggressive close-range lunges."),
            new ProgressionEntryConfig("lurker", "THE LURKER", "A Goblin slips aside from your beam and attacks from the dark."),
            new ProgressionEntryConfig("hexer", "THE HEXER", "A hovering Fairy casts clearly signalled projectiles along your route."),
            new ProgressionEntryConfig("thorncaller", "THE THORNCALLER", "A rooted creature warns the ground before raising deadly thorns.")
        };
        [SerializeField] private ProgressionEntryConfig[] _curses =
        {
            new ProgressionEntryConfig("echo-debt", "ECHO DEBT", "Hard landings leave a delayed noise that hunters can investigate.", traits: ProgressionTraits.EchoDebt),
            new ProgressionEntryConfig("afterimage", "AFTERIMAGE", "Switching off your flashlight leaves a brief light trace at your last position.", traits: ProgressionTraits.Afterimage),
            new ProgressionEntryConfig("restless-masonry", "RESTLESS MASONRY", "Optional rooms begin cracking sooner. The escape route remains intact.", traits: ProgressionTraits.RestlessMasonry),
            new ProgressionEntryConfig("gilded-hunger", "GILDED HUNGER", "Golden Cakes announce their collection with a sound that draws nearby hunters.", traits: ProgressionTraits.GildedHunger),
            new ProgressionEntryConfig("borrowed-footsteps", "BORROWED FOOTSTEPS", "Your footsteps echo along your recent route after you have moved on.", traits: ProgressionTraits.BorrowedFootsteps),
            new ProgressionEntryConfig("unquiet-flame", "UNQUIET FLAME", "Sprinting gutters nearby decorative flames; route lights keep a faint glow.", traits: ProgressionTraits.UnquietFlame),
            new ProgressionEntryConfig("sealed-sills", "SEALED SILLS", "Some optional vault windows are sealed. Required routes and stairs remain passable.", traits: ProgressionTraits.SealedSills),
            new ProgressionEntryConfig("rusher-long-stride", "LONG STRIDE", "The Rusher commits to a farther-reaching lunge. Sidestep its warning line.", traits: ProgressionTraits.RusherLongStride, requiredThreatId: "rusher"),
            new ProgressionEntryConfig("rusher-second-wind", "SECOND WIND", "The Rusher recovers sooner after a missed lunge. Its next windup remains visible.", traits: ProgressionTraits.RusherSecondWind, requiredThreatId: "rusher"),
            new ProgressionEntryConfig("rusher-blood-scent", "BLOOD SCENT", "The Rusher hears farther and remembers your last noise longer.", traits: ProgressionTraits.RusherBloodScent, requiredThreatId: "rusher"),
            new ProgressionEntryConfig("lurker-dark-adaptation", "DARK ADAPTATION", "The Lurker sees a wider arc around itself in darkness.", traits: ProgressionTraits.LurkerDarkAdaptation, requiredThreatId: "lurker"),
            new ProgressionEntryConfig("lurker-crooked-step", "CROOKED STEP", "The Lurker makes a stronger, longer sideways dodge when caught in your light.", traits: ProgressionTraits.LurkerCrookedStep, requiredThreatId: "lurker"),
            new ProgressionEntryConfig("lurker-stolen-silence", "STOLEN SILENCE", "The Lurker attacks with a shorter but still readable windup.", traits: ProgressionTraits.LurkerStolenSilence, requiredThreatId: "lurker"),
            new ProgressionEntryConfig("watcher-long-memory", "LONG MEMORY", "The Watcher remembers seen players and flashlight traces for longer.", traits: ProgressionTraits.WatcherLongMemory, requiredThreatId: "watcher"),
            new ProgressionEntryConfig("watcher-cutting-corners", "CUTTING CORNERS", "The Watcher predicts your route earlier and aims farther ahead when cutting you off.", traits: ProgressionTraits.WatcherCuttingCorners, requiredThreatId: "watcher"),
            new ProgressionEntryConfig("watcher-unquiet-gaze", "UNQUIET GAZE", "The Watcher sees farther and may scream as it attacks.", traits: ProgressionTraits.WatcherUnquietGaze, requiredThreatId: "watcher"),
            new ProgressionEntryConfig("hexer-split-bolt", "SPLIT BOLT", "The Hexer fans three bolts across its warned firing line.", traits: ProgressionTraits.HexerSplitBolt, requiredThreatId: "hexer"),
            new ProgressionEntryConfig("hexer-hasty-script", "HASTY SCRIPT", "The Hexer completes its casting warning sooner. Break line of sight before release.", traits: ProgressionTraits.HexerHastyScript, requiredThreatId: "hexer"),
            new ProgressionEntryConfig("hexer-lingering-hex", "LINGERING HEX", "The Hexer fires slower, wider bolts that occupy your escape path longer.", traits: ProgressionTraits.HexerLingeringHex, requiredThreatId: "hexer"),
            new ProgressionEntryConfig("thorncaller-thorn-ring", "THORN RING", "The Thorncaller raises a ring of thorns around its warned target. Move out of the marked zone.", traits: ProgressionTraits.ThorncallerThornRing, requiredThreatId: "thorncaller"),
            new ProgressionEntryConfig("thorncaller-quick-roots", "QUICK ROOTS", "The Thorncaller shortens the ground warning before its thorns erupt.", traits: ProgressionTraits.ThorncallerQuickRoots, requiredThreatId: "thorncaller"),
            new ProgressionEntryConfig("thorncaller-reaching-roots", "REACHING ROOTS", "The Thorncaller marks a larger eruption area before striking.", traits: ProgressionTraits.ThorncallerReachingRoots, requiredThreatId: "thorncaller")
        };
        [SerializeField] private ProgressionEntryConfig[] _offers =
        {
            new ProgressionEntryConfig("shuttered-lens", "SHUTTERED LENS", "Narrow your beam to reduce incidental exposure while preserving aimed visibility.", price: 3, traits: ProgressionTraits.ShutteredLens),
            new ProgressionEntryConfig("felt-soles", "FELT SOLES", "Quieten ordinary footsteps. Sprinting, hard landings and cursed echoes still carry.", price: 3, traits: ProgressionTraits.FeltSoles),
            new ProgressionEntryConfig("climber-wraps", "CLIMBER'S WRAPS", "Recover control sooner after wall rebounds, within the normal movement limits.", price: 4, traits: ProgressionTraits.ClimberWraps),
            new ProgressionEntryConfig("pilgrim-chalk", "PILGRIM'S CHALK", "Mark doorways you have crossed on this floor. Unvisited rooms remain unknown.", price: 2, traits: ProgressionTraits.PilgrimChalk),
            new ProgressionEntryConfig("field-dressing", "FIELD DRESSING", "Restore 35 health immediately. One dressing per visit; no purchase at full health.", price: 2, healing: 35f, repeatable: true),
            new ProgressionEntryConfig("wax-ward", "WAX WARD", "Automatically break the next shadow-hand grab. Carry one charge; one ward per visit.", price: 2, repeatable: true, grantsWaxWard: true)
        };
        public int ShopInterval => _shopInterval;
        public int MaximumActiveThreats => _maximumActiveThreats;
        public int GoldenCakeValue => _goldenCakeValue;
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
