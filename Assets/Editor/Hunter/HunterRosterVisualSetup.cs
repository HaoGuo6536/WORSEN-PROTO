// ============================================================================
// HunterRosterVisualSetup.cs
// ============================================================================
// PURPOSE:
//   Rebuilds one prototype body for each selectable hunter without retuning its
//   rules. Project manifests drive import, stride references and materials; the legacy
//   HorrorHunterSetup owns the shared creature-child and animation wiring.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Validate manifests with a managed parser and convert authored sRGB colours.
//   - Import project rigs and resolve six clips without editing vendor sources.
//   - Finalize imported project materials and fit/ground visible geometry.
//   - Bind ten stable-identity prefabs, measured strides and Mimic-only cake collision.
//   - Fail the setup gate on missing content, invalid wiring or logged errors.
// DEPENDENCIES:
//   - Hunter configs and HorrorHunterSetup; Floor cake config; native Lumen player.
//   - UnityEditor asset APIs; all cross-system wiring is editor-only.
//   - Framework DataContract JSON reader (no native JsonUtility or extra package).
// USAGE NOTES:
//   Explicit idle Edit Mode operation under the coordinator's Unity lease only.
//   Run after HorrorRun/profile setup, which otherwise restores placeholders.
//   No scenes, vendor assets, keys or gameplay configs are saved. Mimic keeps its
//   existing stationary touch path; Floor receives guidance facts, not a new body.
//   Cake surface properties override manifest tint to match the real pickup exactly.
//   Mimic also copies the configured cake elevation and native Lumen glow layers.
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
using UnityEngine.Rendering;
using Worsen.Domain.Hunter;
using Worsen.Domain.Floor;
using DistantLands.Lumen;
using Object = UnityEngine.Object;

namespace Worsen.Editor.Hunter
{
    public static class HunterRosterVisualSetup
    {
        public const string Summary = "Hunter roster visuals: 10 distinct prefabs; 10 project Generic imports; 0 pack bodies; 60 clips resolved; measured strides persisted; Mimic cake surface, glow and collision matched; other motor collision and gameplay tuning unchanged.";
        public const float PackDarkening = 0.55f;
        public const float PrototypeSmoothness = 0.25f;
        public const float VisualHeightRelativeTolerance = 0.005f;
        public const float VisualFootTolerance = 0.005f;
        public const string CakeMaterialPath = "Assets/Art/Horror/Cake/CakeSlice.mat";
        public const string CakeConfigPath = "Assets/Resources/ScriptableObjects/Domain/Floor/HorrorFloorDriverConfig.asset";
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
            [DataMember(Name = "motion_contract", IsRequired = true)] public MotionSpec Motion;
        }
        [DataContract]
        public sealed class MotionSpec
        {
            [DataMember(Name = "authored_walk_speed_mps", IsRequired = true)] public float WalkSpeed;
            [DataMember(Name = "authored_run_speed_mps", IsRequired = true)] public float RunSpeed;
            [DataMember(Name = "stride_default_reason")] public string DefaultReason;
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
            if (result.Motion == null || !Finite(result.Motion.WalkSpeed) || result.Motion.WalkSpeed <= 0f ||
                !Finite(result.Motion.RunSpeed) || result.Motion.RunSpeed <= 0f)
                throw new FormatException("Positive finite measured walk/run references are required.");
            if (expectedHunter == "Mimic" && string.IsNullOrWhiteSpace(result.Motion.DefaultReason))
                throw new FormatException("Stationary Mimic references require a default reason.");
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
                // Every selectable body is now project-made; vendor importers stay untouched.
                foreach (Entry entry in entries) { ImportProject(entry); Resolve(entry); }
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
            if (name == "Mimic" && entry.Profile.MotorOverride == null)
                throw new InvalidOperationException("Run RosterBProfileSetup::BuildMimic before rebuilding Mimic visuals.");
            RequireMotor(Require<GameObject>(BasePrefabPath(name)));
            switch (name)
            {
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
            // Preserve authored cake smoothing as well as the other bodies' flat normals.
            importer.importNormals = ModelImporterNormals.Import;
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
            string[] names = Roles;
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
            // Repair old generic references on every build while retaining asset identity.
            var stride = new SerializedObject(config);
            stride.FindProperty("_walkStrideSpeed").floatValue = entry.Manifest.Motion.WalkSpeed;
            stride.FindProperty("_runStrideSpeed").floatValue = entry.Manifest.Motion.RunSpeed;
            stride.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(config);
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            if (!exists) RequireVacant(path);
            GameObject root = PrefabUtility.LoadPrefabContents(exists ? path : BasePrefabPath(entry.Name));
            try
            {
                RequireMotor(root); root.name = entry.Profile.ArchetypeKey;
                GameObject creature = HorrorHunterSetup.ReplaceRosterVisual(root, entry.Source, entry.Avatar, entry.Height, config, entry.Clips, attacks);
                // Keep the two merge intents explicit even when an old config has
                // stale serialized fields: authored flinch is not attack recovery.
                var roles = new SerializedObject(config);
                roles.FindProperty("_hit").objectReferenceValue = entry.Clips[5];
                roles.FindProperty("_attackRecovery").objectReferenceValue = entry.Clips[0];
                roles.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(config);
                // Renderer bounds may contain the whole animation envelope. Fit actual rest-pose
                // geometry after the legacy helper, so a long attack never shrinks the idle body.
                Bounds bounds = MeasureVisualBounds(creature);
                float scale = entry.Height / bounds.size.y;
                creature.transform.localScale *= scale;
                bounds = MeasureVisualBounds(creature);
                // The legacy fit has already moved the creature origin. Subtracting a
                // scaled (foot - origin) again grounds at that old origin, not the root.
                // Measure after scaling and translate the actual feet to the root instead.
                creature.transform.position += root.transform.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                ApplyMaterials(entry, creature);
                if (entry.Name == "Mimic")
                {
                    MatchCakePresentation(root, creature);
                    var floor = Require<FloorDriverConfig>(CakeConfigPath);
                    RosterBProfileSetup.ConfigureMimicCollision(root, Vector3.up * floor.PickupHeight + MeasureVisualBounds(floor.CakePrefab).center);
                    var motor = new SerializedObject(root.GetComponent<HunterDriver>());
                    motor.FindProperty("_config").objectReferenceValue = entry.Profile.MotorOverride;
                    motor.ApplyModifiedPropertiesWithoutUndo();
                }
                ValidateVisualFit(entry, root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null) throw new InvalidOperationException("Could not save " + path);
                var serialized = new SerializedObject(entry.Profile);
                serialized.FindProperty("_prefab").objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(entry.Profile);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // Acceptance is about saved content, not just the transient fitting instance.
            root = PrefabUtility.LoadPrefabContents(path);
            try { ValidateVisualFit(entry, root); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void ValidateVisualFit(Entry entry, GameObject root)
        {
            Bounds bounds = MeasureVisualBounds(root.transform.Find("Imported Creature").gameObject);
            float targetHeight = entry.Height, targetFoot = root.transform.position.y;
            if (entry.Name == "Mimic")
            {
                var floor = Require<FloorDriverConfig>(CakeConfigPath);
                Bounds cake = MeasureVisualBounds(floor.CakePrefab);
                targetHeight = cake.size.y; targetFoot += floor.PickupHeight + cake.min.y;
            }
            if (Math.Abs(bounds.size.y / targetHeight - 1f) > VisualHeightRelativeTolerance ||
                Math.Abs(bounds.min.y - targetFoot) > VisualFootTolerance)
                throw new InvalidOperationException(entry.Name + " visual fit failed: height=" + bounds.size.y.ToString("0.######") +
                    " m, target=" + entry.Height.ToString("0.######") + " m, foot offset=" +
                    (bounds.min.y - root.transform.position.y).ToString("0.######") + " m.");
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
        private static void MatchCakePresentation(GameObject root, GameObject creature)
        {
            var floor = Require<FloorDriverConfig>(CakeConfigPath);
            if (floor.CakePrefab == null || floor.LumenExitGlowPrefab == null)
                throw new InvalidOperationException("Build the authored HorrorRun cake and native Lumen glow before Mimic visuals.");
            Bounds cake = MeasureVisualBounds(floor.CakePrefab);
            Bounds actual = MeasureVisualBounds(creature);
            creature.transform.localScale *= cake.size.y / actual.size.y;
            actual = MeasureVisualBounds(creature);
            creature.transform.position += root.transform.position + Vector3.up * floor.PickupHeight + cake.center - actual.center;
            Transform previous = root.transform.Find("Mimic Cake Glow");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var glow = new GameObject("Mimic Cake Glow");
            glow.SetActive(false); glow.transform.SetParent(root.transform, false);
            AddCakeGlow(glow.transform, floor, "Inner Cake Glow", Vector3.up * floor.PickupHeight, floor.CakeGlowRadius);
            AddCakeGlow(glow.transform, floor, "Outer Cake Glow", Vector3.up * (floor.PickupHeight + 0.15f), floor.CakeGlowRadius * 1.5f);
            AddCakeGlow(glow.transform, floor, "Cake Light Pool", Vector3.up * 0.03f, floor.CakePoolRadius);
            glow.SetActive(true);
        }
        private static void AddCakeGlow(Transform parent, FloorDriverConfig floor, string label, Vector3 position, float radius)
        {
            var effect = Object.Instantiate(floor.LumenExitGlowPrefab, parent, false);
            effect.name = label; effect.transform.localPosition = position;
            var player = effect.GetComponentInChildren<LumenEffectPlayer>(true);
            if (player == null || player.profile == null) throw new InvalidOperationException("Cake glow requires a native Lumen profile.");
            // Persist the player fields, not FloorLumenGlow's transient ownership state.
            player.updateFrequency = LumenEffectPlayer.UpdateFrequency.ViaScripting;
            player.autoAssignSun = false; player.useLumenSunScript = false;
            player.initializationBehavior = LumenEffectPlayer.InitializationBehavior.Immediate;
            player.deinitializationBehavior = LumenEffectPlayer.DeinitializationBehavior.Immediate;
            float safeRadius = Mathf.Max(0.1f, radius);
            player.range = Mathf.Sqrt(safeRadius * safeRadius + 1f) - 0.5f;
            player.color = floor.FrostingColor; player.brightness = floor.CakeGlowBrightness;
            player.enabled = true; effect.SetActive(true);
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
            return ValidateAndSaveMaterial(material);
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
            return ValidateAndSaveMaterial(material);
        }
        private static Material ValidateAndSaveMaterial(Material material)
        {
            string path = AssetDatabase.GetAssetPath(material);
            // URP MaterialPostprocessor.OnPostprocessAllAssets owns AssetVersion creation
            // and upgrades (MaterialPostprocessor.cs:144-200), not ShaderGUI validation.
            // Finish that import before persisting defaults or handing a material to a prefab.
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            material = Require<Material>(path);
            // Older pack materials omit newer shader defaults (notably _XRMotionVectorsPass).
            // Persist effective values on the imported object, not its pre-import instance.
            // Getters preserve authored overrides and supply shader defaults for missing slots.
            Shader shader = material.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string property = shader.GetPropertyName(i);
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: material.SetFloat(property, material.GetFloat(property)); break;
                    case ShaderPropertyType.Int: material.SetInteger(property, material.GetInteger(property)); break;
                    case ShaderPropertyType.Color: material.SetColor(property, material.GetColor(property)); break;
                    case ShaderPropertyType.Vector: material.SetVector(property, material.GetVector(property)); break;
                    case ShaderPropertyType.Texture:
                        material.SetTexture(property, material.GetTexture(property));
                        material.SetTextureScale(property, material.GetTextureScale(property));
                        material.SetTextureOffset(property, material.GetTextureOffset(property)); break;
                }
            }
            // Dispatch through UnityEditor so URP's shader-specific keyword/pass/render-state
            // validation runs without a new assembly reference or touching the vendor material.
            var editor = (MaterialEditor)UnityEditor.Editor.CreateEditor(material, typeof(MaterialEditor));
            try
            {
                if (editor.customShaderGUI == null) throw new InvalidOperationException("Missing material validator: " + shader.name);
                editor.customShaderGUI.ValidateMaterial(material);
            }
            finally { Object.DestroyImmediate(editor); }
            // Save the imported material and URP's dirty version sub-asset in this file;
            // do not wait for URP's deferred, project-wide SaveAssetsToDisk callback.
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            return material;
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
