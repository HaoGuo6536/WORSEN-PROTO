// ============================================================================
// DebugOverlayHandTests.cs
// ============================================================================
// PURPOSE:
//   Keeps every hand outcome observable per room without gameplay dependencies.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · DebugOverlay.
// KEY RESPONSIBILITIES:
//   - Verify all six states, stable room ordering, stale rejection and floor reset.
// DEPENDENCIES:
//   Core, Run, DebugOverlay, its Orchestrator, NUnit and transient Unity objects.
// USAGE NOTES:
//   Edit Mode, no UI document, engine clock or scene state required.
// ============================================================================
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.DebugOverlay;
using Worsen.Orchestrator;
using Worsen.Session.Run;
using Object = UnityEngine.Object;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.DebugOverlay
{
    public sealed class DebugOverlayHandTests
    {
        [Test]
        public void RoutePairsOnRebindDisableAndClearsAtCaptureBoundary()
        {
            Assert.That(DebugOverlayManager.Instance, Is.Null);
            var root = new GameObject("hand overlay test"); root.SetActive(false);
            var runRoot = new GameObject("hand overlay run"); runRoot.SetActive(false);
            var manager = root.AddComponent<DebugOverlayManager>(); var driver = root.AddComponent<DebugOverlayDriver>();
            var route = root.AddComponent<DebugOverlayOrchestrator>(); var run = runRoot.AddComponent<RunSessionManager>();
            var state = new DebugOverlayDriverState();
            Set(driver, "_state", state); Set(driver, "_presenter", new DebugOverlayPresenter());
            Set(manager, "_driver", driver); Set(manager, "_initialized", true);
            Set(route, "_run", run); Set(route, "_overlay", manager);
            try
            {
                Call(route, "OnEnable"); Call(route, "OnEnable");
                Assert.That(((Delegate)Get(run, "CollapseHandPublished")).GetInvocationList().Length, Is.EqualTo(1));
                ((Action<CollapseHandFact>)Get(run, "CollapseHandPublished"))(new CollapseHandFact(new EntityId(1), 2,
                    CollapseHandEventKind.Grabbed, Vector3.zero, .5f, 0f, 3));
                Assert.That(state.HandsText, Does.Contain("Room 2: Grabbed @ 3"));
                ((Action<RunCaptureMetadata>)Get(run, "CaptureStarted"))(default);
                Assert.That(state.Hands, Is.Empty);
                Call(route, "OnDisable"); Assert.That(Get(run, "CollapseHandPublished"), Is.Null);
                Assert.That(Get(run, "CaptureStarted"), Is.Null);
            }
            finally { Call(route, "OnDisable"); Object.DestroyImmediate(root); Object.DestroyImmediate(runRoot); }
        }
        private static object Get(object o, string n) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        private static void Set(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);
        private static void Call(object o, string n) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, null);

        [TestCase(CollapseHandEventKind.Warning)] [TestCase(CollapseHandEventKind.Grabbed)]
        [TestCase(CollapseHandEventKind.Escaped)] [TestCase(CollapseHandEventKind.Hit)]
        [TestCase(CollapseHandEventKind.Released)] [TestCase(CollapseHandEventKind.Consumed)]
        public void EveryHandStateIsVisiblePerRoomAndClearsOnReset(CollapseHandEventKind kind)
        {
            var state = new DebugOverlayDriverState(); var presenter = new DebugOverlayPresenter();
            presenter.SetCollapseHand(state, new CollapseHandFact(new EntityId(1), 7, kind, Vector3.zero, 1f, 0f, 12));
            presenter.SetCollapseHand(state, new CollapseHandFact(new EntityId(1), 2, CollapseHandEventKind.Warning, Vector3.zero, 1f, 0f, 11));
            Assert.That(state.HandsText, Is.EqualTo("Hands:\nRoom 2: Warning @ 11\nRoom 7: " + kind + " @ 12"));
            presenter.SetCollapseHand(state, new CollapseHandFact(new EntityId(1), 7, CollapseHandEventKind.Warning, Vector3.zero, 1f, 0f, 10));
            Assert.That(state.Hands[7].Kind, Is.EqualTo(kind));
            presenter.SetCollapseHand(state, new CollapseHandFact(new EntityId(1), 7, CollapseHandEventKind.Consumed, Vector3.zero, 1f, 0f, 13));
            presenter.SetCollapseHand(state, new CollapseHandFact(new EntityId(1), 7, CollapseHandEventKind.Hit, Vector3.zero, 1f, 0f, 13));
            Assert.That(state.Hands[7].Kind, Is.EqualTo(CollapseHandEventKind.Consumed), "Nested consumption cannot be overwritten by the outer Hit callback.");
            presenter.ResetHands(state); Assert.That(state.Hands, Is.Empty); Assert.That(state.HandsText, Is.EqualTo("Hands: —"));
        }
    }
}
