// ============================================================================
// FloorLumenGlow.cs
// ============================================================================
// PURPOSE:
//   Owns one native Lumen fake-light prefab for a room warning or exit glow.
//   The Floor supplies appearance and visibility, so existing warning pulses
//   and opening clocks never create a second simulation loop or real light.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorDriver or FloorExitDoor · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Configure a private Lumen player before activating its effect hierarchy.
//   - Refresh only changed visible appearance and release native meshes on disable.
//   - Leave designer profiles untouched and never fall back to Unity Light.
// DEPENDENCIES:
//   UnityEngine and DistantLands.Lumen runtime engine integration only.
// USAGE NOTES:
//   Scene-owned; no Update or global settings. Designer prefabs contain one
//   white LumenLightLayer with range2 from the shared Editor setup helper.
//   Missing optional prefabs produce no glow in legacy fixtures. Normal scene
//   setup supplies both glow prefabs. Vendor disable clears generated meshes;
//   the parent hierarchy owns disposal. Configuration never edits a shared profile.
//   Requested local visibility is armed even while the owner hierarchy is inactive;
//   hierarchy activation gates actual rendering without requiring Edit Mode callbacks.
// ============================================================================
using System;
using DistantLands.Lumen;
using UnityEngine;

namespace Worsen.Domain.Floor
{
    public sealed class FloorLumenGlow : MonoBehaviour
    {
        private GameObject _effectRoot;
        private LumenEffectPlayer _player;
        private bool _visible;
        private Color _color;
        private float _brightness;

        public void Configure(GameObject prefab, float radius, Color color, float brightness, bool visible)
        {
            if (_effectRoot != null)
            {
                _effectRoot.SetActive(false);
                if (Application.isPlaying) Destroy(_effectRoot); else DestroyImmediate(_effectRoot);
            }
            _effectRoot = null; _player = null;
            _visible = visible; _color = color; _brightness = Mathf.Max(0f, brightness);
            if (prefab == null) return;
            _effectRoot = new GameObject("Floor Native Lumen Effect");
            _effectRoot.SetActive(false); _effectRoot.transform.SetParent(transform, false);
            var effect = Instantiate(prefab, _effectRoot.transform, false);
            _player = effect.GetComponentInChildren<LumenEffectPlayer>(true);
            if (_player == null || _player.profile == null)
                throw new InvalidOperationException("Floor Lumen prefab requires a native effect player and profile.");
            _player.updateFrequency = LumenEffectPlayer.UpdateFrequency.ViaScripting;
            _player.autoAssignSun = false; _player.useLumenSunScript = false;
            _player.initializationBehavior = LumenEffectPlayer.InitializationBehavior.Immediate;
            _player.deinitializationBehavior = LumenEffectPlayer.DeinitializationBehavior.Immediate;
            // The profile's range2 uses Lumen's quadratic shader falloff, not
            // Unity Light.range. Convert the desired radius to its multiplier.
            float safeRadius = Mathf.Max(0.1f, radius);
            _player.range = Mathf.Sqrt(safeRadius * safeRadius + 1f) - 0.5f;
            _player.color = _color; _player.brightness = _brightness;
            effect.SetActive(true);
            _effectRoot.SetActive(_visible && enabled);
        }

        public void SetAppearance(Color color, float brightness)
        {
            brightness = Mathf.Max(0f, brightness);
            if (_color == color && _brightness == brightness) return;
            _color = color; _brightness = brightness;
            if (_player == null) return;
            _player.color = _color; _player.brightness = _brightness;
            if (_effectRoot.activeInHierarchy) _player.RedoEffect(false);
        }
        public void SetColor(Color color) => SetAppearance(color, _brightness);
        public void SetBrightness(float brightness) => SetAppearance(_color, brightness);
        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_effectRoot != null && _effectRoot.activeSelf != (visible && enabled))
                _effectRoot.SetActive(visible && enabled);
        }
        private void OnEnable() { if (_effectRoot != null) _effectRoot.SetActive(_visible); }
        private void OnDisable() { if (_effectRoot != null) _effectRoot.SetActive(false); }
    }
}
