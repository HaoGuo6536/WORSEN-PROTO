// ============================================================================
// WeaverWebDriver.cs
// ============================================================================
// PURPOSE:
//   Performs shared swept-sphere collision and firing-position probes for Weaver
//   and Blinder without either archetype depending on the other's implementation.
//   The floor motor remains authoritative for navigation while the physical
//   capsule and visual children move to the room ceiling, dropping for attacks.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by HunterDriver · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Recheck the full warned sweep at launch; never use a visibility ray as proof.
//   - Invert ceiling bodies about their capsule center and verify partition crossings.
//   - Return raw contacts synchronously for immediate Manager identity resolution.
//   - Reuse pooled physics buffers, growing and retrying saturated queries before consumption.
//   - Leave other Hunters' bodies out of shot, web and body-clearance probes.
// DEPENDENCIES:
//   - Core Weaver facts carry immutable nest commands from the owning Hunter.
//   - Own DriverConfig, Hunter motor config, pure presenters and Unity physics/navigation.
// USAGE NOTES:
//   Scene-owned; no Update, time source, global collision ignore or game-system reads.
//   Projectile colliders are continuous swept spheres, not trigger callbacks. The
//   warning/glow is published for external presentation. Children must own visuals
//   (the existing Hunter placeholder does); root capsule center is offset separately.
//   Area 3 is the PLAN-026 link contract. Crossing snaps between verified endpoints,
//   not through arbitrary walls; teardown restores all original local transforms.
//   Hunters ignore each other's bodies (owner, 2026-10-01): HunterBody and the route gate
//   leave the mask, so another Hunter never blocks a shot, a web or a firing spot.
// ============================================================================
using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
namespace Worsen.Domain.Hunter
{
    public sealed class WeaverWebDriver : MonoBehaviour
    {
        private WeaverDriverState _state;
        private WeaverDriverConfig _config;
        private HunterMotorDriverConfig _motor;
        private readonly WeaverPresenter _presenter = new WeaverPresenter();
        private readonly HunterSteeringPresenter _route = new HunterSteeringPresenter();
        private readonly HunterBodyPresenter _bodies = new HunterBodyPresenter();
        public bool IsReady => _state != null;
        public float ShotHeight => _config.ShotHeight;
        public float BodyOffset => _state?.Offset ?? 0f;
        private int Mask => _state.CollisionMask;
        public void Initialize(WeaverDriverConfig config, HunterMotorDriverConfig motor)
        {
            Teardown();
            _config = config != null ? config : Resources.Load<WeaverDriverConfig>("ScriptableObjects/Domain/Hunter/Archetypes/Weaver/WeaverDriverConfig");
            if (_config == null) throw new InvalidOperationException("Build Weaver profile/configs before spawning it.");
            _motor = motor; _state = new WeaverDriverState { Capsule = GetComponent<CapsuleCollider>() };
            _state.CollisionMask = _bodies.WithoutLayers(_motor.CollisionMask,
                LayerMask.NameToLayer("HunterRouteGate"), LayerMask.NameToLayer("HunterBody"));
            _state.QueryHits = ArrayPool<RaycastHit>.Shared.Rent(64);
            _state.QueryOverlaps = ArrayPool<Collider>.Shared.Rent(64);
            _state.CapsuleCenter = _state.Capsule.center;
            foreach (Transform child in transform)
            { _state.Children.Add(child); _state.ChildPositions.Add(child.localPosition); _state.ChildRotations.Add(child.localRotation); }
        }
        private bool Own(Collider collider) => collider.transform.IsChildOf(transform);
        public bool ClearSweep(Vector3 origin, Vector3 target, float radius, Func<Collider, bool> isTarget)
        {
            if (_state == null || radius <= 0f || radius > .1f || (target - origin).sqrMagnitude <= 0f) return false;
            // SphereCast does not report starting overlaps: prove the muzzle too.
            int overlaps = SphereOverlap(origin, radius);
            for (int i = 0; i < overlaps; i++)
            {
                Collider overlap = _state.QueryOverlaps[i];
                if (!Own(overlap) && (isTarget == null || !isTarget(overlap))) return false;
            }
            Vector3 delta = target - origin;
            int hits = SphereQuery(origin, radius, delta.normalized, delta.magnitude);
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = _state.QueryHits[i];
                if (!Own(hit.collider) && (isTarget == null || !isTarget(hit.collider))) return false;
            }
            return true;
        }
        public WeaverObservation Probe(Vector3 target, float radius, float range, long tick, Func<Collider, bool> isTarget)
        {
            Vector3 floor = transform.position, origin = floor + Vector3.up * _config.ShotHeight;
            bool clear = BodyClear(floor) && Vector3.Distance(origin, target) <= range && ClearSweep(origin, target, radius, isTarget);
            var spots = new List<WeaverShotSpot>();
            var path = new NavMeshPath();
            if (!clear && NavMesh.SamplePosition(floor, out NavMeshHit start, _config.SampleRadius, _motor.NavigationAreaMask))
                for (int i = 0; i < _config.CandidateCount; i++)
                {
                    Vector3 candidate = _presenter.Candidate(floor, i, _config.CandidateCount, _config.CandidateDistance);
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit end, _config.SampleRadius, _motor.NavigationAreaMask) ||
                        Mathf.Abs(end.position.y - floor.y) > _motor.StepHeight || !BodyClear(end.position)) continue;
                    bool reachable = NavMesh.CalculatePath(start.position, end.position, _motor.NavigationAreaMask, path) && path.status == NavMeshPathStatus.PathComplete;
                    Vector3 muzzle = end.position + Vector3.up * _config.ShotHeight;
                    spots.Add(new WeaverShotSpot(end.position, reachable, Vector3.Distance(muzzle, target) <= range && ClearSweep(muzzle, target, radius, isTarget)));
                }
            return new WeaverObservation(tick, origin, target, radius, clear, _state.Offset <= _config.ArrivalTolerance, spots);
        }
        public void SetCeiling(float ceilingHeight, bool ceiling)
        {
            if (_state == null) return;
            float bodyTop = (_state.CapsuleCenter.y + _state.Capsule.height * .5f) * Mathf.Abs(transform.lossyScale.y);
            float offset = _presenter.CeilingOffset(transform.position.y, ceilingHeight, bodyTop, _config.CeilingClearance, !ceiling);
            _state.Offset = offset;
            ApplyBodyPose();
            Physics.SyncTransforms();
        }
        public void ApplyBodyPose()
        {
            if (_state == null) return;
            bool hanging = _state.Offset > 0f;
            Vector3 localOffset = transform.InverseTransformVector(Vector3.up * _state.Offset);
            Quaternion rotation = _presenter.CeilingRotation(hanging);
            _state.Capsule.center = _state.CapsuleCenter + localOffset;
            for (int i = 0; i < _state.Children.Count; i++)
                if (_state.Children[i] != null)
                {
                    // Restore the authored values exactly on drop, without pivot
                    // subtraction/addition rounding accumulating across reuse.
                    _state.Children[i].localPosition = hanging ?
                        _presenter.CeilingPosition(_state.ChildPositions[i], _state.CapsuleCenter, localOffset, rotation) : _state.ChildPositions[i];
                    _state.Children[i].localRotation = hanging ? rotation * _state.ChildRotations[i] : _state.ChildRotations[i];
                }
        }
        public bool Launch(Vector3 origin, Vector3 target, float radius, float speed, float range, int serial, Func<Collider, bool> isTarget)
        {
            if (_state == null || _state.Offset > _config.ArrivalTolerance || !ClearSweep(origin, target, radius, isTarget)) return false;
            _state.Webs.Add(new WeaverWebDriverState { Serial = serial, Position = origin,
                Direction = (target - origin).normalized, Radius = radius, Speed = speed, Remaining = range });
            return true;
        }
        public void AddNest(WeaverFact fact)
        {
            if (_state == null || fact.Kind != WeaverFactKind.DoorwayWebbed) return;
            _state.Webs.Add(new WeaverWebDriverState { Serial = fact.Serial, Position = fact.Position + Vector3.up * _config.ShotHeight,
                Radius = fact.Radius, Remaining = fact.Duration, Nest = true });
        }
        public IReadOnlyList<KeyValuePair<Collider, int>> TickWebs(float dt, Func<Collider, bool> isTarget)
        {
            var contacts = new List<KeyValuePair<Collider, int>>();
            if (_state == null || !(dt > 0f) || float.IsInfinity(dt)) return contacts;
            foreach (WeaverWebDriverState web in _state.Webs)
            {
                Collider hit = null;
                int count = SphereOverlap(web.Position, web.Radius);
                for (int i = 0; i < count; i++)
                {
                    Collider overlap = _state.QueryOverlaps[i];
                    if (!Own(overlap) && (!web.Nest || (isTarget?.Invoke(overlap) ?? false)))
                    { hit = overlap; if (!(isTarget?.Invoke(hit) ?? false)) break; }
                }
                float distance = web.Nest ? 0f : Mathf.Min(web.Remaining, web.Speed * dt);
                if (hit == null && !web.Nest)
                {
                    count = SphereQuery(web.Position, web.Radius, web.Direction, distance);
                    SortHits(count);
                    for (int i = 0; i < count; i++)
                    {
                        RaycastHit candidate = _state.QueryHits[i];
                        if (!Own(candidate.collider)) { hit = candidate.collider; break; }
                    }
                }
                if (hit != null)
                {
                    if (isTarget?.Invoke(hit) ?? false) contacts.Add(new KeyValuePair<Collider, int>(hit, web.Serial));
                    web.Remaining = 0f;
                }
                else { web.Position += web.Direction * distance; web.Remaining -= web.Nest ? dt : distance; }
            }
            _state.Webs.RemoveAll(web => web.Remaining <= 0f);
            return contacts;
        }
        public bool TryCrossPartition(Vector3[] corners, int index, out Vector3 end)
        {
            end = default;
            if (_state == null || (_motor.NavigationAreaMask & (1 << 3)) == 0 || corners == null) return false;
            for (int i = Mathf.Max(0, index - 1); i + 1 < corners.Length && i <= index; i++)
            {
                Vector3 a = corners[i], b = corners[i + 1];
                if (Vector3.Distance(transform.position, a) > _motor.CornerTolerance || Mathf.Abs(a.y - b.y) > _motor.GroundProbeDistance ||
                    !NavMesh.Raycast(a, b, out _, 1) || !BodyClear(b)) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(a, b, _motor.NavigationAreaMask, path) || path.status != NavMeshPathStatus.PathComplete ||
                    !_route.IsDirectSegmentPath(path.corners, a, b, _motor.GroundProbeDistance)) continue;
                // Removing ONLY area 3 must remove this direct segment. Other off-mesh
                // links, ordinary corners and an arbitrary blocked ray do not qualify.
                bool ordinary = NavMesh.CalculatePath(a, b, _motor.NavigationAreaMask & ~(1 << 3), path) &&
                    path.status == NavMeshPathStatus.PathComplete && _route.IsDirectSegmentPath(path.corners, a, b, _motor.GroundProbeDistance);
                if (ordinary) continue;
                end = b; return true;
            }
            return false;
        }
        private bool BodyClear(Vector3 floor)
        {
            Vector3 low = floor + Vector3.up * (_motor.Radius + _motor.SkinWidth);
            Vector3 high = floor + Vector3.up * (_motor.Height - _motor.Radius + _motor.SkinWidth);
            int count = CapsuleOverlap(low, high, Mathf.Max(.001f, _motor.Radius - _motor.SkinWidth));
            for (int i = 0; i < count; i++)
            {
                Collider other = _state.QueryOverlaps[i];
                if (!Own(other)) return false;
            }
            return true;
        }
        private int SphereOverlap(Vector3 origin, float radius)
        {
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(origin, radius, _state.QueryOverlaps, Mask, QueryTriggerInteraction.Ignore)) == _state.QueryOverlaps.Length)
                GrowQuery(ref _state.QueryOverlaps);
            return count;
        }
        private int CapsuleOverlap(Vector3 low, Vector3 high, float radius)
        {
            int count;
            while ((count = Physics.OverlapCapsuleNonAlloc(low, high, radius, _state.QueryOverlaps, Mask, QueryTriggerInteraction.Ignore)) == _state.QueryOverlaps.Length)
                GrowQuery(ref _state.QueryOverlaps);
            return count;
        }
        private int SphereQuery(Vector3 origin, float radius, Vector3 direction, float distance)
        {
            int count;
            while ((count = Physics.SphereCastNonAlloc(origin, radius, direction, _state.QueryHits, distance, Mask, QueryTriggerInteraction.Ignore)) == _state.QueryHits.Length)
                GrowQuery(ref _state.QueryHits);
            return count;
        }
        private void GrowQuery<T>(ref T[] buffer)
        {
            int previous = buffer.Length;
            T[] larger = ArrayPool<T>.Shared.Rent(checked(previous * 2));
            ArrayPool<T>.Shared.Return(buffer, true); buffer = larger;
            Debug.LogWarning($"Weaver physics query buffer saturated; grew from {previous} to {buffer.Length} and retrying.", this);
        }
        private void SortHits(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit hit = _state.QueryHits[i]; int j = i - 1;
                while (j >= 0 && (hit.distance < _state.QueryHits[j].distance ||
                    (hit.distance == _state.QueryHits[j].distance && hit.collider.GetInstanceID() < _state.QueryHits[j].collider.GetInstanceID())))
                { _state.QueryHits[j + 1] = _state.QueryHits[j]; j--; }
                _state.QueryHits[j + 1] = hit;
            }
        }
        private void OnDestroy() { Teardown(); }
        public void Teardown()
        {
            if (_state == null) return;
            if (_state.Capsule != null) _state.Capsule.center = _state.CapsuleCenter;
            for (int i = 0; i < _state.Children.Count; i++)
                if (_state.Children[i] != null)
                { _state.Children[i].localPosition = _state.ChildPositions[i]; _state.Children[i].localRotation = _state.ChildRotations[i]; }
            ArrayPool<RaycastHit>.Shared.Return(_state.QueryHits, true);
            ArrayPool<Collider>.Shared.Return(_state.QueryOverlaps, true);
            _state.QueryHits = null; _state.QueryOverlaps = null;
            _state = null;
        }
    }
}
