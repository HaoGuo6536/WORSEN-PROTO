// ============================================================================
// EnvironmentFluorescentFixture.cs
// ============================================================================
// PURPOSE:
//   Builds a replaceable fluorescent panel or cage lamp at a wall-light socket.
//   It supplies visible theme art without collision, imported assets or real lights.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by EnvironmentDriver · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Build an envelope-limited diffuser or warm cage lamp and apply supplied brightness.
//   - Release its private material when the room is destroyed.
// DEPENDENCIES:
//   - Own DriverConfig/ThemePresenter and Unity rendering APIs.
// USAGE NOTES:
//   Scene-owned through EnvironmentDriver. No Update, clock or global effects.
//   Lumen illumination is separately owned and budgeted by the parent Driver.
//   The panel shader is serialized in the config; missing wiring reports once and aborts.
// ============================================================================
using System;
using UnityEngine;
namespace Worsen.Presentation.Environment
{
    public sealed class EnvironmentFluorescentFixture : MonoBehaviour
    {
        private Material _material;
        private Material _cageMaterial;
        private Color _color;
        private bool _missingShaderReported;
        public void Configure(Vector3 socket, float yaw, Vector3 envelope, EnvironmentDriverConfig config, bool cage = false)
        {
            var shader = config != null ? config.PanelShader : null;
            if (shader == null)
            {
                const string error = "EnvironmentDriverConfig requires PanelShader. Rebuild Environment assets.";
                if (!_missingShaderReported) { _missingShaderReported = true; Debug.LogError(error, this); }
                throw new InvalidOperationException(error);
            }
            _color = cage ? config.WarmColor : config.FluorescentColor;
            _material = new Material(shader) { name = "Owned fluorescent panel" };
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Flat fluorescent diffuser";
            panel.transform.SetParent(transform, false);
            panel.transform.SetPositionAndRotation(socket, Quaternion.Euler(0f, yaw, 0f));
            panel.transform.localScale = EnvironmentThemePresenter.PanelSize(config.FluorescentPanelSize, envelope);
            panel.GetComponent<Collider>().enabled = false;
            panel.GetComponent<Renderer>().sharedMaterial = _material;
            if (cage)
            {
                panel.name = "Cage lamp diffuser";
                var size = panel.transform.localScale; size.z /= 1.3f; panel.transform.localScale = size;
                _cageMaterial = new Material(shader) { name = "Owned cage bars", color = Color.black };
                for (int i = 0; i < 5; i++)
                {
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bar.name = "Cage bar"; bar.transform.SetParent(panel.transform, false);
                    bar.transform.localPosition = new Vector3((i - 2) * .2f, 0f, .55f);
                    bar.transform.localScale = new Vector3(.06f, 1f, .2f);
                    bar.GetComponent<Collider>().enabled = false;
                    bar.GetComponent<Renderer>().sharedMaterial = _cageMaterial;
                }
            }
            SetBrightness(0f);
        }
        public void SetBrightness(float value)
        {
            if (_material == null) return;
            _material.SetColor("_BaseColor", _color * value);
            _material.SetColor("_Color", _color * value);
        }
        private void OnDestroy()
        {
            if (_cageMaterial != null) { if (Application.isPlaying) Destroy(_cageMaterial); else DestroyImmediate(_cageMaterial); }
            if (_material == null) return;
            if (Application.isPlaying) Destroy(_material); else DestroyImmediate(_material);
        }
    }
}
