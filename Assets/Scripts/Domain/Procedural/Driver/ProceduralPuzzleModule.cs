// ============================================================================
// ProceduralPuzzleModule.cs
// ============================================================================
// PURPOSE:
//   Builds a physical optional reward cage and one of four movement lanes.
//   Contacts are engine observations; the Presenter alone decides progress. The
//   parent supplies committed player motion, completed vaults and explicit time.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by ProceduralDriver · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Build collision, vault metadata, contact-trigger plates and fading path visuals.
//   - Apply gate state and return a one-shot solution for publication by the Manager.
// DEPENDENCIES:
//   - Core traversal interfaces, own Presenter/config and an injected identity resolver.
// USAGE NOTES:
//   Scene-owned with the generated root. No Update or engine clock, no global
//   effects or foreign subscriptions. Shared materials are not mutated. Moving-door
//   cages open their rear escape panel after success so stopping cannot imprison a player.
// ============================================================================
using System;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralPuzzleModule : MonoBehaviour
    {
        private readonly ProceduralPuzzlePresenter _rules = new ProceduralPuzzlePresenter();
        private readonly ProceduralPuzzleLayoutPresenter _layout = new ProceduralPuzzleLayoutPresenter();
        private ProceduralPuzzleDriverState _state;
        private ProceduralChallengeConfig _config;
        private Func<Collider, bool> _isPlayer;
        private GameObject _gate, _escape;
        private readonly Renderer[] _tiles = new Renderer[3];
        private MaterialPropertyBlock _color;
        private Material _tileMaterial;
        private bool _contact;
        private int _vault = -1;
        public ProceduralPuzzlePlan Plan { get; private set; }
        public bool GateOpen => _state != null && _state.GateOpen;

        public void Configure(ProceduralPuzzlePlan plan, ProceduralChallengeConfig config, Material material,
            int layer, Func<Collider, bool> isPlayer)
        {
            Plan = plan; _config = config; _isPlayer = isPlayer;
            _state = new ProceduralPuzzleDriverState(); _color = new MaterialPropertyBlock();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("Puzzle path requires an unlit shader.");
            _tileMaterial = new Material(shader);
            gameObject.layer = layer; transform.position = plan.Origin;
            var body = gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var trigger = gameObject.AddComponent<BoxCollider>(); trigger.isTrigger = true;
            trigger.center = Vector3.up * (config.ContactHeight * 0.5f);
            trigger.size = plan.AlongX ? new Vector3(config.LaneLength, config.ContactHeight, config.LaneWidth) :
                new Vector3(config.LaneWidth, config.ContactHeight, config.LaneLength);
            var blocks = _layout.Blocks(plan, config);
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var part = Box(block.Center, block.Size, material, layer);
                if (block.SurfaceId != 0) part.AddComponent<ProceduralTraversalSurface>().Configure(block);
                if (i == 4) _gate = part;
                if (i == 3) _escape = part;
            }
            for (int i = 0; i < 3; i++)
            {
                var size = new Vector3(config.LaneWidth, config.PanelThickness, config.LaneLength * 0.25f);
                if (plan.AlongX) size = new Vector3(size.z, size.y, size.x);
                var part = Box(_layout.Point(plan, config, i) + Vector3.up * (config.PanelThickness * 0.5f), size, _tileMaterial, layer);
                part.name = "Movement plate " + (i + 1);
                part.GetComponent<Collider>().enabled = false;
                _tiles[i] = part.GetComponent<Renderer>();
            }
            Apply();
        }
        public bool Tick(Vector3 position, float speed, float dt)
        {
            int tile = _layout.Tile(Plan, _config, position);
            bool nearby = (position - Plan.Reward.Position).sqrMagnitude <= _config.NearbyRadius * _config.NearbyRadius;
            // Sequence lanes may begin farther from the cage than its motion sensor.
            if (Plan.Kind != ProceduralPuzzleKind.MovingDoor)
                nearby = (position - Plan.Origin).sqrMagnitude <= _config.LaneLength * _config.LaneLength;
            bool solved = _rules.Step(_state, Plan.Kind, dt, _vault >= 0 ? _vault : tile, speed, nearby,
                _config.SequenceSeconds, _config.SegmentSeconds, _config.MovingSpeed, _contact, _vault >= 0);
            _contact = false; _vault = -1;
            Apply();
            return solved;
        }
        public void CompleteVault(int surfaceId, bool succeeded)
        {
            int index = surfaceId - Plan.Id * 10;
            if (Plan.Kind != ProceduralPuzzleKind.TimedVaults || index < 0 || index >= 3) return;
            if (succeeded) _vault = index;
            else _rules.Fail(_state);
        }
        private void OnTriggerStay(Collider other) { if (_isPlayer?.Invoke(other) == true) _contact = true; }
        private void OnDestroy()
        { if (_tileMaterial != null) { if (Application.isPlaying) Destroy(_tileMaterial); else DestroyImmediate(_tileMaterial); } }
        private void Apply()
        {
            _gate.SetActive(!_state.GateOpen);
            if (Plan.Kind == ProceduralPuzzleKind.MovingDoor) _escape.SetActive(!_state.Solved);
            for (int i = 0; i < _tiles.Length; i++)
            {
                float light = Plan.Kind == ProceduralPuzzleKind.DimmingPath ? _rules.Brightness(_state, i, _config.SegmentSeconds) :
                    (_state.Solved || i == _state.Next ? 1f : 0f);
                Color tint = Color.Lerp(_config.DarkColor, _config.LitColor, light);
                _color.SetColor("_BaseColor", tint); _color.SetColor("_Color", tint);
                _color.SetColor("_EmissionColor", tint);
                _tiles[i].SetPropertyBlock(_color);
            }
        }
        private GameObject Box(Vector3 position, Vector3 size, Material material, int layer)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.layer = layer; part.transform.SetParent(transform, false);
            part.transform.position = position; part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }
    }
}
