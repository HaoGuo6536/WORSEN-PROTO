// ============================================================================
// HorrorMicroEventRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Exercises real door application and the collision-free silhouette lifetime.
//   Outcome facts distinguish committed world changes from unavailable integrations.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror.
// KEY RESPONSIBILITIES:
//   - Verify reopenable Level closure, telemetry facts, expiry and chase cleanup.
// DEPENDENCIES:
//   - Core, Domain Level, Horror presentation, HorrorOrchestrator and NUnit.
// USAGE NOTES:
//   Edit Mode with disposable objects. No loaded scene or navigation baking is required.
// ============================================================================
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Orchestrator;
using Worsen.Presentation.Horror;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Horror
{
    public sealed class HorrorMicroEventRoutingTests
    {
        [Test] public void DoorApplicationPublishesActualOutcomeAndDoorRemainsReopenable()
        {
            var world = new GameObject("Micro-event world");
            var owner = new GameObject("Micro-event routing"); owner.SetActive(false);
            try
            {
                var level = world.AddComponent<LevelManager>();
                level.InitializeGenerated(LevelGraphUtility.Build(new[] {
                    new LevelRoom(1, new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f)),
                    new LevelRoom(2, new Vector3(12f, 2f, 0f), new Vector3(12f, 4f, 12f)) },
                    new[] { new LevelEdge(11, 1, 2, true, TraversalAccess.All) }, Array.Empty<LevelAnchor>(), 1, Vector3.zero),
                    new[] { new InteractableState(101, InteractableKind.Door, 1, Vector3.right * 6f, InteractableStateValue.Open, 11) });
                var horror = owner.AddComponent<HorrorManager>(); var route = owner.AddComponent<HorrorOrchestrator>();
                route.Configure(null, null, null, horror); route.ConfigureMicroEvents(level, Array.Empty<Vector3>());
                int facts = 0; bool applied = false;
                horror.MicroEventOccurred += (kind, id, position, seconds, success) => { facts++; applied = success; };
                route.OnMicroEventSelected(1, 101, Vector3.right * 6f, 0f);
                Assert.That(facts, Is.EqualTo(1)); Assert.That(applied, Is.True);
                Assert.That(level.ClosedDoors[11], Is.True);
                Assert.That(level.OpenDoor(101), Is.True);
                route.OnMicroEventSelected(1, 999, Vector3.zero, 0f);
                Assert.That(facts, Is.EqualTo(2)); Assert.That(applied, Is.False);
                route.OnMicroEventSelected(3, 0, Vector3.zero, .7f);
                Assert.That(facts, Is.EqualTo(3)); Assert.That(applied, Is.False, "Unwired HUD cannot be reported as displayed.");
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(world); }
        }

        [Test] public void SilhouetteHasNoCollisionExpiresAndClearsOnChaseOrFloorReset()
        {
            var owner = new GameObject("Micro-event silhouette");
            var config = ScriptableObject.CreateInstance<HorrorDriverConfig>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                var driver = owner.AddComponent<HorrorMicroEventDriver>();
                driver.ResetRun(config, new System.Random(22));
                var state = (HorrorMicroEventDriverState)typeof(HorrorMicroEventDriver)
                    .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
                Assert.That(driver.ShowSilhouette(Vector3.forward * 10f, 1f, config, material), Is.False);
                driver.ObserveProximity(new ProximitySample(new EntityId(1), default, 1, 0, 20f, 0f, false));
                Assert.That(driver.ShowSilhouette(Vector3.forward * 10f, 1f, config, material), Is.True);
                Assert.That(state.Silhouette.GetComponent<Collider>().enabled, Is.False);
                driver.Tick(config, null, .5d, .5f); Assert.That(state.Silhouette, Is.Not.Null);
                driver.Tick(config, null, 1d, .5f); Assert.That(state.Silhouette, Is.Null);
                Assert.That(driver.ShowSilhouette(Vector3.forward * 10f, 1f, config, material), Is.True);
                driver.SetChase(1, true); Assert.That(state.Silhouette, Is.Null);
                Assert.That(driver.ShowSilhouette(Vector3.forward * 10f, 1f, config, material), Is.False);
                state.Used = 1; state.LastSeconds = 60d; double next = state.NextSeconds;
                driver.ObservePlayerOpenedDoor(101, new Bounds(Vector3.back * 10f, Vector3.one));
                driver.ResetFloor();
                Assert.That(state.OpenedDoors, Is.Empty); Assert.That(state.ChaseKnown, Is.False);
                Assert.That(state.Used, Is.EqualTo(1)); Assert.That(state.LastSeconds, Is.EqualTo(60d));
                Assert.That(state.NextSeconds, Is.EqualTo(next));
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(material); Object.DestroyImmediate(config); }
        }
    }
}
