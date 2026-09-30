// ============================================================================
// ProceduralThemeUtility.cs
// ============================================================================
// PURPOSE:
//   Selects a content vocabulary without consuming any layout randomness.
//   The supplied seed chooses the first theme. Paired floor draws exclude both
//   neighbours, allowing constant-time seeking even at extreme round indices.
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
            var entries = new[] { config.Castle, config.HospitalEnabled ? config.Hospital : null,
                config.SchoolEnabled ? config.School : null, config.BasementEnabled ? config.Basement : null }
                .Where(t => t != null).ToArray();
            foreach (var entry in entries) Validate(entry);
            if (entries.Length == 0 || entries.Select(t => t.Id).Distinct().Count() != entries.Length)
                throw new ArgumentException("Theme ids must be present and unique.");
            int first = random.Next(entries.Length);
            if (config.PerRun || entries.Length == 1 || round < Math.Max(2, config.FirstRound)) return entries[first];
            long step = (long)round - Math.Max(2, config.FirstRound) + 1;
            if (entries.Length == 2) return entries[(first + (int)(step % 2)) % 2];
            uint salt = (uint)random.Next();
            int Even(long index) => index == 0 ? first : (int)(Mix(salt ^ (uint)index) % (uint)entries.Length);
            if (step % 2 == 0) return entries[Even(step)];
            int before = Even(step - 1), after = Even(step + 1);
            var allowed = Enumerable.Range(0, entries.Length).Where(i => i != before && i != after).ToArray();
            return entries[allowed[Mix(salt ^ (uint)step) % (uint)allowed.Length]];
        }
        private static uint Mix(uint value)
        {
            unchecked { value ^= value >> 16; value *= 0x7feb352d; value ^= value >> 15;
                value *= 0x846ca68b; return value ^ (value >> 16); }
        }
        public static string Manifest(ProceduralThemeData theme)
        {
            if (theme == null) return "|Theme:castle";
            return "|Theme:" + theme.Id + ",inherit=" + theme.InheritMaterials + "," + string.Join(",", theme.Families) + "," + theme.Prop + "," +
                theme.LightSource + "," + theme.SoundZone + "," + theme.FogLook + "," + theme.HandLook + "," + theme.PropPrimitive + "," +
                string.Join(",", new[] { theme.Wall.r, theme.Wall.g, theme.Wall.b, theme.Floor.r, theme.Floor.g,
                    theme.Floor.b, theme.Ceiling.r, theme.Ceiling.g, theme.Ceiling.b, theme.Smoothness, theme.WallHeight }
                    .Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
        }
        private static void Validate(ProceduralThemeData data)
        {
            if (data == null || data.Families.Count != Enum.GetValues(typeof(ProceduralModuleKind)).Length ||
                data.Families.Concat(new[] { data.Id, data.Prop, data.LightSource, data.SoundZone, data.FogLook, data.HandLook })
                    .Any(s => string.IsNullOrWhiteSpace(s) || s.IndexOfAny(new[] { '|', ',', ';' }) >= 0) ||
                !(data.Smoothness >= 0f && data.Smoothness <= 1f) ||
                !(data.WallHeight > 2.8f) || float.IsInfinity(data.WallHeight) || data.Id != data.Id.ToLowerInvariant() ||
                (data.PropPrimitive != PrimitiveType.Cube && data.PropPrimitive != PrimitiveType.Cylinder))
                throw new ArgumentException("Invalid theme catalogue (props support cube or cylinder).");
            foreach (Color c in new[] { data.Wall, data.Floor, data.Ceiling })
                if (!(c.r >= 0f && c.r <= 1f && c.g >= 0f && c.g <= 1f && c.b >= 0f && c.b <= 1f))
                    throw new ArgumentException("Invalid theme palette.");
        }
    }
}
