// ============================================================================
// FloorCakeVisual.cs
// ============================================================================
// PURPOSE:
//   Builds a small tiered birthday cake when no authored cake visual is supplied.
//   Saturated icing, a repeated name and a candle replace the collectible sphere.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorDriver · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Build collider-free cake decoration and apply injected-time candle flicker.
//   - Keep traps on the same silhouette with an unlit candle; preserve golden tint.
// DEPENDENCIES:
//   UnityEngine; FloorCakePresenter and shared FloorDriverConfig. No foreign systems.
// USAGE NOTES:
//   Scene-owned; FloorDriver owns materials, glow siblings and hierarchy disposal.
//   Geometry ratios are placeholder art at unit scale, not gameplay collision tuning.
//   No Update or global settings; Tick receives the Floor's elapsed simulation time.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Floor
{
    public sealed class FloorCakeVisual : MonoBehaviour
    {
        private readonly FloorCakePresenter _presenter = new FloorCakePresenter();
        private FloorDriverConfig _config;
        private Light _light;
        private Transform _flame;
        public void Configure(FloorDriverConfig config, Material body, Material icing, Material wax, Material flame, bool trap)
        {
            _config = config;
            transform.localScale = Vector3.one * config.CakeVisualScale;
            Part("Lower Tier", PrimitiveType.Cylinder, new Vector3(0f, -0.14f, 0f), new Vector3(0.72f, 0.13f, 0.72f), body);
            Part("Frosting Rim", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.76f, 0.025f, 0.76f), icing);
            Part("Upper Tier", PrimitiveType.Cylinder, new Vector3(0f, 0.09f, 0f), new Vector3(0.5f, 0.075f, 0.5f), body);
            Part("Upper Frosting", PrimitiveType.Cylinder, new Vector3(0f, 0.17f, 0f), new Vector3(0.54f, 0.025f, 0.54f), icing);
            Part("Candle", PrimitiveType.Cylinder, new Vector3(0f, 0.31f, 0.1f), new Vector3(0.045f, 0.12f, 0.045f), wax);
            _flame = Part("Flame", PrimitiveType.Sphere, new Vector3(0f, 0.46f, 0.1f), new Vector3(0.055f, 0.11f, 0.055f), flame);
            _flame.gameObject.SetActive(!trap);
            _light = _flame.gameObject.AddComponent<Light>(); _light.type = LightType.Point;
            _light.color = config.CandleColor; _light.range = config.CandleRange;
            _light.shadows = LightShadows.None; _light.enabled = !trap;
            var label = new GameObject("Piped Name"); label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(0f, 0.2f, -0.08f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = label.AddComponent<TextMesh>(); text.text = config.PipedName;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.characterSize = 0.035f; text.fontSize = 48; text.color = config.CakeColor;
            Tick(0f);
        }
        public void Tick(float elapsed)
        {
            if (_light == null || !_light.enabled) return;
            _light.intensity = _config.CandleIntensity * _presenter.Flicker(elapsed, _config.CandleFlickerRate, _config.CandleFlickerDepth);
        }
        private Transform Part(string label, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type); part.name = label;
            part.transform.SetParent(transform, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            var collider = part.GetComponent<Collider>(); collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            return part.transform;
        }
    }
}
