// ============================================================================
// ShrineDriver.cs
// ============================================================================
// PURPOSE:
//   Creates nonblocking placeholder world objects for the Manager's placements.
//   Distinct primitive silhouettes and small emissive accents identify each shrine without text.
//   Spent shrines retain their shape but grey their body and dim their private accent.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Shrine.
// KEY RESPONSIBILITIES:
//   - Apply configured primitive recipes supplied through the pure Presenter.
//   - Build and destroy owned roots, primitive children and private materials.
//   - Apply active and spent palettes without changing shared assets or lighting.
// DEPENDENCIES:
//   - Own DriverConfig, Presenter and DriverState, plus Core placements; no gameplay reads.
// USAGE NOTES:
//   Scene-owned, no global side effects. Primitive colliders are disabled immediately.
//   Activation uses committed pose envelopes in the Controller, not physics callbacks.
//   No lights or GI contribution are created; emission is surface-only. Clear is idempotent.
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
                    var pedestal = CreatePart(root, config.Shapes.Pedestal, "Pedestal");
                    var body = CreateMaterial(pedestal.GetComponent<Renderer>().sharedMaterial, config.Color, Color.black);
                    var accent = CreateMaterial(pedestal.GetComponent<Renderer>().sharedMaterial,
                        shape.AccentColor, ShrineDriverPresenter.Emission(shape.AccentColor, config.AccentEmission, 1f));
                    pedestal.GetComponent<Renderer>().sharedMaterial = body;
                    state.Bodies.Add(placement.Id, body);
                    state.Accents.Add(placement.Id, accent);
                    state.AccentColors.Add(placement.Id, shape.AccentColor);
                    for (int i = 0; i < shape.Parts.Count; i++)
                    {
                        var part = shape.Parts[i];
                        var child = CreatePart(root, part, "Part " + i);
                        child.GetComponent<Renderer>().sharedMaterial = part.Accent ? accent : body;
                    }
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
        private Material CreateMaterial(Material template, Color color, Color emission)
        {
            var material = new Material(template);
            state.Materials.Add(material);
            if (!material.HasProperty("_EmissionColor"))
                throw new InvalidOperationException("Shrine primitive material must support _EmissionColor.");
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
