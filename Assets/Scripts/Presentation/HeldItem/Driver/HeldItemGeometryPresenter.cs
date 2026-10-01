// ============================================================================
// HeldItemGeometryPresenter.cs
// ============================================================================
// PURPOSE:
//   Supplies distinct primitive silhouettes for the eight manual-use consumables.
//   These authored placeholders require no downloaded art or arms, and can be
//   replaced by item art later without changing inventory or click admission.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · HeldItem.
// KEY RESPONSIBILITIES:
//   - Compose item-specific primitive geometry as immutable local-space data.
// DEPENDENCIES:
//   Own HeldItemPart definitions and Unity value types only.
// USAGE NOTES:
//   Geometry constants are authored placeholder shape data, not gameplay tunables.
//   Cylinder height is two Unity units; runtime scale comes from DriverConfig.
// ============================================================================
using System;
using UnityEngine;
namespace Worsen.Presentation.HeldItem
{
    public sealed class HeldItemGeometryPresenter
    {
        private static HeldItemPart Part(PrimitiveType shape, float x, float y, float z, float sx, float sy, float sz, bool detail = false)
            => new HeldItemPart(shape, new Vector3(x, y, z), new Vector3(sx, sy, sz), detail);
        public HeldItemPart[] Build(string id)
        {
            switch (id)
            {
                case "firecracker": return new[] {
                    Part(PrimitiveType.Cylinder, -.28f, 0, 0, .25f, .6f, .25f),
                    Part(PrimitiveType.Cylinder, 0, 0, 0, .25f, .6f, .25f),
                    Part(PrimitiveType.Cylinder, .28f, 0, 0, .25f, .6f, .25f),
                    Part(PrimitiveType.Cube, 0, 0, 0, .85f, .16f, .3f, true),
                    Part(PrimitiveType.Cylinder, 0, .78f, 0, .04f, .18f, .04f, true) };
                case "gauze": return new[] {
                    Part(PrimitiveType.Cylinder, 0, 0, 0, 1f, .32f, 1f),
                    Part(PrimitiveType.Cylinder, 0, .325f, 0, .32f, .01f, .32f, true),
                    Part(PrimitiveType.Cube, .46f, -.1f, 0, .15f, .85f, .55f) };
                case "smelling-salts": return new[] {
                    Part(PrimitiveType.Cube, 0, 0, 0, .65f, .8f, .45f),
                    Part(PrimitiveType.Cylinder, 0, .5f, 0, .45f, .12f, .45f, true) };
                case "wax-ward": return new[] {
                    Part(PrimitiveType.Cylinder, 0, -.1f, 0, .65f, .55f, .65f),
                    Part(PrimitiveType.Cylinder, 0, .56f, 0, .06f, .12f, .06f, true),
                    Part(PrimitiveType.Sphere, 0, -.05f, -.30f, .28f, .28f, .12f, true) };
                case "doorstop": return new[] {
                    Part(PrimitiveType.Cube, 0, -.3f, 0, .7f, .2f, 1.2f),
                    Part(PrimitiveType.Cube, 0, -.1f, -.2f, .7f, .2f, .8f),
                    Part(PrimitiveType.Cube, 0, .1f, -.4f, .7f, .2f, .4f, true) };
                case "oil-flask": return new[] {
                    Part(PrimitiveType.Sphere, 0, -.15f, 0, .85f, 1f, .65f),
                    Part(PrimitiveType.Cylinder, 0, .4f, 0, .25f, .25f, .25f),
                    Part(PrimitiveType.Cylinder, 0, .7f, 0, .3f, .08f, .3f, true) };
                case "glass-vial": return new[] {
                    Part(PrimitiveType.Cylinder, 0, 0, 0, .32f, .6f, .32f),
                    Part(PrimitiveType.Cylinder, 0, .65f, 0, .4f, .1f, .4f, true),
                    Part(PrimitiveType.Cylinder, 0, -.25f, 0, .34f, .16f, .34f, true) };
                case "adrenaline": return new[] {
                    Part(PrimitiveType.Cylinder, 0, 0, 0, .22f, .55f, .22f),
                    Part(PrimitiveType.Cube, 0, .58f, 0, .6f, .08f, .16f, true),
                    Part(PrimitiveType.Cylinder, 0, .78f, 0, .12f, .2f, .12f),
                    Part(PrimitiveType.Cylinder, 0, -.75f, 0, .025f, .2f, .025f, true) };
                default: return Array.Empty<HeldItemPart>();
            }
        }
    }
}
