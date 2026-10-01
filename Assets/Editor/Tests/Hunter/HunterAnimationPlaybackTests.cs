// ============================================================================
// HunterAnimationPlaybackTests.cs
// ============================================================================
// PURPOSE:
//   Proves that moving roster clips articulate through the runtime manually
//   evaluated PlayableGraph, while Mimic's disguise clips hold the closed cake.
//   Merely creating a graph or finding FBX curves cannot satisfy this regression.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), test suite (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Require ten degrees of moving limb motion, including Ram's ready stamp.
//   - Require exact closed-pose holds for stationary Mimic walk/run, even after bite.
//   - Log observed bone deltas and every curve-to-instance binding on failure.
//   - Check speed application, offscreen playback and symmetric graph teardown.
//   - Exercise ceiling inversion after real animation evaluation.
// DEPENDENCIES:
//   - Saved roster assets, Hunter Drivers, contact-sheet playback probe and NUnit.
// USAGE NOTES:
//   Native Edit Mode only, coordinator lease required; no Play Mode or focus need.
//   No assets are built/changed. Every Driver is torn down before its preview scene.
//   Mimic has Root/Jaw only: bite keeps the ten-degree articulation threshold;
//   walk/run instead compare every transform against the saved closed disguise.
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
                        if (hunter != "Mimic" || role == "attack")
                            yield return new TestCaseData(hunter, role).SetName("RealPlayback_" + hunter + "_" + role);
                yield return new TestCaseData("Ram", "ready").SetName("RealPlayback_Ram_ready");
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
        [TestCase("walk", TestName = "RealPlayback_Mimic_walk")]
        [TestCase("run", TestName = "RealPlayback_Mimic_run")]
        public void MimicLocomotionHoldsClosedCakeEvenAfterBite(string role)
        {
            using var probe = new HunterAnimationContactSheet.PlaybackProbe("Mimic");
            Transform[] transforms = probe.Root.GetComponentsInChildren<Transform>(true);
            Vector3[] positions = transforms.Select(item => item.localPosition).ToArray();
            Quaternion[] rotations = transforms.Select(item => item.localRotation).ToArray();
            Vector3[] scales = transforms.Select(item => item.localScale).ToArray();
            Transform jaw = transforms.Single(item => item.name == "Jaw");
            Quaternion closedJaw = jaw.localRotation;
            try
            {
                // A frozen open jaw must fail too, not just a moving disguise.
                probe.Begin("attack"); probe.AdvanceTo(.267f);
                Assert.That(Quaternion.Angle(closedJaw, jaw.localRotation), Is.GreaterThan(10f), "Bite must open before the hold resets it.");
                probe.Begin(role);
                Assert.That(probe.Animation.Animator.enabled, Is.True);
                for (int sample = 0; sample <= 24; sample++)
                {
                    probe.AdvanceTo(sample / 24f);
                    Assert.That(probe.Animation.State.ActiveClip, Is.EqualTo(role == "walk" ? 1 : 2), "Exercise the requested hold slot, not idle.");
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        string label = role + "/" + transforms[i].name + " sample=" + sample;
                        Assert.That(Vector3.Distance(transforms[i].localPosition, positions[i]), Is.LessThan(.00001f), label + " position");
                        Assert.That(Quaternion.Angle(transforms[i].localRotation, rotations[i]), Is.LessThan(.01f), label + " closed rotation");
                        Assert.That(Vector3.Distance(transforms[i].localScale, scales[i]), Is.LessThan(.00001f), label + " scale");
                    }
                }
            }
            catch { TestContext.WriteLine(probe.BindingReport()); throw; }
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
