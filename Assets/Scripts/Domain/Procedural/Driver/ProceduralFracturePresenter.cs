// ============================================================================
// ProceduralFracturePresenter.cs
// ============================================================================
// PURPOSE:
//   Computes reproducible masonry displacement from room destruction progress.
//   Cracks widen before the mist consumes a room, while small capped floor gaps
//   preserve collection and escape without introducing an unrelated falling death.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Convert staged destruction into crack opacity and bounded fragment movement.
// DEPENDENCIES:
//   - Core RoomDestructionSample and Procedural block/room value types.
// USAGE NOTES:
//   No engine calls or clock reads. The Driver applies the same displacement to
//   the renderer and collider and retains a safety slab below every room.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Procedural
{
    public sealed class ProceduralFracturePresenter
    {
        public Color32[] CrackPixels(int size)
        {
            var pixels = new Color32[size * size];
            for (int branch = 0; branch < 5; branch++)
            {
                int x = 13 + branch * 23;
                for (int y = 0; y < size; y++)
                {
                    x = Mathf.Clamp(x + ((y * 17 + branch * 31) % 7 == 0 ? 2 : (y + branch) % 5 == 0 ? -2 : 0), 1, size - 2);
                    pixels[y * size + x] = new Color32(255, 255, 255, 255);
                    pixels[y * size + x + 1] = new Color32(255, 255, 255, 160);
                }
            }
            return pixels;
        }
        public float CrackOpacity(RoomDestructionSample sample)
            => sample.Phase == RoomPhase.Open ? 0f : sample.Phase == RoomPhase.Telegraph ?
                Mathf.Lerp(0.05f, 0.95f, Mathf.Clamp01(sample.Progress)) : 1f;
        public Vector3 Offset(ProceduralBlock block, Bounds room, RoomDestructionSample sample, int index)
        {
            float progress = sample.Phase == RoomPhase.Tearing ? Mathf.Clamp01(sample.Progress) * 0.45f :
                sample.Phase == RoomPhase.Encroaching ? 0.45f + Mathf.Clamp01(sample.Progress) * 0.55f :
                sample.Phase == RoomPhase.Closed ? 1f : 0f;
            var outward = new Vector3(block.Center.x - room.center.x, 0f, block.Center.z - room.center.z);
            if (outward.sqrMagnitude < 0.01f) outward = (index % 2 == 0 ? Vector3.right : Vector3.forward);
            outward.Normalize();
            // Across a seam the total separation is <= 0.16m, less than the capsule diameter.
            float vertical = block.Kind == ProceduralSurfaceKind.Ceiling ? progress * 0.6f :
                block.Kind == ProceduralSurfaceKind.Floor ? -progress * 0.12f : progress * ((index % 3 - 1) * 0.035f);
            return outward * (progress * 0.08f) + Vector3.up * vertical;
        }
    }
}
