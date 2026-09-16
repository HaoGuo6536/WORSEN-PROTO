// ============================================================================
// FloorDriver.cs
// ============================================================================
// PURPOSE:
//   Builds and operates Floor-owned pickups, exits, warning lights and physical room closure.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Instance the configured cake slice visual independently of collection triggers.
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Scene-owned; no global engine settings. Only FloorManager commands this Driver. Its own sub-drivers own trigger callbacks; navigation paths must be complete before cue publication. No fallback anchor generation.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    [DisallowMultipleComponent]
    public sealed class FloorDriver : MonoBehaviour
    {
        [SerializeField] private FloorDriverConfig _config;
        private readonly FloorDriverState _state = new FloorDriverState();
        private readonly FloorPresenter _presenter = new FloorPresenter();
        public event Action<Collider, int, PickupKind> PickupContact;
        public event Action<Collider, int> LethalContact;
        public event Action<Collider> ExitContact;
        public int OwnedPickupCount => _state.Pickups.Count;
        public int OwnedRoomCount => _state.Rooms.Count;

        public void Initialize(LevelGraph graph, IReadOnlyList<LevelAnchor> anchors)
        {
            Teardown();
            if (_config == null) _config = Resources.Load<FloorDriverConfig>("ScriptableObjects/Domain/Floor/FloorDriverConfig");
            if (_config == null) throw new InvalidOperationException("Build Floor assets before initialization.");
            _state.Root = new GameObject("Generated Floor Runtime");
            _state.Root.SetActive(false); _state.Root.transform.SetParent(transform, false);
            _state.CakeMaterial = MakeMaterial(_config.CakeColor);
            _state.GoldenMaterial = MakeMaterial(_config.GoldenColor);
            _state.BlockerMaterial = MakeMaterial(_config.ClosedColor);
            _state.ExitMaterial = MakeMaterial(_config.ExitLockedColor);
            foreach (var room in graph.Rooms) BuildRoom(room);
            foreach (var anchor in anchors) BuildPickup(anchor, PickupKind.Cake);
            BuildExit(graph.ExitPosition);
            _state.Ready = true;
            _state.Root.SetActive(true);
            if (isActiveAndEnabled) OnEnable();
        }

        public void RemovePickup(int anchorId, PickupKind kind)
        {
            foreach (var pickup in _state.Pickups)
                if (pickup != null && pickup.AnchorId == anchorId && pickup.Kind == kind) pickup.gameObject.SetActive(false);
        }

        public void OpenExit(IReadOnlyList<LevelAnchor> anchors)
        {
            _state.ExitMaterial.color = _config.ExitOpenColor;
            OnDisable();
            foreach (var anchor in anchors) BuildPickup(anchor, PickupKind.GoldenCake);
            if (isActiveAndEnabled) OnEnable();
        }

        public void ApplyRoomPhase(int roomId, RoomPhase phase)
        {
            if (_state.Rooms.TryGetValue(roomId, out var room)) room.ApplyPhase(phase, _config.WarningColor, _config.ClosedColor);
        }

        public void TickWarnings(float elapsed)
        {
            float intensity = _presenter.WarningIntensity(elapsed, _config.WarningPulsePeriod, _config.WarningIntensity);
            foreach (var room in _state.Rooms.Values) room.SetWarningIntensity(intensity);
        }

        public FloorPathCandidate QueryPath(int anchorId, Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from, out var start, _config.PathSampleRadius, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(to, out var end, _config.PathSampleRadius, NavMesh.AllAreas))
                return new FloorPathCandidate(anchorId, float.PositiveInfinity, Vector3.zero);
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                return new FloorPathCandidate(anchorId, float.PositiveInfinity, Vector3.zero);
            var corners = path.corners;
            float length = _presenter.PathLength(corners);
            return new FloorPathCandidate(anchorId, length, _presenter.FirstDirection(from, corners));
        }

        public float PathLength(Vector3 from, Vector3 to) => QueryPath(0, from, to).Length;

        public void Teardown()
        {
            OnDisable();
            if (_state.Root != null) { _state.Root.SetActive(false); Release(_state.Root); }
            foreach (var material in _state.Materials) if (material != null) Release(material);
            _state.Pickups.Clear(); _state.Rooms.Clear(); _state.Materials.Clear();
            _state.Root = null; _state.Exit = null; _state.Ready = false;
        }

        private void OnEnable()
        {
            if (!_state.Ready || _state.Subscribed) return;
            foreach (var pickup in _state.Pickups) pickup.Contact += HandlePickup;
            foreach (var room in _state.Rooms.Values) room.LethalContact += HandleLethal;
            _state.Exit.Contact += HandleExit;
            _state.Subscribed = true;
        }
        private void OnDisable()
        {
            if (!_state.Subscribed) return;
            foreach (var pickup in _state.Pickups) if (pickup != null) pickup.Contact -= HandlePickup;
            foreach (var room in _state.Rooms.Values) if (room != null) room.LethalContact -= HandleLethal;
            if (_state.Exit != null) _state.Exit.Contact -= HandleExit;
            _state.Subscribed = false;
        }
        private void OnDestroy() => Teardown();
        private void HandlePickup(Collider other, int anchor, PickupKind kind) => PickupContact?.Invoke(other, anchor, kind);
        private void HandleLethal(Collider other, int room) => LethalContact?.Invoke(other, room);
        private void HandleExit(Collider other) => ExitContact?.Invoke(other);

        private void BuildPickup(LevelAnchor anchor, PickupKind kind)
        {
            var item = new GameObject();
            item.name = kind + " " + anchor.Id;
            item.transform.SetParent(_state.Root.transform, false);
            item.transform.position = anchor.Position + Vector3.up * _config.PickupHeight;
            var trigger = item.AddComponent<SphereCollider>();
            trigger.radius = _config.PickupRadius;
            trigger.isTrigger = true;
            if (_config.CakePrefab != null)
            {
                var visual = Instantiate(_config.CakePrefab, item.transform, false);
                visual.name = "Cake Slice";
                if (kind == PickupKind.GoldenCake)
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = _state.GoldenMaterial;
            }
            else
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.transform.SetParent(item.transform, false);
                visual.transform.localScale = Vector3.one * (_config.PickupRadius * 2f);
                visual.GetComponent<Renderer>().sharedMaterial = kind == PickupKind.Cake ? _state.CakeMaterial : _state.GoldenMaterial;
                Release(visual.GetComponent<Collider>());
            }
            var pickup = item.AddComponent<CakePickup>(); pickup.Configure(anchor.Id, kind); _state.Pickups.Add(pickup);
        }
        private void BuildExit(Vector3 position)
        {
            var root = new GameObject("Walk-in Exit"); root.transform.SetParent(_state.Root.transform, false);
            root.transform.position = position + Vector3.up * (_config.ExitSize.y * 0.5f);
            var box = root.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = _config.ExitSize;
            _state.Exit = root.AddComponent<FloorExitVolume>();
            var light = root.AddComponent<Light>(); light.type = LightType.Point;
            light.color = _config.ExitOpenColor; light.range = _config.ExitSize.magnitude; light.intensity = _config.WarningIntensity;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube); marker.name = "Exit Marker";
            marker.transform.SetParent(root.transform, false); marker.transform.localPosition = Vector3.up * (_config.ExitSize.y * 0.5f);
            marker.transform.localScale = new Vector3(_config.ExitSize.x, _config.BlockerThickness, _config.BlockerThickness);
            marker.GetComponent<Renderer>().sharedMaterial = _state.ExitMaterial; Release(marker.GetComponent<Collider>());
        }
        private void BuildRoom(LevelRoom room)
        {
            var root = new GameObject("Collapse Room " + room.Id); root.transform.SetParent(_state.Root.transform, false);
            root.transform.position = room.Center;
            var roomVolume = root.AddComponent<RoomCollapseVolume>();
            var blockers = new List<GameObject>();
            foreach (var bounds in _presenter.BoundaryBlockers(room.Bounds, _config.BlockerThickness))
            {
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.name = "Closure Door Blocker";
                blocker.transform.SetParent(root.transform, false); blocker.transform.position = bounds.center; blocker.transform.localScale = bounds.size;
                blocker.GetComponent<Renderer>().sharedMaterial = _state.BlockerMaterial; blockers.Add(blocker);
            }
            var warning = root.AddComponent<Light>(); warning.type = LightType.Point;
            warning.range = room.Size.magnitude; warning.intensity = _config.WarningIntensity;
            roomVolume.Configure(room.Id, room.Size, blockers.ToArray(), warning); _state.Rooms.Add(room.Id, roomVolume);
        }
        private Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Floor requires a lit material shader.");
            var material = new Material(shader) { color = color }; _state.Materials.Add(material); return material;
        }
        private static void Release(UnityEngine.Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
