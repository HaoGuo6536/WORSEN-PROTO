// ============================================================================
// HorrorAmbienceDriver.cs
// ============================================================================
// PURPOSE:
//   Plays the optional quiet room-ambience loop while its Horror owner is active.
//   Keeping this source separate from enemy cues lets scene ownership stop or
//   release the background sound without affecting warnings or shared audio.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by HorrorDriver · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Own one non-spatial looping AudioSource for the configured ambience clip.
//   - Stop on disable and release only that source during teardown/reinitialize.
// DEPENDENCIES:
//   - Unity audio and lifecycle; the Horror system's own DriverConfig.
// USAGE NOTES:
//   Scene-owned child of HorrorDriver. Initialize creates no source when the
//   optional clip is absent and never starts playback by itself. The owner then
//   calls SetOwnerEnabled. Playback occurs only in Play Mode, preserving silent
//   editor setup. This component does not inspect gameplay or change listeners,
//   mixer state, the clip asset, or any AudioSource it did not create.
// ============================================================================
using UnityEngine;

namespace Worsen.Presentation.Horror
{
    [DisallowMultipleComponent]
    public sealed class HorrorAmbienceDriver : MonoBehaviour
    {
        private AudioSource _source;
        private bool _ownerEnabled;

        public void Initialize(HorrorDriverConfig config)
        {
            Teardown();
            if (config == null || config.AmbienceLoop == null) return;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.clip = config.AmbienceLoop;
            _source.volume = config.AmbienceGain;
        }

        public void SetOwnerEnabled(bool value)
        {
            _ownerEnabled = value;
            ApplyPlayback();
        }

        public void Teardown()
        {
            _ownerEnabled = false;
            if (_source == null) return;
            _source.Stop();
            _source.clip = null;
            if (Application.isPlaying) Destroy(_source);
            else DestroyImmediate(_source);
            _source = null;
        }

        private void ApplyPlayback()
        {
            if (_source == null) return;
            if (!_ownerEnabled || !isActiveAndEnabled || !Application.isPlaying)
            {
                _source.Stop();
                return;
            }
            if (!_source.isPlaying) _source.Play();
        }

        private void OnEnable() => ApplyPlayback();
        private void OnDisable() { if (_source != null) _source.Stop(); }
        private void OnDestroy() => Teardown();
    }
}
