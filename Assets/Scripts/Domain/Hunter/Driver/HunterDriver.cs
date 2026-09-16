// ============================================================================
// HunterDriver.cs
// ============================================================================
// PURPOSE:
//   Uses the navigation mesh only to request paths, then applies inertial steering.
//   Sight rays and swept capsule contacts return observations to the owning
//   Manager, keeping game identities and combat decisions outside the engine boundary.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Probe three sight samples and request complete paths without NavMeshAgent steering.
//   - Apply collision-limited steps and report all active-lunge contacts.
//   - Revalidate active gap crossings from stable anchors before keeping progress.
// DEPENDENCIES:
//   - Core sight values; Unity physics and navigation APIs; own Presenter and DriverConfig.
// USAGE NOTES:
//   Scene-owned, commanded only by HunterManager. No global side effects or tick loop.
//   Ignores the authored HunterRouteGate layer for hunter movement and sight.
//   Sweeps inset the query radius by skin width, then retain that skin when stopping.
//   This keeps a floor-tangent spawn out of the sweep's initial-overlap result.
//   An invalid gap remains stopped until a fresh route validates its crossing or
//   the body leaves the crossing on the navigation mesh; a bank sample is not validation.
//   If rejection occurs while still attached to the entry bank, a clear navigation
//   return to that entry permits a fresh detour from the actual position.
//   Unimpeded arrival retains the Presenter's terminal velocity; clipped motion
//   and vertical grounding still feed their actual displacement back to steering.
// ============================================================================
using System;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
namespace Worsen.Domain.Hunter
{
    [RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
    public sealed class HunterDriver : MonoBehaviour
    {
        [SerializeField] private HunterMotorDriverConfig _config;
        [SerializeField] private CapsuleCollider _capsule;
        [SerializeField] private Rigidbody _body;
        private HunterDriverState _state;
        private readonly HunterSteeringPresenter _presenter = new HunterSteeringPresenter();
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public Vector3 Velocity => _state?.Steering.Velocity ?? Vector3.zero;
        public bool PathAvailable => _state != null && _state.PathAvailable;
        public event Action<Collider> OnLungeContact;
        public void Initialize()
        {
            if (_config == null) _config = Resources.Load<HunterMotorDriverConfig>("ScriptableObjects/Domain/Hunter/HunterMotorDriverConfig");
            if (_config == null) throw new InvalidOperationException("Generate and wire HunterMotorDriverConfig before initialization.");
            if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.isKinematic = true; _body.useGravity = false;
            _state = new HunterDriverState { Path = new NavMeshPath() };
            _presenter.Reset(_state.Steering, Position, Forward);
        }
        public SightProbe ProbeSight(Vector3 target, Func<Collider, bool> isTarget)
        {
            Vector3 heights = _config.TargetSampleHeights;
            return new SightProbe(CanSee(target + Vector3.up * heights.x, isTarget),
                CanSee(target + Vector3.up * heights.y, isTarget), CanSee(target + Vector3.up * heights.z, isTarget));
        }
        private bool CanSee(Vector3 point, Func<Collider, bool> isTarget)
        {
            Vector3 origin = Position + Vector3.up * _config.EyeHeight;
            Vector3 delta = point - origin;
            RaycastHit[] hits = Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                WithoutHunterGate(_config.SightMask), QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (Own(hit.collider)) continue;
                return isTarget(hit.collider);
            }
            return false;
        }
        public void Move(Vector3 target, float speed, float acceleration, float turnRate, float dt,
            bool stopped, bool lungeActive, Vector3 lungeDirection, float lungeSpeed, float lungeDistance)
        {
            if (_state == null || !(dt > 0f)) return;
            _state.Contacts.Clear(); _state.PathCooldown -= dt;
            if (!stopped && !lungeActive && (_state.PathCooldown <= 0f ||
                Vector3.SqrMagnitude(target - _state.LastTarget) > _config.CornerTolerance * _config.CornerTolerance))
                RequestPath(target);
            Vector3 start = Position;
            _state.Steering.Position = start;
            Vector3 movement = _presenter.Tick(_state.Steering, dt, speed, acceleration, turnRate,
                stopped || (!lungeActive && !_state.PathAvailable), lungeActive, lungeDirection,
                lungeSpeed, lungeDistance, _config.CornerTolerance);
            movement.y = 0f;
            Vector3 planar = Sweep(start, movement, lungeActive);
            Vector3 position = start + planar;
            if (!stopped && !lungeActive && planar.sqrMagnitude + 0.000001f < movement.sqrMagnitude &&
                TryStep(start, movement, out Vector3 stepped)) position = stepped;
            _state.VerticalSpeed -= _config.Gravity * dt;
            Vector3 vertical = Sweep(position, Vector3.up * (_state.VerticalSpeed * dt), false);
            if (Mathf.Abs(vertical.y) + 0.0001f < Mathf.Abs(_state.VerticalSpeed * dt)) _state.VerticalSpeed = 0f;
            position += vertical;
            _presenter.ReconcileMovement(_state.Steering, start, position, movement, dt);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(_state.Steering.Forward, Vector3.up));
            _body.position = position;
            Physics.SyncTransforms();
            if (lungeActive)
            {
                Capsule(position, out Vector3 low, out Vector3 high);
                foreach (Collider other in Physics.OverlapCapsule(low, high, _config.Radius + _config.SkinWidth,
                    WithoutHunterGate(_config.CollisionMask), QueryTriggerInteraction.Ignore))
                    if (!Own(other) && !_state.Contacts.Contains(other)) _state.Contacts.Add(other);
                foreach (Collider other in _state.Contacts) OnLungeContact?.Invoke(other);
            }
        }
        private void RequestPath(Vector3 target)
        {
            _state.LastTarget = target; _state.PathCooldown = _config.PathRepathSeconds;
            _state.PathAvailable = false;
            bool sampledStart = NavMesh.SamplePosition(Position, out NavMeshHit start, _config.PathSampleRadius, NavMesh.AllAreas);
            if (_state.RepathActive && sampledStart && _presenter.CanReleaseGap(_state.Steering,
                _state.RepathEntry, _state.RepathExit, Position, start.position)) _state.RepathActive = false;
            if (!_state.RepathActive && _presenter.HasActiveSegmentProgress(_state.Steering, Position, _config.CornerTolerance))
            {
                Vector3 entry = _state.Steering.Corners[_state.Steering.CornerIndex - 1];
                Vector3 exit = _state.Steering.Corners[_state.Steering.CornerIndex];
                if (NavMesh.Raycast(entry, exit, out _, NavMesh.AllAreas))
                {
                    _state.RepathEntry = entry; _state.RepathExit = exit;
                    _state.RepathActive = true; _state.RepathReverse = false;
                }
            }
            if (sampledStart)
            {
                if (NavMesh.SamplePosition(target, out NavMeshHit end, _config.PathSampleRadius, NavMesh.AllAreas))
                {
                    if (_state.RepathActive)
                    {
                        _state.PathAvailable = RevalidateGap(end.position);
                        if (!_state.PathAvailable && CanLeaveRejectedGap(start.position)) _state.RepathActive = false;
                    }
                    if (!_state.RepathActive)
                        _state.PathAvailable = NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _state.Path) &&
                            _state.Path.status == NavMeshPathStatus.PathComplete;
                }
            }
            if (_state.PathAvailable && _state.RepathActive) return;
            _presenter.SetPath(_state.Steering, _state.PathAvailable ? _state.Path.corners : Array.Empty<Vector3>());
        }
        private bool RevalidateGap(Vector3 target)
        {
            float verticalTolerance = _config.GroundProbeDistance + _config.SkinWidth;
            if (!NavMesh.SamplePosition(_state.RepathEntry, out NavMeshHit entry, _config.PathSampleRadius, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(_state.RepathExit, out NavMeshHit exit, _config.PathSampleRadius, NavMesh.AllAreas) ||
                !_presenter.IsNavigationAnchor(_state.RepathEntry, entry.position, verticalTolerance) ||
                !_presenter.IsNavigationAnchor(_state.RepathExit, exit.position, verticalTolerance)) return false;
            Vector3[] forwardTail = ValidatedGapTail(entry.position, exit.position, target);
            Vector3[] reverseTail = ValidatedGapTail(exit.position, entry.position, target);
            return _presenter.TrySetGapPath(_state.Steering, _state.RepathEntry, _state.RepathExit,
                forwardTail, reverseTail, _state.RepathReverse, out _state.RepathReverse);
        }
        private bool CanLeaveRejectedGap(Vector3 sample)
        {
            return NavMesh.SamplePosition(_state.RepathEntry, out NavMeshHit entry, _config.PathSampleRadius, NavMesh.AllAreas) &&
                _presenter.CanLeaveRejectedGap(Position, sample, _state.RepathEntry, entry.position,
                    !NavMesh.Raycast(sample, entry.position, out _, NavMesh.AllAreas), _config.GroundProbeDistance + _config.SkinWidth);
        }
        private Vector3[] ValidatedGapTail(Vector3 entry, Vector3 exit, Vector3 target)
        {
            if (NavMesh.CalculatePath(entry, exit, NavMesh.AllAreas, _state.Path) &&
                _state.Path.status == NavMeshPathStatus.PathComplete &&
                _presenter.IsDirectSegmentPath(_state.Path.corners, entry, exit, _config.GroundProbeDistance + _config.SkinWidth) &&
                NavMesh.CalculatePath(exit, target, NavMesh.AllAreas, _state.Path) &&
                _state.Path.status == NavMeshPathStatus.PathComplete) return _state.Path.corners;
            return null;
        }
        private Vector3 Sweep(Vector3 position, Vector3 delta, bool contacts)
        {
            if (delta.sqrMagnitude <= 0.00000001f) return Vector3.zero;
            if (!Cast(position, delta, out RaycastHit hit)) return delta;
            if (contacts && !_state.Contacts.Contains(hit.collider)) _state.Contacts.Add(hit.collider);
            return delta.normalized * Mathf.Max(0f, hit.distance - _config.SkinWidth);
        }
        private bool Cast(Vector3 position, Vector3 delta, out RaycastHit closest)
        {
            Capsule(position, out Vector3 low, out Vector3 high);
            RaycastHit[] hits = Physics.CapsuleCastAll(low, high, Mathf.Max(0.001f, _config.Radius - _config.SkinWidth), delta.normalized,
                delta.magnitude + _config.SkinWidth, WithoutHunterGate(_config.CollisionMask), QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (Own(hit.collider) || Vector3.Dot(hit.normal, delta.normalized) >= -0.0001f) continue;
                closest = hit; return true;
            }
            closest = default; return false;
        }
        private bool TryStep(Vector3 position, Vector3 movement, out Vector3 stepped)
        {
            stepped = position;
            if (_config.StepHeight <= 0f || movement.sqrMagnitude <= 0.000001f) return false;
            Vector3 lift = Vector3.up * _config.StepHeight;
            if (Cast(position, lift, out _)) return false;
            Vector3 raised = position + lift;
            if (Blocked(raised) || Cast(raised, movement, out _)) return false;
            raised += movement;
            Vector3 drop = Vector3.down * (_config.StepHeight + _config.GroundProbeDistance);
            if (!Cast(raised, drop, out RaycastHit ground) ||
                Vector3.Angle(ground.normal, Vector3.up) > _config.SlopeLimitDegrees) return false;
            stepped = raised + Vector3.down * Mathf.Max(0f, ground.distance - _config.SkinWidth);
            return stepped.y <= position.y + _config.StepHeight + _config.SkinWidth && !Blocked(stepped);
        }
        private bool Blocked(Vector3 position)
        {
            Capsule(position, out Vector3 low, out Vector3 high);
            foreach (Collider other in Physics.OverlapCapsule(low, high, Mathf.Max(0.001f, _config.Radius - _config.SkinWidth),
                WithoutHunterGate(_config.CollisionMask), QueryTriggerInteraction.Ignore))
                if (!Own(other)) return true;
            return false;
        }
        private void Capsule(Vector3 position, out Vector3 low, out Vector3 high)
        { low = position + Vector3.up * _config.Radius; high = position + Vector3.up * (_config.Height - _config.Radius); }
        private bool Own(Collider other) => other == _capsule || other.transform.IsChildOf(transform);
        private static int WithoutHunterGate(int mask)
        { int layer = LayerMask.NameToLayer("HunterRouteGate"); return layer >= 0 ? mask & ~(1 << layer) : mask; }
        public void Teardown() { _state = null; }
    }
}
