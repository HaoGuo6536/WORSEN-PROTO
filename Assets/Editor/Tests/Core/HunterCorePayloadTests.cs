// ============================================================================
// HunterCorePayloadTests.cs
// ============================================================================
// PURPOSE:
//   Locks the promoted hunter contracts independently of Domain implementations.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Core.
// KEY RESPONSIBILITIES:
//   - Check duplicate validation, immutable replay snapshots and attributed facts.
// DEPENDENCIES:
//   - Core values, Unity value types and NUnit only.
// USAGE NOTES:
//   Pure Edit Mode tests; no scene, asset or registry mutation.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Core
{
    public sealed class HunterCorePayloadTests
    {
        [Test] public void SpawnIsCompatibleAndRejectsNegativeDuplicateIndices()
        {
            var spawn = new SpawnRequest("echo", Vector3.one, Quaternion.identity, new EntityId(1));
            Assert.That(spawn.DuplicateIndex, Is.Zero);
            var duplicate = new HunterSpawnRequest(spawn, 2);
            Assert.That(duplicate.Spawn.Owner, Is.EqualTo(spawn.Owner));
            Assert.That(duplicate.Spawn.DuplicateIndex, Is.EqualTo(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HunterSpawnRequest(spawn, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnRequest("echo", Vector3.zero, Quaternion.identity, duplicateIndex: -1));
        }
        [Test] public void FactsCopyPathsAndPreserveAttributionAndMutationValues()
        {
            var id = new EntityId(-1); var path = new[] { Vector3.one };
            var replay = new HunterArchetypeFact(id, HunterArchetypeFactKind.TrailRevealed, Vector3.zero, 9, path: path);
            path[0] = Vector3.zero; Assert.That(replay.Path[0], Is.EqualTo(Vector3.one));
            var mutation = new HunterMutation(HunterTunable.Acceleration, 17f, "tell");
            Assert.That(new HunterMutationFact(id, "echo", "tell", 9, mutation).Mutation.Value.Value, Is.EqualTo(17f));
            var noise = new NoiseEvent(new EntityId(1), Vector3.one, 1f, 9);
            Assert.That(new TickingNoiseFact(id, noise).Noise, Is.EqualTo(noise));
            var guidance = new GuidanceTarget(GuidanceKind.ThreatArrow, Vector3.forward, Vector3.one, entityId: id, isFallback: true);
            Assert.That(new TickingGuidanceFact(guidance, false, 9).Target.EntityId, Is.EqualTo(id));
            Assert.That(new TickingSoundFact(TickingSound.Tick, Vector3.one, .5f, 9, id, "ticking.tick").Hunter, Is.EqualTo(id));
            Assert.That(new WebHitFact(id, noise.Source, 9, 2, .5f, 3f, .5f).SlowStrengthMultiplier, Is.EqualTo(.5f));
        }
    }
}
