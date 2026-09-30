// ============================================================================
// ProceduralThemeUtility.cs
// ============================================================================
// PURPOSE:
//   Selects a content vocabulary without consuming any layout randomness.
//   The supplied seeded draw determines the first theme and later rounds alternate,
//   so swapping content cannot change doors, anchors, heights or hunter routes.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Validate theme content and serialize its reproducible manifest suffix.
// DEPENDENCIES:
//   - Own configs and Unity value types only; no engine calls.
// USAGE NOTES:
//   Castle's null entry is the legacy vocabulary. Its manifest suffix is additive;
//   everything preceding it retains the previous castle-rooms-v6 representation.
// ============================================================================
using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralThemeUtility
    {
        public static ProceduralThemeData Select(ProceduralThemeConfig config, int round, System.Random random)
        {
            if (config == null) return null;
            if (round < 1 || config.FirstRound < 1 || random == null) throw new ArgumentException("Invalid theme selection.");
            Validate(config.Castle); Validate(config.Hospital);
            if (config.Castle.Id == config.Hospital.Id) throw new ArgumentException("Theme ids must differ.");
            if (!config.HospitalEnabled || (!config.PerRun && round < config.FirstRound)) return config.Castle;
            int phase = config.PerRun ? 0 : round - config.FirstRound;
            return (random.Next(2) + phase) % 2 == 0 ? config.Castle : config.Hospital;
        }
        public static string Manifest(ProceduralThemeData theme)
        {
            if (theme == null) return "|Theme:castle";
            return "|Theme:" + theme.Id + ",inherit=" + theme.InheritMaterials + "," + string.Join(",", theme.Families) + "," + theme.Prop + "," +
                theme.LightSource + "," + theme.SoundZone + "," + theme.FogLook + "," + theme.HandLook + "," + theme.PropPrimitive + "," +
                string.Join(",", new[] { theme.Wall.r, theme.Wall.g, theme.Wall.b, theme.Floor.r, theme.Floor.g,
                    theme.Floor.b, theme.Ceiling.r, theme.Ceiling.g, theme.Ceiling.b, theme.Smoothness }
                    .Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
        }
        private static void Validate(ProceduralThemeData data)
        {
            if (data == null || data.Families.Count != Enum.GetValues(typeof(ProceduralModuleKind)).Length ||
                data.Families.Concat(new[] { data.Id, data.Prop, data.LightSource, data.SoundZone, data.FogLook, data.HandLook })
                    .Any(s => string.IsNullOrWhiteSpace(s) || s.IndexOfAny(new[] { '|', ',', ';' }) >= 0) ||
                !(data.Smoothness >= 0f && data.Smoothness <= 1f) ||
                (data.PropPrimitive != PrimitiveType.Cube && data.PropPrimitive != PrimitiveType.Cylinder))
                throw new ArgumentException("Invalid theme catalogue (props support cube or cylinder).");
            foreach (Color c in new[] { data.Wall, data.Floor, data.Ceiling })
                if (!(c.r >= 0f && c.r <= 1f && c.g >= 0f && c.g <= 1f && c.b >= 0f && c.b <= 1f))
                    throw new ArgumentException("Invalid theme palette.");
        }
    }
}
