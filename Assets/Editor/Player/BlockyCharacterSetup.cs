// ============================================================================
// BlockyCharacterSetup.cs
// ============================================================================
// PURPOSE:
//   Imports the original generated blocky character and first-person arms with
//   explicit metre-scale rig, material and clip settings. Repeated setup retains
//   material identities and designer edits, then rebuilds the Player's hand wiring.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Editor · Player. No runtime or scene lifecycle.
// KEY RESPONSIBILITIES:
//   - Map the full T-pose skeleton explicitly to a Humanoid avatar.
//   - Import Generic arms and named baked clips without root-motion translation.
//   - Retain flat material colours using the project's Universal Render Pipeline.
// DEPENDENCIES:
//   - UnityEditor import/asset APIs, UnityEngine, and PlayerPrefabGenerator only.
// USAGE NOTES:
//   Coordinator invokes under the Unity lease in idle Edit Mode; never automatic.
//   Writes only generated Player art materials/import settings and Player assets.
//   The .blend is authoring source; runtime wiring always uses the explicit FBX.
// ============================================================================
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Worsen.Editor.Player
{
    public static class BlockyCharacterSetup
    {
        public const string ArtPath = "Assets/Art/Player/BlockyCharacter";
        public const string CharacterPath = ArtPath + "/BlockyCharacter.fbx";
        public const string ArmsPath = ArtPath + "/BlockyArmsFP.fbx";

        [MenuItem("Worsen/Player/Import Blocky Character")]
        public static void ImportBlockyCharacter()
        {
            RequireIdle();
            ConfigureModel(CharacterPath, true);
            PlayerPrefabGenerator.EnsureAssets();
        }

        public static GameObject LoadArmsIfPresent()
        {
            RequireIdle();
            if (!File.Exists(ArmsPath)) return null;
            ConfigureModel(ArmsPath, false);
            return AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPath)
                ?? throw new InvalidOperationException("Blocky arms exist but failed to import.");
        }

        private static void RequireIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Blocky character setup requires idle Edit Mode.");
        }

        private static void ConfigureModel(string path, bool humanoid)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Regenerate the blocky art first.", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("No ModelImporter for " + path);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.preserveHierarchy = true;
            importer.optimizeGameObjects = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.animationType = humanoid ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            if (!humanoid) importer.motionNodeName = "Root";
            else
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Transform[] transforms = model.GetComponentsInChildren<Transform>(true);
                HumanDescription description = importer.humanDescription;
                description.human = HumanTrait.BoneName
                    .Where(name => transforms.Any(t => t.name == name.Replace(" ", "")))
                    .Select(name => new HumanBone { humanName = name, boneName = name.Replace(" ", ""),
                        limit = new HumanLimit { useDefaultValues = true } }).ToArray();
                description.skeleton = transforms.Select(t => new SkeletonBone { name = t.name,
                    position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
                importer.humanDescription = description;
            }
            string[] names = humanoid ? new[] { "Idle", "Walk" } : new[] { "Hold", "Sway" };
            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            importer.clipAnimations = names.Select(name =>
            {
                ModelImporterClipAnimation clip = takes.SingleOrDefault(t => t.name == name || t.name.EndsWith("|" + name, StringComparison.Ordinal));
                if (clip == null) throw new InvalidOperationException(path + " is missing baked take " + name);
                clip.name = name;
                clip.loopTime = name != "Hold";
                clip.loopPose = name != "Hold";
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                return clip;
            }).ToArray();
            importer.SaveAndReimport();
            RemapMaterials(importer);
            if (humanoid)
            {
                Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().SingleOrDefault();
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                    throw new InvalidOperationException("Blocky Humanoid avatar is invalid; inspect the explicit shoulder/limb mapping.");
            }
        }

        private static void RemapMaterials(ModelImporter importer)
        {
            string folder = ArtPath + "/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(ArtPath, "Materials");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Universal Render Pipeline/Lit is unavailable.");
            foreach (Material source in AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<Material>())
            {
                string path = folder + "/" + source.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    Color color = source.color;
                    material = new Material(shader) { name = source.name };
                    material.SetColor("_BaseColor", color);
                    material.SetFloat("_Smoothness", 0f);
                    AssetDatabase.CreateAsset(material, path);
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
            }
            importer.SaveAndReimport();
        }
    }
}
