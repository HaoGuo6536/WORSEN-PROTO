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
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
//   - Resolve walkable step support within the capsule footprint without adding horizontal travel.
//   - Bound each step raise by actual overhead clearance before checking forward travel and support.
//   - Resolve optional authored traversal endpoint pairs through pure geometry before clearance casts.
//   - Hide legacy limb objects immediately at initialization and after all movement commands.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Scene-owned. Owns capsule/kinematic body and visual interpolation; no global side effects. Configuration has a mirrored Resources fallback.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using System;
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
        public Vector3 Position => _state.Position;
        public float Heading => _state.Heading;
        public Vector3 EyePosition => _presenter.EyePosition(_state, _config.EyeHeight, _config.Height);

        public void Initialize()
        {
            if (_config == null) _config = Resources.Load<PlayerMoverDriverConfig>("ScriptableObjects/Domain/Player/PlayerMoverDriverConfig");
            if (_config == null) throw new InvalidOperationException("Build Player assets before spawning a Player.");
            if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
            if (_body == null) _body = GetComponent<Rigidbody>();
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

        public MovementProbe Probe()
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
            float clearance = targetAvailable && !IsBlocked(target, _config.Height) ? _config.Height : 0f;
            return new MovementProbe(grounded, grounded ? ground.normal : Vector3.up,
                rebound, rebound ? Mathf.Max(0f, wall.distance - _config.SkinWidth) : 0f,
                rebound ? wall.normal : Vector3.zero, rebound ? Vector3.Angle(forward, -wall.normal) : 0f,
                rebound ? wallSurface.SurfaceId : 0, candidate,
                candidate ? vault.collider.bounds.max.y - feet.y : 0f, clearance, target,
                _state.Height < _config.Height && IsBlocked(feet, _config.Height));
        }

        public PlayerMoveResult Move(Vector3 displacement, Vector3 velocity, bool crouched, float heading, float dt)
        {
            if (!_state.Ready) throw new InvalidOperationException("PlayerDriver.Initialize must precede Move.");
            SetCapsule(crouched);
            _state.PreviousPosition = _state.Position;
            _state.PreviousHeading = _state.Heading;
            _state.Heading = heading;
            Vector3 position = _state.Position;
            Vector3 remaining = displacement;
            bool grounded = false;
            bool ceiling = false;
            for (int i = 0; i < Mathf.Max(1, _config.CastIterations) && remaining.sqrMagnitude > 0.0000001f; i++)
            {
                if (!Cast(position, _state.Height, remaining.normalized, remaining.magnitude + _config.SkinWidth, out RaycastHit hit))
                { position += remaining; break; }
                Vector3 travel = _presenter.TravelBeforeHit(remaining, hit.distance, _config.SkinWidth);
                position += travel;
                remaining -= travel;
                bool floor = _presenter.IsWalkable(hit.normal, _config.SlopeLimitDegrees);
                if (!floor && _state.Grounded && velocity.y <= 0f && TryStep(position, remaining, out Vector3 stepped))
                { position = stepped; grounded = true; break; }
                grounded |= floor && velocity.y <= 0f;
                ceiling |= hit.normal.y < -0.5f;
                remaining = _presenter.ProjectAfterHit(remaining, hit.normal);
                velocity = _presenter.ProjectAfterHit(velocity, hit.normal);
            }
            if (velocity.y <= 0f && FindGround(position,
                _state.Grounded ? _config.GroundSnapDistance : _config.GroundProbeDistance, displacement, out RaycastHit ground))
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
            Vector3 velocity, float heading, float dt, float maximumSpeed)
        {
            Vector3 before = _state.Position;
            Vector3 target = _presenter.TraversalPosition(from, to, progress, obstacleHeight, _config.TraversalLift,
                _config.TraversalRisePortion, _config.TraversalTraverseEnd);
            Vector3 displacement = _presenter.LimitHorizontalDisplacement(target - before, maximumSpeed, dt);
            PlayerMoveResult resolved = Move(displacement, velocity, false, heading, dt);
            return new PlayerMoveResult(resolved.Position, (resolved.Position - before) / dt, resolved.Grounded, resolved.Ceiling);
        }

        public void ShowMovement(MovementState movement)
        {
            if (_limbs != null) _limbs.Apply(movement, _config.EyeHeight, _config.HandOffset, _config.FootOffset);
        }

        public void Teardown()
        {
            _state.Ready = false;
            _state.Velocity = Vector3.zero;
            if (_capsule != null) _capsule.enabled = false;
            if (_limbs != null) _limbs.Apply(MovementState.Ground, 0f, Vector3.zero, Vector3.zero);
        }

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
            RaycastHit[] hits = Physics.CapsuleCastAll(bottom, top, Mathf.Max(0.001f, _config.Radius - _config.SkinWidth),
                direction, Mathf.Max(0f, distance), _config.CollisionMask, QueryTriggerInteraction.Ignore);
            closest = default;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                closest = hit;
                nearest = hit.distance;
            }
            return nearest < float.PositiveInfinity;
        }

        private bool IsBlocked(Vector3 feet, float height)
        {
            _presenter.Capsule(feet, height, _config.Radius, out Vector3 bottom, out Vector3 top);
            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, Mathf.Max(0.001f, _config.Radius - _config.SkinWidth),
                _config.CollisionMask, QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
                if (!overlap.transform.IsChildOf(transform)) return true;
            return false;
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
                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance,
                    _config.CollisionMask, QueryTriggerInteraction.Ignore);
                RaycastHit first = default;
                float firstDistance = float.PositiveInfinity;
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.distance >= firstDistance) continue;
                    first = hit; firstDistance = hit.distance;
                }
                // A non-walkable first hit occludes any lower surface on this ray.
                if (firstDistance >= nearest || !_presenter.IsWalkable(first.normal, _config.SlopeLimitDegrees)) continue;
                closest = first; nearest = firstDistance; found = true;
            }
            return found;
        }
    }
}
