// ============================================================================
// CameraHandCatchDriver.cs
// ============================================================================
// PURPOSE:
//   Renders a camera-local hand for confirmed collapse deaths without spawning a
//   gameplay actor. Optional authored mesh/material references replace the explicit
//   palm-and-five-fingers placeholder; both start in the captured native fog color.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by CameraDriver · Presentation · Camera.
// KEY RESPONSIBILITIES:
//   - Own only the catch mesh objects and a private unlit material instance.
//   - Apply supplied approach, reveal and grab values after the camera brain update.
//   - Hide/reset immediately and release all transient resources symmetrically.
// DEPENDENCIES:
//   Own Camera config/presenter; Unity rendering and primitive mesh APIs only.
// USAGE NOTES:
//   Scene-owned; shares CameraDriverConfig because the catch is one Camera facet.
//   No lights, collisions, audio, gameplay or global render settings are changed.
//   Native fog color is sampled once at catch start; no fading screen overlay exists.
//   Placeholder dimensions are normalized mesh authoring, scaled by HandSize.
//   Supply a depth-writing material with _BaseColor; URP/Unlit is the fallback.
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;
namespace Worsen.Presentation.Camera
{
    public sealed class CameraHandCatchDriver : MonoBehaviour
    {
        private CameraDriverConfig _config;
        private GameObject _root;
        private Material _material;
        private Color _fog;
        private Transform[] _fingers;

        public void Initialize(CameraDriverConfig config, UnityEngine.Camera output)
        {
            Teardown();
            if (config == null || output == null) return;
            _config = config;
            _fog = RenderSettings.fogColor;
            if (config.HandMaterial != null) _material = new Material(config.HandMaterial);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) { Debug.LogWarning("Hand catch needs a depth-writing unlit material.", this); return; }
                _material = new Material(shader);
            }
            _material.name = "Owned Hand Catch";
            _root = new GameObject("Hand Catch Close-up");
            _root.transform.SetParent(output.transform, false);
            _root.transform.localScale = Vector3.one * Mathf.Max(.01f, config.HandSize);
            // Use the camera's existing visible layer; do not change the camera mask.
            int layer = 0;
            while (layer < 31 && (output.cullingMask & (1 << layer)) == 0) layer++;
            _root.layer = layer;
            if (config.HandMesh != null)
            {
                var model = new GameObject("Authored Hand");
                model.layer = layer; model.transform.SetParent(_root.transform, false);
                model.transform.localRotation = Quaternion.Euler(config.HandMeshEuler);
                model.AddComponent<MeshFilter>().sharedMesh = config.HandMesh;
                ConfigureRenderer(model.AddComponent<MeshRenderer>());
            }
            else BuildPlaceholder();
            Apply(config.HandStartDistance, 0f, 0f);
        }

        private void BuildPlaceholder()
        {
            Part("Palm", new Vector3(0f, -.05f, 0f), new Vector3(.6f, .7f, .18f));
            _fingers = new Transform[5];
            // Fixed normalized mesh authoring, not timing or effect-strength tuning.
            Vector3[] positions = { new Vector3(-.38f, .04f, 0f), new Vector3(-.23f, .33f, 0f),
                new Vector3(-.07f, .4f, 0f), new Vector3(.10f, .38f, 0f), new Vector3(.25f, .30f, 0f) };
            for (int i = 0; i < _fingers.Length; i++)
            {
                var joint = new GameObject("Finger Joint"); joint.transform.SetParent(_root.transform, false);
                joint.transform.localPosition = positions[i]; _fingers[i] = joint.transform;
                var finger = Part("Finger", Vector3.zero, new Vector3(.13f, .55f, .13f));
                finger.SetParent(joint.transform, false); finger.localPosition = new Vector3(0f, .2f, 0f);
            }
        }
        private Transform Part(string label, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = label; part.layer = _root.layer;
            part.transform.SetParent(_root.transform, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>(); collider.enabled = false; Release(collider);
            ConfigureRenderer(part.GetComponent<Renderer>());
            return part.transform;
        }
        private void ConfigureRenderer(Renderer renderer)
        {
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        public void Apply(float distance, float reveal, float grip)
        {
            if (_root == null || !_root.activeSelf) return;
            _root.transform.localPosition = Vector3.forward * distance;
            _material.SetColor("_BaseColor", CameraHandCatchPresenter.Tint(_fog, _config.HandColor, reveal));
            if (_fingers != null)
                foreach (var finger in _fingers) finger.localRotation = CameraHandCatchPresenter.FingerRotation(grip, _config.HandGripDegrees);
        }
        public void Hide() { if (_root != null) _root.SetActive(false); }
        public void Teardown()
        {
            Hide();
            if (_root != null) Release(_root);
            if (_material != null) Release(_material);
            _root = null; _material = null; _fingers = null; _config = null;
        }
        private static void Release(Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDisable() => Hide();
        private void OnDestroy() => Teardown();
    }
}
