// ============================================================================
// LevelTraversalEndpointAuthoringTests.cs
// ============================================================================
// PURPOSE:
//   Verifies explicit landing-pair metadata from the owned authoring helpers and
//   preserves legacy single-target markers independently of graph direction.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Level authoring.
// KEY RESPONSIBILITIES:
//   - Check both authored endpoints for TagArena and FloorLoop waist vaults.
//   - Keep opt-out targets and unrelated marker kinds unchanged.
// DEPENDENCIES:
//   - Core optional endpoint contract, LevelMarker and owned editor builders.
//   - NUnit, UnityEditor serialized fields and temporary Unity scene objects.
// USAGE NOTES:
//   Calls only object-local private authoring helpers through reflection. It never
//   invokes full builders, generates/saves assets, bakes navigation or changes a
//   saved scene. Temporary objects are destroyed in teardown. These are native
//   authoring checks; reverse traversal requires the separate real Run fixture.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;
using Worsen.Editor.Level;

namespace Worsen.Tests.Level
{
    public sealed class LevelTraversalEndpointAuthoringTests
    {
        private GameObject root;
        [SetUp] public void SetUp() { root = new GameObject("[Test] Traversal endpoint authoring"); }
        [TearDown] public void TearDown() { if (root != null) UnityEngine.Object.DestroyImmediate(root); }

        [Test]
        public void LegacyGraphDirectionDoesNotOptInSingleTargetMarkers()
        {
            foreach (var example in new[]
            {
                (201, LevelMarkerKind.VaultSurface, true, new Vector3(-4f, 0f, -4.5f)),
                (106, LevelMarkerKind.OneWayDrop, false, new Vector3(7.5f, 0f, 4f)),
                (999, LevelMarkerKind.VaultSurface, true, new Vector3(3f, 2f, 7f))
            })
            {
                var item = Child("Legacy " + example.Item1);
                var marker = (LevelMarker)Method(typeof(TagArenaLevelSetup), "Marker").Invoke(null, new object[]
                    { item, example.Item1, example.Item2, 2, 0, example.Item3, TraversalAccess.Player, example.Item4, null });
                Assert.That(marker.HasEndpointPair, Is.False);
                Assert.That(marker.Target, Is.EqualTo(example.Item4));
                Assert.That(marker.EndpointA, Is.EqualTo(marker.Target));
                Assert.That(marker.EndpointB, Is.EqualTo(Vector3.zero));
                Assert.That(marker.Capture().Bidirectional, Is.EqualTo(example.Item3));
            }
        }

        [Test]
        public void TagArenaClutterRouteAuthorsThreeExplicitPairsAndLeavesSlideMarkersOptedOut()
        {
            Method(typeof(TagArenaLevelSetup), "BuildPlayerRoute").Invoke(null,
                new object[] { root.transform, null, null, null, null });
            var markers = root.GetComponentsInChildren<LevelMarker>();
            var pairs = markers.Where(marker => marker.HasEndpointPair).OrderBy(marker => marker.SurfaceId).ToArray();
            Assert.That(pairs.Select(marker => marker.SurfaceId).ToArray(), Is.EqualTo(new[] { 210, 211, 212 }));
            foreach (var marker in pairs)
            {
                ITraversalEndpointPair pair = marker;
                float x = marker.transform.position.x;
                Assert.That(pair.EndpointA, Is.EqualTo(new Vector3(x + 1.6f, 0f, 15f)));
                Assert.That(pair.EndpointB, Is.EqualTo(new Vector3(x - 1.6f, 0f, 15f)));
                Assert.That(marker.Target, Is.EqualTo(pair.EndpointA));
                Assert.That(marker.Capture().Bidirectional, Is.True);
                // Turning off the optional adapter never changes the original Target.
                var fields = new SerializedObject(marker);
                fields.FindProperty("_hasEndpointPair").boolValue = false;
                fields.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(pair.HasEndpointPair, Is.False);
                Assert.That(marker.Target, Is.EqualTo(new Vector3(x + 1.6f, 0f, 15f)));
            }
            Assert.That(markers.Count(marker => marker.MarkerKind == LevelMarkerKind.SlideGate), Is.EqualTo(2));
            Assert.That(markers.Where(marker => marker.MarkerKind == LevelMarkerKind.SlideGate)
                .All(marker => !marker.HasEndpointPair), Is.True);
        }

        [Test]
        public void FloorLoopAuthorsSixWaistPairsAndLeavesOtherRecordsOptedOut()
        {
            var records = FloorLoopLevelSetup.CreateMarkerRecords();
            int count = 0;
            foreach (var record in records)
            {
                var item = Child("Floor " + record.Id);
                item.transform.position = record.Position;
                Method(typeof(FloorLoopLevelSetup), "AddMarker").Invoke(null, new object[] { item, record });
                var marker = item.GetComponent<LevelMarker>();
                bool paired = record.Kind == LevelMarkerKind.VaultSurface && record.Id >= 201 && record.Id <= 206;
                Assert.That(marker.HasEndpointPair, Is.EqualTo(paired), "Marker " + record.Id);
                Assert.That(marker.Capture().Bidirectional, Is.EqualTo(record.Bidirectional));
                if (!paired) continue;
                count++;
                float floor = FloorLoopLevelSetup.FloorHeight(record.Position.x);
                Assert.That(marker.EndpointA, Is.EqualTo(new Vector3(record.Position.x, floor, record.Position.z + 1.6f)));
                Assert.That(marker.EndpointB, Is.EqualTo(new Vector3(record.Position.x, floor, record.Position.z - 1.6f)));
                Assert.That(marker.Target, Is.EqualTo(marker.EndpointA));
                var room = records.Single(candidate => candidate.Kind == LevelMarkerKind.Room && candidate.Id == record.RoomId);
                var bounds = new Bounds(room.Position, room.Size);
                Assert.That(bounds.Contains(marker.EndpointA) && bounds.Contains(marker.EndpointB), Is.True);
            }
            Assert.That(count, Is.EqualTo(6));

            var oneWay = new LevelMarkerRecord(201, LevelMarkerKind.VaultSurface, 1, 0,
                new Vector3(-24f, 0.5f, -20f), new Vector3(4f, 1f, 0.8f), false);
            var oneWayObject = Child("Explicit one-way Floor vault");
            Method(typeof(FloorLoopLevelSetup), "AddMarker").Invoke(null, new object[] { oneWayObject, oneWay });
            Assert.That(oneWayObject.GetComponent<LevelMarker>().HasEndpointPair, Is.False);
        }

        private GameObject Child(string name)
        {
            var item = new GameObject("[Test] " + name);
            item.transform.SetParent(root.transform, false);
            return item;
        }
        private static MethodInfo Method(Type owner, string name) => owner.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing owned authoring helper: " + owner.Name + "." + name);
    }
}
