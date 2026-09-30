// ============================================================================
// HorrorMicroEventDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains the run admission schedule separately from floor-local candidates.
//   Safe world observations are injected; no scene discovery or gameplay state lives here.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Store seeded randomness, chase membership, provenance and placeholder lifetime.
// DEPENDENCIES:
//   - Core read-only interactables and Unity value/reference data only.
// USAGE NOTES:
//   Owned by HorrorMicroEventDriver. Floor resets preserve run admission history.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Horror
{
    public sealed class HorrorMicroEventDriverState
    {
        public System.Random Random;
        public int Used;
        public bool ChaseKnown;
        public double NextSeconds, LastSeconds = double.NegativeInfinity;
        public readonly HashSet<int> Chases = new HashSet<int>();
        public readonly Dictionary<int, Bounds> OpenedDoors = new Dictionary<int, Bounds>();
        public IReadOnlyInteractableSet World;
        public Vector3[] UnreachableAnchors = new Vector3[0];
        public bool CounterAvailable;
        public GameObject Silhouette;
        public float SilhouetteRemaining;
    }
}
