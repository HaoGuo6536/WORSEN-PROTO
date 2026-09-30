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
//   - Publish selected slots with uses and transact consumable/Extra Life admission once.
//   - Commit shrine costs and transient Player shield grants before publishing outcomes.
//   - Publish belief-drop/world-effect intent and delayed shared-hearing noise facts.
//   - Own persistent state and explicitly seeded new-run/replay initialization.
//   - Relay choices, purchases, ward consumption, health and floor lifecycle facts.
//   - Own the delegated shop through the controller; route rerolls and replacements.
//   - Load catalogue/shop assets, with an owned default catalogue when not yet wired.
//   - Forward the bail flag through the normal guarded floor-completion transaction.
//   - Publish Core snapshots and newly committed generation requests once.
//   - Pair each progression/inventory revision with a frozen active-effects view.
//   - Publish read-only before/after transactions for observational consumers.
// DEPENDENCIES:
//   - Progression Config, Controller and BehaviorState; Core progression types.
//   - Domain Player receives shield grants by a transient argument, never a retained scene reference.
// USAGE NOTES:
//   Persistent on its own root object. Initialize returns the canonical instance
//   and never resets an existing expedition. Bind listeners before StartRun.
//   Scene integration owns generation, teardown and readiness acknowledgment.
//   GenerationRequested reports that a request was committed, not an engine call.
//   TransactionCommitted reports accepted actions before other publication;
//   operation and choice identity describe facts without changing any rule.
//   Integration must supply bailed=true for an early escape; legacy calls remain penalty-free.
// ============================================================================
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression.Shop;
using Worsen.Domain.Player;

namespace Worsen.Session.Progression
{
    public sealed class ProgressionSessionManager : MonoBehaviour
    {
        private ProgressionSessionBehaviorState state;
        private ProgressionSessionController controller;
        private ProgressionConfig config;
        private EffectCatalogueConfig shopCatalogue;
        private ShopConfig shopConfig;
        private bool ownsCatalogue;
        public static ProgressionSessionManager Instance { get; private set; }
        public event Action<ProgressionSnapshot> SnapshotChanged;
        public event Action<ConsumableInventorySnapshot> ConsumablesChanged;
        public ConsumableInventorySnapshot Consumables => controller == null ? default : controller.Consumables();
        public event Action<ProgressionSnapshot, IReadOnlyActiveEffects> EffectsSnapshotChanged;
        public event Action<ProgressionGenerationRequest> GenerationRequested;
        public event Action<ShrineResolvedFact> ShrineResolved;
        public event Action<NoiseEvent> ShrineNoiseEmitted;
        public IReadOnlyActiveEffects FloorEffects => controller == null ? default(ActiveEffects) : controller.FloorEffects;
        public float ShrineYieldMultiplier => controller?.ShrineYieldMultiplier ?? 1f;
        public event Action<ProgressionSnapshot, ProgressionSnapshot, string, string> TransactionCommitted;
        public ProgressionSnapshot Snapshot => controller == null ? default : controller.Snapshot();
        public ProgressionEffectsSnapshot EffectsSnapshot => controller == null ? default : controller.EffectsSnapshot();

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
                shopConfig = config.ShopConfig ?? Resources.Load<ShopConfig>("ScriptableObjects/Session/Progression/Shop/ShopConfig");
                shopCatalogue = config.EffectCatalogue ?? Resources.Load<EffectCatalogueConfig>("ScriptableObjects/Session/Progression/EffectCatalogueConfig");
                if (shopCatalogue == null)
                {
                    shopCatalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
                    ownsCatalogue = true;
                }
                state = new ProgressionSessionBehaviorState();
                controller = new ProgressionSessionController(state, config, new System.Random(seed), shopConfig, shopCatalogue);
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return this;
        }

        public void StartRun(int seed)
        {
            RequireInitialized();
            ProgressionSnapshot previous = controller.Snapshot();
            int previousGeneration = state.GenerationId;
            controller = new ProgressionSessionController(state, config, new System.Random(seed), shopConfig, shopCatalogue);
            controller.StartRun(seed);
            TransactionCommitted?.Invoke(previous, controller.Snapshot(), nameof(StartRun), string.Empty);
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

        public bool ChooseThreat(string id, int revision) => Change(() => controller.ChooseThreat(id, revision), id);
        public bool ChooseCurse(string id, int revision) => Change(() => controller.ChooseCurse(id, revision), id);
        public bool TakeBargain(string id, int revision) => Change(() => controller.TakeBargain(id, revision), id);
        public bool Purchase(string id, int revision) => Change(() => controller.Purchase(id, revision), id);
        public bool RerollShop(int revision) => Change(() => controller.RerollShop(revision));
        public bool RerollSelection(int revision) => Change(() => controller.RerollSelection(revision));
        public bool ReplaceInventorySlot(int slot, int revision) => Change(() => controller.ReplaceInventorySlot(slot, revision),
            Snapshot.PendingOfferId, nameof(Purchase));
        public bool CancelReplacement(int revision) => Change(() => controller.CancelReplacement(revision));
        public bool ContinueShop(int revision) => Change(() => controller.ContinueShop(revision));
        public bool ConfirmFloorReady(int generationId) => Change(() => controller.ConfirmFloorReady(generationId));
        public bool FailGeneration(int generationId, string reason) => Change(() => controller.FailGeneration(generationId, reason));
        public bool CompleteFloor(int generationId) => Change(() => controller.CompleteFloor(generationId));
        public bool CompleteFloor(int generationId, bool bailed) => Change(() => controller.CompleteFloor(generationId, bailed),
            reason: bailed ? "EarlyBail" : nameof(CompleteFloor));
        public bool RecordGoldenCollected(int generationId, int anchorId) => Change(() => controller.RecordGoldenCollected(generationId, anchorId));
        public bool TryConsumeWaxWard(int generationId) => Change(() => controller.TryConsumeWaxWard(generationId));
        public bool CycleConsumable(int generationId, int direction) => Change(() => controller.CycleConsumable(generationId, direction));
        public bool TryConsumeSelected(int generationId, int revision, string id) => Change(() => controller.TryConsumeSelected(generationId, revision, id), id);
        public bool TryConsumeExtraLife(int generationId) => Change(() => controller.TryConsumeExtraLife(generationId));
        public bool RecordHealth(int generationId, float health) => Change(() => controller.RecordHealth(generationId, health));
        public bool EndRun(int generationId) => Change(() => controller.EndRun(generationId));

        public bool ActivateShrine(int generationId, ShrineActivatedFact fact, PlayerManager player, float collectedFraction = 0f)
        {
            RequireInitialized();
            var previous = controller.Snapshot();
            if (!controller.ActivateShrine(generationId, fact, player != null ? player.ShieldCapacity : 0f,
                collectedFraction, out var result)) return false;
            if (result.Shield > 0f && !player.GrantShield(result.Shield))
                throw new InvalidOperationException("Admitted shield grant failed before shrine publication.");
            TransactionCommitted?.Invoke(previous, controller.Snapshot(), nameof(ActivateShrine), fact.Kind.ToString());
            ShrineResolved?.Invoke(result);
            Publish(generationId);
            return true;
        }

        public void TickShrines(int generationId, float dt, long tick)
        {
            RequireInitialized();
            foreach (var noise in controller.TickShrines(generationId, dt, tick)) ShrineNoiseEmitted?.Invoke(noise);
        }

        private bool Change(Func<bool> action, string choiceId = "", [CallerMemberName] string reason = "")
        {
            RequireInitialized();
            ProgressionSnapshot previous = controller.Snapshot();
            int revision = state.Revision;
            int generation = state.GenerationId;
            bool accepted = action();
            if (accepted)
            {
                var current = controller.Snapshot();
                TransactionCommitted?.Invoke(previous, current,
                    reason == nameof(Purchase) && !string.IsNullOrEmpty(current.PendingOfferId) ? "ReservePurchase" : reason, choiceId);
            }
            if (state.Revision != revision) Publish(generation);
            return accepted;
        }

        private void Publish(int previousGeneration)
        {
            ProgressionEffectsSnapshot paired = controller.EffectsSnapshot();
            ProgressionSnapshot snapshot = paired.Progression;
            ProgressionGenerationRequest request = controller.GenerationRequest();
            ConsumableInventorySnapshot slots = controller.Consumables();
            SnapshotChanged?.Invoke(snapshot);
            // A legacy listener can synchronously commit a replacement revision.
            if (state.Revision != snapshot.Revision) return;
            ConsumablesChanged?.Invoke(slots);
            if (state.Revision != snapshot.Revision) return;
            EffectsSnapshotChanged?.Invoke(snapshot, paired.ActiveEffects);
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
            ConsumablesChanged = null;
            EffectsSnapshotChanged = null;
            GenerationRequested = null;
            ShrineResolved = null;
            ShrineNoiseEmitted = null;
            TransactionCommitted = null;
            controller = null;
            state = null;
            config = null;
            if (ownsCatalogue && shopCatalogue != null) Destroy(shopCatalogue);
            shopCatalogue = null; shopConfig = null; ownsCatalogue = false;
        }
    }
}
