// ============================================================================
// PlayerEffectUtility.cs
// ============================================================================
// PURPOSE:
//   Combines Player effect rows without engine calls or catalogue identifiers.
//   Reduction follows config order, independent of active-view enumeration order,
//   and returns the exact baseline when no matching effect exists.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Multiply factors, sum fractional and flat bonuses, then enforce channel caps.
//   - Keep disabling factors authoritative over fractional upgrades.
// DEPENDENCIES:
//   - Core read-only effect contracts and PlayerEffectConfig.
// USAGE NOTES:
//   Fractional stacks add together before multiplying the factored baseline;
//   flat bonuses follow. No Regen and Heavy Legs therefore remain zero with upgrades.
//   Bad authored rows fail explicitly rather than silently repairing identifiers.
// ============================================================================
using System;
using Worsen.Core;

namespace Worsen.Domain.Player
{
    public static class PlayerEffectUtility
    {
        public static bool HasModifier(PlayerEffectConfig config, IReadOnlyActiveEffects effects, PlayerEffectStat stat)
        {
            if (config is null || effects == null || effects.Count == 0) return false;
            foreach (PlayerEffectConfig.Mapping row in config.Mappings)
                if (row != null && row.Stat == stat && effects.Stacks(new EffectId(row.EffectId)) > 0) return true;
            return false;
        }

        public static float Value(PlayerEffectConfig config, IReadOnlyActiveEffects effects,
            PlayerEffectStat stat, float baseline)
        {
            if (config is null || effects == null || effects.Count == 0) return baseline;
            double factor = 1d, fraction = 0d, bonus = 0d;
            bool matched = false;
            foreach (PlayerEffectConfig.Mapping row in config.Mappings)
            {
                if (row == null || row.Stat != stat) continue;
                if (string.IsNullOrWhiteSpace(row.EffectId) || row.StackCap < 1
                    || float.IsNaN(row.Amount) || float.IsInfinity(row.Amount) || row.Amount < 0f)
                    throw new ArgumentException("Invalid Player effect mapping.", nameof(config));
                int stacks = Math.Min(row.StackCap, effects.Stacks(new EffectId(row.EffectId)));
                if (stacks <= 0) continue;
                matched = true;
                switch (row.Operation)
                {
                    case PlayerEffectOperation.Multiply: factor *= Math.Pow(row.Amount, stacks); break;
                    case PlayerEffectOperation.Add: bonus += (double)row.Amount * stacks; break;
                    case PlayerEffectOperation.AddFraction: fraction += (double)row.Amount * stacks; break;
                    default: throw new ArgumentOutOfRangeException(nameof(row.Operation));
                }
            }
            if (!matched) return baseline;
            double value = baseline * factor * (1d + fraction) + bonus;
            if (stat == PlayerEffectStat.SprintSpeed) value = Math.Min(value, SprintCeiling(config));
            if (stat == PlayerEffectStat.TraversalDuration) value = Math.Max(Math.Max(0.1f, config.MinimumTraversalSeconds), value);
            if (stat == PlayerEffectStat.SlideRetention || stat == PlayerEffectStat.FloorStartHealth) value = Math.Min(1d, value);
            if (double.IsNaN(value) || double.IsInfinity(value) || value > float.MaxValue || value < 0d)
                throw new ArgumentOutOfRangeException(nameof(config), "Player effect result must be finite and nonnegative.");
            return (float)value;
        }

        public static float SprintCeiling(PlayerEffectConfig config)
        {
            float ceiling = config.HunterChaseSpeedCeiling - config.ChaseSpeedMargin;
            if (float.IsNaN(ceiling) || float.IsInfinity(ceiling) || ceiling <= 0f
                || ceiling >= config.HunterChaseSpeedCeiling)
                throw new ArgumentOutOfRangeException(nameof(config), "Sprint ceiling requires a positive strict margin.");
            return ceiling;
        }
    }
}
