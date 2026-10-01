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
//   - Apply swept navigation or explicit ghost replay poses with target-only contact queries.
//   - Honor grace layer exclusions in motor queries; pass through other Hunters' bodies everywhere.
//   - Sample navigation progress, stalls, sight, hearing and retreat evidence.
//   - Route shared/module animation and query contacts at the rendered ceiling body.
//   - Apply charge, teleport and reaction motion; probe silent interception contacts.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Scene-owned, no independent simulation loop. Teardown destroys only owned transient effects.
//   Stall observation uses the default navigation query agent (type 0), matching
//   configured-area path queries. It does not measure avoidance or change paths.
//   Replay disables this body's colliders/agents, not global collision rules. It
//   restores original component flags on teardown; queries still admit target contacts.
//   Hunters ignore each other's bodies (owner, 2026-10-01): HunterBody leaves every
//   motor and sight mask, and a collider under another HunterDriver never blocks,
//   occludes, receives a contact or yields a push normal. Walls and the target still do.
// ============================================================================
using System;
using System.Buffers;
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
        private WeaverWebDriver _weaver;
        private IHunterPlacementDriver _stare;
        public void ConfigureStare(IHunterPlacementDriver placement)
        {
            _stare = placement ?? throw new ArgumentNullException(nameof(placement));
            _stare.Initialize();
        }
        public void SetStarePresent(bool present) { if (_stare != null) _stare.SetPresent(present); }
        public bool ProbeStare(Vector3 candidate, Vector3 player, HunterPlayerView view, out Vector3 point)
            => _stare.Probe(candidate, player, view, _config, out point);
        public bool PlayerViewClear(HunterPlayerView view, Vector3 point, float height)
            => ClearSegment(view.Origin, point + Vector3.up * height, _state.TargetFilter);
        public void PlaceStare(Vector3 point)
        {
            _presenter.Reset(_state.Steering, point, Forward); _state.VerticalSpeed = 0f; _state.PathCooldown = 0f;
            transform.position = point; _body.position = point; Physics.SyncTransforms();
        }
        private readonly HunterRoutePresenter _routePresenter = new HunterRoutePresenter();
        private readonly HunterLightPresenter _lightPresenter = new HunterLightPresenter();
        private readonly HunterBodyPresenter _bodyPresenter = new HunterBodyPresenter();
        private HunterDriverState _state;
        private readonly HunterSteeringPresenter _presenter = new HunterSteeringPresenter();
        private readonly HunterStallPresenter _stallPresenter = new HunterStallPresenter();
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public Vector3 Velocity => _state?.Steering.Velocity ?? Vector3.zero;
        public bool PathAvailable => _state != null && _state.PathAvailable;
        public void RemoveMomentum()
        { if (_state != null) { _state.Steering.Velocity = Vector3.zero; _state.VerticalSpeed = 0f; } }
        public System.Collections.Generic.IReadOnlyList<Vector3> ProbeReactionPath(Vector3 target)
        {
            if (_state == null) return Array.Empty<Vector3>();
            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(Position, out NavMeshHit start, _config.PathSampleRadius, _config.NavigationAreaMask) &&
                NavMesh.SamplePosition(target, out NavMeshHit end, _config.PathSampleRadius, _config.NavigationAreaMask) &&
                NavMesh.CalculatePath(start.position, end.position, _config.NavigationAreaMask, path)) return path.corners;
            return Array.Empty<Vector3>();
        }
        public event Action<Collider> OnLungeContact;
        public Vector3 ContactNormal(Collider other)
        {
            // Another Hunter's body is never depenetrated against or pushed away from.
            if (other == null || _capsule == null || (_state != null && ForeignHunter(other))) return Vector3.zero;
            if (Physics.ComputePenetration(other, other.transform.position, other.transform.rotation,
                _capsule, _capsule.transform.position, _capsule.transform.rotation, out var normal, out _)) return normal;
            Vector3 delta = other.bounds.center - _capsule.bounds.center;
            delta.y = 0f;
            return delta.normalized;
        }
        public event Action<Collider, int> OnRangedContact;
        public event Action<int> OnRangedMiss;
        public event Action<HunterFeedbackEvent> OnAttackFeedback;
        public event Action<HunterStallFact> OnStall;
        public void ObserveStall(float dt, long tick, Worsen.Core.EntityId hunter, HunterAction action, int lastRoom)
        {
            if (_state == null) return;
            Vector3[] corners = _state.Steering.Corners;
            if (!_stallPresenter.Observe(_state.Stall, Position, corners, _state.PathAvailable, dt,
                _config.StallDuration, _config.StallMinimumProgress, _config.StallMinimumRemaining, out double remaining)) return;
            Vector3 scale = _capsule.transform.lossyScale;
            OnStall?.Invoke(new HunterStallFact(hunter, tick, Position, lastRoom > 0 ? lastRoom : (int?)null,
                corners, NavMesh.GetSettingsByID(0).agentRadius,
                _capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)), _config.Radius, action, remaining));
        }
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
        public void SetTargetFilter(Func<Collider, bool> filter)
        { if (_state != null) _state.TargetFilter = filter; if (_attacks != null) _attacks.SetTargetFilter(filter); }
        public void SetEmergence(bool enabled, Vector3 observer, int budget)
        {
            if (_state == null) return;
            if (_state.EmergenceEnabled != enabled) _state.PathCooldown = 0f;
            _state.EmergenceEnabled = enabled; _state.EmergenceObserver = observer;
            _state.EmergenceBudget = Mathf.Clamp(budget, 2, 32);
            if (!enabled) _state.EmergenceCorner = -1;
        }
        public void SetLook(Vector3 target, bool looking, bool catchActive)
        { if (_animation != null) _animation.SetLook(target, looking, catchActive); }
        public float NoiseTransmission(Vector3 source) => ClearSegment(Position + Vector3.up * _config.EyeHeight, source + Vector3.up * 0.5f) ? 1f : 0.35f;
        public System.Collections.Generic.IReadOnlyList<int> ProbeOccludedRooms(LevelGraph graph, Vector3 observer)
        {
            var rooms = new System.Collections.Generic.List<int>();
            if (graph == null) return rooms;
            foreach (LevelRoom room in graph.Rooms)
            {
                Vector3 point = new Vector3(room.Center.x, room.Bounds.min.y, room.Center.z);
                if (!ClearSegment(observer + Vector3.up * _config.EyeHeight, point + Vector3.up * _config.EyeHeight)) rooms.Add(room.Id);
            }
            return rooms;
        }
        public void ApplyDecisionMotion(Vector3 stumble, Vector3 facing)
        {
            if (_state == null || (stumble.sqrMagnitude == 0f && facing.sqrMagnitude == 0f)) return;
            Vector3 position = Position + Sweep(Position, stumble, false);
            _state.Steering.Position = position;
            _state.Steering.Velocity = Vector3.zero;
            if (facing.sqrMagnitude > 0f) _state.Steering.Forward = facing;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(_state.Steering.Forward, Vector3.up));
            _body.position = position; Physics.SyncTransforms();
        }
        public void BeginAttackWarning(HunterAttackStyle style, int serial, Vector3 target, float range, float radius, bool split, bool ring)
        { if (_attacks != null) _attacks.BeginWarning(style, serial, target, range, radius, split, ring); }
        public void FireAttack(float speed, float radius) { if (_attacks != null) _attacks.Fire(speed, radius); }
        public void TickAttacks(float dt, long tick) { if (_attacks != null) _attacks.Tick(dt, tick); }
        public void ConfigureWeaver(WeaverDriverConfig config)
        {
            _weaver = GetComponent<WeaverWebDriver>();
            if (_weaver == null) _weaver = gameObject.AddComponent<WeaverWebDriver>();
            _weaver.Initialize(config, _config);
        }
        public float WeaverShotHeight => _weaver.ShotHeight;
        public WeaverObservation ProbeWeaver(Vector3 target, float radius, float range, long tick)
            => _weaver.Probe(target, radius, range, tick, _state.TargetFilter);
        public void SetWeaverCeiling(float height, bool ceiling) { _weaver.SetCeiling(height, ceiling); }
        public bool LaunchWeb(Vector3 origin, Vector3 target, float radius, float speed, float range, int serial)
            => _weaver.Launch(origin, target, radius, speed, range, serial, _state.TargetFilter);
        public void AddWeaverNest(WeaverFact fact) { _weaver.AddNest(fact); }
        public System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<Collider, int>> TickWebs(float dt)
            => _weaver.TickWebs(dt, _state.TargetFilter);
        public void Initialize(HunterMotorDriverConfig configOverride = null)
        {
            Teardown();
            if (configOverride != null) _config = configOverride;
            if (_config == null) _config = Resources.Load<HunterMotorDriverConfig>("ScriptableObjects/Domain/Hunter/HunterMotorDriverConfig");
            if (_config == null) throw new InvalidOperationException("Generate and wire HunterMotorDriverConfig before initialization.");
            if (_capsule == null) _capsule = GetComponent<CapsuleCollider>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.isKinematic = true; _body.useGravity = false;
            _state = new HunterDriverState { Path = new NavMeshPath() };
            _state.QueryHits = ArrayPool<RaycastHit>.Shared.Rent(64);
            _state.QueryOverlaps = ArrayPool<Collider>.Shared.Rent(64);
            int gate = LayerMask.NameToLayer("HunterRouteGate");
            _state.HunterBodyLayer = LayerMask.NameToLayer("HunterBody");
            _state.CollisionMask = _bodyPresenter.WithoutLayers(_config.CollisionMask, gate, _state.HunterBodyLayer);
            _state.SightMask = _bodyPresenter.WithoutLayers(_config.SightMask, gate, _state.HunterBodyLayer);
            _presenter.Reset(_state.Steering, Position, Forward);
            if (_animation == null) _animation = GetComponentInChildren<HunterAnimationDriver>();
            if (_animation != null) _animation.Initialize();
            if (_animation != null && _animation.IsReady && _animation.State.HasHumanoidRig)
            {
                Animator animator = _animation.Animator;
                _state.IKDriver = animator.GetComponent<HunterAnimatorIKDriver>();
                if (_state.IKDriver != null && !_state.IKDriver.enabled) _state.IKDriver = null;
                _state.OwnIKDriver = _state.IKDriver == null;
                if (_state.OwnIKDriver) _state.IKDriver = animator.gameObject.AddComponent<HunterAnimatorIKDriver>();
                _state.IKDriver.Bind(animator, transform, _animation.Config, _animation.State);
            }
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
                _state.SightMask, QueryTriggerInteraction.Ignore))
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
            int count = RayQuery(origin, delta.normalized, Mathf.Max(0f, delta.magnitude - 0.08f), _state.SightMask);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _state.QueryHits[i];
                if (!Own(hit.collider) && (permitted == null || !permitted(hit.collider)) && !ForeignHunter(hit.collider)) return false;
            }
            return true;
        }
        public bool ValidateReactionTarget(Vector3 target)
        {
            if (!NavMesh.SamplePosition(Position, out NavMeshHit start, 0.5f, _config.NavigationAreaMask) ||
                !NavMesh.SamplePosition(target, out NavMeshHit end, 0.75f, _config.NavigationAreaMask) ||
                Mathf.Abs(end.position.y - target.y) > _config.StepHeight ||
                NavMesh.Raycast(start.position, end.position, out _, _config.NavigationAreaMask)) return false;
            return _routePresenter.Allowed(new[] { Position, end.position }, _state.UnavailableRooms) &&
                ClearSegment(Position + Vector3.up * _config.EyeHeight, end.position + Vector3.up * _config.EyeHeight);
        }
        public void TriggerAnimation(HunterAnimationPhase phase)
        { if (_animation != null) _animation.Trigger(phase); }
        public void Animate(float dt, int phase, float progress, HunterAnimationPhase module = HunterAnimationPhase.None)
        {
            if (_animation != null) _animation.Apply(dt, Velocity.magnitude, phase, progress, module);
            // Generic root curves must not undo the parent-owned ceiling transform.
            if (_weaver != null && _weaver.BodyOffset > 0f) _weaver.ApplyBodyPose();
        }
        public void ConfigureKinematicReplay()
        {
            if (_state == null || _state.KinematicReplay) return;
            _state.KinematicReplay = true;
            _state.ReplayRenderers = GetComponentsInChildren<Renderer>(true);
            _state.ReplayRenderingOff = new bool[_state.ReplayRenderers.Length];
            for (int i = 0; i < _state.ReplayRenderers.Length; i++)
            {
                _state.ReplayRenderingOff[i] = _state.ReplayRenderers[i].forceRenderingOff;
                _state.ReplayRenderers[i].forceRenderingOff = true;
            }
            _state.ReplayColliders = GetComponentsInChildren<Collider>(true);
            _state.ReplayColliderEnabled = new bool[_state.ReplayColliders.Length];
            for (int i = 0; i < _state.ReplayColliders.Length; i++)
            {
                _state.ReplayColliderEnabled[i] = _state.ReplayColliders[i].enabled;
                _state.ReplayColliders[i].enabled = false;
            }
            _state.ReplayAgents = GetComponentsInChildren<NavMeshAgent>(true);
            _state.ReplayAgentEnabled = new bool[_state.ReplayAgents.Length];
            for (int i = 0; i < _state.ReplayAgents.Length; i++)
            {
                _state.ReplayAgentEnabled[i] = _state.ReplayAgents[i].enabled;
                _state.ReplayAgents[i].enabled = false;
            }
            RemoveMomentum();
        }
        public void MoveKinematicReplay(bool present, HunterReplayPose pose,
            System.Collections.Generic.IReadOnlyList<HunterReplayPose> points, float dt)
        {
            if (_state == null || !_state.KinematicReplay) return;
            bool wasPresent = _state.ReplayPresent;
            _state.ReplayPresent = present;
            for (int i = 0; i < _state.ReplayRenderers.Length; i++)
                if (_state.ReplayRenderers[i] != null)
                    _state.ReplayRenderers[i].forceRenderingOff = !present || _state.ReplayRenderingOff[i];
            RemoveMomentum();
            if (!present) return;
            Vector3 start = Position;
            // First appearance is placement, not a sweep from the arbitrary spawn.
            if (!wasPresent) PlaceReplay(points.Count > 0 ? points[0] : pose);
            foreach (HunterReplayPose point in points)
            {
                Vector3 previous = Position;
                PlaceReplay(point);
                ProbeReplaySegment(previous, point.Position);
                ProbeBodyContact();
            }
            PlaceReplay(pose);
            _state.Steering.Velocity = wasPresent && dt > 0f ? (Position - start) / dt : Vector3.zero;
            ProbeBodyContact();
        }
        private void PlaceReplay(HunterReplayPose pose)
        {
            Quaternion rotation = Quaternion.Euler(0f, pose.HeadingDegrees, 0f);
            transform.SetPositionAndRotation(pose.Position, rotation);
            _body.position = pose.Position; _body.rotation = rotation;
            _state.Steering.Position = pose.Position; _state.Steering.Forward = transform.forward;
            Physics.SyncTransforms();
        }
        private void ProbeReplaySegment(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude <= 0f) return;
            Capsule(from, out Vector3 low, out Vector3 high);
            int count = CapsuleQuery(low, high, _config.Radius, delta.normalized, delta.magnitude);
            for (int i = 0; i < count; i++)
            {
                Collider other = _state.QueryHits[i].collider;
                // Walls never stop the replay or occlude its target-only contacts.
                if (!IgnoreMotorCollider(other) && (_state.TargetFilter?.Invoke(other) ?? false)) OnLungeContact?.Invoke(other);
            }
        }
        public int MoveRecording(System.Collections.Generic.IReadOnlyList<Vector3> points, float dt)
            => MoveRecording(points, dt, out _);
        public bool MoveCharge(Vector3 displacement, Vector3 direction, float dt, out Collider blocker)
        {
            blocker = null;
            if (_state == null || !(dt > 0f)) return false;
            Vector3 start = Position, movement = displacement;
            bool blocked = !_routePresenter.Allowed(new[] { start, start + movement }, _state.UnavailableRooms);
            if (blocked) movement = Vector3.zero;
            else if (movement.sqrMagnitude > 0f && Cast(start, movement, out RaycastHit hit))
            {
                blocked = true; blocker = hit.collider;
                movement = movement.normalized * Mathf.Min(movement.magnitude, Mathf.Max(0f, hit.distance - _config.SkinWidth));
            }
            // No NavMesh steering, step-up, slide or turn during a charge.
            if (movement.sqrMagnitude > 0f && !HasLevelSupport(start + movement))
            { movement = Vector3.zero; blocker = null; blocked = true; }
            _state.Steering.Position = start + movement;
            _state.Steering.Velocity = blocked ? Vector3.zero : movement / dt;
            _state.Steering.Forward = direction;
            _state.PathCooldown = 0f; _state.VerticalSpeed = 0f;
            transform.SetPositionAndRotation(start + movement, Quaternion.LookRotation(direction, Vector3.up));
            _body.position = start + movement; Physics.SyncTransforms();
            if (blocker != null) OnLungeContact?.Invoke(blocker);
            return blocked;
        }
        public bool TryTeleport(Vector3 position)
        {
            if (_state == null || float.IsNaN(position.sqrMagnitude) || float.IsInfinity(position.sqrMagnitude) ||
                !NavMesh.SamplePosition(position, out NavMeshHit hit, _config.SkinWidth, _config.NavigationAreaMask) ||
                Vector3.Distance(position, hit.position) > _config.SkinWidth || Blocked(position) ||
                !_routePresenter.Allowed(new[] { position, position }, _state.UnavailableRooms)) return false;
            // Atomic endpoint transfer: no audio, trail, animation or intermediate poses.
            transform.position = position; _body.position = position;
            _presenter.Reset(_state.Steering, position, Forward);
            _state.PathCooldown = 0f; _state.VerticalSpeed = 0f;
            Physics.SyncTransforms(); return true;
        }
        public void ProbeMimicTouch(float radius)
        {
            int count = SphereOverlap(Position + Vector3.up * radius, radius);
            for (int i = 0; i < count; i++)
            {
                Collider other = _state.QueryOverlaps[i];
                if (!Own(other) && (_state.TargetFilter?.Invoke(other) ?? false)) OnLungeContact?.Invoke(other);
            }
        }
        public void ProbeBodyContact()
        {
            if (_state == null || (_state.KinematicReplay && !_state.ReplayPresent)) return;
            // Navigation sweeps intentionally stay at floor level. Body contacts
            // must instead follow the real capsule when Weaver hangs overhead.
            Capsule(Position + Vector3.up * (_weaver != null ? _weaver.BodyOffset : 0f), out Vector3 low, out Vector3 high);
            int count = CapsuleOverlap(low, high, _config.Radius + _config.SkinWidth);
            for (int i = 0; i < count; i++)
            {
                Collider other = _state.QueryOverlaps[i];
                if (!IgnoreMotorCollider(other) && (_state.TargetFilter?.Invoke(other) ?? false)) OnLungeContact?.Invoke(other);
            }
        }
        public int MoveRecording(System.Collections.Generic.IReadOnlyList<Vector3> points, float dt, out bool unreachable)
        {
            unreachable = false;
            if (_state == null || !(dt > 0f) || points == null) return 0;
            Vector3 start = Position, position = start;
            int reached = 0;
            _state.PathAvailable = true;
            foreach (Vector3 point in points)
            {
                // Validate, never substitute sampled positions or a NavMesh route.
                if (!NavMesh.SamplePosition(position, out NavMeshHit a, _config.GroundProbeDistance + _config.SkinWidth, _config.NavigationAreaMask) ||
                    !NavMesh.SamplePosition(point, out NavMeshHit b, _config.GroundProbeDistance + _config.SkinWidth, _config.NavigationAreaMask) ||
                    Vector3.ProjectOnPlane(a.position - position, Vector3.up).sqrMagnitude > _config.SkinWidth * _config.SkinWidth ||
                    Vector3.ProjectOnPlane(b.position - point, Vector3.up).sqrMagnitude > _config.SkinWidth * _config.SkinWidth ||
                    NavMesh.Raycast(a.position, b.position, out _, _config.NavigationAreaMask))
                { _state.PathAvailable = false; unreachable = true; break; }
                if (!_routePresenter.Allowed(new[] { position, point }, _state.UnavailableRooms) ||
                    !ClearCornerSegment(position, point - position))
                { _state.PathAvailable = false; break; }
                Vector3 direction = point - position; direction.y = 0f;
                if (direction.sqrMagnitude > 0f) _state.Steering.Forward = direction.normalized;
                position = point; reached++;
            }
            _state.Steering.Position = position; _state.Steering.Velocity = (position - start) / dt;
            _state.VerticalSpeed = 0f; _state.PathCooldown = 0f;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(_state.Steering.Forward, Vector3.up));
            _body.position = position; Physics.SyncTransforms();
            return reached;
        }
        private bool CanSee(Vector3 point, Func<Collider, bool> isTarget)
        {
            Vector3 origin = Position + Vector3.up * _config.EyeHeight;
            Vector3 delta = point - origin;
            int count = RayQuery(origin, delta.normalized, delta.magnitude, _state.SightMask);
            SortHits(count);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _state.QueryHits[i];
                if (Own(hit.collider) || ForeignHunter(hit.collider)) continue;
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
            if (!stopped && !lungeActive && _state.PathAvailable && _weaver != null &&
                _weaver.TryCrossPartition(_state.Steering.Corners, _state.Steering.CornerIndex, out Vector3 linkEnd))
            {
                // Kinematic endpoint transfer only; no global/layer collision changes.
                transform.position = linkEnd; _body.position = linkEnd;
                _presenter.Reset(_state.Steering, linkEnd, Forward);
                _state.PathCooldown = 0f; _state.VerticalSpeed = 0f;
                Physics.SyncTransforms(); return;
            }
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
                int count = CapsuleOverlap(low, high, _config.Radius + _config.SkinWidth);
                for (int i = 0; i < count; i++)
                {
                    Collider other = _state.QueryOverlaps[i];
                    if (!Own(other) && !ForeignHunter(other) && !_state.Contacts.Contains(other)) _state.Contacts.Add(other);
                }
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
            // Precise steering reaches this hidden corner before emerging; no new waypoint,
            // shortcut or tolerance bypass is introduced into PLAN-014's corner/gap logic.
            if (corner == _state.EmergenceCorner) return false;
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
            int overlaps = CapsuleOverlap(low, high, Mathf.Max(0.001f, _config.Radius - _config.SkinWidth));
            for (int i = 0; i < overlaps; i++) if (!IgnoreMotorCollider(_state.QueryOverlaps[i])) return false;
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
            int hits = CapsuleQuery(low, high, _config.Radius, delta.normalized, delta.magnitude + _config.SkinWidth);
            for (int i = 0; i < hits; i++)
                if (!IgnoreMotorCollider(_state.QueryHits[i].collider) && Vector3.Dot(_state.QueryHits[i].normal, delta.normalized) < -0.0001f) return false;
            return true;
        }
        private bool HasLevelSupport(Vector3 position)
        {
            int hits = RayQuery(position + Vector3.up * _config.GroundProbeDistance,
                Vector3.down, _config.GroundProbeDistance * 2f + _config.SkinWidth, _state.CollisionMask);
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = _state.QueryHits[i];
                if (!IgnoreMotorCollider(hit.collider) && Vector3.Angle(hit.normal, Vector3.up) <= _config.SlopeLimitDegrees) return true;
            }
            return false;
        }
        private void RequestPath(Vector3 target)
        {
            _state.LastTarget = target; _state.PathCooldown = _config.PathRepathSeconds;
            _state.PathAvailable = false;
            bool sampledStart = NavMesh.SamplePosition(Position, out NavMeshHit start, _config.PathSampleRadius, _config.NavigationAreaMask);
            if (_state.RepathActive && sampledStart && _presenter.CanReleaseGap(_state.Steering,
                _state.RepathEntry, _state.RepathExit, Position, start.position)) _state.RepathActive = false;
            if (!_state.RepathActive && _presenter.HasActiveSegmentProgress(_state.Steering, Position, _config.CornerTolerance))
            {
                Vector3 entry = _state.Steering.Corners[_state.Steering.CornerIndex - 1];
                Vector3 exit = _state.Steering.Corners[_state.Steering.CornerIndex];
                if (NavMesh.Raycast(entry, exit, out _, _config.NavigationAreaMask))
                {
                    _state.RepathEntry = entry; _state.RepathExit = exit;
                    _state.RepathActive = true; _state.RepathReverse = false;
                }
            }
            if (sampledStart)
            {
                if (NavMesh.SamplePosition(target, out NavMeshHit end, _config.PathSampleRadius, _config.NavigationAreaMask))
                {
                    if (_state.RepathActive)
                    {
                        _state.PathAvailable = RevalidateGap(end.position);
                        if (!_state.PathAvailable && CanLeaveRejectedGap(start.position)) _state.RepathActive = false;
                    }
                    if (!_state.RepathActive)
                        _state.PathAvailable = NavMesh.CalculatePath(start.position, end.position, _config.NavigationAreaMask, _state.Path) &&
                            _state.Path.status == NavMeshPathStatus.PathComplete;
                }
            }
            if (_state.PathAvailable && !_routePresenter.Allowed(_state.RepathActive ? _state.Steering.Corners : _state.Path.corners, _state.UnavailableRooms))
                _state.PathAvailable = false;
            if (_state.PathAvailable && _state.RepathActive) return;
            _presenter.SetPath(_state.Steering, _state.PathAvailable ? _state.Path.corners : Array.Empty<Vector3>());
            _state.EmergenceCorner = -1;
            if (_state.PathAvailable && _state.EmergenceEnabled)
            {
                Vector3[] corners = _state.Steering.Corners;
                var occluded = new bool[Math.Min(corners.Length, _state.EmergenceBudget)];
                for (int i = 0; i < occluded.Length; i++)
                    occluded[i] = !ClearSegment(_state.EmergenceObserver + Vector3.up * _config.EyeHeight,
                        corners[i] + Vector3.up * _config.EyeHeight, _state.TargetFilter);
                _state.EmergenceCorner = _routePresenter.EmergenceCorner(corners, occluded, true, _state.EmergenceBudget);
            }
        }
        private bool RevalidateGap(Vector3 target)
        {
            float verticalTolerance = _config.GroundProbeDistance + _config.SkinWidth;
            if (!NavMesh.SamplePosition(_state.RepathEntry, out NavMeshHit entry, _config.PathSampleRadius, _config.NavigationAreaMask) ||
                !NavMesh.SamplePosition(_state.RepathExit, out NavMeshHit exit, _config.PathSampleRadius, _config.NavigationAreaMask) ||
                !_presenter.IsNavigationAnchor(_state.RepathEntry, entry.position, verticalTolerance) ||
                !_presenter.IsNavigationAnchor(_state.RepathExit, exit.position, verticalTolerance)) return false;
            Vector3[] forwardTail = ValidatedGapTail(entry.position, exit.position, target);
            Vector3[] reverseTail = ValidatedGapTail(exit.position, entry.position, target);
            return _presenter.TrySetGapPath(_state.Steering, _state.RepathEntry, _state.RepathExit,
                forwardTail, reverseTail, _state.RepathReverse, out _state.RepathReverse);
        }
        private bool CanLeaveRejectedGap(Vector3 sample)
        {
            return NavMesh.SamplePosition(_state.RepathEntry, out NavMeshHit entry, _config.PathSampleRadius, _config.NavigationAreaMask) &&
                _presenter.CanLeaveRejectedGap(Position, sample, _state.RepathEntry, entry.position,
                    !NavMesh.Raycast(sample, entry.position, out _, _config.NavigationAreaMask), _config.GroundProbeDistance + _config.SkinWidth);
        }
        private Vector3[] ValidatedGapTail(Vector3 entry, Vector3 exit, Vector3 target)
        {
            if (NavMesh.CalculatePath(entry, exit, _config.NavigationAreaMask, _state.Path) &&
                _state.Path.status == NavMeshPathStatus.PathComplete &&
                _presenter.IsDirectSegmentPath(_state.Path.corners, entry, exit, _config.GroundProbeDistance + _config.SkinWidth) &&
                NavMesh.CalculatePath(exit, target, _config.NavigationAreaMask, _state.Path) &&
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
            int count = CapsuleQuery(low, high, Mathf.Max(0.001f, _config.Radius - _config.SkinWidth), delta.normalized,
                delta.magnitude + _config.SkinWidth);
            SortHits(count);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _state.QueryHits[i];
                if (IgnoreMotorCollider(hit.collider) || Vector3.Dot(hit.normal, delta.normalized) >= -0.0001f) continue;
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
            int count = RayQuery(origin, Vector3.down, _config.StepHeight + _config.GroundProbeDistance, _state.CollisionMask);
            SortHits(count);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _state.QueryHits[i];
                if (IgnoreMotorCollider(hit.collider)) continue;
                float rise = hit.point.y - position.y;
                return hit.collider == edge && rise > _config.SkinWidth && rise <= _config.StepHeight + _config.SkinWidth &&
                    Vector3.Angle(hit.normal, Vector3.up) <= _config.SlopeLimitDegrees;
            }
            return false;
        }
        private bool Blocked(Vector3 position)
        {
            Capsule(position, out Vector3 low, out Vector3 high);
            int count = CapsuleOverlap(low, high, Mathf.Max(0.001f, _config.Radius - _config.SkinWidth));
            for (int i = 0; i < count; i++)
            {
                Collider other = _state.QueryOverlaps[i];
                if (!IgnoreMotorCollider(other)) return true;
            }
            return false;
        }
        private void Capsule(Vector3 position, out Vector3 low, out Vector3 high)
        { low = position + Vector3.up * _config.Radius; high = position + Vector3.up * (_config.Height - _config.Radius); }
        private bool Own(Collider other) => other == _capsule || other.transform.IsChildOf(transform);
        // Static physics queries do not apply the queried collider's contact exclusions.
        // Player temporarily excludes this body's layer during revival collision grace.
        private bool IgnoreMotorCollider(Collider other) => other == null || Own(other) ||
            (other.excludeLayers.value & (1 << _capsule.gameObject.layer)) != 0 || ForeignHunter(other);
        // Hunters ignore each other's bodies (owner, 2026-10-01). The layer covers spawned
        // prefabs; the parent driver covers bodies off that layer. The target is never skipped.
        private bool ForeignHunter(Collider other) => other != null && !Own(other) &&
            ((_state.HunterBodyLayer >= 0 && other.gameObject.layer == _state.HunterBodyLayer) ||
                other.GetComponentInParent<HunterDriver>() != null) &&
            !(_state.TargetFilter?.Invoke(other) ?? false);
        private int RayQuery(Vector3 origin, Vector3 direction, float distance, int mask)
        {
            int count;
            while ((count = Physics.RaycastNonAlloc(origin, direction, _state.QueryHits, distance, mask, QueryTriggerInteraction.Ignore)) == _state.QueryHits.Length)
                GrowQuery(ref _state.QueryHits);
            return count;
        }
        private int CapsuleQuery(Vector3 low, Vector3 high, float radius, Vector3 direction, float distance)
        {
            int count;
            while ((count = Physics.CapsuleCastNonAlloc(low, high, radius, direction, _state.QueryHits, distance, _state.CollisionMask, QueryTriggerInteraction.Ignore)) == _state.QueryHits.Length)
                GrowQuery(ref _state.QueryHits);
            return count;
        }
        private int CapsuleOverlap(Vector3 low, Vector3 high, float radius)
        {
            int count;
            while ((count = Physics.OverlapCapsuleNonAlloc(low, high, radius, _state.QueryOverlaps, _state.CollisionMask, QueryTriggerInteraction.Ignore)) == _state.QueryOverlaps.Length)
                GrowQuery(ref _state.QueryOverlaps);
            return count;
        }
        private int SphereOverlap(Vector3 origin, float radius)
        {
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(origin, radius, _state.QueryOverlaps, _state.CollisionMask, QueryTriggerInteraction.Ignore)) == _state.QueryOverlaps.Length)
                GrowQuery(ref _state.QueryOverlaps);
            return count;
        }
        private void GrowQuery<T>(ref T[] buffer)
        {
            int previous = buffer.Length;
            T[] larger = ArrayPool<T>.Shared.Rent(checked(previous * 2));
            ArrayPool<T>.Shared.Return(buffer, true); buffer = larger;
            Debug.LogWarning($"Hunter physics query buffer saturated; grew from {previous} to {buffer.Length} and retrying.", this);
        }
        private void SortHits(int count)
        {
            // Only the populated prefix participates; ties never depend on PhysX encounter order.
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
            if (_state != null && _state.KinematicReplay)
            {
                for (int i = 0; i < _state.ReplayRenderers.Length; i++)
                    if (_state.ReplayRenderers[i] != null) _state.ReplayRenderers[i].forceRenderingOff = _state.ReplayRenderingOff[i];
                for (int i = 0; i < _state.ReplayColliders.Length; i++)
                    if (_state.ReplayColliders[i] != null) _state.ReplayColliders[i].enabled = _state.ReplayColliderEnabled[i];
                for (int i = 0; i < _state.ReplayAgents.Length; i++)
                    if (_state.ReplayAgents[i] != null) _state.ReplayAgents[i].enabled = _state.ReplayAgentEnabled[i];
            }
            if (_stare != null) _stare.Teardown();
            if (_weaver != null) _weaver.Teardown();
            if (_state != null && _state.IKDriver != null)
            {
                _state.IKDriver.Unbind();
                if (_state.OwnIKDriver)
                {
                    _state.IKDriver.enabled = false;
                    if (Application.isPlaying) Destroy(_state.IKDriver); else DestroyImmediate(_state.IKDriver);
                }
            }
            if (_animation != null) _animation.Teardown();
            if (_attacks != null) _attacks.Teardown();
            if (_state != null)
            {
                // A driver torn down before Initialize rented its buffers has nothing to return.
                if (_state.QueryHits != null) ArrayPool<RaycastHit>.Shared.Return(_state.QueryHits, true);
                if (_state.QueryOverlaps != null) ArrayPool<Collider>.Shared.Return(_state.QueryOverlaps, true);
                _state.QueryHits = null; _state.QueryOverlaps = null;
            }
            _state = null;
        }
    }
}
