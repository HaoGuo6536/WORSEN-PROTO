// ============================================================================
// FloorExitDoor.cs
// ============================================================================
// PURPOSE:
//   Operates a lone weathered door whose single leaf reveals a front-only escape.
//   Its owned visual assembly keeps the hinge, aperture and collision aligned.
//   Locked contact does nothing; open exits require deliberate crossing.
//   Explicit timing and crossing observations keep the transition reproducible.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorDriver · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Keep visible door movement and physical passage in agreement.
//   - Delegate art and escape rendering to the owned visual sub-driver.
//   - Apply pure opening transitions using the injected Floor clock.
//   - Expose continuous normalized opening progress.
//   - Prevent a stationary overlap from becoming an accidental floor transition.
// DEPENDENCIES:
//   - Core shared values and Floor-owned visual configuration only.
// USAGE NOTES:
//   Scene-owned through FloorDriver. Session supplies elapsed time; no Update loop.
//   No global settings. Reinitialization clears crossing and opening state.
//   Only fully-open crossing observations publish contact to FloorManager.
// ============================================================================
using System;

using UnityEngine;

namespace Worsen.Domain.Floor
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FloorExitDoor : MonoBehaviour
    {
        private readonly FloorExitDoorDriverState _state = new FloorExitDoorDriverState();
        private readonly FloorExitDoorPresenter _presenter = new FloorExitDoorPresenter();
        private FloorDriverConfig _config;
        public event Action<Collider> Contact;
        public bool FullyOpen => _state.FullyOpen;
        public bool Opening => _state.Opening;
        public float OpeningProgress => _config == null ? 0f : _presenter.OpeningProgress(_state.Elapsed, _config.ExitDoorOpeningDuration);
        public void Configure(FloorDriverConfig config, Material wood, Material stone, Material seal)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Teardown();
            _config = config;
            _presenter.Reset(_state);
            _state.Threshold = GetComponent<BoxCollider>();
            _state.Threshold.isTrigger = true;
            _state.Threshold.size = new Vector3(config.ExitSize.x, config.ExitSize.y, Mathf.Max(config.ExitSize.z, config.ExitCrossingDistance * 2f + 0.6f));
            _state.Threshold.center = Vector3.up * (config.ExitSize.y * 0.5f);
            _state.Threshold.enabled = true;
            var root = new GameObject("Standalone Exit Assembly");
            root.transform.SetParent(transform, false);
            _state.Visual = root.AddComponent<FloorExitDoorVisual>();
            _state.Visual.Configure(config, wood, stone, seal);
        }
        public void Open() { if (_config != null) _presenter.Open(_state); }
        public void Tick(float clock)
        {
            if (_config == null) return;
            float previous = _state.Elapsed;
            _presenter.Tick(_state, clock, _config.ExitDoorOpeningDuration);
            if (_state.Elapsed == previous) return;
            float progress = OpeningProgress;
            _state.Visual.Apply(_presenter.HingeAngle(progress, _config.ExitDoorOpeningAngle), progress);
            Physics.SyncTransforms();
        }
        public void Teardown()
        {
            if (_state.Visual != null)
            {
                _state.Visual.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_state.Visual.gameObject);
                else DestroyImmediate(_state.Visual.gameObject);
            }
            _state.Visual = null;
            if (_state.Threshold != null) _state.Threshold.enabled = false;
            _config = null;
            _presenter.Reset(_state);
        }
        private void OnDestroy() => Teardown();
        private void Observe(Collider other)
        {
            if (!isActiveAndEnabled || _config == null || !_state.Threshold.enabled ||
                other == null || !other.enabled || !other.gameObject.activeInHierarchy) return;
            if (!_state.FullyOpen) return;
            int key=other.GetInstanceID();
            if(!_state.Contacts.TryGetValue(key,out var crossing))
            { crossing=new FloorExitCrossingDriverState();_state.Contacts.Add(key,crossing); }
            Vector3 position=transform.InverseTransformPoint(other.bounds.center);
            if(_presenter.ObserveCrossing(crossing,position,_state.Threshold.size,_config.ExitCrossingDistance))
                Contact?.Invoke(other);
        }
        private void OnTriggerEnter(Collider other) => Observe(other);
        private void OnTriggerStay(Collider other) => Observe(other);
        private void OnTriggerExit(Collider other)
        {
            if (other == null) return;
            _state.Contacts.Remove(other.GetInstanceID());
        }
        private void OnDisable() => _state.Contacts.Clear();
    }
}
