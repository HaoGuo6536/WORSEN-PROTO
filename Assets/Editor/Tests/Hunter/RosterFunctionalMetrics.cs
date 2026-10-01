// ============================================================================
// RosterFunctionalMetrics.cs
// ============================================================================
// PURPOSE:
//   Records observable Hunter output rather than counting controller intentions.
//   Each native case prints the same metrics, including on failure, so spawning,
//   motion, contact and rule facts cannot be mistaken for one another.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), test support (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Pair subscriptions to real Manager and Driver publishers.
//   - Retain typed facts for defining-behaviour assertions.
//   - Report movement and contact latency with an explicit damage-evidence boundary.
// DEPENDENCIES:
//   - Hunter publishers, Core immutable facts, NUnit and Unity position values.
// USAGE NOTES:
//   Hits means HunterHit candidates, not Session-accepted health changes. Effect
//   contacts and body contacts are separate. Missing contact is printed as none.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    internal sealed class RosterFunctionalMetrics
    {
        private readonly string _name;
        private HunterManager _hunter;
        private HunterDriver _driver;
        private EntityId _player;
        private Vector3 _previous;
        private float? _firstContact;
        public bool Spawned { get; private set; }
        public float Distance { get; private set; }
        public int BodyContacts { get; private set; }
        public readonly List<HunterHit> Hits = new List<HunterHit>();
        public readonly List<HunterArchetypeFact> Archetype = new List<HunterArchetypeFact>();
        public readonly List<HunterFeedbackEvent> Feedback = new List<HunterFeedbackEvent>();
        public readonly List<WeaverFact> Weaver = new List<WeaverFact>();
        public readonly List<WebHitFact> WebHits = new List<WebHitFact>();
        public readonly List<RamFact> Ram = new List<RamFact>();
        public readonly List<SkipFact> Skip = new List<SkipFact>();
        public readonly List<MimicFact> Mimic = new List<MimicFact>();
        public readonly List<BlinderThrowFact> Throws = new List<BlinderThrowFact>();
        public readonly List<BlinderHitFact> BlindHits = new List<BlinderHitFact>();
        public readonly List<BlinderSoundFact> BlindSounds = new List<BlinderSoundFact>();
        public readonly List<BlinderTrapPolicyFact> TrapPolicies = new List<BlinderTrapPolicyFact>();
        public readonly List<HeraldScreamFact> Screams = new List<HeraldScreamFact>();
        public readonly List<HeraldBreathFact> Breaths = new List<HeraldBreathFact>();
        public readonly List<HeraldDeafenFact> Deafen = new List<HeraldDeafenFact>();
        public readonly List<MannequinFact> Mannequin = new List<MannequinFact>();
        public readonly List<StareFact> Stare = new List<StareFact>();
        public readonly List<TickingSoundFact> ClockSounds = new List<TickingSoundFact>();
        public readonly List<TickingGuidanceFact> Keys = new List<TickingGuidanceFact>();
        public RosterFunctionalMetrics(string name) { _name = name; }
        public void Attach(HunterManager hunter, HunterDriver driver, EntityId player)
        {
            _hunter = hunter; _driver = driver; _player = player; _previous = driver.Position; Spawned = true;
            hunter.OnLungeHit += Hit; driver.OnLungeContact += Contact;
            hunter.OnArchetypeFact += Archetype.Add; hunter.OnFeedback += Feedback.Add;
            hunter.OnWeaverFact += Weaver.Add; hunter.OnWebHit += WebHit;
            hunter.OnRamFact += Ram.Add; hunter.OnSkipFact += Skip.Add; hunter.OnMimicFact += Mimic.Add;
            hunter.OnBlinderThrow += Throws.Add; hunter.OnBlinderHit += BlindHit;
            hunter.OnBlinderSound += BlindSounds.Add; hunter.OnBlinderTrapPolicy += TrapPolicies.Add;
            hunter.OnHeraldScream += Screams.Add; hunter.OnHeraldBreath += Breaths.Add; hunter.OnHeraldDeafen += DeafenHit;
            hunter.OnMannequinFact += Mannequin.Add; hunter.OnStareFact += Stare.Add;
            if (hunter.Ticking != null) { hunter.Ticking.OnSound += ClockSounds.Add; hunter.Ticking.OnGuidance += Keys.Add; }
        }
        private void First(long tick) { if (!_firstContact.HasValue) _firstContact = tick * RosterFunctionalArena.Dt; }
        private void Hit(HunterHit hit) { Hits.Add(hit); First(hit.Tick); }
        private void WebHit(WebHitFact hit) { WebHits.Add(hit); First(hit.Tick); }
        private void BlindHit(BlinderHitFact hit) { BlindHits.Add(hit); First(hit.Tick); }
        private void DeafenHit(HeraldDeafenFact hit) { Deafen.Add(hit); First(hit.Tick); }
        private void Contact(Collider collider)
        {
            if (collider.GetComponentInParent<IEntityHandle>()?.Id != _player) return;
            BodyContacts++; First(_hunter.ReadOnlyState.Tick);
        }
        public void Observe(float seconds, Vector3 position)
        { Distance += Vector3.Distance(position, _previous); _previous = position; }
        public void Write(float seconds, double wallSeconds)
        {
            int facts = Archetype.Count + Feedback.Count + Weaver.Count + WebHits.Count + Ram.Count + Skip.Count + Mimic.Count +
                Throws.Count + BlindHits.Count + BlindSounds.Count + TrapPolicies.Count + Screams.Count + Breaths.Count + Deafen.Count +
                Mannequin.Count + Stare.Count + ClockSounds.Count + Keys.Count + Hits.Count;
            TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "ROSTER archetype={0} spawned={1} distance_m={2:F4} facts_emitted={3} hits={4} effect_contacts={5} body_contacts={6} time_to_first_contact_s={7} simulated_s={8:F3} wall_s={9:F3} hit_scope=HunterHit_candidates",
                _name, Spawned, Distance, facts, Hits.Count, WebHits.Count + BlindHits.Count + Deafen.Count, BodyContacts,
                _firstContact.HasValue ? _firstContact.Value.ToString("F3", CultureInfo.InvariantCulture) : "none", seconds, wallSeconds));
            TestContext.WriteLine($"ROSTER_FACTS archetype={Archetype.Count} feedback={Feedback.Count} weaver={Weaver.Count} web_hits={WebHits.Count} ram={Ram.Count} skip={Skip.Count} mimic={Mimic.Count} throws={Throws.Count} blind_hits={BlindHits.Count} blind_sounds={BlindSounds.Count} trap_policies={TrapPolicies.Count} screams={Screams.Count} breaths={Breaths.Count} deafen={Deafen.Count} mannequin={Mannequin.Count} stare={Stare.Count} clock_sounds={ClockSounds.Count} key_guidance={Keys.Count}");
        }
        public void Detach()
        {
            if (_hunter == null) return;
            _hunter.OnLungeHit -= Hit; _driver.OnLungeContact -= Contact;
            _hunter.OnArchetypeFact -= Archetype.Add; _hunter.OnFeedback -= Feedback.Add;
            _hunter.OnWeaverFact -= Weaver.Add; _hunter.OnWebHit -= WebHit;
            _hunter.OnRamFact -= Ram.Add; _hunter.OnSkipFact -= Skip.Add; _hunter.OnMimicFact -= Mimic.Add;
            _hunter.OnBlinderThrow -= Throws.Add; _hunter.OnBlinderHit -= BlindHit;
            _hunter.OnBlinderSound -= BlindSounds.Add; _hunter.OnBlinderTrapPolicy -= TrapPolicies.Add;
            _hunter.OnHeraldScream -= Screams.Add; _hunter.OnHeraldBreath -= Breaths.Add; _hunter.OnHeraldDeafen -= DeafenHit;
            _hunter.OnMannequinFact -= Mannequin.Add; _hunter.OnStareFact -= Stare.Add;
            if (_hunter.Ticking != null) { _hunter.Ticking.OnSound -= ClockSounds.Add; _hunter.Ticking.OnGuidance -= Keys.Add; }
            _hunter = null; _driver = null;
        }
    }
}
