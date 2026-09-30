// ============================================================================
// ProgressionRosterUtility.cs
// ============================================================================
// PURPOSE:
//   Defines the approved run roster independently of stale serialized catalogues.
//   Compatibility scenes may still construct the legacy profiles directly; they
//   cannot reintroduce retired hunters or curses into expedition selection.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Enforce owner-approved first-admission rounds without lifetime prerequisites.
//   - Identify retired effect identities for runtime admission and editor migration.
// DEPENDENCIES:
//   - System string comparison only; no engine or foreign system calls.
// USAGE NOTES:
//   Round gates are owner decisions, not provisional difficulty tuning.
// ============================================================================
using System;
namespace Worsen.Session.Progression
{
    public static class ProgressionRosterUtility
    {
        public static int FirstRound(string id)
        {
            switch (id)
            {
                case "echo": case "weaver": case "ticking": return 1;
                case "ram": case "mannequin": return 4;
                case "mimic": case "blinder": return 5;
                case "skip": case "herald": case "stare": return 6;
                default: return int.MaxValue;
            }
        }
        public static bool Admits(string id, int round) => FirstRound(id) != int.MaxValue && round >= FirstRound(id);
        public static bool Retired(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (string legacy in new[] { "watcher", "rusher", "lurker", "hexer", "thorncaller" })
                if (id == legacy || id.StartsWith(legacy + "-", StringComparison.Ordinal)) return true;
            switch (id)
            {
                case "echo-debt": case "afterimage": case "restless-masonry": case "gilded-hunger":
                case "borrowed-footsteps": case "unquiet-flame": case "sealed-sills":
                case "thin-skin": case "bail-bond": case "field-dressing": return true;
                default: return false;
            }
        }
    }
}
