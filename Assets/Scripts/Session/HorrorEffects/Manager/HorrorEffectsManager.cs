// ============================================================================
// HorrorEffectsManager.cs
// ============================================================================
// PURPOSE:
//   Hosts one authoritative flashlight and general-curse rule service.
//   It routes committed gameplay facts directly to registered actors and safe room
//   previews, while publishing Core values for presentation.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Session · HorrorEffects (Service system).
// KEY RESPONSIBILITIES:
//   - Sequence Progression item uses, Player effects and consumable lifetimes.
//   - Complete in-place revival once after the catch without resetting collapse.
//   - Route hand/ward/throw outcomes while rejecting revival-protected grabs.
//   - Own effect lifecycle, actor refresh, trap slows and world jam bindings.
//   - Publish sensory facts; only authorized item noise reaches hunter hearing.
// DEPENDENCIES:
//   Domain Level closes/breaks doors. Own Driver observes head bones and physics sweeps.
//   Core contracts; Domain Player/Hunter registries and managers; Domain Floor manager.
//   Domain Director receives environmental noise through the floor-scoped Session binding.
//   Session Progression consumes wards in the declared HorrorEffects -> Progression direction.
// USAGE NOTES:
//   Persistent service initialized explicitly by scene setup. The declared Session dependency
//   is HorrorEffects -> Progression; Domain Player/Floor/Hunter calls are downward.
//   Floor references exist only during ConfigureHazards binding; ClearHazards before load
//   or scene teardown, Suspend on death, and BeginFloor on each generation.
//   BindActors after assembly; Tick also discovers newly registered actor identities.
//   Perk refresh preserves active grab multipliers and never reapplies unchanged perks.
//   Hazard subscription pairs OnEnable/OnDisable; reconfiguration detaches the old floor.
//   Without a bound Director, legacy scenes use direct hunter hearing, never both paths.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Director;
using Worsen.Domain.Level;
using Worsen.Session.Progression;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Session.HorrorEffects
{
    public sealed class HorrorEffectsManager : MonoBehaviour
    {
        private HorrorEffectsController controller;
        private ConsumableController consumables;
        private HorrorEffectsConfig config;
        private HorrorEffectsDriver driver;
        private LevelManager level;
        private bool worldBound;
        private ProgressionSessionManager progression;
        private FloorManager floor;
        private DirectorManager director;
        private bool hazardsBound;
        public static HorrorEffectsManager Instance { get; private set; }
        public event Action<FlashlightSample> FlashlightChanged;
        public event Action<FlashlightSample, float> AfterimageChanged;
        public event Action<NoiseEvent> NoiseEmitted;
        public event Action<Vector3, float, float> FlameDimChanged;
        public event Action<int> OptionalRoomCracked;
        public event Action<int, Vector3> DoorMarked;
        public event Action<HunterStunFact> HunterStunned;
        public event Action<HunterSlipFact> HunterSlipped;
        public event Action<DoorJamFact> DoorJamChanged;
        public event Action<SensoryCleanseFact> SensesCleansed;
        public event Action<ConsumableUsedFact> ConsumableUsed;
        public event Action<EntityId> PlayerRevived;
        public bool FlashlightCharged => consumables != null && consumables.Charged;
        public bool FlashlightEnabled => controller != null && controller.FlashlightEnabled;
        public float FootstepLoudnessMultiplier => controller == null ? 1f : controller.FootstepLoudnessMultiplier;
        public float ReboundCooldownMultiplier => controller == null ? 1f : controller.ReboundCooldownMultiplier;
        public float OptionalWindowMultiplier => controller == null ? 1f : controller.OptionalWindowMultiplier;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public HorrorEffectsManager Initialize(HorrorEffectsConfig config)
        {
            if (Instance != null && Instance != this) { Destroy(this); return Instance; }
            if (controller == null) controller = new HorrorEffectsController(new HorrorEffectsBehaviorState(), config);
            this.config = config;
            if (consumables == null) consumables = new ConsumableController(new ConsumableBehaviorState(), config);
            if (driver == null) driver = gameObject.AddComponent<HorrorEffectsDriver>();
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return this;
        }
        public void BeginFloor(int generationId, ProgressionEffects effects) { Suspend(); controller?.BeginFloor(generationId, effects); consumables?.BeginFloor(); RefreshActors(); Publish(); }
        public void ResetRun() => consumables?.ResetRun();
        public void UpdateEffects(ProgressionEffects effects) { controller?.UpdateEffects(effects); RefreshActors(); Publish(); }
        public void ObserveAim(FlashlightSample aim) { controller?.ObserveAim(aim); Publish(); }
        public void ReceiveInput(InputFrame frame) { controller?.ReceiveInput(frame); consumables?.ReceiveInput(frame); Publish(); }
        public void ObserveMovement(PlayerMovementSample sample) { controller?.ObserveMovement(sample); Publish(); }
        public void ObserveTraversal(PlayerTraversalFact fact) { controller?.ObserveTraversal(fact); Publish(); }
        public void RecordGoldenCollected(EntityId source, Vector3 position, long tick)
        { controller?.RecordGoldenCollected(source, position, tick); Publish(); }
        public void SetOptionalRooms(int[] roomIds) => controller?.SetOptionalRooms(roomIds);
        public void RecordDoorCrossed(int doorId, Vector3 position) { controller?.RecordDoorCrossed(doorId, position); Publish(); }
        public void Tick(InputFrame frame, float dt, long tick) { RefreshActors(); controller?.Tick(frame, dt, tick); consumables?.ReceiveInput(frame); TickConsumables(frame, dt, tick); RefreshTrapSlows(); Publish(); }
        public void Tick(float dt, long tick) { RefreshActors(); controller?.Tick(dt, tick); TickConsumables(default, dt, tick); RefreshTrapSlows(); Publish(); }
        public void BindActors() => RefreshActors();
        public void RefreshActors()
        {
            if (controller == null) return;
            foreach (PlayerManager player in PlayerRegistry.Items)
                if (player != null && player.ReadOnlyState != null && player.ReadOnlyState.IsAlive &&
                    controller.TryGetActorEffects(player.Id, out float footsteps, out float rebound, out float grabSpeed))
                    player.SetMovementEffects(footsteps, rebound, grabSpeed);
            foreach (HunterManager hunter in HunterRegistry.Items)
                if (hunter != null && controller.TryGetHunterEffects(hunter.Id, out FlashlightSample light,
                    out FlashlightSample afterimage, out float lifetime))
                {
                    hunter.SetFlashlight(light);
                    hunter.SetAfterimage(afterimage, lifetime);
                }
        }
        public void Suspend()
        {
            foreach (PlayerManager player in PlayerRegistry.Items)
                if (player != null) { player.SetGrabSpeedMultiplier(1f); player.SetTrapSpeedMultiplier(1f); player.SetConsumableSpeedMultiplier(1f); }
            consumables?.Suspend();
            controller?.Suspend(); Publish(); PublishConsumables();
        }
        public void ConfigureHazards(ProgressionSessionManager progressionService, FloorManager floorService,
            DirectorManager directorService = null, LevelManager levelService = null)
        {
            ClearHazards();
            progression = progressionService;
            floor = floorService;
            director = directorService;
            ConfigureConsumableWorld(levelService);
            BindHazards();
        }
        public void ClearHazards()
        {
            ConfigureConsumableWorld(null);
            UnbindHazards();
            controller?.ClearTrapSlows();
            RefreshTrapSlows();
            floor = null;
            director = null;
            progression = null;
        }
        private void BindHazards()
        {
            if (hazardsBound || !isActiveAndEnabled || floor == null) return;
            floor.OnCollapseHand += HandleCollapseHand;
            floor.OnTrapSprung += HandleTrapSprung;
            hazardsBound = true;
        }
        private void UnbindHazards()
        {
            if (hazardsBound && floor != null)
            { floor.OnCollapseHand -= HandleCollapseHand; floor.OnTrapSprung -= HandleTrapSprung; }
            hazardsBound = false;
        }
        private void HandleCollapseHand(CollapseHandFact fact)
        {
            if (controller == null || !PlayerRegistry.TryGet(fact.PlayerId, out PlayerManager player)) return;
            if (player.ReadOnlyState == null) return;
            player.AdvanceRecovery(Math.Max(fact.Tick, player.ReadOnlyState.Tick));
            if (player.RevivalDamageImmune)
            {
                controller.ReleaseGrab(fact.PlayerId);
                player.SetGrabSpeedMultiplier(1f);
                // Only initiating facts can cancel a grab; release callbacks must not recurse.
                if (fact.Kind == CollapseHandEventKind.Warning || fact.Kind == CollapseHandEventKind.Grabbed)
                    floor?.CancelCollapseGrab(fact.PlayerId);
                return;
            }
            HorrorHazardResolution result = controller.ResolveHand(fact, player.ReadOnlyState != null && player.ReadOnlyState.IsAlive);
            if (!result.Accepted) return;
            FloorManager eventFloor = floor;
            if (result.TryWard && progression != null && progression.TryConsumeWaxWard(controller.GenerationId))
            {
                controller.ReleaseGrab(fact.PlayerId);
                player.SetGrabSpeedMultiplier(1f);
                eventFloor?.CancelCollapseGrab(fact.PlayerId);
                return;
            }
            player.SetGrabSpeedMultiplier(result.SpeedMultiplier);
            if (result.Damage <= 0f) return;
            bool accepted = player.ApplyHit(result.Damage, fact.Position, HitSeverity.Light, HitSource.Hand);
            if (player.ReadOnlyState != null && !player.ReadOnlyState.IsAlive)
                eventFloor?.ConfirmCollapseDeath(fact.PlayerId, fact.RoomId);
            else if (accepted && player.ReadOnlyState != null)
                player.ApplyExternalVelocity(fact.ThrowVelocity, ExternalMotionKind.CollapseHandThrow);
        }

        private void HandleTrapSprung(FloorTrapSprungFact fact)
        {
            if (fact.Kind != FloorTrapKind.Slow || controller == null ||
                !PlayerRegistry.TryGet(fact.PlayerId, out PlayerManager player) || player.ReadOnlyState?.IsAlive != true) return;
            if (controller.StartTrapSlow(fact.PlayerId, fact.TrapId))
                player.SetTrapSpeedMultiplier(controller.TrapSpeedMultiplier(fact.PlayerId));
        }

        private void RefreshTrapSlows()
        {
            if (controller == null) return;
            foreach (PlayerManager player in PlayerRegistry.Items)
                if (player != null) player.SetTrapSpeedMultiplier(controller.TrapSpeedMultiplier(player.Id));
        }

        private void Publish()
        {
            if (controller == null) return;
            foreach (HorrorEffectFact fact in controller.DrainFacts())
            {
                switch (fact.Kind)
                {
                    case HorrorEffectKind.Flashlight:
                        foreach (HunterManager hunter in HunterRegistry.Items) if (hunter != null) hunter.SetFlashlight(fact.Light);
                        FlashlightChanged?.Invoke(fact.Light); break;
                    case HorrorEffectKind.Afterimage:
                        foreach (HunterManager hunter in HunterRegistry.Items) if (hunter != null) hunter.SetAfterimage(fact.Light, fact.Value);
                        AfterimageChanged?.Invoke(fact.Light, fact.Value); break;
                    case HorrorEffectKind.Noise:
                        // Legacy curse echoes and world noises are presentation-only.
                        NoiseEmitted?.Invoke(fact.Noise); break;
                    case HorrorEffectKind.FlameDim: FlameDimChanged?.Invoke(fact.Position, fact.Radius, fact.Value); break;
                    case HorrorEffectKind.OptionalRoomCrack:
                        if (floor != null && floor.TelegraphOptionalRoom(fact.RoomId)) OptionalRoomCracked?.Invoke(fact.RoomId);
                        break;
                    case HorrorEffectKind.DoorMark: DoorMarked?.Invoke(fact.RoomId, fact.Position); break;
                }
            }
        }
        public void ConfigureConsumableWorld(LevelManager service)
        {
            UnbindWorld(); level = service; BindWorld();
        }
        private void BindWorld()
        { if (!worldBound && isActiveAndEnabled && level != null) { level.InteractableChanged += OnDoorChanged; worldBound = true; } }
        private void UnbindWorld()
        {
            if (worldBound && level != null) level.InteractableChanged -= OnDoorChanged;
            level?.ClearDoorJams(); worldBound = false;
        }
        private void OnDoorChanged(InteractableState before, InteractableState after)
        {
            if (consumables == null || !consumables.IsJammed(after.Id)) return;
            if (after.Value == InteractableStateValue.Broken) consumables.EndJam(after.Id, true, CurrentTick);

            PublishConsumables();
        }
        public bool IsDoorJammed(int id) => consumables != null && consumables.IsJammed(id);
        public bool CompleteDoorBreak(int id) => IsDoorJammed(id) && level != null && level.Break(id);
        private long CurrentTick => controller == null ? 0 : controller.CurrentLight.Tick;
        public bool TryBeginRevival(EntityId id)
        {
            if (consumables == null || !consumables.Active || consumables.RevivalPending || progression == null ||
                !PlayerRegistry.TryGet(id, out var player) || player.ReadOnlyState == null || player.ReadOnlyState.IsAlive ||
                !progression.TryConsumeExtraLife(controller.GenerationId)) return false;
            return consumables.BeginRevival(id);
        }
        public bool CompleteRevival(EntityId id)
        {
            if (consumables == null || !consumables.RevivalPending || !PlayerRegistry.TryGet(id, out var player) ||
                !consumables.CompleteRevival(id)) return false;
            controller.ClearTrapSlow(id); controller.ReleaseGrab(id); floor?.CancelCollapseGrab(id);
            if (!player.ReviveInPlace(consumables.RevivalHealthFraction)) return false;
            PlayerRevived?.Invoke(id);
            return true;
        }
        private void TickConsumables(InputFrame frame, float dt, long tick)
        {
            if (consumables == null || controller == null) return;
            FlashlightSample light = controller.CurrentLight;
            PlayerRegistry.TryGet(light.Source, out var player);
            var faces = new List<HunterFaceSample>();
            foreach (var hunter in HunterRegistry.Items)
            {
                if (hunter == null || hunter.ReadOnlyState?.IsActive != true) continue;
                Vector3 head = driver.Head(hunter.gameObject, config.FallbackHeadHeight);
                faces.Add(new HunterFaceSample(hunter.Id, head, hunter.ReadOnlyState.Position,
                    player != null && driver.Visible(light.Origin, head, player.gameObject, hunter.gameObject)));
            }
            int stacks = progression == null ? 0 : progression.EffectsSnapshot.ActiveEffects.Stacks(new EffectId("steady-hand"));
            if (!consumables.Tick(dt, tick, light, faces, player?.ReadOnlyState?.Velocity ?? Vector3.zero,
                player?.ReadOnlyState?.IsAlive == true, stacks, out float healingSeconds)) return;
            if (player != null)
            {
                player.HealOverTime(consumables.HealingRate, healingSeconds);
                if (progression != null && player.ReadOnlyState.IsAlive)
                {
                    int direction = ConsumableController.CycleDirection(frame);
                    if (direction != 0) progression.CycleConsumable(controller.GenerationId, direction);
                    if ((frame.Pressed & InputButtons.UseConsumable) != 0) UseSelected(player, light, faces, tick);
                }
                player.SetConsumableSpeedMultiplier(consumables.SpeedMultiplier);
            }
            foreach (var flight in consumables.AdvanceThrows(dt))
                if (driver.Impact(flight.From, flight.To, player != null ? player.gameObject : null, out Vector3 point)) consumables.Impact(flight.Id, point, tick);
            PublishConsumables();
        }
        private void UseSelected(PlayerManager player, FlashlightSample aim, IReadOnlyList<HunterFaceSample> faces, long tick)
        {
            var slots = progression.Consumables;
            if (slots.Inventory == null || slots.SelectedIndex < 0 || slots.SelectedIndex >= slots.Inventory.Count) return;
            string id = slots.Inventory[slots.SelectedIndex].Id;
            var doors = new List<InteractableState>();
            if (level != null && level.ReadOnlyState.IsReady)
                foreach (var room in level.ReadOnlyState.Graph.Rooms) doors.AddRange(level.Interactables.InRoom(room.Id));
            if (!consumables.CanUse(id, player.ReadOnlyState.Health, player.ReadOnlyState.MaxHealth, aim, faces, doors,
                player.ReadOnlyState.Position, out EntityId target, out int door)) return;
            Vector3 doorPosition = Vector3.zero;
            if (door > 0)
            {
                if (level == null || !level.Interactables.TryGet(door, out var observed)) return;
                if (observed.Value != InteractableStateValue.Inactive && !level.CloseDoor(door)) return;
                doorPosition = observed.Position;
            }
            if (!progression.TryConsumeSelected(controller.GenerationId, progression.Snapshot.Revision, id)) return;
            consumables.CommitUse(id, aim, player.ReadOnlyState.Position, target, door, doorPosition, tick);
            PublishConsumables();
            if (id == "smelling-salts")
            {
                controller.ClearTrapSlow(player.Id); player.ClearSlows();
                SensesCleansed?.Invoke(new SensoryCleanseFact(player.Id, tick));
            }
            ConsumableUsed?.Invoke(new ConsumableUsedFact(player.Id, id, tick));
        }
        private void PublishConsumables()
        {
            if (consumables == null) return;
            foreach (var fact in consumables.DrainStuns()) HunterStunned?.Invoke(fact);
            foreach (var fact in consumables.DrainSlips()) HunterSlipped?.Invoke(fact);
            foreach (var fact in consumables.DrainDoors())
            { level?.SetDoorJammed(fact.DoorId, fact.Active); DoorJamChanged?.Invoke(fact); }
            foreach (var fact in consumables.DrainNoiseFacts())
            {
                var noise = fact.Noise;
                if (HorrorNoiseUtility.HunterAudible(fact))
                {
                    if (director != null) director.HearNoise(noise);
                    else foreach (var hunter in HunterRegistry.Items) if (hunter != null) hunter.HearNoise(noise);
                }
                NoiseEmitted?.Invoke(noise);
            }
        }
        private void OnEnable() { BindHazards(); BindWorld(); }
        private void OnDisable() { UnbindHazards(); UnbindWorld(); Suspend(); }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            FlashlightChanged = null;
            AfterimageChanged = null;
            NoiseEmitted = null;
            FlameDimChanged = null;
            OptionalRoomCracked = null;
            DoorMarked = null;
            HunterStunned = null; HunterSlipped = null; DoorJamChanged = null; SensesCleansed = null; ConsumableUsed = null; PlayerRevived = null;
            controller = null;
            ClearHazards();
        }
    }
}
