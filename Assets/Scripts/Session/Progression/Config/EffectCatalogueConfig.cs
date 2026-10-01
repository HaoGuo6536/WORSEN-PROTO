// ============================================================================
// EffectCatalogueConfig.cs
// ============================================================================
// PURPOSE:
//   Authors stable effect identities and the rules for admitting their cards.
//   The run uses identifiers rather than legacy traits. Module curse identities
//   match their type-wide readers; world delivery still belongs to each owner.
// ARCHITECTURAL ROLE:
//   Config (§4) · Session · Progression.
// KEY RESPONSIBILITIES:
//   - Register owner-approved curses and upgrades while preserving module effect ids.
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
            new EffectCatalogueEntry("echo", EffectKind.Threat, FearAxis.Unpredictability, "The Echo", "Adds a hunter that repeats your exact path a few seconds behind you."),
            new EffectCatalogueEntry("weaver", EffectKind.Threat, FearAxis.Agency, "The Weaver", "Adds a ceiling hunter whose webs slow you."),
            new EffectCatalogueEntry("ticking", EffectKind.Threat, FearAxis.Time, "The Ticking", "Adds a clock hunter; collect keys or it hunts when the ticking stops."),
            new EffectCatalogueEntry("mannequin", EffectKind.Threat, FearAxis.Information, "The Mannequin", "Adds a hunter that moves only while unseen.", floor: 4),
            new EffectCatalogueEntry("stare", EffectKind.Threat, FearAxis.Time, "The Stare", "Adds a threat you must find and stare down in time.", floor: 6),
            new EffectCatalogueEntry("ram", EffectKind.Threat, FearAxis.Agency, "The Ram", "Adds a hunter that charges in a line and cannot turn mid-charge.", floor: 4),
            new EffectCatalogueEntry("mimic", EffectKind.Threat, FearAxis.Information, "The Mimic", "Adds false cakes. The arrow never points at them.", floor: 5),
            new EffectCatalogueEntry("skip", EffectKind.Threat, FearAxis.Unpredictability, "The Skip", "Adds a hunter that ambushes doorways you reuse.", floor: 6),
            new EffectCatalogueEntry("blinder", EffectKind.Threat, FearAxis.Information, "The Blinder", "Adds a hunter whose traps and shots blind you briefly.", floor: 5),
            new EffectCatalogueEntry("herald", EffectKind.Threat, FearAxis.Information, "The Herald", "Adds a hunter whose scream reveals you and deafens you.", floor: 6),
            new EffectCatalogueEntry("no-look-back", EffectKind.Curse, FearAxis.Information, "No Look-Back", "Removes the look-back snap."),
            new EffectCatalogueEntry("silent-presence", EffectKind.Curse, FearAxis.Information, "Silent Presence", "Removes distant hunter presence sounds."),
            new EffectCatalogueEntry("hidden-count", EffectKind.Curse, FearAxis.Information, "Hidden Count", "Hides the cake counter during a floor."),
            new EffectCatalogueEntry("mimic-faithless-arrow", EffectKind.Curse, FearAxis.Information, "Faithless Arrow", "The arrow sometimes shows the way to a Mimic instead.", hunters: new[] { "mimic" }),
            new EffectCatalogueEntry("blinder-more-traps", EffectKind.Curse, FearAxis.Information, "More Traps", "Adds more Blinder traps.", cap: 3, hunters: new[] { "blinder" }),
            new EffectCatalogueEntry("blinder-silent-traps", EffectKind.Curse, FearAxis.Information, "Silent Traps", "Removes the sound warning from Blinder traps.", hunters: new[] { "blinder" }),
            new EffectCatalogueEntry("darker-floors", EffectKind.Curse, FearAxis.Information, "Darker Floors", "Reduces lit rooms and how far you see through fog."),
            new EffectCatalogueEntry("random-spawn", EffectKind.Curse, FearAxis.Unpredictability, "Random Spawn", "Replaces the exit-room start with a random start."),
            new EffectCatalogueEntry("shuffled-collapse", EffectKind.Curse, FearAxis.Unpredictability, "Shuffled Collapse", "Replaces farthest-first collapse with a random order."),
            new EffectCatalogueEntry("nothing", EffectKind.Curse, FearAxis.Unpredictability, "Nothing???", "Nothing???", change: "Reduces shop prices by 15%; adds one enemy and one stack at each shop round."),
            new EffectCatalogueEntry("spent-pockets", EffectKind.Curse, FearAxis.Stakes, "Spent Pockets", "Removes unused consumables at the exit."),
            new EffectCatalogueEntry("short-grace", EffectKind.Curse, FearAxis.Agency, "Short Grace", "Shortens the grace window after a hit."),
            new EffectCatalogueEntry("faster-collapse", EffectKind.Curse, FearAxis.Time, "Faster Collapse", "Rooms collapse faster; +15% golden cakes."),
            new EffectCatalogueEntry("slow-mend", EffectKind.Curse, FearAxis.Stakes, "Slow Mend", "Reduces health regeneration to half speed."),
            new EffectCatalogueEntry("no-regen", EffectKind.Curse, FearAxis.Stakes, "No Regen", "Removes health regeneration during a floor.", floor: 12, prerequisite: "slow-mend"),
            new EffectCatalogueEntry("rough-start", EffectKind.Curse, FearAxis.Stakes, "Rough Start", "Replaces full starting health with half on every floor."),
            new EffectCatalogueEntry("short-burst", EffectKind.Curse, FearAxis.Agency, "Short Burst", "Shortens the on-hit speed boost."),
            new EffectCatalogueEntry("heavy-legs", EffectKind.Curse, FearAxis.Agency, "Heavy Legs", "Removes the on-hit speed boost.", prerequisite: "short-burst"),
            new EffectCatalogueEntry("stored-momentum", EffectKind.Upgrade, FearAxis.Agency, "Stored Momentum", "A vault stores your speed and the next jump releases it.", price: 12),
            new EffectCatalogueEntry("soft-landing", EffectKind.Upgrade, FearAxis.Agency, "Soft Landing", "Hard landings no longer stumble you.", price: 4),
            new EffectCatalogueEntry("quiet-slide", EffectKind.Upgrade, FearAxis.Information, "Quiet Slide", "Slides make no noise at all.", price: 5),
            new EffectCatalogueEntry("thick-skin", EffectKind.Upgrade, FearAxis.Agency, "Thick Skin", "The grace window after a hit lasts longer.", cap: 3, price: 12),
            new EffectCatalogueEntry("wax-heart", EffectKind.Upgrade, FearAxis.Agency, "Wax Heart", "The first grab of every floor breaks automatically.", floor: 2, price: 12),
            new EffectCatalogueEntry("low-profile", EffectKind.Upgrade, FearAxis.Agency, "Low Profile", "Slides cannot be grabbed by collapse hands.", floor: 2, price: 12),
            new EffectCatalogueEntry("steady-hand", EffectKind.Upgrade, FearAxis.Time, "Steady Hand", "The flashlight stun recharges faster.", floor: 2, price: 12),
            new EffectCatalogueEntry("second-bounce", EffectKind.Upgrade, FearAxis.Agency, "Second Bounce", "Adds one rebound off a hunter's body per chase.", floor: 3, price: 12, hunters: new[] { "echo", "weaver", "ticking", "mannequin", "ram", "blinder", "herald" }),
            new EffectCatalogueEntry("sweet-tooth", EffectKind.Upgrade, FearAxis.Stakes, "Sweet Tooth", "One cake trap per floor is a real cake instead.", floor: 3, price: 12),
            new EffectCatalogueEntry("glimpse", EffectKind.Upgrade, FearAxis.Information, "Glimpse", "Look-back also outlines the hunter behind you.", floor: 4, price: 12, hunters: new[] { "echo", "weaver", "ticking", "mannequin", "ram", "blinder", "herald" }),
            new EffectCatalogueEntry("latch", EffectKind.Upgrade, FearAxis.Agency, "Latch", "The first door you sprint through in each room closes behind you.", floor: 4, price: 12, hunters: new[] { "skip", "echo" }),
            new EffectCatalogueEntry("echo-boots", EffectKind.Upgrade, FearAxis.Information, "Echo Boots", "Your footsteps sound from where you were 3 s ago instead.", floor: 5, price: 12, hunters: new[] { "herald", "ram" }),
            new EffectCatalogueEntry("exit-sense", EffectKind.Upgrade, FearAxis.Information, "Exit Sense", "Once the exit opens, its arrow shows through walls.", floor: 5, price: 12),
            new EffectCatalogueEntry("loud-heart", EffectKind.Upgrade, FearAxis.Stakes, "Loud Heart", "Faster sprint while chased; hunters hear your heartbeat.", floor: 6, price: 12, hunters: new[] { "herald" }),
            new EffectCatalogueEntry("gilded-greed", EffectKind.Upgrade, FearAxis.Stakes, "Gilded Greed", "Golden Cakes count double; the exit needs one Golden Cake.", floor: 8, price: 12),
            new EffectCatalogueEntry("longer-slide", EffectKind.Upgrade, FearAxis.Agency, "Longer Slide", "Slides last longer and keep more speed.", price: 12),
            new EffectCatalogueEntry("higher-jump", EffectKind.Upgrade, FearAxis.Agency, "Higher Jump", "Increases jump height so more ledges become reachable.", price: 12),
            new EffectCatalogueEntry("sticky-fingers", EffectKind.Upgrade, FearAxis.Agency, "Sticky Fingers", "Cakes are collected from slightly farther away.", floor: 2, price: 12),
            new EffectCatalogueEntry("bigger-pockets", EffectKind.Upgrade, FearAxis.Agency, "Bigger Pockets", "Adds one consumable slot.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("cat-eyes", EffectKind.Upgrade, FearAxis.Information, "Cat Eyes", "You see farther into the dark and fog.", floor: 2, price: 12),
            new EffectCatalogueEntry("field-kit", EffectKind.Upgrade, FearAxis.Stakes, "Field Kit", "Health regenerates faster during a floor.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("lucky-reroll", EffectKind.Upgrade, FearAxis.Agency, "Lucky Reroll", "Adds one reroll at hunter and curse selection.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("bargain-hunter", EffectKind.Upgrade, FearAxis.Stakes, "Bargain Hunter", "Prices are lower at the next shop.", floor: 3, price: 12),
            new EffectCatalogueEntry("keen-ears", EffectKind.Upgrade, FearAxis.Information, "Keen Ears", "Hunter presence cues are audible from farther away.", floor: 3, price: 12, hunters: new[] { "echo", "weaver", "ticking", "mannequin", "ram", "blinder", "herald" }),
            new EffectCatalogueEntry("trail-reader", EffectKind.Upgrade, FearAxis.Information, "Trail Reader", "After a look-back, the Echo's remaining path is briefly visible.", floor: 3, price: 12, hunters: new[] { "echo" }),
            new EffectCatalogueEntry("sure-footing", EffectKind.Upgrade, FearAxis.Agency, "Sure Footing", "Removes damage from glancing Ram charges.", floor: 3, price: 12, hunters: new[] { "ram" }),
            new EffectCatalogueEntry("golden-sense", EffectKind.Upgrade, FearAxis.Information, "Golden Sense", "Adds a second arrow to the nearest Golden Cake during collapse.", floor: 4, price: 12),
            new EffectCatalogueEntry("stone-nerves", EffectKind.Upgrade, FearAxis.Time, "Stone Nerves", "The Stare's window is longer.", floor: 4, price: 12, hunters: new[] { "stare" }),
            new EffectCatalogueEntry("web-cutter", EffectKind.Upgrade, FearAxis.Agency, "Web Cutter", "Webs slow you half as much.", floor: 4, price: 12, hunters: new[] { "weaver" }),
            new EffectCatalogueEntry("marked-doors", EffectKind.Upgrade, FearAxis.Information, "Marked Doors", "Doorways the Skip watches show a faint mark.", floor: 4, price: 12, hunters: new[] { "skip" }),
            new EffectCatalogueEntry("spare-key", EffectKind.Upgrade, FearAxis.Time, "Spare Key", "Keys spawn closer to you.", floor: 4, price: 12, hunters: new[] { "ticking" }),
            new EffectCatalogueEntry("mirror-skin", EffectKind.Upgrade, FearAxis.Information, "Mirror Skin", "Blindness lasts half as long.", floor: 5, price: 12, hunters: new[] { "blinder" }),
            new EffectCatalogueEntry("ear-plugs", EffectKind.Upgrade, FearAxis.Information, "Ear Plugs", "The Herald's scream deafens you for half as long.", floor: 5, price: 12, hunters: new[] { "herald" }),
            new EffectCatalogueEntry("extra-life", EffectKind.Upgrade, FearAxis.Stakes, "Extra Life", "Once per run, revives you in place after a catch.", floor: 6, price: 12),
            new EffectCatalogueEntry("golden-touch", EffectKind.Upgrade, FearAxis.Stakes, "Golden Touch", "Increases each Golden Cake's value by one.", floor: 2, price: 12),
            new EffectCatalogueEntry("shop-reroll", EffectKind.Upgrade, FearAxis.Agency, "Shop Reroll", "Adds one free shop-offer reroll per visit.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("loyalty-card", EffectKind.Upgrade, FearAxis.Stakes, "Loyalty Card", "All shop prices are lower.", floor: 3, cap: 3, price: 12),
            new EffectCatalogueEntry("interest", EffectKind.Upgrade, FearAxis.Stakes, "Interest", "The wallet grows by a capped share between floors.", floor: 3, price: 12),
            new EffectCatalogueEntry("refund", EffectKind.Upgrade, FearAxis.Stakes, "Refund", "A consumable replaced at the shop refunds half its price.", floor: 4, price: 12),
            new EffectCatalogueEntry("extra-pedestal", EffectKind.Upgrade, FearAxis.Agency, "Extra Pedestal", "The shop shows one more offer per visit.", floor: 4, price: 12),
            new EffectCatalogueEntry("more-shrines", EffectKind.Upgrade, FearAxis.Agency, "More Shrines", "Adds one shrine per eligible floor above the depth curve.", price: 12),
            new EffectCatalogueEntry("business-license", EffectKind.Upgrade, FearAxis.Stakes, "Business License", "Increases Golden Cake yield to x1.25 (x1.5 with two).", floor: 5, cap: 2, price: 12),
            new EffectCatalogueEntry("speed-boost", EffectKind.Upgrade, FearAxis.Agency, "Speed Boost", "Increases sprint speed.", cap: 3, price: 12),
            new EffectCatalogueEntry("quick-start", EffectKind.Upgrade, FearAxis.Agency, "Quick Start", "Increases acceleration from a standstill.", cap: 3, price: 12),
            new EffectCatalogueEntry("air-control", EffectKind.Upgrade, FearAxis.Agency, "Air Control", "Increases steering while airborne.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("fast-hands", EffectKind.Upgrade, FearAxis.Agency, "Fast Hands", "Vaults, mantles and ledge climbs complete faster.", floor: 2, cap: 3, price: 12),
            new EffectCatalogueEntry("long-boost", EffectKind.Upgrade, FearAxis.Agency, "Long Boost", "The on-hit speed boost lasts longer.", floor: 3, cap: 3, price: 12),
            new EffectCatalogueEntry("firecracker", EffectKind.Consumable, FearAxis.Information, "Firecracker", "Two throws. The bang draws nearby hunters.", price: 4),
            new EffectCatalogueEntry("gauze", EffectKind.Consumable, FearAxis.Stakes, "Gauze", "Restores health over a few seconds.", price: 4),
            new EffectCatalogueEntry("smelling-salts", EffectKind.Consumable, FearAxis.Agency, "Smelling Salts", "Instantly removes deafness, blindness or a web slow.", price: 4),
            new EffectCatalogueEntry("wax-ward", EffectKind.Consumable, FearAxis.Agency, "Wax Ward", "Breaks the next collapse-hand grab automatically.", price: 4),
            new EffectCatalogueEntry("doorstop", EffectKind.Consumable, FearAxis.Time, "Doorstop", "Two uses. Jams a door behind you briefly.", price: 4),
            new EffectCatalogueEntry("oil-flask", EffectKind.Consumable, FearAxis.Agency, "Oil Flask", "Hunters slip on the spill and lose speed.", price: 4),
            new EffectCatalogueEntry("glass-vial", EffectKind.Consumable, FearAxis.Agency, "Glass Vial", "Throw at a hunter to make it flinch.", price: 4),
            new EffectCatalogueEntry("adrenaline", EffectKind.Consumable, FearAxis.Agency, "Adrenaline", "Adds a brief speed burst, usable only while critical.", price: 4),
            new EffectCatalogueEntry("echo-shorter-delay", EffectKind.Curse, FearAxis.Time, "Shorter Delay", "Shortens the delay before the Echo replays your path.", cap: 3, hunters: new[] { "echo" }, numbersOnly: true),
            new EffectCatalogueEntry("echo-faster-playback", EffectKind.Curse, FearAxis.Time, "Faster Playback", "The Echo replays your path faster than you moved.", cap: 3, hunters: new[] { "echo" }, numbersOnly: true),
            new EffectCatalogueEntry("echo-silent-steps", EffectKind.Curse, FearAxis.Information, "Silent Steps", "Reduces Echo footstep volume.", cap: 3, hunters: new[] { "echo" }),
            new EffectCatalogueEntry("weaver-stickier-webs", EffectKind.Curse, FearAxis.Agency, "Stickier Webs", "Increases the duration of web slows.", cap: 3, hunters: new[] { "weaver" }, numbersOnly: true),
            new EffectCatalogueEntry("weaver-wider-webs", EffectKind.Curse, FearAxis.Agency, "Wider Webs", "Increases web size.", cap: 3, hunters: new[] { "weaver" }, numbersOnly: true),
            new EffectCatalogueEntry("weaver-doorway-nests", EffectKind.Curse, FearAxis.Agency, "Doorway Nests", "Adds webs to some doorways at floor start.", cap: 3, hunters: new[] { "weaver" }),
            new EffectCatalogueEntry("weaver-quick-spin", EffectKind.Curse, FearAxis.Time, "Quick Spin", "Shortens the Weaver's warning before a shot.", cap: 3, hunters: new[] { "weaver" }, numbersOnly: true),
            new EffectCatalogueEntry("ticking-runs-faster", EffectKind.Curse, FearAxis.Time, "Runs Faster", "Shortens the time before the Ticking winds down.", cap: 3, hunters: new[] { "ticking" }, numbersOnly: true),
            new EffectCatalogueEntry("ticking-farther-keys", EffectKind.Curse, FearAxis.Time, "Farther Keys", "Increases key spawn distance from you.", cap: 3, hunters: new[] { "ticking" }),
            new EffectCatalogueEntry("ticking-loud-keys", EffectKind.Curse, FearAxis.Information, "Loud Keys", "Removes quiet key pickup; taking a key makes a loud sound.", hunters: new[] { "ticking" }),
            new EffectCatalogueEntry("ticking-double-spring", EffectKind.Curse, FearAxis.Time, "Double Spring", "Replaces one-key winding with two keys.", hunters: new[] { "ticking" }),
            new EffectCatalogueEntry("mannequin-longer-strides", EffectKind.Curse, FearAxis.Time, "Longer Strides", "The Mannequin reaches you faster.", cap: 3, hunters: new[] { "mannequin" }, numbersOnly: true),
            new EffectCatalogueEntry("mannequin-peripheral-creep", EffectKind.Curse, FearAxis.Information, "Peripheral Creep", "Seeing it at the edge of view no longer freezes it.", hunters: new[] { "mannequin" }),
            new EffectCatalogueEntry("stare-shorter-window", EffectKind.Curse, FearAxis.Time, "Shorter Window", "Shortens the time to find and face every Stare.", cap: 3, hunters: new[] { "stare" }, numbersOnly: true),
            new EffectCatalogueEntry("stare-sooner-return", EffectKind.Curse, FearAxis.Time, "Sooner Return", "Shortens the respite before the Stare returns.", cap: 3, hunters: new[] { "stare" }, numbersOnly: true),
            new EffectCatalogueEntry("stare-quieter-call", EffectKind.Curse, FearAxis.Information, "Quieter Call", "Reduces the call that helps locate the Stare.", cap: 3, hunters: new[] { "stare" }),
            new EffectCatalogueEntry("stare-wider-wander", EffectKind.Curse, FearAxis.Unpredictability, "Wider Wander", "The Stare appears in a wider range instead.", cap: 3, hunters: new[] { "stare" }),
            new EffectCatalogueEntry("ram-longer-charge", EffectKind.Curse, FearAxis.Agency, "Longer Charge", "Removes safe distance at the end of a Ram charge.", cap: 3, hunters: new[] { "ram" }, numbersOnly: true),
            new EffectCatalogueEntry("ram-shorter-windup", EffectKind.Curse, FearAxis.Time, "Shorter Windup", "Shortens the Ram's charge warning.", cap: 3, hunters: new[] { "ram" }, numbersOnly: true),
            new EffectCatalogueEntry("ram-partition-breaker", EffectKind.Curse, FearAxis.Agency, "Partition Breaker", "Removes breakable partitions as cover from the Ram.", hunters: new[] { "ram" }),
            new EffectCatalogueEntry("ram-second-charge", EffectKind.Curse, FearAxis.Time, "Second Charge", "The Ram charges again if it still sees you, instead of recovering.", hunters: new[] { "ram" }),
            new EffectCatalogueEntry("mimic-more-mimics", EffectKind.Curse, FearAxis.Unpredictability, "More Mimics", "Adds more false cakes for each retained Mimic.", cap: 3, hunters: new[] { "mimic" }),
            new EffectCatalogueEntry("mimic-golden-mimic", EffectKind.Curse, FearAxis.Information, "Golden Mimic", "Removes golden appearance as proof a cake is safe.", hunters: new[] { "mimic" }),
            new EffectCatalogueEntry("mimic-longer-bite", EffectKind.Curse, FearAxis.Time, "Longer Bite", "Increases the time spent caught by a Mimic bite.", cap: 3, hunters: new[] { "mimic" }, numbersOnly: true),
            new EffectCatalogueEntry("skip-shorter-cooldown", EffectKind.Curse, FearAxis.Time, "Shorter Cooldown", "Shortens the safe interval between Skip interceptions.", cap: 3, hunters: new[] { "skip" }, numbersOnly: true),
            new EffectCatalogueEntry("skip-quicker-learner", EffectKind.Curse, FearAxis.Unpredictability, "Quicker Learner", "The Skip learns your routes faster.", cap: 3, hunters: new[] { "skip" }),
            new EffectCatalogueEntry("skip-wider-reach", EffectKind.Curse, FearAxis.Agency, "Wider Reach", "Stairs and drops no longer avoid the Skip.", hunters: new[] { "skip" }),
            new EffectCatalogueEntry("skip-no-tell", EffectKind.Curse, FearAxis.Information, "No Tell", "Removes the Skip's interception mark.", hunters: new[] { "skip" }),
            new EffectCatalogueEntry("blinder-longer-dark", EffectKind.Curse, FearAxis.Time, "Longer Dark", "Increases the time vision is removed by the Blinder.", cap: 3, hunters: new[] { "blinder" }, numbersOnly: true),
            new EffectCatalogueEntry("blinder-muffled-dark", EffectKind.Curse, FearAxis.Information, "Muffled Dark", "Removes clear hearing while blinded by the Blinder.", hunters: new[] { "blinder" }),
            new EffectCatalogueEntry("herald-longer-deafness", EffectKind.Curse, FearAxis.Time, "Longer Deafness", "Increases the time hearing is removed by a Herald scream.", cap: 3, hunters: new[] { "herald" }, numbersOnly: true),
            new EffectCatalogueEntry("herald-wider-scream", EffectKind.Curse, FearAxis.Agency, "Wider Scream", "Removes safe distance around the Herald's scream.", cap: 3, hunters: new[] { "herald" }, numbersOnly: true),
            new EffectCatalogueEntry("herald-sharper-ears", EffectKind.Curse, FearAxis.Information, "Sharper Ears", "Replaces the Herald's regional warning with your exact position.", hunters: new[] { "herald" }),
            new EffectCatalogueEntry("herald-restless-throat", EffectKind.Curse, FearAxis.Time, "Restless Throat", "Shortens the respite between Herald calls.", cap: 3, hunters: new[] { "herald" }, numbersOnly: true),
            new EffectCatalogueEntry("herald-deaf-landing", EffectKind.Curse, FearAxis.Information, "Deaf Landing", "Removes landing feedback while deafened by the Herald.", hunters: new[] { "herald" })
        };
        public IReadOnlyList<EffectCatalogueEntry> Entries => Array.AsReadOnly(_entries ?? Array.Empty<EffectCatalogueEntry>());
    }
}
