// ============================================================================
// EnvironmentLumenDriver.cs
// ============================================================================
//
// PURPOSE:
//   Builds private Lumen profiles for restrained lanterns and exit-frame rays.
//   Shared assets remain untouched and each layered source occupies one budget slot.
//
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by EnvironmentDriver · Presentation · Environment.
//
// KEY RESPONSIBILITIES:
//   - Use a cold pool with no warm flare for fluorescent fixtures; preserve the shared budget.
//   - Clone lantern profiles, soften ground pools and retain a small source halo.
//   - Build a deterministic exit fan and release its private mesh/profile on teardown.
//
// DEPENDENCIES:
//   - Environment config/state; HorrorLumenPresenter pure math; Lumen runtime SDK.
//
// USAGE NOTES:
//   Scene-owned and created beneath an inactive budget root. No global settings.
//   The owner controls activation and brightness; vendor rendering stays off in Edit Mode.
// ============================================================================
using UnityEngine;
using DistantLands.Lumen;
using Worsen.Presentation.Horror;

namespace Worsen.Presentation.Environment
{
    public sealed class EnvironmentLumenDriver : MonoBehaviour
    {
        private readonly EnvironmentLumenDriverState _state = new EnvironmentLumenDriverState();
        private readonly HorrorLumenPresenter _presenter = new HorrorLumenPresenter();

        public LumenEffectPlayer CreateLamp(EnvironmentDriverConfig config, bool moon, bool fluorescent = false)
        {
            GameObject prefab = moon ? config.LumenMoonPrefab : config.LumenLanternPrefab;
            if (prefab == null) return null;
            GameObject root = Instantiate(prefab, transform, false);
            LumenEffectPlayer player = root.GetComponentInChildren<LumenEffectPlayer>(true);
            if (player == null || player.profile == null) return null;
            _state.Profile = Instantiate(player.profile);
            if (!moon)
                foreach (LumenEffectLayer layer in _state.Profile.layers)
                {
                    if (layer is LumenLightLayer light)
                    {
                        light.range = _presenter.RangeMultiplier(config.GroundPoolRadius, 1f);
                        light.intensity = config.GroundPoolStrength;
                        light.smoothness = config.GroundPoolSoftness;
                        light.isSpotlight = true;
                        Vector2 angles = _presenter.ConeAngles(config.GroundPoolCone);
                        light.minSpotlightAngle = angles.x; light.maxSpotlightAngle = angles.y;
                        light.rotation = new Vector3(90f, 0f, 0f);
                        light.color = fluorescent ? config.FluorescentColor : config.WarmColor;
                    }
                    else if (layer is LumenFlareLayer) layer.brightness = fluorescent ? 0f : config.LanternHaloStrength;
                    else layer.active = false;
                }
            Configure(player, config.LumenFlareScale, config.LumenRangeMultiplier);
            root.SetActive(true);
            return player;
        }

        public LumenEffectPlayer CreateExit(EnvironmentDriverConfig config)
        {
            _state.RayMesh = new Mesh { name = "Owned exit ray" };
            _state.RayMesh.vertices = _presenter.RayVertices(config.ExitRaySize.x, config.ExitRaySize.y);
            // Lumen Ray reads vertex red (not UV alpha) for its opacity envelope.
            _state.RayMesh.colors = new[] { Color.white, Color.black, Color.black };
            _state.RayMesh.uv = new[] { new Vector2(.5f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            _state.RayMesh.triangles = new[] { 0, 1, 2, 2, 1, 0 };
            _state.RayMesh.RecalculateBounds();
            _state.RayMesh.RecalculateNormals();
            _state.Profile = ScriptableObject.CreateInstance<LumenEffectProfile>();
            int count = Mathf.Clamp(config.ExitRayCount, 1, 9);
            for (int i = 0; i < count; i++)
                _state.Profile.layers.Add(new StaticRayLayer { mesh = _state.RayMesh,
                    rotation = new Vector3(0f, _presenter.FanYaw(i, count, config.ExitRaySpread), 0f),
                    color = config.WarmColor, sceneDepthFade = true, sceneDepthFadeEnd = config.ExitRaySize.y });
            var player = gameObject.AddComponent<LumenEffectPlayer>();
            Configure(player, 1f, 1f);
            return player;
        }

        private void Configure(LumenEffectPlayer player, float scale, float range)
        {
            player.enabled = Application.isPlaying;
            player.profile = _state.Profile;
            player.autoAssignSun = false; player.useLumenSunScript = false;
            player.updateFrequency = LumenEffectPlayer.UpdateFrequency.ViaScripting;
            player.initializationBehavior = LumenEffectPlayer.InitializationBehavior.Immediate;
            player.deinitializationBehavior = LumenEffectPlayer.DeinitializationBehavior.Immediate;
            player.scale = scale; player.range = range; player.brightness = 0f;
        }

        public void Teardown()
        {
            gameObject.SetActive(false);
            DestroyOwned(_state.Profile); DestroyOwned(_state.RayMesh);
            _state.Profile = null; _state.RayMesh = null;
        }
        private void OnDestroy() => Teardown();
        private static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
