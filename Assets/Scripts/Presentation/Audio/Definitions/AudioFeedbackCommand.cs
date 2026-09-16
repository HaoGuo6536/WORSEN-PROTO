// ============================================================================
// AudioFeedbackCommand.cs
// ============================================================================
//
// PURPOSE:
//   Carries an audible presentation decision without playing a source.
//   The owning Driver interprets these values after a pure Presenter observes committed facts.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Describe one sound or emitter release.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Pure data only; positions are world metres.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Presentation.Audio
{
    public struct AudioFeedbackCommand
    {
        public CueId Cue;
        public Vector3 Position;
        public float Gain;
        public int Emitter;
        public bool StopEmitter;
    }
}
