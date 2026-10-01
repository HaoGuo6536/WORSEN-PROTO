// ============================================================================
// ShrineDriver.cs
// ============================================================================
// PURPOSE:
//   Creates nonblocking authored models for the Manager's placements, or primitive fallbacks.
//   Distinct silhouettes and small emissive accents identify each shrine without text.
//   Spent shrines retain their shape but grey their body and dim their private accent.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Shrine.
// KEY RESPONSIBILITIES:
//   - Select configured models or apply fallback recipes through the pure Presenter.
//   - Build and destroy owned roots, static model/primitive children and private materials.
//   - Reject active prefab behaviours and disable every cloned collider before activation.
//   - Apply active and spent palettes without changing shared assets or lighting.
// DEPENDENCIES:
//   - Own DriverConfig, Presenter and DriverState, plus Core placements; no gameplay reads.
// USAGE NOTES:
//   Scene-owned, no global side effects. Primitive colliders are disabled immediately.
//   Activation uses committed pose envelopes in the Controller, not physics callbacks.
//   No lights or GI contribution are created; emission is surface-only. Clear is idempotent.
//   Models must contain only Transform/MeshFilter/MeshRenderer/Collider components and
//   the two named material slots. Malformed configured art fails closed, not silently hidden.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Shrine
{
    public sealed class ShrineDriver : MonoBehaviour
    {
        private readonly ShrineDriverState state = new ShrineDriverState();
        private ShrineDriverConfig config;
        public void Build(IReadOnlyList<ShrinePlacement> placements, ShrineDriverConfig configuration)
        {
            Clear();
            if (placements == null) throw new ArgumentNullException(nameof(placements));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            config = configuration;
            try
            {
                foreach (var placement in placements)
                {
                    var shape = config.Shapes.Get(placement.Kind);
                    // Register before any engine work so failed builds are also owned by Clear.
                    var root = new GameObject("Shrine " + placement.Id + " - " + placement.Kind);
                    try { state.Objects.Add(placement.Id, root); }
                    catch { Release(root); throw; }
                    root.transform.SetParent(transform, false);
                    root.transform.position = placement.Site.Position;
                    root.SetActive(false);
                    var model = config.GetModel(placement.Kind);
                    if (model != null) BuildModel(root, model, placement.Id, shape.AccentColor);
                    else BuildFallback(root, placement.Id, shape);
                    state.AccentColors.Add(placement.Id, shape.AccentColor);
                    root.SetActive(true);
                }
            }
            catch { Clear(); throw; }
        }
        public void MarkUsed(int id)
        {
            if (!state.Objects.ContainsKey(id)) return;
            state.Bodies[id].color = config.SpentColor;
            var accent = state.Accents[id];
            var color = state.AccentColors[id];
            accent.color = ShrineDriverPresenter.DimAccent(color, config.SpentAccentMultiplier);
            accent.SetColor("_EmissionColor", ShrineDriverPresenter.Emission(color,
                config.AccentEmission, config.SpentAccentMultiplier));
        }
        public void Clear()
        {
            foreach (var body in state.Objects.Values) if (body != null) { body.SetActive(false); Release(body); }
            foreach (var material in state.Materials) if (material != null) Release(material);
            state.Objects.Clear(); state.Materials.Clear();
            state.Bodies.Clear(); state.Accents.Clear(); state.AccentColors.Clear();
            config = null;
        }
        private GameObject CreatePart(GameObject root, ShrineDriverConfig.Part part, string name)
        {
            var child = GameObject.CreatePrimitive(part.Primitive);
            try
            {
                child.GetComponent<Collider>().enabled = false;
                child.transform.SetParent(root.transform, false);
                child.name = name;
                child.transform.localPosition = ShrineDriverPresenter.Position(part, config.Size);
                child.transform.localScale = ShrineDriverPresenter.Scale(part, config.Size);
                child.transform.localRotation = Quaternion.Euler(part.Euler);
                return child;
            }
            catch { Release(child); throw; }
        }
        private void BuildFallback(GameObject root, int id, ShrineDriverConfig.Silhouette shape)
        {
            var pedestal = CreatePart(root, config.Shapes.Pedestal, "Pedestal");
            var template = pedestal.GetComponent<Renderer>().sharedMaterial;
            var body = CreateMaterial(template, config.Color, Color.black);
            var accent = CreateMaterial(template, shape.AccentColor,
                ShrineDriverPresenter.Emission(shape.AccentColor, config.AccentEmission, 1f));
            pedestal.GetComponent<Renderer>().sharedMaterial = body;
            state.Bodies.Add(id, body); state.Accents.Add(id, accent);
            for (int i = 0; i < shape.Parts.Count; i++)
            {
                var part = shape.Parts[i];
                var child = CreatePart(root, part, "Part " + i);
                child.GetComponent<Renderer>().sharedMaterial = part.Accent ? accent : body;
            }
        }
        private void BuildModel(GameObject root, GameObject model, int id, Color color)
        {
            // Validate BEFORE Instantiate: even an inactive clone must not carry user scripts.
            foreach (var component in model.GetComponentsInChildren<Component>(true))
                if (!(component is Transform) && !(component is MeshFilter) &&
                    !(component is MeshRenderer) && !(component is Collider))
                    throw new InvalidOperationException("Shrine models must be static mesh-only hierarchies: " + model.name);
            Material bodyTemplate = null, accentTemplate = null;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && material.name == ShrineDriverConfig.BodyMaterialName) bodyTemplate = material;
                    else if (material != null && material.name == ShrineDriverConfig.AccentMaterialName) accentTemplate = material;
                    else throw new InvalidOperationException("Shrine model has an unnamed or unsupported material slot: " + model.name);
                }
            if (bodyTemplate == null || accentTemplate == null)
                throw new InvalidOperationException("Shrine model requires body and emissive accent material slots: " + model.name);
            var body = CreateMaterial(bodyTemplate, bodyTemplate.color, Color.black);
            var accent = CreateMaterial(accentTemplate, color,
                ShrineDriverPresenter.Emission(color, config.AccentEmission, 1f));
            state.Bodies.Add(id, body); state.Accents.Add(id, accent);
            var instance = Instantiate(model, root.transform, false);
            instance.name = "Model";
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = materials[i].name == ShrineDriverConfig.AccentMaterialName ? accent : body;
                renderer.sharedMaterials = materials;
            }
            instance.SetActive(true);
        }
        private Material CreateMaterial(Material template, Color color, Color emission)
        {
            var material = new Material(template);
            state.Materials.Add(material);
            if (!material.HasProperty("_EmissionColor"))
                throw new InvalidOperationException("Shrine material must support _EmissionColor.");
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return material;
        }
        private void Release(UnityEngine.Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDestroy() => Clear();
    }
}
