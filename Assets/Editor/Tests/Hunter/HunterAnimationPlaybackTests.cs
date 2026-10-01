// ============================================================================
// HunterAnimationPlaybackTests.cs
// ============================================================================
// PURPOSE:
//   Proves that every saved roster body actually changes bone rotations through
//   the runtime manually evaluated PlayableGraph. Merely creating a graph or
//   finding FBX curves cannot satisfy this native Edit Mode regression.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), test suite (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Require ten degrees of within-clip limb motion for walk, run and attack.
//   - Log observed bone deltas and every curve-to-instance binding on failure.
//   - Check speed application, offscreen playback and symmetric graph teardown.
//   - Exercise ceiling inversion after real animation evaluation.
// DEPENDENCIES:
//   - Saved roster assets, Hunter Drivers, contact-sheet playback probe and NUnit.
// USAGE NOTES:
//   Native Edit Mode only, coordinator lease required; no Play Mode or focus need.
//   No assets are built/changed. Every Driver is torn down before its preview scene.
//   Mimic has Root/Jaw only: its Jaw is the explicit anatomical substitute for a
//   limb, with the identical ten-degree threshold; no stationary-clip exemption.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Worsen.Domain.Hunter;
using Worsen.Editor.Hunter;

namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class HunterAnimationPlaybackTests
    {
        public static IEnumerable RosterRoles
        {
            get
            {
                foreach (string hunter in HunterRosterVisualSetup.Names)
                    foreach (string role in new[] { "walk", "run", "attack" })
                        yield return new TestCaseData(hunter, role).SetName("RealPlayback_" + hunter + "_" + role);
            }
        }
        [TestCaseSource(nameof(RosterRoles))]
        public void RealPlaybackChangesAnArticulatedBoneByTenDegrees(string hunter, string role)
        {
            using var probe = new HunterAnimationContactSheet.PlaybackProbe(hunter);
            var deltas = new Dictionary<Transform, float>();
            try
            {
                probe.Begin(role);
                Assert.That(probe.Animation.Animator.enabled, Is.True);
                Transform[] bones = probe.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .SelectMany(skin => skin.bones).Where(bone => bone != null).Distinct().ToArray();
                Assert.That(bones, Is.Not.Empty, "Saved prefab must contain skinned bones.");
                var samples = bones.ToDictionary(bone => bone, bone => new List<Quaternion>());
                foreach (Transform bone in bones) deltas.Add(bone, 0f);
                for (int sample = 0; sample <= 24; sample++)
                {
                    probe.AdvanceTo(sample / 24f);
                    foreach (Transform bone in bones)
                    {
                        Quaternion rotation = bone.localRotation;
                        foreach (Quaternion previous in samples[bone])
                            deltas[bone] = Mathf.Max(deltas[bone], Quaternion.Angle(previous, rotation));
                        samples[bone].Add(rotation);
                    }
                }
                var limbs = deltas.Where(pair => Articulated(pair.Key.name, hunter)).OrderByDescending(pair => pair.Value).ToArray();
                Assert.That(limbs, Is.Not.Empty, "No leg/arm bones (Mimic: Jaw) found in this imported rig.");
                var moving = limbs[0];
                string path = AnimationUtility.CalculateTransformPath(moving.Key, probe.Animation.Animator.transform);
                TestContext.WriteLine(hunter + "/" + role + ": " + path + " changed " + moving.Value + " degrees; speed=" + probe.Speed);
                Assert.That(moving.Value, Is.GreaterThanOrEqualTo(10f), "Real playback is static or unreadable: " + hunter + "/" + role);
                Assert.That(probe.Root.transform.position, Is.EqualTo(Vector3.zero), "Animation cannot move the motor root.");
                Assert.That(Quaternion.Angle(probe.Root.transform.rotation, Quaternion.identity), Is.LessThan(.001f));
            }
            catch
            {
                foreach (var pair in deltas.OrderByDescending(pair => pair.Value))
                    TestContext.WriteLine("OBSERVED " + AnimationUtility.CalculateTransformPath(pair.Key, probe.Animation.Animator.transform) +
                        " localRotation delta=" + pair.Value);
                TestContext.WriteLine(probe.BindingReport());
                throw;
            }
        }
        private static bool Articulated(string name, string hunter)
        {
            string lower = name.ToLowerInvariant();
            if (hunter == "Mimic") return lower == "jaw";
            return new[] { "arm", "leg", "thigh", "shin", "calf", "hand", "foot", "segment" }.Any(lower.Contains);
        }
        [Test] public void RealGraphAppliesBothStrideRatesAndRestoresAnimatorFlagsOnTeardown()
        {
            using var probe = new HunterAnimationContactSheet.PlaybackProbe("Echo");
            probe.Driver.Teardown();
            Animator animator = probe.Animation.Animator;
            animator.cullingMode = AnimatorCullingMode.CullCompletely; animator.applyRootMotion = true;
            probe.Driver.Initialize();
            var state = probe.Animation.State; var config = probe.Animation.Config;
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
            probe.Animation.Apply(.1f, 4f, 0, 0);
            var presenter = new HunterAnimationPresenter();
            Assert.That(state.Clips[1].GetSpeed(), Is.EqualTo(presenter.PlaybackRate(4, config.WalkStrideSpeed,
                config.MinimumLocomotionRate, config.MaximumLocomotionRate)).Within(.00001));
            Assert.That(state.Clips[2].GetSpeed(), Is.EqualTo(presenter.PlaybackRate(4, config.RunStrideSpeed,
                config.MinimumLocomotionRate, config.MaximumLocomotionRate)).Within(.00001));
            Assert.That(state.Clips[5].GetAnimationClip(), Is.Not.SameAs(state.Clips[6].GetAnimationClip()));
            probe.Driver.Teardown();
            Assert.That(state.Graph.IsValid(), Is.False); Assert.That(probe.Animation.IsReady, Is.False);
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely)); Assert.That(animator.applyRootMotion, Is.True);
        }
        [Test] public void WeaverStaysInvertedDuringRealRunPlaybackAndRestoresOnDrop()
        {
            using var probe = new HunterAnimationContactSheet.PlaybackProbe("Weaver");
            Vector3 original = probe.Animation.transform.localPosition;
            Quaternion rotation = probe.Animation.transform.localRotation;
            probe.Begin("run", true);
            for (int i = 0; i <= 12; i++)
            {
                probe.AdvanceTo(i / 12f);
                // Also exercise the owner's post-evaluation placement path without
                // a fresh SetWeaverCeiling command from the evidence probe.
                probe.Driver.Animate(.1f, 0, 0f, HunterAnimationPhase.Run);
                Assert.That(Vector3.Dot(probe.Animation.transform.up, Vector3.down), Is.GreaterThan(.99f));
            }
            Assert.That(probe.Root.GetComponent<CapsuleCollider>().bounds.min.y, Is.GreaterThan(0f));
            probe.Driver.SetWeaverCeiling(5f, false);
            Assert.That(probe.Animation.transform.localPosition, Is.EqualTo(original));
            Assert.That(Quaternion.Angle(probe.Animation.transform.localRotation, rotation), Is.LessThan(.001f));
        }
    }
}
