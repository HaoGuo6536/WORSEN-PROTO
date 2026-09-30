// ============================================================================
// EnvironmentDriver.cs
// ============================================================================
// PURPOSE:
//   Instantiates imported medieval dressing and real Lumen 2 effects for generated rooms.
//   Local warm pools and selective cold light break up darkness while a nearest-effects
//   budget keeps the amount of expensive lighting independent of the total floor size.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Presentation · Environment.
// KEY RESPONSIBILITIES:
//   - Swap torch art for cold hospital panels before construction, without moving or renumbering sockets.
//   - Apply a density budget to eligible torches only; moon/exit lights keep the shared cap.
//   - Apply safe wall slots, remove decorative collision and own every spawned object.
//   - Replace imported real lights with configured Lumen fake-light strengths without exposing vendor commands to gameplay systems.
//   - Fade destruction and localized cursed flames while preserving route readability.
//   - Own small chalk threshold crosses and clear them on room/floor teardown.
//   - Own runtime Lumen grammar sub-drivers; budget exit fans alongside local lamps.
//   - Bind Core light facts by exact generated socket position; unlit torches consume no budget.
//   - Build dressing and open-sky light pools per footprint cell, retaining one room owner.
// DEPENDENCIES:
//   - Own Presenter/DriverState/DriverConfig; DistantLands.Lumen.Runtime external SDK.
//   - Core interactable snapshots are pushed by the owning Manager, never pulled from Level.
//   - HorrorLumenPresenter pure light math only (acyclic Environment to Horror dependency).
// USAGE NOTES:
//   Scene-owned. No RenderSettings writes; Horror owns global fog and daylight.
//   SetObserver is pushed from the owner. Floor replacement disables old roots immediately.
//   Lumen owns its internal vendor manager; this Driver never accesses its singleton.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DistantLands.Lumen;
using Worsen.Core;

namespace Worsen.Presentation.Environment
{
    public sealed class EnvironmentDriver : MonoBehaviour
    {
        private EnvironmentDriverConfig _config;
        private readonly EnvironmentDriverState _state = new EnvironmentDriverState();
        private readonly Worsen.Presentation.Horror.HorrorLumenPresenter _lumen = new Worsen.Presentation.Horror.HorrorLumenPresenter();
        public bool IsReady => _config != null;
        public int RoomCount => _state.Rooms.Count;
        public int ActiveLumenCount => _state.ActiveLumenCount;
        public int ActiveLightCount => _state.ActiveLightCount;
        public int DoorMarkCount => _state.DoorMarks.Count;

        public void Initialize(EnvironmentDriverConfig config)
        {
            _config = config != null ? config : Resources.Load<EnvironmentDriverConfig>("ScriptableObjects/Presentation/Environment/EnvironmentDriverConfig");
            if (_config == null) Debug.LogWarning("Environment dressing needs its EnvironmentDriverConfig asset.", this);
            else if (_config.LumenLanternPrefab == null || _config.LumenMoonPrefab == null) Debug.LogWarning("Environment requires the project-owned Lumen torch and moon profiles. Rebuild HorrorRun assets.", this);
        }

        public void SetTheme(string theme, string lightSource)
        { _state.ThemeId = theme; _state.LightSource = lightSource; }
        public void SetRoomTheme(int room, string theme, string family) => _state.RoomThemes[room] = (theme, family);

        public void BeginFloor(bool clearTheme = true)
        {
            if (clearTheme) { _state.ThemeId = null; _state.LightSource = null; _state.RoomThemes.Clear(); }
            foreach (EnvironmentFlameDriverState flame in _state.Flames) if (flame.Grammar != null) flame.Grammar.Teardown();
            _state.ExitLightIndex = -1;
            _state.DarkerFloors = _state.Wick = false;
            _state.TorchCountMultiplier = 1f;
            foreach (GameObject room in _state.Rooms.Values) RemoveOwned(room);
            foreach (EnvironmentDoorMarkDriverState mark in _state.DoorMarks.Values) RemoveOwned(mark.Root);
            _state.DoorMarks.Clear(); _state.RoomBounds.Clear(); _state.ConsumedRooms.Clear();
            if (_state.ChalkMaterial != null)
            { if (Application.isPlaying) Destroy(_state.ChalkMaterial); else DestroyImmediate(_state.ChalkMaterial); _state.ChalkMaterial = null; }
            _state.Rooms.Clear(); _state.Flames.Clear(); _state.Positions.Clear(); _state.Available.Clear();
            _state.Elapsed = 0f; _state.UntilRefresh = 0f; _state.Gutter = 0f;
            _state.FlameDimMultiplier = 1f; _state.FlameDimRadius = 0f;
            _state.ActiveLumenCount = 0; _state.ActiveLightCount = 0;
        }

        public void AddRoom(int id, Bounds bounds, bool openSky, bool refuge, Vector3[] portalCenters, Bounds[] reserved = null,
            IReadOnlyList<Bounds> cells = null)
        {
            if (_config == null || _state.Rooms.ContainsKey(id)) return;
            bool fluorescent = EnvironmentThemePresenter.IsFluorescent(_state, id);
            string family = _state.RoomThemes.TryGetValue(id, out var roomTheme) ? roomTheme.Family : string.Empty;
            var root = new GameObject("Room " + id + (fluorescent ? " Hospital " : " Castle ") + family + " Dressing");
            root.transform.SetParent(transform, false); root.SetActive(false);
            _state.Rooms.Add(id, root);
            _state.RoomBounds.Add(id, bounds);
            EnvironmentSlot[] slots = EnvironmentPresenter.BuildDressing(id, bounds, openSky, refuge, portalCenters, reserved, cells);
            for (int i = 0; i < slots.Length; i++)
            {
                EnvironmentSlot slot = slots[i];
                if (slot.Torch)
                {
                    if (!fluorescent) SpawnDecoration(_config.WallTorchPrefab, root.transform, slot, slot.Envelope);
                    AddFlame(id, id * 13 + i, root.transform, slot.Position + Vector3.up * 0.25f, refuge, false,
                        slot.Position, fluorescent, slot.Yaw, slot.Envelope);
                }
                else if (!fluorescent)
                {
                    bool groundProp = slot.Kind == EnvironmentDecorationKind.FloorProp || slot.Kind == EnvironmentDecorationKind.MerchantDisplay;
                    if (groundProp && Physics.CheckBox(slot.Position, slot.Envelope * .5f - Vector3.one * .025f,
                        Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    SpawnDecoration(DecorationPrefab(slot.Kind, id + i), root.transform, slot, slot.Envelope);
                }
            }
            if (openSky)
                foreach (var cell in cells ?? new[] { bounds })
                    AddFlame(id, id * 13 + 12, root.transform,
                        new Vector3(cell.center.x, cell.min.y + 5.5f, cell.center.z), false, true);
            root.SetActive(_state.OwnerEnabled);
            _state.UntilRefresh = 0f;
        }

        private GameObject DecorationPrefab(EnvironmentDecorationKind kind, int variant)
        {
            if (kind == EnvironmentDecorationKind.Arch) return _config.DoorArchPrefab;
            if (kind == EnvironmentDecorationKind.Column) return _config.CornerColumnPrefab;
            if (kind == EnvironmentDecorationKind.MerchantDisplay) return _config.MerchantDisplayPrefab;
            GameObject[] options = kind == EnvironmentDecorationKind.FloorProp ? _config.FloorPropPrefabs : _config.WallDecorationPrefabs;
            return options != null && options.Length > 0 ? options[(variant & int.MaxValue) % options.Length] : null;
        }

        private void SpawnDecoration(GameObject prefab, Transform parent, EnvironmentSlot slot, Vector3 maximumSize)
        {
            if (prefab == null) return;
            GameObject item = Instantiate(prefab, parent);
            item.name = prefab.name + " Dressing";
            item.transform.SetPositionAndRotation(slot.Position, Quaternion.Euler(0f, slot.Yaw, 0f));
            foreach (Collider collider in item.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            RemoveImportedLights(item);
            foreach (AudioSource audio in item.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds extent = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) extent.Encapsulate(renderers[i].bounds);
            float scale = EnvironmentPresenter.FitScale(extent.size, maximumSize, slot.Yaw);
            item.transform.localScale *= scale;
            Vector3 centerOffset = (extent.center - slot.Position) * scale;
            item.transform.position = slot.Position - centerOffset;
        }

        private static void RemoveImportedLights(GameObject root)
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
                // Imported visual instances are owned, inactive dressing during construction.
                // Remove URP's dependent component before its required Light component.
                foreach (Component component in light.GetComponents<Component>())
                    if (component != null && component.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalAdditionalLightData")
                        DestroyImmediate(component);
                DestroyImmediate(light);
            }
        }

        private void AddFlame(int roomId, int identity, Transform parent, Vector3 position, bool refuge, bool moon,
            Vector3 socketPosition = default, bool fluorescent = false, float yaw = 0f, Vector3 envelope = default)
        {
            var holder = new GameObject(moon ? "Lumen 2 Moon Pool" : fluorescent ? "Lumen 2 Fluorescent Pool" : "Lumen 2 Torch Pool");
            holder.transform.SetParent(parent, false); holder.transform.position = position;
            // Vendor shader compares surface-to-source with local -Z, so the beam points +Z.
            if (moon) holder.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var effectRoot = new GameObject("Budgeted Flame FX");
            effectRoot.transform.SetParent(holder.transform, false); effectRoot.SetActive(false);
            var grammar = effectRoot.AddComponent<EnvironmentLumenDriver>();
            LumenEffectPlayer lumen = grammar.CreateLamp(_config, moon, fluorescent);
            EnvironmentFluorescentFixture panel = null;
            if (fluorescent)
            {
                var panelRoot = new GameObject("Cold fluorescent panel"); panelRoot.transform.SetParent(holder.transform, false);
                panel = panelRoot.AddComponent<EnvironmentFluorescentFixture>();
                panel.Configure(socketPosition, yaw, envelope, _config);
            }
            if (!moon && !fluorescent && _config.FirePrefab != null)
            {
                GameObject fire = Instantiate(_config.FirePrefab, effectRoot.transform);
                fire.transform.localPosition = Vector3.zero; fire.transform.localScale *= 0.35f;
                RemoveImportedLights(fire);
                foreach (AudioSource audio in fire.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
                foreach (Collider collider in fire.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            }
            _state.Flames.Add(new EnvironmentFlameDriverState
            {
                RoomId = roomId, Identity = identity, EffectRoot = effectRoot, Lumen = lumen, Grammar = grammar,
                Fluorescent = fluorescent, Panel = panel,
                Intensity = refuge ? 1.4f : 1f, Moon = moon, SocketPosition = socketPosition
            });
            _state.Positions.Add(position); _state.Available.Add(true);
        }

        public Vector3[] GetTorchPositions(int roomId)
        {
            var positions = new List<Vector3>();
            for (int i = 0; i < _state.Flames.Count; i++)
                if (_state.Flames[i].RoomId == roomId && !_state.Flames[i].Moon && !_state.Flames[i].Exit) positions.Add(_state.Positions[i]);
            return positions.ToArray();
        }

        public void SetLightingHooks(bool darkerFloors, bool wick)
        { _state.DarkerFloors = darkerFloors; _state.Wick = wick; _state.UntilRefresh = 0f; }
        public void SetTorchCountMultiplier(float multiplier)
        { _state.TorchCountMultiplier = multiplier; _state.UntilRefresh = 0f; }

        public void ApplyLight(InteractableState light)
        {
            if (!EnvironmentPresenter.ApplyLight(_state, light)) return;
            _state.UntilRefresh = 0f;
            Tick(0f);
        }

        public void SetExitFrame(int roomId, Vector3 position, Quaternion rotation)
        {
            if (_config == null || !_state.Rooms.TryGetValue(roomId, out GameObject room)) return;
            if (_state.ExitLightIndex < 0)
            {
                var root = new GameObject("Budgeted exit rays");
                root.SetActive(false); root.transform.SetParent(room.transform, false);
                var grammar = root.AddComponent<EnvironmentLumenDriver>();
                _state.ExitLightIndex = _state.Flames.Count;
                _state.Flames.Add(new EnvironmentFlameDriverState { RoomId = roomId, Exit = true,
                    EffectRoot = root, Grammar = grammar, Lumen = grammar.CreateExit(_config), Intensity = 1f });
                _state.Positions.Add(position); _state.Available.Add(true);
            }
            var exit = _state.Flames[_state.ExitLightIndex];
            exit.EffectRoot.transform.SetParent(room.transform, true);
            exit.EffectRoot.transform.SetPositionAndRotation(position, rotation);
            exit.RoomId = roomId;
            _state.Positions[_state.ExitLightIndex] = position;
            _state.UntilRefresh = 0f;
        }

        public void SetExitProgress(float progress)
        {
            if (_state.ExitLightIndex < 0) return;
            _state.Flames[_state.ExitLightIndex].OpeningProgress = progress;
            _state.UntilRefresh = 0f;
        }

        public float FogBoundaryGlow(float density) => _config == null ? 0f :
            _lumen.FogBoundaryGlow(density, _config.ThinFogLimit, _config.FogBoundaryStrength);
        public Color FogBoundaryColor => _config != null ? _config.MoonColor : Color.black;
        public float HunterRim(bool lookBack) => _config == null ? 0f :
            _lumen.HunterRim(_config.HunterRimEnabled, lookBack, _config.HunterRimStrength);

        public void SetObserver(Vector3 position) { _state.Observer = position; }
        public void SetFlameGutter(float amount) { _state.Gutter = Mathf.Clamp01(amount); }
        public void SetFlameDim(Vector3 position, float radius, float multiplier)
        {
            _state.FlameDimPosition = position;
            bool reset = multiplier >= 1f || float.IsNaN(multiplier) || !(radius > 0f) || float.IsInfinity(radius);
            _state.FlameDimRadius = reset ? 0f : radius;
            _state.FlameDimMultiplier = reset ? 1f : Mathf.Clamp(multiplier, .3f, 1f);
            _state.UntilRefresh = 0f;
        }
        public void MarkDoor(int doorId, Vector3 position)
        {
            if (!_state.OwnerEnabled || _state.DoorMarks.ContainsKey(doorId) || _state.DoorMarks.Count >= 128) return;
            int[] rooms = EnvironmentPresenter.ChalkRooms(position, _state.RoomBounds);
            foreach (int room in rooms) if (_state.ConsumedRooms.Contains(room)) return;
            if (_state.ChalkMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) { Debug.LogWarning("Threshold chalk needs the project URP particle unlit shader.", this); return; }
                _state.ChalkMaterial = new Material(shader) { name = "Owned threshold chalk" };
                _state.ChalkMaterial.SetColor("_BaseColor", Color.white);
            }
            var root = new GameObject("Pilgrim chalk crossed threshold " + doorId);
            root.transform.SetParent(transform, false);
            foreach (Vector3[] stroke in EnvironmentPresenter.ChalkCross(position))
            {
                var child = new GameObject("Chalk stroke"); child.transform.SetParent(root.transform, false);
                child.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                LineRenderer line = child.AddComponent<LineRenderer>();
                line.alignment = LineAlignment.TransformZ;
                line.sharedMaterial = _state.ChalkMaterial; line.useWorldSpace = true;
                line.positionCount = stroke.Length; line.SetPositions(stroke); line.widthMultiplier = .035f;
                line.startColor = line.endColor = new Color(.82f, .82f, .67f, 1f);
                line.numCapVertices = 2; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            }
            _state.DoorMarks.Add(doorId, new EnvironmentDoorMarkDriverState { Root = root, RoomIds = rooms });
        }
        public void SetRoomConsumed(int id) { SetRoomDestruction(id, 1f); }
        public void SetRoomDestruction(int id, float severity)
        {
            if (severity >= 1f)
            {
                _state.ConsumedRooms.Add(id);
                var removed = new List<int>();
                foreach (var pair in _state.DoorMarks)
                    if (System.Array.IndexOf(pair.Value.RoomIds, id) >= 0) { RemoveOwned(pair.Value.Root); removed.Add(pair.Key); }
                foreach (int door in removed) _state.DoorMarks.Remove(door);
            }
            else _state.ConsumedRooms.Remove(id);
            for (int i = 0; i < _state.Flames.Count; i++)
                if (_state.Flames[i].RoomId == id)
                { _state.Flames[i].Destruction = Mathf.Clamp01(severity); _state.Available[i] = severity < 1f && _state.Flames[i].Lit; }
            if (_state.Rooms.TryGetValue(id, out GameObject room)) room.SetActive(_state.OwnerEnabled && severity < 1f);
            _state.UntilRefresh = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (_config == null || !_state.OwnerEnabled) return;
            _state.Elapsed += Mathf.Max(0f, deltaTime); _state.UntilRefresh -= Mathf.Max(0f, deltaTime);
            if (_state.UntilRefresh > 0f) return;
            _state.UntilRefresh = _config.RefreshInterval;
            int[] visible = EnvironmentPresenter.BudgetedLights(_state,
                _config.MaximumLumenEffects, _config.EffectDistance);
            _state.ActiveLumenCount = 0; _state.ActiveLightCount = 0;
            for (int i = 0; i < _state.Flames.Count; i++)
            {
                EnvironmentFlameDriverState flame = _state.Flames[i];
                int rank = System.Array.IndexOf(visible, i);
                bool effectActive = rank >= 0 && rank < _config.MaximumLumenEffects;
                float gutter = EnvironmentPresenter.CombinedFlameGutter(_state.Gutter, _state.Positions[i],
                    _state.FlameDimPosition, _state.FlameDimRadius, _state.FlameDimMultiplier);
                float intensity = flame.Exit ? _lumen.ExitRayIntensity(flame.OpeningProgress,
                    _config.ExitRayClosedIntensity, _config.ExitRayOpenIntensity) * (1f - flame.Destruction) :
                    flame.Moon ? 1f - flame.Destruction :
                    EnvironmentThemePresenter.LampBrightness(flame.Fluorescent, _state.Elapsed, flame.Identity, gutter,
                        flame.Destruction, _state.Wick, _config.FluorescentFlickerDepth, _config.FluorescentFlickerRate);
                if (_state.DarkerFloors) intensity *= Mathf.Clamp01(_config.DarkerLightMultiplier);
                if (flame.Panel != null) flame.Panel.SetBrightness(effectActive ? intensity : 0f);
                if (flame.EffectRoot.activeSelf != effectActive) flame.EffectRoot.SetActive(effectActive);
                if (effectActive && flame.Lumen != null)
                { flame.Lumen.brightness = intensity * flame.Intensity * _config.LumenBrightness;
                    if (Application.isPlaying) flame.Lumen.RedoEffect(false); _state.ActiveLumenCount++; }
            }
        }

        public void SetOwnerEnabled(bool value)
        {
            _state.OwnerEnabled = value;
            if (!value)
            { _state.ActiveLumenCount = 0; _state.ActiveLightCount = 0; _state.Gutter = 0f; _state.FlameDimRadius = 0f; _state.FlameDimMultiplier = 1f; }
            foreach (var pair in _state.Rooms) pair.Value.SetActive(value && !_state.ConsumedRooms.Contains(pair.Key));
            foreach (var pair in _state.DoorMarks) pair.Value.Root.SetActive(value);
            _state.UntilRefresh = 0f;
        }
        public void Teardown() { BeginFloor(); }
        private static void RemoveOwned(GameObject item)
        {
            if (item == null) return;
            item.SetActive(false);
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
    }
}
