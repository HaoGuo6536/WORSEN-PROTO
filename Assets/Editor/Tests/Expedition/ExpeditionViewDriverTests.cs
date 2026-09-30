// ============================================================================
// ExpeditionViewDriverTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the hunter view uses the rendered camera lens and transform.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Include pitch/look-back and reject disabled cameras without a guessed view.
// DEPENDENCIES:
//   - Expedition Driver, Unity Camera, reflection and NUnit.
// USAGE NOTES:
//   Requires Unity engine objects; does not load or save scenes.
// ============================================================================
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Session.Expedition;
namespace Worsen.Tests.Expedition
{
    public sealed class ExpeditionViewDriverTests
    {
        [Test] public void CameraTransformAndActualLensReachTheSameTick()
        {
            var owner = new GameObject("View sample fixture");
            try
            {
                var camera = owner.AddComponent<UnityEngine.Camera>(); camera.fieldOfView = 72f; camera.aspect = 1.6f;
                camera.transform.SetPositionAndRotation(new Vector3(3, 2, 1), Quaternion.Euler(25, 180, 7));
                var driver = owner.AddComponent<ExpeditionViewDriver>();
                typeof(ExpeditionViewDriver).GetField("_camera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(driver, camera);
                Assert.That(driver.TrySample(12, out var view), Is.True);
                Assert.That(view.Origin, Is.EqualTo(camera.transform.position)); Assert.That(view.Rotation, Is.EqualTo(camera.transform.rotation));
                Assert.That(view.VerticalFov, Is.EqualTo(72f));
                Assert.That(view.HorizontalFov, Is.EqualTo(UnityEngine.Camera.VerticalToHorizontalFieldOfView(72f, 1.6f)));
                Assert.That(view.Tick, Is.EqualTo(12));
                camera.enabled = false; Assert.That(driver.TrySample(13, out view), Is.False); Assert.That(view.HorizontalFov, Is.Zero);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
