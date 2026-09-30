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
//   - Forward roster/tell, acoustic, movement, progression and sensory facts to AudioDriver.
//   - Forward settings, pause, injury and aggregate proximity without gameplay rules.
//   - Forward confirmed catch identity to guarded per-run sting admission.
//   - Establish exactly one persistent Audio service and retire duplicate roots.
//   - Pair owner lifecycle and floor/run resets with the owned Driver lifetime.
//
// DEPENDENCIES:
//   - Core CueId and MovementState; Audio system's own Driver and DriverConfig.
//
// USAGE NOTES:
//   - Persistent (DontDestroyOnLoad); requires a dedicated root GameObject.
//   - Scene assembly must retain the canonical instance returned by Initialize.
//   - ResetRun releases all previous run playback; no scene-owned references are cached.
//   - Restart and capture reset rearm catch admission through the same ResetRun command.
//   - Floor resets may retain music contact; full restart and terminal run reset never do.
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
        public void ApplySettings(PlayerSettingsRecord settings) { if (_initialized) _driver.ApplySettings(settings); }
        public void SetPaused(bool paused) { if (_initialized) _driver.SetPaused(paused); }
        public void PlayCatchSting(EntityId player) { if (_initialized && isActiveAndEnabled) _driver.PlayCatchSting(player); }
        public void SetProximity(float closeness) { if (_initialized) _driver.SetProximity(closeness); }
        public void SetMovementState(MovementState movement) { if (_initialized) _driver.SetMovementState(movement); }
        public void SetSpeedNormalized(float speed) { if (_initialized) _driver.SetSpeedNormalized(speed); }
        public void SetInjury(float currentHealth, float maxHealth) { if (_initialized) _driver.SetInjury(currentHealth, maxHealth); }
        public void ObserveMovement(PlayerMovementSample sample) { if (_initialized && isActiveAndEnabled) _driver.ObserveMovement(sample); }
        public void ObserveTraversal(PlayerTraversalFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveTraversal(fact); }
        public void ObserveHunterFeedback(HunterFeedbackEvent fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveHunterFeedback(fact); }
        public void ObserveArchetype(HunterArchetypeFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveArchetype(fact); }
        public void ObserveWeaver(WeaverFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveWeaver(fact); }
        public void ObserveTicking(TickingSoundFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveTicking(fact); }
        public void ObserveHerald(HeraldScreamFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveHerald(fact); }
        public void ObserveHeraldBreath(HeraldBreathFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveHeraldBreath(fact); }
        public void ObserveHeraldDeafen(HeraldDeafenFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveHeraldDeafen(fact); }
        public void ObserveBlinder(BlinderSoundFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveBlinder(fact); }
        public void ObserveBlinderHit(BlinderHitFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveBlinderHit(fact); }
        public void ObserveRam(RamFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveRam(fact); }
        public void ObserveMimic(MimicFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveMimic(fact); }
        public void ObserveStare(StareFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveStare(fact); }
        public void ObserveMannequin(MannequinFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveMannequin(fact); }
        public void ObserveHabit(HunterHabitFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveHabit(fact); }
        public void ObserveDeliberation(EntityId hunter, Vector3 position, long tick) { if (_initialized && isActiveAndEnabled) _driver.ObserveDeliberation(hunter, position, tick); }
        public void ObserveProgressionEvent(ProgressionEventFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveProgressionEvent(fact); }
        public void ObserveHit(HunterHit fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveHit(fact); }
        public void SetTheme(string zone) { if (_initialized) _driver.SetTheme(zone); }
        public void SetRoomTheme(int room, string theme, string family) { if (_initialized) _driver.SetRoomTheme(room, theme, family); }
        public void ClearSenses() { if (_initialized) _driver.ClearSenses(); }
        public void ObservePickup(PickupCollectedFact fact, Vector3 position) { if (_initialized && isActiveAndEnabled) _driver.ObservePickup(fact, position); }
        public void ObserveHand(CollapseHandFact fact) { if (_initialized && isActiveAndEnabled) _driver.ObserveHand(fact); }
        public void ObserveRoom(RoomDestructionSample sample, Vector3 position) { if (_initialized && isActiveAndEnabled) _driver.ObserveRoom(sample, position); }
        public void ObserveHealth(EntityId id, float health, float maximum) { if (_initialized && isActiveAndEnabled) _driver.ObserveHealth(id, health, maximum); }
        public void ObserveProgression(ProgressionSnapshot sample) { if (_initialized && isActiveAndEnabled) _driver.ObserveProgression(sample); }
        public void ObserveTransaction(ProgressionSnapshot previous, ProgressionSnapshot current, ProgressionOperation operation) { if (_initialized) _driver.ObserveTransaction(previous, current, operation); }
        public void ObserveExit(FloorDisplaySnapshot sample, Vector3 position) { if (_initialized) _driver.ObserveExit(sample, position); }
        public void SetWorld(LevelGraph graph, System.Collections.Generic.IReadOnlyDictionary<int, bool> doors) { if (_initialized) _driver.SetWorld(graph, doors); }
        public void SetActiveEffects(IReadOnlyActiveEffects effects) { if (_initialized) _driver.SetActiveEffects(effects); }
        public void SetInRun(bool inRun) { if (_initialized) _driver.SetInRun(inRun); }
        public void SetDeafening(float seconds) { if (_initialized) _driver.SetDeafening(seconds); }
        public void SetMuffledDark(float seconds) { if (_initialized) _driver.SetMuffledDark(seconds); }
        public void ObserveGrace(GraceWindowFact fact) { if (_initialized) _driver.ObserveGrace(fact); }
        public void ObserveAfterimage(FlashlightSample sample, float lifetime) { if (_initialized && isActiveAndEnabled) _driver.ObserveAfterimage(sample, lifetime); }
        public void ObserveFlashlight(FlashlightSample sample) { if (_initialized && isActiveAndEnabled) _driver.ObserveFlashlight(sample); }

        public void SetTorchPositions(int roomId, Vector3[] positions) { if (_initialized) _driver.SetTorchPositions(roomId, positions); }
        public void SetRooms(System.Collections.Generic.IReadOnlyList<GeneratedRoomSample> rooms) { if (_initialized) _driver.SetRooms(rooms); }
        public void ObserveRoom(RoomDestructionSample sample) { if (_initialized) _driver.ObserveRoom(sample); }
        public void PlayCueAt(CueId cue, Vector3 position, float gain = 1f, int emitterId = 0) { if (_initialized && isActiveAndEnabled) _driver.PlayCueAt(cue, position, gain, emitterId); }
        public void SetThreat(int id, bool chasing, float closeness) { if (_initialized) _driver.SetThreat(id, chasing, closeness); }
        public void ObserveProximity(ProximitySample sample) { if (_initialized) _driver.ObserveProximity(sample); }
        public void RemoveThreat(int id) { if (_initialized) _driver.RemoveThreat(id); }
        public void SetAmbience(float openness, float collapse) { if (_initialized) _driver.SetAmbience(openness, collapse); }
        public void SetListenerPosition(Vector3 position) { if (_initialized) _driver.SetListenerPosition(position); }
        public void SetFootstepGain(float gain) { if (_initialized) _driver.SetFootstepGain(gain); }
        public void StopEmitter(int emitter) { if (_initialized) _driver.StopEmitter(emitter); }
        public void SetEmitterOcclusion(int emitter, float amount) { if (_initialized) _driver.SetEmitterOcclusion(emitter, amount); }
        public void ResetRun(bool preserveMusicContact = false, bool preserveZones = false) { if (_initialized) _driver.ResetRun(preserveMusicContact, preserveZones); }

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
