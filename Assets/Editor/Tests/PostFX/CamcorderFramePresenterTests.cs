// ============================================================================
// CamcorderFramePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Protects constant lens shaping and degradation-only tape with explicit time.
//   Shader source checks guard the text-free one-pass contract, not rendered quality.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · PostFX.
// KEY RESPONSIBILITIES:
//   - Verify base parameters, rise/relax, configuration, deterministic time and reset.
//   - Guard the shader against text overlays and implicit engine time.
// DEPENDENCIES:
//   PostFX presentation, NUnit, UnityEditor transient config serialization.
// USAGE NOTES:
//   Edit Mode; shader import, volume wiring and GPU timing require coordinator checks.
// ============================================================================
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Presentation.PostFX;
namespace Worsen.Tests.PostFX
{
    public sealed class CamcorderFramePresenterTests
    {
        private PostFXDriverConfig _config;
        private CamcorderFrameDriverState _state;
        [SetUp] public void Setup()
        { _config = ScriptableObject.CreateInstance<PostFXDriverConfig>(); _state = new CamcorderFrameDriverState(); }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(_config);
        private void Tick(float dt, float intrusion = 0f) => CamcorderFramePresenter.Tick(_state, _config, 0f, 0f, intrusion, dt);

        [Test] public void HealthyBaseNeverWobblesAndLensIsConstantDuringDegradation()
        {
            Tick(10f);
            Assert.That(_state.Lens, Is.EqualTo(new Vector4(.22f, .25f, .2f, 1.25f)));
            Assert.That(_state.EdgeStart, Is.EqualTo(.65f));
            Assert.That(_state.Tape.x + _state.Tape.y, Is.Zero);
            var lens = _state.Lens;
            Tick(_config.TapeRiseSeconds / 2f, 1f);
            Assert.That(_state.Degradation, Is.EqualTo(.5f).Within(.0001f));
            Tick(_config.TapeRiseSeconds / 2f, 1f);
            Assert.That(_state.Tape.x, Is.EqualTo(_config.TapeJitterPixels));
            Assert.That(_state.Tape.y, Is.EqualTo(_config.TapeChromaPixels));
            Tick(_config.TapeRelaxSeconds / 2f);
            Assert.That(_state.Degradation, Is.EqualTo(.5f).Within(.0001f));
            Tick(_config.TapeRelaxSeconds / 2f);
            Assert.That(_state.Tape.x + _state.Tape.y, Is.Zero);
            Assert.That(_state.Lens, Is.EqualTo(lens));
        }
        [Test] public void TapeUsesOnlyInjectedTimeAndSubtleIntrusionsStaySubtle()
        {
            var other = new CamcorderFrameDriverState();
            foreach (float dt in new[] { .02f, .1f, .33f, 10f })
            {
                Tick(dt, .12f);
                CamcorderFramePresenter.Tick(other, _config, 0f, 0f, .12f, dt);
                Assert.That(_state.Tape, Is.EqualTo(other.Tape));
                Assert.That(_state.Degradation, Is.LessThanOrEqualTo(.12f));
            }
            Assert.That(_state.Phase, Is.InRange(0f, Mathf.PI * 2f));
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidTimeDoesNotAdvance(float dt)
        {
            Tick(.01f, 1f); var tape = _state.Tape;
            Tick(dt, 1f);
            Assert.That(_state.Tape, Is.EqualTo(tape));
        }
        [Test] public void TunablesAreReadOnlyAndDisableIsNeutral()
        {
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_camcorderCorners").floatValue = .4f;
            serialized.FindProperty("_tapeJitterPixels").floatValue = 2f;
            serialized.FindProperty("_tapeFrequency").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string before = JsonUtility.ToJson(_config);
            Tick(1f, 1f);
            Assert.That(_state.Lens.x, Is.EqualTo(.4f));
            Assert.That(_state.Tape.x, Is.EqualTo(2f));
            Assert.That(_state.Phase, Is.Zero);
            Assert.That(JsonUtility.ToJson(_config), Is.EqualTo(before));
            serialized.FindProperty("_camcorderEnabled").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo(); Tick(0f);
            Assert.That(_state.Lens, Is.EqualTo(Vector4.zero));
            Assert.That(_state.Tape, Is.EqualTo(Vector4.zero));
            CamcorderFramePresenter.Reset(_state);
            Assert.That(_state.Phase + _state.HitWeight + _state.Degradation, Is.Zero);
        }
        [Test] public void FrameShaderHasOnePassNoTextTexturesAndNoEngineClock()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Shaders/CamcorderFrame.shader"));
            Assert.That(System.Text.RegularExpressions.Regex.Matches(source, @"\bPass\s*\{").Count, Is.EqualTo(1));
            foreach (string forbidden in new[] { "_Time", "_SinTime", "_CosTime", "_Font", "_Text", "_Timestamp", "_Battery", "_REC" })
                Assert.That(source, Does.Not.Contain(forbidden));
            Assert.That(source, Does.Contain("_CamcorderTape"));
            Assert.That(source, Does.Contain("_BlitTexture"));
        }
    }
}
