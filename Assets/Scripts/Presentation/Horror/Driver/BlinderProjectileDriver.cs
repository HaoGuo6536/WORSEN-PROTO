// ============================================================================
// BlinderProjectileDriver.cs
// ============================================================================
// PURPOSE:
//   Displays a small unlit Blinder projectile and its last simulation-step trail.
//   These cosmetics have no collider, audio, targeting or damage authority.
//   The Horror owner supplies launch/hit facts and the same clock as gameplay.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by HorrorDriver · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Own collider-free projectile meshes, trails and their shared generated material.
//   - Apply pure flight poses, retire confirmed hits and clear floor-local visuals.
// DEPENDENCIES:
//   Core Blinder facts; own Presenter/DriverState and HorrorDriverConfig; Unity rendering.
// USAGE NOTES:
//   Scene-owned; explicitly initialized and ticked by HorrorDriver, never an Orchestrator.
//   Shares HorrorDriverConfig's WebShader/WebMaterial, WebGlowColor and WebLineWidth;
//   no additional tunables. Disable/reset releases every visual; Teardown releases material.
//   Launch facts carry no wall-stop fact, so unmatched flights expire at their range.
// ============================================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Worsen.Core;
namespace Worsen.Presentation.Horror
{
    public sealed class BlinderProjectileDriver : MonoBehaviour
    {
        private readonly BlinderProjectilePresenter _presenter = new BlinderProjectilePresenter();
        private readonly Dictionary<(int, int), BlinderProjectileDriverState> _flights = new Dictionary<(int, int), BlinderProjectileDriverState>();
        private readonly Dictionary<(int, int), GameObject> _objects = new Dictionary<(int, int), GameObject>();
        private HorrorDriverConfig _config;
        private Material _material;
        private bool _ownsMaterial;
        public int VisualCount => _objects.Count;
        public void Initialize(HorrorDriverConfig config)
        {
            Teardown();
            if (config == null || (config.WebMaterial == null && config.WebShader == null))
            { Debug.LogError("Blinder projectiles require Horror WebMaterial or WebShader.", this); return; }
            _config = config;
            _material = config.WebMaterial;
            if (_material == null)
            { _material = new Material(config.WebShader) { name = "Owned Blinder projectile" }; _ownsMaterial = true; }
        }
        public void Observe(BlinderThrowFact fact)
        {
            if (_config == null || !isActiveAndEnabled) return;
            var state = _presenter.Create(fact);
            var key = (fact.Hunter.Value, fact.Serial);
            if (state == null || _flights.ContainsKey(key)) return;
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Blinder projectile " + key;
            var collider = body.GetComponent<Collider>(); collider.enabled = false; DestroyOwned(collider);
            body.transform.SetParent(transform, false);
            body.transform.position = fact.Origin;
            body.transform.localScale = Vector3.one * fact.Radius * 2f;
            ApplyMaterial(body.GetComponent<Renderer>());
            var trail = body.AddComponent<LineRenderer>();
            trail.useWorldSpace = true; trail.positionCount = 2;
            trail.SetPosition(0, fact.Origin); trail.SetPosition(1, fact.Origin);
            trail.widthMultiplier = _config.WebLineWidth;
            trail.startColor = trail.endColor = Color.white;
            ApplyMaterial(trail);
            _flights.Add(key, state); _objects.Add(key, body);
        }
        public void ObserveHit(BlinderHitFact fact)
        {
            var key = (fact.Hunter.Value, fact.Serial);
            if (_flights.TryGetValue(key, out var state) && _presenter.Matches(state, fact)) Remove(key);
        }
        public void Tick(float delta)
        {
            foreach (var key in _flights.Keys.ToArray())
            {
                var state = _flights[key];
                if (!_presenter.Tick(state, delta)) { Remove(key); continue; }
                var body = _objects[key]; body.transform.position = state.Position;
                var trail = body.GetComponent<LineRenderer>();
                trail.SetPosition(0, state.PreviousPosition); trail.SetPosition(1, state.Position);
            }
        }
        public void Reset()
        { foreach (var key in _flights.Keys.ToArray()) Remove(key); }
        public void Teardown()
        { Reset(); if (_ownsMaterial) DestroyOwned(_material); _material = null; _ownsMaterial = false; _config = null; }
        private void ApplyMaterial(Renderer renderer)
        {
            renderer.sharedMaterial = _material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", _config.WebGlowColor); properties.SetColor("_BaseColor", _config.WebGlowColor);
            renderer.SetPropertyBlock(properties);
        }
        private void Remove((int, int) key)
        { DestroyOwned(_objects[key]); _objects.Remove(key); _flights.Remove(key); }
        private void OnDisable() => Reset();
        private void OnDestroy() => Teardown();
        private static void DestroyOwned(Object value)
        { if (value != null) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); } }
    }
}
