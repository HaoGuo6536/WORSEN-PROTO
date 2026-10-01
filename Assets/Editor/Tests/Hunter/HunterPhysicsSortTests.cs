// ============================================================================
// HunterPhysicsSortTests.cs
// ============================================================================
// PURPOSE:
//   Exercise the actual in-place driver sorts as managed prefix operations.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Hunter managed allocation verification.
// KEY RESPONSIBILITIES:
//   - Require seeded distance ordering and untouched unused buffer slots.
//   - Measure warmed sorts without reflection allocations or native collider lookups.
// DEPENDENCIES:
//   NUnit, managed reflection/data shells, RaycastHit values and Hunter driver sorts.
// USAGE NOTES:
//   Uninitialized driver shells never enter Unity lifecycle; only their passive state is set.
//   Distances are unique here; collider-ID ties are covered by HunterPhysicsAllocationTests.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Hunter;

namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterPhysicsSortTests
    {
        [TestCase("Hunter", 7)] [TestCase("Hunter", 77)]
        [TestCase("Attack", 7)] [TestCase("Attack", 77)]
        [TestCase("Weaver", 7)] [TestCase("Weaver", 77)]
        public void SeededPopulatedPrefixSortsInPlaceWithNoWarmedAllocations(string owner, int seed)
        {
            const int count = 64;
            var hits = new RaycastHit[count + 1];
            var random = new System.Random(seed);
            int[] shuffled = Enumerable.Range(1, count).OrderBy(_ => random.Next()).ToArray();
            for (int i = 0; i < count; i++) hits[i].distance = shuffled[i];
            hits[count].distance = -100f;
            Type type;
            object state;
            switch (owner)
            {
                case "Hunter": type = typeof(HunterDriver); state = new HunterDriverState { QueryHits = hits }; break;
                case "Attack": type = typeof(HunterAttackDriver); state = new HunterAttackDriverState { QueryHits = hits }; break;
                case "Weaver": type = typeof(WeaverWebDriver); state = new WeaverDriverState { QueryHits = hits }; break;
                default: throw new ArgumentException(owner);
            }
            object driver = FormatterServices.GetUninitializedObject(type);
            type.GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(driver, state);
            var sort = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), driver,
                type.GetMethod("SortHits", BindingFlags.Instance | BindingFlags.NonPublic));
            sort(count);
            for (int i = 0; i < count; i++) Assert.That(hits[i].distance, Is.EqualTo(i + 1));
            Assert.That(hits[count].distance, Is.EqualTo(-100f), "Unused slots must not participate in sorting.");
            for (int warm = 0; warm < 32; warm++) { Reverse(hits, count); sort(count); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 100; iteration++) { Reverse(hits, count); sort(count); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            for (int i = 0; i < count; i++) Assert.That(hits[i].distance, Is.EqualTo(i + 1));
            Assert.That(hits[count].distance, Is.EqualTo(-100f));
            sort(0); sort(1);
            Assert.That(hits[count].distance, Is.EqualTo(-100f));
        }
        private static void Reverse(RaycastHit[] hits, int count)
        {
            for (int i = 0; i < count / 2; i++)
            { RaycastHit value = hits[i]; hits[i] = hits[count - 1 - i]; hits[count - 1 - i] = value; }
        }
    }
}
