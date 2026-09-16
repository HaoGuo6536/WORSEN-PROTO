// ============================================================================
// AudioSoundscapeDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Defines the soundscape banks, bus gains and adaptive music layers.
//   Independent ambience loops accompany a DSP-scheduled run intro, loop and ending.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Expose reusable clip banks and independent effects, ambience and music gains.
//   - Tune smooth attack, chase hold and release.
//   - Tune normal/stress impact speed and the louder confirmed-chase danger bed.
//   - Tune exertion onset/recovery and its attenuation beneath critical-health breathing.
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

namespace Worsen.Presentation.Audio
{
    [CreateAssetMenu(fileName = "AudioSoundscapeDriverConfig", menuName = "Worsen/Audio/Soundscape Config")]
    public sealed class AudioSoundscapeDriverConfig : ScriptableObject
    {
        [SerializeField] private AudioSoundDefinition[] _sounds = new AudioSoundDefinition[0];
        [SerializeField, Range(8, 48)] private int _voiceCount = 24;
        [SerializeField, Range(0f, 1f)] private float _effectsGain = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _musicGain = 0.52f;
        [SerializeField, Range(0f, 1f)] private float _ambienceGain = 0.25f;
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
