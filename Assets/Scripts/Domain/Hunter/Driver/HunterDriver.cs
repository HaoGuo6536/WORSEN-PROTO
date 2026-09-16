// ============================================================================
// HunterDriver.cs
// ============================================================================
// PURPOSE:
//   Applies the named Hunter engine interaction from explicit owner commands.
//   Unity physics, animation or rendering remains at this engine boundary.
//   Contacts and observable feedback return to the Manager through typed events.
// ARCHITECTURAL ROLE:
//   Driver (section 7a) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Traverse physically clear stair risers and verify rounded-edge tread support.
//   - Preserve open-turn inertia after bounded capsule/floor prediction at path refresh.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Scene-owned, no independent simulation loop. Teardown destroys only owned transient effects.
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
        [SerializeField] private HunterAnimationDriver _animation;
        [SerializeField] private HunterAttackDriver _attacks;
        private readonly HunterRoutePresenter _routePresenter = new HunterRoutePresenter();
        private readonly HunterLightPresenter _lightPresenter = new HunterLightPresenter();
        private HunterDriverState _state;
        private readonly HunterSteeringPresenter _presenter = new HunterSteeringPresenter();
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public Vector3 Velocity => _state?.Steering.Velocity ?? Vector3.zero;
        public bool PathAvailable => _state != null && _state.PathAvailable;
        public event Action<Collider> OnLungeContact;
        public event Action<Collider, int> OnRangedContact;
        public event Action<int> OnRangedMiss;
        public event Action<HunterFeedbackEvent> OnAttackFeedback;
        private void OnEnable()
        {
            if (_attacks == null) _attacks = GetComponent<HunterAttackDriver>();
            if (_attacks != null) { _attacks.OnContact += HandleAttackContact; _attacks.OnMiss += HandleAttackMiss; _attacks.OnFeedback += HandleAttackFeedback; }
        }
        private void OnDisable()
        {
            if (_attacks != null) { _attacks.OnContact -= HandleAttackContact; _attacks.OnMiss -= HandleAttackMiss; _attacks.OnFeedback -= HandleAttackFeedback; }
        }
        private void HandleAttackContact(Collider collider, int serial) { OnRangedContact?.Invoke(collider, serial); }
        private void HandleAttackFeedback(HunterFeedbackEvent feedback) { OnAttackFeedback?.Invoke(feedback); }
        public void ConfigureAttackFeedback(Worsen.Core.EntityId hunter, string key) { if (_attacks != null) _attacks.ConfigureFeedback(hunter, key); }
        private void HandleAttackMiss(int serial) { OnRangedMiss?.Invoke(serial); }
        public void SetUnavailableRooms(System.Collections.Generic.IReadOnlyList<Bounds> rooms)
        {
            if (_state == null) return;
            _state.UnavailableRooms.Clear();
            foreach (Bounds room in rooms) _state.UnavailableRooms.Add(room);
            _state.PathCooldown = 0f; _state.PathAvailable = false;
            _presenter.SetPath(_state.Steering, Array.Empty<Vector3>());
        }
        public void SetTargetFilter(Func<Collider, bool> filter) { if (_attacks != null) _attacks.SetTargetFilter(filter); }
        public float NoiseTransmission(Vector3 source) => ClearSegment(Position + Vector3.up * _config.EyeHeight, source + Vector3.up * 0.5f) ? 1f : 0.35f;
        public void BeginAttackWarning(HunterAttackStyle style, int serial, Vector3 target, float range, float radius, bool split, bool ring)
        { if (_attacks != null) _attacks.BeginWarning(style, serial, target, range, radius, split, ring); }
        public void FireAttack(float speed, float radius) { if (_attacks != null) _attacks.Fire(speed, radius); }
        public void TickAttacks(float dt, long tick) { if (_attacks != null) _attacks.Tick(dt, tick); }
        public void Initialize()
        {
            if (_config == null) _config = Resources.Load<HunterMotorDriverConfig>("ScriptableObjects/Domain/Hunter/HunterMotorDriverConfig");
            if (_config == null) throw new InvalidOperationException("Generate and wire HunterMotorDriverConfig before initialization.");
            if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.isKinematic = true; _body.useGravity = false;
            _state = new HunterDriverState { Path = new NavMeshPath() };
            _presenter.Reset(_state.Steering, Position, Forward);
            if (_animation == null) _animation = GetComponentInChildren<HunterAnimationDriver>();
            if (_animation != null) _animation.Initialize();
            if (_attacks == null) _attacks = GetComponent<HunterAttackDriver>();
            if (_attacks != null) _attacks.Initialize();
        }
        public SightProbe ProbeSight(Vector3 target, Func<Collider, bool> isTarget)
        {
            Vector3 heights = _config.TargetSampleHeights;
            return new SightProbe(CanSee(target + Vector3.up * heights.x, isTarget),
                CanSee(target + Vector3.up * heights.y, isTarget), CanSee(target + Vector3.up * heights.z, isTarget));
        }
        public HunterLightObservation ProbeLight(FlashlightSample sample, long tick, float sightRange, float sightCone, int maxAge, Func<Collider, bool> isEmitter = null)
        {
            if (!_lightPresenter.IsFresh(sample, tick, maxAge)) return default;
            Vector3 eye = Position + Vector3.up * _config.EyeHeight;
            bool illuminated = _lightPresenter.InBeam(sample, eye) && ClearSegment(sample.Origin, eye);
            bool sourceVisible = _lightPresenter.InSight(eye, Forward, sample.Origin, sightRange, sightCone) && ClearSegment(eye, sample.Origin, isEmitter);
            if (illuminated || sourceVisible) return new HunterLightObservation(true, illuminated, sample.Origin, tick);
            if (Physics.Raycast(sample.Origin, sample.Direction.normalized, out RaycastHit hit, sample.Range,
                WithoutHunterGate(_config.SightMask), QueryTriggerInteraction.Ignore))
            {
                Vector3 patch = hit.point + hit.normal * 0.03f;
                if (_lightPresenter.InSight(eye, Forward, patch, sightRange, sightCone) && ClearSegment(eye, patch))
                    return new HunterLightObservation(true, false, patch, tick);
            }
            return default;
        }
        private bool ClearSegment(Vector3 origin, Vector3 point, Func<Collider, bool> permitted = null)
        {
            Vector3 delta = point - origin;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, Mathf.Max(0f, delta.magnitude - 0.08f),
                WithoutHunterGate(_config.SightMask), QueryTriggerInteraction.Ignore))
                if (!Own(hit.collider) && (permitted == null || !permitted(hit.collider))) return false;
            return true;
        }
        public bool ValidateReactionTarget(Vector3 target)
        {
            if (!NavMesh.SamplePosition(Position, out NavMeshHit start, 0.5f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(target, out NavMeshHit end, 0.75f, NavMesh.AllAreas) ||
                Mathf.Abs(end.position.y - target.y) > _config.StepHeight ||
                NavMesh.Raycast(start.position, end.position, out _, NavMesh.AllAreas)) return false;
            return _routePresenter.Allowed(new[] { Position, end.position }, _state.UnavailableRooms) &&
                ClearSegment(Position + Vector3.up * _config.EyeHeight, end.position + Vector3.up * _config.EyeHeight);
        }
        public void Animate(float dt, int phase, float progress)
        { if (_animation != null) _animation.Apply(dt, Velocity.magnitude, phase, progress); }
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
            bool refresh = !stopped && !lungeActive && (_state.PathCooldown <= 0f ||
                Vector3.SqrMagnitude(target - _state.LastTarget) > _config.CornerTolerance * _config.CornerTolerance);
            if (refresh)
                RequestPath(target);
            Vector3 start = Position;
            _state.Steering.Position = start;
            if (refresh) _state.ClearCornerArc = HasClearCornerArc(speed, acceleration, turnRate);
            Vector3 movement = _presenter.Tick(_state.Steering, dt, speed, acceleration, turnRate,
                stopped || (!lungeActive && !_state.PathAvailable), lungeActive, lungeDirection,
                lungeSpeed, lungeDistance, _config.CornerTolerance, _state.ClearCornerArc);
            movement.y = 0f;
            Vector3 planar = Sweep(start, movement, lungeActive);
            if (_state.ClearCornerArc && planar.sqrMagnitude + 0.000001f < movement.sqrMagnitude)
            { _state.ClearCornerArc = false; _state.PathCooldown = 0f; }
            Vector3 position = start + planar;
            if (!stopped && planar.sqrMagnitude + 0.000001f < movement.sqrMagnitude &&
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
        private bool HasClearCornerArc(float speed, float acceleration, float turnRate)
        {
            // A navigation corner can have ample outside clearance. Stopping at every
            // bevel removes the intended turn inertia. Prove the ordinary arc before
            // allowing it; stairs, gaps, blocked or unfinished predictions stay precise.
            HunterSteeringDriverState source = _state.Steering;
            if (!_state.PathAvailable || _state.RepathActive || source.AlignAfterCorner ||
                speed <= 0f || acceleration <= 0f || turnRate <= 0f) return false;
            int corner = source.CornerIndex;
            while (corner == 0 && corner < source.Corners.Length && Vector3.Distance(source.Position, source.Corners[corner]) <= _config.CornerTolerance)
                corner++;
            if (corner <= 0 || corner >= source.Corners.Length - 1) return false;
            Vector3 incoming = source.Corners[corner] - source.Corners[corner - 1]; incoming.y = 0f;
            Vector3 outgoing = source.Corners[corner + 1] - source.Corners[corner]; outgoing.y = 0f;
            if (incoming.sqrMagnitude < 0.0001f || outgoing.sqrMagnitude < 0.0001f ||
                Vector3.Dot(incoming.normalized, outgoing.normalized) >= 0.99f) return false;
            Vector3 approach = source.Corners[corner] - source.Position; approach.y = 0f;
            if (approach.magnitude > speed * speed / (2f * acceleration) + speed * _config.PathRepathSeconds + _config.CornerTolerance)
                return false;
            for (int i = corner; i < source.Corners.Length; i++)
                if (Mathf.Abs(source.Corners[i].y - source.Corners[0].y) > _config.GroundProbeDistance) return false;
            HunterSteeringDriverState preview = _state.CornerPreview;
            preview.Position = source.Position; preview.Forward = source.Forward; preview.Velocity = source.Velocity;
            preview.Corners = source.Corners; preview.CornerIndex = source.CornerIndex;
            preview.AlignAfterCorner = false; preview.LungeWasActive = false;
            Capsule(preview.Position, out Vector3 low, out Vector3 high);
            int overlaps = Physics.OverlapCapsuleNonAlloc(low, high, Mathf.Max(0.001f, _config.Radius - _config.SkinWidth),
                _state.CornerOverlaps, WithoutHunterGate(_config.CollisionMask), QueryTriggerInteraction.Ignore);
            if (overlaps == _state.CornerOverlaps.Length) return false;
            for (int i = 0; i < overlaps; i++) if (!Own(_state.CornerOverlaps[i])) return false;
            const float step = 1f / 30f;
            for (int sample = 0; sample < 48; sample++)
            {
                Vector3 before = preview.Position;
                Vector3 delta = _presenter.Tick(preview, step, speed, acceleration, turnRate,
                    false, false, Vector3.zero, 0f, 0f, _config.CornerTolerance, true);
                if (!ClearCornerSegment(before, delta) || !HasLevelSupport(preview.Position)) return false;
                if (preview.CornerIndex > corner && preview.CornerIndex < preview.Corners.Length)
                {
                    Vector3 direction = preview.Corners[preview.CornerIndex] - preview.Position; direction.y = 0f;
                    if (Vector3.Dot(preview.Forward, direction.normalized) > 0.999f &&
                        Vector3.Dot(preview.Velocity.normalized, direction.normalized) > 0.99f) return true;
                }
            }
            return false;
        }
        private bool ClearCornerSegment(Vector3 position, Vector3 delta)
        {
            if (delta.sqrMagnitude <= 0.00000001f) return true;
            Capsule(position, out Vector3 low, out Vector3 high);
            low += Vector3.up * _config.SkinWidth; high += Vector3.up * _config.SkinWidth;
            int hits = Physics.CapsuleCastNonAlloc(low, high, _config.Radius, delta.normalized, _state.CornerCastHits,
                delta.magnitude + _config.SkinWidth, WithoutHunterGate(_config.CollisionMask), QueryTriggerInteraction.Ignore);
            if (hits == _state.CornerCastHits.Length) return false;
            for (int i = 0; i < hits; i++)
                if (!Own(_state.CornerCastHits[i].collider) && Vector3.Dot(_state.CornerCastHits[i].normal, delta.normalized) < -0.0001f) return false;
            return true;
        }
        private bool HasLevelSupport(Vector3 position)
        {
            int hits = Physics.RaycastNonAlloc(position + Vector3.up * _config.GroundProbeDistance,
                Vector3.down, _state.CornerCastHits, _config.GroundProbeDistance * 2f + _config.SkinWidth,
                WithoutHunterGate(_config.CollisionMask), QueryTriggerInteraction.Ignore);
            if (hits == _state.CornerCastHits.Length) return false;
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = _state.CornerCastHits[i];
                if (!Own(hit.collider) && Vector3.Angle(hit.normal, Vector3.up) <= _config.SlopeLimitDegrees) return true;
            }
            return false;
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
            if (_state.PathAvailable && !_routePresenter.Allowed(_state.RepathActive ? _state.Steering.Corners : _state.Path.corners, _state.UnavailableRooms))
                _state.PathAvailable = false;
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
            if (!Cast(raised, drop, out RaycastHit ground)) return false;
            if (Vector3.Angle(ground.normal, Vector3.up) > _config.SlopeLimitDegrees &&
                !SupportedStairEdge(position, raised, movement, ground.collider)) return false;
            stepped = raised + Vector3.down * Mathf.Max(0f, ground.distance - _config.SkinWidth);
            return stepped.y <= position.y + _config.StepHeight + _config.SkinWidth && !Blocked(stepped);
        }
        private bool SupportedStairEdge(Vector3 position, Vector3 raised, Vector3 movement, Collider edge)
        {
            // At walking speed the capsule first lands on the rounded lip, whose
            // contact normal can exceed the slope limit even on a flat stair top.
            // Confirm that top within the forward foot footprint on the same solid;
            // never classify a wall, steep ramp or unrelated floor as a stair.
            Vector3 direction = new Vector3(movement.x, 0f, movement.z).normalized;
            Vector3 origin = raised + direction * Mathf.Max(0f, _config.Radius - _config.SkinWidth);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, _config.StepHeight + _config.GroundProbeDistance,
                WithoutHunterGate(_config.CollisionMask), QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (Own(hit.collider)) continue;
                float rise = hit.point.y - position.y;
                return hit.collider == edge && rise > _config.SkinWidth && rise <= _config.StepHeight + _config.SkinWidth &&
                    Vector3.Angle(hit.normal, Vector3.up) <= _config.SlopeLimitDegrees;
            }
            return false;
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
        public void Teardown()
        {
            if (_animation != null) _animation.Teardown();
            if (_attacks != null) _attacks.Teardown();
            _state = null;
        }
    }
}
