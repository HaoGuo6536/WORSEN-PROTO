// ============================================================================
// HunterAttackDriver.cs
// ============================================================================
// PURPOSE:
//   Applies the named Hunter engine interaction from explicit owner commands.
//   Unity physics, animation or rendering remains at this engine boundary.
//   Contacts and observable feedback return to the Manager through typed events.
// ARCHITECTURAL ROLE:
//   Sub-driver (section 7e), owned by HunterDriver - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Scene-owned, no independent simulation loop. Teardown destroys only owned transient effects.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterAttackDriver : MonoBehaviour
    {
        [SerializeField] private HunterAttackDriverConfig _config;
        private HunterAttackDriverState _state;
        private readonly HunterAttackPresenter _presenter = new HunterAttackPresenter();
        public event Action<Collider, int> OnContact;
        public event Action<int> OnMiss;
        public event Action<HunterFeedbackEvent> OnFeedback;
        public void Initialize() { Teardown(); _state = new HunterAttackDriverState(); }
        public void ConfigureFeedback(EntityId hunter, string archetypeKey)
        { if (_state != null) { _state.Hunter = hunter; _state.ArchetypeKey = archetypeKey; } }
        private void Feedback(HunterFeedbackKind kind, Vector3 position, int serial, int emitterId = 0)
        { OnFeedback?.Invoke(new HunterFeedbackEvent(_state.Hunter, _state.ArchetypeKey, kind, position, _state.Tick, serial, emitterId)); }
        public void SetTargetFilter(Func<Collider, bool> isTarget) { if (_state != null) _state.IsTarget = isTarget; }
        public void BeginWarning(HunterAttackStyle style, int serial, Vector3 target, float range, float radius, bool split, bool ring)
        {
            if (_state == null) return;
            ClearWarnings();
            _state.Style = style; _state.Serial = serial; _state.Origin = transform.position + Vector3.up * 1.1f;
            _state.Target = target + Vector3.up * (style == HunterAttackStyle.Projectile ? 0.9f : 0f);
            _state.Range = range; _state.Radius = radius; _state.Split = split;
            _state.GroundPoints.Clear();
            if (style == HunterAttackStyle.Projectile)
            {
                foreach (Vector3 direction in _presenter.Directions(_state.Origin, _state.Target, split))
                {
                    float length = range;
                    if (FirstHit(_state.Origin, direction, range, 0f, out RaycastHit hit)) length = hit.distance;
                    for (float distance = 0.2f; distance < length; distance += 0.85f)
                        Line(new[] { _state.Origin + direction * distance, _state.Origin + direction * Mathf.Min(length, distance + 0.4f) }, false);
                }
            }
            else if (style == HunterAttackStyle.GroundSpikes)
            {
                foreach (Vector3 point in _presenter.GroundPoints(target, radius, ring))
                    if (GroundPoint(point, out Vector3 ground))
                    {
                        _state.GroundPoints.Add(ground);
                        Feedback(HunterFeedbackKind.SpikeWarning, ground, serial, serial * 16 + _state.GroundPoints.Count);
                        var circle = new Vector3[32];
                        for (int i = 0; i < circle.Length; i++) circle[i] = ground + Vector3.up * 0.06f + Quaternion.Euler(0, i * 360f / circle.Length, 0) * Vector3.forward * radius;
                        Line(circle, true);
                    }
            }
        }
        public void Fire(float projectileSpeed, float projectileRadius)
        {
            if (_state == null) return;
            ClearWarnings();
            if (_state.Style == HunterAttackStyle.Projectile)
            {
                foreach (Vector3 direction in _presenter.Directions(_state.Origin, _state.Target, _state.Split))
                {
                    if (_state.Projectiles.Count >= 12) break;
                    GameObject visual = Visual(_config != null ? _config.ProjectilePrefab : null, PrimitiveType.Sphere,
                        _state.Origin, Vector3.one * projectileRadius * 2f, _config != null ? _config.ProjectileMaterial : null);
                    int emitterId = ++_state.NextEmitterId;
                    Feedback(HunterFeedbackKind.ProjectileLaunched, _state.Origin, _state.Serial, emitterId);
                    Feedback(HunterFeedbackKind.ProjectileTravel, _state.Origin, _state.Serial, emitterId);
                    _state.Projectiles.Add(new HunterProjectileDriverState { Visual = visual, Position = _state.Origin, Direction = direction,
                        EmitterId = emitterId, Radius = projectileRadius, Speed = projectileSpeed, Remaining = _state.Range, Serial = _state.Serial });
                }
            }
            else if (_state.Style == HunterAttackStyle.GroundSpikes)
            {
                foreach (Vector3 point in _state.GroundPoints)
                {
                    // Recheck the room still exists; collapsed floors cannot host a floating hit.
                    if (!GroundPoint(point, out Vector3 ground) || Mathf.Abs(point.y - ground.y) > 0.2f) continue;
                    Feedback(HunterFeedbackKind.SpikeErupt, ground, _state.Serial, _state.Serial * 16 + _state.GroundPoints.IndexOf(point) + 1);
                    foreach (Collider collider in Physics.OverlapCapsule(ground + Vector3.up * 0.2f, ground + Vector3.up * 1.2f,
                        _state.Radius, Mask, QueryTriggerInteraction.Ignore))
                        if (!Own(collider) && Mathf.Abs(collider.bounds.min.y - ground.y) < 1.5f &&
                            Clear(ground + Vector3.up * 0.3f, collider.ClosestPoint(ground + Vector3.up * 0.7f), collider))
                        { OnContact?.Invoke(collider, _state.Serial); }
                    _state.Spikes.Add(Visual(_config != null ? _config.SpikePrefab : null, PrimitiveType.Cylinder,
                        ground + Vector3.up * 0.65f, new Vector3(_state.Radius * 0.4f, 0.65f, _state.Radius * 0.4f),
                        _config != null ? _config.SpikeMaterial : null));
                }
                _state.SpikeSeconds = 0.65f;
                OnMiss?.Invoke(_state.Serial);
            }
        }
        public void Tick(float dt, long tick = 0)
        {
            if (_state == null || !(dt > 0f)) return;
            _state.Tick = tick;
            for (int i = _state.Projectiles.Count - 1; i >= 0; i--)
            {
                HunterProjectileDriverState bolt = _state.Projectiles[i];
                float step = Mathf.Min(bolt.Remaining, bolt.Speed * dt);
                bool collided = FirstHit(bolt.Position, bolt.Direction, step, bolt.Radius, out RaycastHit hit);
                if (collided)
                { Feedback(HunterFeedbackKind.ProjectileImpact, hit.point, bolt.Serial, bolt.EmitterId); OnContact?.Invoke(hit.collider, bolt.Serial); }
                bolt.Position += bolt.Direction * (collided ? hit.distance : step); bolt.Remaining -= step;
                if (!collided) Feedback(HunterFeedbackKind.ProjectileMoved, bolt.Position, bolt.Serial, bolt.EmitterId);
                if (bolt.Visual != null) bolt.Visual.transform.position = bolt.Position;
                if (!collided && bolt.Remaining > 0f) continue;
                if (!collided) Feedback(HunterFeedbackKind.ProjectileExpired, bolt.Position, bolt.Serial, bolt.EmitterId);
                if (bolt.Visual != null) Release(bolt.Visual);
                _state.Projectiles.RemoveAt(i);
                bool remaining = _state.Projectiles.Exists(other => other.Serial == bolt.Serial);
                if (!remaining) OnMiss?.Invoke(bolt.Serial);
            }
            _state.SpikeSeconds -= dt;
            if (_state.SpikeSeconds <= 0f && _state.Spikes.Count > 0)
            { foreach (GameObject spike in _state.Spikes) if (spike != null) Release(spike); _state.Spikes.Clear(); }
        }
        private int Mask => _config != null ? _config.CollisionMask : ~0;
        private bool Own(Collider collider) => collider.transform.IsChildOf(transform);
        private bool FirstHit(Vector3 origin, Vector3 direction, float distance, float radius, out RaycastHit closest)
        {
            RaycastHit[] hits = radius > 0f ? Physics.SphereCastAll(origin, radius, direction, distance, Mask, QueryTriggerInteraction.Ignore) :
                Physics.RaycastAll(origin, direction, distance, Mask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits) if (!Own(hit.collider)) { closest = hit; return true; }
            closest = default; return false;
        }
        private bool Clear(Vector3 origin, Vector3 target, Collider permitted = null)
        {
            Vector3 delta = target - origin;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, Mathf.Max(0f, delta.magnitude - 0.05f), Mask, QueryTriggerInteraction.Ignore))
                if (!Own(hit.collider) && hit.collider != permitted && (_state.IsTarget == null || !_state.IsTarget(hit.collider))) return false;
            return true;
        }
        private bool GroundPoint(Vector3 point, out Vector3 ground)
        {
            ground = default;
            if (Vector3.Distance(_state.Origin, point) > _state.Range + 2f || Mathf.Abs(point.y - transform.position.y) > 1.2f) return false;
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 0.6f, Vector3.down, 1.5f, Mask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (Own(hit.collider) || (_state.IsTarget != null && _state.IsTarget(hit.collider))) continue;
                if (hit.normal.y < 0.7f || !Clear(_state.Origin, hit.point + Vector3.up * 0.2f)) return false;
                ground = hit.point; return true;
            }
            return false;
        }
        private void Line(Vector3[] points, bool loop)
        {
            var go = new GameObject("Hunter attack warning");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _config != null && _config.WarningMaterial != null ? _config.WarningMaterial : Fallback();
            line.positionCount = points.Length; line.SetPositions(points); line.loop = loop;
            line.startWidth = line.endWidth = 0.055f;
            line.startColor = line.endColor = _config != null ? _config.WarningColor : Color.magenta;
            _state.Warnings.Add(line);
        }
        private Material Fallback()
        {
            if (_state.FallbackMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader != null) _state.FallbackMaterial = new Material(shader) { color = Color.magenta };
            }
            return _state.FallbackMaterial;
        }
        private GameObject Visual(GameObject prefab, PrimitiveType fallback, Vector3 point, Vector3 scale, Material material)
        {
            GameObject go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(fallback);
            go.name = "Hunter attack visual"; go.transform.position = point;
            if (prefab == null) go.transform.localScale = scale;
            foreach (Collider collider in go.GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (prefab == null) go.GetComponent<Renderer>().sharedMaterial = material != null ? material : Fallback();
            return go;
        }
        private void ClearWarnings()
        { foreach (LineRenderer line in _state.Warnings) if (line != null) Release(line.gameObject); _state.Warnings.Clear(); }
        public void Teardown()
        {
            if (_state == null) return;
            ClearWarnings();
            foreach (HunterProjectileDriverState bolt in _state.Projectiles)
            {
                Feedback(HunterFeedbackKind.ProjectileExpired, bolt.Position, bolt.Serial, bolt.EmitterId);
                if (bolt.Visual != null) Release(bolt.Visual);
            }
            foreach (GameObject spike in _state.Spikes) if (spike != null) Release(spike);
            if (_state.FallbackMaterial != null) Release(_state.FallbackMaterial);
            _state = null;
        }
        private static void Release(UnityEngine.Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDestroy() { Teardown(); }
    }
}
