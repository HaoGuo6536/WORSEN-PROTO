// ============================================================================
// AudioManager.cs
// ============================================================================
//
// PURPOSE:
//   Provides one persistent command surface for audible run feedback.
//   Explicit initialization returns the canonical service across repeated scene
//   creation, keeping event routing separate from source playback and mixer math.
//   Emitter, threat and portal facts drive the spatial soundscape without scene queries.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Audio (Service system).
//   Owns AudioDriver and forwards Core facts into its presentation stack.
//
// KEY RESPONSIBILITIES:
//   - Establish exactly one persistent Audio service and retire duplicate roots.
//   - Forward cue, movement, fractional injury and proximity commands without gameplay rules.
//   - Pair owner enable/disable and teardown with the owned Driver lifetime.
//
// DEPENDENCIES:
//   - Core CueId and MovementState; Audio system's own Driver and DriverConfig.
//
// USAGE NOTES:
//   - Persistent (DontDestroyOnLoad); requires a dedicated root GameObject.
//   - Scene assembly must retain the canonical instance returned by Initialize.
//   - ResetRun releases all previous run playback; no scene-owned references are cached.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioDriver))]
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioDriverConfig _config;
        [SerializeField] private AudioDriver _driver;
        private bool _initialized;
        public static AudioManager Instance { get; private set; }
        public float BreathGain => _driver != null ? _driver.BreathGain : 0f;
        public float HunterGain => _driver != null ? _driver.HunterGain : 0f;

        public AudioManager Initialize()
        {
            if (Instance != null && Instance != this)
            {
                AudioManager canonical = Instance;
                enabled = false;
                var duplicate = GetComponent<AudioDriver>();
                if (duplicate != null) { duplicate.Teardown(); duplicate.enabled = false; }
                Destroy(gameObject);
                return canonical;
            }
            Instance = this;
            if (_initialized) return this;
            DontDestroyOnLoad(gameObject);
            if (_driver == null) _driver = GetComponent<AudioDriver>();
            _driver.Initialize(_config);
            _initialized = _driver.IsInitialized;
            if (_initialized) _driver.SetOwnerEnabled(isActiveAndEnabled);
            return this;
        }

        public void PlayCue(CueId cue) { if (_initialized && isActiveAndEnabled) _driver.PlayCue(cue); }
        public void SetProximity(float closeness) { if (_initialized) _driver.SetProximity(closeness); }
        public void SetMovementState(MovementState movement) { if (_initialized) _driver.SetMovementState(movement); }
        public void SetSpeedNormalized(float speed) { if (_initialized) _driver.SetSpeedNormalized(speed); }
        public void SetInjury(float currentHealth, float maxHealth) { if (_initialized) _driver.SetInjury(currentHealth, maxHealth); }
        public void ObserveMovement(PlayerMovementSample sample) { if (_initialized && isActiveAndEnabled) _driver.ObserveMovement(sample); }
        public void ObserveTraversal(PlayerTraversalFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveTraversal(fact); }
        public void ObserveHunterFeedback(HunterFeedbackEvent fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveHunterFeedback(fact); }
        public void ObservePickup(PickupCollectedFact fact, Vector3 position) { if (_initialized && isActiveAndEnabled) _driver.ObservePickup(fact, position); }
        public void ObserveHand(CollapseHandFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveHand(fact); }
        public void ObserveRoom(RoomDestructionSample sample, Vector3 position) { if (_initialized && isActiveAndEnabled) _driver.ObserveRoom(sample, position); }
        public void ObserveHealth(EntityId id, float health, float maximum) { if (_initialized && isActiveAndEnabled) _driver.ObserveHealth(id, health, maximum); }
        public void ObserveProgression(ProgressionSnapshot sample) { if (_initialized && isActiveAndEnabled) _driver.ObserveProgression(sample); }
        public void ObserveAfterimage(FlashlightSample sample, float lifetime) { if (_initialized && isActiveAndEnabled) _driver.ObserveAfterimage(sample, lifetime); }
        public void ObserveFlashlight(FlashlightSample sample) { if (_initialized && isActiveAndEnabled) _driver.ObserveFlashlight(sample); }

        public void SetTorchPositions(int roomId, Vector3[] positions) { if (_initialized) _driver.SetTorchPositions(roomId, positions); }
        public void SetRooms(System.Collections.Generic.IReadOnlyList<GeneratedRoomSample> rooms) { if (_initialized) _driver.SetRooms(rooms); }
        public void ObserveRoom(RoomDestructionSample sample) { if (_initialized) _driver.ObserveRoom(sample); }
        public void PlayCueAt(CueId cue, Vector3 position, float gain = 1f, int emitterId = 0) { if (_initialized && isActiveAndEnabled) _driver.PlayCueAt(cue, position, gain, emitterId); }
        public void SetThreat(int id, bool chasing, float closeness) { if (_initialized) _driver.SetThreat(id, chasing, closeness); }
        public void RemoveThreat(int id) { if (_initialized) _driver.RemoveThreat(id); }
        public void SetAmbience(float openness, float collapse) { if (_initialized) _driver.SetAmbience(openness, collapse); }
        public void SetListenerPosition(Vector3 position) { if (_initialized) _driver.SetListenerPosition(position); }
        public void SetFootstepGain(float gain) { if (_initialized) _driver.SetFootstepGain(gain); }
        public void StopEmitter(int emitter) { if (_initialized) _driver.StopEmitter(emitter); }
        public void SetEmitterOcclusion(int emitter, float amount) { if (_initialized) _driver.SetEmitterOcclusion(emitter, amount); }
        public void ResetRun() { if (_initialized) _driver.ResetRun(); }

        private void OnEnable() { if (_initialized) _driver.SetOwnerEnabled(true); }
        private void OnDisable() { if (_initialized && _driver != null) _driver.SetOwnerEnabled(false); }
        private void OnDestroy()
        {
            if (_initialized && _driver != null) _driver.Teardown();
            if (Instance == this) Instance = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
