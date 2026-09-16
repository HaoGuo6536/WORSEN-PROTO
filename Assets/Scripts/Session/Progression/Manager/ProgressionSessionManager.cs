// ============================================================================
// ProgressionSessionManager.cs
// ============================================================================
// PURPOSE:
//   Keeps one expedition alive while its physical floors are replaced.
//   It delegates every rule to its controller and publishes accepted state
//   changes so scene integration and menus can act on the same committed facts.
// ARCHITECTURAL ROLE:
//   Manager (§1, §8b) · Session · Progression (Session system).
// KEY RESPONSIBILITIES:
//   - Own persistent state and explicitly seeded new-run/replay initialization.
//   - Relay choices, purchases, ward consumption, health and floor lifecycle facts.
//   - Publish Core snapshots and newly committed generation requests once.
// DEPENDENCIES:
//   - Progression Config, Controller and BehaviorState; Core progression types.
//   - Unity persistence lifecycle only; no other system or scene references.
// USAGE NOTES:
//   Persistent on its own root object. Initialize returns the canonical instance
//   and never resets an existing expedition. Bind listeners before StartRun.
//   Scene integration owns generation, teardown and readiness acknowledgment.
//   GenerationRequested reports that a request was committed, not an engine call.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Session.Progression
{
    public sealed class ProgressionSessionManager : MonoBehaviour
    {
        private ProgressionSessionBehaviorState state;
        private ProgressionSessionController controller;
        private ProgressionConfig config;
        public static ProgressionSessionManager Instance { get; private set; }
        public event Action<ProgressionSnapshot> SnapshotChanged;
        public event Action<ProgressionGenerationRequest> GenerationRequested;
        public ProgressionSnapshot Snapshot => controller == null ? default : controller.Snapshot();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public ProgressionSessionManager Initialize(ProgressionConfig configuration, int seed)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return Instance;
            }
            if (controller == null)
            {
                config = configuration ?? throw new ArgumentNullException(nameof(configuration));
                state = new ProgressionSessionBehaviorState();
                controller = new ProgressionSessionController(state, config, new System.Random(seed));
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return this;
        }

        public void StartRun(int seed)
        {
            RequireInitialized();
            int previousGeneration = state.GenerationId;
            controller = new ProgressionSessionController(state, config, new System.Random(seed));
            controller.StartRun(seed);
            Publish(previousGeneration);
        }

        // The one-argument API deliberately replays the current expedition seed.
        public bool RestartRun(int revision) => RestartRun(revision, Snapshot.Seed);

        public bool RestartRun(int revision, int seed)
        {
            RequireInitialized();
            ProgressionSnapshot snapshot = controller.Snapshot();
            if (snapshot.Revision != revision || !snapshot.CanRestart) return false;
            StartRun(seed);
            return true;
        }

        public bool ChooseThreat(string id, int revision) => Change(() => controller.ChooseThreat(id, revision));
        public bool ChooseCurse(string id, int revision) => Change(() => controller.ChooseCurse(id, revision));
        public bool Purchase(string id, int revision) => Change(() => controller.Purchase(id, revision));
        public bool ContinueShop(int revision) => Change(() => controller.ContinueShop(revision));
        public bool ConfirmFloorReady(int generationId) => Change(() => controller.ConfirmFloorReady(generationId));
        public bool FailGeneration(int generationId, string reason) => Change(() => controller.FailGeneration(generationId, reason));
        public bool CompleteFloor(int generationId) => Change(() => controller.CompleteFloor(generationId));
        public bool RecordGoldenCollected(int generationId, int anchorId) => Change(() => controller.RecordGoldenCollected(generationId, anchorId));
        public bool TryConsumeWaxWard(int generationId) => Change(() => controller.TryConsumeWaxWard(generationId));
        public bool RecordHealth(int generationId, float health) => Change(() => controller.RecordHealth(generationId, health));
        public bool EndRun(int generationId) => Change(() => controller.EndRun(generationId));

        private bool Change(Func<bool> action)
        {
            RequireInitialized();
            int revision = state.Revision;
            int generation = state.GenerationId;
            bool accepted = action();
            if (state.Revision != revision) Publish(generation);
            return accepted;
        }

        private void Publish(int previousGeneration)
        {
            ProgressionSnapshot snapshot = controller.Snapshot();
            ProgressionGenerationRequest request = controller.GenerationRequest();
            SnapshotChanged?.Invoke(snapshot);
            if (request.GenerationId != previousGeneration && state.GenerationId == request.GenerationId &&
                state.Phase == ProgressionPhase.Generating)
                GenerationRequested?.Invoke(request);
        }

        private void RequireInitialized()
        {
            if (controller == null || Instance != this)
                throw new InvalidOperationException("Initialize and use the canonical Progression Session first.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SnapshotChanged = null;
            GenerationRequested = null;
            controller = null;
            state = null;
            config = null;
        }
    }
}
