// ============================================================================
// HorrorManager.cs
// ============================================================================
//
// PURPOSE:
//   Exposes the dark-room flashlight and attack warning command surface.
//   It forwards already-decided presentation facts and leaves world rendering and sound to its Driver.
//   It exposes Horror's whole-run gameplay clock without depending on Session or engine time.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Horror (Service system).
//
// KEY RESPONSIBILITIES:
//   - Forward room collapse, Blinder flights, Weaver warnings and Afterglow windows.
//   - Forward injected candidates/effects and publish micro-event outcomes.
//   - Forward authoritative aim and afterimages without taking gameplay ownership.
//   - Forward resets, clock deltas, startle admission and lighting hooks.
//   - Pair lifecycle boundaries with rendering restoration.
//
// DEPENDENCIES:
//   - Core HunterAttackSample and EntityId; its own Horror presentation stack.
//
// USAGE NOTES:
//   Scene-owned; exactly one service per assembled gameplay scene, with no singleton.
//   The scene setup explicitly wires the Driver's output camera, fog Volume and optional daylights.
//   RunElapsedSeconds is NaN until initialized; ResetRound preserves the clock and ResetRun zeroes it.
//
// ============================================================================

using UnityEngine;
using System;
using System.Collections.Generic;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Horror
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HorrorDriver))]
    public sealed class HorrorManager : MonoBehaviour
    {
        [SerializeField] private HorrorDriverConfig _config;
        [SerializeField] private HorrorDriver _driver;
        public event Action<int, int, Vector3, float> MicroEventSelected;
        public event Action<int, int, Vector3, float, bool> MicroEventOccurred;
        public event Action<float, bool> LightingHooksChanged;
        public float TorchCountMultiplier => _driver != null ? _driver.TorchCountMultiplier : 1f;
        public bool Wick => _driver != null && _driver.Wick;
        public bool IsReady => _driver != null && _driver.IsReady;
        public bool FlashlightEnabled => _driver != null && _driver.FlashlightEnabled;
        public float FogCurveStart => _driver != null ? _driver.FogCurveStart : 0f;
        public float FogCurveEnd => _driver != null ? _driver.FogCurveEnd : 0f;
        public float FlashlightRange => _driver != null ? _driver.FlashlightRange : 0f;
        public double RunElapsedSeconds => _driver != null ? _driver.RunElapsedSeconds : double.NaN;

        private void Awake() { if (_driver == null) _driver = GetComponent<HorrorDriver>(); }

        public HorrorManager Initialize(HorrorDriverConfig config)
        {
            if (_driver == null) _driver = GetComponent<HorrorDriver>();
            if (config != null) _config = config;
            _driver.Initialize(_config);
            _driver.SetOwnerEnabled(isActiveAndEnabled);
            if (isActiveAndEnabled) OnEnable();
            return this;
        }

        public void SetFlashlight(FlashlightSample sample) { if (_driver != null) _driver.SetFlashlight(sample); }
        public void SetAfterimage(FlashlightSample sample, float lifetime) { if (_driver != null) _driver.SetAfterimage(sample, lifetime); }
        public void SetAfterglow(InteractableState light, float safetySeconds)
        { if (_driver != null && isActiveAndEnabled) _driver.SetAfterglow(light, safetySeconds); }
        public void ToggleFlashlight() { if (_driver != null && isActiveAndEnabled) _driver.ToggleFlashlight(); }
        public void SetEffects(float fogMultiplier, float flashlightMultiplier)
        { if (_driver != null) _driver.SetEffects(fogMultiplier, flashlightMultiplier); }
        public void ResetRound() { if (_driver != null) _driver.ResetRound(); }
        public void SetCollapseRooms(IReadOnlyList<GeneratedRoomSample> rooms, int? exitRoom)
        { if (_driver != null) _driver.SetCollapseRooms(rooms, exitRoom); }
        public void ObserveCollapse(RoomDestructionSample sample) { if (_driver != null) _driver.ObserveCollapse(sample); }
        public void SetActiveEffects(IReadOnlyActiveEffects effects) { if (_driver != null) _driver.SetActiveEffects(effects); }
        public void SetMicroEventWorld(IReadOnlyInteractableSet world, IReadOnlyList<Vector3> unreachableAnchors)
        { if (_driver != null) _driver.SetMicroEventWorld(world, unreachableAnchors); }
        public void ObservePlayerOpenedDoor(int id, Bounds bounds) { if (_driver != null) _driver.ObservePlayerOpenedDoor(id, bounds); }
        public void SetMicroEventChase(int id, bool active) { if (_driver != null) _driver.SetMicroEventChase(id, active); }
        public void ObserveMicroEventProximity(ProximitySample sample) { if (_driver != null) _driver.ObserveMicroEventProximity(sample); }
        public void InvalidateMicroEventChase() { if (_driver != null) _driver.InvalidateMicroEventChase(); }
        public void SetCounterAvailable(bool available) { if (_driver != null) _driver.SetCounterAvailable(available); }
        public bool ShowMicroSilhouette(Vector3 position, float seconds)
            => _driver != null && _driver.ShowMicroSilhouette(position, seconds);
        public void ReportMicroEvent(int kind, int target, Vector3 position, float seconds, bool applied)
            => MicroEventOccurred?.Invoke(kind, target, position, seconds, applied);
        public void ResetRun(int seed) { if (_driver != null) _driver.ResetRun(seed); }
        public bool AdvanceRunClock(float deltaSeconds)
            => _driver != null && isActiveAndEnabled && _driver.AdvanceRunClock(deltaSeconds);
        public bool TryStartle(double runSeconds, bool earned)
            => _driver != null && isActiveAndEnabled && _driver.TryStartle(runSeconds, earned);
        public void SetLightingHooks(bool darkerFloors, bool catEyes)
        { if (_driver != null) _driver.SetLightingHooks(darkerFloors, catEyes); }
        public void SetAttack(HunterAttackSample sample)
        { if (_driver != null && isActiveAndEnabled) _driver.SetAttack(sample); }
        public void ObserveWeaver(WeaverFact fact)
        { if (_driver != null && isActiveAndEnabled) _driver.ObserveWeaver(fact); }
        public void RemoveAttack(EntityId hunter) { if (_driver != null) _driver.RemoveAttack(hunter); }
        public void LaunchProjectile(BlinderThrowFact fact) { if (_driver != null && isActiveAndEnabled) _driver.LaunchProjectile(fact); }
        public void HitProjectile(BlinderHitFact fact) { if (_driver != null && isActiveAndEnabled) _driver.HitProjectile(fact); }
        public void TickProjectiles(float delta) { if (_driver != null && isActiveAndEnabled) _driver.TickProjectiles(delta); }
        public void ClearProjectiles() { if (_driver != null) _driver.ClearProjectiles(); }
        public void Teardown() { OnDisable(); if (_driver != null) _driver.Teardown(); }

        private void OnEnable()
        {
            if (_driver == null) return;
            _driver.MicroEventSelected -= OnMicroEventSelected;
            _driver.LightingHooksChanged -= OnLightingHooksChanged;
            _driver.MicroEventSelected += OnMicroEventSelected;
            _driver.LightingHooksChanged += OnLightingHooksChanged;
            _driver.SetOwnerEnabled(true);
        }
        private void OnDisable()
        {
            if (_driver == null) return;
            _driver.MicroEventSelected -= OnMicroEventSelected;
            _driver.LightingHooksChanged -= OnLightingHooksChanged;
            _driver.SetOwnerEnabled(false);
        }
        private void OnMicroEventSelected(int kind, int target, Vector3 position, float seconds)
            => MicroEventSelected?.Invoke(kind, target, position, seconds);
        private void OnLightingHooksChanged(float torches, bool wick) => LightingHooksChanged?.Invoke(torches, wick);
        private void OnDestroy() => Teardown();
    }
}
