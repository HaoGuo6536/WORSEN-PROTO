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
//   Own the controller lifecycle and publish light, noise and spatial-effect facts.
//   Sequence Domain Player/Floor hand outcomes and Progression ward consumption.
//   Apply perk revisions once per registered player and light/noise facts to hunters.
// DEPENDENCIES:
//   Core contracts; Domain Player/Hunter registries and managers; Domain Floor manager.
//   Session Progression consumes wards in the declared HorrorEffects -> Progression direction.
// USAGE NOTES:
//   Persistent service initialized explicitly by scene setup. The declared Session dependency
//   is HorrorEffects -> Progression; Domain Player/Floor/Hunter calls are downward.
//   Floor references exist only during ConfigureHazards binding; ClearHazards before load
//   or scene teardown, Suspend on death, and BeginFloor on each generation.
//   BindActors after assembly; Tick also discovers newly registered actor identities.
//   Perk refresh preserves active grab multipliers and never reapplies unchanged perks.
//   Hazard subscription pairs OnEnable/OnDisable; reconfiguration detaches the old floor.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Session.Progression;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Session.HorrorEffects
{
    public sealed class HorrorEffectsManager : MonoBehaviour
    {
        private HorrorEffectsController controller;
        private ProgressionSessionManager progression;
        private FloorManager floor;
        private bool hazardsBound;
        public static HorrorEffectsManager Instance { get; private set; }
        public event Action<FlashlightSample> FlashlightChanged;
        public event Action<FlashlightSample, float> AfterimageChanged;
        public event Action<NoiseEvent> NoiseEmitted;
        public event Action<Vector3, float, float> FlameDimChanged;
        public event Action<int> OptionalRoomCracked;
        public event Action<int, Vector3> DoorMarked;
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
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return this;
        }
        public void BeginFloor(int generationId, ProgressionEffects effects) { controller?.BeginFloor(generationId, effects); RefreshActors(); Publish(); }
        public void UpdateEffects(ProgressionEffects effects) { controller?.UpdateEffects(effects); RefreshActors(); Publish(); }
        public void ObserveAim(FlashlightSample aim) { controller?.ObserveAim(aim); Publish(); }
        public void ReceiveInput(InputFrame frame) { controller?.ReceiveInput(frame); Publish(); }
        public void ObserveMovement(PlayerMovementSample sample) { controller?.ObserveMovement(sample); Publish(); }
        public void ObserveTraversal(PlayerTraversalFact fact) { controller?.ObserveTraversal(fact); Publish(); }
        public void RecordGoldenCollected(EntityId source, Vector3 position, long tick)
        { controller?.RecordGoldenCollected(source, position, tick); Publish(); }
        public void SetOptionalRooms(int[] roomIds) => controller?.SetOptionalRooms(roomIds);
        public void RecordDoorCrossed(int doorId, Vector3 position) { controller?.RecordDoorCrossed(doorId, position); Publish(); }
        public void Tick(InputFrame frame, float dt, long tick) { RefreshActors(); controller?.Tick(frame, dt, tick); Publish(); }
        public void Tick(float dt, long tick) { RefreshActors(); controller?.Tick(dt, tick); Publish(); }
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
            foreach (PlayerManager player in PlayerRegistry.Items) player.SetGrabSpeedMultiplier(1f);
            controller?.Suspend(); Publish();
        }
        public void ConfigureHazards(ProgressionSessionManager progressionService, FloorManager floorService)
        {
            ClearHazards();
            progression = progressionService;
            floor = floorService;
            BindHazards();
        }
        public void ClearHazards()
        {
            UnbindHazards();
            floor = null;
            progression = null;
        }
        private void BindHazards()
        {
            if (hazardsBound || !isActiveAndEnabled || floor == null) return;
            floor.OnCollapseHand += HandleCollapseHand;
            hazardsBound = true;
        }
        private void UnbindHazards()
        {
            if (hazardsBound && floor != null) floor.OnCollapseHand -= HandleCollapseHand;
            hazardsBound = false;
        }
        private void HandleCollapseHand(CollapseHandFact fact)
        {
            if (controller == null || !PlayerRegistry.TryGet(fact.PlayerId, out PlayerManager player)) return;
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
            player.ApplyHit(result.Damage, fact.Position);
            if (player.ReadOnlyState != null && !player.ReadOnlyState.IsAlive)
                eventFloor?.ConfirmCollapseDeath(fact.PlayerId, fact.RoomId);
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
                        foreach (HunterManager hunter in HunterRegistry.Items) if (hunter != null) hunter.HearNoise(fact.Noise);
                        NoiseEmitted?.Invoke(fact.Noise); break;
                    case HorrorEffectKind.FlameDim: FlameDimChanged?.Invoke(fact.Position, fact.Radius, fact.Value); break;
                    case HorrorEffectKind.OptionalRoomCrack:
                        if (floor != null && floor.TelegraphOptionalRoom(fact.RoomId)) OptionalRoomCracked?.Invoke(fact.RoomId);
                        break;
                    case HorrorEffectKind.DoorMark: DoorMarked?.Invoke(fact.RoomId, fact.Position); break;
                }
            }
        }
        private void OnEnable() => BindHazards();
        private void OnDisable() { UnbindHazards(); Suspend(); }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            FlashlightChanged = null;
            AfterimageChanged = null;
            NoiseEmitted = null;
            FlameDimChanged = null;
            OptionalRoomCracked = null;
            DoorMarked = null;
            controller = null;
            ClearHazards();
        }
    }
}
