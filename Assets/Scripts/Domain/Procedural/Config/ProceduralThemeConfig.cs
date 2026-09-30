// ============================================================================
// ProceduralThemeConfig.cs
// ============================================================================
// PURPOSE:
//   Supplies interchangeable content for the existing room connection vocabulary.
//   Castle inherits current materials exactly; hospital is provisional data, not
//   a different topology or a permission to change objective and collapse rules.
// ARCHITECTURAL ROLE:
//   Config (§4) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Store selection policy, family names, surface palette and consumer tags.
// DEPENDENCIES:
//   - Unity serialization and own content data only.
// USAGE NOTES:
//   Null config means legacy castle. Per-run draw ignores depth; per-round mode
//   starts at FirstRound and alternates from a seeded initial draw. No asset writes.
// ============================================================================
using System;
using UnityEngine;

namespace Worsen.Domain.Procedural
{
    [CreateAssetMenu(menuName = "Worsen/Procedural/Theme Config")]
    public sealed class ProceduralThemeConfig : ScriptableObject
    {
        [SerializeField] private int _firstRound = 4;
        [SerializeField] private bool _perRun = false;
        [SerializeField] private bool _hospitalEnabled = true;
        [SerializeField] private ProceduralThemeData _castle = new ProceduralThemeData("castle", true,
            new[] { "VaultPartition", "WindowPartition", "SlidePartition", "TorchGallery", "OpenStairHall", "SplitLevelLibrary", "BrokenCloister", "BrokenGallery", "MerchantRefuge", "ExitHub" },
            "masonry", "torch", "castle-stone", "black-mist", "shadow-hands",
            new Color(0.19f, 0.21f, 0.2f), new Color(0.11f, 0.12f, 0.11f), new Color(0.09f, 0.1f, 0.09f), 0.08f);
        [SerializeField] private ProceduralThemeData _hospital = new ProceduralThemeData("hospital", false,
            new[] { "TriagePartition", "ObservationWindow", "ServiceDuct", "Ward", "Atrium", "RecordsGallery", "TreatmentHall", "SurgicalGallery", "StaffRefuge", "Reception" },
            "medical-canister", "fluorescent", "hospital-tile", "cold-black-mist", "gloved-shadow-hands",
            new Color(0.32f, 0.4f, 0.38f), new Color(0.16f, 0.22f, 0.21f), new Color(0.24f, 0.28f, 0.27f), 0.35f, PrimitiveType.Cylinder);
        public int FirstRound => _firstRound;
        public bool PerRun => _perRun;
        public bool HospitalEnabled => _hospitalEnabled;
        public ProceduralThemeData Castle => _castle;
        public ProceduralThemeData Hospital => _hospital;
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
        public ProceduralThemeData(string id, bool inheritMaterials, string[] families, string prop,
            string lightSource, string soundZone, string fogLook, string handLook,
            Color wall, Color floor, Color ceiling, float smoothness, PrimitiveType propPrimitive = PrimitiveType.Cube)
        { _id = id; _inheritMaterials = inheritMaterials; _families = families; _prop = prop;
            _lightSource = lightSource; _soundZone = soundZone; _fogLook = fogLook; _handLook = handLook;
            _wall = wall; _floor = floor; _ceiling = ceiling; _smoothness = smoothness; _propPrimitive = propPrimitive; }
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
    }
}
