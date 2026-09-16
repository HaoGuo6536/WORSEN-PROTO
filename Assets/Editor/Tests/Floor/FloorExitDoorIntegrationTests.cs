// ============================================================================
// FloorExitDoorIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Checks generated physical panels, opening/trigger agreement, callback crossing and reset in the engine.
//   The doorway distinguishes collecting the last cake from deliberately leaving.
//   Explicit timing and crossing observations keep the transition reproducible.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Keep visible door movement and physical passage in agreement.
//   - Prevent a stationary overlap from becoming an accidental floor transition.
// DEPENDENCIES:
//   - Core shared values and Floor-owned visual configuration only.
// USAGE NOTES:
//   Scene-owned through FloorDriver. Session supplies elapsed time; no Update loop.
//   No global settings. Reinitialization clears crossing and opening state.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Floor
{
    public sealed class FloorExitDoorIntegrationTests
    {
        [Test] public void ClosedDoorHasPhysicalLeavesAndDisablesThresholdUntilSwingFinishes()
        {
            using(var f=new Fixture())
            {
                var trigger=f.Door.GetComponent<BoxCollider>();
                Assert.That(trigger.enabled,Is.False);
                Physics.SyncTransforms();
                Assert.That(Physics.Raycast(f.Origin+new Vector3(0.25f,1.5f,-1f),Vector3.forward,2f,~0,QueryTriggerInteraction.Ignore),Is.True);
                var hinges=f.Root.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("Hinged Door")).ToArray();
                Assert.That(hinges.Length,Is.EqualTo(2));
                var leaves=f.Root.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Door Leaf Collision").ToArray();
                Assert.That(leaves.Length,Is.EqualTo(2));Assert.That(leaves.All(c=>c.enabled&&!c.isTrigger),Is.True);
                f.Driver.OpenExit(Array.Empty<LevelAnchor>());
                f.Driver.TickWarnings(0.6f);
                Assert.That(trigger.enabled,Is.False);Assert.That(f.Door.Opening,Is.True);
                Assert.That(Quaternion.Angle(Quaternion.identity,hinges[0].localRotation),Is.GreaterThan(1f));
                f.Driver.TickWarnings(1.2f);
                Assert.That(trigger.enabled,Is.True);Assert.That(f.Door.FullyOpen,Is.True);
                Assert.That(leaves.All(c=>c.enabled),Is.True,"Physical leaves remain aligned with visible leaves.");
                Assert.That(Physics.Raycast(f.Origin+new Vector3(0.25f,1.5f,-1f),Vector3.forward,2f,~0,QueryTriggerInteraction.Ignore),Is.False);
                Assert.That(Quaternion.Angle(Quaternion.identity,hinges[0].localRotation),Is.EqualTo(100f).Within(0.001f));
            }
        }
        [Test] public void OpeningNearbyRequiresLaterMovementThroughDoorAndRoutesContactOnce()
        {
            using(var f=new Fixture())
            {
                int contacts=0;f.Driver.ExitContact+=_=>contacts++;
                f.Actor.transform.position=f.Origin+new Vector3(0f,1f,-0.6f);
                Physics.SyncTransforms();Invoke(f.Door,"OnTriggerEnter",f.Actor.GetComponent<Collider>());
                Assert.That(contacts,Is.Zero);
                f.Driver.OpenExit(Array.Empty<LevelAnchor>());f.Driver.TickWarnings(1.2f);
                Invoke(f.Door,"OnTriggerEnter",f.Actor.GetComponent<Collider>());
                for(int i=0;i<4;i++)Invoke(f.Door,"OnTriggerStay",f.Actor.GetComponent<Collider>());
                Assert.That(contacts,Is.Zero);
                f.Actor.transform.position=f.Origin+new Vector3(0f,1f,0.6f);Physics.SyncTransforms();
                Invoke(f.Door,"OnTriggerStay",f.Actor.GetComponent<Collider>());
                Invoke(f.Door,"OnTriggerStay",f.Actor.GetComponent<Collider>());
                Assert.That(contacts,Is.EqualTo(1));
            }
        }
        [Test] public void ReinitializationReturnsClosedDoorAndClearsPreviousOpening()
        {
            using(var f=new Fixture())
            {
                f.Driver.OpenExit(Array.Empty<LevelAnchor>());f.Driver.TickWarnings(1.2f);
                var old=f.Door;Assert.That(old.FullyOpen,Is.True);
                f.Initialize();Assert.That(old==null,Is.True);
                Assert.That(f.Door.FullyOpen,Is.False);Assert.That(f.Door.GetComponent<BoxCollider>().enabled,Is.False);
            }
        }
        private static void Invoke(FloorExitDoor door,string method,Collider actor)
        {typeof(FloorExitDoor).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(door,new object[]{actor});}
        private sealed class Fixture:IDisposable
        {
            public readonly Vector3 Origin=new Vector3(45000f,0f,45000f);
            public readonly GameObject Root,Actor;public readonly FloorDriver Driver;
            public FloorExitDoor Door=>Root.GetComponentInChildren<FloorExitDoor>();
            private readonly FloorDriverConfig _config;
            private readonly LevelGraph _graph;
            public Fixture()
            {
                _config=ScriptableObject.CreateInstance<FloorDriverConfig>();
                typeof(FloorDriverConfig).GetField("_usePhysicalExitDoor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(_config,true);
                Root=new GameObject("Physical Exit Fixture");Driver=Root.AddComponent<FloorDriver>();
                typeof(FloorDriver).GetField("_config",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Driver,_config);
                Actor=new GameObject("Door Crossing Actor");Actor.AddComponent<CapsuleCollider>();
                _graph=LevelGraphUtility.Build(new[]{new LevelRoom(1,Origin+Vector3.up*2f,new Vector3(12f,4f,12f))},
                    Array.Empty<LevelEdge>(),new[]{new LevelAnchor(1,1,CakeAnchorType.Flow,Origin+Vector3.left*3f)},1,Origin);
                Initialize();
            }
            public void Initialize()=>Driver.Initialize(_graph,Array.Empty<LevelAnchor>());
            public void Dispose()
            {Driver.Teardown();Object.DestroyImmediate(Root);Object.DestroyImmediate(Actor);Object.DestroyImmediate(_config);}
        }
    }
}
