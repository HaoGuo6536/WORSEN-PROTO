// ============================================================================
// HorrorAttackCueDriver.cs
// ============================================================================
//
// PURPOSE:
//   Renders one enemy's warning ring and lunge-direction arrow and plays its spatial growl.
//   The owner supplies a computed visual snapshot, so this component never queries combat state.
//
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by HorrorDriver · Presentation · Horror.
//
// KEY RESPONSIBILITIES:
//   - Apply shared-material line rendering to the pushed pose and color.
//   - Play the assigned imported growl only on a supplied windup-entry edge.
//
// DEPENDENCIES:
//   - Unity line rendering/audio and its own Horror config and visual definitions.
//
// USAGE NOTES:
//   Scene-owned; one object per presented enemy. No game or manager lookups.
//   Owns its line objects and AudioSource; Teardown releases all of them.
//
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;

namespace Worsen.Presentation.Horror
{
    public sealed class HorrorAttackCueDriver : MonoBehaviour
    {
        private HorrorDriverConfig _config;
        private HorrorAttackCueDriverState _state;

        public void Initialize(HorrorDriverConfig config, Material material, Vector3[] ring, Vector3[] arrow)
        {
            _config = config;
            _state = new HorrorAttackCueDriverState { Properties = new MaterialPropertyBlock() };
            _state.Ring = CreateLine("Attack warning ring", material, ring, true);
            _state.Arrow = CreateLine("Attack direction arrow", material, arrow, false);
            _state.Growl = gameObject.AddComponent<AudioSource>();
            _state.Growl.playOnAwake = false;
            _state.Growl.spatialBlend = 1f;
            _state.Growl.dopplerLevel = 0f;
            _state.Growl.rolloffMode = AudioRolloffMode.Logarithmic;
            _state.Growl.minDistance = config.AttackAudioMinDistance;
            _state.Growl.maxDistance = config.AttackAudioMaxDistance;
            SetVisible(false);
        }

        public void Apply(HorrorAttackVisual visual)
        {
            if (_state == null) return;
            transform.SetPositionAndRotation(visual.Position, visual.Rotation);
            _state.Ring.transform.localScale = new Vector3(visual.Radius, 1f, visual.Radius);
            _state.Arrow.transform.localScale = new Vector3(1f, 1f, visual.ArrowLength);
            _state.Properties.SetColor("_BaseColor", visual.Color);
            _state.Properties.SetColor("_Color", visual.Color);
            _state.Ring.SetPropertyBlock(_state.Properties);
            _state.Arrow.SetPropertyBlock(_state.Properties);
            SetVisible(visual.Visible);
            if (visual.PlayGrowl && _config.AttackGrowl != null)
                _state.Growl.PlayOneShot(_config.AttackGrowl, _config.AttackGain);
        }

        public void SetVisible(bool visible)
        {
            if (_state == null) return;
            _state.Ring.enabled = visible;
            _state.Arrow.enabled = visible;
        }

        public void Stop()
        {
            SetVisible(false);
            if (_state != null && _state.Growl != null) _state.Growl.Stop();
        }

        public void Teardown()
        {
            if (_state == null) return;
            Stop();
            DestroyOwned(_state.Ring != null ? _state.Ring.gameObject : null);
            DestroyOwned(_state.Arrow != null ? _state.Arrow.gameObject : null);
            DestroyOwned(_state.Growl);
            _state = null;
            _config = null;
        }

        private LineRenderer CreateLine(string label, Material material, Vector3[] positions, bool loop)
        {
            var lineObject = new GameObject(label);
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.widthMultiplier = _config.AttackLineWidth;
            line.sharedMaterial = material;
            line.startColor = line.endColor = Color.white;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = positions.Length;
            line.SetPositions(positions);
            return line;
        }

        private static void DestroyOwned(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void OnDestroy() => Teardown();
    }
}

