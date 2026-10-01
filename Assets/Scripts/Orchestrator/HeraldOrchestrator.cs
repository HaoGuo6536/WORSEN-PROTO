// ============================================================================
// HeraldOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes the explicit Herald gameplay broadcast separately from acoustic playback.
//   The injected Director command receives the whole typed fact, preserving source,
//   player-clue position and observation age without forging ordinary noise provenance.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · Herald gameplay broadcast.
// KEY RESPONSIBILITIES:
//   - Pair the Run hunter-fact subscription with the injected typed Director ingress.
//   - Detach the old publisher and receiver when the scene is rebound or disabled.
// DEPENDENCIES:
//   Session Run and Core Herald facts; Director command injected by scene composition.
// USAGE NOTES:
//   Scene-owned. Bind Director's typed broadcast method, not ordinary HearNoise.
//   Director owns fan-out to other hunters, so this route must not duplicate it.
//   A missing receiver admits no subscription; this is not evidence of live hearing.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Run;
namespace Worsen.Orchestrator
{
    public sealed class HeraldOrchestrator : MonoBehaviour
    {
        private RunSessionManager _run;
        private Action<HeraldScreamFact> _broadcast;
        public void Configure(RunSessionManager run, Action<HeraldScreamFact> broadcast)
        { OnDisable(); _run = run; _broadcast = broadcast; if (isActiveAndEnabled) OnEnable(); }
        private void OnEnable()
        {
            OnDisable();
            if (_run != null && _broadcast != null) _run.HunterFacts.HeraldScreamPublished += OnScream;
        }
        private void OnDisable()
        { if (_run != null) _run.HunterFacts.HeraldScreamPublished -= OnScream; }
        private void OnDestroy() => OnDisable();
        private void OnScream(HeraldScreamFact fact) => _broadcast(fact);
    }
}
