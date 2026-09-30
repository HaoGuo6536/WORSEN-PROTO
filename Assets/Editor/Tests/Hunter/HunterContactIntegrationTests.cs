// ============================================================================
// HunterContactIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the silent-contact and Mannequin catch facts at the Manager boundary.
//   Distant transient objects avoid modifying the coordinator's scene assets.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify native body-contact relay, target filtering and collider exclusions.
//   - Verify distinct Mannequin catch identity, idempotence and life reset.
// DEPENDENCIES:
//   - Hunter Manager/Driver, Core values, Player fixtures, Unity Physics and NUnit.
// USAGE NOTES:
//   Requires coordinator Unity execution; never counted as a headless pure pass.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Mannequin;
using Worsen.Domain.Hunter.Archetypes.Skip;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterContactIntegrationTests
    {
        [Test] public void SkipBodyContactRelaysNormalHitOnceAndHonoursExcludedPlayerCollider()
        {
            var root = new GameObject("Skip contact fixture"); var target = new GameObject("Skip target");
            var profile = ScriptableObject.CreateInstance<HunterProfile>(); var rules = ScriptableObject.CreateInstance<SkipConfig>();
            var motor = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            var hits = new List<HunterHit>(); HunterManager manager = null;
            try
            {
                root.transform.position = new Vector3(5700, 100, 5700);
                target.transform.position = root.transform.position + Vector3.up * .5f;
                var handle = target.AddComponent<TickingTestEntityHandle>(); handle.Value = new EntityId(1);
                var collider = target.AddComponent<BoxCollider>();
                EchoControllerTests.Tune(profile, "_archetypeRules", rules); EchoControllerTests.Tune(profile, "_motorOverride", motor);
                manager = root.AddComponent<HunterManager>();
                manager.Initialize(profile, new EntityContext(new EntityId(-1), new System.Random(3)),
                    new PlayerBehaviorState { Id = handle.Id, Health = 100, Position = target.transform.position }, new EchoControllerTests.World());
                manager.OnLungeHit += hits.Add;
                var driver = root.GetComponent<HunterDriver>(); Physics.SyncTransforms();
                collider.excludeLayers = 1 << root.layer;
                driver.ProbeBodyContact(); Assert.That(hits, Is.Empty);
                collider.excludeLayers = 0;
                handle.Value = new EntityId(2); driver.ProbeBodyContact(); Assert.That(hits, Is.Empty);
                handle.Value = new EntityId(1); driver.ProbeBodyContact(); driver.ProbeBodyContact();
                Assert.That(hits.Count, Is.EqualTo(1)); Assert.That(hits[0].Hunter, Is.EqualTo(manager.Id));
                Assert.That(hits[0].Damage, Is.EqualTo(profile.LungeDamage)); Assert.That(hits[0].Source, Is.EqualTo(HitSource.Lunge));
                Assert.That(hits[0].Severity, Is.EqualTo(HitSeverity.Heavy));
            }
            finally
            {
                if (manager != null) manager.OnLungeHit -= hits.Add;
                Object.DestroyImmediate(root); Object.DestroyImmediate(target);
                Object.DestroyImmediate(profile); Object.DestroyImmediate(rules); Object.DestroyImmediate(motor);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void OnlyMannequinPublishesOneDistinctCatchPerLife(bool mannequin)
        {
            var root = new GameObject("Mannequin catch fixture"); var profile = ScriptableObject.CreateInstance<HunterProfile>();
            var motor = ScriptableObject.CreateInstance<HunterMotorDriverConfig>();
            var rules = ScriptableObject.CreateInstance<MannequinConfig>();
            HunterManager manager = null; int count = 0; EntityId caught = default; Vector3 position = default; long tick = -1;
            Action<EntityId, Vector3, long> receive = (id, point, at) => { count++; caught = id; position = point; tick = at; };
            try
            {
                root.transform.position = new Vector3(5800, 100, 5800);
                if (mannequin) EchoControllerTests.Tune(profile, "_archetypeRules", rules);
                EchoControllerTests.Tune(profile, "_motorOverride", motor);
                var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100 };
                var world = new EchoControllerTests.World(); manager = root.AddComponent<HunterManager>();
                manager.Initialize(profile, new EntityContext(new EntityId(-1), new System.Random(7)), player, world);
                manager.OnMannequinCatch += receive;
                manager.BeginCatch(Vector3.zero); manager.BeginCatch(Vector3.zero);
                Assert.That(count, Is.EqualTo(mannequin ? 1 : 0));
                if (mannequin)
                { Assert.That(caught, Is.EqualTo(manager.Id)); Assert.That(position, Is.EqualTo(root.transform.position)); Assert.That(tick, Is.Zero); }
                manager.EndCatch(); manager.BeginCatch(Vector3.zero);
                Assert.That(count, Is.EqualTo(mannequin ? 1 : 0));
                manager.Initialize(profile, new EntityContext(new EntityId(-2), new System.Random(7)), player, world);
                manager.BeginCatch(Vector3.zero); Assert.That(count, Is.EqualTo(mannequin ? 2 : 0));
            }
            finally
            {
                if (manager != null) manager.OnMannequinCatch -= receive;
                Object.DestroyImmediate(root); Object.DestroyImmediate(profile); Object.DestroyImmediate(motor); Object.DestroyImmediate(rules);
            }
        }
    }
}
