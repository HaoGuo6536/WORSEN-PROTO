// ============================================================================
// ShrineManager.cs
// ============================================================================
// PURPOSE:
//   Owns the floor's shrine placements, world representations and activation endpoint.
//   It reports single-use facts while Session decides costs and run-scoped consequences.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Domain · Shrine (Service system).
// KEY RESPONSIBILITIES:
//   - Assemble supplied sites through the Controller and command the owned Driver.
//   - Publish activation once from committed movement and pressed input samples.
// DEPENDENCIES:
//   - Own Controller/State/Config/Driver and Core values only; no other Domain system.
// USAGE NOTES:
//   Scene-owned, no singleton or tick loop. Assembly supplies configs and a seeded random
//   source; Run integration calls Sample once per committed living-player tick.
//   Reassemble only for a new floor. This Manager never locks or moves the player.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Shrine
{
    public sealed class ShrineManager : MonoBehaviour
    {
        private ShrineController controller;
        private ShrineDriver driver;
        public event Action<ShrineActivatedFact> Activated;
        public IReadOnlyList<ShrinePlacement> Assemble(IReadOnlyList<ShrineSite> sites, int floor,
            ShrineConfig config, ShrineDriverConfig driverConfig, System.Random random,
            bool moreShrines = false, IReadOnlyCollection<FearAxis> excludedAxes = null)
        {
            if (driverConfig == null) throw new ArgumentNullException(nameof(driverConfig));
            controller = new ShrineController(new ShrineBehaviorState(), config, random);
            if (driver == null) driver = gameObject.AddComponent<ShrineDriver>();
            var placements = controller.Assemble(sites, floor, moreShrines, excludedAxes);
            driver.Build(placements, driverConfig);
            return placements;
        }
        public bool Sample(Vector3 position, Vector3 velocity, InputButtons pressed, long tick)
        {
            if (!isActiveAndEnabled || controller == null || !controller.TryActivate(position, velocity, pressed, tick, out var fact)) return false;
            driver.MarkUsed(fact.ShrineId);
            Activated?.Invoke(fact);
            return true;
        }
        public void Teardown()
        {
            controller = null;
            if (driver != null)
            {
                driver.Clear();
                if (Application.isPlaying) Destroy(driver); else DestroyImmediate(driver);
                driver = null;
            }
        }
        private void OnDestroy() { Teardown(); Activated = null; }
    }
}
