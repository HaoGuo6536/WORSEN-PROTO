// ============================================================================
// HorrorAfterglowDriver.cs
// ============================================================================
// PURPOSE:
//   Displays accepted Afterglow windows using the project's authored Lumen fill.
//   It owns temporary light instances and leaves environment objects and safety rules alone.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by HorrorDriver · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Render supplied dying-light envelopes without editing shared profiles.
//   - Release transient lights on expiry, floor reset, disable and teardown.
// DEPENDENCIES:
//   Core observations, own Horror config/presenters and wrapped Lumen engine API.
// USAGE NOTES:
//   Scene-owned. Requires the existing LumenNearFillPrefab; missing wiring warns,
//   never creates an unapproved Unity point-light fallback. No gameplay outputs.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using DistantLands.Lumen;
namespace Worsen.Presentation.Horror
{
    public sealed class HorrorAfterglowDriver : MonoBehaviour
    {
        private readonly HorrorAfterglowDriverState state = new HorrorAfterglowDriverState();
        private readonly Dictionary<int, LumenEffectPlayer> lights = new Dictionary<int, LumenEffectPlayer>();
        private readonly HorrorLumenPresenter lumen = new HorrorLumenPresenter();
        private HorrorDriverConfig config;
        private IReadOnlyActiveEffects effects;
        public void Initialize(HorrorDriverConfig value) { Clear(); config = value; }
        public void SetEffects(IReadOnlyActiveEffects value)
        { effects = value; if (effects?.Has(new EffectId("afterglow")) != true) Clear(); }
        public void Observe(InteractableState light, float seconds)
        {
            if (config == null || !isActiveAndEnabled) return;
            HorrorAfterglowPresenter.Observe(state, effects, light, seconds);
            Apply();
        }
        public void Tick(float dt) { HorrorAfterglowPresenter.Tick(state, dt); Apply(); }
        private void Apply()
        {
            state.Expired.Clear();
            foreach (var pair in lights)
                if (!state.Lights.TryGetValue(pair.Key, out var entry) || entry.Remaining <= 0f) state.Expired.Add(pair.Key);
            foreach (int id in state.Expired) { Release(lights[id].gameObject); lights.Remove(id); }
            foreach (var entry in state.Lights.Values)
            {
                if (entry.Remaining <= 0f) continue;
                if (!lights.TryGetValue(entry.Light.Id, out var player))
                {
                    if (config.LumenNearFillPrefab == null)
                    { Debug.LogWarning("Afterglow requires the authored Lumen near-fill prefab.", this); entry.Remaining = 0f; continue; }
                    var guard = new GameObject("Afterglow initialization");
                    guard.SetActive(false); guard.transform.SetParent(transform, false);
                    var root = Instantiate(config.LumenNearFillPrefab, guard.transform, false);
                    root.SetActive(false); root.transform.SetParent(transform, false); Release(guard);
                    root.name = "Afterglow " + entry.Light.Id;
                    player = root.GetComponent<LumenEffectPlayer>();
                    if (player == null || player.profile == null)
                    { Release(root); entry.Remaining = 0f; Debug.LogWarning("Afterglow prefab requires a Lumen player/profile.", this); continue; }
                    player.enabled = Application.isPlaying;
                    player.autoAssignSun = false; player.useLumenSunScript = false;
                    player.updateFrequency = LumenEffectPlayer.UpdateFrequency.ViaScripting;
                    player.initializationBehavior = LumenEffectPlayer.InitializationBehavior.Immediate;
                    player.deinitializationBehavior = LumenEffectPlayer.DeinitializationBehavior.Immediate;
                    lights.Add(entry.Light.Id, player);
                }
                player.transform.position = entry.Light.Position;
                float basis = 2f;
                foreach (var layer in player.profile.layers) if (layer is LumenLightLayer light) { basis = light.range; break; }
                player.range = lumen.RangeMultiplier(config.AfterglowRadius, basis);
                player.color = config.AfterglowColor;
                player.brightness = HorrorAfterglowPresenter.Intensity(entry, config.AfterglowStrength);
                player.gameObject.SetActive(true);
                if (Application.isPlaying) player.RedoEffect(false);
            }
        }
        public void Clear()
        {
            foreach (var player in lights.Values) if (player != null) Release(player.gameObject);
            lights.Clear(); state.Lights.Clear(); state.Expired.Clear();
        }
        private static void Release(Object target)
        {
            if (target is GameObject owner) owner.SetActive(false);
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();
    }
}
