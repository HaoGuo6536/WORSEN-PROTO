// ============================================================================
// RoomCollapseVolume.cs
// ============================================================================
// PURPOSE:
//   Renders cracks, clipped mist, and pooled hands without closure walls or lethal triggers.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorDriver Â· Domain Â· Floor.
// KEY RESPONSIBILITIES:
//   - Route warning phase and pulse changes to a native Lumen fake-light effect.
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
// USAGE NOTES:
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
// ============================================================================
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    public sealed class RoomCollapseVolume : MonoBehaviour
    {
        private readonly RoomCollapseDriverState _state = new RoomCollapseDriverState();
        private readonly RoomCollapsePresenter _presenter = new RoomCollapsePresenter();
        private FloorDriverConfig _config;
        public int RoomId => _state.RoomId;
        public RoomPhase Phase => _state.Phase;
        public int HandCount => _state.Hands.Count;
        public Bounds RoomBounds => _state.Bounds;

        public void Configure(LevelRoom room, FloorDriverConfig config, Material darkMaterial, FloorLumenGlow warning)
        {
            _config = config; _state.Bounds = room.Bounds; _state.RoomId = room.Id; _state.Warning = warning;
            _state.Phase = RoomPhase.Open; warning.SetVisible(false);
            int width = config.HandGridWidth;
            var fogMaterial = config.MistMaterial != null ? config.MistMaterial : MakeFogMaterial();
            for (int i = 0; i < width * width; i++)
            {
                Vector3 point = _presenter.GridPoint(room.Bounds, i, width, config.PortalInset);
                point.y = SurfaceHeight(point, room.Bounds);
                AddHand(point, i, width, darkMaterial, fogMaterial, true);
                if (point.y > room.Bounds.min.y + 1.5f)
                    AddHand(new Vector3(point.x,room.Bounds.min.y + 0.015f,point.z), i, width, darkMaterial, fogMaterial, false);
            }
            BuildCracks(config.CrackMaterial != null ? config.CrackMaterial : darkMaterial);
        }

        private void AddHand(Vector3 point, int gridIndex, int width, Material darkMaterial, Material fogMaterial, bool addMist)
        {
            int index = _state.Hands.Count;
            var hand = _config.HandPrefab != null ? Instantiate(_config.HandPrefab, transform, false) : BuildHand(darkMaterial);
            hand.name = "Shadow Hand " + index; hand.transform.position = point;
            hand.transform.rotation = Quaternion.Euler(0f, index * 137.5f, 0f);
            foreach (var collider in hand.GetComponentsInChildren<Collider>()) collider.enabled = false;
            hand.SetActive(false); _state.Hands.Add(hand.transform); _state.HandPositions.Add(point);
            _state.Mist.Add(addMist ? BuildMist(gridIndex,point,width,fogMaterial) : null);
        }
        public void ApplyHandFact(CollapseHandFact fact)
        {
            for (int i=0;i<_state.HandPositions.Count;i++)
            {
                if (Vector3.SqrMagnitude(_state.HandPositions[i]-fact.Position)>0.01f) continue;
                foreach(var skin in _state.Hands[i].GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if(skin.sharedMesh==null)continue;
                    int index=skin.sharedMesh.GetBlendShapeIndex("Grasp");
                    if(index>=0)skin.SetBlendShapeWeight(index,_presenter.GripWeight(fact.Kind));
                }
            }
        }

        public void ApplyPhase(RoomPhase phase, Color warningColor, Color closedColor)
        {
            _state.Phase = phase;
            if (_state.Warning == null) return;
            _state.Warning.SetColor(warningColor);
            _state.Warning.SetVisible(phase == RoomPhase.Telegraph || phase == RoomPhase.Tearing);
        }
        public void PreviewCracks() => _state.OptionalCracks = true;
        public void SetWarningIntensity(float intensity) { if (_state.Warning != null) _state.Warning.SetBrightness(intensity); }
        public void ApplyDestruction(RoomDestructionSample sample, float elapsed)
        {
            _state.Phase = sample.Phase; _state.Progress = sample.Progress; _state.Elapsed = elapsed;
            float mist = _presenter.MistProgress(sample.Phase, sample.Progress);
            float cracks = sample.Phase == RoomPhase.Open ? (_state.OptionalCracks ? 0.18f : 0f) :
                sample.Phase == RoomPhase.Telegraph ? Mathf.Lerp(0.08f, 1f, sample.Progress) : 1f;
            foreach (var crack in _state.Cracks)
            {
                crack.enabled = cracks > 0f;
                crack.widthMultiplier = Mathf.Lerp(0.012f, 0.11f, cracks);

            }
            for (int i = 0; i < _state.Hands.Count; i++)
            {
                float reveal = _presenter.HandReveal(_state.Bounds, _state.HandPositions[i], mist);
                var hand = _state.Hands[i]; hand.gameObject.SetActive(reveal > 0.01f);
                hand.localScale = _presenter.HandScale(reveal, elapsed, i, _config.HandVisualScale);
                hand.localRotation = Quaternion.Euler(Mathf.Sin(elapsed + i) * 6f, i * 137.5f, Mathf.Cos(elapsed * 0.7f + i) * 7f);
                var fog = _state.Mist[i];
                if (fog == null) continue;
                if (reveal > 0.01f && !fog.gameObject.activeSelf) { fog.gameObject.SetActive(true); fog.Play(); }
                if (reveal <= 0.01f && fog.gameObject.activeSelf) { fog.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); fog.gameObject.SetActive(false); }
            }
        }

        public FloorHandProbe Probe(Vector3 playerPosition, int preferredHand = -1)
        {
            if (!_presenter.Contains(_state.Bounds, playerPosition, _config.PortalInset)) return default;
            int selected = -1; float nearest = float.PositiveInfinity;
            for (int i = 0; i < _state.HandPositions.Count; i++)
            {
                if (preferredHand >= 0 && i != preferredHand || !_state.Hands[i].gameObject.activeSelf) continue;
                Vector3 hand = _state.HandPositions[i];
                if (Mathf.Abs(playerPosition.y - hand.y) > 1.5f) continue;
                float distance = Vector3.Distance(playerPosition, hand);
                if (distance >= nearest || Occluded(hand + Vector3.up * 0.55f, playerPosition + Vector3.up * 0.6f)) continue;
                nearest = distance; selected = i;
            }
            return selected < 0 ? default : new FloorHandProbe(_state.RoomId, selected, _state.HandPositions[selected], nearest, true);
        }
        public bool PickupOvertaken(Vector3 position)
        {
            if (_state.Phase == RoomPhase.Closed) return true;
            float mist = _presenter.MistProgress(_state.Phase, _state.Progress);
            return mist > 0f && _presenter.Contains(_state.Bounds, position, _config.PortalInset) &&
                _presenter.InwardFraction(_state.Bounds, position) < mist - 0.08f;
        }

        private bool Occluded(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            foreach (var hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.attachedRigidbody == null && !hit.collider.transform.IsChildOf(transform)) return true;
            return false;
        }
        private float SurfaceHeight(Vector3 point, Bounds bounds)
        {
            float floor = bounds.min.y;
            foreach (var hit in Physics.RaycastAll(new Vector3(point.x, bounds.max.y - 0.05f, point.z), Vector3.down, bounds.size.y, ~0, QueryTriggerInteraction.Ignore))
                if (hit.normal.y > 0.65f && hit.collider.attachedRigidbody == null && !hit.collider.transform.IsChildOf(transform))
                    floor = Mathf.Max(floor, hit.point.y);
            return floor + 0.015f;
        }

        private GameObject BuildHand(Material material)
        {
            var root = new GameObject("Hand"); root.transform.SetParent(transform, false);
            // One palm and five articulated silhouettes are a readable fallback;
            // setup supplies a project-owned spectral mesh for the final art pass.
            Part(root.transform, new Vector3(0f, 0.38f, 0f), new Vector3(0.45f, 0.7f, 0.15f), Quaternion.identity, material);
            for (int digit = 0; digit < 5; digit++)
            {
                float length = digit == 0 || digit == 4 ? 0.48f : 0.7f;
                Part(root.transform, new Vector3((digit - 2) * 0.115f, 0.72f + length * 0.45f, -0.05f),
                    new Vector3(0.075f, length, 0.085f), Quaternion.Euler(-22f, 0f, (digit - 2) * -8f), material);
            }
            return root;
        }
        private static void Part(Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.transform.SetParent(parent, false); part.transform.localPosition = position;
            part.transform.localScale = scale; part.transform.localRotation = rotation;
            part.GetComponent<Collider>().enabled = false; part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private ParticleSystem BuildMist(int index, Vector3 point, int width, Material material)
        {
            var root = new GameObject("Clipped Shadow Mist " + index); root.transform.SetParent(transform, false);
            float cell = Mathf.Min(_state.Bounds.size.x, _state.Bounds.size.z) / width;
            root.transform.position = new Vector3(point.x, _state.Bounds.center.y, point.z);
            var fog = root.AddComponent<ParticleSystem>(); fog.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fog.useAutoRandomSeed = false; fog.randomSeed = (uint)(1 + _state.RoomId * 97 + index);
            var main = fog.main; main.loop = true; main.duration = 8f; main.startLifetime = 8f;
            main.startSpeed = 0f; main.startSize = Mathf.Min(cell * 0.9f, 1.9f);
            main.startColor = new Color(0.012f, 0.008f, 0.025f, 0.9f); main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = fog.emission; emission.rateOverTime = 4f;
            var shape = fog.shape; shape.shapeType = ParticleSystemShapeType.Box;
            float margin = main.startSize.constant;
            shape.scale = new Vector3(Mathf.Max(0.05f, cell - margin), Mathf.Max(0.05f, _state.Bounds.size.y - margin - 0.1f), Mathf.Max(0.05f, cell - margin));
            var color = fog.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            fog.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            root.SetActive(false); return fog;
        }
        private Material MakeFogMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader); _state.OwnedMaterials.Add(material);
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Collapse soft fog falloff" };
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float radial = Mathf.Clamp01(1f - new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude);
                pixels[y * 32 + x] = new Color(1f, 1f, 1f, radial * radial);
            }
            texture.SetPixels(pixels); texture.Apply(); material.mainTexture = texture; _state.OwnedResources.Add(texture);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            return material;
        }

        private void BuildCracks(Material material)
        {
            var bounds = _state.Bounds;
            for (int surface = 0; surface < 6; surface++) for (int branch = 0; branch < 4; branch++)
            {
                Vector3 normal = surface < 2 ? Vector3.up : surface < 4 ? Vector3.right : Vector3.forward;
                if (surface % 2 == 0) normal = -normal;
                Vector3 tangent = surface < 2 ? Vector3.right : Vector3.up;
                Vector3 bitangent = Vector3.Cross(normal, tangent);
                Vector3 center = bounds.center + Vector3.Scale(normal, bounds.extents - Vector3.one * 0.07f);
                center += bitangent * ((branch - 1.5f) * Mathf.Min(bounds.size.x, bounds.size.z) * 0.17f);
                float length = surface < 2 ? bounds.size.x * 0.78f : bounds.size.y * 0.85f;
                var root = new GameObject("Surface Fracture " + surface + " " + branch); root.transform.SetParent(transform, false);
                var line = root.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = true;
                line.positionCount = 9; line.SetPositions(_presenter.CrackPath(center, tangent, bitangent, length, surface * 7 + branch));
                line.widthMultiplier = 0.02f; line.numCapVertices = 2; line.enabled = false; _state.Cracks.Add(line);
            }
        }
        private void OnDestroy()
        {
            foreach (var resource in _state.OwnedResources) Release(resource);
            foreach (var material in _state.OwnedMaterials) Release(material);
        }
        private static void Release(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
