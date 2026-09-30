// ============================================================================
// PlayerEffectConfig.cs
// ============================================================================
// PURPOSE:
//   Maps exact catalogue keys to Player-owned rules and provisional stack tuning.
//   The catalogue may ship later: this asset supplies all Player defaults without
//   placing curse or upgrade identifiers in movement logic.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Own per-stack operations, caps, traversal floor and hunter speed ceiling.
// DEPENDENCIES:
//   - Player definitions and Unity serialization only.
// USAGE NOTES:
//   Designer data only; runtime never edits it. Multiple rows may share an id to
//   modify multiple channels. Row order is the deterministic reduction order.
//   Create via Worsen/Player/Ensure Effect Config; existing tuning is preserved.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Domain.Player
{
    [CreateAssetMenu(menuName = "Worsen/Player/Player Effect Config")]
    public sealed class PlayerEffectConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Mapping
        {
            [SerializeField] private string _effectId;
            [SerializeField] private PlayerEffectStat _stat;
            [SerializeField] private PlayerEffectOperation _operation;
            [SerializeField, Min(0f)] private float _amount;
            [SerializeField, Min(1)] private int _stackCap;
            public string EffectId => _effectId;
            public PlayerEffectStat Stat => _stat;
            public PlayerEffectOperation Operation => _operation;
            public float Amount => _amount;
            public int StackCap => _stackCap;
            public Mapping(string id, PlayerEffectStat stat, PlayerEffectOperation operation, float amount, int cap = 1)
            { _effectId = id; _stat = stat; _operation = operation; _amount = amount; _stackCap = cap; }
        }

        [SerializeField, Min(0.1f)] private float _hunterChaseSpeedCeiling = 9.5f;
        [SerializeField, Min(0.001f)] private float _chaseSpeedMargin = 0.01f;
        [SerializeField, Min(0.1f)] private float _minimumTraversalSeconds = 0.1f;
        [SerializeField, Min(0f)] private float _standstillSpeed = 0.1f;
        [SerializeField, Min(0f)] private float _storedMomentumWindow = 1f;
        [SerializeField] private Mapping[] _mappings =
        {
            new Mapping("short-grace", PlayerEffectStat.GraceSeconds, PlayerEffectOperation.Multiply, 0.6f),
            new Mapping("thick-skin", PlayerEffectStat.GraceSeconds, PlayerEffectOperation.Add, 0.25f, 3),
            new Mapping("short-burst", PlayerEffectStat.BoostDuration, PlayerEffectOperation.Multiply, 0.5f),
            new Mapping("heavy-legs", PlayerEffectStat.BoostDuration, PlayerEffectOperation.Multiply, 0f),
            new Mapping("long-boost", PlayerEffectStat.BoostDuration, PlayerEffectOperation.AddFraction, 0.3f, 3),
            new Mapping("slow-mend", PlayerEffectStat.Regeneration, PlayerEffectOperation.Multiply, 0.5f),
            new Mapping("no-regen", PlayerEffectStat.Regeneration, PlayerEffectOperation.Multiply, 0f),
            new Mapping("field-kit", PlayerEffectStat.Regeneration, PlayerEffectOperation.AddFraction, 0.5f, 3),
            new Mapping("rough-start", PlayerEffectStat.FloorStartHealth, PlayerEffectOperation.Multiply, 0.5f),
            new Mapping("thin-skin", PlayerEffectStat.MaximumHealth, PlayerEffectOperation.Multiply, 0.75f),
            new Mapping("speed-boost", PlayerEffectStat.SprintSpeed, PlayerEffectOperation.Add, 0.4f, int.MaxValue),
            new Mapping("quick-start", PlayerEffectStat.GroundAcceleration, PlayerEffectOperation.AddFraction, 0.2f, 3),
            new Mapping("air-control", PlayerEffectStat.AirAcceleration, PlayerEffectOperation.AddFraction, 0.25f, 3),
            new Mapping("fast-hands", PlayerEffectStat.TraversalDuration, PlayerEffectOperation.Multiply, 0.85f, 3),
            new Mapping("longer-slide", PlayerEffectStat.SlideDuration, PlayerEffectOperation.Multiply, 1.3f),
            new Mapping("longer-slide", PlayerEffectStat.SlideRetention, PlayerEffectOperation.Add, 0.05f),
            new Mapping("higher-jump", PlayerEffectStat.JumpHeight, PlayerEffectOperation.Multiply, 1.2f),
            new Mapping("stored-momentum", PlayerEffectStat.StoredMomentum, PlayerEffectOperation.Add, 1f),
            new Mapping("soft-landing", PlayerEffectStat.SoftLanding, PlayerEffectOperation.Add, 1f),
            new Mapping("quiet-slide", PlayerEffectStat.QuietSlide, PlayerEffectOperation.Add, 1f),
            new Mapping("low-profile", PlayerEffectStat.LowProfile, PlayerEffectOperation.Add, 1f),
            new Mapping("no-look-back", PlayerEffectStat.NoLookBack, PlayerEffectOperation.Add, 1f)
        };
        public float HunterChaseSpeedCeiling => _hunterChaseSpeedCeiling;
        public float ChaseSpeedMargin => _chaseSpeedMargin;
        public float MinimumTraversalSeconds => _minimumTraversalSeconds;
        public float StandstillSpeed => _standstillSpeed;
        public float StoredMomentumWindow => _storedMomentumWindow;
        public IReadOnlyList<Mapping> Mappings => Array.AsReadOnly(_mappings ?? Array.Empty<Mapping>());
    }
}
