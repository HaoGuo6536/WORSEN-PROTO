// ============================================================================
// ProgressionConfig.cs
// ============================================================================
// PURPOSE:
//   Defines the first expedition's threat, curse and shop catalog with its caps.
//   Designers can replace the placeholder names or tune the functional effects
//   without changing wallet, selection or round-transition code.
// ARCHITECTURAL ROLE:
//   Config (§4) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Keep all balance values and offer descriptions in designer-owned data.
//   - Supply safe defaults for a shop on every fourth generated floor.
//   - Describe threat choices separately once the active hunter limit is reached.
// DEPENDENCIES:
//   - Unity ScriptableObject serialization and System collection interfaces.
// USAGE NOTES:
//   Mirrored asset: ScriptableObjects/Session/Progression/ProgressionConfig.
//   Runtime code only reads this asset. Counts, purchases and health live in State.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

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

        public ProgressionEntryConfig(string id, string title, string description, int price = 0,
            float movementSpeedMultiplier = 1f, float hunterSpeedMultiplier = 1f,
            float fogDensityMultiplier = 1f, float flashlightRangeMultiplier = 1f,
            float maximumHealthDelta = 0f, float healing = 0f, string descriptionAtThreatCap = null)
        {
            _id = id; _title = title; _description = description; _price = price;
            _movementSpeedMultiplier = movementSpeedMultiplier; _hunterSpeedMultiplier = hunterSpeedMultiplier;
            _fogDensityMultiplier = fogDensityMultiplier; _flashlightRangeMultiplier = flashlightRangeMultiplier;
            _maximumHealthDelta = maximumHealthDelta; _healing = healing;
            _descriptionAtThreatCap = descriptionAtThreatCap;
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
    }

    [CreateAssetMenu(menuName = "Worsen/Progression/Progression Config")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        [SerializeField, Min(2)] private int _shopInterval = 4;
        [SerializeField, Min(1)] private int _maximumActiveThreats = 3;
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
            new ProgressionEntryConfig("watcher", "THE WATCHER", "An unrelenting hunter joins the expedition.",
                descriptionAtThreatCap: "Hunter limit reached. Keep the current hunters; no additional hunter or other effect."),
            new ProgressionEntryConfig("rusher", "THE RUSHER", "A hunter joins. All hunters move 8% faster.", hunterSpeedMultiplier: 1.08f,
                descriptionAtThreatCap: "No additional hunter. All hunters move 8% faster, up to the speed limit."),
            new ProgressionEntryConfig("lurker", "THE LURKER", "A hunter joins. Hunters move 4% slower, but fog thickens 12%.", hunterSpeedMultiplier: 0.96f, fogDensityMultiplier: 1.12f,
                descriptionAtThreatCap: "No additional hunter. Hunters move 4% slower and fog thickens 12%, within their limits.")
        };
        [SerializeField] private ProgressionEntryConfig[] _curses =
        {
            new ProgressionEntryConfig("fading-light", "FADING LIGHT", "Your flashlight reaches 15% less far.", flashlightRangeMultiplier: 0.85f),
            new ProgressionEntryConfig("restless", "RESTLESS", "Every hunter moves 6% faster.", hunterSpeedMultiplier: 1.06f),
            new ProgressionEntryConfig("frailty", "FRAILTY", "Lose 10 maximum health for this expedition.", maximumHealthDelta: -10f)
        };
        [SerializeField] private ProgressionEntryConfig[] _offers =
        {
            new ProgressionEntryConfig("medkit", "MEDKIT", "Restore 35 health, up to your current maximum.", price: 3, healing: 35f),
            new ProgressionEntryConfig("running-shoes", "RUNNING SHOES", "Move 8% faster for the rest of this expedition.", price: 5, movementSpeedMultiplier: 1.08f),
            new ProgressionEntryConfig("focus-lens", "FOCUS LENS", "Extend flashlight reach by 20% for this expedition.", price: 4, flashlightRangeMultiplier: 1.2f)
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
