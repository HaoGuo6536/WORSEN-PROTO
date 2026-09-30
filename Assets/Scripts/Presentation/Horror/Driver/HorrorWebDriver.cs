// ============================================================================
// HorrorWebDriver.cs
// ============================================================================
// PURPOSE:
//   Renders Weaver warnings as thin world lines and webs as faint unlit placeholders.
//   These objects carry no colliders, gameplay effects or independent audio sources.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by HorrorDriver · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Apply presenter lifetimes and release owned lines, quads and generated material.
// DEPENDENCIES:
//   - Core Weaver facts, own Horror config/state/presenter and Unity rendering.
// USAGE NOTES:
//   Scene-owned; shares HorrorDriverConfig. Time is supplied by the owner's Run
//   clock, so pause freezes lifetimes. Disable/reset destroys all web objects.
//   Door facts have no orientation: the placeholder uses two crossed upright quads.
//   Missing WebMaterial and WebShader reports once per sub-driver and prevents visual admission.
// ============================================================================
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Worsen.Core;
namespace Worsen.Presentation.Horror
{
    public sealed class HorrorWebDriver : MonoBehaviour
    {
        private readonly HorrorWebDriverState _state = new HorrorWebDriverState();
        private readonly HorrorWebPresenter _presenter = new HorrorWebPresenter();
        private HorrorDriverConfig _config;
        private Material _material;
        private bool _missingMaterialReported;
        private bool _ownsMaterial;
        public int VisualCount => _state.Objects.Count;
        public void Initialize(HorrorDriverConfig config)
        {
            Teardown();
            _material = config != null ? config.WebMaterial : null;
            if (_material == null && (config == null || config.WebShader == null))
            {
                if (!_missingMaterialReported)
                {
                    _missingMaterialReported = true;
                    Debug.LogError("HorrorDriverConfig requires WebMaterial or WebShader. Rebuild Horror assets.", this);
                }
                return;
            }
            _config = config;
            if (_material == null)
            { _material = new Material(config.WebShader) { name = "Owned web placeholder" }; _ownsMaterial = true; }
        }
        public void Observe(WeaverFact fact)
        { if (_config != null && isActiveAndEnabled) { _presenter.Observe(_state, fact); Apply(); } }
        public void Tick(float delta) { _presenter.Tick(_state, delta); Apply(); }
        private void Apply()
        {
            foreach (var key in _state.Objects.Keys.ToArray())
                if (!_state.Visuals.ContainsKey(key)) { DestroyOwned(_state.Objects[key]); _state.Objects.Remove(key); }
            if (_material == null) return;
            foreach (var pair in _state.Visuals)
            {
                WeaverFact fact = pair.Value.Fact;
                if (_state.Objects.TryGetValue(pair.Key, out var existing))
                {
                    var retainedLine = existing.GetComponent<LineRenderer>();
                    if (retainedLine != null) { retainedLine.SetPosition(0, fact.Position); retainedLine.SetPosition(1, fact.End); }
                    continue;
                }
                var root = new GameObject("Web " + fact.Hunter.Value + " " + fact.Kind + " " + fact.Serial);
                root.transform.SetParent(transform, false);
                _state.Objects.Add(pair.Key, root);
                Color color = fact.Kind == WeaverFactKind.WetClick ? _config.WebWarningColor : _config.WebGlowColor;
                if (fact.Kind == WeaverFactKind.DoorwayWebbed)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        quad.name = "Non-colliding webbed doorway"; quad.transform.SetParent(root.transform, false);
                        DestroyOwned(quad.GetComponent<Collider>());
                        quad.transform.SetPositionAndRotation(fact.Position + Vector3.up * _config.WebDoorHeight * .5f, Quaternion.Euler(0f, i * 90f, 0f));
                        quad.transform.localScale = new Vector3(Mathf.Max(_config.WebLineWidth, fact.Radius * 2f), _config.WebDoorHeight, 1f);
                        ApplyMaterial(quad.GetComponent<Renderer>(), color);
                    }
                }
                else
                {
                    var line = root.AddComponent<LineRenderer>();
                    line.useWorldSpace = true; line.positionCount = 2;
                    line.SetPosition(0, fact.Position); line.SetPosition(1, fact.End);
                    line.widthMultiplier = _config.WebLineWidth;
                    line.startColor = line.endColor = Color.white;
                    ApplyMaterial(line, color);
                }
            }
        }
        private void ApplyMaterial(Renderer renderer, Color color)
        {
            renderer.sharedMaterial = _material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock(); properties.SetColor("_Color", color); properties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(properties);
        }
        public void Reset()
        { _presenter.Reset(_state); Apply(); }
        public void Teardown()
        { Reset(); if (_ownsMaterial) DestroyOwned(_material); _material = null; _ownsMaterial = false; _config = null; }
        private void OnDisable() => Reset();
        private void OnDestroy() => Teardown();
        private static void DestroyOwned(Object value)
        { if (value != null) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); } }
    }
}
