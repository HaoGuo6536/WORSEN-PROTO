// ============================================================================
// ShrineDriver.cs
// ============================================================================
// PURPOSE:
//   Creates nonblocking placeholder world objects for the Manager's placements.
//   The world labels show each type and used state without directing the player to it.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Shrine.
// KEY RESPONSIBILITIES:
//   - Build and destroy owned primitive meshes, labels and private materials.
// DEPENDENCIES:
//   - Own DriverConfig and Core placements only; never reads gameplay systems.
// USAGE NOTES:
//   Scene-owned, no global side effects. Primitive colliders are disabled immediately.
//   Activation uses committed pose envelopes in the Controller, not physics callbacks.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Shrine
{
    public sealed class ShrineDriver : MonoBehaviour
    {
        private readonly ShrineDriverState state = new ShrineDriverState();
        private ShrineDriverConfig config;
        public void Build(IReadOnlyList<ShrinePlacement> placements, ShrineDriverConfig configuration)
        {
            Clear(); config = configuration;
            foreach (var placement in placements)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Shrine " + placement.Id + " - " + placement.Kind;
                body.GetComponent<Collider>().enabled = false;
                body.transform.SetParent(transform, false);
                body.transform.position = placement.Site.Position + Vector3.up * config.Size.y * 0.5f;
                body.transform.localScale = config.Size;
                var material = new Material(body.GetComponent<Renderer>().sharedMaterial);
                body.GetComponent<Renderer>().sharedMaterial = material;
                material.color = config.Color; state.Materials.Add(material);
                var label = new GameObject("Kind").AddComponent<TextMesh>();
                label.transform.SetParent(body.transform, false);
                label.transform.localPosition = Vector3.up;
                label.text = placement.Kind.ToString(); label.characterSize = config.LabelSize;
                label.anchor = TextAnchor.MiddleCenter;
                state.Objects.Add(placement.Id, body);
            }
        }
        public void MarkUsed(int id)
        {
            if (!state.Objects.TryGetValue(id, out var body)) return;
            body.GetComponent<Renderer>().sharedMaterial.color = config.SpentColor;
            body.GetComponentInChildren<TextMesh>().text += " (spent)";
        }
        public void Clear()
        {
            foreach (var body in state.Objects.Values) if (body != null) { body.SetActive(false); Release(body); }
            foreach (var material in state.Materials) if (material != null) Release(material);
            state.Objects.Clear(); state.Materials.Clear();
        }
        private void Release(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDestroy() => Clear();
    }
}
