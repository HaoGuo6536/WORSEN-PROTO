// ============================================================================
// TelemetryObservationPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Converts committed progression transactions and translated navigation evidence
//   into observational samples. It preserves exact integer identities and seeds
//   in the existing Core Detail contract without applying progression rules.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Telemetry.
//
// KEY RESPONSIBILITIES:
//   - Add fallback evidence and a stable UTF-8 manifest SHA-256 to generation-failure round rows only.
//   - Emit one wallet row per balance change and one choice row per accepted choice.
//   - Describe round boundaries and generation seeds with named CSV payload fields.
//   - Format Hunter evidence supplied as primitives, never Hunter-local types.
//
// DEPENDENCIES:
//   - Core progression/sample values, local CSV presenter, UTF-8/SHA-256 and Unity value types.
//
// USAGE NOTES:
//   Pure and stateless; ticks and floor seeds are supplied by the routing boundary.
//   Round start means a committed generation request, not successful assembly.
//   Failed generation ends that round. Shop continuation is an explicit outcome.
//   Early bail is retired; operation labels never classify an escape as a bail.
//   Missing floor seed or room/obstacle evidence remains blank, never invented.
//   Failure evidence stays in CSV Detail; existing named-column/schema widths remain unchanged.
//
// ============================================================================
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Telemetry
{
    public sealed class TelemetryObservationPresenter
    {
        public IEnumerable<TelemetrySample> Transaction(ProgressionSnapshot before, ProgressionSnapshot after,
            string operation, string choiceId, long tick, int? floorSeed, bool? usedFallback = null, string layoutManifest = null)
        {
            bool ended = before.Phase == ProgressionPhase.Exploring &&
                (after.Round != before.Round || after.Phase == ProgressionPhase.Ended);
            bool shopEnded = before.Phase == ProgressionPhase.Shop && after.Round != before.Round;
            bool failed = before.Phase == ProgressionPhase.Generating && after.Phase == ProgressionPhase.GenerationFailed;
            if (failed)
                yield return TelemetryCsvPresenter.Observation(tick, TelemetrySampleKind.RoundEnded,
                    ("round_index", before.Round), ("generation_id", before.GenerationId), ("generation_seed", floorSeed),
                    ("round_outcome", "GenerationFailed"), ("used_fallback", usedFallback),
                    ("layout_manifest_hash", ManifestHash(layoutManifest)));
            else if (ended || shopEnded)
                yield return Round(tick, TelemetrySampleKind.RoundEnded, before.Round, before.GenerationId, floorSeed,
                    shopEnded ? "ShopContinued" : after.Phase == ProgressionPhase.Ended ? "Died" : "Escaped");
            if (before.Wallet != after.Wallet)
                yield return TelemetryCsvPresenter.Observation(tick, TelemetrySampleKind.WalletChanged,
                    ("round_index", before.Round), ("generation_id", before.GenerationId), ("generation_seed", floorSeed),
                    ("wallet_delta", (long)after.Wallet - before.Wallet), ("wallet_balance", after.Wallet), ("wallet_reason", operation));
            string kind = operation == "ChooseThreat" ? "Threat" : operation == "ChooseCurse" ? "Curse" :
                operation == "Purchase" ? "Purchase" : null;
            if (kind != null)
                yield return TelemetryCsvPresenter.Observation(tick, TelemetrySampleKind.ProgressionChoice,
                    ("round_index", after.Round), ("choice_id", choiceId), ("choice_kind", kind));
        }

        public static string ManifestHash(string manifest)
        {
            if (string.IsNullOrEmpty(manifest)) return "";
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(manifest));
                var text = new StringBuilder();
                foreach (byte value in bytes) text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        public IEnumerable<TelemetrySample> Generation(ProgressionGenerationRequest request, long tick)
        {
            yield return Round(tick, TelemetrySampleKind.RoundStarted, request.Round, request.GenerationId, request.Seed, "Started");
            yield return TelemetryCsvPresenter.Observation(tick, TelemetrySampleKind.FloorSeed,
                ("round_index", request.Round), ("generation_id", request.GenerationId), ("generation_seed", request.Seed));
        }

        public TelemetrySample Stall(EntityId hunter, string archetypeKey, long tick, Vector3 position, int? roomId,
            double remainingDistance, float queryRadius, float capsuleRadius, float sweepRadius, string action,
            IReadOnlyList<Vector3> corners, Vector3? nearestObstacle, int seed)
        {
            var path = new List<string>();
            if (corners != null)
                foreach (Vector3 corner in corners)
                    path.Add(string.Format(CultureInfo.InvariantCulture, "{0:R}:{1:R}:{2:R}", corner.x, corner.y, corner.z));
            return TelemetryCsvPresenter.Observation(tick, TelemetrySampleKind.HunterStall,
                ("hunter_id", hunter.Value), ("archetype_key", archetypeKey), ("generation_seed", seed),
                ("position_x", position.x), ("position_y", position.y), ("position_z", position.z), ("room_id", roomId),
                ("remaining_distance", remainingDistance), ("query_radius", queryRadius), ("capsule_radius", capsuleRadius),
                ("sweep_radius", sweepRadius), ("action", action), ("corner_count", corners?.Count ?? 0),
                ("path_corners", string.Join("|", path)), ("nearest_obstacle_x", nearestObstacle?.x),
                ("nearest_obstacle_y", nearestObstacle?.y), ("nearest_obstacle_z", nearestObstacle?.z));
        }

        private static TelemetrySample Round(long tick, TelemetrySampleKind kind, int round, int generation,
            int? seed, string outcome) => TelemetryCsvPresenter.Observation(tick, kind,
                ("round_index", round), ("generation_id", generation), ("generation_seed", seed), ("round_outcome", outcome));
    }
}
