// ============================================================================
// ShrineConfig.cs
// ============================================================================
// PURPOSE:
//   Authors shrine admission, moving-contact ranges and the provisional depth curve.
//   Event schedulers can exclude fear axes without adding a dependency on Session.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Shrine.
// KEY RESPONSIBILITIES:
//   - Keep each kind's availability and fear axis in designer-owned data.
//   - Tune the base count cap and the separate More Shrines bonus.
// DEPENDENCIES:
//   - Core shrine/fear values and Unity serialization only.
// USAGE NOTES:
//   Base cap applies before the one-upgrade bonus; defaults permit four with More Shrines.
//   Sites are supplied by assembly; this system never generates rooms or gap geometry.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Shrine
{
    [Serializable]
    public sealed class ShrineAvailability
    {
        [SerializeField] private ShrineKind _kind;
        [SerializeField] private int _floor;
        [SerializeField] private FearAxis _axis;
        public ShrineAvailability(ShrineKind kind, int floor, FearAxis axis)
        { _kind = kind; _floor = floor; _axis = axis; }
        public ShrineKind Kind => _kind;
        public int Floor => _floor;
        public FearAxis Axis => _axis;
    }

    [CreateAssetMenu(menuName = "Worsen/Shrine/Shrine Config")]
    public sealed class ShrineConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _minimumSpeed = 0.1f;
        [SerializeField, Min(0f)] private float _contactRadius = 0.8f;
        [SerializeField, Min(0f)] private float _interactRadius = 2f;
        [SerializeField, Min(0f)] private float _verticalTolerance = 1.5f;
        [SerializeField] private int[] _countFloors = { 3, 6, 10 };
        [SerializeField, Min(0)] private int _baseCap = 3;
        [SerializeField, Min(0)] private int _moreShrinesBonus = 1;
        [SerializeField] private ShrineAvailability[] _availability =
        {
            new ShrineAvailability(ShrineKind.Chance, 3, FearAxis.Unpredictability),
            new ShrineAvailability(ShrineKind.Bargain, 3, FearAxis.Stakes),
            new ShrineAvailability(ShrineKind.Pacification, 5, FearAxis.Information),
            new ShrineAvailability(ShrineKind.Wick, 6, FearAxis.Information),
            new ShrineAvailability(ShrineKind.Passage, 8, FearAxis.Time),
            new ShrineAvailability(ShrineKind.Protection, 8, FearAxis.Stakes),
            new ShrineAvailability(ShrineKind.Echo, 8, FearAxis.Unpredictability),
            new ShrineAvailability(ShrineKind.Purgatory, 8, FearAxis.Stakes)
        };
        public float MinimumSpeed => _minimumSpeed;
        public float ContactRadius => _contactRadius;
        public float InteractRadius => _interactRadius;
        public float VerticalTolerance => _verticalTolerance;
        public IReadOnlyList<int> CountFloors => Array.AsReadOnly(_countFloors);
        public int BaseCap => _baseCap;
        public int MoreShrinesBonus => _moreShrinesBonus;
        public IReadOnlyList<ShrineAvailability> Availability => Array.AsReadOnly(_availability);
    }
}
