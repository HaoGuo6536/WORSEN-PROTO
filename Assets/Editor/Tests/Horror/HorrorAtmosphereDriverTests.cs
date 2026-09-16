// ============================================================================
// HorrorAtmosphereDriverTests.cs
// ============================================================================
//
// PURPOSE:
//   Verifies the real Unity light and Volume ownership lifecycle in an isolated preview scene.
//   The fixture exercises camera flashlight placement and confirms that disabling or teardown restores prior state.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror.
//
// KEY RESPONSIBILITIES:
//   - Check spotlight output and private dither parameters against unchanged shared assets.
//   - Check camera, daylight, global render and prior Volume restoration and object cleanup.
//   - Preserve existing scene identities, dirty flags, roots and render settings.
//
// DEPENDENCIES:
//   - HorrorAtmosphereDriver; Unity Editor scene management; imported DitherFogVolume; NUnit.
//
// USAGE NOTES:
//   Native Edit Mode tests; the coordinator must hold the exclusive Unity lease.
//   Each case owns a preview scene and a paired lighting override, using the same
//   scope as Unity's PreviewRenderUtility. Existing scenes stay active and open,
//   including dirty/untitled scenes; no save, discard or scene-setup reload occurs.
//   Tests remain synchronous Edit Mode cases so immediate teardown is exercised.
//   Sun assignment is read from scoped RenderSettings serialization: its public
//   getter can resolve an unrelated brightest directional light when none is set.
//
// ============================================================================

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using FronkonGames.Weird.DitherFog;
using Worsen.Presentation.Horror;

namespace Worsen.Tests.Horror
{
    public sealed class HorrorAtmosphereDriverTests
    {
        private Scene _previousScene;
        private Scene[] _existingScenes;
        private bool[] _existingDirty;
        private bool[] _existingLoaded;
        private GameObject[][] _existingRoots;
        private Object _previousRenderSettings;
        private string _previousRenderSettingsJson;
        private bool _lightingOverride;
        private Scene _scene;
        private HorrorDriverConfig _config;
        private HorrorAtmosphereDriver _driver;
        private UnityEngine.Camera _camera;
        private Volume _volume;
        private VolumeProfile _shared;
        private VolumeProfile _previousPrivate;
        private Light _daylight;

        [SetUp]
        public void SetUp()
        {
            _previousScene = SceneManager.GetActiveScene();
            CaptureExistingScenes();
            _previousRenderSettings = Unsupported.GetRenderSettings();
            _previousRenderSettingsJson = EditorJsonUtility.ToJson(_previousRenderSettings);
            _scene = EditorSceneManager.NewPreviewScene();
            Assert.That(_scene.IsValid() && EditorSceneManager.IsPreviewScene(_scene), Is.True);
            // Preview scenes are not made active. Redirect RenderSettings instead,
            // as PreviewRenderUtility does, so the user's scene is never darkened.
            Unsupported.SetOverrideLightingSettings(_scene);
            _lightingOverride = true;
            Assert.That(Unsupported.GetRenderSettings(), Is.Not.SameAs(_previousRenderSettings));
            _config = ScriptableObject.CreateInstance<HorrorDriverConfig>();
            _camera = CreateOwned<UnityEngine.Camera>("Horror test camera");
            _camera.farClipPlane = 100f;
            _camera.clearFlags = CameraClearFlags.Depth;
            _camera.backgroundColor = Color.magenta;
            _volume = CreateOwned<Volume>("Horror test volume");
            _volume.isGlobal = true;
            _shared = ScriptableObject.CreateInstance<VolumeProfile>();
            DitherFogVolume fog = _shared.Add<DitherFogVolume>(true);
            fog.fogCurveStart.Override(0.21f);
            fog.fogCurveEnd.Override(0.75f);
            _volume.sharedProfile = _shared;
            _daylight = CreateOwned<Light>("Prior daylight");
            _daylight.type = LightType.Directional;
            _daylight.enabled = true;
            RenderSettings.sun = _daylight;
            _driver = CreateOwned<HorrorAtmosphereDriver>("Horror test owner");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (_driver != null) _driver.Teardown();
                DestroyProfile(_previousPrivate);
                DestroyProfile(_shared);
                if (_config != null) Object.DestroyImmediate(_config);
            }
            finally
            {
                try
                {
                    if (_lightingOverride)
                    {
                        Unsupported.RestoreOverrideLightingSettings();
                        _lightingOverride = false;
                    }
                }
                finally
                {
                    if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
                }
            }
            AssertExistingScenesUnchanged();
        }

        [Test]
        public void CameraSpotlightStartsOnAndTheSwitchLeavesOnlyDimNearVisibility()
        {
            Initialize();
            _driver.SetOwnershipEnabled(true);
            _driver.Apply(0.08f, 0.24f, 18f, 7.5f, true);
            Light spot = FindLight(LightType.Spot);
            Light fill = FindLight(LightType.Point);
            Assert.That(spot.enabled, Is.True);
            Assert.That(spot.range, Is.EqualTo(18f));
            Assert.That(spot.intensity, Is.EqualTo(7.5f));
            Assert.That(spot.transform.parent, Is.SameAs(_camera.transform));
            Assert.That(spot.transform.localPosition, Is.EqualTo(_config.FlashlightLocalOffset));
            Assert.That(spot.shadows, Is.EqualTo(LightShadows.Soft));
            _driver.Apply(0.08f, 0.24f, 18f, 7.5f, false);
            Assert.That(spot.enabled, Is.False);
            Assert.That(fill.enabled, Is.True);
            Assert.That(fill.intensity, Is.LessThan(spot.intensity));
        }

        [Test]
        public void FogChangesOnlyTheOwnedProfileAndPreservesTheSharedAsset()
        {
            Initialize();
            Assert.That(_driver.IsReady, Is.True);
            _driver.SetOwnershipEnabled(true);
            _driver.Apply(0.04f, 0.12f, 13.5f, 7.5f, true);
            Assert.That(_volume.profile, Is.Not.SameAs(_shared));
            Assert.That(_volume.profile.TryGet(out DitherFogVolume runtimeFog), Is.True);
            Assert.That(runtimeFog.fogCurveStart.value, Is.EqualTo(0.04f));
            Assert.That(runtimeFog.fogCurveEnd.value, Is.EqualTo(0.12f));
            Assert.That(runtimeFog.curvedFog.value, Is.False);
            Assert.That(runtimeFog.fogStart.value, Is.EqualTo(0.45f));
            Assert.That(runtimeFog.intensity.value, Is.EqualTo(1f));
            Assert.That(_shared.TryGet(out DitherFogVolume originalFog), Is.True);
            Assert.That(originalFog.fogCurveStart.value, Is.EqualTo(0.21f));
            Assert.That(originalFog.fogCurveEnd.value, Is.EqualTo(0.75f));
            _driver.SetOwnershipEnabled(false);
            Assert.That(_volume.HasInstantiatedProfile(), Is.False);
            Assert.That(_volume.sharedProfile, Is.SameAs(_shared));
        }

        [Test]
        public void DisableRestoresDaylightCameraGlobalLightingAndAPriorPrivateProfile()
        {
            Assert.That(AssignedSun(), Is.SameAs(_daylight), "The fixture must begin with its own assigned sun.");
            Assert.That(RenderSettings.sun, Is.SameAs(_daylight));
            _previousPrivate = _volume.profile;
            AmbientMode priorMode = RenderSettings.ambientMode;
            Color priorAmbient = RenderSettings.ambientLight;
            float priorIntensity = RenderSettings.ambientIntensity;
            float priorReflection = RenderSettings.reflectionIntensity;
            Material priorSkybox = RenderSettings.skybox;
            bool priorFog = RenderSettings.fog;
            bool priorVolumeEnabled = _volume.enabled;
            Initialize();
            _driver.SetOwnershipEnabled(true);
            _driver.Apply(0.08f, 0.24f, 18f, 7.5f, true);
            Assert.That(_daylight.enabled, Is.False);
            Assert.That(AssignedSun(), Is.Null, "Darkening must clear the assigned sun in the owned render settings.");
            Assert.That(RenderSettings.skybox, Is.Null);
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
            Assert.That(_camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            _driver.SetOwnershipEnabled(false);
            Assert.That(_daylight.enabled, Is.True);
            Assert.That(AssignedSun(), Is.SameAs(_daylight), "Restoration must reinstate the exact assigned sun.");
            Assert.That(RenderSettings.sun, Is.SameAs(_daylight));
            Assert.That(RenderSettings.skybox, Is.SameAs(priorSkybox));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(priorMode));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(priorAmbient));
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(priorIntensity));
            Assert.That(RenderSettings.reflectionIntensity, Is.EqualTo(priorReflection));
            Assert.That(RenderSettings.fog, Is.EqualTo(priorFog));
            Assert.That(_camera.clearFlags, Is.EqualTo(CameraClearFlags.Depth));
            Assert.That(_camera.backgroundColor, Is.EqualTo(Color.magenta));
            Assert.That(_volume.profile, Is.SameAs(_previousPrivate));
            Assert.That(_volume.enabled, Is.EqualTo(priorVolumeEnabled));
        }

        [Test]
        public void TeardownDestroysOnlyOwnedLightsAndProfileAndCanInitializeAgain()
        {
            Initialize();
            _driver.SetOwnershipEnabled(true);
            _driver.Apply(0.08f, 0.24f, 18f, 7.5f, true);
            Light spot = FindLight(LightType.Spot);
            GameObject lightRoot = spot.gameObject;
            VolumeProfile runtimeProfile = _volume.profile;
            _driver.Teardown();
            Assert.That(lightRoot == null, Is.True);
            Assert.That(runtimeProfile == null, Is.True);
            Assert.That(_shared != null, Is.True);
            Assert.That(_daylight != null && _daylight.enabled, Is.True);
            Assert.That(_volume.HasInstantiatedProfile(), Is.False);
            Initialize();
            Assert.That(_camera.GetComponentsInChildren<Light>().Length, Is.EqualTo(2));
            _driver.Teardown();
            _driver.Teardown();
            Assert.That(_camera.GetComponentsInChildren<Light>(), Is.Empty);
        }

        private void Initialize() => _driver.Initialize(_config, _camera, _volume, new[] { _daylight });

        private static Light AssignedSun()
        {
            // RenderSettings.sun falls back to the brightest directional light
            // when this serialized assignment is null. Inspect the assignment to
            // distinguish clearing it from retaining the fixture's disabled sun.
            using (var settings = new SerializedObject(Unsupported.GetRenderSettings()))
            {
                var sun = settings.FindProperty("m_Sun");
                Assert.That(sun, Is.Not.Null, "Unity's RenderSettings sun serialization changed.");
                return sun.objectReferenceValue as Light;
            }
        }

        private T CreateOwned<T>(string name) where T : Component
        {
            var owner = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
            SceneManager.MoveGameObjectToScene(owner, _scene);
            return owner.AddComponent<T>();
        }

        private void CaptureExistingScenes()
        {
            _existingScenes = new Scene[SceneManager.sceneCount];
            _existingDirty = new bool[_existingScenes.Length];
            _existingLoaded = new bool[_existingScenes.Length];
            _existingRoots = new GameObject[_existingScenes.Length][];
            for (int index = 0; index < _existingScenes.Length; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                _existingScenes[index] = scene;
                _existingDirty[index] = scene.isDirty;
                _existingLoaded[index] = scene.isLoaded;
                if (scene.isLoaded) _existingRoots[index] = scene.GetRootGameObjects();
            }
        }

        private void AssertExistingScenesUnchanged()
        {
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(_previousScene));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(_existingScenes.Length));
            for (int index = 0; index < _existingScenes.Length; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                Assert.That(scene, Is.EqualTo(_existingScenes[index]), "Existing scene identity/order changed.");
                Assert.That(scene.isLoaded, Is.EqualTo(_existingLoaded[index]));
                Assert.That(scene.isDirty, Is.EqualTo(_existingDirty[index]), "The fixture changed an existing scene's dirty flag.");
                if (scene.isLoaded) Assert.That(scene.GetRootGameObjects(), Is.EquivalentTo(_existingRoots[index]));
            }
            Assert.That(Unsupported.GetRenderSettings(), Is.SameAs(_previousRenderSettings));
            Assert.That(EditorJsonUtility.ToJson(_previousRenderSettings), Is.EqualTo(_previousRenderSettingsJson),
                "The fixture changed render settings belonging to the previously active scene.");
        }

        private Light FindLight(LightType type)
        {
            foreach (Light light in _camera.GetComponentsInChildren<Light>())
                if (light.type == type) return light;
            Assert.Fail("Expected owned light type " + type);
            return null;
        }
        private static void DestroyProfile(VolumeProfile profile)
        {
            if (profile == null) return;
            foreach (VolumeComponent component in profile.components)
                if (component != null) Object.DestroyImmediate(component);
            Object.DestroyImmediate(profile);
        }
    }
}
