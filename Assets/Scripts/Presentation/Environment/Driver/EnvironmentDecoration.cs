// ============================================================================
// EnvironmentDecoration.cs
// ============================================================================
// PURPOSE:
//   Creates one measured visual under an inactive room without activating a prefab.
//   Mesh-local bounds retain child transforms and imported scale, unlike renderer
//   world bounds that can be empty before the hierarchy has been enabled.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by EnvironmentDriver · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Instantiate and measure static or skinned mesh bounds in the placement frame.
//   - Apply the pure fit/contact solution or reject an unmeasurable or absurd fit.
//   - Remove imported lights and disable collision/audio in decorative instances.
// DEPENDENCIES:
//   - Own PlacementPresenter and DriverConfig; Unity mesh and transform APIs.
// USAGE NOTES:
//   Scene-owned by the room root, with no private native allocations. Configure once.
//   The caller destroys a rejected root; imported child rotations/scales are preserved.
// ============================================================================
using UnityEngine;

namespace Worsen.Presentation.Environment
{
    public sealed class EnvironmentDecoration : MonoBehaviour
    {
        public Vector3 LightPosition { get; private set; }
        public bool Configure(GameObject prefab, EnvironmentSlot slot, EnvironmentDriverConfig config,
            bool floor, bool ceiling = false, bool alignLongAxis = true, float modelYaw = 0f)
        {
            if (prefab == null) return false;
            transform.SetPositionAndRotation(slot.Position, Quaternion.Euler(0f, slot.Yaw, 0f));
            var item = Instantiate(prefab, transform, false);
            item.transform.localPosition = Vector3.zero;
            foreach (Collider collider in item.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (AudioSource audio in item.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (Light light in item.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
                foreach (Component component in light.GetComponents<Component>())
                    if (component != null && component.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalAdditionalLightData")
                        DestroyImmediate(component);
                DestroyImmediate(light);
            }
            if (!TryMeasure(out Bounds mesh)) return false;
            float adjustment = modelYaw + (alignLongAxis ? EnvironmentPlacementPresenter.LongAxisYaw(mesh.size) : 0f);
            item.transform.localRotation = Quaternion.Euler(0f, adjustment, 0f) * item.transform.localRotation;
            if (!TryMeasure(out mesh)) return false;
            float scale = EnvironmentPlacementPresenter.FitScale(mesh.size, slot.Envelope,
                config.MinimumPropScale, config.MaximumPropScale);
            if (scale == 0f) return false;
            Vector3 offset = EnvironmentPlacementPresenter.PlacementOffset(mesh, slot.Envelope, scale, floor, ceiling);
            item.transform.localScale *= scale;
            item.transform.localPosition = offset;
            var placed = new Bounds(mesh.center * scale + offset, mesh.size * scale);
            LightPosition = transform.TransformPoint(EnvironmentPlacementPresenter.FixtureSource(placed, ceiling));
            item.SetActive(true);
            return true;
        }

        private bool TryMeasure(out Bounds result)
        {
            result = default; bool found = false;
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled) continue;
                Include(filter.sharedMesh.bounds, filter.transform, ref result, ref found);
            }
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh != null && renderer.enabled)
                    Include(renderer.sharedMesh.bounds, renderer.transform, ref result, ref found);
            return found;
        }

        private void Include(Bounds mesh, Transform child, ref Bounds result, ref bool found)
        {
            Bounds measured = EnvironmentPlacementPresenter.TransformBounds(mesh, transform.worldToLocalMatrix * child.localToWorldMatrix);
            if (found) result.Encapsulate(measured); else { result = measured; found = true; }
        }
    }
}
