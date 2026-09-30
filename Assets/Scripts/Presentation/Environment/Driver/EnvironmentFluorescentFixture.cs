// ============================================================================
// EnvironmentFluorescentFixture.cs
// ============================================================================
// PURPOSE:
//   Builds a replaceable flat fluorescent panel at an existing wall-light socket.
//   It supplies visible cold art without collision, imported assets or real lights.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by EnvironmentDriver · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Build an envelope-limited panel and apply owner-supplied brightness.
//   - Release its private material when the room is destroyed.
// DEPENDENCIES:
//   - Own DriverConfig/ThemePresenter and Unity rendering APIs.
// USAGE NOTES:
//   Scene-owned through EnvironmentDriver. No Update, clock or global effects.
//   Lumen illumination is separately owned and budgeted by the parent Driver.
// ============================================================================
using System;
using UnityEngine;
namespace Worsen.Presentation.Environment
{
    public sealed class EnvironmentFluorescentFixture : MonoBehaviour
    {
        private Material _material;
        private Color _color;
        public void Configure(Vector3 socket, float yaw, Vector3 envelope, EnvironmentDriverConfig config)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("Fluorescent panels require an unlit shader.");
            _color = config.FluorescentColor;
            _material = new Material(shader) { name = "Owned fluorescent panel" };
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Flat fluorescent diffuser";
            panel.transform.SetParent(transform, false);
            panel.transform.SetPositionAndRotation(socket, Quaternion.Euler(0f, yaw, 0f));
            panel.transform.localScale = EnvironmentThemePresenter.PanelSize(config.FluorescentPanelSize, envelope);
            panel.GetComponent<Collider>().enabled = false;
            panel.GetComponent<Renderer>().sharedMaterial = _material;
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
            if (_material == null) return;
            if (Application.isPlaying) Destroy(_material); else DestroyImmediate(_material);
        }
    }
}
