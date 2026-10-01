// ============================================================================
// MimicDriver.cs
// ============================================================================
// PURPOSE:
//   Applies Mimic disguise facts to the art-owned body without creating pickups.
//   Golden disguises share Floor's configured cake colour, while a sprung Mimic
//   becomes invisible immediately so its spent body no longer advertises a cake.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Hunter Mimic facet.
// KEY RESPONSIBILITIES:
//   - Snapshot and restore renderer flags and property blocks per life.
//   - Tint golden poses and hide spent poses without touching meshes or shared materials.
// DEPENDENCIES:
//   - Core Mimic facts, own DriverState and Floor's read-only DriverConfig colours.
// USAGE NOTES:
//   Scene-owned, commanded only by MimicModuleManager; no Update or subscriptions.
//   Shares FloorDriverConfig (§7d): there are no independent visual tunables.
//   Does not create colliders, anchors, real cakes or Golden Cake credit.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
namespace Worsen.Domain.Hunter.Archetypes.Mimic
{
    public sealed class MimicDriver : MonoBehaviour
    {
        private readonly MimicDriverState _state = new MimicDriverState();
        public void Initialize(FloorDriverConfig config = null)
        {
            Teardown();
            if (config == null) config = Resources.Load<FloorDriverConfig>("ScriptableObjects/Domain/Floor/FloorDriverConfig");
            if (config == null) throw new InvalidOperationException("Mimic disguise requires the Floor cake palette.");
            _state.GoldenColor = config.GoldenColor;
            _state.Renderers = GetComponentsInChildren<Renderer>(true);
            _state.Enabled = new bool[_state.Renderers.Length];
            _state.Original = new MaterialPropertyBlock[_state.Renderers.Length];
            _state.Tint = new MaterialPropertyBlock();
            for (int i = 0; i < _state.Renderers.Length; i++)
            {
                _state.Enabled[i] = _state.Renderers[i].enabled;
                _state.Original[i] = new MaterialPropertyBlock();
                _state.Renderers[i].GetPropertyBlock(_state.Original[i]);
            }
        }
        public void Apply(MimicFact fact)
        {
            if (_state.Renderers == null || (fact.Kind != MimicFactKind.Pose && fact.Kind != MimicFactKind.PoseRemoved)) return;
            for (int i = 0; i < _state.Renderers.Length; i++)
            {
                var renderer = _state.Renderers[i];
                if (renderer == null) continue;
                renderer.enabled = fact.Kind == MimicFactKind.Pose && _state.Enabled[i];
                renderer.SetPropertyBlock(_state.Original[i]);
                if (fact.Kind != MimicFactKind.Pose || !fact.Golden) continue;
                renderer.GetPropertyBlock(_state.Tint);
                _state.Tint.SetColor("_BaseColor", _state.GoldenColor);
                _state.Tint.SetColor("_Color", _state.GoldenColor);
                renderer.SetPropertyBlock(_state.Tint);
            }
        }
        public void Teardown()
        {
            if (_state.Renderers == null) return;
            for (int i = 0; i < _state.Renderers.Length; i++)
                if (_state.Renderers[i] != null)
                { _state.Renderers[i].enabled = _state.Enabled[i]; _state.Renderers[i].SetPropertyBlock(_state.Original[i]); }
            _state.Renderers = null; _state.Enabled = null; _state.Original = null; _state.Tint = null;
        }
        private void OnDestroy() => Teardown();
    }
}
