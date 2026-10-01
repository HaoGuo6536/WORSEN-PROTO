// ============================================================================
// ExpeditionActiveEffectsTests.cs
// ============================================================================
// PURPOSE:
//   Checks the Session-to-Player effect hand-off without generating a native floor.
//   Actual Manager callbacks deliver replacements; a source-order assertion covers
//   the assembly seam that otherwise requires navigation and a scene.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Expedition.
// KEY RESPONSIBILITIES:
//   - Check delivery before floor health, mid-floor updates and stale rejection.
//   - Keep baseline health/sprint neutral and preserve pending floor-start effects.
// DEPENDENCIES:
//   - Core; Domain Player/Floor; Session Progression/Expedition/Run; NUnit and Unity.
// USAGE NOTES:
//   Edit Mode with inactive test objects and injected Controllers, no singleton setup.
//   Does not replace the coordinator's live assembly and scene checks.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Floor;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using Object = UnityEngine.Object;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Expedition
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ExpeditionActiveEffectsTests
    {
        [Test]
        public void AssemblyPassesViewBeforeFloorHealthAndUsesUnmodifiedBaselines()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath,
                "Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs"));
            int spawn = source.IndexOf("_playerFactory.Spawn", StringComparison.Ordinal);
            int effects = source.IndexOf("player.SetActiveEffects(_progression.EffectsSnapshot.ActiveEffects)", StringComparison.Ordinal);
            int health = source.IndexOf("player.BeginFloorHealth(request.Effects.MaximumHealth, request.Effects.MovementSpeedMultiplier)", StringComparison.Ordinal);
            Assert.That(spawn, Is.GreaterThanOrEqualTo(0));
            Assert.That(effects, Is.GreaterThan(spawn)); Assert.That(health, Is.GreaterThan(effects));
        }

        [TestCase(false)] [TestCase(true)]
        public void PairedSnapshotsReachCurrentPlayerAndUnsubscribeWithoutResettingFloorHealth(bool shop)
        {
            var owned = new List<Object>();
            PlayerManager player = null; ExpeditionSessionManager expedition = null;
            try
            {
                var profile = Config<PlayerProfile>(owned); var effectsConfig = Config<PlayerEffectConfig>(owned);
                var playerState = new PlayerBehaviorState();
                var playerController = new PlayerController(playerState, profile, new System.Random(5), effectsConfig);
                playerController.Reset(new EntityId(-823), Vector3.zero, 0f);
                player = Component<PlayerManager>(owned);
                Set(player, "_state", playerState); Set(player, "_controller", playerController);
                typeof(PlayerRegistry).GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { player });
                var progression = Component<ProgressionSessionManager>(owned);
                var config = Config<ProgressionConfig>(owned);
                var state = new ProgressionSessionBehaviorState();
                var controller = new ProgressionSessionController(state, config, new System.Random(5));
                Set(progression, "controller", controller); Set(progression, "state", state);
                var expeditionState = new ExpeditionSessionBehaviorState();
                var assembly = new ExpeditionSessionController(expeditionState); assembly.Bind(SceneKey.HorrorRun);
                var baseline = new ProgressionEffects(1f, 1f, 1f, 1f, 100f, 100f, 0);
                assembly.Queue(new ProgressionGenerationRequest(1, 5, 1, shop, baseline)); assembly.Begin(1);
                assembly.RecordPlayer(player.Id); assembly.Ready();
                expedition = Component<ExpeditionSessionManager>(owned);
                Set(expedition, "_state", expeditionState); Set(expedition, "_controller", assembly);
                Set(expedition, "_progression", progression); Set(expedition, "_run", Component<RunSessionManager>(owned));
                Set(expedition, "_floor", Component<FloorManager>(owned));
                Call(expedition, "OnEnable"); Call(expedition, "OnEnable");
                Property(state, "Round", 1); Property(state, "GenerationId", 1);
                Property(state, "Phase", shop ? ProgressionPhase.Shop : ProgressionPhase.Exploring);
                Property(state, "MaximumHealth", 100f); Property(state, "Health", 100f); Property(state, "Revision", 7);
                IReadOnlyActiveEffects first = new ActiveEffects(new[] {
                    new ActiveEffect(new EffectId("thin-skin"), EffectKind.Curse, 1),
                    new ActiveEffect(new EffectId("rough-start"), EffectKind.Curse, 1),
                    new ActiveEffect(new EffectId("speed-boost"), EffectKind.Upgrade, 1) });
                var published = (Action<ProgressionSnapshot, IReadOnlyActiveEffects>)Get(progression, "EffectsSnapshotChanged");
                Assert.That(published.GetInvocationList().Length, Is.EqualTo(1));
                published(controller.Snapshot(), first);
                Assert.That(playerState.ActiveEffects, Is.SameAs(first));
                player.BeginFloorHealth(100f, 1f);
                published(controller.Snapshot(), first); // Readiness/wallet-only publication must not erase Rough Start.
                Assert.That(playerState.FloorHealthPending, Is.True);
                var ground = new MovementProbe(true, Vector3.up);
                playerController.Tick(default, ground, 1f / 60f, 1);
                Assert.That(playerState.MaxHealth, Is.EqualTo(75f));
                Assert.That(playerState.Health, Is.InRange(37.5f, 37.6f));
                Assert.That(playerState.MovementSpeedMultiplier, Is.EqualTo(1f));
                var oldSnapshot = controller.Snapshot();
                Property(state, "Revision", 8);
                IReadOnlyActiveEffects replacement = new ActiveEffects(new[] { new ActiveEffect(new EffectId("speed-boost"), EffectKind.Upgrade, 2) });
                published(controller.Snapshot(), replacement);
                Assert.That(playerState.ActiveEffects, Is.SameAs(replacement));
                published(oldSnapshot, first);
                Assert.That(playerState.ActiveEffects, Is.SameAs(replacement), "Stale revision cannot replace a newer view.");
                Property(state, "GenerationId", 2); published(controller.Snapshot(), first);
                Assert.That(playerState.ActiveEffects, Is.SameAs(replacement), "Another generation cannot reach this Player.");
                Call(expedition, "Unsubscribe");
                Assert.That(Get(progression, "EffectsSnapshotChanged"), Is.Null);
            }
            finally
            {
                if (expedition != null) expedition.ClearScene();
                if (player != null) typeof(PlayerRegistry).GetMethod("Unregister", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { player });
                for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            }
        }
        private static T Component<T>(List<Object> owned) where T : Component
        { var go = new GameObject(typeof(T).Name); owned.Add(go); go.SetActive(false); return go.AddComponent<T>(); }
        private static T Config<T>(List<Object> owned) where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
        private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
