// ============================================================================
// FloorCakeTrap.cs
// ============================================================================
// PURPOSE:
//   Relays cake-shaped trap contacts and plays the owner's explicit audible tick.
//   It never decides whether a player springs a trap or applies a foreign effect.
// ARCHITECTURAL ROLE:
//   Sub-driver (§7e), owned by FloorDriver · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Report enter/stay contacts using the same spherical trigger semantics as cakes.
//   - Play an injected placeholder clip only when commanded by the Floor tick.
// DEPENDENCIES:
//   UnityEngine physics/audio and System events only.
// USAGE NOTES:
//   Scene-owned; the parent owns the clip. No Update, global settings or random clock.
//   FloorDriver pairs contact subscriptions and destroys the generated hierarchy.
// ============================================================================
using System;
using UnityEngine;

namespace Worsen.Domain.Floor
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class FloorCakeTrap : MonoBehaviour
    {
        private AudioSource _audio;
        public int TrapId { get; private set; }
        public event Action<Collider, int> Contact;
        public void Configure(int id, AudioClip clip)
        {
            TrapId = id;
            if (clip == null) return;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false; _audio.loop = false; _audio.spatialBlend = 1f;
            _audio.dopplerLevel = 0f; _audio.rolloffMode = AudioRolloffMode.Logarithmic;
            _audio.clip = clip;
        }
        public void PlayTick(float volume)
        { if (_audio != null && gameObject.activeInHierarchy) _audio.PlayOneShot(_audio.clip, volume); }
        private void OnTriggerEnter(Collider other) => Contact?.Invoke(other, TrapId);
        private void OnTriggerStay(Collider other) => Contact?.Invoke(other, TrapId);
    }
}
