// ============================================================================
// FloorDriver.cs
// ============================================================================
// PURPOSE:
//   Builds and operates Floor-owned pickups, exits, warning lights and staged room destruction.
//   It samples navigation paths for guidance while the pure Presenter computes
//   sampled-origin directions, straight-line fallbacks and retained directions.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Keep player guidance on Walkable areas, never Weaver-only partition links.
//   - Register optional gold sockets and avoid duplicate spawns when collapse creates ordinary gold.
//   - Push cosmetic hand look tags into owned room visuals without changing probes.
//   - Build tiered candle-lit cakes independently of unchanged pickup triggers; retain authored art overrides.
//   - Tint owned golden material copies while retaining authored cake textures and alpha.
//   - Own Lumen glow layers and a candle point light; warnings and exit remain fake-light only.
//   - Relay trap contacts and apply explicit visual/audio commands from the Manager.
//   - Support staged collapse and an opt-in hinged exit that requires a real crossing.
//   - Relay locked-door overlaps/departures and present bails without spawning Golden Cakes.
//   - Report physical opening progress; the legacy marker changes immediately.
//   - Sample dedicated fog triggers and reach for rewards; Controller owns completed-room losses.
//   - Place room warnings on occupied cells and restrict cake reach to footprint membership.
//   - Supply complete path corners and expose target-local fallback/held flags.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - FloorPresenter, DriverState, DriverConfig and owned sub-drivers; Unity navigation APIs.
// USAGE NOTES:
//   Scene-owned; no global engine settings. Only FloorManager commands this Driver.
//   Its sub-drivers own trigger callbacks; failed paths use flagged guidance, not fallback anchors.
//   PathLength remains a complete-path-only query and does not mutate guidance history.
//   Manager-injected identity resolution lets the physical door report destroyed-collider departures.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    [DisallowMultipleComponent]
    public sealed class FloorDriver : MonoBehaviour
    {
        [SerializeField] private FloorDriverConfig _config;
        private readonly FloorDriverState _state = new FloorDriverState();
        private readonly FloorPresenter _presenter = new FloorPresenter();
        public event Action<Collider, int, PickupKind> PickupContact;
        public event Action<Collider, int> TrapContact;

        public event Action<Collider> ExitContact;
        public event Action<EntityId> ExitDeparted;
        public int OwnedPickupCount => _state.Pickups.Count;
        public int OwnedRoomCount => _state.Rooms.Count;
        public float OpeningProgress(bool open) => _state.ExitDoor != null ? _state.ExitDoor.OpeningProgress : open ? 1f : 0f;

        public void Initialize(LevelGraph graph, IReadOnlyList<LevelAnchor> anchors, Func<Collider, EntityId> resolveIdentity = null,
            float boundaryReach = 0f, IReadOnlyList<FloorTrapSpawn> traps = null)
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
            foreach (var room in graph.Rooms)
            {
                int cakes = 0;
                foreach (var anchor in anchors) if (anchor.RoomId == room.Id) cakes++;
                BuildRoom(room, resolveIdentity, boundaryReach, cakes);
            }
            foreach (var anchor in anchors) { _state.Anchors.Add(anchor.Id, anchor); BuildPickup(anchor, PickupKind.Cake); }
            if (traps != null && traps.Count > 0)
            {
                var samples = new FloorCakePresenter().TickSamples(22050, _config.TrapTickDuration, _config.TrapTickFrequency);
                _state.TrapTickClip = AudioClip.Create("Floor Blinder Tick", samples.Length, 1, 22050, false);
                _state.TrapTickClip.SetData(samples, 0);
                foreach (var trap in traps)
                { _state.Anchors.Add(trap.Anchor.Id, trap.Anchor); BuildPickup(trap.Anchor, PickupKind.Cake, true, trap.Kind == FloorTrapKind.Blind); }
            }
            BuildExit(graph.ExitPosition, resolveIdentity);
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
            if (_state.ExitGlow != null) _state.ExitGlow.SetAppearance(_config.ExitOpenColor, _config.WarningIntensity);
            if (_state.ExitDoor != null) _state.ExitDoor.Open();
            SpawnGoldenCakes(anchors);
        }

        public void SpawnGoldenCakes(IReadOnlyList<LevelAnchor> anchors)
        {
            OnDisable();
            foreach (var anchor in anchors)
            {
                if (_state.Pickups.Exists(p => p != null && p.AnchorId == anchor.Id && p.Kind == PickupKind.GoldenCake)) continue;
                _state.Anchors[anchor.Id] = anchor;
                BuildPickup(anchor, PickupKind.GoldenCake);
            }
            if (isActiveAndEnabled) OnEnable();
        }

        public void SetHandLook(string look)
        { foreach (var room in _state.Rooms.Values) room.SetHandLook(look); }

        public void RemoveTrap(int id) { if (_state.Traps.TryGetValue(id, out var trap)) trap.gameObject.SetActive(false); }
        public void PlayTrapTick(int id, float volume) { if (_state.Traps.TryGetValue(id, out var trap)) trap.PlayTick(volume); }
        public void TickCakeVisuals(float elapsed)
        { foreach (var visual in _state.CakeVisuals) if (visual != null && visual.gameObject.activeInHierarchy) visual.Tick(elapsed); }

        public void RefreshExitContacts()
        {
            if (_state.ExitDoor != null) _state.ExitDoor.RefreshContacts();
            if (_state.Exit != null) _state.Exit.RefreshContacts();
        }
        public void RefreshHandContacts()
        {
            Physics.SyncTransforms();
            foreach (var room in _state.Rooms.Values) room.RefreshContacts();
        }
        public void PresentBail() { if (_state.ExitDoor != null) _state.ExitDoor.PresentBail(); }

        public void ApplyRoomPhase(int roomId, RoomPhase phase)
        {
            if (_state.Rooms.TryGetValue(roomId, out var room)) room.ApplyPhase(phase, _config.WarningColor, _config.ClosedColor);
        }

        public void TickWarnings(float elapsed)
        {
            if (_state.ExitDoor != null) _state.ExitDoor.Tick(elapsed);

        }

        public void ApplyDestruction(RoomDestructionSample sample, float elapsed)
        {
            if (!_state.Rooms.TryGetValue(sample.RoomId, out var room)) return;
            var cakes = new List<Vector3>();
            foreach (var pickup in _state.Pickups)
                if (pickup != null && pickup.gameObject.activeSelf && _state.Anchors.TryGetValue(pickup.AnchorId, out var anchor) &&
                    anchor.RoomId == sample.RoomId && room.ContainsXZ(anchor.Position)) cakes.Add(pickup.transform.position);
            room.ApplyDestruction(sample, elapsed, cakes);
        }
        public void ApplyHandFact(CollapseHandFact fact)
        { if (_state.Rooms.TryGetValue(fact.RoomId, out var room)) room.ApplyHandFact(fact); }
        public void PreviewCracks(int roomId)
        { if (_state.Rooms.TryGetValue(roomId, out var room)) room.PreviewCracks(); }
        public bool PickupAvailable(int anchorId)
        {
            return _state.Anchors.TryGetValue(anchorId, out var anchor) &&
                _state.Rooms.TryGetValue(anchor.RoomId, out var room) && !room.PickupOvertaken(anchor.Position);
        }
        public FloorHandProbe QueryHand(Vector3 playerPosition, int preferredRoom = 0, int preferredHand = -1,
            EntityId playerId = default, bool closedOnly = false)
        {
            if (preferredHand >= 0)
                return _state.Rooms.TryGetValue(preferredRoom, out var preferred) ? preferred.Probe(playerPosition, preferredHand, playerId) : default;
            FloorHandProbe closest = default;
            foreach (var room in _state.Rooms.Values)
            {
                if (closedOnly && room.Phase != RoomPhase.Closed) continue;
                var probe = room.Probe(playerPosition, -1, playerId);
                if (probe.Available && (!closest.Available || probe.Distance < closest.Distance ||
                    probe.Distance == closest.Distance && probe.RoomId < closest.RoomId)) closest = probe;
            }
            return closest;
        }

        public FloorPathCandidate QueryPath(int anchorId, Vector3 from, Vector3 to)
        {
            var corners = QueryCorners(from, to, out var sampledStart);
            return _presenter.PathCandidate(_state, anchorId, from, sampledStart, to, corners,
                _config.DirectionCornerSkipDistance);
        }

        public bool IsDirectionFallback(int anchorId) => _state.FallbackDirections.Contains(anchorId);
        public bool IsDirectionHeld(int anchorId) => _state.HeldDirections.Contains(anchorId);
        public float PathLength(Vector3 from, Vector3 to) => _presenter.PathLength(QueryCorners(from, to, out _));

        private Vector3[] QueryCorners(Vector3 from, Vector3 to, out Vector3 sampledStart)
        {
            sampledStart = from;
            if (!NavMesh.SamplePosition(from, out var start, _config.PathSampleRadius, 1) ||
                !NavMesh.SamplePosition(to, out var end, _config.PathSampleRadius, 1))
                return null;
            sampledStart = start.position;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, 1, path) || path.status != NavMeshPathStatus.PathComplete)
                return null;
            return path.corners;
        }

        public void Teardown()
        {
            OnDisable();
            if (_state.Root != null) { _state.Root.SetActive(false); Release(_state.Root); }
            foreach (var material in _state.Materials) if (material != null) Release(material);
            if (_state.TrapTickClip != null) Release(_state.TrapTickClip);
            _state.TrapTickClip = null; _state.Traps.Clear(); _state.CakeVisuals.Clear();
            _state.Pickups.Clear(); _state.Rooms.Clear(); _state.Materials.Clear(); _state.Anchors.Clear();
            _state.LastGoodDirections.Clear(); _state.FallbackDirections.Clear(); _state.HeldDirections.Clear();
            _state.Root = null; _state.Exit = null; _state.ExitDoor = null; _state.ExitGlow = null; _state.Ready = false;
        }

        private void OnEnable()
        {
            if (!_state.Ready || _state.Subscribed) return;
            foreach (var pickup in _state.Pickups) pickup.Contact += HandlePickup;
            foreach (var trap in _state.Traps.Values) trap.Contact += HandleTrap;

            if (_state.Exit != null) { _state.Exit.Contact += HandleExit; _state.Exit.Departed += HandleDeparture; }
            if (_state.ExitDoor != null)
            { _state.ExitDoor.Contact += HandleExit; _state.ExitDoor.Departed += HandleDeparture; }
            _state.Subscribed = true;
        }
        private void OnDisable()
        {
            if (!_state.Subscribed) return;
            foreach (var pickup in _state.Pickups) if (pickup != null) pickup.Contact -= HandlePickup;
            foreach (var trap in _state.Traps.Values) if (trap != null) trap.Contact -= HandleTrap;

            if (_state.Exit != null) { _state.Exit.Contact -= HandleExit; _state.Exit.Departed -= HandleDeparture; }
            if (_state.ExitDoor != null)
            { _state.ExitDoor.Contact -= HandleExit; _state.ExitDoor.Departed -= HandleDeparture; }
            _state.Subscribed = false;
        }
        private void OnDestroy() => Teardown();
        private void HandlePickup(Collider other, int anchor, PickupKind kind) => PickupContact?.Invoke(other, anchor, kind);
        private void HandleTrap(Collider other, int anchor) => TrapContact?.Invoke(other, anchor);

        private void HandleExit(Collider other) => ExitContact?.Invoke(other);
        private void HandleDeparture(EntityId id) => ExitDeparted?.Invoke(id);

        private void BuildPickup(LevelAnchor anchor, PickupKind kind, bool trap = false, bool ticks = false)
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
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            var source = materials[i];
                            if (source == null) continue;
                            // Cache one Floor-owned copy per source material. A
                            // plain opaque gold material loses the cake's palette
                            // and can fill transparent decoration into solid quads.
                            string ownedName = "Floor Golden Cake " + source.GetInstanceID();
                            var golden = _state.Materials.Find(candidate => candidate != null && candidate.name == ownedName);
                            if (golden == null)
                            {
                                golden = new Material(source) { name = ownedName };
                                foreach (string property in new[] { "_BaseColor", "_Color", "_EmissionColor" })
                                    if (golden.HasProperty(property))
                                    {
                                        var color = golden.GetColor(property);
                                        golden.SetColor(property, new Color(color.r * _config.GoldenColor.r,
                                            color.g * _config.GoldenColor.g, color.b * _config.GoldenColor.b, color.a));
                                    }
                                _state.Materials.Add(golden);
                            }
                            materials[i] = golden;
                        }
                        renderer.sharedMaterials = materials;
                    }
            }
            else
            {
                var visualRoot = new GameObject("Baked Cake"); visualRoot.transform.SetParent(item.transform, false);
                var visual = visualRoot.AddComponent<FloorCakeVisual>();
                var flame = MakeMaterial(_config.CandleColor);
                flame.EnableKeyword("_EMISSION"); flame.SetColor("_EmissionColor", _config.CandleColor * 2f);
                visual.Configure(_config, kind == PickupKind.Cake ? _state.CakeMaterial : _state.GoldenMaterial,
                    MakeMaterial(kind == PickupKind.Cake ? _config.FrostingColor : _config.GoldenColor), _state.CakeMaterial, flame, trap);
                _state.CakeVisuals.Add(visual);
            }
            var glowColor = kind == PickupKind.GoldenCake ? _config.GoldenColor : _config.FrostingColor;
            BuildCakeGlow(item.transform, "Inner Cake Glow", Vector3.zero, _config.CakeGlowRadius, glowColor);
            BuildCakeGlow(item.transform, "Outer Cake Glow", Vector3.up * 0.15f, _config.CakeGlowRadius * 1.5f, glowColor);
            BuildCakeGlow(item.transform, "Cake Light Pool", Vector3.down * (_config.PickupHeight - 0.03f), _config.CakePoolRadius, glowColor);
            if (trap)
            {
                item.name = "Cake Trap " + anchor.Id;
                var contact = item.AddComponent<FloorCakeTrap>();
                contact.Configure(anchor.Id, ticks ? _state.TrapTickClip : null); _state.Traps.Add(anchor.Id, contact);
            }
            else
            { var pickup = item.AddComponent<CakePickup>(); pickup.Configure(anchor.Id, kind); _state.Pickups.Add(pickup); }
        }
        private void BuildCakeGlow(Transform parent, string label, Vector3 position, float radius, Color color)
        {
            var root = new GameObject(label); root.transform.SetParent(parent, false); root.transform.localPosition = position;
            root.AddComponent<FloorLumenGlow>().Configure(_config.LumenExitGlowPrefab, radius, color, _config.CakeGlowBrightness, true);
        }
        private void BuildExit(Vector3 position, Func<Collider, EntityId> resolveIdentity)
        {
            if (_config.UsePhysicalExitDoor)
            {
                var doorRoot = new GameObject("The Final Door");
                doorRoot.transform.SetParent(_state.Root.transform, false); doorRoot.transform.position = position;
                doorRoot.transform.rotation = Quaternion.Euler(0f, _config.ExitDoorYaw, 0f);
                _state.ExitDoor = doorRoot.AddComponent<FloorExitDoor>();
                var wood = _config.ExitDoorMaterial != null ? _config.ExitDoorMaterial : MakeMaterial(new Color(0.105f, 0.045f, 0.025f));
                var stone = MakeMaterial(new Color(0.18f, 0.19f, 0.22f));
                _state.ExitDoor.Configure(_config, wood, stone, _state.ExitMaterial, resolveIdentity);
                return;
            }
            var root = new GameObject("Walk-in Exit"); root.transform.SetParent(_state.Root.transform, false);
            root.transform.position = position + Vector3.up * (_config.ExitSize.y * 0.5f);
            var box = root.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = _config.ExitSize;
            _state.Exit = root.AddComponent<FloorExitVolume>();
            _state.Exit.Configure(resolveIdentity);
            _state.ExitGlow = root.AddComponent<FloorLumenGlow>();
            _state.ExitGlow.Configure(_config.LumenExitGlowPrefab, _config.ExitSize.magnitude,
                _config.ExitLockedColor, 0.3f, true);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube); marker.name = "Exit Marker";
            marker.transform.SetParent(root.transform, false); marker.transform.localPosition = Vector3.up * (_config.ExitSize.y * 0.5f);
            marker.transform.localScale = new Vector3(_config.ExitSize.x, _config.BlockerThickness, _config.BlockerThickness);
            marker.GetComponent<Renderer>().sharedMaterial = _state.ExitMaterial; Release(marker.GetComponent<Collider>());
        }
        private void BuildRoom(LevelRoom room, Func<Collider, EntityId> resolveIdentity, float boundaryReach, int cakes)
        {
            var root = new GameObject("Collapse Room " + room.Id); root.transform.SetParent(_state.Root.transform, false);
            root.transform.position = room.Center;
            var roomVolume = root.AddComponent<RoomCollapseVolume>();
            var warningRoot = root;
            if (room.Cells.Count > 1)
            {
                warningRoot = new GameObject("Room Warning"); warningRoot.transform.SetParent(root.transform, false);
                warningRoot.transform.position = room.Cells[0].center;
            }
            var warning = warningRoot.AddComponent<FloorLumenGlow>();
            warning.Configure(_config.LumenRoomWarningPrefab, Mathf.Min(room.Cells[0].size.x, room.Cells[0].size.z) * 0.48f,
                _config.WarningColor, _config.WarningIntensity, false);
            roomVolume.Configure(room, _config, _state.BlockerMaterial, warning, resolveIdentity, boundaryReach, cakes); _state.Rooms.Add(room.Id, roomVolume);
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
