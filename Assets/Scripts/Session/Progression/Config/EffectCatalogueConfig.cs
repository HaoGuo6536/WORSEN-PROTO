// ============================================================================
// EffectCatalogueConfig.cs
// ============================================================================
// PURPOSE:
//   Authors stable effect identities and the rules for admitting their cards.
//   This catalogue adds the approved content without replacing legacy traits or
//   claiming that another system has implemented the described effect.
// ARCHITECTURAL ROLE:
//   Config (§4) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Register More Shrines and Blinder curses while preserving exact module effect ids.
//   - Store copy, fear axes, availability, requirements, caps and future shop prices.
//   - Give Bargain a separate curse value, defaulting to one rather than shop price.
// DEPENDENCIES:
//   - Core effect/threat definitions and Unity serialization only.
// USAGE NOTES:
//   Runtime reads only. RequiredHunterIds is an ANY-of requirement; the effect
//   prerequisite is ANDed with it. Threat caps are ignored. Nothing??? deliberately
//   hides its rule in display copy; ChangeStatement preserves its validation copy.
//   Threat rows describe identities, not factory registration or selection admission.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Session.Progression
{
    [Serializable]
    public sealed class EffectCatalogueEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private EffectKind _kind;
        [SerializeField] private FearAxis _axis;
        [SerializeField] private string _title;
        [SerializeField, TextArea] private string _cardCopy;
        [SerializeField, Min(1)] private int _availabilityRound;
        [SerializeField] private string[] _requiredHunterIds;
        [SerializeField] private string _prerequisiteEffectId;
        [SerializeField, Min(1)] private int _stackCap;
        [SerializeField, Min(0)] private int _price;
        [SerializeField, Min(1)] private int _value = 1;
        [SerializeField] private bool _onlyRaisesHunterNumbers;
        [SerializeField, TextArea] private string _changeStatement;
        public string Id => _id;
        public EffectKind Kind => _kind;
        public FearAxis Axis => _axis;
        public string Title => _title;
        public string CardCopy => _cardCopy;
        public int AvailabilityRound => _availabilityRound;
        public IReadOnlyList<string> RequiredHunterIds => Array.AsReadOnly(_requiredHunterIds ?? Array.Empty<string>());
        public string PrerequisiteEffectId => _prerequisiteEffectId;
        public int StackCap => _stackCap;
        public int Price => _price;
        public int Value => _value;
        public bool OnlyRaisesHunterNumbers => _onlyRaisesHunterNumbers;
        public string ChangeStatement => _changeStatement;
        public EffectCatalogueEntry(string id, EffectKind kind, FearAxis axis, string title, string copy,
            int floor = 1, int cap = 1, int price = 0, string prerequisite = null,
            string[] hunters = null, bool numbersOnly = false, string change = null, int value = 1)
        {
            _id = id; _kind = kind; _axis = axis; _title = title; _cardCopy = copy;
            _availabilityRound = floor; _stackCap = cap; _price = price;
            _value = value;
            _prerequisiteEffectId = prerequisite; _requiredHunterIds = hunters == null ? Array.Empty<string>() : (string[])hunters.Clone();
            _onlyRaisesHunterNumbers = numbersOnly; _changeStatement = change ?? copy;
        }
    }

    [CreateAssetMenu(menuName = "Worsen/Progression/Effect Catalogue")]
    public sealed class EffectCatalogueConfig : ScriptableObject
    {
        [SerializeField] private EffectCatalogueEntry[] _entries =
        {
            new EffectCatalogueEntry("echo", EffectKind.Threat, FearAxis.Unpredictability, "The Echo", "Adds an Echo that replays your path; backtracking is no longer safe."),
            new EffectCatalogueEntry("weaver", EffectKind.Threat, FearAxis.Agency, "The Weaver", "Adds a ceiling hunter whose webs slow your movement."),
            new EffectCatalogueEntry("ticking", EffectKind.Threat, FearAxis.Time, "The Ticking", "Adds a winding clock; collect keys before its ticking stops and it hunts."),
            new EffectCatalogueEntry("mannequin", EffectKind.Threat, FearAxis.Information, "The Mannequin", "Adds a hunter that moves in darkness and freezes while lit or observed."),
            new EffectCatalogueEntry("stare", EffectKind.Threat, FearAxis.Time, "The Stare", "Adds a threat you must find and stare down before its window ends."),
            new EffectCatalogueEntry("ram", EffectKind.Threat, FearAxis.Agency, "The Ram", "Adds a charging hunter that cannot turn until it stops."),
            new EffectCatalogueEntry("mimic", EffectKind.Threat, FearAxis.Information, "The Mimic", "Adds false cakes; the white arrow never points at them."),
            new EffectCatalogueEntry("skip", EffectKind.Threat, FearAxis.Unpredictability, "The Skip", "Adds a threat that intercepts doorways you reuse."),
            new EffectCatalogueEntry("blinder", EffectKind.Threat, FearAxis.Information, "The Blinder", "Adds a hunter whose traps and projectiles temporarily remove vision."),
            new EffectCatalogueEntry("herald", EffectKind.Threat, FearAxis.Information, "The Herald", "Adds a screaming hunter that broadcasts your position and deafens you."),
            new EffectCatalogueEntry("no-look-back", EffectKind.Curse, FearAxis.Information, "No Look-Back", "Removes the look-back snap."),
            new EffectCatalogueEntry("silent-presence", EffectKind.Curse, FearAxis.Information, "Silent Presence", "Removes distant hunter presence loops; detection and attack cues remain."),
            new EffectCatalogueEntry("hidden-count", EffectKind.Curse, FearAxis.Information, "Hidden Count", "Removes the shelter's active hunter list."),
            new EffectCatalogueEntry("blinder-more-traps", EffectKind.Curse, FearAxis.Information, "More Traps", "Adds Blinder traps, removing safe paths through the floor.", hunters: new[] { "blinder" }),
            new EffectCatalogueEntry("blinder-silent-traps", EffectKind.Curse, FearAxis.Information, "Silent Traps", "Removes the sound warning from Blinder traps.", hunters: new[] { "blinder" }),
            new EffectCatalogueEntry("darker-floors", EffectKind.Curse, FearAxis.Information, "Darker Floors", "Reduces lit rooms and the distance you can see through fog."),
            new EffectCatalogueEntry("random-spawn", EffectKind.Curse, FearAxis.Unpredictability, "Random Spawn", "Replaces the exit-room start with a spawn elsewhere on the floor."),
            new EffectCatalogueEntry("shuffled-collapse", EffectKind.Curse, FearAxis.Unpredictability, "Shuffled Collapse", "Removes farthest-first collapse order; an escape route remains guaranteed."),
            new EffectCatalogueEntry("nothing", EffectKind.Curse, FearAxis.Unpredictability, "Nothing???", "Nothing???", change: "Changes one hunter rule for the rest of the run, with a tell but no announcement."),
            new EffectCatalogueEntry("thin-skin", EffectKind.Curse, FearAxis.Stakes, "Thin Skin", "Reduces maximum health."),
            new EffectCatalogueEntry("spent-pockets", EffectKind.Curse, FearAxis.Stakes, "Spent Pockets", "Removes unused consumables at the exit; they no longer carry between floors."),
            new EffectCatalogueEntry("greedy-door", EffectKind.Curse, FearAxis.Agency, "Greedy Door", "Replaces immediate exit opening with a Golden Cake quota: 40%, scaled by remaining versus collected cakes. With none collected, completed collapse opens it anyway; the exit room never collapses."),
            new EffectCatalogueEntry("short-grace", EffectKind.Curse, FearAxis.Agency, "Short Grace", "Shortens the grace window after a hit."),
            new EffectCatalogueEntry("faster-collapse", EffectKind.Curse, FearAxis.Time, "Faster Collapse", "Shortens the time before rooms collapse after the exit opens."),
            new EffectCatalogueEntry("slow-mend", EffectKind.Curse, FearAxis.Stakes, "Slow Mend", "Reduces health regeneration to half speed."),
            new EffectCatalogueEntry("no-regen", EffectKind.Curse, FearAxis.Stakes, "No Regen", "Removes health regeneration during a floor.", prerequisite: "slow-mend"),
            new EffectCatalogueEntry("rough-start", EffectKind.Curse, FearAxis.Stakes, "Rough Start", "Replaces full starting health with half health on every floor."),
            new EffectCatalogueEntry("short-burst", EffectKind.Curse, FearAxis.Agency, "Short Burst", "Shortens the on-hit speed boost."),
            new EffectCatalogueEntry("heavy-legs", EffectKind.Curse, FearAxis.Agency, "Heavy Legs", "Removes the on-hit speed boost.", prerequisite: "short-burst"),
            new EffectCatalogueEntry("stored-momentum", EffectKind.Upgrade, FearAxis.Agency, "Stored Momentum", "A vault stores your speed and the next jump releases it.", price: 12),
            new EffectCatalogueEntry("soft-landing", EffectKind.Upgrade, FearAxis.Agency, "Soft Landing", "Hard landings no longer stumble you.", price: 12),
            new EffectCatalogueEntry("quiet-slide", EffectKind.Upgrade, FearAxis.Information, "Quiet Slide", "Slides make no noise at all.", price: 12),
            new EffectCatalogueEntry("thick-skin", EffectKind.Upgrade, FearAxis.Agency, "Thick Skin", "The grace window after a hit lasts longer.", cap: 3, price: 12),
            new EffectCatalogueEntry("wax-heart", EffectKind.Upgrade, FearAxis.Agency, "Wax Heart", "The first grab of every floor breaks automatically.", floor: 2, price: 12),
            new EffectCatalogueEntry("low-profile", EffectKind.Upgrade, FearAxis.Agency, "Low Profile", "Sliding passes under collapse hands; a slide cannot be grabbed.", floor: 2, price: 12),
            new EffectCatalogueEntry("steady-hand", EffectKind.Upgrade, FearAxis.Time, "Steady Hand", "The flashlight stun recharges faster.", floor: 2, price: 12),
            new EffectCatalogueEntry("second-bounce", EffectKind.Upgrade, FearAxis.Agency, "Second Bounce", "Adds one rebound off a hunter's body per chase.", floor: 3, price: 12, hunters: new[] { "echo", "weaver", "mannequin", "ram", "blinder", "herald", "watcher", "rusher", "lurker", "hexer", "thorncaller" }),
            new EffectCatalogueEntry("sweet-tooth", EffectKind.Upgrade, FearAxis.Stakes, "Sweet Tooth", "One cake trap per floor is a real cake instead.", floor: 3, price: 12),
            new EffectCatalogueEntry("glimpse", EffectKind.Upgrade, FearAxis.Information, "Glimpse", "The look-back snap also outlines the hunter behind you briefly through fog.", floor: 4, price: 12, hunters: new[] { "echo", "weaver", "mannequin", "ram", "blinder", "herald", "watcher", "rusher", "lurker", "hexer", "thorncaller" }),
            new EffectCatalogueEntry("latch", EffectKind.Upgrade, FearAxis.Agency, "Latch", "The first door you sprint through in each room closes behind you.", floor: 4, price: 12, hunters: new[] { "skip", "echo" }),
            new EffectCatalogueEntry("echo-boots", EffectKind.Upgrade, FearAxis.Information, "Echo Boots", "Replaces your footstep origin with where you were three seconds ago.", floor: 5, price: 12, hunters: new[] { "herald", "ram" }),
            new EffectCatalogueEntry("exit-sense", EffectKind.Upgrade, FearAxis.Information, "Exit Sense", "Once the exit opens, its arrow shows through walls.", floor: 5, price: 12),
            new EffectCatalogueEntry("blind-faith", EffectKind.Upgrade, FearAxis.Information, "Blind Faith", "Every cake counts double toward the exit, but the arrow is removed.", floor: 6, price: 12, hunters: new[] { "mimic" }),
            new EffectCatalogueEntry("loud-heart", EffectKind.Upgrade, FearAxis.Stakes, "Loud Heart", "You sprint faster while chased, but hunters hear your heartbeat during a chase.", floor: 6, price: 12, hunters: new[] { "herald" }),
            new EffectCatalogueEntry("gilded-greed", EffectKind.Upgrade, FearAxis.Stakes, "Gilded Greed", "Golden Cakes are worth double, but the exit only opens after your first Golden Cake.", floor: 8, price: 12),
            new EffectCatalogueEntry("longer-slide", EffectKind.Upgrade, FearAxis.Agency, "Longer Slide", "Slides last longer and keep more speed.", price: 12),
            new EffectCatalogueEntry("higher-jump", EffectKind.Upgrade, FearAxis.Agency, "Higher Jump", "Increases jump height so more ledges become reachable.", price: 12),
            new EffectCatalogueEntry("sticky-fingers", EffectKind.Upgrade, FearAxis.Agency, "Sticky Fingers", "Cakes are collected from slightly farther away.", floor: 2, price: 12),
            new EffectCatalogueEntry("bigger-pockets", EffectKind.Upgrade, FearAxis.Agency, "Bigger Pockets", "Adds one consumable slot above the base of three.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("cat-eyes", EffectKind.Upgrade, FearAxis.Information, "Cat Eyes", "You see farther into the dark; fog starts farther out.", floor: 2, price: 12),
            new EffectCatalogueEntry("field-kit", EffectKind.Upgrade, FearAxis.Stakes, "Field Kit", "Health regenerates faster during a floor.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("lucky-reroll", EffectKind.Upgrade, FearAxis.Agency, "Lucky Reroll", "Adds one reroll at hunter and curse selection.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("bargain-hunter", EffectKind.Upgrade, FearAxis.Stakes, "Bargain Hunter", "Prices are lower at the next shop.", floor: 3, price: 12),
            new EffectCatalogueEntry("keen-ears", EffectKind.Upgrade, FearAxis.Information, "Keen Ears", "Hunter presence cues are audible from farther away.", floor: 3, price: 12, hunters: new[] { "echo", "weaver", "mannequin", "ram", "blinder", "herald", "watcher", "rusher", "lurker", "hexer", "thorncaller" }),
            new EffectCatalogueEntry("trail-reader", EffectKind.Upgrade, FearAxis.Information, "Trail Reader", "After a look-back, the Echo's remaining path is briefly visible.", floor: 3, price: 12, hunters: new[] { "echo" }),
            new EffectCatalogueEntry("sure-footing", EffectKind.Upgrade, FearAxis.Agency, "Sure Footing", "Removes damage from a glancing Ram charge; it still knocks you aside.", floor: 3, price: 12, hunters: new[] { "ram" }),
            new EffectCatalogueEntry("golden-sense", EffectKind.Upgrade, FearAxis.Information, "Golden Sense", "Adds a second arrow to the nearest Golden Cake during collapse.", floor: 4, price: 12),
            new EffectCatalogueEntry("stone-nerves", EffectKind.Upgrade, FearAxis.Time, "Stone Nerves", "The Stare's window is longer.", floor: 4, price: 12, hunters: new[] { "stare" }),
            new EffectCatalogueEntry("web-cutter", EffectKind.Upgrade, FearAxis.Agency, "Web Cutter", "Webs slow you half as much.", floor: 4, price: 12, hunters: new[] { "weaver" }),
            new EffectCatalogueEntry("marked-doors", EffectKind.Upgrade, FearAxis.Information, "Marked Doors", "Doorways the Skip watches show a faint mark.", floor: 4, price: 12, hunters: new[] { "skip" }),
            new EffectCatalogueEntry("afterglow", EffectKind.Upgrade, FearAxis.Time, "Afterglow", "After a light breaks, its room stays safe from the Mannequin a few seconds longer.", floor: 4, price: 12, hunters: new[] { "mannequin" }),
            new EffectCatalogueEntry("spare-key", EffectKind.Upgrade, FearAxis.Time, "Spare Key", "Keys spawn closer to you.", floor: 4, price: 12, hunters: new[] { "ticking" }),
            new EffectCatalogueEntry("mirror-skin", EffectKind.Upgrade, FearAxis.Information, "Mirror Skin", "Blindness lasts half as long.", floor: 5, price: 12, hunters: new[] { "blinder" }),
            new EffectCatalogueEntry("ear-plugs", EffectKind.Upgrade, FearAxis.Information, "Ear Plugs", "The Herald's scream deafens you for half as long.", floor: 5, price: 12, hunters: new[] { "herald" }),
            new EffectCatalogueEntry("bail-bond", EffectKind.Upgrade, FearAxis.Stakes, "Bail Bond", "Reduces the early bail penalty by half.", floor: 5, price: 12),
            new EffectCatalogueEntry("extra-life", EffectKind.Upgrade, FearAxis.Stakes, "Extra Life", "Once per run, dying returns you to the floor start at half health while collapse continues.", floor: 6, price: 12),
            new EffectCatalogueEntry("golden-touch", EffectKind.Upgrade, FearAxis.Stakes, "Golden Touch", "Increases each Golden Cake's value by one.", floor: 2, price: 12),
            new EffectCatalogueEntry("shop-reroll", EffectKind.Upgrade, FearAxis.Agency, "Shop Reroll", "Adds one free shop-offer reroll per visit.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("loyalty-card", EffectKind.Upgrade, FearAxis.Stakes, "Loyalty Card", "All shop prices are lower.", floor: 3, cap: 3, price: 12),
            new EffectCatalogueEntry("interest", EffectKind.Upgrade, FearAxis.Stakes, "Interest", "The wallet grows by a capped share between floors.", floor: 3, price: 12),
            new EffectCatalogueEntry("refund", EffectKind.Upgrade, FearAxis.Stakes, "Refund", "A consumable replaced at the shop refunds half its price.", floor: 4, price: 12),
            new EffectCatalogueEntry("extra-pedestal", EffectKind.Upgrade, FearAxis.Agency, "Extra Pedestal", "The shop shows one more offer per visit.", floor: 4, price: 12),
            new EffectCatalogueEntry("more-shrines", EffectKind.Upgrade, FearAxis.Agency, "More Shrines", "Adds one shrine per eligible floor above the depth curve.", price: 12),
            new EffectCatalogueEntry("business-license", EffectKind.Upgrade, FearAxis.Stakes, "Business License", "Golden Cake yield increases to 1.25 times, or 1.5 times with two copies.", floor: 5, cap: 2, price: 12),
            new EffectCatalogueEntry("speed-boost", EffectKind.Upgrade, FearAxis.Agency, "Speed Boost", "Increases sprint speed, capped below hunters' chase speed.", cap: 3, price: 12),
            new EffectCatalogueEntry("quick-start", EffectKind.Upgrade, FearAxis.Agency, "Quick Start", "Increases acceleration from a standstill.", cap: 3, price: 12),
            new EffectCatalogueEntry("air-control", EffectKind.Upgrade, FearAxis.Agency, "Air Control", "Increases steering while airborne.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("fast-hands", EffectKind.Upgrade, FearAxis.Agency, "Fast Hands", "Vaults, mantles and ledge climbs complete faster.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("long-boost", EffectKind.Upgrade, FearAxis.Agency, "Long Boost", "The on-hit speed boost lasts longer.", floor: 3, cap: 3, price: 12),
            new EffectCatalogueEntry("firecracker", EffectKind.Consumable, FearAxis.Information, "Firecracker", "Two throws: the bang draws nearby hunters to the impact point.", price: 4),
            new EffectCatalogueEntry("gauze", EffectKind.Consumable, FearAxis.Stakes, "Gauze", "Restores health over a few seconds while you keep moving.", price: 4),
            new EffectCatalogueEntry("smelling-salts", EffectKind.Consumable, FearAxis.Agency, "Smelling Salts", "Instantly removes deafness, blindness or a web slow.", price: 4),
            new EffectCatalogueEntry("wax-ward", EffectKind.Consumable, FearAxis.Agency, "Wax Ward", "Breaks the next collapse-hand grab automatically.", price: 4),
            new EffectCatalogueEntry("doorstop", EffectKind.Consumable, FearAxis.Time, "Doorstop", "Two uses: jams the door behind you briefly; hunters must break it loudly.", price: 4),
            new EffectCatalogueEntry("oil-flask", EffectKind.Consumable, FearAxis.Agency, "Oil Flask", "A slick patch makes hunters slip and lose momentum briefly.", price: 4),
            new EffectCatalogueEntry("glass-vial", EffectKind.Consumable, FearAxis.Agency, "Glass Vial", "Shatter it at a hunter's face for an instant short flinch, weaker than flashlight stun.", price: 4),
            new EffectCatalogueEntry("adrenaline", EffectKind.Consumable, FearAxis.Agency, "Adrenaline", "Adds a brief speed burst, usable only while critical.", price: 4),
            new EffectCatalogueEntry("echo-shorter-delay", EffectKind.Curse, FearAxis.Time, "Shorter Delay", "Shortens the delay before the Echo replays your path.", cap: 3, hunters: new[] { "echo" }, numbersOnly: true),
            new EffectCatalogueEntry("echo-faster-playback", EffectKind.Curse, FearAxis.Time, "Faster Playback", "Increases Echo playback speed above your recorded speed, gaining on straight sections.", cap: 3, hunters: new[] { "echo" }, numbersOnly: true),
            new EffectCatalogueEntry("echo-silent-steps", EffectKind.Curse, FearAxis.Information, "Silent Steps", "Reduces the volume of the Echo's footsteps, making them harder to place.", cap: 3, hunters: new[] { "echo" }),
            new EffectCatalogueEntry("weaver-stickier-webs", EffectKind.Curse, FearAxis.Agency, "Stickier Webs", "Increases the duration of web slows.", cap: 3, hunters: new[] { "weaver" }, numbersOnly: true),
            new EffectCatalogueEntry("weaver-wider-webs", EffectKind.Curse, FearAxis.Agency, "Wider Webs", "Increases web projectile size, leaving less room to sidestep.", cap: 3, hunters: new[] { "weaver" }, numbersOnly: true),
            new EffectCatalogueEntry("weaver-doorway-nests", EffectKind.Curse, FearAxis.Agency, "Doorway Nests", "Replaces clear doorways with webs in some rooms at floor start.", cap: 3, hunters: new[] { "weaver" }),
            new EffectCatalogueEntry("weaver-quick-spin", EffectKind.Curse, FearAxis.Time, "Quick Spin", "Shortens the Weaver's warning before a shot.", cap: 3, hunters: new[] { "weaver" }, numbersOnly: true),
            new EffectCatalogueEntry("ticking-runs-faster", EffectKind.Curse, FearAxis.Time, "Runs Faster", "Shortens the time before the Ticking winds down.", cap: 3, hunters: new[] { "ticking" }, numbersOnly: true),
            new EffectCatalogueEntry("ticking-farther-keys", EffectKind.Curse, FearAxis.Time, "Farther Keys", "Increases key spawn distance from you.", cap: 3, hunters: new[] { "ticking" }),
            new EffectCatalogueEntry("ticking-loud-keys", EffectKind.Curse, FearAxis.Information, "Loud Keys", "Removes quiet key collection; taking a key makes a noise every hunter hears.", hunters: new[] { "ticking" }),
            new EffectCatalogueEntry("ticking-double-spring", EffectKind.Curse, FearAxis.Time, "Double Spring", "Replaces one-key winding with two keys to fully wind the Ticking.", hunters: new[] { "ticking" })
        };
        public IReadOnlyList<EffectCatalogueEntry> Entries => Array.AsReadOnly(_entries ?? Array.Empty<EffectCatalogueEntry>());
    }
}
