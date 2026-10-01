// ============================================================================
// HunterRosterVisualSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds one prototype body for each selectable hunter without retuning its
//   motor or rules. Project manifests drive import and materials; the legacy
//   HorrorHunterSetup owns the shared creature-child and animation wiring.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Validate manifests with a managed parser and convert authored sRGB colours.
//   - Import project rigs and resolve six clips without editing vendor sources.
//   - Create project materials and fit visible geometry, not collision capsules.
//   - Reuse legacy wiring and bind ten distinct, stable-identity prefabs.
//   - Fail the setup gate on missing content, invalid wiring or logged errors.
// DEPENDENCIES:
//   - Hunter configs and HorrorHunterSetup; UnityEditor asset APIs.
//   - Framework DataContract JSON reader (no native JsonUtility or extra package).
// USAGE NOTES:
//   Explicit idle Edit Mode operation under the coordinator's Unity lease only.
//   Run after HorrorRun/profile setup, which otherwise restores placeholders.
//   No scenes, vendor assets, keys or gameplay configs are saved. Mimic keeps its
//   existing stationary touch path; Floor receives guidance facts, not a new body.
//   Cake surface properties override manifest tint to match the real pickup exactly.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Hunter;
using Object = UnityEngine.Object;

namespace Worsen.Editor.Hunter
{
    public static class HunterRosterVisualSetup
    {
        public const string Summary = "Hunter roster visuals: 10 distinct prefabs; 7 project Generic imports; 3 pack bodies; 60 clips resolved; motor collision and gameplay tuning unchanged.";
        public const float PackDarkening = 0.55f;
        public const float PrototypeSmoothness = 0.25f;
        public const string CakeMaterialPath = "Assets/Art/Horror/Cake/CakeSlice.mat";
        private const string Bundle = "Assets/External/NHance/Creatures/StylizedCreaturesBundle/";
        private static readonly string[] Roles = { "idle", "walk", "run", "ready", "attack", "hit" };
        public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[]
            { "Echo", "Weaver", "Ticking", "Ram", "Skip", "Mimic", "Blinder", "Herald", "Mannequin", "Stare" });

        [DataContract]
        public sealed class Manifest
        {
            [DataMember(Name = "schema_version", IsRequired = true)] public int Version;
            [DataMember(Name = "hunter", IsRequired = true)] public string Hunter;
            [DataMember(Name = "fps", IsRequired = true)] public int Fps;
            [DataMember(Name = "height_m", IsRequired = true)] public float Height;
            [DataMember(Name = "actions", IsRequired = true)] public ActionSpec[] Actions;
            [DataMember(Name = "materials", IsRequired = true)] public MaterialSpec[] Materials;
        }
        [DataContract]
        public sealed class ActionSpec
        {
            [DataMember(Name = "name", IsRequired = true)] public string Name;
            [DataMember(Name = "frames", IsRequired = true)] public int[] Frames;
            [DataMember(Name = "loop", IsRequired = true)] public bool Loop;
            [DataMember(Name = "contact_frame")] public int? ContactFrame;
        }
        [DataContract]
        public sealed class MaterialSpec
        {
            [DataMember(Name = "name", IsRequired = true)] public string Name;
            [DataMember(Name = "base_color_srgb", IsRequired = true)] public float[] BaseColor;
            [DataMember(Name = "emission_color_srgb")] public float[] EmissionColor;
            [DataMember(Name = "emission_strength")] public float EmissionStrength;
            [DataMember(Name = "textures")] public TextureSpec Textures;
        }
        [DataContract]
        public sealed class TextureSpec
        {
            [DataMember(Name = "Base Color")] public string Base;
            [DataMember(Name = "Emission Color")] public string Emission;
            [DataMember(Name = "Alpha")] public string Alpha;
        }
        private sealed class Entry
        {
            public string Name, Model, SourcePath;
            public float Height;
            public Manifest Manifest;
            public HunterProfile Profile;
            public GameObject Source;
            public Avatar Avatar;
            public AnimationClip[] Clips;
        }

        public static string ProfilePath(string name) => RosterBProfileSetup.Root + name + "/" + name + "Profile.asset";
        public static string PrefabPath(string name) => HorrorHunterSetup.PrefabDirectory + "/" + name.ToLowerInvariant() + ".prefab";
        public static string ArtPath(string name) => "Assets/Art/Hunter/" + name;
        public static string ManifestPath(string name) => ArtPath(name) + "/WORSEN_Hunter" + name + ".manifest.json";
        public static string BasePrefabPath(string name) => name == "Ticking" ? TickingProfileSetup.HunterPrefabPath : PrefabPath("rusher");
        public static string AnimationPath(string name) => RosterBProfileSetup.Root + name + "/" + name + "AnimationDriverConfig.asset";

        // DataContractJsonSerializer is entirely managed, so this exact parser runs headless.
        public static Manifest ParseManifest(string json, string expectedHunter)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Empty hunter manifest.");
            Manifest result;
            try
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                result = (Manifest)new DataContractJsonSerializer(typeof(Manifest)).ReadObject(stream);
            }
            catch (SerializationException error) { throw new FormatException("Invalid hunter manifest JSON.", error); }
            if (result == null || result.Version != 1 || result.Hunter != expectedHunter || result.Fps <= 0 ||
                !Finite(result.Height) || result.Height <= 0f) throw new FormatException("Invalid manifest identity, version, fps or height.");
            if (result.Actions == null || result.Actions.Length != Roles.Length || result.Actions.Any(action => action == null) ||
                !result.Actions.Select(action => action.Name).OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(Roles.OrderBy(value => value, StringComparer.Ordinal)))
                throw new FormatException("Manifest must declare each of the six clip roles exactly once.");
            foreach (ActionSpec action in result.Actions)
            {
                if (action.Frames == null || action.Frames.Length != 2 || action.Frames[0] < 0 || action.Frames[1] <= action.Frames[0])
                    throw new FormatException("Invalid frame range: " + action.Name);
                if (action.ContactFrame.HasValue && (action.ContactFrame < action.Frames[0] || action.ContactFrame > action.Frames[1]))
                    throw new FormatException("Contact is outside its action: " + action.Name);
                if (action.Name == "attack" && !action.ContactFrame.HasValue) throw new FormatException("Attack contact frame is required.");
            }
            if (result.Materials == null || result.Materials.Length == 0) throw new FormatException("Manifest materials are required.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (MaterialSpec material in result.Materials)
            {
                if (material == null || !SafeName(material.Name) || !names.Add(material.Name)) throw new FormatException("Invalid/duplicate material name.");
                ToLinearColor(material.BaseColor);
                if (material.EmissionColor != null) ToLinearColor(material.EmissionColor);
                if (!Finite(material.EmissionStrength) || material.EmissionStrength < 0f ||
                    (material.EmissionStrength > 0f && material.EmissionColor == null)) throw new FormatException("Invalid emission.");
                if (material.Textures != null)
                    foreach (string texture in new[] { material.Textures.Base, material.Textures.Emission, material.Textures.Alpha })
                        if (texture != null && (!SafeName(texture) || !texture.EndsWith(".png", StringComparison.Ordinal)))
                            throw new FormatException("Texture must be a local PNG filename.");
            }
            return result;
        }
        private static bool SafeName(string value) => !string.IsNullOrWhiteSpace(value) && value != "." && value != ".." &&
            value.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-' || character == '.');
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static Color ToLinearColor(float[] srgb)
        {
            if (srgb == null || srgb.Length != 4 || srgb.Any(value => !Finite(value) || value < 0f || value > 1f))
                throw new FormatException("An sRGB colour requires four finite channels in [0,1].");
            return new Color(LinearChannel(srgb[0]), LinearChannel(srgb[1]), LinearChannel(srgb[2]), srgb[3]);
        }
        private static float LinearChannel(float value) => value <= 0.04045f ? value / 12.92f : (float)Math.Pow((value + 0.055) / 1.055, 2.4);

        [MenuItem("Worsen/Hunter/Build Roster Visuals")]
        public static void BuildMenu() => Debug.Log(Build());
        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Roster visuals require idle Edit Mode and the coordinator's Unity lease.");
            var errors = new List<string>();
            void Capture(string message, string trace, LogType type)
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
            Application.logMessageReceived += Capture;
            try
            {
                Entry[] entries = Names.Select(Prepare).ToArray();
                var attack = Require<HunterAttackDriverConfig>(HorrorHunterSetup.ProfileDirectory + "/rusher_Attack.asset");
                var attackData = new SerializedObject(attack);
                foreach (string field in new[] { "_warningMaterial", "_fallbackShader", "_projectileMaterial", "_spikeMaterial", "_projectilePrefab", "_spikePrefab" })
                    if (attackData.FindProperty(field)?.objectReferenceValue == null)
                        throw new InvalidOperationException("Legacy attack wiring is unresolved: " + field);
                var animation = Require<HunterAnimationDriverConfig>(HorrorHunterSetup.ProfileDirectory + "/rusher_Animation.asset");
                Require<Material>(CakeMaterialPath);
                if (Shader.Find("Universal Render Pipeline/Lit") == null) throw new InvalidOperationException("URP Lit shader missing.");
                // Vendor rigs are validated before the first project import; never repair vendor importers.
                foreach (Entry entry in entries.Where(value => value.Manifest == null)) Resolve(entry);
                foreach (Entry entry in entries.Where(value => value.Manifest != null)) { ImportProject(entry); Resolve(entry); }
                foreach (Entry entry in entries) BuildEntry(entry, attack, animation);
                var prefabs = new HashSet<GameObject>();
                foreach (Entry entry in entries)
                    if (entry.Profile.Prefab == null || !prefabs.Add(entry.Profile.Prefab) || AssetDatabase.GetAssetPath(entry.Profile.Prefab) != PrefabPath(entry.Name))
                        throw new InvalidOperationException("Roster prefab binding is not unique: " + entry.Name);
                if (errors.Count != 0) throw new InvalidOperationException("Roster setup logged errors: " + string.Join(" | ", errors));
                return Summary;
            }
            finally { Application.logMessageReceived -= Capture; }
        }

        private static Entry Prepare(string name)
        {
            var entry = new Entry { Name = name, Profile = Require<HunterProfile>(ProfilePath(name)) };
            if (entry.Profile.ArchetypeKey != name.ToLowerInvariant()) throw new InvalidOperationException("Profile key mismatch: " + name);
            RequireMotor(Require<GameObject>(BasePrefabPath(name)));
            switch (name)
            {
                case "Ram":
                    entry.Model = Bundle + "Meshes/Satyr/Satyr_Full.fbx";
                    entry.SourcePath = Bundle + "Prefabs/Satyr/Satyr_Unarmed.prefab"; entry.Height = 2.1f; break;
                case "Skip":
                    entry.Model = Bundle + "Meshes/ForestImp/ForestImp.fbx";
                    entry.SourcePath = SelectPackPrefab(Bundle + "Prefabs/ForestImp", entry.Model); entry.Height = 1.2f; break;
                case "Blinder":
                    // Goblin, not the Kobold Thief: the Kobold pack is Humanoid-rigged and this pipeline needs a
                    // Generic avatar (batch 14 setup failure). The Goblin is the proven legacy lurker body.
                    entry.Model = Bundle + "Meshes/Goblin/GoblinMale.fbx";
                    entry.SourcePath = SelectPackPrefab(Bundle + "Prefabs/GoblinMale", entry.Model); entry.Height = 1.5f; break;
                default:
                    entry.Manifest = ParseManifest(File.ReadAllText(ManifestPath(name)), name);
                    entry.Height = entry.Manifest.Height;
                    entry.Model = ArtPath(name) + "/WORSEN_Hunter" + name + ".fbx"; entry.SourcePath = entry.Model; break;
            }
            Require<GameObject>(entry.Model); Require<GameObject>(entry.SourcePath);
            return entry;
        }
        private static string SelectPackPrefab(string folder, string model)
        {
            // Pack colour variants are selected by ordinal asset path, never filesystem/GUID order.
            if (!AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("Missing vendor prefab folder: " + folder);
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value, StringComparer.Ordinal))
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source != null && source.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Any(skin => AssetDatabase.GetAssetPath(skin.sharedMesh) == model)) return path;
            }
            throw new InvalidOperationException("No pack prefab uses " + model);
        }
        private static void ImportProject(Entry entry)
        {
            var importer = AssetImporter.GetAtPath(entry.Model) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing model importer: " + entry.Model);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null; importer.bakeAxisConversion = false;
            importer.importAnimation = true; importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            foreach (var remap in importer.GetExternalObjectMap())
                if (remap.Key.type == typeof(Material)) importer.RemoveRemap(remap.Key);
            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (string role in Roles)
            {
                ActionSpec action = entry.Manifest.Actions.Single(value => value.Name == role);
                var matches = takes.Where(take => ClipRole(take.name) == role || ClipRole(take.takeName) == role).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException(entry.Name + " requires one FBX take for " + role);
                ModelImporterClipAnimation clip = matches[0];
                // Unity take frames can be rebased to zero; preserve the manifest duration, not an off-by-one trim.
                float span = action.Frames[1] - action.Frames[0];
                if (Math.Abs(clip.lastFrame - clip.firstFrame - span) > 0.1f)
                    throw new InvalidOperationException(entry.Name + " take duration disagrees with manifest: " + role);
                clip.name = role; clip.lastFrame = clip.firstFrame + span;
                clip.loopTime = action.Loop; clip.loopPose = action.Loop;
                clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true; clip.keepOriginalPositionY = true; clip.keepOriginalPositionXZ = true;
                clip.events = Array.Empty<AnimationEvent>();
                clips.Add(clip);
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }
        private static string ClipRole(string name) => (name ?? "").Split('|').Last();
        private static void Resolve(Entry entry)
        {
            entry.Source = Require<GameObject>(entry.SourcePath);
            Animator animator = entry.Source.GetComponentInChildren<Animator>(true);
            entry.Avatar = animator != null ? animator.avatar : null;
            if (entry.Avatar == null || !entry.Avatar.isValid || entry.Avatar.isHuman)
                throw new InvalidOperationException(entry.Name + " requires a valid Generic avatar.");
            string[] names = entry.Name == "Ram" ? new[] { "Idle_Unarmed", "Walk_Unarmed", "run", "Ready_Unarmed", "Attack_Unarmed", "Hit_Unarmed" } :
                (entry.Name == "Skip" || entry.Name == "Blinder") ? new[] { "idle", "walk", "run", "Ready", "attack", "hit" } :
                Roles;
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(entry.Model).OfType<AnimationClip>().ToArray();
            entry.Clips = names.Select(name =>
            {
                var matches = clips.Where(clip => clip.name == name && !clip.legacy && clip.length > 0f).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException(entry.Name + " needs one nonempty clip: " + name);
                return matches[0];
            }).ToArray();
            foreach (Renderer renderer in entry.Source.GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material == null))
                    throw new InvalidOperationException(entry.Name + " has unresolved source material slots.");
        }
        private static void BuildEntry(Entry entry, HunterAttackDriverConfig attacks, HunterAnimationDriverConfig template)
        {
            string path = PrefabPath(entry.Name);
            EnsureFolder(HorrorHunterSetup.PrefabDirectory); EnsureFolder(ArtPath(entry.Name) + "/Materials");
            HunterAnimationDriverConfig config = AssetDatabase.LoadAssetAtPath<HunterAnimationDriverConfig>(AnimationPath(entry.Name));
            if (config == null)
            {
                RequireVacant(AnimationPath(entry.Name));
                config = Object.Instantiate(template); config.name = entry.Name + "AnimationDriverConfig";
                AssetDatabase.CreateAsset(config, AnimationPath(entry.Name));
            }
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            if (!exists) RequireVacant(path);
            GameObject root = PrefabUtility.LoadPrefabContents(exists ? path : BasePrefabPath(entry.Name));
            try
            {
                RequireMotor(root); root.name = entry.Profile.ArchetypeKey;
                GameObject creature = HorrorHunterSetup.ReplaceRosterVisual(root, entry.Source, entry.Avatar, entry.Height, config, entry.Clips, attacks);
                // Renderer bounds may contain the whole animation envelope. Fit actual rest-pose
                // geometry after the legacy helper, so a long attack never shrinks the idle body.
                Bounds bounds = MeasureVisualBounds(creature);
                float scale = entry.Height / bounds.size.y;
                Vector3 origin = creature.transform.position;
                Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                creature.transform.localScale *= scale; creature.transform.position -= (foot - origin) * scale;
                ApplyMaterials(entry, creature);
                if (Math.Abs(MeasureVisualBounds(creature).size.y / entry.Height - 1f) > 0.05f)
                    throw new InvalidOperationException(entry.Name + " visual height " + MeasureVisualBounds(creature).size.y.ToString("0.###") + " m is outside 5% of " + entry.Height.ToString("0.###") + " m.");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null) throw new InvalidOperationException("Could not save " + path);
                var serialized = new SerializedObject(entry.Profile);
                serialized.FindProperty("_prefab").objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(entry.Profile);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static Bounds MeasureVisualBounds(GameObject visual)
        {
            bool found = false; Bounds bounds = default;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Mesh mesh = null; bool temporary = false;
                try
                {
                    if (renderer is SkinnedMeshRenderer skin)
                    // BakeMesh(mesh) already yields world-scale offsets in the renderer's axes, so map them with
                    // position and rotation only; TransformPoint applied the scale twice (batch 15: the Satyr,
                    // lossy scale 0.394, measured 1.21 m instead of 3.06 m; verified against renderer bounds).
                    { mesh = new Mesh(); temporary = true; skin.BakeMesh(mesh); }
                    else if (renderer is MeshRenderer) mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) throw new InvalidOperationException("Visible renderer has no measurable mesh: " + renderer.name);
                    // Mesh.bounds is readable even when vendor vertex buffers are not CPU-readable.
                    Bounds local = mesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Matrix4x4 toWorld = renderer is SkinnedMeshRenderer
                            ? Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one)
                            : renderer.transform.localToWorldMatrix;
                        Vector3 point = toWorld.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
                    }
                }
                finally { if (temporary) Object.DestroyImmediate(mesh); }
            }
            if (!found || !Finite(bounds.size.y) || bounds.size.y <= 0.001f) throw new InvalidOperationException("Degenerate visual geometry.");
            return bounds;
        }
        private static void ApplyMaterials(Entry entry, GameObject creature)
        {
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            if (entry.Manifest != null)
                foreach (MaterialSpec spec in entry.Manifest.Materials) materials.Add(spec.Name, ProjectMaterial(entry.Name, spec));
            foreach (Renderer renderer in creature.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material source = slots[i];
                    if (source == null) throw new InvalidOperationException("Missing material: " + entry.Name);
                    if (entry.Manifest != null)
                    {
                        if (!materials.TryGetValue(source.name, out slots[i])) throw new InvalidOperationException("Undeclared manifest material: " + source.name);
                    }
                    else
                    {
                        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id))
                            throw new InvalidOperationException("Pack material has no stable asset identity: " + source.name);
                        string key = guid + "_" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        if (!materials.TryGetValue(key, out Material variant))
                        {
                            variant = PackMaterial(entry.Name, key, source); materials.Add(key, variant);
                        }
                        slots[i] = variant;
                    }
                }
                renderer.sharedMaterials = slots;
            }
        }
        private static Material ProjectMaterial(string hunter, MaterialSpec spec)
        {
            Material material = EnsureMaterial(ArtPath(hunter) + "/Materials/" + spec.Name + ".mat");
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            // Clear stale maps/keywords/render state while retaining the asset and its GUID.
            var defaults = new Material(material.shader);
            try { material.CopyPropertiesFromMaterial(defaults); }
            finally { Object.DestroyImmediate(defaults); }
            material.SetColor("_BaseColor", ToLinearColor(spec.BaseColor));
            Color emission = spec.EmissionColor == null ? Color.black : ToLinearColor(spec.EmissionColor) * spec.EmissionStrength;
            material.SetColor("_EmissionColor", emission); material.SetFloat("_Smoothness", PrototypeSmoothness);
            material.SetFloat("_Metallic", 0f); material.SetFloat("_Surface", 0f);
            material.SetTexture("_BaseMap", Texture(hunter, spec.Textures?.Base));
            material.SetTexture("_EmissionMap", Texture(hunter, spec.Textures?.Emission));
            if (spec.EmissionStrength > 0f) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = spec.EmissionStrength > 0f ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            // The authored cake mesh/UVs are shared in the Blender source. Tint/emission in its
            // manifest differ from HorrorArtSetup.EnsureCake; copy the actual pickup surface.
            if (hunter == "Mimic" && spec.Name == "M_HunterMimic_Cake")
            {
                Material cake = Require<Material>(CakeMaterialPath);
                if (cake.shader.name != "Universal Render Pipeline/Lit" || cake.GetTexture("_BaseMap") == null || cake.GetTexture("_EmissionMap") == null)
                    throw new InvalidOperationException("Build the real cake surface before Mimic visuals.");
                material.shader = cake.shader; material.CopyPropertiesFromMaterial(cake);
            }
            material.name = spec.Name; material.enableInstancing = true;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
        }
        private static Texture2D Texture(string hunter, string name)
        {
            if (name == null) return null;
            string folder = hunter == "Mimic" && (name == "CakePalette.png" || name == "CakeEmission.png") ? "Assets/Art/Horror/Cake" : ArtPath(hunter);
            return Require<Texture2D>(folder + "/" + name);
        }
        private static Material PackMaterial(string hunter, string key, Material source)
        {
            Material material = EnsureMaterial(ArtPath(hunter) + "/Materials/Pack_" + key + ".mat");
            material.shader = source.shader; material.CopyPropertiesFromMaterial(source);
            string tint = material.HasProperty("_BaseColor") ? "_BaseColor" : material.HasProperty("_Color") ? "_Color" : null;
            if (tint == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                throw new InvalidOperationException("Pack material has no usable shader/tint: " + source.name);
            Color color = source.GetColor(tint);
            material.SetColor(tint, new Color(color.r * PackDarkening, color.g * PackDarkening, color.b * PackDarkening, color.a));
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", source.GetColor("_EmissionColor") * PackDarkening);
            material.name = hunter + "_" + source.name;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
        }
        private static Material EnsureMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            RequireVacant(path); material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void RequireMotor(GameObject root)
        {
            if (root.GetComponent<CapsuleCollider>() == null || root.GetComponent<Rigidbody>() == null ||
                root.GetComponent<HunterDriver>() == null || root.GetComponent<HunterManager>() == null)
                throw new InvalidOperationException("Base prefab lacks the proven capsule motor: " + root.name);
        }
        private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing " + typeof(T).Name + ": " + path);
        private static void RequireVacant(string path)
        { if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path)) throw new InvalidOperationException("Conflicting/unimported asset: " + path); }
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            { string next = parent + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]); parent = next; }
        }
    }
}
