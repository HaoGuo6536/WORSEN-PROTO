// ============================================================================
// ProgressionUIDefinitions.cs
// ============================================================================
//
// PURPOSE:
//   Carries prepared card content between the pure menu presenter and drawing code.
//   These values are display copies; choice validity, prices and progression
//   remain owned by the Session system through the supplied Core snapshot.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Name local UI actions and immutable display cards.
//
// DEPENDENCIES:
//   Core progression snapshots and own ProgressionUI stack only.
//
// USAGE NOTES:
//   No game rules, callbacks or engine objects.
//
// ============================================================================

namespace Worsen.Presentation.ProgressionUI
{
    public enum ProgressionUIAction { ChooseThreat, ChooseCurse, Purchase, Continue, Restart }

    public readonly struct ProgressionUICard
    {
        public ProgressionUICard(string id, string title, string description, string detail, string action,
            bool enabled, ProgressionUIAction kind)
        { Id = id; Title = title; Description = description; Detail = detail; Action = action; Enabled = enabled; Kind = kind; }
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public string Detail { get; }
        public string Action { get; }
        public bool Enabled { get; }
        public ProgressionUIAction Kind { get; }
    }
}
