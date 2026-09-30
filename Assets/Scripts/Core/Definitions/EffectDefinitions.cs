// ============================================================================
// EffectDefinitions.cs
// ============================================================================
// PURPOSE:
//   Gives shared effects stable catalogue identities without a fixed flag budget.
//   Frozen snapshots allow consumers to inspect stacks without owning progression.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Effects contracts.
// KEY RESPONSIBILITIES:
//   - Preserve ordinal identities and validate immutable, value-equal effect sets.
// DEPENDENCIES:
//   - System collections only; no other project layers.
// USAGE NOTES:
//   Default ids are invalid; default sets are empty. Ids are not trimmed or folded.
//   Enumeration is ordinal by id. Catalogue owners enforce content-specific caps;
//   a stack count also represents repeated threats, not a limit on active bodies.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;

namespace Worsen.Core
{
    /// <summary>The catalogue category of a retained effect.</summary>
    public enum EffectKind { Threat, Curse, Upgrade, Consumable }

    /// <summary>The fear dimension changed by an effect, if any.</summary>
    public enum FearAxis { None, Information, Unpredictability, Stakes, Agency, Time }

    /// <summary>A stable, case-sensitive catalogue key; default is the invalid sentinel.</summary>
    public readonly struct EffectId : IEquatable<EffectId>
    {
        private readonly string _value;
        public EffectId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Effect id must be nonblank.", nameof(value));
            _value = value;
        }
        public string Value => _value ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(_value);
        public bool Equals(EffectId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is EffectId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(EffectId left, EffectId right) => left.Equals(right);
        public static bool operator !=(EffectId left, EffectId right) => !left.Equals(right);
    }

    /// <summary>One positive stack count for a unique effect identity.</summary>
    public readonly struct ActiveEffect : IEquatable<ActiveEffect>
    {
        public ActiveEffect(EffectId id, EffectKind kind, int stackCount)
        {
            if (!id.IsValid) throw new ArgumentException("Effect id must be valid.", nameof(id));
            if (kind < EffectKind.Threat || kind > EffectKind.Consumable) throw new ArgumentOutOfRangeException(nameof(kind));
            if (stackCount <= 0) throw new ArgumentOutOfRangeException(nameof(stackCount));
            Id = id; Kind = kind; StackCount = stackCount;
        }
        public EffectId Id { get; }
        public EffectKind Kind { get; }
        public int StackCount { get; }
        public bool Equals(ActiveEffect other) => Id == other.Id && Kind == other.Kind && StackCount == other.StackCount;
        public override bool Equals(object obj) => obj is ActiveEffect other && Equals(other);
        public override int GetHashCode() => unchecked((Id.GetHashCode() * 397 ^ (int)Kind) * 397 ^ StackCount);
        public static bool operator ==(ActiveEffect left, ActiveEffect right) => left.Equals(right);
        public static bool operator !=(ActiveEffect left, ActiveEffect right) => !left.Equals(right);
    }

    /// <summary>A consumer-only view of effect presence, stacks and entries.</summary>
    public interface IReadOnlyActiveEffects : IEnumerable<ActiveEffect>
    {
        int Count { get; }
        bool Has(EffectId id);
        int Stacks(EffectId id);
    }

    /// <summary>A defensively copied, order-independent value snapshot of active effects.</summary>
    public readonly struct ActiveEffects : IReadOnlyActiveEffects, IEquatable<ActiveEffects>
    {
        private readonly ActiveEffect[] _entries;
        public ActiveEffects(IEnumerable<ActiveEffect> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var copy = new List<ActiveEffect>();
            var ids = new HashSet<EffectId>();
            foreach (var entry in entries)
            {
                if (!entry.Id.IsValid || entry.StackCount <= 0 ||
                    entry.Kind < EffectKind.Threat || entry.Kind > EffectKind.Consumable)
                    throw new ArgumentException("Every entry must be a valid positive stack.", nameof(entries));
                if (!ids.Add(entry.Id)) throw new ArgumentException("Duplicate effect id.", nameof(entries));
                copy.Add(entry);
            }
            copy.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id.Value, b.Id.Value));
            _entries = copy.ToArray();
        }
        public int Count => _entries?.Length ?? 0;
        public bool Has(EffectId id) => Stacks(id) > 0;
        public int Stacks(EffectId id)
        {
            int low = 0, high = Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                int order = StringComparer.Ordinal.Compare(_entries[middle].Id.Value, id.Value);
                if (order == 0) return _entries[middle].StackCount;
                if (order < 0) low = middle + 1; else high = middle - 1;
            }
            return 0;
        }
        public IEnumerator<ActiveEffect> GetEnumerator() =>
            ((IEnumerable<ActiveEffect>)(_entries ?? Array.Empty<ActiveEffect>())).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool Equals(ActiveEffects other)
        {
            if (Count != other.Count) return false;
            for (int i = 0; i < Count; i++)
                if (_entries[i] != other._entries[i]) return false;
            return true;
        }
        public override bool Equals(object obj) => obj is ActiveEffects other && Equals(other);
        public override int GetHashCode()
        {
            int hash = 0;
            for (int i = 0; i < Count; i++) hash = unchecked(hash * 397 ^ _entries[i].GetHashCode());
            return hash;
        }
        public static bool operator ==(ActiveEffects left, ActiveEffects right) => left.Equals(right);
        public static bool operator !=(ActiveEffects left, ActiveEffects right) => !left.Equals(right);
    }
}
