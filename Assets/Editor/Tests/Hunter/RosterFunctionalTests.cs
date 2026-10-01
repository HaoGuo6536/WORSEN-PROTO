// ============================================================================
// RosterFunctionalTests.cs
// ============================================================================
// PURPOSE:
//   Proves each approved hunter's defining rule through its real prefab, Manager
//   and Driver in a native generated arena. This closes the gap left by pure rule
//   tests and the focus-gated legacy chase smoke without entering Play Mode.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), test suite (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Exercise all ten production profiles without substitute rules or tuning edits.
//   - Assert rule-specific motion, counterplay, frozen silence and physical contact facts.
//   - Write per-archetype metrics and always explicitly release native ownership.
// DEPENDENCIES:
//   - Hunter configs, Core facts, scripted native arena and NUnit.
// USAGE NOTES:
//   Coordinator runs after production profile/prefab setup; absent content fails.
//   Each case has a 60s wall budget, independent of injected simulation duration.
//   Echo deliberately follows the owner's new mirror/contact/closed-door rule,
//   not the obsolete door-blocked lunge implementation. No cases are quarantined.
//   Session damage/grace, sensory presentation, audio and owner feel remain separate.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Echo;
using Worsen.Domain.Hunter.Archetypes.Ticking;
using Worsen.Domain.Hunter.Archetypes.Ram;
using Worsen.Domain.Hunter.Archetypes.Skip;
using Worsen.Domain.Hunter.Archetypes.Mimic;
using Worsen.Domain.Hunter.Archetypes.Blinder;
using Worsen.Domain.Hunter.Archetypes.Herald;
using Worsen.Domain.Hunter.Archetypes.Mannequin;
using Worsen.Domain.Hunter.Archetypes.Stare;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class RosterFunctionalTests
    {
        private static void Run(string name, Vector3 spawn, Vector3 player, Action<RosterFunctionalArena> assert)
        {
            var arena = new RosterFunctionalArena(name);
            try { arena.Build(name, spawn, player); assert(arena); }
            finally { arena.Dispose(); }
        }
        private static float Planar(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
        private static void Frozen(RosterFunctionalArena a, float seconds)
        {
            Vector3 held = a.Driver.Position; int hits = a.Metrics.Hits.Count, feedback = a.Metrics.Feedback.Count;
            a.For(seconds, () => Assert.That(Vector3.Distance(a.Driver.Position, held), Is.LessThan(.002f)));
            Assert.That(Vector3.Distance(a.Driver.Position, held), Is.LessThan(.002f), "Observation/shrine hold leaked motion on its first or subsequent tick.");
            Assert.That(a.Driver.Velocity.sqrMagnitude, Is.LessThan(.000001f), "Frozen motor retained movement velocity.");
            Assert.That(a.Metrics.Hits.Count, Is.EqualTo(hits), "Observation/shrine hold leaked damage.");
            Assert.That(a.Metrics.Feedback.Count, Is.EqualTo(feedback), "Frozen Mannequin emitted movement/attack audio feedback.");
        }

        [Test]
        public void Mannequin_LitAndDarkUnseenAdvance_SeenAndWickFreezeSilently_ThenContacts()
        {
            Run("Mannequin", new Vector3(0, 0, -12), new Vector3(0, 0, -2), a => {
                Assert.That(a.Profile.ArchetypeRules, Is.TypeOf<MannequinConfig>());
                foreach (bool lit in new[] { true, false })
                {
                    a.SetLit(lit); a.ViewRotation = Quaternion.identity;
                    float distance = Planar(a.Driver.Position, a.Player.Position);
                    a.Until(() => Planar(a.Driver.Position, a.Player.Position) < distance - .5f, 3f,
                        (lit ? "Lit" : "Dark") + " unseen Mannequin never advanced toward the player.");
                    a.ViewRotation = Quaternion.LookRotation(a.Driver.Position + Vector3.up * ((MannequinConfig)a.Profile.ArchetypeRules).ObservationHeight -
                        (a.Player.Position + Vector3.up * 1.5f));
                    // Capture the pre-observation pose: no unasserted transition tick.
                    Frozen(a, .5f);
                    a.ViewRotation = Quaternion.identity; a.Hunter.SetWickActive(true); Frozen(a, .5f);
                    a.Hunter.SetWickActive(false);
                }
                a.SetLit(true);
                a.Until(() => a.Metrics.Hits.Count > 0, 8f, "Unseen Mannequin could move but never delivered a native contact hit.");
                Assert.That(a.Metrics.BodyContacts, Is.GreaterThan(0));
                Assert.That(a.Metrics.Hits[0].Damage, Is.EqualTo(a.Profile.LungeDamage));
                Assert.That(a.Metrics.Mannequin.Any(f => f.Kind == MannequinFactKind.SilentSoundSet), Is.True);
                Assert.That(a.Metrics.Mannequin.All(f => f.Kind == MannequinFactKind.SilentSoundSet), Is.True,
                    "Mannequin must never emit room-light overrides or lamp budgets.");
                Assert.That(a.Metrics.Feedback, Is.Empty, "Ordinary Mannequin movement/attacks are silent.");
            });
        }

        [Test]
        public void Echo_DelayedExactMirrorCrossesClosedDoor_AndDealsContactDamageWithoutLunging()
        {
            Run("Echo", new Vector3(8, 0, -12), new Vector3(0, 0, -8), a => {
                var config = (EchoConfig)a.Profile.ArchetypeRules;
                Assert.That(a.Profile.ChaseSpeedMultiplier, Is.EqualTo(1f), "This baseline exact-delay case assumes unmutated 1x playback.");
                float delay = config.DelaySeconds;
                Vector3 start = a.Player.Position;
                Vector3 Trail(float time) => time <= 3f ? start + Vector3.forward * (4f * Mathf.Max(0, time)) :
                    start + Vector3.forward * 12f + Vector3.right * (4f * Mathf.Min(1.5f, time - 3f));
                int samples = Mathf.CeilToInt((delay + 6f) / RosterFunctionalArena.Dt);
                int exactSamples = 0;
                for (int i = 0; i < samples; i++)
                {
                    float now = (a.Tick + 1) * RosterFunctionalArena.Dt;
                    a.SetPlayer(Trail(now));
                    a.Player.RecentNoises = new[] { new NoiseEvent(a.Player.Id, a.Player.Position, 1f, a.Tick + 1, NoiseSourceKind.Footstep, NoiseOrigin.PlayerMovement) };
                    if (!a.Door.activeSelf && now > 2.3f) a.CloseDoor();
                    a.Step();
                    Assert.That(a.Hunter.AttackSample.Phase, Is.EqualTo((int)HunterLungePhase.None), "Echo must not leave its exact trail for a shared lunge.");
                    if (now < delay + RosterFunctionalArena.Dt) continue;
                    exactSamples++;
                    Assert.That(Vector3.Distance(a.Driver.Position, Trail(now - delay)), Is.LessThan(.025f),
                        "Echo must mirror the delayed player sample, including corners, from an off-trail production-style spawn.");
                }
                Assert.That(exactSamples, Is.GreaterThan(60));
                Assert.That(a.Door.activeSelf && a.Level.Doors[7], Is.True);
                Assert.That(a.Driver.Position.z, Is.GreaterThan(RosterFunctionalArena.Origin.z + 3f), "Closed physical door blocked replay.");
                Assert.That(a.Metrics.Distance, Is.GreaterThan(10f));
                Assert.That(a.Metrics.Archetype.Any(f => f.Kind == HunterArchetypeFactKind.ReplayedFootstep), Is.True);
                Assert.That(a.Metrics.Hits.Count, Is.GreaterThan(0), "Stationary end-of-trail player was never damaged by contact.");
                Assert.That(a.Metrics.Hits.All(h => h.Target == a.Player.Id && h.Damage == a.Profile.LungeDamage), Is.True);
            });
        }

        [Test]
        public void Weaver_WarnsLaunchesAndSlowsWithNativeWeb_ThenWebsPassedDoorway()
        {
            Run("Weaver", new Vector3(0, 0, -10), new Vector3(0, 0, 10), a => {
                a.Until(() => a.Metrics.WebHits.Count > 0, 12f, "Weaver did not land its defining web attack.");
                var warning = a.Metrics.Weaver.First(f => f.Kind == WeaverFactKind.WetClick);
                var launch = a.Metrics.Weaver.First(f => f.Kind == WeaverFactKind.WebLaunched);
                Assert.That(launch.Tick, Is.GreaterThan(warning.Tick));
                Assert.That((launch.Tick - warning.Tick) * RosterFunctionalArena.Dt, Is.GreaterThanOrEqualTo(warning.Duration - RosterFunctionalArena.Dt));
                Assert.That(a.Metrics.WebHits[0].Player, Is.EqualTo(a.Player.Id));
                Assert.That(a.Metrics.WebHits[0].SlowMultiplier, Is.InRange(.01f, .99f));
                Assert.That(a.Metrics.WebHits[0].Duration, Is.GreaterThan(0));
                a.SetPlayer(a.Point(new Vector3(0, 0, 25)));
                a.Until(() => a.Metrics.Weaver.Any(f => f.Kind == WeaverFactKind.DoorwayWebbed), 18f,
                    "Weaver never crossed and webbed the actual room doorway.");
                Assert.That(a.Metrics.Distance, Is.GreaterThan(1f));
            });
        }

        [Test]
        public void Ticking_TicksSlowerOffersKey_WakesAndPhysicallyThreatens()
        {
            Run("Ticking", new Vector3(0, 0, -24), new Vector3(0, 0, -2), a => {
                var config = (TickingConfig)a.Profile.ArchetypeRules;
                a.Until(() => a.Metrics.ClockSounds.Any(f => f.Sound == TickingSound.Wake), config.SpringSeconds + 1f, "Clock never woke.");
                var ticks = a.Metrics.ClockSounds.Where(f => f.Sound == TickingSound.Tick).ToArray();
                Assert.That(ticks.Length, Is.GreaterThan(2));
                Assert.That(ticks[ticks.Length - 1].Interval, Is.GreaterThan(ticks[0].Interval));
                Assert.That(a.Metrics.Keys.Any(f => f.Active && f.Target.Kind == GuidanceKind.ThreatArrow), Is.True, "Native key placement/guidance never succeeded.");
                Assert.That(a.Metrics.Hits, Is.Empty, "Dormant clock must not hit before waking.");
                Assert.That(a.Metrics.Distance, Is.GreaterThan(.5f), "Maintenance body never followed into its rear pocket.");
                a.Until(() => a.Metrics.Hits.Count > 0, 12f, "Awakened clock never delivered a real threat contact.");
            });
        }

        [Test]
        public void Ram_StampsLocksHeadingCharges_AndStaggersOnWallAfterSidestep()
        {
            Run("Ram", new Vector3(0, 0, -12), new Vector3(0, 0, -4), a => {
                var config = (RamConfig)a.Profile.ArchetypeRules;
                a.Box("Ram stopping wall", RosterFunctionalArena.Origin + Vector3.up * 2, new Vector3(12, 4, .5f));
                a.Until(() => a.Metrics.Ram.Any(f => f.Kind == RamFactKind.Stamp), 3f, "Ram never warned.");
                RamFact stamp = a.Metrics.Ram.First(f => f.Kind == RamFactKind.Stamp);
                Vector3 held = a.Driver.Position;
                a.SetPlayer(a.Point(new Vector3(5, 0, -4)));
                a.Until(() => a.Metrics.Ram.Any(f => f.Kind == RamFactKind.Bellow), config.WindupSeconds + 1f, "Ram warning never became a charge.");
                Assert.That(Planar(a.Driver.Position, held), Is.LessThan(.01f), "Ram moved during its stamp.");
                a.Until(() => a.Metrics.Ram.Any(f => f.Kind == RamFactKind.WallStagger), 3f, "Ram failed to stop at the physical wall.");
                Assert.That(Vector3.Dot(a.Driver.Forward, stamp.Direction), Is.GreaterThan(.999f), "Charge steered after player sidestep.");
                Assert.That(Planar(a.Driver.Position, held), Is.GreaterThan(3f));
                Assert.That(a.Driver.Position.z, Is.LessThan(RosterFunctionalArena.Origin.z));
                Assert.That(a.Metrics.Hits, Is.Empty, "Sidestepped target was hit off the committed line.");
                Vector3 stopped = a.Driver.Position; a.For(config.StaggerSeconds * .5f);
                Assert.That(Planar(a.Driver.Position, stopped), Is.LessThan(.01f));
            });
        }

        [Test]
        public void Skip_LearnsThenSilentlyIntercepts_AndUsesNormalContactHitRecovery()
        {
            Run("Skip", new Vector3(-14, 0, -12), new Vector3(6, 0, 6), a => {
                var config = (SkipConfig)a.Profile.ArchetypeRules;
                Assert.That(a.Hunter.BeginSkipFloor(1), Is.True);
                a.For(config.CooldownSeconds + RosterFunctionalArena.Dt);
                Assert.That(a.Metrics.Skip, Is.Empty, "Skip invented a route before any completed uses.");
                Vector3 anchor = a.Point(Vector3.zero);
                for (int use = 1; use <= config.UsesRequired; use++)
                    Assert.That(a.Hunter.RecordSkipUse(new SkipTraversalUse(1, use, a.Player.Id, 7, SkipRouteKind.Doorway, anchor, 301)), Is.True);
                a.Until(() => a.Metrics.Skip.Any(f => f.Kind == SkipFactKind.Teleported), 1f, "Learned route never produced a native teleport.");
                Assert.That(Planar(a.Driver.Position, anchor), Is.LessThan(.01f));
                Assert.That(a.Metrics.Skip.Any(f => f.Kind == SkipFactKind.Marked && f.MarkId == 301), Is.True);
                Assert.That(a.Metrics.Skip.All(f => !f.Audible && !f.VisibleTransition), Is.True);
                a.SetPlayer(anchor + Vector3.right * .5f); a.Step();
                Assert.That(a.Metrics.Hits.Count, Is.EqualTo(1), "Skip physical interception did not emit exactly one normal hit.");
                HunterHit hit = a.Metrics.Hits[0];
                Assert.That(hit.Target, Is.EqualTo(a.Player.Id)); Assert.That(hit.Damage, Is.EqualTo(a.Profile.LungeDamage));
                Assert.That(hit.Severity, Is.EqualTo(new HunterHit(a.Hunter.Id, a.Player.Id, 1, 1, anchor).Severity));
                Assert.That(hit.Source, Is.EqualTo(new HunterHit(a.Hunter.Id, a.Player.Id, 1, 1, anchor).Source));
                a.For(.1f); Assert.That(a.Metrics.Hits.Count, Is.EqualTo(1), "Body overlap bypassed normal contact recovery.");
                Assert.That(a.Metrics.Feedback, Is.Empty);
            });
        }

        [Test]
        public void Mimic_PosesWithoutMoving_ThenBitesOnceOnPhysicalTouch()
        {
            Run("Mimic", new Vector3(0, 0, -8), new Vector3(0, 0, -3), a => {
                var config = (MimicConfig)a.Profile.ArchetypeRules;
                Vector3 spawn = a.Driver.Position; a.For(.5f);
                Assert.That(Planar(a.Driver.Position, spawn), Is.LessThan(.002f)); Assert.That(a.Metrics.Hits, Is.Empty);
                Assert.That(a.Metrics.Mimic.Any(f => f.Kind == MimicFactKind.Pose && !f.WhiteArrowEligible), Is.True);
                a.SetPlayer(a.Driver.Position + Vector3.right * (config.TouchRadius * .7f)); a.Step();
                Assert.That(a.Metrics.Hits.Count, Is.EqualTo(1));
                Assert.That(a.Metrics.Hits[0].Damage, Is.EqualTo(config.BiteDamage));
                Assert.That(a.Metrics.Hits[0].Source, Is.EqualTo(HitSource.Trap));
                Assert.That(a.Metrics.Mimic.Count(f => f.Kind == MimicFactKind.BiteStarted), Is.EqualTo(1));
                Assert.That(a.Metrics.Mimic.Any(f => f.Kind == MimicFactKind.PoseRemoved), Is.True);
                a.For(config.BiteSeconds + 1f);
                Assert.That(a.Metrics.Hits.Count, Is.EqualTo(1), "Spent false cake bit again.");
                Assert.That(a.Metrics.Mimic.Count(f => f.Kind == MimicFactKind.BiteEnded), Is.EqualTo(1));
            });
        }

        [Test]
        public void Blinder_WarnsThrowsAndProducesBlindnessFromNativeProjectileContact()
        {
            Run("Blinder", new Vector3(0, 0, -12), new Vector3(0, 0, -2), a => {
                var config = (BlinderConfig)a.Profile.ArchetypeRules;
                a.Until(() => a.Metrics.BlindHits.Count > 0, 12f, "Blinder never landed its defining projectile.");
                var hiss = a.Metrics.BlindSounds.First(f => f.Sound == BlinderSound.ThrowHiss);
                var shot = a.Metrics.Throws.First(); var hit = a.Metrics.BlindHits.First();
                Assert.That(shot.Tick, Is.GreaterThan(hiss.Tick));
                Assert.That((shot.Tick - hiss.Tick) * RosterFunctionalArena.Dt, Is.GreaterThanOrEqualTo(config.WarningSeconds - RosterFunctionalArena.Dt));
                Assert.That(hit.Serial, Is.EqualTo(shot.Serial)); Assert.That(hit.Player, Is.EqualTo(a.Player.Id));
                Assert.That(hit.Duration, Is.EqualTo(config.BlindSeconds)); Assert.That(hit.Trap, Is.False);
                Assert.That(a.Metrics.TrapPolicies.Count, Is.GreaterThan(0));
            });
        }

        [Test]
        public void Herald_BroadcastsPlayerClue_ThenWarnsDamagesAndDeafensInRadius()
        {
            Run("Herald", new Vector3(0, 0, -10), new Vector3(0, 0, -4), a => {
                var config = (HeraldConfig)a.Profile.ArchetypeRules;
                a.Until(() => a.Metrics.Deafen.Count > 0, 8f, "Herald never completed its radius attack.");
                var discovery = a.Metrics.Screams.First(f => f.Sound == HeraldSound.Discovery);
                Assert.That(discovery.FloorWideHint.Source, Is.EqualTo(a.Hunter.Id));
                Assert.That(Vector3.Distance(discovery.FloorWideHint.Position, a.Player.Position), Is.LessThan(.01f));
                var breath = a.Metrics.Breaths.First(); var attack = a.Metrics.Screams.First(f => f.Sound == HeraldSound.Attack);
                Assert.That((attack.Noise.Tick - breath.Tick) * RosterFunctionalArena.Dt, Is.GreaterThanOrEqualTo(config.WarningSeconds - RosterFunctionalArena.Dt));
                Assert.That(attack.Pitch, Is.EqualTo(1f));
                Assert.That(a.Metrics.Deafen[0].Player, Is.EqualTo(a.Player.Id));
                Assert.That(a.Metrics.Deafen[0].Damage, Is.EqualTo(config.Damage));
                Assert.That(a.Metrics.Deafen[0].Duration, Is.EqualTo(config.DeafenSeconds));
                Assert.That(a.Metrics.Hits, Is.Empty, "Herald must not substitute a shared lunge for its scream.");
            });
        }

        [Test]
        public void Stare_AppearsAndDismissesUnderHeldLook_ReturnsAndHuntsWhenIgnored()
        {
            Run("Stare", new Vector3(0, 0, -16), new Vector3(0, 0, -4), a => {
                var config = (StareConfig)a.Profile.ArchetypeRules;
                a.Until(() => a.Metrics.Stare.Any(f => f.Kind == StareFactKind.Appeared), 2f, "Stare never obtained a real placement.");
                Assert.That(a.Metrics.Stare.Any(f => f.Kind == StareFactKind.Sound), Is.True);
                Vector3 pinned = a.Driver.Position;
                a.ViewRotation = Quaternion.LookRotation(pinned + Vector3.up * config.ObservationHeight - (a.Player.Position + Vector3.up * 1.5f));
                a.Until(() => a.Metrics.Stare.Any(f => f.Kind == StareFactKind.Disappeared), config.HoldSeconds + 1f, "Held camera look never dismissed Stare.");
                Assert.That(Planar(a.Driver.Position, pinned), Is.LessThan(.01f));
                Assert.That(a.Hunter.GetComponent<CapsuleCollider>().enabled, Is.False, "Logical disappearance did not hide physical body.");
                a.ViewRotation = Quaternion.identity;
                a.Until(() => a.Metrics.Stare.Count(f => f.Kind == StareFactKind.Appeared) == 2, config.ReturnSeconds + 1f, "Stare never returned.");
                a.Until(() => a.Metrics.Stare.Any(f => f.Kind == StareFactKind.ChaseStarted), config.WindowSeconds + 1f, "Ignoring attention never started pursuit.");
                Assert.That(a.Hunter.GetComponent<CapsuleCollider>().enabled, Is.True);
                Assert.That(((IReadOnlyHunterPursuitState)a.Hunter.ReadOnlyState).LossSeconds, Is.EqualTo(a.Profile.LossSeconds * config.LossMultiplier));
                a.Until(() => a.Metrics.Hits.Count > 0, 10f, "Failed attention window never became a physical threat.");
            });
        }
    }
}
