// ============================================================================
// FloorExitDoorVisual.cs
// ============================================================================
// PURPOSE:
//   Presents a single weathered door, its physical leaf and a one-sided escape.
//   The exported assembly supplies the hinge origin and portal material so builds
//   retain shader references without runtime shader lookup or a second camera.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorExitDoor · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Instantiate the authored assembly or a paneled compatibility fallback.
//   - Attach collision in the same canonical frame as the moving leaf.
//   - Apply the supplied hinge angle and reveal the aperture surface.
//   - Fade the existing native Lumen spill without allocating real lights.
// DEPENDENCIES:
//   FloorDriverConfig, FloorLumenGlow and Unity engine objects only.
// USAGE NOTES:
//   Scene-owned. No Update, subscriptions, shader lookup or global settings.
//   Geometry numbers describe the exported 2 m by 3 m aperture contract, not
//   independent tuning. Config ExitSize scales X/Z together to preserve swing.
//   ExitDoorPrefab must contain DoorLeaf, DoorFrame and EscapeSurface; old castle
//   leaves are deliberately not instantiated. Editor setup maps EscapeSurface's
//   material to Worsen/ExitPortal. Shared materials are never changed at runtime.
//   Audio remains on the existing OpeningProgress -> AudioOrchestrator route.
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;

namespace Worsen.Domain.Floor
{
    public sealed class FloorExitDoorVisual : MonoBehaviour
    {
        private Transform _leaf;
        private Renderer _escape;
        private FloorLumenGlow _glow;
        private FloorDriverConfig _config;

        public void Configure(FloorDriverConfig config, Material wood, Material stone, Material metal)
        {
            _config = config;
            transform.localScale = new Vector3(config.ExitSize.x / 2f, config.ExitSize.y / 3f, config.ExitSize.x / 2f);
            bool authored = HasAssembly(config.ExitDoorPrefab);
            if (authored)
            {
                var model = Instantiate(config.ExitDoorPrefab, transform, false);
                model.name = "Weathered Exit Door";
                foreach (var behavior in model.GetComponentsInChildren<MonoBehaviour>(true)) behavior.enabled = false;
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                model.SetActive(true);
                foreach (var child in model.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "DoorLeaf") _leaf = child;
                    if (child.name == "EscapeSurface") _escape = child.GetComponent<Renderer>();
                }
                if (_escape != null)
                {
                    _escape.shadowCastingMode = ShadowCastingMode.Off;
                    _escape.receiveShadows = false;
                    _escape.enabled = false;
                    if (_escape.sharedMaterial == null || _escape.sharedMaterial.shader.name != "Worsen/ExitPortal")
                    {
                        Debug.LogError("Exit EscapeSurface requires the Worsen/ExitPortal material. Run exit asset setup.", this);
                        _escape = null;
                    }
                }
            }
            else BuildFallback(wood, metal);

            // Invisible structural collision is independent of decorative bevels.
            Collision("Left Door Support", transform, new Vector3(-1.08f, 1.5f, 0f), new Vector3(.16f, 3f, .26f));
            Collision("Right Door Support", transform, new Vector3(1.08f, 1.5f, 0f), new Vector3(.16f, 3f, .26f));
            Collision("Door Crossbar", transform, new Vector3(0f, 3.08f, 0f), new Vector3(2.32f, .16f, .26f));
            Collision("Door Leaf Collision", _leaf, new Vector3(1f, 1.5f, 0f), new Vector3(1.98f, 2.96f, .16f));
            if (!authored)
                Block("Threshold", transform, new Vector3(0f, .02f, 0f), new Vector3(2.32f, .04f, .4f), stone);
            var lightRoot = new GameObject("Exit Threshold Glow");
            lightRoot.transform.SetParent(transform, false);
            lightRoot.transform.localPosition = new Vector3(0f, 2.25f, -.55f);
            _glow = lightRoot.AddComponent<FloorLumenGlow>();
            _glow.Configure(config.LumenExitGlowPrefab, Mathf.Max(config.ExitSize.x, config.ExitSize.y) * 2f,
                config.ExitLockedColor, .3f, true);
            Apply(0f, 0f);
        }

        public void Apply(float angle, float progress)
        {
            _leaf.localRotation = Quaternion.Euler(0f, angle, 0f);
            if (_escape != null) _escape.enabled = progress > 0f;
            _glow.SetAppearance(Color.Lerp(_config.ExitLockedColor, _config.ExitOpenColor, progress), Mathf.Lerp(.3f, 2.5f, progress));
        }

        private static bool HasAssembly(GameObject prefab)
        {
            if (prefab == null) return false;
            bool leaf = false, frame = false, escape = false;
            foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
            {
                leaf |= child.name == "DoorLeaf";
                frame |= child.name == "DoorFrame";
                escape |= child.name == "EscapeSurface" && child.GetComponent<Renderer>() != null;
            }
            return leaf && frame && escape;
        }

        private void BuildFallback(Material wood, Material metal)
        {
            Block("DoorFrame Left", transform, new Vector3(-1.08f, 1.5f, 0f), new Vector3(.16f, 3f, .26f), wood);
            Block("DoorFrame Right", transform, new Vector3(1.08f, 1.5f, 0f), new Vector3(.16f, 3f, .26f), wood);
            Block("DoorFrame Crown", transform, new Vector3(0f, 3.08f, 0f), new Vector3(2.32f, .16f, .26f), wood);
            var leaf = new GameObject("DoorLeaf"); leaf.transform.SetParent(transform, false);
            _leaf = leaf.transform; _leaf.localPosition = Vector3.left;
            Block("Paneled Leaf", _leaf, new Vector3(1f, 1.5f, 0f), new Vector3(1.98f, 2.96f, .12f), wood);
            foreach (float x in new[] { .18f, 1f, 1.82f })
                Block("Leaf Stile", _leaf, new Vector3(x, 1.5f, -.075f), new Vector3(.12f, 2.96f, .04f), wood);
            foreach (float y in new[] { .16f, 1.08f, 2.84f })
                Block("Leaf Rail", _leaf, new Vector3(1f, y, -.075f), new Vector3(1.9f, .16f, .04f), wood);
            Block("Brass Latch", _leaf, new Vector3(1.8f, 1.3f, -.13f), new Vector3(.1f, .26f, .09f), metal);
        }

        private static void Collision(string name, Transform parent, Vector3 center, Vector3 size)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            var collider = root.AddComponent<BoxCollider>(); collider.center = center; collider.size = size;
        }
        private static void Block(string name, Transform parent, Vector3 center, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = center; part.transform.localScale = size;
            part.GetComponent<Collider>().enabled = false;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
