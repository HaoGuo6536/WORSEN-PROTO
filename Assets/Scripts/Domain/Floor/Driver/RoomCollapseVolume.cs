// ============================================================================
// RoomCollapseVolume.cs
// ============================================================================
// PURPOSE:
//   Operates a collapse boundary trigger independently of pooled reaching hand art and cracks.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorDriver Â· Domain Â· Floor.
// KEY RESPONSIBILITIES:
//   - Apply deterministic hand flailing/player strain and native Lumen warning pulses.
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
//   - Reconcile overlap contacts before each Floor tick, including stationary actors on activation.
//   - Build cell-local pools and triggers; never probe or reach across a missing notch.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
// USAGE NOTES:
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
//   Visual colliders stay disabled. The dedicated trigger includes the room interior
//   so penetrating or spawning inside a consumed room cannot evade its boundary.
//   Pooled query buffers grow on saturation, retry without truncation and return on destroy.
//   Doorway mist is owned by Presentation/Fog from published portal geometry, not hand cells.
// ============================================================================
using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    public sealed class RoomCollapseVolume : MonoBehaviour
    {
        private readonly RoomCollapseDriverState _state = new RoomCollapseDriverState();
        private readonly RoomCollapsePresenter _presenter = new RoomCollapsePresenter();
        private FloorDriverConfig _config;
        private bool _missingShaderReported;
        public int RoomId => _state.RoomId;
        public RoomPhase Phase => _state.Phase;
        public int HandCount => _state.Hands.Count;
        public Bounds RoomBounds => _state.Bounds;
        public bool ContainsXZ(Vector3 position) => _state.Room.ContainsXZ(position);

        public void Configure(LevelRoom room, FloorDriverConfig config, Material darkMaterial, FloorLumenGlow warning,
            Func<Collider, EntityId> resolveIdentity = null, float boundaryReach = 0f, int minimumHands = 0)
        {
            if (config == null || (config.MistMaterial == null && config.MistShader == null))
            {
                const string error = "FloorDriverConfig requires MistMaterial or MistShader. Rebuild Floor assets.";
                if (!_missingShaderReported) { _missingShaderReported = true; Debug.LogError(error, this); }
                throw new InvalidOperationException(error);
            }
            _config = config; _state.Bounds = room.Bounds; _state.Room = room; _state.RoomId = room.Id; _state.Warning = warning;
            if (_state.QueryHits == null) _state.QueryHits = ArrayPool<RaycastHit>.Shared.Rent(64);
            if (_state.QueryOverlaps == null) _state.QueryOverlaps = ArrayPool<Collider>.Shared.Rent(64);
            _state.ResolveIdentity = resolveIdentity; _state.BoundaryReach = boundaryReach;
            _state.Phase = RoomPhase.Open; warning.SetVisible(false);
            int width = config.HandGridWidth;

            foreach (var cell in room.Cells)
            {
                var boundary = gameObject.AddComponent<BoxCollider>();
                boundary.isTrigger = true; boundary.enabled = false;
                boundary.center = transform.InverseTransformPoint(cell.center);
                boundary.size = cell.size + (room.Cells.Count == 1 ? new Vector3(boundaryReach * 2f, 0f, boundaryReach * 2f) : Vector3.zero);
                _state.Boundaries.Add(boundary);
                for (int i = 0; i < width * width; i++)
                {
                    Vector3 point = _presenter.GridPoint(cell, i, width, config.PortalInset);
                    point.y = SurfaceHeight(point, cell);
                    AddHand(point, darkMaterial, cell);
                    if (point.y > cell.min.y + 1.5f)
                        AddHand(new Vector3(point.x, cell.min.y + 0.015f, point.z), darkMaterial, cell);
                }
                BuildCracks(config.CrackMaterial != null ? config.CrackMaterial : darkMaterial, cell);
            }
            while (_state.Hands.Count < minimumHands)
                AddHand(_presenter.GridPoint(room.Cells[0], _state.Hands.Count % (width * width), width, config.PortalInset),
                    darkMaterial, room.Cells[0]);
        }

        private void AddHand(Vector3 point, Material darkMaterial, Bounds cell)
        {
            int index = _state.Hands.Count;
            var hand = _config.HandPrefab != null ? Instantiate(_config.HandPrefab, transform, false) : BuildHand(darkMaterial);
            hand.name = "Shadow Hand " + index; hand.transform.position = point;
            hand.transform.rotation = Quaternion.Euler(0f, index * 137.5f, 0f);
            foreach (var collider in hand.GetComponentsInChildren<Collider>()) collider.enabled = false;
            hand.SetActive(false); _state.Hands.Add(hand.transform); _state.HandPositions.Add(point);
            _state.HandBounds.Add(cell);
            _state.HandSkins.Add(hand.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        }
        public void SetHandLook(string look) => _state.HandLook = look;
        public void ApplyHandFact(CollapseHandFact fact)
        {
            _state.GripWeight = _presenter.GripWeight(fact.Kind);
            for (int i=0;i<_state.HandPositions.Count;i++)
            {
                foreach(var skin in _state.HandSkins[i])
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
            bool active = phase == RoomPhase.Tearing || phase == RoomPhase.Encroaching || phase == RoomPhase.Closed;
            foreach (var boundary in _state.Boundaries) boundary.enabled = active;
            if (!active) _state.Contacts.Clear();
            if (_state.Warning == null) return;
            _state.Warning.SetColor(warningColor);
            _state.Warning.SetVisible(phase != RoomPhase.Open && phase != RoomPhase.Closed);
        }
        public void PreviewCracks() => _state.OptionalCracks = true;
        public void SetWarningIntensity(float intensity) { if (_state.Warning != null) _state.Warning.SetBrightness(intensity); }
        public void ApplyDestruction(RoomDestructionSample sample, float elapsed, IReadOnlyList<Vector3> cakes = null)
        {
            ApplyPhase(sample.Phase, _config.WarningColor, _config.ClosedColor);
            _state.Phase = sample.Phase; _state.Progress = sample.Progress; _state.Elapsed = elapsed;
            float pulse = _presenter.Pulse(sample);
            SetWarningIntensity(_config.WarningIntensity * pulse);
            float mist = _presenter.MistProgress(sample.Phase, sample.Progress);
            float cracks = sample.Phase == RoomPhase.Open ? (_state.OptionalCracks ? 0.18f : 0f) :
                sample.Phase == RoomPhase.Telegraph ? Mathf.Lerp(0.08f, 1f, sample.Progress) : 1f;
            foreach (var crack in _state.Cracks)
            {
                crack.enabled = cracks > 0f;
                crack.widthMultiplier = Mathf.Lerp(0.012f, 0.11f, cracks) * (1f + pulse * _config.WarningCrackPulseGain);

            }
            for (int i = 0; i < _state.Hands.Count; i++)
            {
                var cell = _state.HandBounds[i];
                float reveal = _presenter.HandReveal(cell, _state.HandPositions[i], mist);
                bool reaching = cakes != null && i < cakes.Count && mist > 0f;
                if (reaching && _state.Room.Cells.Count > 1)
                    reaching = cakes[i].x >= cell.min.x && cakes[i].x <= cell.max.x && cakes[i].z >= cell.min.z && cakes[i].z <= cell.max.z;
                if (reaching) reveal = Mathf.Max(reveal, mist);
                var hand = _state.Hands[i]; hand.gameObject.SetActive(reveal > 0.01f);
                Vector3 origin = reaching ? _presenter.CakeReach(_state.HandPositions[i], cakes[i], sample.Phase, sample.Progress) : _state.HandPositions[i];
                var pose = FloorHandPresenter.Pose(origin, _state.PlayerTarget, elapsed, i,
                    _config.HandFlailAmplitude, _config.HandFlailRate, _config.HandVisualReachRange, _config.HandVisualReachDistance);
                // Root motion stays within its footprint cell, including at cake targets near notches.
                hand.position = cell.ClosestPoint(origin + pose.Offset);
                hand.localScale = _presenter.HandScale(reveal, elapsed, i,
                    FloorHandPresenter.VisualScale(_state.HandLook, _config.HandVisualScale, _config.GlovedHandScaleMultiplier));
                hand.rotation = Quaternion.FromToRotation(Vector3.up, pose.Direction) * Quaternion.Euler(0f, i * 137.5f, 0f);
                foreach (var skin in _state.HandSkins[i])
                {
                    if (skin.sharedMesh == null) continue;
                    int grip = skin.sharedMesh.GetBlendShapeIndex("Grasp");
                    if (grip >= 0) skin.SetBlendShapeWeight(grip, Mathf.Max(_state.GripWeight, pose.Grip));
                }
            }
            _state.PlayerTarget = null; // Observations expire unless the owner samples the player again.
        }

        public void ObservePlayer(Vector3 position, EntityId playerId)
        {
            // Cosmetic reach does not require a gameplay-trigger contact. Sample at the
            // visual range while retaining footprint/height rejection and wall occlusion.
            var probe = _presenter.BoundaryProbe(_state.Room, _state.Phase, position, _config.HandVisualReachRange);
            if (!probe.Available) return;
            if (probe.Distance > 0f)
            {
                Vector3 from = position + Vector3.up * _config.HandTargetHeight;
                Vector3 delta = probe.Position + Vector3.up * _config.HandTargetHeight - from;
                int count = RayHits(from, delta.normalized, delta.magnitude);
                for (int i = 0; i < count; i++)
                {
                    var hit = _state.QueryHits[i];
                    if (!hit.collider.transform.IsChildOf(transform) &&
                        (!playerId.IsValid || (_state.ResolveIdentity?.Invoke(hit.collider) ?? EntityId.None) != playerId)) return;
                }
            }
            Vector3 target = position + Vector3.up * _config.HandTargetHeight;
            if (!_state.PlayerTarget.HasValue || (target - _state.Bounds.center).sqrMagnitude <
                (_state.PlayerTarget.Value - _state.Bounds.center).sqrMagnitude) _state.PlayerTarget = target;
        }

        public FloorHandProbe Probe(Vector3 playerPosition, int preferredHand = -1, EntityId playerId = default)
        {
            if (playerId.IsValid && !_state.Contacts.ContainsValue(playerId)) return default;
            var probe = _presenter.BoundaryProbe(_state.Room, _state.Phase,
                playerPosition, _state.BoundaryReach, preferredHand);
            // Only exterior reaches cross a portal. Do not raycast at the visual
            // hands or let scenery inside a consumed room suppress its spring.
            if (probe.Available && probe.Distance > 0f)
            {
                Vector3 from = playerPosition + Vector3.up * 0.6f;
                Vector3 delta = probe.Position + Vector3.up * 0.6f - from;
                int count = RayHits(from, delta.normalized, delta.magnitude);
                for (int i = 0; i < count; i++)
                {
                    var hit = _state.QueryHits[i];
                    if (!hit.collider.transform.IsChildOf(transform) &&
                        (!playerId.IsValid || (_state.ResolveIdentity?.Invoke(hit.collider) ?? EntityId.None) != playerId)) return default;
                }
            }
            return probe;
        }
        public bool PickupOvertaken(Vector3 position) => _state.Room.ContainsXZ(position) && _state.Phase == RoomPhase.Closed;

        public void RefreshContacts()
        {
            _state.Contacts.Clear();
            if (!isActiveAndEnabled) return;
            foreach (var boundary in _state.Boundaries)
            {
                if (!boundary.enabled) continue;
                var bounds = boundary.bounds;
                if (_state.Room.Cells.Count > 1) bounds.Expand(new Vector3(_state.BoundaryReach * 2f, 0f, _state.BoundaryReach * 2f));
                int count;
                while ((count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, _state.QueryOverlaps,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) == _state.QueryOverlaps.Length)
                    GrowQuery(ref _state.QueryOverlaps);
                for (int i = 0; i < count; i++) Observe(_state.QueryOverlaps[i]);
            }
        }
        private void Observe(Collider other)
        {
            if (_state.Boundaries.Count == 0 || !_state.Boundaries[0].enabled || other == null || !other.enabled || !other.gameObject.activeInHierarchy) return;
            EntityId id = _state.ResolveIdentity?.Invoke(other) ?? EntityId.None;
            if (id.IsValid) _state.Contacts[other] = id;
        }
        private void OnTriggerEnter(Collider other) => Observe(other);
        private void OnTriggerStay(Collider other) => Observe(other);
        private void OnTriggerExit(Collider other) { if (!ReferenceEquals(other, null)) _state.Contacts.Remove(other); }
        private void OnDisable() => _state.Contacts.Clear();
        private float SurfaceHeight(Vector3 point, Bounds bounds)
        {
            float floor = bounds.min.y;
            int count = RayHits(new Vector3(point.x, bounds.max.y - 0.05f, point.z), Vector3.down, bounds.size.y);
            for (int i = 0; i < count; i++)
            {
                var hit = _state.QueryHits[i];
                if (hit.normal.y > 0.65f && hit.collider.attachedRigidbody == null && !hit.collider.transform.IsChildOf(transform))
                    floor = Mathf.Max(floor, hit.point.y);
            }
            return floor + 0.015f;
        }

        private int RayHits(Vector3 origin, Vector3 direction, float distance)
        {
            int count;
            while ((count = Physics.RaycastNonAlloc(origin, direction, _state.QueryHits, distance,
                ~0, QueryTriggerInteraction.Ignore)) == _state.QueryHits.Length)
                GrowQuery(ref _state.QueryHits);
            return count;
        }

        private void GrowQuery<T>(ref T[] buffer)
        {
            int previous = buffer.Length;
            var larger = ArrayPool<T>.Shared.Rent(checked(previous * 2));
            ArrayPool<T>.Shared.Return(buffer, true);
            buffer = larger;
            Debug.LogWarning($"Floor physics query buffer saturated; grew from {previous} to {buffer.Length} and retrying.", this);
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


        private void BuildCracks(Material material, Bounds bounds)
        {
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
        public void Teardown()
        {
            if (_state.QueryHits != null) ArrayPool<RaycastHit>.Shared.Return(_state.QueryHits, true);
            if (_state.QueryOverlaps != null) ArrayPool<Collider>.Shared.Return(_state.QueryOverlaps, true);
            _state.QueryHits = null; _state.QueryOverlaps = null;
            _state.Contacts.Clear(); _state.PlayerTarget = null;
        }
        private void OnDestroy() => Teardown();

    }
}
