// ============================================================================
// HorrorMicroEventDriver.cs
// ============================================================================
// PURPOSE:
//   Samples conservative camera visibility for injected micro-event candidates.
//   It renders a collision-free silhouette only at owner-certified unreachable anchors.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by HorrorDriver · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Offer decisions as primitive facts and own the temporary placeholder's lifetime.
// DEPENDENCIES:
//   - Core read-only interactables; Unity camera/physics and owned Horror math/config.
// USAGE NOTES:
//   Scene-owned. No autonomous Update: accepted gameplay ticks drive scheduling and expiry.
//   Anchors must be certified by the level owner for ALL active hunter traversal capabilities.
//   No anchors or player-open provenance means no corresponding event; never guess either.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Horror
{
    public sealed class HorrorMicroEventDriver : MonoBehaviour
    {
        private readonly HorrorMicroEventDriverState _state = new HorrorMicroEventDriverState();
        public event Action<int, int, Vector3, float> Selected;

        public void ResetRun(HorrorDriverConfig config, System.Random random)
        { ResetFloor(); HorrorMicroEventPresenter.ResetRun(_state, config, random); }

        public void SetWorld(IReadOnlyInteractableSet world, IReadOnlyList<Vector3> unreachableAnchors)
        {
            _state.World = world;
            _state.UnreachableAnchors = new Vector3[unreachableAnchors?.Count ?? 0];
            for (int i = 0; i < _state.UnreachableAnchors.Length; i++) _state.UnreachableAnchors[i] = unreachableAnchors[i];
        }
        public void ObservePlayerOpenedDoor(int id, Bounds bounds) { if (id > 0) _state.OpenedDoors[id] = bounds; }
        public void SetCounterAvailable(bool available) => _state.CounterAvailable = available;
        public void ObserveProximity(ProximitySample sample)
        {
            if (!sample.Player.IsValid) return;
            _state.ChaseKnown = true; _state.Chases.Clear();
            SetChase(sample.ChaseId, sample.InChase);
        }
        public void InvalidateChase() { _state.ChaseKnown = false; ClearSilhouette(); }
        public void SetChase(int id, bool active)
        {
            if (active) { _state.Chases.Add(id); ClearSilhouette(); }
            else _state.Chases.Remove(id);
        }

        public void Tick(HorrorDriverConfig config, UnityEngine.Camera camera, double now, float dt)
        {
            _state.SilhouetteRemaining = Mathf.Max(0f, _state.SilhouetteRemaining - dt);
            if (_state.SilhouetteRemaining <= 0f) ClearSilhouette();
            if (camera == null || now < _state.NextSeconds || !_state.ChaseKnown || _state.Chases.Count != 0
                || _state.Used >= Mathf.Max(0, config.MicroEventsPerRun))
            { HorrorMicroEventPresenter.Select(_state, config, now, false, false, false); return; }
            int doorId = 0;
            Vector3 doorPosition = default, anchor = default;
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            if (_state.World != null)
                foreach (var item in _state.OpenedDoors)
                    if (_state.World.TryGet(item.Key, out var door) && HorrorMicroEventPresenter.DoorEligible(door, item.Value, true,
                        GeometryUtility.TestPlanesAABB(planes, item.Value), camera.transform.position, camera.transform.forward,
                        config.MicroEventMinimumDistance) && (doorId == 0 || item.Key < doorId))
                    { doorId = item.Key; doorPosition = door.Position; }
            bool silhouette = false;
            foreach (var position in _state.UnreachableAnchors)
                if (Vector3.Distance(camera.transform.position, position) >= config.MicroEventMinimumDistance
                    && HorrorMicroEventPresenter.AtViewEdge(camera.WorldToViewportPoint(position), config.SilhouetteEdgeFraction)
                    && !Physics.Linecast(camera.transform.position, position, ~0, QueryTriggerInteraction.Ignore))
                { silhouette = true; anchor = position; break; }
            int kind = HorrorMicroEventPresenter.Select(_state, config, now, doorId != 0, silhouette, _state.CounterAvailable);
            if (kind != 0) Selected?.Invoke(kind, kind == 1 ? doorId : 0, kind == 1 ? doorPosition : anchor,
                kind == 2 ? config.SilhouetteSeconds : kind == 3 ? config.CounterCakeSeconds : 0f);
        }

        public bool ShowSilhouette(Vector3 position, float seconds, HorrorDriverConfig config, Material material)
        {
            if (!_state.ChaseKnown || _state.Chases.Count != 0 || material == null || seconds <= 0f) return false;
            ClearSilhouette();
            _state.Silhouette = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _state.Silhouette.name = "Unverifiable silhouette (cosmetic)";
            _state.Silhouette.SetActive(false);
            var collider = _state.Silhouette.GetComponent<Collider>();
            collider.enabled = false;
            _state.Silhouette.transform.SetParent(transform, false);
            _state.Silhouette.transform.position = position;
            _state.Silhouette.transform.localScale = config.SilhouetteScale;
            var renderer = _state.Silhouette.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", Color.black); properties.SetColor("_Color", Color.black);
            renderer.SetPropertyBlock(properties);
            _state.SilhouetteRemaining = seconds;
            _state.Silhouette.SetActive(true);
            return true;
        }

        public void ResetFloor()
        {
            ClearSilhouette(); _state.Chases.Clear(); _state.OpenedDoors.Clear();
            _state.ChaseKnown = false;
            _state.World = null; _state.UnreachableAnchors = new Vector3[0];
        }
        public void ClearSilhouette()
        {
            if (_state.Silhouette == null) return;
            _state.Silhouette.SetActive(false);
            if (Application.isPlaying) Destroy(_state.Silhouette); else DestroyImmediate(_state.Silhouette);
            _state.Silhouette = null; _state.SilhouetteRemaining = 0f;
        }
        private void OnDisable() => InvalidateChase();
        private void OnDestroy() => ClearSilhouette();
    }
}
