// ============================================================================
// ExpeditionSessionBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores the identity of the floor being assembled and the entities it owns.
//   It contains no scene references, wallet, choices or health authority, so
//   replacing geometry cannot reset progression or retain destroyed objects.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Retain accepted run mutations and floor-scoped per-archetype duplicate counters.
//   - Retain pre-initialization puzzle/freeze facts and deduplicate committed puzzle ticks.
//   - Retain shield transfer, lamp restoration and physical Golden Cake collection accounting.
//   - Retain the pending request and assembly phase across the teardown yield.
//   - Track immutable room presentation and genuine portal crossings for floor-scoped marks.
//   - Track factory identities for complete, idempotent cleanup.
//   - Retain generation fallback evidence and capacity shortfalls after failed assembly.
// DEPENDENCIES:
//   - Core progression, scene and entity definitions; System collections.
// USAGE NOTES:
//   Persistent only through ExpeditionSessionManager. Its Controller alone
//   changes this data; other systems receive primitive Manager properties.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Session.Expedition
{
    public sealed class ExpeditionSessionBehaviorState
    {
        public ExpeditionAssemblyPhase Phase { get; internal set; }
        public SceneKey Scene { get; internal set; }
        public int LastGenerationId { get; internal set; }
        public ProgressionGenerationRequest Request { get; internal set; }
        public EntityId Player { get; internal set; }
        internal IReadOnlyList<GeneratedRoomSample> Rooms = System.Array.Empty<GeneratedRoomSample>();
        internal bool HasPreviousPosition;
        internal readonly List<int> FreezeAnchors = new List<int>();
        internal readonly List<int> FreezeBehindRooms = new List<int>();
        internal readonly Dictionary<int, (int Anchor, UnityEngine.Vector3 Position)> PuzzleRewards =
            new Dictionary<int, (int, UnityEngine.Vector3)>();
        internal string HandLook;
        internal PlayerMovementSample PuzzleMovement;
        internal long PuzzleTick = -1;
        internal long PuzzleVaultTick = -1;
        internal readonly HashSet<int> PuzzleGoldenEligible = new HashSet<int>();
        internal UnityEngine.Vector3 PreviousPosition;
        internal int PreviousRoom;
        public string Failure { get; internal set; } = string.Empty;
        public bool UsedFallback { get; internal set; }
        public string LayoutManifest { get; internal set; } = string.Empty;
        public int HunterSpawnShortfall { get; internal set; }
        internal List<EntityId> Hunters { get; } = new List<EntityId>();
        internal readonly Dictionary<string, int> NextDuplicate = new Dictionary<string, int>(System.StringComparer.Ordinal);
        internal readonly Dictionary<string, Dictionary<HunterTunable, HunterMutation>> Mutations =
            new Dictionary<string, Dictionary<HunterTunable, HunterMutation>>(System.StringComparer.Ordinal);
        internal float CarriedShield;
        internal bool ShieldTransferAllowed;
        internal float WickRemaining;
        internal long WickTick = -1;
        internal readonly Dictionary<int, bool> LampStates = new Dictionary<int, bool>();
        internal readonly HashSet<int> RequiredAnchors = new HashSet<int>();
        internal readonly HashSet<int> GoldenEligible = new HashSet<int>();
        internal readonly HashSet<int> GoldenCollected = new HashSet<int>();
        internal bool BlindFaith, GoldCreated;
        internal readonly HashSet<int> ResolvedShrines = new HashSet<int>();
    }
}
