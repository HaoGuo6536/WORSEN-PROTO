// ============================================================================
// AudioCueCataloguePresenter.cs
// ============================================================================
// PURPOSE:
//   Enforces the silence-first budget at every playback entrance, including old configs.
//   Legacy ids remain serialized safely, but rejected ids cannot acquire a voice.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
// KEY RESPONSIBILITIES:
//   - Map accepted cues to Core owner slots and explicit environmental noise kinds.
//   - Collapse aliases and keep interface feedback outside active floors.
//   - Admit injury and collapse transitions within existing contact/hazard slots, per room.
// DEPENDENCIES:
//   - Core budget/hearing contracts and Audio catalogue data only.
// USAGE NOTES:
//   Enemy footsteps share presence; attack phases other than windup are removed.
//   ChainCreak, TorchLoop, SpikeErupt and WardBreak are reused ASSET banks for the
//   closed-door, torch-gutter, trap and shrine slots, never free-running layers.
// ============================================================================
using Worsen.Core;

namespace Worsen.Presentation.Audio
{
    public sealed class AudioCueCataloguePresenter
    {
        public CueId Canonical(CueId cue)
        {
            switch (cue)
            {
                case CueId.FootstepWood: case CueId.FootstepMetal: case CueId.FootstepSoil: return CueId.Footstep;
                case CueId.ExitOpen: return CueId.DoorOpen;
                case CueId.RoomCrack: return CueId.RoomTelegraph;
                default: return cue;
            }
        }
        public bool TryGet(CueId cue, out AudioCueCatalogueEntry entry)
        {
            switch (Canonical(cue))
            {
                case CueId.Footstep: entry = Player(PlayerCueSlot.Footstep, NoiseSourceKind.Footstep); break;
                case CueId.PlayerCritical: entry = Player(PlayerCueSlot.Breathing); break;
                case CueId.PlayerHit: entry = Player(PlayerCueSlot.TraversalContact); break;
                case CueId.Land: case CueId.SlideEnd: entry = Player(PlayerCueSlot.TraversalContact, NoiseSourceKind.Landing); break;
                case CueId.SlideLoop: entry = Player(PlayerCueSlot.TraversalContact, NoiseSourceKind.Slide); break;
                case CueId.Vault: entry = Player(PlayerCueSlot.TraversalContact, NoiseSourceKind.Vault); break;
                case CueId.WallRebound: entry = Player(PlayerCueSlot.TraversalContact, NoiseSourceKind.Rebound); break;
                case CueId.Presence: entry = Hunter(HunterCueSlot.Presence, NoiseSourceKind.Other, true); break;
                case CueId.EnemyFootstep: entry = Hunter(HunterCueSlot.Presence, NoiseSourceKind.Footstep, true); break;
                case CueId.Detection: entry = Hunter(HunterCueSlot.Detection, NoiseSourceKind.Scream); break;
                case CueId.Chase: case CueId.EnemyScream: entry = Hunter(HunterCueSlot.ChaseLayer, NoiseSourceKind.Scream); break;
                case CueId.EnemyWindup: case CueId.SpikeWarning: entry = Hunter(HunterCueSlot.AttackTiming, NoiseSourceKind.Other, true, true); break;
                case CueId.Death: entry = Hunter(HunterCueSlot.DeathSting); break;
                case CueId.CakeCollect: case CueId.GoldenCakeCollect: entry = World(WorldCueSlot.CakePickup, NoiseSourceKind.CakePickup); break;
                case CueId.DoorOpen: entry = World(WorldCueSlot.ExitDoor, NoiseSourceKind.Door); break;
                case CueId.RoomTelegraph: entry = World(WorldCueSlot.RoomTelegraph, NoiseSourceKind.Other); break;
                case CueId.RoomTear: case CueId.MistAdvance: case CueId.RoomConsumed:
                    entry = new AudioCueCatalogueEntry(CueCategory.World, (int)WorldCueSlot.RoomTelegraph, NoiseSourceKind.Other, false, true); break;
                case CueId.GrabWarning: case CueId.GrabStart: case CueId.GrabHit: case CueId.GrabEscape:
                    entry = new AudioCueCatalogueEntry(CueCategory.World, (int)WorldCueSlot.TrapTrigger, NoiseSourceKind.Trap, false, true); break;
                case CueId.TorchLoop: entry = World(WorldCueSlot.TorchGutter, NoiseSourceKind.Other); break;
                case CueId.ChainCreak: entry = World(WorldCueSlot.HunterClosedDoor, NoiseSourceKind.Door); break;
                case CueId.SpikeErupt: entry = World(WorldCueSlot.TrapTrigger, NoiseSourceKind.Trap); break;
                case CueId.WardBreak: entry = World(WorldCueSlot.ShrineActivate, NoiseSourceKind.Shrine); break;
                case CueId.CurseOffer: case CueId.CurseSelect: case CueId.ShopOpen: case CueId.ShopBuy:
                case CueId.ShopReject: case CueId.UiMove: case CueId.UiConfirm: case CueId.UiBack: case CueId.Restart:
                    entry = new AudioCueCatalogueEntry(CueCategory.Interface, 0); break;
                default: entry = default; return false;
            }
            return true;
        }
        public bool Admits(CueId cue, bool inRun) => TryGet(cue, out var entry) && (!inRun || entry.Category != CueCategory.Interface);
        public bool FalsePositiveExempt(CueId cue) => cue == CueId.Footstep || cue == CueId.ChainCreak;
        public bool SameVoice(AudioCueCatalogueEntry a, int ownerA, AudioCueCatalogueEntry b, int ownerB) =>
            a.Category == b.Category && a.Slot == b.Slot &&
            (!(a.Category == CueCategory.Hunter || a.Category == CueCategory.World &&
                (a.Slot == (int)WorldCueSlot.RoomTelegraph || a.Slot == (int)WorldCueSlot.TrapTrigger)) || ownerA == ownerB);
        private AudioCueCatalogueEntry Player(PlayerCueSlot slot, NoiseSourceKind? noise = null) => new AudioCueCatalogueEntry(CueCategory.Player, (int)slot, noise);
        private AudioCueCatalogueEntry Hunter(HunterCueSlot slot, NoiseSourceKind? noise = null, bool protect = false, bool tell = false) =>
            new AudioCueCatalogueEntry(CueCategory.Hunter, (int)slot, noise, protect, tell);
        private AudioCueCatalogueEntry World(WorldCueSlot slot, NoiseSourceKind noise) => new AudioCueCatalogueEntry(CueCategory.World, (int)slot, noise);
    }
}
