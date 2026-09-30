// ============================================================================
// DirectorHintAssertions.cs
// ============================================================================
// PURPOSE:
//   Checks delivered room hints against the historical source and actual topology.
//   Radius expectations use shared acoustic portal/door loss, not the delivered
//   radius or a fixed prototype radius, while preserving tight numeric assertions.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Director integration assertions.
// KEY RESPONSIBILITIES:
//   - Inspect the Director's injected level and closed-door inputs without mutation.
//   - Require the historical room centre and occlusion expansion capped by config.
//   - Check exact legacy fallback when either endpoint has no acoustic room route.
// DEPENDENCIES:
//   - Core acoustics, Domain Director/Level, NUnit and read-only reflection.
// USAGE NOTES:
//   Called synchronously on delivery, before the next simulation tick. The cap is
//   the maximum added uncertainty, not a cap on the base-plus-expansion radius.
//   Missing topology is a fixture failure, not permission to skip assertions.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Director;
using Worsen.Domain.Level;

namespace Worsen.Tests.Director
{
    internal static class DirectorHintAssertions
    {
        internal static void Region(DirectorManager director, DirectorConfig config, HintPayload hint,
            Vector3 historicalPosition, Vector3 hunterPosition, HearingModelSettings hearing)
        {
            var controller = Read<DirectorController>(director, "_controller");
            var level = Read<IReadOnlyLevelState>(controller, "_level");
            var doors = Read<IReadOnlyDictionary<int, bool>>(controller, "_closedDoors");
            Assert.That(level, Is.Not.Null);
            Assert.That(level.IsReady, Is.True);
            var source = level.Graph.Rooms.FirstOrDefault(room => room.Bounds.Contains(historicalPosition));
            var listener = level.Graph.Rooms.FirstOrDefault(room => room.Bounds.Contains(hunterPosition));
            if (source.Id == 0 || listener.Id == 0 || hearing.ReferenceDistance <= 0f)
            {
                HistoricalFallback(config, hint, historicalPosition);
                return;
            }
            var sample = AcousticOcclusionUtility.Sample(level.Graph, source.Id, historicalPosition,
                listener.Id, hunterPosition, 1f, hearing, doors);
            if (sample.PortalCount < 0)
            {
                HistoricalFallback(config, hint, historicalPosition);
                return;
            }
            // Remove distance falloff: only portal and closed-door transmission widens a room hint.
            double x = (double)historicalPosition.x - hunterPosition.x;
            double y = (double)historicalPosition.y - hunterPosition.y;
            double z = (double)historicalPosition.z - hunterPosition.z;
            double falloff = Math.Pow(Math.Max(1d, Math.Sqrt(x * x + y * y + z * z) /
                hearing.ReferenceDistance), -hearing.Rolloff);
            float occlusion = 1f - Mathf.Clamp01((float)(sample.PerceivedLoudness / Math.Max(double.Epsilon, falloff)));
            float expansion = config.OcclusionHintRadiusMeters * occlusion;
            Assert.That(expansion, Is.InRange(0f, config.OcclusionHintRadiusMeters));
            Assert.That(hint.Radius, Is.EqualTo(config.HintRadiusMeters + expansion).Within(0.0001f),
                "Radius must use actual portal/closed-door attenuation; the cap bounds added uncertainty.");
            var centre = new Vector3(source.Center.x, source.Bounds.min.y, source.Center.z);
            Assert.That(Vector3.Distance(hint.Position, centre), Is.LessThan(0.002f),
                "Region delivery must identify the actual historical room, not the current player pose.");
        }

        private static void HistoricalFallback(DirectorConfig config, HintPayload hint, Vector3 historicalPosition)
        {
            Assert.That(hint.Radius, Is.EqualTo(config.HintRadiusMeters).Within(0.0001f));
            Assert.That(Vector3.Distance(hint.Position, historicalPosition), Is.LessThan(0.002f));
        }

        private static T Read<T>(object target, string name)
        {
            Assert.That(target, Is.Not.Null, "Missing owner while inspecting " + name);
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing inspected field " + target.GetType().Name + "." + name);
            return (T)field.GetValue(target);
        }
    }
}
