// ============================================================================
// HeldItemDriver.cs
// ============================================================================
// PURPOSE:
//   Builds the selected consumable's arm-free silhouette under an injected view.
//   It applies pure transition/sway poses and owns every generated object and
//   material. Selection never triggers gameplay use or changes camera aim.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · HeldItem.
// KEY RESPONSIBILITIES:
//   - Build collider-free primitive view models and apply the computed local pose.
//   - Sample render delta time and forward selection and suppression to the Presenter.
//   - Pair generated objects and materials with explicit teardown and disable hiding.
// DEPENDENCIES:
//   Core inventory snapshots and own HeldItem presentation stack only.
// USAGE NOTES:
//   Scene-owned by HeldItemManager; uses its own DriverConfig. No global side effects.
//   View is the output camera, supplied at scene assembly, never discovered.
//   Requires a serialized build-retained shader; parts use Ignore Raycast layer and
//   never cast shadows. A camera-parented item inherits view motion without a tick race.
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using Worsen.Core;
namespace Worsen.Presentation.HeldItem
{
    public sealed class HeldItemDriver : MonoBehaviour
    {
        private HeldItemDriverConfig _config;
        private HeldItemDriverState _state;
        private readonly HeldItemPresenter _presenter = new HeldItemPresenter();
        private readonly HeldItemGeometryPresenter _geometry = new HeldItemGeometryPresenter();
        private UnityEngine.Camera _view;
        private Transform _model;
        private Material _body, _detail;
        private string _builtId = "";

        public void Initialize(UnityEngine.Camera view, HeldItemDriverConfig config)
        {
            Teardown();
            _state = new HeldItemDriverState(); _config = config; _view = view;
            if (view == null || config == null || config.Shader == null)
            {
                Debug.LogWarning("HeldItem needs an output camera and config with a retained unlit shader.", this);
                return;
            }
            _body = new Material(config.Shader) { name = "Held item body (owned)", color = config.BodyColor };
            _detail = new Material(config.Shader) { name = "Held item detail (owned)", color = config.DetailColor };
        }
        public void SetConsumables(ConsumableInventorySnapshot snapshot)
        { if (_state != null) _presenter.SetSelection(_state, snapshot); }
        public void SetSuppressed(bool suppressed)
        {
            if (_state == null) return;
            _presenter.SetSuppressed(_state, suppressed);
            if (suppressed && _model != null) _model.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if (_state == null || _config == null || _view == null || _body == null) return;
            _presenter.Tick(_state, Time.deltaTime, _config.TransitionSeconds, _config.SwayPeriod);
            if (_builtId != _state.DisplayedId) Rebuild();
            if (_model == null) return;
            _model.gameObject.SetActive(!_state.Suppressed && _state.Raise > 0f);
            _model.localPosition = _presenter.ViewPosition(
                _presenter.Position(_state, _config.Position, _config.LowerDistance, _config.SwayAmplitude), _view.fieldOfView, _view.aspect);
            _model.localRotation = Quaternion.Euler(_config.Euler);
            _model.localScale = Vector3.one * (_config.Scale * _presenter.ProjectionScale(_view.fieldOfView));
        }
        private void Rebuild()
        {
            ClearModel(); _builtId = _state.DisplayedId;
            if (_builtId.Length == 0) return;
            _model = new GameObject("Held " + _builtId).transform;
            _model.SetParent(_view.transform, false);
            foreach (var part in _geometry.Build(_builtId))
            {
                var piece = GameObject.CreatePrimitive(part.Shape);
                piece.name = _builtId + " part";
                piece.layer = 2; // Built-in Ignore Raycast; no project layer mutation.
                var collider = piece.GetComponent<Collider>();
                if (collider != null) { collider.enabled = false; Destroy(collider); }
                piece.transform.SetParent(_model, false);
                piece.transform.localPosition = part.Position;
                piece.transform.localScale = part.Scale;
                var renderer = piece.GetComponent<Renderer>();
                renderer.sharedMaterial = part.Detail ? _detail : _body;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }
        private void ClearModel()
        {
            if (_model != null) { _model.gameObject.SetActive(false); Destroy(_model.gameObject); }
            _model = null; _builtId = "";
        }
        public void Teardown()
        {
            ClearModel();
            if (_body != null) Destroy(_body);
            if (_detail != null) Destroy(_detail);
            _body = _detail = null; _view = null; _config = null; _state = null;
        }
        private void OnDisable() { if (_model != null) _model.gameObject.SetActive(false); }
        private void OnDestroy() => Teardown();
    }
}
