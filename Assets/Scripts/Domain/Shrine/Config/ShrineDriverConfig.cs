// ============================================================================
// ShrineDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Tunes the temporary code-built shrine silhouettes without touching game rules.
//   These shapes identify the kind and spent state until authored art is supplied.
//   Serialized primitive recipes allow the owner to replace provisional forms and palettes.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Domain · Shrine.
// KEY RESPONSIBILITIES:
//   - Supply the common pedestal and eight distinct primitive recipes and accent colors.
//   - Tune overall size, body colors and active/spent emission strength.
// DEPENDENCIES:
//   - Core ShrineKind, System collections and Unity serialization/value types only.
// USAGE NOTES:
//   No runtime writes; ShrineDriver owns every generated object and material.
//   Recipe positions and full dimensions are normalized against Size, with the floor at y=0.
//   Cylinder dimensions describe its full height, not Unity's two-unit primitive scale.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Shrine
{
    [CreateAssetMenu(menuName = "Worsen/Shrine/Shrine Driver Config")]
    public sealed class ShrineDriverConfig : ScriptableObject
    {
        [SerializeField] private Vector3 _size = new Vector3(0.6f, 1f, 0.6f);
        [SerializeField] private Color _color = new Color(0.6f, 0.5f, 0.2f);
        [SerializeField] private Color _spentColor = Color.gray;
        [SerializeField, Min(0f)] private float _accentEmission = 0.65f;
        [SerializeField, Range(0f, 1f)] private float _spentAccentMultiplier = 0.12f;
        [SerializeField] private Silhouettes _silhouettes = new Silhouettes();
        public Vector3 Size => _size;
        public Color Color => _color;
        public Color SpentColor => _spentColor;
        public float AccentEmission => _accentEmission;
        public float SpentAccentMultiplier => _spentAccentMultiplier;
        public Silhouettes Shapes => _silhouettes;

        [Serializable]
        public struct Part
        {
            [SerializeField] private PrimitiveType _primitive;
            [SerializeField] private Vector3 _position;
            [SerializeField] private Vector3 _dimensions;
            [SerializeField] private Vector3 _euler;
            [SerializeField] private bool _accent;
            public Part(PrimitiveType primitive, Vector3 position, Vector3 dimensions,
                bool accent = false, Vector3 euler = default)
            { _primitive = primitive; _position = position; _dimensions = dimensions; _accent = accent; _euler = euler; }
            public PrimitiveType Primitive => _primitive;
            public Vector3 Position => _position;
            public Vector3 Dimensions => _dimensions;
            public Vector3 Euler => _euler;
            public bool Accent => _accent;
        }

        [Serializable]
        public sealed class Silhouette
        {
            [SerializeField] private Color _accentColor;
            [SerializeField] private Part[] _parts;
            public Silhouette(Color accentColor, params Part[] parts)
            { _accentColor = accentColor; _parts = parts; }
            public Color AccentColor => _accentColor;
            public IReadOnlyList<Part> Parts => _parts;
        }

        [Serializable]
        public sealed class Silhouettes
        {
            [SerializeField] private Part _pedestal = new Part(PrimitiveType.Cube,
                new Vector3(0f, .09f, 0f), new Vector3(1f, .18f, 1f));
            [SerializeField] private Silhouette _chance = new Silhouette(new Color(.65f, .8f, 1f),
                new Part(PrimitiveType.Cube, new Vector3(0f, .53f, 0f), new Vector3(.62f, .62f, .62f)),
                new Part(PrimitiveType.Sphere, new Vector3(0f, .53f, -.31f), new Vector3(.14f, .14f, .06f), true),
                new Part(PrimitiveType.Sphere, new Vector3(0f, .53f, .31f), new Vector3(.14f, .14f, .06f), true),
                new Part(PrimitiveType.Sphere, new Vector3(-.31f, .53f, 0f), new Vector3(.06f, .14f, .14f), true),
                new Part(PrimitiveType.Sphere, new Vector3(.31f, .53f, 0f), new Vector3(.06f, .14f, .14f), true),
                new Part(PrimitiveType.Sphere, new Vector3(0f, .84f, 0f), new Vector3(.14f, .06f, .14f), true));
            [SerializeField] private Silhouette _bargain = new Silhouette(new Color(1f, .65f, .25f),
                new Part(PrimitiveType.Cube, new Vector3(0f, .48f, 0f), new Vector3(.1f, .6f, .12f)),
                new Part(PrimitiveType.Cube, new Vector3(0f, .78f, 0f), new Vector3(.95f, .06f, .12f)),
                new Part(PrimitiveType.Cylinder, new Vector3(-.32f, .67f, 0f), new Vector3(.025f, .2f, .025f)),
                new Part(PrimitiveType.Cylinder, new Vector3(.32f, .67f, 0f), new Vector3(.025f, .2f, .025f)),
                new Part(PrimitiveType.Sphere, new Vector3(-.32f, .55f, 0f), new Vector3(.36f, .12f, .36f), true),
                new Part(PrimitiveType.Sphere, new Vector3(.32f, .55f, 0f), new Vector3(.36f, .12f, .36f), true));
            [SerializeField] private Silhouette _pacification = new Silhouette(new Color(.4f, .85f, .65f),
                new Part(PrimitiveType.Sphere, new Vector3(0f, .58f, 0f), new Vector3(.7f, .5f, .7f)),
                new Part(PrimitiveType.Cylinder, new Vector3(0f, .4f, 0f), new Vector3(.8f, .04f, .8f), true),
                new Part(PrimitiveType.Cylinder, new Vector3(0f, .29f, 0f), new Vector3(.12f, .22f, .12f)),
                new Part(PrimitiveType.Cylinder, new Vector3(0f, .89f, 0f), new Vector3(.1f, .12f, .1f)));
            [SerializeField] private Silhouette _wick = new Silhouette(new Color(1f, .45f, .15f),
                new Part(PrimitiveType.Cylinder, new Vector3(0f, .465f, 0f), new Vector3(.24f, .55f, .24f)),
                new Part(PrimitiveType.Sphere, new Vector3(0f, .855f, 0f), new Vector3(.12f, .23f, .12f), true));
            [SerializeField] private Silhouette _passage = new Silhouette(new Color(.3f, .75f, 1f),
                new Part(PrimitiveType.Cube, new Vector3(-.37f, .55f, 0f), new Vector3(.17f, .74f, .22f)),
                new Part(PrimitiveType.Cube, new Vector3(.37f, .55f, 0f), new Vector3(.17f, .74f, .22f)),
                new Part(PrimitiveType.Cube, new Vector3(0f, .925f, 0f), new Vector3(.91f, .15f, .22f)),
                new Part(PrimitiveType.Cube, new Vector3(0f, .875f, 0f), new Vector3(.56f, .035f, .24f), true));
            [SerializeField] private Silhouette _protection = new Silhouette(new Color(.5f, .6f, 1f),
                new Part(PrimitiveType.Cube, new Vector3(0f, .7f, 0f), new Vector3(.7f, .45f, .18f)),
                new Part(PrimitiveType.Cube, new Vector3(0f, .47f, 0f), new Vector3(.36f, .36f, .18f), euler: new Vector3(0f, 0f, 45f)),
                new Part(PrimitiveType.Cube, new Vector3(0f, .66f, 0f), new Vector3(.08f, .42f, .2f), true));
            [SerializeField] private Silhouette _echo = new Silhouette(new Color(.85f, .5f, 1f),
                new Part(PrimitiveType.Cube, new Vector3(-.3f, .575f, 0f), new Vector3(.2f, .65f, .2f), euler: new Vector3(0f, 0f, -12f)),
                new Part(PrimitiveType.Cube, new Vector3(.3f, .575f, 0f), new Vector3(.2f, .65f, .2f), euler: new Vector3(0f, 0f, 12f)),
                new Part(PrimitiveType.Cube, new Vector3(0f, .27f, 0f), new Vector3(.5f, .08f, .16f)),
                new Part(PrimitiveType.Sphere, new Vector3(-.37f, .91f, 0f), new Vector3(.18f, .18f, .18f), true),
                new Part(PrimitiveType.Sphere, new Vector3(.37f, .91f, 0f), new Vector3(.18f, .18f, .18f), true));
            [SerializeField] private Silhouette _purgatory = new Silhouette(new Color(1f, .25f, .3f),
                new Part(PrimitiveType.Cube, new Vector3(-.28f, .55f, -.28f), new Vector3(.08f, .74f, .08f)),
                new Part(PrimitiveType.Cube, new Vector3(.28f, .55f, -.28f), new Vector3(.08f, .74f, .08f)),
                new Part(PrimitiveType.Cube, new Vector3(-.28f, .55f, .28f), new Vector3(.08f, .74f, .08f)),
                new Part(PrimitiveType.Cube, new Vector3(.28f, .55f, .28f), new Vector3(.08f, .74f, .08f)),
                new Part(PrimitiveType.Cube, new Vector3(0f, .94f, 0f), new Vector3(.72f, .12f, .72f)),
                new Part(PrimitiveType.Sphere, new Vector3(0f, .48f, 0f), new Vector3(.28f, .42f, .28f), true));
            public Part Pedestal => _pedestal;
            public Silhouette Get(ShrineKind kind)
            {
                switch (kind)
                {
                    case ShrineKind.Chance: return _chance;
                    case ShrineKind.Bargain: return _bargain;
                    case ShrineKind.Pacification: return _pacification;
                    case ShrineKind.Wick: return _wick;
                    case ShrineKind.Passage: return _passage;
                    case ShrineKind.Protection: return _protection;
                    case ShrineKind.Echo: return _echo;
                    case ShrineKind.Purgatory: return _purgatory;
                    default: throw new ArgumentOutOfRangeException(nameof(kind));
                }
            }
        }
    }
}
