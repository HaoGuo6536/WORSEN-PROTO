// ============================================================================
// AudioSoundscapeDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Defines the soundscape banks, bus gains and adaptive music layers.
//   Near-silent ambience accompanies belief-gated music with a persistent tension floor.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Bind named roster/tell ids to existing banks or explicit silent placeholders and room filters.
//   - Tune shared hearing, bodily masking, false positives and timed mix effects.
//   - Expose mixer parameters without writing designer assets at runtime.
//   - Expose clip banks, category gains and protected roster variation.
//   - Tune belief-gated music, exertion and critical-health breathing.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Mirrored asset: Resources/ScriptableObjects/Presentation/Audio/AudioSoundscapeDriverConfig.
//   Runtime never mutates this shared asset. Independent ambience loops need not match musical duration.
//
// ============================================================================

using UnityEngine;
using UnityEngine.Audio;
using Worsen.Core;

namespace Worsen.Presentation.Audio
{
    [CreateAssetMenu(fileName = "AudioSoundscapeDriverConfig", menuName = "Worsen/Audio/Soundscape Config")]
    public sealed class AudioSoundscapeDriverConfig : ScriptableObject
    {
        [SerializeField] private AudioSoundDefinition[] _sounds = new AudioSoundDefinition[0];
        [Header("Roster bindings and sound zones (provisional)")]
        [SerializeField] private AudioRosterBinding[] _rosterBindings = {
            new AudioRosterBinding("hunter.presence", CueId.Presence),
            new AudioRosterBinding("hunter.detection", CueId.Detection),
            new AudioRosterBinding("hunter.chase", CueId.Chase),
            new AudioRosterBinding("hunter.attack", CueId.EnemyWindup),
            new AudioRosterBinding("hunter.death", CueId.Death),
            new AudioRosterBinding("hunter.turn", CueId.Presence),
            new AudioRosterBinding("hunter.cake-reaction", CueId.Presence),
            new AudioRosterBinding("echo.footstep", CueId.Footstep),
            new AudioRosterBinding("weaver.skitter", CueId.Presence, true),
            new AudioRosterBinding("weaver.wet-click", CueId.EnemyWindup, true),
            new AudioRosterBinding("ticking.tick", CueId.Presence, true),
            new AudioRosterBinding("ticking.winding", CueId.Presence, true),
            new AudioRosterBinding("ticking.key-appeared", CueId.Presence, true),
            new AudioRosterBinding("ticking.wake", CueId.Detection),
            new AudioRosterBinding("echo.quickened-recording", CueId.Presence, true),
            new AudioRosterBinding("weaver-quickened-skitter", CueId.Presence, true)
        };
        [SerializeField] private AudioSoundZone[] _soundZones = {
            new AudioSoundZone("castle-stone", "castle", 6500f),
            new AudioSoundZone("hospital-tile", "hospital", 16000f)
        };
        [SerializeField, Range(0f, 1f)] private float _rosterClipGain = .5f;
        [SerializeField, Range(0, 100)] private int _rosterClipPriority = 60;
        [SerializeField, Range(0f, .2f)] private float _rosterPitchVariation = .03f;
        [SerializeField, Range(0f, .2f)] private float _rosterGainVariation = .02f;
        [SerializeField, Range(0f, 1f)] private float _earPlugsDurationMultiplier = .5f;
        [SerializeField, Range(0f, 1f)] private float _mirrorSkinDurationMultiplier = .5f;
        public float RosterPitchVariation => _rosterPitchVariation;
        public float RosterGainVariation => _rosterGainVariation;
        public float EarPlugsDurationMultiplier => _earPlugsDurationMultiplier;
        public float MirrorSkinDurationMultiplier => _mirrorSkinDurationMultiplier;
        public System.Collections.Generic.IReadOnlyList<AudioRosterBinding> RosterBindings => _rosterBindings;
        public System.Collections.Generic.IReadOnlyList<AudioSoundZone> SoundZones => _soundZones;
        public float RosterClipGain => _rosterClipGain;
        public int RosterClipPriority => _rosterClipPriority;
        [Header("Silence-first world mix (provisional)")]
        [SerializeField, Min(.01f)] private float _hearingReferenceMeters = 2f;
        [SerializeField, Min(0f)] private float _hearingRolloff = 1f;
        [SerializeField, Range(0f, 1f)] private float _portalRetention = .7f;
        [SerializeField, Range(0f, 1f)] private float _closedDoorRetention = .35f;
        [SerializeField, Range(0f, 1f)] private float _hearingThreshold = .01f;
        [SerializeField, Min(1f)] private float _keenEarsRangeMultiplier = 1.5f;
        [SerializeField, Min(90f)] private float _falsePositiveMinimumSeconds = 90f;
        [SerializeField, Min(0f)] private float _falsePositiveExtraSeconds = 90f;
        [SerializeField, Min(1f)] private float _falsePositiveDistance = 12f;
        [SerializeField, Range(0f, 1f)] private float _falsePositiveGain = .3f;
        [SerializeField, Min(0f)] private float _timingJitterSeconds = .025f;
        [SerializeField, Range(0f, 1f)] private float _pursuitBreathGain = .45f;
        [SerializeField, Range(0f, 1f)] private float _heartbeatGain = .3f;
        [SerializeField, Min(.1f)] private float _heartbeatSlowSeconds = 1.1f;
        [SerializeField, Min(.1f)] private float _heartbeatFastSeconds = .4f;
        [SerializeField, Min(.01f)] private float _heartbeatPulseSeconds = .12f;
        [SerializeField, Min(0f)] private float _graceSpikeSeconds = .65f;
        [SerializeField, Range(0f, 1f)] private float _heartbeatMask = .12f;
        [SerializeField, Range(0f, 1f)] private float _deafenedGain = .25f;
        [SerializeField, Range(10f, 22000f)] private float _muffledCutoff = 900f;
        [SerializeField, Min(.1f)] private float _collapseSlowSeconds = 2f;
        [SerializeField, Min(.1f)] private float _collapseFastSeconds = .3f;
        [SerializeField, Range(0f, 1f)] private float _exitOpeningThreshold = .01f;
        [SerializeField] private AudioClip _heartbeatClip = null;
        [SerializeField] private AudioMixer _mixer = null;
        [SerializeField] private string _masterParameter = "MasterVolume";
        [SerializeField] private string _musicParameter = "MusicVolume";
        [SerializeField] private string _effectsParameter = "EffectsVolume";
        public HearingModelSettings Hearing => new HearingModelSettings(_hearingReferenceMeters, _hearingRolloff, _portalRetention, _closedDoorRetention, _hearingThreshold);
        public float KeenEarsRangeMultiplier => _keenEarsRangeMultiplier;
        public float FalsePositiveMinimumSeconds => Mathf.Max(90f, _falsePositiveMinimumSeconds);
        public float FalsePositiveExtraSeconds => _falsePositiveExtraSeconds;
        public float FalsePositiveDistance => _falsePositiveDistance;
        public float FalsePositiveGain => _falsePositiveGain;
        public float TimingJitterSeconds => _timingJitterSeconds;
        public float PursuitBreathGain => _pursuitBreathGain;
        public float HeartbeatGain => _heartbeatGain;
        public float HeartbeatSlowSeconds => _heartbeatSlowSeconds;
        public float HeartbeatFastSeconds => _heartbeatFastSeconds;
        public float HeartbeatPulseSeconds => _heartbeatPulseSeconds;
        public float GraceSpikeSeconds => _graceSpikeSeconds;
        public float HeartbeatMask => _heartbeatMask;
        public float DeafenedGain => _deafenedGain;
        public float MuffledCutoff => _muffledCutoff;
        public float CollapseSlowSeconds => _collapseSlowSeconds;
        public float CollapseFastSeconds => _collapseFastSeconds;
        public float ExitOpeningThreshold => _exitOpeningThreshold;
        public AudioClip HeartbeatClip => _heartbeatClip;
        public AudioMixer Mixer => _mixer;
        public string MasterParameter => _masterParameter;
        public string MusicParameter => _musicParameter;
        public string EffectsParameter => _effectsParameter;
        [SerializeField, Range(8, 48)] private int _voiceCount = 24;
        [SerializeField, Range(0f, 1f)] private float _effectsGain = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _musicGain = 0.52f;
        [SerializeField, Range(0f, 1f)] private float _ambienceGain = 0.01f;
        [SerializeField, Min(0.05f)] private float _attackSeconds = 0.65f;
        [SerializeField, Min(0.05f)] private float _releaseSeconds = 3.5f;
        [SerializeField, Min(0f)] private float _chaseHoldSeconds = 2.5f;
        [SerializeField, Min(0.05f)] private float _exertionOnsetSeconds = 0.8f;
        [SerializeField, Min(0.05f)] private float _exertionReleaseSeconds = 1.1f;
        [SerializeField, Range(0f, 1f)] private float _criticalExertionMultiplier = 0f;
        [SerializeField] private AudioClip _tensionStem;
        [SerializeField] private AudioClip _chaseStem;
        [SerializeField] private AudioClip _dangerStem;
        [SerializeField] private AudioClip _runIntro;
        [SerializeField] private AudioClip _runLoop;
        [SerializeField] private AudioClip _runEnd;
        [SerializeField, Range(0f, 1f)] private float _deepImpactGain = .5f;
        [SerializeField, Range(0f, 1f)] private float _tensionFloorFraction = .12f;
        [SerializeField, Min(0f)] private float _dangerReleaseMinimumSeconds = 1.5f;
        [SerializeField, Min(0f)] private float _dangerReleaseMaximumSeconds = 5f;
        [SerializeField, Range(0f, 1f)] private float _earlyDangerFadeProbability = .15f;
        [SerializeField, Range(0f, 1f)] private float _dangerProximityThreshold = .35f;
        [SerializeField, Range(0f, 1f)] private float _stressImpactGain = .6f;
        [SerializeField, Range(0f, 1f)] private float _dangerAmbienceGain = .25f;
        [SerializeField, Range(0f, 1f)] private float _chaseDangerGain = .65f;
        [SerializeField, Range(0f, 1f)] private float _runGain = .55f;
        [SerializeField, Range(.5f, 1f)] private float _impactMinimumPitch = .85f;
        [SerializeField, Range(1f, 1.5f)] private float _impactMaximumPitch = 1.3f;
        [SerializeField, Min(.1f)] private float _impactRampSeconds = 4f;
        [SerializeField] private AudioClip _interiorAmbience;
        [SerializeField] private AudioClip _exteriorAmbience;
        [SerializeField] private AudioMixerGroup _effectsGroup;
        [SerializeField] private AudioMixerGroup _musicGroup;
        [SerializeField] private AudioMixerGroup _ambienceGroup;
        [SerializeField, Min(1f)] private float _torchAudibleDistance = 16f;
        [SerializeField, Min(2f)] private float _accentMinimumSeconds = 7f;
        [SerializeField, Min(2f)] private float _accentMaximumSeconds = 15f;
        public float TorchAudibleDistance => _torchAudibleDistance;
        public float AccentMinimumSeconds => _accentMinimumSeconds;
        public float AccentMaximumSeconds => _accentMaximumSeconds;
        [SerializeField] private LayerMask _footstepGroundMask = ~0;
        public AudioSoundDefinition[] Sounds => _sounds;
        public int VoiceCount => _voiceCount;
        public float EffectsGain => _effectsGain;
        public float MusicGain => _musicGain;
        public float AmbienceGain => _ambienceGain;
        public float AttackSeconds => _attackSeconds;
        public float ReleaseSeconds => _releaseSeconds;
        public float ChaseHoldSeconds => _chaseHoldSeconds;
        public float ExertionOnsetSeconds => _exertionOnsetSeconds;
        public float ExertionReleaseSeconds => _exertionReleaseSeconds;
        public float CriticalExertionMultiplier => _criticalExertionMultiplier;
        public AudioClip TensionStem => _tensionStem;
        public AudioClip ChaseStem => _chaseStem;
        public AudioClip DangerStem => _dangerStem;
        public AudioClip RunIntro => _runIntro;
        public AudioClip RunLoop => _runLoop;
        public AudioClip RunEnd => _runEnd;
        public float DeepImpactGain => _deepImpactGain;
        public float TensionFloorFraction => _tensionFloorFraction;
        public float DangerReleaseMinimumSeconds => _dangerReleaseMinimumSeconds;
        public float DangerReleaseMaximumSeconds => _dangerReleaseMaximumSeconds;
        public float EarlyDangerFadeProbability => _earlyDangerFadeProbability;
        public float DangerProximityThreshold => _dangerProximityThreshold;
        public float StressImpactGain => _stressImpactGain;
        public float DangerAmbienceGain => _dangerAmbienceGain;
        public float ChaseDangerGain => _chaseDangerGain;
        public float RunGain => _runGain;
        public float ImpactMinimumPitch => _impactMinimumPitch;
        public float ImpactMaximumPitch => _impactMaximumPitch;
        public float ImpactRampSeconds => _impactRampSeconds;
        public AudioClip InteriorAmbience => _interiorAmbience;
        public AudioClip ExteriorAmbience => _exteriorAmbience;
        public AudioMixerGroup EffectsGroup => _effectsGroup;
        public AudioMixerGroup MusicGroup => _musicGroup;
        public AudioMixerGroup AmbienceGroup => _ambienceGroup;
        public LayerMask FootstepGroundMask => _footstepGroundMask;
    }
}
