// ============================================================================
// PostFXDriverConfig.cs
// ============================================================================
//
// PURPOSE:
//   Stores the prototype's pursuit, injury and intrusion effect settings.
//   Each response can be tuned without changing the code that routes gameplay facts.
//
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · PostFX.
//
// KEY RESPONSIBILITIES:
//   - Expose optional re-acquire blur and bounded effect strength.
//   - Keep runtime envelopes out of shared assets.
//
// DEPENDENCIES:
//   - No other project systems.
//
// USAGE NOTES:
//   - Mirrored Resources/ScriptableObjects/Presentation/PostFX asset; runtime read-only.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.PostFX
{
    [CreateAssetMenu(fileName = "PostFXDriverConfig", menuName = "Worsen/PostFX/Driver Config")]
    public sealed class PostFXDriverConfig : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float _peripheralChromatic = 0.25f;
        [SerializeField, Range(0f, 0.3f)] private float _peripheralDistortion = 0.12f;
        [SerializeField, Range(0f, 1f)] private float _injuryVignette = 0.45f;
        [SerializeField] private bool _reacquireBlurEnabled = true;
        [SerializeField, Min(0.001f)] private float _reacquireBlurSeconds = 0.1f;
        [SerializeField, Range(0.5f, 1.5f)] private float _blurRadius = 1f;
        [SerializeField, Range(0f, 100f)] private float _intrusionDesaturation = 70f;
        [SerializeField, Range(0f, 1f)] private float _intrusionGrain = 0.5f;
        [SerializeField, Min(0f)] private float _volumePriority = 20f;

        public float PeripheralChromatic => _peripheralChromatic;
        public float PeripheralDistortion => _peripheralDistortion;
        public float InjuryVignette => _injuryVignette;
        public bool ReacquireBlurEnabled => _reacquireBlurEnabled;
        public float ReacquireBlurSeconds => _reacquireBlurSeconds;
        public float BlurRadius => _blurRadius;
        public float IntrusionDesaturation => _intrusionDesaturation;
        public float IntrusionGrain => _intrusionGrain;
        public float VolumePriority => _volumePriority;
    }
}

