// ============================================================================
// EnvironmentFluorescentFixture.cs
// ============================================================================
// PURPOSE:
//   Modulates only the luminous material slots of a placed kit fixture.
//   Its housing retains the kit's lit materials, rather than becoming an Unlit
//   square. The parent places and budgets the matching Lumen source separately.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by EnvironmentDriver · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Clone luminous kit materials and apply supplied brightness without recolouring housings.
//   - Release private materials explicitly during floor teardown.
// DEPENDENCIES:
//   - Own DriverConfig and Unity rendering APIs.
// USAGE NOTES:
//   Scene-owned through EnvironmentDriver. No Update, clock or global effects.
//   Lumen illumination is separately owned and budgeted by the parent Driver.
//   Configure binds already placed children; coordinates remain for API compatibility.
//   Legacy shader admission is retained until shared setup/tests migrate together.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Presentation.Environment
{
    public sealed class EnvironmentFluorescentFixture : MonoBehaviour
    {
        private readonly List<Material> _materials = new List<Material>();
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
            _color = (cage ? config.WarmColor : config.FluorescentColor) * config.FixtureEmission;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || !source.HasProperty("_EmissionColor") ||
                        !(source.name.EndsWith("_light", StringComparison.Ordinal) ||
                          source.name.EndsWith("_tube", StringComparison.Ordinal) && !source.name.EndsWith("_dead_tube", StringComparison.Ordinal) ||
                          source.name.EndsWith("_sodium", StringComparison.Ordinal))) continue;
                    var owned = new Material(source) { name = "Owned fixture emitter" };
                    owned.EnableKeyword("_EMISSION"); materials[i] = owned; _materials.Add(owned);
                }
                renderer.sharedMaterials = materials;
            }
            SetBrightness(0f);
        }
        public void SetBrightness(float value)
        {
            foreach (Material material in _materials)
                material.SetColor("_EmissionColor", _color * Mathf.Max(0f, value));
        }
        public void Teardown()
        {
            foreach (Material material in _materials)
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            _materials.Clear();
        }
        private void OnDestroy() => Teardown();
    }
}
