// ============================================================================
// PlayerDriver.cs
// ============================================================================
// PURPOSE:
//   Gathers capsule contacts and applies resolved movement for its owning PlayerManager.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Probe support, endpoints, ledges and clearance; capture per-vault obstacle phases.
//   - Resolve swept movement and interpolation, excluding only the admitted obstacle during its arc.
//   - Filter hunter bodies from all queries and capsule contacts during collision grace.
//   - Restore an explicit pose and capsule posture without interpolation on revival.
//   - Resolve configuration and own first-person limb lifecycle.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Scene-owned. Owns capsule/kinematic body and visual interpolation; no global side effects. Configuration has a mirrored Resources fallback.
//   Unity 6 per-collider exclusions avoid a global IgnoreLayerCollision change and restore on end/disable/teardown.
//   Both current actor bodies are kinematic; their transforms are moved explicitly, not by contact impulses.
//   Traversal filtering is query-local, not Physics.IgnoreCollision: no pair/layer state leaks on cancellation.
//   Genuine arc obstructions latch until the existing completion deadline; no delayed catch-up is attempted.
//   No other Domain system or Presentation system is referenced.
//   Query buffers are pooled until teardown; saturation grows and retries before
//   consuming any contacts. Equal-distance hits retain the query's encounter order.
// ============================================================================
using System;
using System.Buffers;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Player
{
    [RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
    public sealed class PlayerDriver : MonoBehaviour
    {
        [SerializeField] private PlayerMoverDriverConfig _config;
        [SerializeField] private CapsuleCollider _capsule;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private PlayerLimbStandIn _limbs;
        private readonly PlayerDriverState _state = new PlayerDriverState();
        private readonly PlayerMoverPresenter _presenter = new PlayerMoverPresenter();
        private static readonly PlayerDriverState SessionWarnings = new PlayerDriverState();
        public float FixedDeltaTime => Time.fixedDeltaTime;
        private int MovementMask => _presenter.MovementMask(_config.CollisionMask, _state.HunterBodyLayer, _state.GraceActive);
        public Vector3 Position => _state.Position;
        public float Heading => _state.Heading;
        public Vector3 EyePosition => _presenter.EyePosition(_state, _config.EyeHeight, _config.Height);

        public PlayerEffectConfig ResolveEffectConfig(PlayerEffectConfig configured)
            => configured != null ? configured : Resources.Load<PlayerEffectConfig>("ScriptableObjects/Domain/Player/PlayerEffectConfig");

        public void Initialize()
        {
            ClearTraversal();
            SetGraceActive(false);
            if (_config == null) _config = Resources.Load<PlayerMoverDriverConfig>("ScriptableObjects/Domain/Player/PlayerMoverDriverConfig");
            if (_config == null) throw new InvalidOperationException("Build Player assets before spawning a Player.");
            if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            _state.HunterBodyLayer = LayerMask.NameToLayer(_config.HunterBodyLayer);
            if (_state.QueryHits == null) _state.QueryHits = ArrayPool<RaycastHit>.Shared.Rent(64);
            if (_state.QueryOverlaps == null) _state.QueryOverlaps = ArrayPool<Collider>.Shared.Rent(64);
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.None;
            _state.Position = _state.PreviousPosition = transform.position;
            _state.Heading = _state.PreviousHeading = transform.eulerAngles.y;
            _state.Velocity = Vector3.zero;
            _state.LastStepTime = Time.time;
            _state.LastStepDuration = 0f;
            _state.Grounded = false;
            _state.Ready = true;
            SetCapsule(false);
            _capsule.enabled = true;
            ShowMovement(MovementState.Ground);
        }

        public void Teleport(Vector3 position, float heading, bool crouched = false)
        {
            ClearTraversal();
            _state.Position = _state.PreviousPosition = position;
            _state.Heading = _state.PreviousHeading = heading;
            _state.Velocity = Vector3.zero;
            _state.LastStepDuration = 0f;
            _state.Grounded = false;
            SetCapsule(crouched);
            _body.position = position;
            _body.rotation = Quaternion.Euler(0f, heading, 0f);
            transform.SetPositionAndRotation(position, _body.rotation);
        }

        public MovementProbe Probe(float ledgeReach = 0f, float ledgeMinimumHeight = 0f,
            float ledgeMaximumHeight = 0f, float ledgeChestHeight = 0f)
        {
            if (!_state.Ready) return default;
            Vector3 feet = _state.Position;
            Vector3 forward = Quaternion.Euler(0f, _state.Heading, 0f) * Vector3.forward;
            bool grounded = FindGround(feet, _config.GroundProbeDistance, _state.Velocity, out RaycastHit ground);
            bool wallHit = Cast(feet, _state.Height, forward, _config.WallProbeDistance + _config.SkinWidth, out RaycastHit wall);
            ITraversalSurface wallSurface = wallHit ? wall.collider.GetComponentInParent<ITraversalSurface>() : null;
            bool rebound = wallSurface != null && wallSurface.Kind == TraversalSurfaceKind.Rebound;
            bool vaultHit = Cast(feet, Mathf.Min(_state.Height, _config.Height * 0.5f), forward,
                _config.VaultProbeDistance, out RaycastHit vault);
            ITraversalSurface vaultSurface = vaultHit ? vault.collider.GetComponentInParent<ITraversalSurface>() : null;
            bool candidate = vaultSurface != null && vaultSurface.Kind == TraversalSurfaceKind.Vault;
            Vector3 target = candidate ? vaultSurface.Target : Vector3.zero;
            bool targetAvailable = candidate;
            if (candidate && vaultSurface is ITraversalEndpointPair pair && pair.HasEndpointPair)
                targetAvailable = _presenter.TrySelectTraversalEndpoint(feet, forward,
                    pair.EndpointA, pair.EndpointB, out target);
            _state.ProbedTraversalCollider = targetAvailable ? vault.collider : null;
            _state.ProbedTraversalTarget = target;
            float clearance = targetAvailable && !IsBlocked(target, _config.Height) ? _config.Height : 0f;
            float height = candidate ? vault.collider.bounds.max.y - feet.y : 0f;
            // Preserve authored route semantics. Only untagged geometry offers an automatic
            // ledge: positive checked clearance with VaultCandidate=false is replayable data.
            if (!candidate && !grounded && ledgeReach > 0f && TryLedge(feet, forward, ledgeReach,
                ledgeMinimumHeight, ledgeMaximumHeight, ledgeChestHeight, out Vector3 ledge))
            { target = ledge; height = ledge.y - feet.y; clearance = _config.Height; _state.ProbedTraversalTarget = target; }
            return new MovementProbe(grounded, grounded ? ground.normal : Vector3.up,
                rebound, rebound ? Mathf.Max(0f, wall.distance - _config.SkinWidth) : 0f,
                rebound ? wall.normal : Vector3.zero, rebound ? Vector3.Angle(forward, -wall.normal) : 0f,
                rebound ? wallSurface.SurfaceId : 0, candidate,
                height, clearance, target,
                _state.Height < _config.Height && IsBlocked(feet, _config.Height), candidate ? vaultSurface.SurfaceId : 0);
        }

        public PlayerMoveResult Move(Vector3 displacement, Vector3 velocity, bool crouched, float heading, float dt,
            bool sliding = false, float slideWallRetention = 0f)
        {
            ClearTraversal();
            return MoveResolved(displacement, velocity, crouched, heading, dt, sliding, slideWallRetention);
        }

        private PlayerMoveResult MoveResolved(Vector3 displacement, Vector3 velocity, bool crouched, float heading, float dt,
            bool sliding = false, float slideWallRetention = 0f)
        {
            if (!_state.Ready) throw new InvalidOperationException("PlayerDriver.Initialize must precede Move.");
            SetCapsule(crouched);
            _state.PreviousPosition = _state.Position;
            _state.PreviousHeading = _state.Heading;
            _state.Heading = heading;
            Vector3 position = _state.Position;
            ResolvePenetrations(ref position);
            Vector3 remaining = displacement;
            bool grounded = false;
            bool ceiling = false;
            for (int i = 0; i < Mathf.Max(1, _config.CastIterations) && remaining.sqrMagnitude > 0.0000001f; i++)
            {
                if (!Cast(position, _state.Height, remaining.normalized, remaining.magnitude + _config.SkinWidth, out RaycastHit hit))
                { position += remaining; break; }
                if (_presenter.IsInitialOverlap(hit.distance, hit.point))
                {
                    // PhysX returns -castDirection for initial overlaps, not a surface normal.
                    // Depenetrate and retry; never project velocity onto that synthetic plane.
                    velocity = _presenter.ContactVelocity(velocity, hit.normal, true);
                    if (!Depenetrate(ref position, hit.collider)) break;
                    continue;
                }
                Vector3 travel = _presenter.TravelBeforeHit(remaining, hit.distance, _config.SkinWidth);
                position += travel;
                remaining -= travel;
                bool floor = _presenter.IsWalkable(hit.normal, _config.SlopeLimitDegrees);
                if (!floor && _state.Grounded && velocity.y <= 0f && TryStep(position, remaining, out Vector3 stepped))
                { position = stepped; grounded = true; break; }
                grounded |= _presenter.CanGround(velocity, hit.normal, _config.SlopeLimitDegrees);
                ceiling |= hit.normal.y < -0.5f;
                bool slideWall = sliding && !floor && Mathf.Abs(hit.normal.y) < 0.5f;
                remaining = slideWall ? _presenter.RedirectSlide(remaining, hit.normal, slideWallRetention)
                    : _presenter.ProjectAfterHit(remaining, hit.normal);
                velocity = slideWall ? _presenter.RedirectSlide(velocity, hit.normal, slideWallRetention)
                    : _presenter.ContactVelocity(velocity, hit.normal, false);
            }
            if (FindGround(position,
                _state.Grounded ? _config.GroundSnapDistance : _config.GroundProbeDistance, displacement, out RaycastHit ground)
                && _presenter.CanSnapToGround(velocity, ground.normal, _config.SlopeLimitDegrees))
            {
                if (_state.Grounded || grounded)
                    position = _presenter.GroundSnap(position, ground.distance, _config.SkinWidth, _config.GroundSnapDistance);
                grounded = true;
                velocity = _presenter.ProjectAfterHit(velocity, ground.normal);
            }
            _state.Position = position;
            _state.Velocity = velocity;
            _state.Grounded = grounded;
            _state.LastStepTime = Time.time;
            _state.LastStepDuration = dt;
            _body.position = position;
            _body.rotation = Quaternion.Euler(0f, heading, 0f);
            transform.SetPositionAndRotation(position, _body.rotation);
            return new PlayerMoveResult(position, velocity, grounded, ceiling);
        }

        public PlayerMoveResult MoveTraversal(Vector3 from, Vector3 to, float progress, float obstacleHeight,
            Vector3 velocity, float heading, float dt, float maximumSpeed, Vector3 steeringOffset = default)
        {
            if (!(dt > 0f) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!_state.TraversalActive)
            {
                _state.TraversalActive = true;
                _state.TraversalObstructed = false;
                _state.TraversalCollider = to == _state.ProbedTraversalTarget ? _state.ProbedTraversalCollider : null;
                _presenter.TraversalPhases(from, to,
                    _state.TraversalCollider != null ? _state.TraversalCollider.bounds : (Bounds?)null,
                    _config.Radius, _config.SkinWidth, _config.TraversalRisePortion, _config.TraversalTraverseEnd,
                    out _state.TraversalRisePortion, out _state.TraversalTraverseEnd);
            }
            Vector3 before = _state.Position;
            Vector3 target = _presenter.TraversalPosition(from, to, progress, obstacleHeight, _config.TraversalLift,
                _state.TraversalRisePortion, _state.TraversalTraverseEnd) + steeringOffset;
            // Admission checks distance/duration against the speed budget. Clamping an
            // absolute target here accumulates debt, then releases it as a catch-up jump.
            // Landing admission and the actual landing sweep still reject other geometry.
            if (_state.TraversalObstructed) target = before;
            _state.IgnoredTraversalCollider = _state.TraversalCollider;
            try
            {
                Vector3 displacement = target - before;
                PlayerMoveResult resolved = MoveResolved(displacement, displacement / dt, false, heading, dt);
                // Never resume a blocked arc if the obstruction subsequently moves away.
                // The Controller retains its deadline and resolves failure from the endpoint.
                _state.TraversalObstructed |= (resolved.Position - target).sqrMagnitude > _config.SkinWidth * _config.SkinWidth;
                return new PlayerMoveResult(resolved.Position, (resolved.Position - before) / dt, resolved.Grounded, resolved.Ceiling);
            }
            finally
            {
                _state.IgnoredTraversalCollider = null;
                if (progress >= 1f) ClearTraversal();
            }
        }

        public void ShowMovement(MovementState movement)
        {
            if (movement != MovementState.Vault) ClearTraversal();
            // Relaxed arms swing with horizontal speed and settle when crouched (PlayerLimbPresenter).
            if (_limbs != null) _limbs.Apply(movement, _config.EyeHeight, _config.HandOffset, _config.FootOffset,
                new Vector2(_state.Velocity.x, _state.Velocity.z).magnitude, _state.Height < _config.Height, _state.LastStepDuration, _config);
        }

        public void Teardown()
        {
            ClearTraversal();
            SetGraceActive(false);
            _state.Ready = false;
            _state.Velocity = Vector3.zero;
            if (_capsule != null) _capsule.enabled = false;
            if (_limbs != null) _limbs.Apply(MovementState.Ground, 0f, Vector3.zero, Vector3.zero);
            ReleaseQueries();
        }

        private void OnDestroy() { ReleaseQueries(); }

        private void ReleaseQueries()
        {
            if (_state.QueryHits != null) ArrayPool<RaycastHit>.Shared.Return(_state.QueryHits, true);
            if (_state.QueryOverlaps != null) ArrayPool<Collider>.Shared.Return(_state.QueryOverlaps, true);
            _state.QueryHits = null;
            _state.QueryOverlaps = null;
        }

        private void GrowQuery<T>(ref T[] buffer)
        {
            int previous = buffer.Length;
            var larger = ArrayPool<T>.Shared.Rent(checked(previous * 2));
            ArrayPool<T>.Shared.Return(buffer, true);
            buffer = larger;
            Debug.LogWarning($"Player physics query buffer saturated; grew from {previous} to {buffer.Length} and retrying.", this);
        }

        public void SetGraceActive(bool active)
        {
            if (active && !_state.Ready) return;
            if (active && _presenter.ShouldWarnMissingHunterLayer(SessionWarnings, _state.HunterBodyLayer))
                Debug.LogWarning($"Player hunter-body layer '{_config.HunterBodyLayer}' is missing; hit grace still blocks damage, but hunter pass-through is disabled for this session.", this);
            active &= _state.HunterBodyLayer >= 0;
            if (_state.GraceActive == active) return;
            if (active)
            {
                _state.OriginalExcludeLayers = _capsule.excludeLayers;
                _capsule.excludeLayers = _state.OriginalExcludeLayers | (1 << _state.HunterBodyLayer);
            }
            else if (_capsule != null) _capsule.excludeLayers = _state.OriginalExcludeLayers;
            _state.GraceActive = active;
        }

        private void OnDisable() { ClearTraversal(); SetGraceActive(false); }

        private void ClearTraversal()
        {
            _state.TraversalActive = _state.TraversalObstructed = false;
            _state.TraversalCollider = _state.IgnoredTraversalCollider = _state.ProbedTraversalCollider = null;
            _state.TraversalRisePortion = _state.TraversalTraverseEnd = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionWarnings() { SessionWarnings.MissingHunterLayerWarned = false; }

        private void LateUpdate()
        {
            if (!_state.Ready || _visualRoot == null) return;
            _visualRoot.SetPositionAndRotation(_config.InterpolateVisuals ? _presenter.InterpolatePosition(_state, Time.time) : _state.Position,
                Quaternion.Euler(0f, _config.InterpolateVisuals ? _presenter.InterpolateHeading(_state, Time.time) : _state.Heading, 0f));
        }

        private void SetCapsule(bool crouched)
        {
            _state.Height = Mathf.Max(_config.Radius * 2f, _config.Height * (crouched ? _config.SlideHeightRatio : 1f));
            _capsule.radius = _config.Radius;
            _capsule.height = _state.Height;
            _capsule.center = Vector3.up * (_state.Height * 0.5f);
            _capsule.direction = 1;
        }

        private bool Cast(Vector3 feet, float height, Vector3 direction, float distance, out RaycastHit closest)
        {
            _presenter.Capsule(feet, height, _config.Radius, out Vector3 bottom, out Vector3 top);
            int count;
            while ((count = Physics.CapsuleCastNonAlloc(bottom, top, Mathf.Max(0.001f, _config.Radius - _config.SkinWidth),
                direction, _state.QueryHits, Mathf.Max(0f, distance), MovementMask, QueryTriggerInteraction.Ignore)) == _state.QueryHits.Length)
                GrowQuery(ref _state.QueryHits);
            closest = default;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _state.QueryHits[i];
                if (hit.collider == null || hit.collider == _state.IgnoredTraversalCollider
                    || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                closest = hit;
                nearest = hit.distance;
            }
            return nearest < float.PositiveInfinity;
        }

        private bool IsBlocked(Vector3 feet, float height)
        {
            _presenter.Capsule(feet, height, _config.Radius, out Vector3 bottom, out Vector3 top);
            int count = OverlapCapsule(bottom, top);
            for (int i = 0; i < count; i++)
                if (_state.QueryOverlaps[i] != _state.IgnoredTraversalCollider
                    && !_state.QueryOverlaps[i].transform.IsChildOf(transform)) return true;
            return false;
        }

        private int OverlapCapsule(Vector3 bottom, Vector3 top)
        {
            int count;
            while ((count = Physics.OverlapCapsuleNonAlloc(bottom, top, Mathf.Max(0.001f, _config.Radius - _config.SkinWidth),
                _state.QueryOverlaps, MovementMask, QueryTriggerInteraction.Ignore)) == _state.QueryOverlaps.Length)
                GrowQuery(ref _state.QueryOverlaps);
            return count;
        }

        private bool TryLedge(Vector3 feet, Vector3 forward, float reach, float minimumHeight,
            float maximumHeight, float chestHeight, out Vector3 target)
        {
            target = Vector3.zero;
            if (!Ray(feet + Vector3.up * chestHeight, forward, reach, out RaycastHit chest)
                || chest.collider.GetComponentInParent<ITraversalSurface>() != null) return false;
            Vector3 upper = feet + Vector3.up * (maximumHeight + _config.SkinWidth);
            bool aboveBlocked = Ray(upper, forward, chest.distance + _config.Radius + _config.SkinWidth, out _);
            Vector3 topOrigin = chest.point + forward * (_config.Radius + _config.SkinWidth);
            topOrigin.y = upper.y;
            bool topFound = Ray(topOrigin, Vector3.down, maximumHeight - minimumHeight + _config.SkinWidth, out RaycastHit top);
            if (!_presenter.CanClimbLedge(true, aboveBlocked, topFound, topFound && IsBlocked(top.point, _config.Height),
                feet, top.point, top.normal, reach, minimumHeight, maximumHeight, _config.SlopeLimitDegrees)) return false;
            target = top.point;
            _state.ProbedTraversalCollider = chest.collider;
            return true;
        }

        private bool Ray(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
        {
            closest = default;
            float nearest = float.PositiveInfinity;
            int count;
            while ((count = Physics.RaycastNonAlloc(origin, direction, _state.QueryHits, Mathf.Max(0f, distance),
                MovementMask, QueryTriggerInteraction.Ignore)) == _state.QueryHits.Length)
                GrowQuery(ref _state.QueryHits);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _state.QueryHits[i];
                if (hit.collider == null || hit.collider == _state.IgnoredTraversalCollider
                    || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                closest = hit; nearest = hit.distance;
            }
            return nearest < float.PositiveInfinity;
        }

        private bool Depenetrate(ref Vector3 position, Collider obstacle)
        {
            if (obstacle == _state.IgnoredTraversalCollider) return false;
            if (!Physics.ComputePenetration(_capsule, position, Quaternion.identity, obstacle,
                obstacle.transform.position, obstacle.transform.rotation, out Vector3 direction, out float depth)) return false;
            position += _presenter.PenetrationOffset(direction, depth, _config.SkinWidth);
            return true;
        }

        private void ResolvePenetrations(ref Vector3 position)
        {
            for (int pass = 0; pass < Mathf.Max(1, _config.CastIterations); pass++)
            {
                _presenter.Capsule(position, _state.Height, _config.Radius, out Vector3 bottom, out Vector3 top);
                int count = OverlapCapsule(bottom, top);
                bool moved = false;
                for (int i = 0; i < count; i++)
                {
                    Collider obstacle = _state.QueryOverlaps[i];
                    if (!obstacle.transform.IsChildOf(transform)) moved |= Depenetrate(ref position, obstacle);
                }
                if (!moved) break;
            }
        }

        private bool TryStep(Vector3 position, Vector3 displacement, out Vector3 stepped)
        {
            stepped = position;
            Vector3 horizontal = new Vector3(displacement.x, 0f, displacement.z);
            if (horizontal.sqrMagnitude < 0.000001f || _config.StepHeight <= 0f) return false;
            float actualRaise = _config.StepHeight;
            if (Cast(position, _state.Height, Vector3.up, _config.StepHeight, out RaycastHit overhead))
                actualRaise = _presenter.TravelBeforeHit(Vector3.up * _config.StepHeight, overhead.distance, _config.SkinWidth).y;
            if (!(actualRaise > 0f && actualRaise <= _config.StepHeight)) return false;
            Vector3 raised = position + Vector3.up * actualRaise;
            if (IsBlocked(raised, _state.Height)
                || Cast(raised, _state.Height, horizontal.normalized, horizontal.magnitude + _config.SkinWidth, out _)) return false;
            raised += horizontal;
            if (!FindGround(raised, actualRaise + _config.GroundProbeDistance, horizontal, out RaycastHit hit)
                || hit.point.y - position.y > actualRaise + 0.001f) return false;
            stepped = _presenter.GroundSnap(raised, hit.distance, _config.SkinWidth, actualRaise + _config.GroundProbeDistance);
            return _presenter.IsValidStep(position, stepped, actualRaise) && !IsBlocked(stepped, _state.Height);
        }

        private bool FindGround(Vector3 feet, float distance, Vector3 travel, out RaycastHit closest)
        {
            bool capsuleHit = Cast(feet, _state.Height, Vector3.down, distance, out closest);
            if (capsuleHit && _presenter.IsInitialOverlap(closest.distance, closest.point))
            {
                if (!Physics.ComputePenetration(_capsule, feet, Quaternion.identity, closest.collider,
                    closest.collider.transform.position, closest.collider.transform.rotation, out Vector3 normal, out _)) return false;
                closest.normal = normal;
            }
            bool found = capsuleHit && _presenter.IsWalkable(closest.normal, _config.SlopeLimitDegrees);
            // A steep contact still blocks the full capsule. Peripheral support
            // may shorten this descent, never snap through it to a lower floor.
            float nearest = capsuleHit ? closest.distance : float.PositiveInfinity;
            if (found && nearest <= _config.SkinWidth + 0.001f) return true;

            // A short capsule-down cast can hit a rounded edge rather than its
            // walkable top. These rays inspect actual support inside the shrunken
            // footprint; only the vertical landing changes, never the travel budget.
            float radius = Mathf.Max(0.001f, _config.Radius - _config.SkinWidth);
            Vector3 forward = new Vector3(travel.x, 0f, travel.z).normalized;
            for (int sample = -1; sample < 8; sample++)
            {
                Vector3 offset = sample < 0 ? forward : Quaternion.Euler(0f, sample * 45f, 0f) * Vector3.forward;
                Vector3 origin = feet + Vector3.up * _config.SkinWidth + offset * radius;
                bool rayHit = Ray(origin, Vector3.down, distance, out RaycastHit first);
                float firstDistance = rayHit ? first.distance : float.PositiveInfinity;
                // A non-walkable first hit occludes any lower surface on this ray.
                if (firstDistance >= nearest || !_presenter.IsWalkable(first.normal, _config.SlopeLimitDegrees)) continue;
                closest = first; nearest = firstDistance; found = true;
            }
            return found;
        }
    }
}
