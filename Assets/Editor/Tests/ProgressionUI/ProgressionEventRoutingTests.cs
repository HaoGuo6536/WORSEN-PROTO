// ============================================================================
// ProgressionEventRoutingTests.cs
// ============================================================================
// PURPOSE:
//   Verifies progression publication through the real manager and interface router.
//   Inactive temporary objects avoid persistence and native UI creation while the
//   normal commands, subscription methods and pure Presenter remain in use.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · ProgressionUI.
// KEY RESPONSIBILITIES:
//   - Set and clear Hidden Count from accepted choices and fresh-run snapshots.
//   - Publish each automatic event once before the affected generation is requested.
// DEPENDENCIES:
//   - NUnit, Core, Progression, ProgressionUI and the ProgressionUI Orchestrator.
// USAGE NOTES:
//   Edit Mode; no assets or scenes are saved. The canonical singleton is restored.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression;
using Worsen.Presentation.ProgressionUI;
using Worsen.Orchestrator;
using Object = UnityEngine.Object;
namespace Worsen.Tests.ProgressionUI
{
    public sealed class ProgressionEventRoutingTests
    {
        [Test] public void HiddenCountTracksAcceptedEffectsAndClearsOnRestartWithPairedTeardown()
        {
            WithManager((manager, config, catalogue) =>
            {
                var root = new GameObject("Hidden Count routing"); root.SetActive(false);
                var routeRoot = new GameObject("Hidden Count route"); routeRoot.SetActive(false);
                var route = routeRoot.AddComponent<ProgressionUIOrchestrator>();
                try
                {
                    var ui = root.AddComponent<ProgressionUIManager>(); var driver = root.AddComponent<ProgressionUIDriver>();
                    var view = new ProgressionUIDriverState();
                    Set(driver, "_state", view); Set(driver, "_presenter", new ProgressionUIPresenter()); Set(ui, "_driver", driver);
                    route.Configure(manager, ui); Invoke(route, "OnEnable");
                    manager.StartRun(73);
                    Assert.That(view.HideActiveHunters, Is.False);
                    manager.ChooseThreat("weaver", manager.Snapshot.Revision);
                    Assert.That(manager.ChooseCurse("hidden-count", manager.Snapshot.Revision), Is.True);
                    Assert.That(view.HideActiveHunters, Is.True); Assert.That(view.RetainedText, Does.Not.Contain("Weaver"));
                    manager.StartRun(73); Assert.That(view.HideActiveHunters, Is.False);
                    manager.ChooseThreat("weaver", manager.Snapshot.Revision);
                    Assert.That(view.RetainedText, Does.Contain("Weaver"));
                    Invoke(route, "OnDisable");
                    Assert.That(Field(manager, "SnapshotChanged").GetValue(manager), Is.Null);
                    Assert.That(Field(manager, "TransactionCommitted").GetValue(manager), Is.Null);
                }
                finally { Invoke(route, "OnDisable"); Object.DestroyImmediate(routeRoot); Object.DestroyImmediate(root); }
            });
        }
        [Test] public void EventPublicationPrecedesGenerationAndRepeatedCommandsDoNotReplayIt()
        {
            WithManager((manager, config, catalogue) =>
            {
                var facts = new List<ProgressionEventFact>();
                manager.ProgressionEventCommitted += facts.Add;
                Action<ProgressionGenerationRequest> observe = request =>
                {
                    if (request.Round == 8)
                    {
                        Assert.That(facts.Count, Is.EqualTo(1));
                        Assert.That(request.Effects.ActiveThreatIds.Count, Is.EqualTo(4));
                        Assert.That(manager.CurrentEventFearAxis, Is.EqualTo(FearAxis.Agency));
                    }
                };
                manager.GenerationRequested += observe;
                try
                {
                    manager.StartRun(73);
                    for (int round = 1; round <= 10; round++)
                    {
                        if (manager.Snapshot.Phase == ProgressionPhase.ChooseThreat) manager.ChooseThreat("weaver", manager.Snapshot.Revision);
                        if (manager.Snapshot.Phase == ProgressionPhase.ChooseCurse) manager.ChooseCurse(manager.Snapshot.Choices[0].Id, manager.Snapshot.Revision);
                        int generation = manager.Snapshot.GenerationId;
                        Assert.That(manager.ConfirmFloorReady(generation), Is.True);
                        Assert.That(manager.ConfirmFloorReady(generation), Is.False);
                        if (manager.Snapshot.Phase == ProgressionPhase.Shop) manager.ContinueShop(manager.Snapshot.Revision);
                        else { manager.CompleteFloor(generation); Assert.That(manager.CompleteFloor(generation), Is.False); }
                    }
                    Assert.That(facts.Count, Is.EqualTo(1)); Assert.That(facts[0].Round, Is.EqualTo(8));
                    manager.StartRun(73); Assert.That(manager.EventHistory, Is.Empty);
                }
                finally { manager.ProgressionEventCommitted -= facts.Add; manager.GenerationRequested -= observe; }
            });
        }
        private static void WithManager(Action<ProgressionSessionManager, ProgressionConfig, EffectCatalogueConfig> test)
        {
            Assert.That(ProgressionSessionManager.Instance, Is.Null, "Do not replace a live Session owner.");
            var root = new GameObject("Progression event routing"); root.SetActive(false);
            var config = ScriptableObject.CreateInstance<ProgressionConfig>();
            var catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            try
            {
                Set(config, "_effectCatalogue", catalogue); Set(config, "_eventPool", new[] { ProgressionEventKind.ExtraHunter });
                Set(config, "_threats", new[] { new ProgressionEntryConfig("weaver", "Weaver", "Adds a hunter.") });
                Set(config, "_curses", new[] { new ProgressionEntryConfig("hidden-count", "Hidden Count", "Removes hunter names.") });
                Set(catalogue, "_entries", new[] {
                    new EffectCatalogueEntry("weaver", EffectKind.Threat, FearAxis.Agency, "Weaver", "Adds a hunter."),
                    new EffectCatalogueEntry("hidden-count", EffectKind.Curse, FearAxis.Information, "Hidden Count", "Removes hunter names.") });
                var manager = root.AddComponent<ProgressionSessionManager>(); var state = new ProgressionSessionBehaviorState();
                Set(manager, "config", config); Set(manager, "state", state);
                Set(manager, "controller", new ProgressionSessionController(state, config, new System.Random(73)));
                typeof(ProgressionSessionManager).GetProperty("Instance").SetValue(null, manager);
                test(manager, config, catalogue);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(config); Object.DestroyImmediate(catalogue);
                typeof(ProgressionSessionManager).GetProperty("Instance").SetValue(null, null);
            }
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
