// ============================================================================
// EffectCatalogueUtility.cs
// ============================================================================
// PURPOSE:
//   Checks catalogue integrity and admits effects from an immutable run view.
//   Shared pure rules keep displayed offers and accepted choices in agreement.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Validate identities, kinds, axes, requirements, cycles and positive caps.
//   - Enforce round, prerequisite, hunter and retained-stack requirements.
// DEPENDENCIES:
//   - Own catalogue data and Core effect values; no engine calls.
// USAGE NOTES:
//   Legacy hunter ids are supplied explicitly. No threat has a gameplay stack cap.
//   Copy validation is a conservative editorial lint, not natural-language proof.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Worsen.Core;

namespace Worsen.Session.Progression
{
    public static class EffectCatalogueUtility
    {
        public static EffectCatalogueEntry Find(EffectCatalogueConfig catalogue, string id)
        {
            if (catalogue is null) return null;
            foreach (var entry in catalogue.Entries) if (entry.Id == id) return entry;
            return null;
        }

        public static bool Eligible(EffectCatalogueEntry entry, int round, IReadOnlyActiveEffects active)
        {
            if (round < entry.AvailabilityRound || (entry.Kind != EffectKind.Threat && active.Stacks(new EffectId(entry.Id)) >= entry.StackCap)) return false;
            if (!string.IsNullOrEmpty(entry.PrerequisiteEffectId) && !active.Has(new EffectId(entry.PrerequisiteEffectId))) return false;
            if (entry.RequiredHunterIds.Count == 0) return true;
            foreach (var effect in active)
                if (effect.Kind == EffectKind.Threat)
                    foreach (string hunter in entry.RequiredHunterIds) if (effect.Id.Value == hunter) return true;
            return false;
        }

        public static void Validate(EffectCatalogueConfig catalogue, IEnumerable<string> legacyHunters)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var hunters = new HashSet<string>(legacyHunters, StringComparer.Ordinal);
            foreach (var entry in catalogue.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Id) || !Regex.IsMatch(entry.Id, @"^[a-z0-9]+(-[a-z0-9]+)*$") ||
                    !ids.Add(entry.Id) || entry.Kind < EffectKind.Threat || entry.Kind > EffectKind.Consumable ||
                    entry.Axis <= FearAxis.None || entry.Axis > FearAxis.Time || entry.AvailabilityRound < 1 ||
                    entry.StackCap < 1 || entry.Price < 0 || string.IsNullOrWhiteSpace(entry.Title))
                    throw new ArgumentException("Invalid or duplicate catalogue entry.", nameof(catalogue));
                if (entry.Kind == EffectKind.Threat) hunters.Add(entry.Id);
            }
            foreach (var entry in catalogue.Entries)
            {
                foreach (string hunter in entry.RequiredHunterIds)
                    if (!hunters.Contains(hunter)) throw new ArgumentException("Unknown required hunter: " + hunter);
                var chain = new HashSet<string>(StringComparer.Ordinal) { entry.Id };
                var cursor = entry;
                while (!string.IsNullOrEmpty(cursor.PrerequisiteEffectId))
                {
                    if (!chain.Add(cursor.PrerequisiteEffectId)) throw new ArgumentException("Cyclic effect prerequisite: " + entry.Id);
                    cursor = Find(catalogue, cursor.PrerequisiteEffectId);
                    if (cursor == null) throw new ArgumentException("Unknown effect prerequisite: " + entry.Id);
                }
            }
        }

        public static bool CopyStatesChange(EffectCatalogueEntry entry)
        {
            // SPEC-004 explicitly requires the hidden mutation's displayed copy to say only this.
            string copy = entry.Id == "nothing" && entry.CardCopy == "Nothing???" ? entry.ChangeStatement : entry.CardCopy;
            return !string.IsNullOrWhiteSpace(copy) && Regex.IsMatch(copy,
                @"\b(removes?|reduces?|replaces?|shortens?|increases?|adds?|changes?|stores?|releases?|no longer|no noise|cannot|instead|longer|faster|half|double|breaks?|closes?|outlines?|farther|closer|visible|lower|grows|refunds?|shows?|multiplied|restores?|ends?|jams?|slip|flinch|draws?|mark|stops?|deafens?)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
