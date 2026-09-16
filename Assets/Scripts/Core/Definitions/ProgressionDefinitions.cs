// ============================================================================
// ProgressionDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries the state of a multi-floor expedition between gameplay and its menus.
//   These values keep generated rooms, retained choices and shop presentation
//   independent of the Session implementation that owns the progression rules.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Progression shared contracts.
// KEY RESPONSIBILITIES:
//   - Describe immutable display snapshots and committed generation requests.
//   - Carry the effective loadout without exposing mutable run state.
// DEPENDENCIES:
//   - System collection interfaces only; no project layer dependencies.
// USAGE NOTES:
//   GenerationId and Revision are monotonically increasing within one manager's
//   lifetime, including restarts. Snapshot collections are read-only copies made
//   by the owning controller; constructors retain the supplied read-only values.
// ============================================================================
using System.Collections.Generic;

namespace Worsen.Core
{
    public enum ProgressionPhase { Dormant, ChooseThreat, ChooseCurse, Generating, Exploring, Shop, Ended, GenerationFailed }
    public enum ProgressionChoiceKind { Threat, Curse, Upgrade }

    public readonly struct ProgressionChoice
    {
        public ProgressionChoice(string id, string title, string description, int selectedCount)
        { Id = id; Title = title; Description = description; SelectedCount = selectedCount; }
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public int SelectedCount { get; }
    }

    public readonly struct ProgressionOffer
    {
        public ProgressionOffer(string id, string title, string description, int price, bool purchased, bool canAfford)
        { Id = id; Title = title; Description = description; Price = price; Purchased = purchased; CanAfford = canAfford; }
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public int Price { get; }
        public bool Purchased { get; }
        public bool CanAfford { get; }
    }

    public readonly struct ProgressionSelection
    {
        public ProgressionSelection(string id, string title, ProgressionChoiceKind kind, int count)
        { Id = id; Title = title; Kind = kind; Count = count; }
        public string Id { get; }
        public string Title { get; }
        public ProgressionChoiceKind Kind { get; }
        public int Count { get; }
    }

    public readonly struct ProgressionEffects
    {
        public ProgressionEffects(float movementSpeedMultiplier, float hunterSpeedMultiplier, float fogDensityMultiplier,
            float flashlightRangeMultiplier, float maximumHealth, float health, int activeThreatBudget)
        {
            MovementSpeedMultiplier = movementSpeedMultiplier; HunterSpeedMultiplier = hunterSpeedMultiplier;
            FogDensityMultiplier = fogDensityMultiplier; FlashlightRangeMultiplier = flashlightRangeMultiplier;
            MaximumHealth = maximumHealth; Health = health; ActiveThreatBudget = activeThreatBudget;
        }
        public float MovementSpeedMultiplier { get; }
        public float HunterSpeedMultiplier { get; }
        public float FogDensityMultiplier { get; }
        public float FlashlightRangeMultiplier { get; }
        public float MaximumHealth { get; }
        public float Health { get; }
        public int ActiveThreatBudget { get; }
    }

    public readonly struct ProgressionGenerationRequest
    {
        public ProgressionGenerationRequest(int generationId, int seed, int round, bool isShop, ProgressionEffects effects)
        { GenerationId = generationId; Seed = seed; Round = round; IsShop = isShop; Effects = effects; }
        public int GenerationId { get; }
        public int Seed { get; }
        public int Round { get; }
        public bool IsShop { get; }
        public ProgressionEffects Effects { get; }
    }

    public readonly struct ProgressionSnapshot
    {
        public ProgressionSnapshot(int revision, int generationId, int round, int seed, int wallet,
            int threatCount, int curseCount, ProgressionPhase phase, float health, float maxHealth,
            IReadOnlyList<ProgressionChoice> choices, IReadOnlyList<ProgressionOffer> offers,
            IReadOnlyList<ProgressionSelection> retained, ProgressionEffects effects, string message,
            bool canContinue, bool canRestart)
        {
            Revision = revision; GenerationId = generationId; Round = round; Seed = seed; Wallet = wallet;
            ThreatCount = threatCount; CurseCount = curseCount; Phase = phase; Health = health; MaxHealth = maxHealth;
            Choices = choices; Offers = offers; Retained = retained; Effects = effects; Message = message;
            CanContinue = canContinue; CanRestart = canRestart;
        }
        public int Revision { get; }
        public int GenerationId { get; }
        public int Round { get; }
        public int Seed { get; }
        public int Wallet { get; }
        public int ThreatCount { get; }
        public int CurseCount { get; }
        public ProgressionPhase Phase { get; }
        public float Health { get; }
        public float MaxHealth { get; }
        public IReadOnlyList<ProgressionChoice> Choices { get; }
        public IReadOnlyList<ProgressionOffer> Offers { get; }
        public IReadOnlyList<ProgressionSelection> Retained { get; }
        public ProgressionEffects Effects { get; }
        public string Message { get; }
        public bool CanContinue { get; }
        public bool CanRestart { get; }
    }
}
