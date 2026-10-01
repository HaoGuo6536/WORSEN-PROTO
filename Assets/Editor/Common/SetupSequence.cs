// ============================================================================
// SetupSequence.cs
// ============================================================================
// PURPOSE:
//   Executes an explicitly ordered setup manifest without hidden discovery order.
//   Returns a structured record of successful, failed and unattempted steps so a
//   caller cannot mistake a partial rebuild for a complete publication.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Common setup sequencing (pure C#).
// KEY RESPONSIBILITIES:
//   - Validate the whole manifest before executing any action.
//   - Stop at the first exception and retain the complete failure diagnostic.
//   - Report every manifest entry in its declared order.
// DEPENDENCIES:
//   - System collections and delegates only; no Unity dependencies.
// USAGE NOTES:
//   No rollback is promised. Earlier steps may already have saved owned assets.
//   Unity admission and logged-error promotion belong to the invoking editor tool.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace Worsen.Editor.Common
{
    public readonly struct SetupStep
    {
        public string Name { get; }
        public string Reason { get; }
        public Action Execute { get; }
        public SetupStep(string name, string reason, Action execute)
        { Name = name; Reason = reason; Execute = execute; }
    }

    public enum SetupStepStatus { NotRun, Succeeded, Failed }

    public readonly struct SetupStepResult
    {
        public string Name { get; }
        public string Reason { get; }
        public SetupStepStatus Status { get; }
        public string Error { get; }
        public SetupStepResult(SetupStep step, SetupStepStatus status, string error)
        { Name = step.Name; Reason = step.Reason; Status = status; Error = error; }
    }

    public sealed class SetupReport
    {
        public IReadOnlyList<SetupStepResult> Steps { get; }
        public bool Succeeded => Steps.Count > 0 && Steps.All(step => step.Status == SetupStepStatus.Succeeded);
        public SetupReport(SetupStepResult[] steps) => Steps = Array.AsReadOnly((SetupStepResult[])steps.Clone());
        public override string ToString() => string.Join("\n", Steps.Select(step => step.Name + ": " + step.Status +
            (string.IsNullOrEmpty(step.Error) ? "" : "\n" + step.Error)));
    }

    public static class SetupSequence
    {
        public static SetupReport Run(IEnumerable<SetupStep> manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            SetupStep[] steps = manifest.ToArray();
            if (steps.Length == 0) throw new ArgumentException("A setup manifest must not be empty.", nameof(manifest));
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in steps)
                if (string.IsNullOrWhiteSpace(step.Name) || string.IsNullOrWhiteSpace(step.Reason) || step.Execute == null || !names.Add(step.Name))
                    throw new ArgumentException("Setup steps require unique names, reasons and actions.", nameof(manifest));
            var results = new SetupStepResult[steps.Length];
            bool failed = false;
            for (int index = 0; index < steps.Length; index++)
            {
                var step = steps[index];
                if (failed) { results[index] = new SetupStepResult(step, SetupStepStatus.NotRun, ""); continue; }
                try
                {
                    step.Execute();
                    results[index] = new SetupStepResult(step, SetupStepStatus.Succeeded, "");
                }
                catch (Exception error)
                {
                    failed = true;
                    results[index] = new SetupStepResult(step, SetupStepStatus.Failed, error.ToString());
                }
            }
            return new SetupReport(results);
        }
    }
}
