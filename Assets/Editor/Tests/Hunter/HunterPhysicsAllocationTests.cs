// ============================================================================
// HunterPhysicsAllocationTests.cs
// ============================================================================
// PURPOSE:
//   Compare pooled driver query prefixes with allocating PhysX queries on seeded layouts.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Hunter physics integration.
// KEY RESPONSIBILITIES:
//   - Exercise every Hunter cast/overlap helper at exact and repeated saturation.
//   - Verify distance/instance-ID ordering, complete hits and shorter-query reuse.
//   - Require one missing-shader error and an explicitly referenced fallback shader.
// DEPENDENCIES:
//   NUnit, Unity physics/editor, Hunter drivers and test-only layout/reflection helpers.
// USAGE NOTES:
//   Edit Mode engine tests; no navigation bake, assets, scenes or global physics changes.
//   Reflection reaches private queries to prove every hit, not only the first blocker.
// ============================================================================
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Stare;
using Worsen.Domain.Hunter.Archetypes.Ticking;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterPhysicsAllocationTests
    {
        [TestCase("Hunter", "Ray", 7, false)] [TestCase("Hunter", "Ray", 77, true)]
        [TestCase("Hunter", "Capsule", 7, false)] [TestCase("Hunter", "Capsule", 77, true)]
        [TestCase("Attack", "Ray", 7, false)] [TestCase("Attack", "Ray", 77, true)]
        [TestCase("Attack", "Sphere", 7, false)] [TestCase("Attack", "Sphere", 77, true)]
        [TestCase("Weaver", "Sphere", 7, false)] [TestCase("Weaver", "Sphere", 77, true)]
        [TestCase("Ticking", "Sphere", 7, false)] [TestCase("Ticking", "Sphere", 77, true)]
        public void CastSaturationReturnsEveryLegacyHitAndPreservesSortedOrdering(string owner, string shape, int seed, bool repeated)
        {
            using (var layout = new PhysicsAllocationLayout())
            {
                Component driver = CreateDriver(layout, owner);
                int capacity = PhysicsAllocationLayout.ReplaceBuffer<RaycastHit>(driver, "QueryHits");
                int population = repeated ? capacity * 2 + 1 : capacity;
                layout.CastBoxes(population, seed);
                Physics.SyncTransforms();
                RaycastHit[] expected = layout.LegacyCast(shape);
                Assert.That(expected.Length, Is.EqualTo(population));
                layout.ExpectGrowth(owner == "Attack" ? "Hunter attack" : owner, repeated ? 2 : 1);
                int count = Cast(driver, owner, shape, layout);
                Assert.That(count, Is.EqualTo(expected.Length));
                Assert.That(PhysicsAllocationLayout.Buffer<RaycastHit>(driver, "QueryHits").Length, Is.GreaterThan(capacity));
                CompareHits(driver, owner, expected, count);

                RaycastHit[] retained = PhysicsAllocationLayout.Buffer<RaycastHit>(driver, "QueryHits");
                layout.KeepFirst(4); Physics.SyncTransforms();
                expected = layout.LegacyCast(shape);
                count = Cast(driver, owner, shape, layout);
                CompareHits(driver, owner, expected, count);
                Assert.That(PhysicsAllocationLayout.Buffer<RaycastHit>(driver, "QueryHits"), Is.SameAs(retained));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [TestCase("Hunter", "Sphere", 7)] [TestCase("Hunter", "Capsule", 77)]
        [TestCase("Attack", "Capsule", 7)]
        [TestCase("Weaver", "Sphere", 77)] [TestCase("Weaver", "Capsule", 7)]
        [TestCase("Ticking", "Sphere", 77)] [TestCase("Stare", "Capsule", 7)]
        public void OverlapRepeatedSaturationReturnsAllCollidersAndReusesBuffer(string owner, string shape, int seed)
        {
            using (var layout = new PhysicsAllocationLayout())
            {
                Component driver = CreateDriver(layout, owner);
                int capacity = PhysicsAllocationLayout.ReplaceBuffer<Collider>(driver, "QueryOverlaps");
                layout.OverlapBoxes(capacity * 2 + 1, seed);
                Physics.SyncTransforms();
                Collider[] expected = layout.LegacyOverlap(shape);
                Assert.That(expected.Length, Is.EqualTo(capacity * 2 + 1));
                layout.ExpectGrowth(owner == "Attack" ? "Hunter attack" : owner, 2);
                int count = Overlap(driver, owner, shape, layout);
                Assert.That(PhysicsAllocationLayout.Buffer<Collider>(driver, "QueryOverlaps").Take(count), Is.EquivalentTo(expected));
                Assert.That(count, Is.EqualTo(expected.Length));
                Collider[] retained = PhysicsAllocationLayout.Buffer<Collider>(driver, "QueryOverlaps");
                Assert.That(retained.Length, Is.GreaterThan(capacity));
                layout.KeepFirst(4); Physics.SyncTransforms();
                count = Overlap(driver, owner, shape, layout);
                Assert.That(PhysicsAllocationLayout.Buffer<Collider>(driver, "QueryOverlaps").Take(count), Is.EquivalentTo(layout.LegacyOverlap(shape)));
                Assert.That(count, Is.EqualTo(4));
                Assert.That(PhysicsAllocationLayout.Buffer<Collider>(driver, "QueryOverlaps"), Is.SameAs(retained));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void MissingFallbackShaderReportsOneErrorAcrossWarningsAndVisuals(bool configPresent)
        {
            using (var layout = new PhysicsAllocationLayout())
            {
                var driver = layout.Root.AddComponent<HunterAttackDriver>();
                if (configPresent) PhysicsAllocationLayout.Set(driver, "_config", layout.Config<HunterAttackDriverConfig>());
                driver.Initialize();
                LogAssert.Expect(LogType.Error, "HunterAttackDriver requires a serialized fallback shader on HunterAttackDriverConfig; run the Hunter horror roster setup.");
                for (int serial = 1; serial <= 3; serial++)
                {
                    driver.BeginWarning(HunterAttackStyle.Projectile, serial, layout.Root.transform.position + Vector3.forward * 5f, 10f, .1f, false, false);
                    driver.Fire(10f, .1f);
                }
                Assert.That(PhysicsAllocationLayout.Get(PhysicsAllocationLayout.Get(driver, "_state"), "FallbackMaterial"), Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [Test]
        public void SerializedFallbackShaderCreatesOneOwnedMaterialWithoutLookup()
        {
            using (var layout = new PhysicsAllocationLayout())
            {
                var driver = (HunterAttackDriver)CreateDriver(layout, "Attack");
                var config = (HunterAttackDriverConfig)PhysicsAllocationLayout.Get(driver, "_config");
                Shader shader = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat").shader;
                PhysicsAllocationLayout.Set(config, "_fallbackShader", shader);
                var first = (Material)PhysicsAllocationLayout.Call(driver, "Fallback");
                Assert.That(first.shader, Is.SameAs(shader));
                Assert.That(PhysicsAllocationLayout.Call(driver, "Fallback"), Is.SameAs(first));
                driver.Teardown();
                Assert.That(first == null, Is.True, "Only the driver-owned material is destroyed.");
                Assert.That(config.FallbackShader, Is.SameAs(shader));
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static Component CreateDriver(PhysicsAllocationLayout layout, string owner)
        {
            switch (owner)
            {
                case "Hunter":
                    var hunter = layout.Root.AddComponent<HunterDriver>();
                    hunter.Initialize(layout.Motor()); return hunter;
                case "Attack":
                    var attack = layout.Root.AddComponent<HunterAttackDriver>();
                    var attacks = layout.Config<HunterAttackDriverConfig>();
                    PhysicsAllocationLayout.Set(attacks, "_collisionMask", (LayerMask)PhysicsAllocationLayout.Mask);
                    PhysicsAllocationLayout.Set(attack, "_config", attacks); attack.Initialize(); return attack;
                case "Weaver":
                    layout.Root.AddComponent<CapsuleCollider>();
                    var weaver = layout.Root.AddComponent<WeaverWebDriver>();
                    weaver.Initialize(layout.Config<WeaverDriverConfig>(), layout.Motor()); return weaver;
                case "Ticking":
                    var ticking = layout.Root.AddComponent<TickingDriver>();
                    var config = layout.Config<TickingDriverConfig>();
                    PhysicsAllocationLayout.Set(config, "_keyPrefab", layout.Object("Key prefab"));
                    PhysicsAllocationLayout.Set(config, "_obstacleMask", (LayerMask)PhysicsAllocationLayout.Mask);
                    PhysicsAllocationLayout.Set(config, "_clearanceRadius", PhysicsAllocationLayout.Radius);
                    ticking.Initialize(config); return ticking;
                case "Stare":
                    var stare = layout.Root.AddComponent<StareDriver>(); stare.Initialize(); return stare;
                default: throw new ArgumentException(owner);
            }
        }
        private static int Cast(Component driver, string owner, string shape, PhysicsAllocationLayout layout)
        {
            if (owner == "Attack") return (int)PhysicsAllocationLayout.Call(driver, "CastQuery", layout.Origin, Vector3.forward,
                PhysicsAllocationLayout.Distance, shape == "Sphere" ? PhysicsAllocationLayout.Radius : 0f);
            if (shape == "Ray") return (int)PhysicsAllocationLayout.Call(driver, "RayQuery", layout.Origin, Vector3.forward,
                PhysicsAllocationLayout.Distance, PhysicsAllocationLayout.Mask);
            if (shape == "Capsule") return (int)PhysicsAllocationLayout.Call(driver, "CapsuleQuery", layout.Low, layout.High,
                PhysicsAllocationLayout.Radius, Vector3.forward, PhysicsAllocationLayout.Distance);
            if (owner == "Ticking") return (int)PhysicsAllocationLayout.Call(driver, "SphereQuery", layout.Origin, Vector3.forward, PhysicsAllocationLayout.Distance);
            return (int)PhysicsAllocationLayout.Call(driver, "SphereQuery", layout.Origin, PhysicsAllocationLayout.Radius, Vector3.forward, PhysicsAllocationLayout.Distance);
        }
        private static int Overlap(Component driver, string owner, string shape, PhysicsAllocationLayout layout)
        {
            if (shape == "Sphere")
                return (int)(owner == "Ticking" ? PhysicsAllocationLayout.Call(driver, "SphereOverlap", layout.Origin) :
                    PhysicsAllocationLayout.Call(driver, "SphereOverlap", layout.Origin, PhysicsAllocationLayout.Radius));
            if (owner == "Stare") return (int)PhysicsAllocationLayout.Call(driver, "CapsuleOverlap", layout.Low, layout.High,
                PhysicsAllocationLayout.Radius, PhysicsAllocationLayout.Mask);
            return (int)PhysicsAllocationLayout.Call(driver, "CapsuleOverlap", layout.Low, layout.High, PhysicsAllocationLayout.Radius);
        }
        private static void CompareHits(Component driver, string owner, RaycastHit[] expected, int count)
        {
            RaycastHit[] actual = PhysicsAllocationLayout.Buffer<RaycastHit>(driver, "QueryHits");
            if (owner != "Ticking")
            {
                PhysicsAllocationLayout.Call(driver, "SortHits", count);
                PhysicsAllocationLayout.AssertOrdered(expected, actual, count);
            }
            else Assert.That(actual.Take(count).Select(hit => hit.collider), Is.EquivalentTo(expected.Select(hit => hit.collider)));
        }
    }

    // Test-only owner of engine objects and pooled buffer replacement; no production API expansion.
    internal sealed class PhysicsAllocationLayout : IDisposable
    {
        internal const int Mask = 1 << 30;
        internal const float Radius = .05f, Distance = 40f;
        internal readonly Vector3 Origin = new Vector3(8100f, 800f, 8100f);
        internal Vector3 Low => Origin - Vector3.up * .1f;
        internal Vector3 High => Origin + Vector3.up * .1f;
        internal readonly GameObject Root;
        private readonly List<Object> owned = new List<Object>();
        private readonly List<Collider> boxes = new List<Collider>();
        internal PhysicsAllocationLayout()
        { Root = Object("Query owner"); Root.transform.position = Origin + Vector3.left * 100f; }
        internal GameObject Object(string label)
        { var value = new GameObject(label); owned.Add(value); return value; }
        internal T Config<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
        internal HunterMotorDriverConfig Motor()
        {
            var value = Config<HunterMotorDriverConfig>();
            Set(value, "_collisionMask", (LayerMask)Mask); Set(value, "_sightMask", (LayerMask)Mask); return value;
        }
        internal void CastBoxes(int count, int seed)
        {
            var random = new System.Random(seed);
            // Paired identical geometry guarantees distance ties; shuffled creation breaks spatial ID order.
            int[] order = Enumerable.Range(0, count).OrderBy(_ => random.Next()).ToArray();
            foreach (int index in order) Box(Origin + Vector3.forward * (2f + index / 2 * .4f), new Vector3(.2f, .6f, .2f));
            Box(Origin + Vector3.forward, Vector3.one * .2f, true);
        }
        internal void OverlapBoxes(int count, int seed)
        {
            var random = new System.Random(seed);
            for (int i = 0; i < count; i++) Box(Origin + new Vector3((float)random.NextDouble() * .02f,
                (float)random.NextDouble() * .02f, (float)random.NextDouble() * .02f), Vector3.one * .02f);
            Box(Origin, Vector3.one * .02f, true);
        }
        private void Box(Vector3 position, Vector3 size, bool trigger = false)
        {
            var item = Object("Seeded collider"); item.layer = 30; item.transform.position = position;
            var box = item.AddComponent<BoxCollider>(); box.size = size; box.isTrigger = trigger;
            if (!trigger) boxes.Add(box);
        }
        internal void KeepFirst(int count)
        { for (int i = count; i < boxes.Count; i++) boxes[i].enabled = false; }
        internal RaycastHit[] LegacyCast(string shape)
        {
            switch (shape)
            {
                case "Ray": return Physics.RaycastAll(Origin, Vector3.forward, Distance, Mask, QueryTriggerInteraction.Ignore);
                case "Sphere": return Physics.SphereCastAll(Origin, Radius, Vector3.forward, Distance, Mask, QueryTriggerInteraction.Ignore);
                case "Capsule": return Physics.CapsuleCastAll(Low, High, Radius, Vector3.forward, Distance, Mask, QueryTriggerInteraction.Ignore);
                default: throw new ArgumentException(shape);
            }
        }
        internal Collider[] LegacyOverlap(string shape) => shape == "Sphere"
            ? Physics.OverlapSphere(Origin, Radius, Mask, QueryTriggerInteraction.Ignore)
            : Physics.OverlapCapsule(Low, High, Radius, Mask, QueryTriggerInteraction.Ignore);
        internal void ExpectGrowth(string label, int count)
        { for (int i = 0; i < count; i++) LogAssert.Expect(LogType.Warning, new Regex("^" + label + " physics query buffer saturated; grew from [0-9]+ to [0-9]+ and retrying[.]$")); }
        internal static object Get(object target, string field) => Field(target, field).GetValue(target);
        internal static void Set(object target, string field, object value) => Field(target, field).SetValue(target, value);
        private static FieldInfo Field(object target, string field) => target.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingFieldException(target.GetType().FullName, field);
        internal static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        internal static T[] Buffer<T>(Component driver, string field) => (T[])Get(Get(driver, "_state"), field);
        internal static int ReplaceBuffer<T>(Component driver, string field)
        {
            object state = Get(driver, "_state");
            T[] old = (T[])Get(state, field);
            if (old != null) ArrayPool<T>.Shared.Return(old, true);
            T[] buffer = ArrayPool<T>.Shared.Rent(16); Set(state, field, buffer); return buffer.Length;
        }
        internal static void AssertOrdered(RaycastHit[] expected, RaycastHit[] actual, int count)
        {
            Array.Sort(expected, (a, b) => { int distance = a.distance.CompareTo(b.distance);
                return distance != 0 ? distance : a.collider.GetInstanceID().CompareTo(b.collider.GetInstanceID()); });
            Assert.That(count, Is.EqualTo(expected.Length));
            for (int i = 0; i < count; i++)
            {
                Assert.That(actual[i].collider, Is.SameAs(expected[i].collider), "ordered collider " + i);
                Assert.That(actual[i].distance, Is.EqualTo(expected[i].distance));
                Assert.That(actual[i].point, Is.EqualTo(expected[i].point));
                Assert.That(actual[i].normal, Is.EqualTo(expected[i].normal));
            }
        }
        public void Dispose()
        {
            // Destroy the driver before its configs; OnDestroy returns all currently owned buffers.
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
        }
    }
}
