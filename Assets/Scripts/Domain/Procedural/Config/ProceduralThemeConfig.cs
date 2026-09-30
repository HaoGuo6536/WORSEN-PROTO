// ============================================================================
// ProceduralThemeConfig.cs
// ============================================================================
// PURPOSE:
//   Supplies interchangeable content for the existing room connection vocabulary.
//   Castle, hospital, school and basement provide complete vocabularies and wall
//   heights. Theme selection uses its own seeded stream, never layout randomness.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Store selection policy, family names, surface palette and consumer tags.
// DEPENDENCIES:
//   - Unity serialization and own content data only.
// USAGE NOTES:
//   Null config means legacy castle. FirstRound now controls the first re-roll,
//   not access to the catalogue: round one always draws from all enabled entries.
// ============================================================================
using System;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    [CreateAssetMenu(menuName = "Worsen/Procedural/Theme Config")]
    public sealed class ProceduralThemeConfig : ScriptableObject
    {
        [SerializeField] private int _firstRound = 2;
        [SerializeField] private bool _perRun = false;
        [SerializeField] private bool _hospitalEnabled = true;
        [SerializeField] private bool _schoolEnabled = true, _basementEnabled = true;
        [SerializeField] private ProceduralThemeData _castle = new ProceduralThemeData("castle", true,
            new[] { "VaultPartition", "WindowPartition", "SlidePartition", "TorchGallery", "OpenStairHall", "SplitLevelLibrary", "BrokenCloister", "BrokenGallery", "MerchantRefuge", "ExitHub" },
            "masonry", "torch", "castle-stone", "black-mist", "shadow-hands",
            new Color(0.19f, 0.21f, 0.2f), new Color(0.11f, 0.12f, 0.11f), new Color(0.09f, 0.1f, 0.09f), 0.08f);
        [SerializeField] private ProceduralThemeData _hospital = new ProceduralThemeData("hospital", false,
            new[] { "TriagePartition", "ObservationWindow", "ServiceDuct", "Ward", "Atrium", "RecordsGallery", "TreatmentHall", "SurgicalGallery", "StaffRefuge", "Reception" },
            "medical-canister", "fluorescent", "hospital-tile", "cold-black-mist", "gloved-shadow-hands",
            new Color(0.32f, 0.4f, 0.38f), new Color(0.16f, 0.22f, 0.21f), new Color(0.24f, 0.28f, 0.27f), 0.35f, PrimitiveType.Cylinder, 3.6f);
        [SerializeField] private ProceduralThemeData _school = new ProceduralThemeData("school", false,
            new[] { "ClassPartition", "ClassWindow", "ServiceDuct", "Classroom", "AssemblyHall", "Library", "Gymnasium", "ScienceGallery", "StaffRoom", "EntranceHall" },
            "school-furniture", "fluorescent", "school-corridor", "chalk-grey-mist", "chalk-shadow-hands",
            new Color(0.34f, 0.31f, 0.23f), new Color(0.18f, 0.16f, 0.12f), new Color(0.3f, 0.3f, 0.26f), 0.2f, PrimitiveType.Cube, 3.8f);
        [SerializeField] private ProceduralThemeData _basement = new ProceduralThemeData("basement", false,
            new[] { "PipePartition", "InspectionWindow", "VentDuct", "PumpRoom", "BoilerHall", "ServiceGallery", "PlantRoom", "PipeGallery", "MaintenanceRoom", "ServiceHub" },
            "hvac-piping", "caged-bulb", "basement-metal", "oily-black-mist", "soot-shadow-hands",
            new Color(0.22f, 0.25f, 0.24f), new Color(0.12f, 0.13f, 0.13f), new Color(0.15f, 0.17f, 0.16f), 0.45f, PrimitiveType.Cylinder, 3.2f);
        public int FirstRound => _firstRound;
        public bool PerRun => _perRun;
        public bool HospitalEnabled => _hospitalEnabled;
        public bool SchoolEnabled => _schoolEnabled;
        public bool BasementEnabled => _basementEnabled;
        public ProceduralThemeData Castle => _castle;
        public ProceduralThemeData Hospital => _hospital;
        public ProceduralThemeData School => _school;
        public ProceduralThemeData Basement => _basement;
    }

    [Serializable]
    public sealed class ProceduralThemeData
    {
        [SerializeField] private string _id;
        [SerializeField] private bool _inheritMaterials;
        [SerializeField] private string[] _families;
        [SerializeField] private string _prop, _lightSource, _soundZone, _fogLook, _handLook;
        [SerializeField] private Color _wall, _floor, _ceiling;
        [SerializeField] private float _smoothness;
        [SerializeField] private PrimitiveType _propPrimitive;
        [SerializeField] private float _wallHeight = 7f;
        public ProceduralThemeData(string id, bool inheritMaterials, string[] families, string prop,
            string lightSource, string soundZone, string fogLook, string handLook,
            Color wall, Color floor, Color ceiling, float smoothness, PrimitiveType propPrimitive = PrimitiveType.Cube, float wallHeight = 7f)
        { _id = id; _inheritMaterials = inheritMaterials; _families = families; _prop = prop;
            _lightSource = lightSource; _soundZone = soundZone; _fogLook = fogLook; _handLook = handLook;
            _wall = wall; _floor = floor; _ceiling = ceiling; _smoothness = smoothness; _propPrimitive = propPrimitive; _wallHeight = wallHeight; }
        public string Id => _id;
        public bool InheritMaterials => _inheritMaterials;
        public System.Collections.Generic.IReadOnlyList<string> Families => Array.AsReadOnly(_families);
        public string Prop => _prop;
        public string LightSource => _lightSource;
        public string SoundZone => _soundZone;
        public string FogLook => _fogLook;
        public string HandLook => _handLook;
        public Color Wall => _wall;
        public Color Floor => _floor;
        public Color Ceiling => _ceiling;
        public float Smoothness => _smoothness;
        public PrimitiveType PropPrimitive => _propPrimitive;
        public float WallHeight => _wallHeight;
    }
}
