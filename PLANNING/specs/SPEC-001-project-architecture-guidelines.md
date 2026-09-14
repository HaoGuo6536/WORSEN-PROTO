---
id: SPEC-001
type: spec
title: Project architecture and modular design guidelines
status: LIVE
created: 2026-09-14
updated: 2026-09-14
owner: UNKNOWN — owner input needed
supersedes: none
superseded_by: none
source: Assets/Scripts/PROJECT_ARCHITECTURE_GUIDELINES.md
archived: none
---

# Project Architecture & Modular Design Guidelines

> SPEC-001 · LIVE since 2026-09-14. See the [registry](../index.md). Implemented by [PLAN-001](../plans/PLAN-001-worsen-boilerplate.md).
> Migrated from `Assets/Scripts/PROJECT_ARCHITECTURE_GUIDELINES.md`. Existing enforcement labels using `PROJECT_ARCHITECTURE_GUIDELINES.md` refer to this spec. The original body and stable section numbers are preserved; historical verification notes below were not reverified by this migration.

When adding features or modifying code in this project, **you must strictly adhere to the modular architecture below.** The codebase separates *state*, *logic*, *configuration*, *presentation*, and *cross-system wiring* into distinct script types with distinct rules, and separates *systems* into layers with a fixed dependency direction.

Do not write monolithic scripts. Every system is built from the script types in the taxonomy below, every script belongs to exactly one type, and every system belongs to exactly one layer.

**The one-sentence model:** each system has one **Manager** sitting at the junction of two stacks — a **logic stack** (Controller / BehaviorState / Config) that decides *what happens*, and a **presentation stack** (Driver / Presenter / DriverState / DriverConfig) that is the system's boundary with the engine in *both* directions. The Manager is the only script that talks to both. Logic never references presentation types; presentation never references game systems.

```
                       ┌── Controller ── BehaviorState   (+ Config)              logic stack
 input / events ──► Manager ──┤
                       └── Driver ── Presenter ── DriverState  (+ DriverConfig) ◄─► engine
                              └── sub-drivers                                    presentation stack
```

**The one-sentence system model:** systems live in layers (Core → Domain → Session → Presentation → Orchestrator). References point *down* the layer graph only; anything that must flow *up* is a C# event that an Orchestrator subscribes to and routes. Domain code never knows that UI, audio, or cameras exist.

Section numbers (§0–§13) are stable and are referenced from script headers, ast-grep rule messages, and test names — do not renumber them.

### Adoption values for this project (WORSEN)
The rules are project-independent; these are the values WORSEN filled in. They are already applied throughout this document and encoded in the enforcement layers (§13) — change them here *and* in `ArchitectureConformanceTests.cs` and `tools/ast-grep/rules/menu-under-project-root.yml` together, or the gates will disagree with the prose.

| Value | This project |
|---|---|
| Editor menu root (§10) | `Worsen/` |
| Asmdef / namespace prefix (§12) | `Worsen` — `Worsen.Core`, `Worsen.Domain`, `Worsen.Session`, `Worsen.Presentation`, `Worsen.Orchestrator`, `Worsen.Editor`, `Worsen.Tests` |
| Concrete Orchestrator list (§6) | `InputOrchestrator`, `DebugOverlayOrchestrator` — see the current roster below |
| Reference implementations (§7b, §10) | `InputFramePresenter` and `DebugOverlayPresenter` with their tests; `TagArenaSceneSetup` for deterministic scene and config wiring |
| Appendix A | Empty |

---

## Quick Reference — The Script Taxonomy

Every script in `Assets/Scripts/` is one of these. Use this table to classify a script; use the sections below for the full rules.

| Type | Name pattern | Base | Folder | Owns runtime state? | Engine calls? | Unit-tested? | Job in one line |
|---|---|---|---|---|---|---|---|
| **Manager** (§1) | `[System]Manager` | `MonoBehaviour` | `Manager/` | No — delegates to State | Lifecycle; create/destroy own Drivers; resolve `GameObject`→`EntityId` | No | Entry point: lifecycle, wiring, sequencing its system |
| **Factory** (§1c) | `[Entity]Factory` | `MonoBehaviour` | `Manager/` | Pools only | `Instantiate`/`Destroy` of entity prefabs | No | Spawns, pools, and despawns entity instances |
| **Registry** (§8) | `[Thing]Registry` | `MonoBehaviour` or `static` | `Manager/` of its consumer | List of registrants | No | — | Lets objects announce themselves to a longer-lived system |
| **Controller** (§2) | `[System]Controller` | pure C# | `Controller/` | Via BehaviorState | **Never** | **Yes** | Game rules and calculations |
| **Utility** (§2b) | `[Name]Utility` | `static class` | `Controller/` or `Core/Utility/` | Never | **Never** | **Yes** | Stateless pure helpers |
| **BehaviorState** (§3) | `[System]BehaviorState` | pure C# | `State/` | *Is* the state | Never (may hold references) | — | Runtime game data |
| **Config** (§4) | `[System]Config` | `ScriptableObject` | `Config/` | Never | — | — | Designer tunables; one asset per system |
| **Content SO** (§4b) | `[Name]Profile` / `[Name]SO` / `[Name]Data` | `ScriptableObject` | `Config/` | **Never** | **Never** | Hooks: yes | Designer content; N assets per system |
| **Definitions** (§5) | `[System]Definitions` | enums / structs / DTOs | `Definitions/` or `Core/Definitions/` | — | — | — | Shared types, no logic |
| **Orchestrator** (§6) | `[Target]Orchestrator` | `MonoBehaviour` | `Orchestrator/` | **No** | No | No | Subscribes to lower-layer events, calls the target layer |
| **SceneRoot** (§6b) | `[Scene]SceneRoot` | `MonoBehaviour` | `Orchestrator/Scenes/` | No | Instantiate/place for assembly only | No | Assembles a scene's systems |
| **Driver** (§7a) | `[System]Driver` / `[X]InputDriver` | `MonoBehaviour` | `Driver/` | No — delegates to DriverState | **Yes — this is its job** | No | The system's engine boundary, in and out |
| **Presenter** (§7b) | `[X]Presenter` | pure C# | `Driver/` | Via DriverState | **Never** | **Yes** | Visual / physical math |
| **DriverState** (§7c) | `[X]DriverState` | pure C# | `Driver/` | *Is* the state | Never (may hold references) | — | Transient presentation data |
| **DriverConfig** (§7d) | `[X]DriverConfig` | `ScriptableObject` | `Config/` | Never | — | — | Visual / physical tunables |
| **Sub-driver** (§7e) | descriptive (`ShatterFragment`) | `MonoBehaviour` | `Driver/` | Local to one object | **Yes** | No | Per-object engine work, owned by a Driver |
| **Editor tool** (§10) | `[X]Setup` / `[X]Generator` / `[X]Window` / `[X]Editor` / `[X]Drawer` | `Editor` / `EditorWindow` | `Assets/Editor/[System]/` | — | Editor-only | — | Rebuilds non-versioned wiring |

### Where does this code go?

Walk this list top to bottom; stop at the first match.

1. **Is it a type** (enum, struct, plain data class, interface with no behavior)? → **Definitions** (§5). Used by more than one system → `Core/Definitions/`.
2. **Is it a designer-tunable value** (number, curve, color, asset reference)? → **Config** if it tunes how the system behaves, **DriverConfig** if it tunes how it looks/feels, **Content SO** if it *is* content (§4, §4b, §7d).
3. **Does it change at runtime?** → **BehaviorState** for game data, **DriverState** for presentation data (§3, §7c).
4. **Is it an engine call** — a method or property on a live engine object (`VisualElement`, `Transform`, `Rigidbody`, `Animator`, …) *or* a static engine API (`Physics.*`, `Time.*`, `SceneManager.*`, `Resources.*`, `Instantiate`)? → **Driver** or **Sub-driver** (§7a, §7e). The four exceptions: a Manager creating its own Driver objects and resolving `GameObject`→`EntityId` (§1), a Factory instantiating entities (§1c), a SceneRoot assembling a scene (§6b), and the `SceneFlowManager` loading scenes (§8b). See §7 for what counts as an engine call.
5. **Is it a computation with no engine calls?** → about game rules: **Controller** (§2); about visuals, physics, layout, or timing: **Presenter** (§7b); stateless and reusable: **Utility** (§2b).
6. **Does it run Unity lifecycle, subscribe to events, or wire pieces together?** → **Manager** (§1).
7. **Does it create or destroy entities?** → **Factory** (§1c).
8. **Does it cross a system boundary?** → check the layer graph (§9) before writing anything. Downward: direct call. Upward: event, routed by an **Orchestrator** (§6).
9. **Does it rebuild asset wiring or add a menu item?** → **Editor tool** (§10).

If a script matches two rows, it is two scripts.

---

## 0. Script Header Documentation (Mandatory for ALL Scripts)

**Every script file in this project must begin with a documentation header comment block** placed at the very top of the file, before `using` statements. This header is the grounding context for both human developers and AI agents: anyone reading or modifying the file must understand its purpose before touching a line of code. It is also machine-read by the conformance tests (§13).

### Required Format
```csharp
// ============================================================================
// [FileName].cs
// ============================================================================
//
// PURPOSE:
//   [A verbose, high-level explanation of what this script does and WHY it
//   exists. Describe the problem it solves or the role it fills within the
//   larger system. This should be understandable to someone with zero prior
//   context about the project.]
//
// ARCHITECTURAL ROLE:
//   [Type from the Quick Reference · Layer · System (and system kind for
//   Managers). E.g. "Driver (§7a) · Domain · Combat (Entity system)",
//   "Presenter (§7b) · Presentation · CombatHUD",
//   "Sub-driver (§7e), owned by EncounterTransitionDriver · Session · Encounter".
//   Then describe how it fits into the data/logic flow of its parent system.]
//
// KEY RESPONSIBILITIES:
//   - [Responsibility 1]
//   - [Responsibility 2]
//   - [Responsibility 3]
//
// DEPENDENCIES:
//   - [Every other *system* this file references, and how: e.g.
//     "Reads IReadOnlyStatsState (passed in by CombatManager)",
//     "Calls SFXManager.Play (Orchestrator → Presentation)".
//     This list must agree with the layer graph in §9.]
//
// USAGE NOTES:
//   [Caveats, assumptions, or constraints a developer or agent must know
//   before modifying this file. Mandatory declarations that belong here:
//   lifecycle tier (§8), any [DefaultExecutionOrder] justification (§8),
//   any global engine side effect a Driver owns (§7a), any DriverConfig
//   sharing (§7d), pooled-entity reset behavior (§1c).]
//
// ============================================================================
```

### Rules
*   **Non-negotiable**: No script may omit this header. If you encounter an existing script without one, add it before making any other changes.
*   **Be verbose, not terse**: PURPOSE is at minimum 2–3 sentences. "Handles combat" is unacceptable. Explain *what*, *why*, and *how* at a high level.
*   **Keep it current**: When a script's responsibilities or dependencies change, **update the header in the same change**. A stale header is worse than no header.
*   **Plain language**: Write for an audience that has never seen this codebase. No unexplained shorthand or jargon.

---

## 1. Manager (`[System]Manager.cs`)
The **Manager** is the entry point and the "brain" of a system, and the only script in the system that knows both the logic stack and the presentation stack exist. It **owns** its Controller(s), its Driver(s), and — through the Controller — its BehaviorState(s).

*   **Does**:
    *   Handles Unity lifecycle (`Awake`, `OnEnable`, `Update`, `OnDisable`, `OnDestroy`) and subscribes/unsubscribes to events.
    *   Translates inputs and events into Controller calls, passing in time (`Tick(float dt)`), randomness, and any cross-system data the Controller needs (§2c).
    *   Takes Controller results and turns them into Driver commands (§7a), Factory calls (§1c), or downward calls to other Managers (§9).
    *   Publishes state-change events for upward listeners (§9). This is the *only* way a Manager influences a higher layer.
    *   Resolves engine identities to game identities: a `Collider` or `GameObject` reported by a Driver becomes an `EntityId` here (via `GetComponentInParent<IEntityHandle>()`) before it reaches the Controller. Controllers never see `GameObject`s.
    *   Declares its lifecycle tier (§8) and system kind (§1b) in its header.
*   **Does NOT**:
    *   Hold raw state data (delegate to `BehaviorState`).
    *   Perform calculations or rule logic (delegate to `Controller`).
    *   Manipulate engine objects — no `VisualElement`, `Transform`, `Rigidbody`, `Animator`, or material work (delegate to `Driver`). The permitted engine interactions are **creating, destroying, enabling, and wiring its own Driver objects** and the identity resolution above; once created, the Manager talks to Drivers only through their command methods.
    *   Reference a system in a higher layer, or in the same layer against the declared dependency order (§9).
    *   Contain a sequence that spans systems or scenes — that is a Session Manager's job (§8b).
*   **Multiple Controllers are allowed but not encouraged by default.** Start with one. Split only when a clear, distinct responsibility boundary emerges (e.g. `CombatController` + `CombatAnimationController`). Do not pre-emptively create Controllers "just in case" — that is bloat, not architecture.

### 1b. System kinds
Every system is one of three kinds, declared in its Manager's header. The kind determines instancing, lifecycle, and `Instance` rules.

| Kind | Instances | Lifecycle | `Instance`? | Examples |
|---|---|---|---|---|
| **Service** | Exactly one | Persistent or scene-owned (§8) | Only if persistent | `InputManager`, `MusicManager`, `CombatHUDManager`, `StatsManager` |
| **Entity** | N, one per prefab instance | Scene-owned; created/destroyed by a Factory | **Never** | `PlayerManager`, `EnemyManager`, `ProjectileManager` |
| **Session** | Exactly one | **Always persistent** | Yes | `SceneFlowManager`, `EncounterSessionManager`, `SaveManager` |

*   **Entity systems** are prefab-rooted: the prefab root carries `[Entity]Manager`, which owns that instance's Controller, BehaviorState, and Driver stack exactly like a Service does. The Controller and State are *per instance* — never a shared list on a singleton. An Entity Manager implements `IEntityHandle` (`EntityId Id { get; }`, in `Core/Definitions/`).
*   **Entity systems receive their dependencies at spawn.** The Factory calls `Initialize(TArchetype archetype, EntityContext ctx)` on the Manager; everything the entity needs (its Content SO, read-only views of shared state, services it may call) arrives there. `Awake` does one-time internal wiring only. An entity may fall back to `Instance` on a *persistent Service in its own layer* (§9, rung 5) but never hunts for scene objects and never touches Orchestrators or Presentation.
*   **Session systems** own flows; see §8b.

### 1c. Factory (`[Entity]Factory.cs`)
The **Factory** is the only script that instantiates or destroys entity prefabs. There is one per Entity system, living in that system's `Manager/` folder.
*   **Role**: `EntityId Spawn(SpawnRequest req)` — instantiate (or take from pool) the prefab named by the archetype Content SO, assign a fresh `EntityId`, call `Initialize(...)` on the root Manager, register it in the entity Registry (§8), return the id. `Despawn(EntityId id)` — call `Teardown()` on the Manager, unregister, return to pool or destroy.
*   **Rules**:
    *   Callers are Managers in the same or a higher layer. They request spawns with a `SpawnRequest` (Core DTO) produced by a Controller — Controllers decide *that* something spawns and *what*; they never spawn.
    *   **Pooling is a Factory concern.** Pooled entities must be fully reset by `Initialize`; nothing about a prior life may leak through the State. An entity that cannot be cleanly reset is not poolable — declare that in its header.
    *   `EntityId` is a `readonly struct` in `Core/Definitions/`. It is the *only* entity identity that crosses a Controller boundary; `GameObject`s and `Transform`s stop at the Manager.
    *   The Factory never applies game rules (e.g. "can the player afford this summon?") — that is a Controller decision made before the request reaches the Factory.

## 2. Controller (`[System]Controller.cs`)
The **Controller** is a pure C# class (non-MonoBehaviour) that executes the system's game logic: rules, calculations, and state mutation. A Controller **may own more than one BehaviorState** when it manages distinct data domains (e.g. `CombatController` with `CombatBehaviorState` and `TurnBehaviorState`).

*   **Role**: Takes inputs from the Manager, reads/mutates its BehaviorState(s), applies rules from the Config, and returns plain results. It does not know that Drivers, Factories, or other systems exist — the Manager routes its results.
*   **Rules**:
    *   Must not inherit from `MonoBehaviour`.
    *   **No engine calls** — neither on live objects nor static engine APIs (§7, "What counts as an engine call"). Anything Unity-specific it needs is passed in as data.
    *   **Time is a parameter.** Controllers expose `Tick(float deltaTime)` (or take `float now`) — never read `Time.*`.
    *   **Randomness is injected.** Controllers take a `System.Random` (or an `IRandom` from Core) in their constructor — never `UnityEngine.Random`. Tests pass a seeded instance.
    *   **Results, not side effects.** A Controller returns what should happen (a result struct, a list of `SpawnRequest`s, a new phase) and the Manager makes it happen. A Controller that "does" things has become a Manager.
    *   Every Controller with nontrivial logic ships with a test file (§11).
    *   **Add Controllers and BehaviorStates on an as-needed basis only.** One Controller with one BehaviorState is the correct design when it cleanly covers the system. Split in response to real complexity, never speculatively.
*   **Pure-C# dispatchers are Controllers.** A message bus or dispatcher that one Manager constructs and owns (e.g. `CombatEventBusController`, built by `SkillManager` in `Awake()`) executes logic and mutates state, so it is Controller-layer machinery regardless of what it is called. It lives in `Controller/` and its class/file name carries the `Controller` suffix. (Contrast with Orchestrators, §6.)

### 2b. Utility (`[Name]Utility.cs`)
A **Utility** is a `static class` of pure, stateless functions (`PatternGraphUtility`, `PatternDirectionUtility`).
*   **Rules**: No state, no engine calls, no dependencies on any Manager. System-local utilities live in that system's `Controller/`; utilities used by more than one system live in `Core/Utility/`. Nontrivial utilities are tested like Controllers (§11).

### 2c. Cross-system data in Controllers
A Controller references only its own State(s), its Config, Definitions (own and Core), Utilities, and **read-only views** of other systems' state. It never references another system's Controller, Manager, Driver, or mutable State.
*   **Reads**: every BehaviorState that another system needs to read exposes an `IReadOnly[System]State` interface (getters only, declared next to the State in `State/`). The owning Manager hands the interface to whoever needs it (a sibling Manager, the Factory's `EntityContext`), and that Manager passes it into its Controller as a constructor or method parameter. `CombatController` takes an `IReadOnlyStatsState`; it never holds a `StatsBehaviorState`.
*   **Writes**: a Controller never mutates foreign state. It returns a result ("deal 12 damage to entity 7") and its Manager routes it — a direct call to `StatsManager.ApplyDamage(...)` if Stats is a declared same-layer dependency (§9), an event otherwise.
*   **Dependency direction inside a layer**: a system's header DEPENDENCIES lists every system it reads from or calls. Within a layer the resulting graph must be acyclic; if two systems need each other, the shared piece moves down (a Core DTO, a lower system) or the coordination moves up (a Session Manager). Cycles are checked in the graph (§13).

## 3. BehaviorState (`[System]BehaviorState.cs`)
The **BehaviorState** is a pure data container for the system's dynamic runtime data (current health, accumulated charge, active target, visited nodes).

*   **Rules**:
    *   Data only, with at most trivial helpers (getters/setters, simple list adds).
    *   **NO logic, NO Unity manipulation, NO execution flows.**
    *   **Passive engine references are data, not logic.** A BehaviorState MAY hold a `GameObject`, `Transform`, or `Vector3` when the reference *is* the data — e.g. `EncounterBehaviorState` carrying the overworld enemy that triggered the encounter. It must NEVER *operate* on them: no `SetActive`, no transform manipulation, no component lookups. If you find yourself calling a method on a stored reference, that call belongs in a Controller or Driver. Prefer an `EntityId` over an engine reference wherever one exists.
    *   **States do not publish events.** Notification is the owning Manager's job. A BehaviorState with `public event` declarations has grown logic — move the events up.
    *   **States are never independently persistent** (§8). State survives scene loads only because a persistent Manager owns it — never make a state a `MonoBehaviour` or `DontDestroyOnLoad` object in its own right.
    *   **Read-only view**: if any other system reads this state, declare `IReadOnly[System]State` beside it and implement it (§2c). Mutators stay off the interface.
    *   **Save boundary**: the BehaviorState is the unit of persistence. A State that must survive across play sessions is `[Serializable]` with plain fields — `EntityId`s and archetype keys, never `Transform`s or `GameObject`s. `SaveManager` (§8b) asks each persistent Manager for a snapshot and hands one back on load; Managers re-resolve engine references afterwards. Controllers never see save I/O.

## 4. Config (`[System]Config.cs`)
The **Config** is a `ScriptableObject` holding a system's static, designer-tunable parameters: multipliers, damage values, speeds, base opacities, default colors — every "magic number."

*   **Rules**:
    *   No dynamic state is ever stored here.
    *   **Never hardcode tunable values in a Manager, Controller, Driver, or Presenter.** Expose them in a Config (or DriverConfig, §7d) so designers can tune them in the Editor.
    *   Typically one Config asset per system. If you find yourself wanting many assets of the same Config type, it is probably a Content SO (§4b).
    *   Fields are `[SerializeField] private` with public getters. **No public setters on any ScriptableObject** — a runtime write to an SO field mutates a shared asset and, in the Editor, persists to disk.

### 4a. ScriptableObject Asset Placement (all SO types — non-negotiable)
Applies to Config, DriverConfig, and Content SO assets alike.
*   **Class files** (`.cs`) live in the system's `Config/` folder.
*   **Asset files** (`.asset`) live under `Assets/Resources/ScriptableObjects/`, in a subfolder tree that **mirrors** the system hierarchy under `Assets/Scripts/` including the layer folder (e.g. `Scripts/Domain/EnemyAI/Config/EnemyCombatProfile.cs` → `Resources/ScriptableObjects/Domain/EnemyAI/EnemyCombatProfile.asset`). See §12 for the mirror rule.
*   **Never place `.asset` files inside `Scripts/` or its subdirectories.** Mixing assets into `Scripts/` conflates source code with designer-facing data; a single `Resources/ScriptableObjects/` root gives designers one predictable place to find every tunable, and placing it under `Resources/` enables `Resources.Load<>()` as a runtime self-heal path (§10). If you encounter `.asset` files inside `Scripts/`, migrate them and update serialized references.

### 4b. Content ScriptableObjects (`[Name]Profile.cs` / `[Name]SO.cs` / `[Name]Data.cs`)
Not every ScriptableObject is a Config. **Content SOs** are designer-authored *archetypes and content* of which many instances exist — `EnemyCombatProfile` (one per enemy type), `PassiveSkillSO` (one per skill), `CreatureEssenceProfile`, `EnemyPhaseData`.
*   **The distinction**: A Config tunes *how a system behaves* (one asset per system). A Content SO *is the content* the system consumes (N assets — one per enemy, skill, item, phase). If adding another asset of this type is "adding content" rather than "re-tuning the system," it is a Content SO.
*   **Naming**: `[Name]Profile` for entity archetypes (these carry the prefab reference a Factory instantiates), `[Name]SO` for polymorphic content with behavior hooks, `[Name]Data` for inert data blocks.
*   **Location**: class files in the system's `Config/` folder (they are designer-facing schemas, not runtime type definitions); assets per §4a, with **one subfolder per archetype** when an archetype owns multiple assets (`Domain/EnemyAI/Trickster/`, `Domain/Skills/Trickster/`, `Domain/Stats/Trickster/`). Named sets and difficulty variants get their own folder at the same level (`PreAlphaEasy/`, `Tutorial/`).
*   **Behavior hooks on `[Name]SO` are pure functions.** A hook has the shape `Result Apply(in Context ctx)`: it reads the context and its own serialized fields, returns a result, and the calling Controller applies it. Hooks:
    *   never touch engine objects or static engine APIs, never read `Instance`, never call a Manager or Controller;
    *   **never write to the SO's own fields** — per-instance runtime data ("this skill has 2 charges left") lives in the owning entity's BehaviorState, keyed by the SO or its id;
    *   are unit-tested like Controllers (§11) — a hook that cannot be called from a test has broken one of the rules above.

## 5. Definitions (`[System]Definitions.cs`)
*   **Role**: The system's shared types — enums, structs, plain data classes, and behavior-free interfaces (`AttackType`, `Direction`, `PatternData`, `SpawnRequest`) — that other scripts reference.
*   **Rules**:
    *   Types only. No logic, no utilities, no static helpers (those are §2b).
    *   A type used by more than one system lives in `Core/Definitions/`, not in either system. This is what keeps the layer graph acyclic: `EntityId`, `IEntityHandle`, `EntityContext`, `SpawnRequest`, and every event payload that crosses a layer are Core types.
    *   Keep them in separate definition files rather than nested inside Managers so they can be referenced without creating dependency loops.

## 6. Orchestrator (`[Target]Orchestrator.cs`)
The **Orchestrator** is a scene-level `MonoBehaviour` in the top layer that connects layers which are not allowed to reference each other. It **subscribes** to events published by Domain and Session Managers and **calls** Presentation Managers — and, for the return trip, subscribes to Presentation events (input, UI interaction, animation completion) and calls Domain/Session Managers. It is the only script that references both sides.

```
 Domain / Session Manager ──event──► [Target]Orchestrator ──call──► Presentation Manager
 Domain / Session Manager ◄──call─── [Target]Orchestrator ◄──event── Presentation Manager
```

*   **Rules**:
    *   **Stateless and thin.** Each handler translates a payload and forwards it — one to three lines. If an Orchestrator needs to remember anything ("we're waiting for the attack animation"), that memory is a phase in the *originating* system's BehaviorState, and the Orchestrator merely delivers the completion event back.
    *   **One Orchestrator per Presentation target.** The current roster is below. Create each one only when its Presentation target actually exists, record it here, and before creating a new one check whether an existing one already owns that target — extend it if so. There is no god-Orchestrator.
    *   **Method signatures use Core types only.** An Orchestrator never leaks a Domain type into Presentation (pass `EntityId`, `int`, a Core DTO — not `IReadOnlyCombatState`) and never leaks a Presentation type into Domain.
    *   **Lower layers never reference an Orchestrator.** They publish events; the Orchestrator finds them (it lives above, so it may hold references to any Manager). If you are typing `SomethingOrchestrator.` inside a Domain or Presentation system, stop — publish an event instead.
    *   **Placement**: all Orchestrators live in `Assets/Scripts/Orchestrator/`. There is no such thing as an orchestrator internal to one system — inside a system, the Manager coordinates. Anything `new`-ed up by one Manager is that Manager's Controller-layer machinery (§2), whatever its role name.

Current roster:

| Orchestrator | Presentation target | Routing and lifecycle |
|---|---|---|
| [InputOrchestrator](../../Assets/Scripts/Orchestrator/InputOrchestrator.cs) | `InputManager` | Persistent on the Input service root. Routes `BeforeTick` to input publication and `FramePublished` to the Run Session; forwards scene readiness and load-start facts to input gating and run readiness. M0 buffers input in Session; the Domain player consumer is M1 work. |
| [DebugOverlayOrchestrator](../../Assets/Scripts/Orchestrator/DebugOverlayOrchestrator.cs) | `DebugOverlayManager` | Persistent on the overlay service root. Routes completed ticks and run phase changes as primitive display values. Player speed and movement remain explicitly unavailable until M1 supplies telemetry. |

### 6b. SceneRoot (`[Scene]SceneRoot.cs`)
A **SceneRoot** is a scene-owned `MonoBehaviour` whose only job is to assemble a scene: instantiate prefabs, place objects, wire serialized references between the scene's Managers, Drivers, and Orchestrators, and hand the scene to the current Session (§8b) once assembled.
*   **Rules**: No game logic, no per-frame work, no state beyond what it needs during assembly. It may `Instantiate` and position objects because assembly *is* its concern. One per scene, in `Assets/Scripts/Orchestrator/Scenes/`. When assembly is complete it publishes `SceneReady` (a Core event payload) — Session Managers wait for that, never for `Start` ordering.

## 7. The Presentation Stack (`Driver/`)
The presentation stack is everything that touches the engine: UI Toolkit `VisualElement` binding and manipulation, Input System callbacks, physics bodies and callbacks, `Animator`/`PlayableGraph` work, runtime meshes, materials, particles, camera rigs, audio sources, and third-party engine-facing SDKs (atmosphere, tweening, etc.).

**The boundary rule that defines this stack:** *every engine call happens in a Driver or Sub-driver.* Not in a Manager (beyond the exceptions in §1), not in a Controller, not in a Presenter, not in a Content SO hook. If a script makes an engine call, it is presentation and lives here.

### What counts as an engine call
| Allowed anywhere (pure-safe) | Engine call (Driver / Sub-driver only, with the four exceptions in "Where does this code go?") |
|---|---|
| `Vector2/3/4`, `Quaternion`, `Matrix4x4`, `Mathf`, `Color`, `Rect`, `Bounds`, `Ray` (as a value) | Any method or property on a `Component`, `GameObject`, `Transform`, `VisualElement`, `Material`, `Mesh` (live instance) |
| `AnimationCurve.Evaluate`, `Gradient.Evaluate` on a Config-owned curve | `Physics.*`, `Physics2D.*` (raycasts, overlaps, casts) |
| `Debug.Log*`, `Debug.Assert` (sparingly in pure layers) | `Time.*`, `UnityEngine.Random.*`, `Application.*`, `Screen.*`, `Camera.main` |
| `System.*` (`System.Random`, `System.Math`, collections, LINQ) | `Object.Instantiate` / `Destroy` / `Find*`, `GetComponent*`, `SceneManager.*`, `Resources.*`, `AssetDatabase.*` |
| Reading serialized fields of a Config / Content SO | Coroutines, `Awaitable`, `Invoke`, any `MonoBehaviour` callback |

A physics query a Controller *needs* (is there a wall ahead?) is performed by the Driver on the Manager's request and the *result* is passed into the Controller as data. This costs one round trip and buys a Controller that runs in a test with no scene.

**Drivers are the engine boundary in both directions.** Output Drivers apply presentation; **input Drivers** (`[X]InputDriver`) wrap Input System actions, physics callbacks, and UI interaction and publish them as events. The purity rules are identical.

The stack mirrors the logic stack one-for-one, so the same purity rules apply and the same testability payoff results:

| Logic stack | Presentation stack | Nature |
|---|---|---|
| Manager | **Driver** (§7a) | `MonoBehaviour`. Owns engine objects; lifecycle + sequencing only. |
| Controller | **Presenter** (§7b) | Pure C#. All visual/physical math. Tested. |
| BehaviorState | **DriverState** (§7c) | Pure data. Transient presentation state (optional). |
| Config | **DriverConfig** (§7d) | `ScriptableObject`. Visual/physical tunables. |
| — | **Sub-drivers** (§7e) | Single-concern `MonoBehaviour`s owned by the Driver. |

The parallel is *structural*, not hierarchical: the Driver is not a peer of the Manager. **The Manager owns the Driver** and is the only thing that commands it. Data flows Manager ➡️ Driver as commands, and Driver ➡️ Manager as events — never the other way.

Everything in the stack lives together in the system's `Driver/` folder, except the DriverConfig, which lives in `Config/` with the other ScriptableObject schemas (§4a).

### Decomposition triggers (any one ⇒ split into the full stack)
A trivial Driver — one engine concern, a handful of primitive commands — may stand alone. A Driver **must** decompose into Presenter / DriverState / sub-drivers when:
*   it exceeds ~300 lines, or
*   it mixes two or more engine domains (rendering + physics + animation), or
*   it contains math you would want to test without a scene.

### 7a. Driver (`[System]Driver.cs`)
*   **Role**: Owns the engine objects, runs the coroutines / `Update`, and *applies* what the Presenter computed. For UI Toolkit systems, the Driver owns the `UIDocument` root query and all `VisualElement` binding. After decomposition it should read like a sequence script — one to two screens.
*   **Rules**:
    *   **Never pulls game state.** Data flows **in** only via (a) primitive command parameters (`UpdateColor(Color c)`, `Move(Vector3 pos)`), (b) its DriverConfig, or (c) a read-only state view its Manager hands it (an `IReadOnly[X]State`). Prefer (a); use (c) when the parameter list would become unwieldy. In every case the Manager *pushes* — the Driver never reads `SomeManager.Instance`, never subscribes to game systems, never looks anything up (§9).
    *   Data flows **out** only via C# events (`OnShatterComplete`, input events from `PatternInputDriver`). Event payloads are Core/Definitions types; the one exception is a raw `Collider`/`GameObject` from a physics callback, which the Manager resolves to an `EntityId` immediately (§1).
    *   **Receives commands from exactly one owner — its Manager.** Orchestrators and other systems never command a Driver directly.
    *   Global engine side effects (`Time.timeScale`, render settings) are allowed only when the Driver *owns* that concern for the duration of its sequence, and the ownership must be declared in the header's USAGE NOTES.
    *   **Third-party SDKs are wrapped here.** The rest of the system sees primitives and events, never a vendor type.
    *   There is one `[System]Driver` per system (facet systems: one per facet, §12), plus any `[X]InputDriver`s. Everything else in `Driver/` that is a `MonoBehaviour` is a sub-driver (§7e).

### 7b. Presenter (`[X]Presenter.cs`)
*   **Role**: The Controller of the presentation stack. Computes everything that is *math about visuals or physics*: mesh generation, interpolation paths, layout, curves, timing tables, pose computation, animation-phase transitions. Takes primitives + DriverConfig values (+ a DriverState, if the sequence has one), returns plain data for the Driver to apply.
*   **Naming note**: this is *not* the Presenter of Model-View-Presenter. An MVP presenter mediates between model and view; ours never sees either — it is a pure calculator. The mediating role belongs to the Manager.
*   **Rules**:
    *   Must not inherit from `MonoBehaviour` and makes no engine calls. Constructing data to *return* — a `Mesh`, a `Vector3[]` path — is fine; touching scene objects is not.
    *   Holds no state of its own. Multi-step sequences keep their current phase and bookkeeping in a DriverState (§7c) that the Presenter reads and updates, exactly as a Controller uses a BehaviorState. A "state machine" Presenter is therefore a set of pure transition functions over a DriverState.
    *   Time and randomness are parameters, as for Controllers (§2).
    *   **Every nontrivial Presenter ships with a test file** (§11). This is the main payoff of the split.
*   **Reference implementations**: [InputFramePresenter](../../Assets/Scripts/Presentation/Input/Driver/InputFramePresenter.cs) demonstrates pure input accumulation over a supplied `InputDriverState`; [its tests](../../Assets/Editor/Tests/Input/InputFramePresenterTests.cs) cover short taps, one-time edge consumption, look motion, and input gates. [DebugOverlayPresenter](../../Assets/Scripts/Presentation/DebugOverlay/Driver/DebugOverlayPresenter.cs) demonstrates display formatting over a supplied `DebugOverlayDriverState`; [its tests](../../Assets/Editor/Tests/DebugOverlay/DebugOverlayPresenterTests.cs) cover locale-independent numbers, invalid samples, and unavailable player telemetry. Both keep all state in their caller-owned DriverState and make no engine calls.

### 7c. DriverState (`[X]DriverState.cs`) — optional
*   **Role**: Transient presentation data for multi-step sequences: current phase, spawned-object lists, active tween registry.
*   **Rules**: Same purity rules as a BehaviorState (§3) — may hold engine references, never operates on them, never publishes events. Extract it when a Driver runs multi-step sequences; a simple stateless Driver skips it.

### 7d. DriverConfig (`[X]DriverConfig.cs`)
*   **Role**: Visual/physical tunables (UI offsets, material properties, physical forces, tween durations) — distinct from the system's logical parameters in its Config.
*   **Rules**:
    *   Default: every Driver has its own `[X]DriverConfig.cs`, living in the system's `Config/` folder with assets placed per §4a.
    *   Sharing the main system Config is permitted **only** when the Driver has zero tunables of its own, and the sharing must be declared in the Driver's header USAGE NOTES.

### 7e. Sub-drivers
*   **Role**: Small single-concern `MonoBehaviour`s the Driver spawns or owns for per-object engine work (`ShatterFragment` handling per-fragment fade; `ClashDashAnimationDriver` as a pose-holding playable-graph helper; a `HitboxTrigger` that forwards `OnTriggerEnter`).
*   **Rules**:
    *   Commanded only by their owning Driver — never by Managers, never by other systems, never by each other.
    *   Named descriptively for the object or concern they handle. A sub-driver may carry a `...Driver` suffix when it sequences something, but the bare `[System]Driver` name is reserved for the system's top-level Driver.
    *   The header's ARCHITECTURAL ROLE must read `Sub-driver (§7e), owned by [X]Driver` so ownership is readable without opening the owner.

### 7f. Presentation-layer systems
Systems in the Presentation layer (§9) — HUDs, menus, audio, camera rigs, VFX — have **no game rules**, so they have **no Controller and no BehaviorState**. Their shape is:

```
[Target]Orchestrator ──call──► [System]Manager ──► [System]Driver ── Presenter / DriverState / sub-drivers
[Target]Orchestrator ◄─event── [System]Manager ◄── Driver events (clicks, input, completion)
```

The Manager is thin — it receives Orchestrator calls, issues Driver commands, and re-publishes Driver events — but it still exists, because a Driver is commanded by exactly one Manager (§7a) and the Manager is where lifecycle, event subscription, and Config/DriverConfig ownership live. **Do not invent a Controller to fill the folder.** If real logic appears (a menu that validates a name), add a Presenter if it is about presentation, or move the rule into a Domain system if it is a game rule.

Presentation-layer systems reference only Core. They know nothing about Domain types: the Orchestrator has already translated everything into primitives and Core DTOs.

### 7g. Animation policy
An Animator Controller asset is a state machine — logic — stored in a non-versioned binary that agents cannot read, diff, or test. Therefore:
*   **Transition decisions live in a Presenter**, computed from primitives the Driver receives. The Driver applies them with `Play`/`CrossFade` or a `PlayableGraph`.
*   Animator Controller assets, when used at all, hold clips and blend trees only — no transition conditions, no parameters that encode game state.
*   Playables are preferred for anything sequenced in code (`ClashDashAnimationDriver` is the model).

---

## 8. Lifecycle & Scene Ownership

Every `MonoBehaviour` system belongs to exactly one of two lifecycle tiers, and its header's USAGE NOTES must state which:

*   **Persistent (`DontDestroyOnLoad`)**: Created once (boot scene or first access) and survives every scene load. Session Managers are always persistent; Services may be. Examples: `InputManager`, `MusicManager`, `EncounterSessionManager`, `SceneFlowManager`, the Orchestrators.
*   **Scene-owned**: Lives inside a scene and dies with it. Entities are always scene-owned. Examples: per-scene camera rigs, atmosphere volumes, arena setup objects, SceneRoots.

**M0 Run lifecycle:** [RunSessionManager](../../Assets/Scripts/Session/Run/Manager/RunSessionManager.cs) is persistent on its own root GameObject. [TagArenaSceneRoot](../../Assets/Scripts/Orchestrator/Scenes/TagArenaSceneRoot.cs) explicitly initializes the canonical services, supplies its serialized run seed, and publishes `SceneReady` once the scene is assembled. `InputOrchestrator` forwards readiness to the Run Session, which clears pending input, resets counters, and recreates the random source from the retained seed for the new run. Its `FixedUpdate` owns the 60 Hz tick: `BeforeTick` requests synchronous input publication, the Controller advances with explicit delta time, then `TickAdvanced` publishes the consumed frame and tick. Scene-load start suspends ticking until the next readiness hand-off. The Session retains no scene-object references; M0 has no Domain simulation registered yet.

### Rules
*   **Persistent systems must never cache scene-owned objects across loads.** A stale cached reference is exactly how the player-death health-reset bug happened. Re-acquire on scene load — or better, invert the dependency with the Registry pattern.
*   **Registry pattern** (`[Thing]Registry`): When a longer-lived system needs shorter-lived objects, the short-lived object *registers itself* on `OnEnable` and unregisters on `OnDisable` (`CameraRigRegistry` is the model; each Entity system's Factory registers spawned entities in `[Entity]Registry`). The long-lived side never goes hunting. A Registry holds only the collection of registrants — no logic — and lives in the `Manager/` folder of the system that consumes it. Registries expose lookups (`TryGet(EntityId, out IEntityHandle)`) and enumeration; nothing else.
*   **No Awake-order coupling between systems.** Never assume another system initialized first. Use a lazily-initializing `Instance` accessor (the `PlayerSessionManager` model) or explicit `Initialize()` calls from a single boot owner. If two systems must handshake at startup, the *later* one asks; the earlier one never pushes. Scene-level readiness is signalled by the SceneRoot's `SceneReady` event (§6b), never inferred from `Start`.
*   **`[DefaultExecutionOrder]` is a last resort**, reserved for engine-adjacent frame synchronization (e.g. `IKConstraintSync` at 10000 must run after all animation). Every use must be justified in the header's USAGE NOTES.
*   **Teardown is symmetric.** What `Awake` creates, `OnDestroy` destroys; what `OnEnable` subscribes, `OnDisable` unsubscribes — same pair, same method group. A Manager tears down its Drivers; a Driver tears down its sub-drivers; a Factory despawns what it spawned. Nothing is left for the scene unload to clean up by accident.
*   **BehaviorStates are never independently persistent** (§3).

### 8b. Sessions & Flows
A **flow** is any multi-step sequence that spans systems or scenes: overworld encounter → transition → battle scene → combat start → victory → return. Flows are owned by **Session Managers** (§1b) in the Session layer, never by Orchestrators (stateless) and never by a Domain Manager (single-system scope).

*   **Shape**: a Session Manager is a full system — `[X]SessionManager` + `[X]SessionController` + `[X]SessionBehaviorState` (+ Config). The State holds the current `Phase` (an enum in the system's Definitions) and whatever must survive a scene load. The Controller holds the phase transitions as pure functions (`Phase Next(Phase current, FlowEvent evt)`) and is tested (§11). The Manager sequences: it runs the coroutine/`Awaitable`, calls Domain Managers directly (downward), publishes phase-change events upward for Orchestrators, and waits on the events that come back.
*   **Scene loading is a Session concern.** Exactly one script — `SceneFlowManager` — calls `SceneManager.Load*`/`Unload*`. Everyone else asks it (`RequestLoad(SceneKey)`) and waits for its `SceneLoaded` event and the SceneRoot's `SceneReady`.
*   **Scene-load contract**: before requesting a load, a Session captures everything it needs *in its own State* (`EntityId`s, archetype keys, phase — never references). After `SceneReady`, scene-owned objects have registered themselves (§8) and the Session resumes from its phase by *asking* Registries. A Session that holds a scene object across a load has broken §8.
*   **Waiting is state, not a local variable.** "Waiting for the attack animation" is `Phase.AwaitingPresentation` in a BehaviorState, with the completion arriving as an event via an Orchestrator. A coroutine that yields on a Presentation event is fine; a coroutine that *is* the only record of where the flow is, is not — if the Manager is destroyed mid-flow, the phase must be recoverable from State.
*   **`SaveManager`** is a Session Manager that collects `[Serializable]` State snapshots from persistent Managers (§3) and restores them; it owns file I/O and versioning of the save format.

## 9. Layers & Communication

### The layer graph
Every system folder lives under exactly one layer folder (§12), and every layer is its own assembly definition (§13). References point along the arrows only.

```
                 ┌──────────────► Presentation ──┐
 Orchestrator ───┼──► Session ──► Domain ────────┼──► Core
                 └──────────────────────────────►┘
```

| Layer | Holds | May reference | Reaches everything else via |
|---|---|---|---|
| **Core** | `Core/Definitions/`, `Core/Utility/` — types and pure helpers shared by more than one system | nothing (UnityEngine value types only) | — |
| **Domain** | Game-rule systems: Stats, Combat, EnemyAI, Inventory, Pattern logic, all Entity systems | Core; other Domain systems in the declared, acyclic order (§2c) | events (upward) |
| **Session** | Flow owners (§8b): `SceneFlowManager`, `EncounterSessionManager`, `SaveManager` | Core, Domain | events (upward) |
| **Presentation** | Systems that only present (§7f): HUDs, menus, audio, camera, VFX, input | Core **only** | events (upward) |
| **Orchestrator** | `[Target]Orchestrator`s, `[Scene]SceneRoot`s | everything | — (top of the graph) |

Two consequences worth stating plainly:
*   **Domain and Session never see Presentation, and Presentation never sees Domain.** Combat publishes `HealthChanged(EntityId, int current, int max)`; `CombatHUDOrchestrator` hears it and calls `CombatHUDManager.SetHealth(...)`. The HUD Manager commands its Driver. The HUD does not know what an enemy is.
*   **Input is Presentation.** `InputManager` and its `[X]InputDriver`s publish Core-typed input events; an Orchestrator (`MenuOrchestrator`, `PatternInputOrchestrator`) routes them to the Domain or Session Manager that cares. Domain never reads input directly.

The *presentation stack* inside a Domain system (an enemy's mesh and animation, §7) is that system's own engine boundary and lives in the Domain layer with it. The *Presentation layer* is for systems that are nothing but a presentation stack.

### The communication ladder
How scripts talk to each other, in order of preference. Reach for the **lowest rung** that solves the problem:

1.  **Manager → own Controller / Driver / Factory**: plain method calls. The default inside a system.
2.  **Same-layer sibling Managers**: direct references, in the direction declared in the caller's header DEPENDENCIES; the graph stays acyclic (§2c).
3.  **Higher layer → lower layer**: direct calls (Session → Domain Manager; Orchestrator → anything). Data passed downward is Core-typed or a read-only view.
4.  **Lower layer → higher layer**: **C# events only** — "this happened," never "do this." The Orchestrator layer subscribes and routes. Managers publish state-change events; Drivers publish completion/interaction events to their Manager, which re-publishes what is relevant. Drivers never *subscribe* to game systems (§7a).
5.  **Singleton `Instance`**: allowed on persistent-tier Services and Sessions only, and only readable from the same or a higher layer. Entities and scene-owned components never expose `Instance`; Presentation systems never read one from Domain (they can't — different assembly).

### Event rules
Because the entire upward channel is events, event hygiene is architecture:
*   **Instance events on Managers are the default.** A publisher exposes `public event Action<Payload> OnThing;` and the subscriber holds a reference to the publisher.
*   **Static events** are permitted for exactly one case: a scene-owned publisher announcing itself to a longer-lived subscriber that cannot hold a reference to it yet (the Registry handshake, trigger drivers). Every static event is cleared in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` reset method in the same file, so Enter Play Mode without domain reload does not leak subscribers.
*   **Subscribe in `OnEnable`, unsubscribe in `OnDisable`** — same method group, no exceptions. Never subscribe in `Awake`/`Start` without the matching `OnDestroy`.
*   **Payloads are Core/Definitions types**: `EntityId`, primitives, DTOs. Never an engine object, never a Manager, never a mutable State (a read-only view is acceptable).
*   **Handlers are synchronous and return nothing.** No `async void` handlers; a handler that needs to wait sets a phase and lets its Manager's sequence pick it up (§8b).
*   **An event is a fact, not a request.** `OnEnemyDied`, not `OnPleasePlayDeathSound`. The subscriber decides what to do with a fact.

### Discovery rules
*   `Find*ObjectByType` is permitted only in self-heal/setup paths (Awake fallback wiring, editor tooling) — never per-frame, never as the primary wiring mechanism.
*   Scene-owned wiring uses serialized Inspector references (set by the SceneRoot or a Setup tool) or the Registry pattern (§8).
*   **Banned**: a Driver calling `SomeManager.Instance` to read game state. Patterns to copy instead: trigger drivers publish events the Manager subscribes to; `ClashCameraDriver` publishes computed poses its owner forwards; `EncounterTransitionDriver` receives a Manager-injected resolver callback for late-bound data.

## 10. Editor Tooling & Self-Healing

### What is versioned
This repository takes the stricter option: **everything under `Assets/` is versioned except what `.gitignore` excludes** — source (`*.cs`), assembly definitions (`*.asmdef`, `*.asmref`), the ast-grep rule files (§13), **and also `*.meta`, `*.uxml`, `*.uss`, scenes, prefabs, and text-serialized `*.asset` files**. They are small, diffable, and — for UXML, metas, and asmdefs — *are* architecture. `.gitattributes` already forces LF and the Unity YAML merge driver on them, and routes binaries to LFS.

Consequently the self-heal doctrine below covers a narrower surface than it would elsewhere: **binary and third-party assets**, plus anything a re-import can silently rewrite. It is not thereby optional. A versioned prefab still loses a component reference when a package is re-imported, and a fresh clone still needs a way to rebuild wiring nobody noticed was broken — so editor tooling remains a first-class architectural layer.

### The Self-Healing Doctrine
*   **Any component/config wiring stored in a non-versioned asset must be rebuildable by a deterministic editor tool.** If a system's runtime depends on a prefab carrying a component, a config asset existing, a UXML document being referenced, or a scene object being placed, ship a Setup/Generator that recreates that wiring from code. The current reference is [TagArenaSceneSetup](../../Assets/Editor/Scenes/TagArenaSceneSetup.cs), invoked by `Worsen/Scenes/1 — Build TagArena`. It creates missing mirrored Input/DebugOverlay config and UI panel assets, wires the persistent services and scene root, rebuilds `Assets/Scenes/TagArena.unity`, registers it in Build Settings, and sets the fixed timestep to 1/60. Existing config and panel assets are reused to preserve their identities and tunings. The builder refuses Play Mode and a dirty TagArena, and does not save or close unrelated scenes.
*   Where practical, add a cheap runtime self-heal fallback too: serialized reference first, `Resources.Load` by mirrored path second, a logged warning third.
*   **UI assets** (`.uxml`, `.uss`) live under `Assets/Resources/UI/[Layer]/[System]/`, mirroring the script tree like ScriptableObjects (§4a), so a Driver's self-heal path can `Resources.Load<VisualTreeAsset>` by convention.

### File Placement (unified — non-negotiable)
*   **All editor code** → `Assets/Editor/[System]/` (generators, scene setups, custom inspectors, drawers). One folder per system, mirroring the system name; project-wide windows and utilities in `Assets/Editor/Shared/`.
*   **All unit tests** → `Assets/Editor/Tests/[System]/` (§11).
*   No other locations. Editor code never lives under `Assets/Scripts/` — the layer assemblies there are runtime-only (§13).

### Menu Placement (unified — non-negotiable)
*   Every tool registers under the single top-level **`Worsen/`** menu: `[MenuItem("Worsen/[System or Group]/[Action]")]`.
*   Groups match system names. New entries must always be grouped — no bare items at the `Worsen/` root. **Never under `Tools/` or any second root menu.**
*   Multi-step workflows number their steps (`Scene Migration/1 — ...`) and provide a `Run All Steps` entry.

### Naming
`[System]ConfigGenerator`, `[System]SceneSetup`, `[Target]Setup`, `[Name]Window` (EditorWindow), `[TypeName]Editor` (custom inspector), `[TypeName]Drawer` (property drawer).

## 11. Testing

Tests are editor-mode NUnit suites targeting the pure C# layers — Controllers, Utilities, Presenters, Session Controllers, and Content SO hooks. This is the payoff of the purity rules in §2, §2b, §4b, and §7b: logic and visual math are testable without a scene or Play Mode.

*   **Canonical location — one folder, no exceptions**: `Assets/Editor/Tests/[System]/`, mirroring the system name. A test file targets one script and is named after it: `ClashPoseControllerTests.cs` tests `ClashPoseController.cs`. Every script's tests are findable from the script name alone.
*   When you modify a Controller, Utility, Presenter, or hook that has a test file, update the tests in the same change. A new one with nontrivial logic ships with its test file.
*   Tests inject a seeded `System.Random` and explicit `deltaTime` values (§2); a pure-layer script that cannot be driven this way has an engine dependency to remove.
*   **`ArchitectureConformanceTests.cs`** (in `Assets/Editor/Tests/Architecture/`) is a permanent, always-run suite that checks the codebase against this document (§13). It never gets skipped or `[Ignore]`d to make a change pass — fix the code or amend this document.
*   **Test *content* is not a test**: sample/placeholder ScriptableObject content lives in the owning system under a `Samples/` folder — never under a `Tests/` name — so "Tests" always means unit tests.
*   Do not create test folders inside `Assets/Scripts/`.

## 12. Folder & Naming Conventions

### Canonical project layout
```
Assets/
    Scripts/
        Core/                          Worsen.Core.asmdef       — refs: nothing
            Definitions/               EntityId, IEntityHandle, EntityContext, SpawnRequest, shared enums, event payloads
            Utility/                   shared static helpers
        Domain/                        Worsen.Domain.asmdef     — refs: Core
            [System]/                  system skeleton (below)
        Session/                       Worsen.Session.asmdef    — refs: Core, Domain
            SceneFlow/  Encounter/  Save/  ...
        Presentation/                  Worsen.Presentation.asmdef — refs: Core
            [System]/                  system skeleton, minus Controller/ and State/ (§7f)
        Orchestrator/                  Worsen.Orchestrator.asmdef — refs: Core, Domain, Session, Presentation
            [Target]Orchestrator.cs
            Scenes/[Scene]SceneRoot.cs
    Editor/                            Worsen.Editor.asmdef     — Editor platform only; refs: all runtime asmdefs
        [System]/                      tools (§10)
        Shared/
        Tests/                         Worsen.Tests.asmdef      — Editor platform only; refs: all + NUnit
            Architecture/ArchitectureConformanceTests.cs
            [System]/
    Resources/
        ScriptableObjects/[Layer]/[System]/...     mirror of Scripts/ (§4a)
        UI/[Layer]/[System]/*.uxml, *.uss          mirror of Scripts/ (§10)
```

### Canonical system skeleton
```
Assets/Scripts/[Layer]/[System]/
    Manager/       [System]Manager.cs  (+ facet Managers, [Entity]Factory.cs, [X]Registry.cs)
    Controller/    [System]Controller.cs, pure-C# dispatchers, system-local [Name]Utility.cs
    State/         [System]BehaviorState.cs, IReadOnly[System]State.cs
    Config/        [System]Config.cs, [X]DriverConfig.cs, Content SO classes
    Definitions/   [System]Definitions.cs  (types only — no logic, no utilities)
    Driver/        [System]Driver.cs, [X]InputDriver.cs, [X]Presenter.cs, [X]DriverState.cs, sub-drivers
    Samples/       (only if the system ships placeholder content)
```
Create only the folders the system actually uses — a Presentation-layer system (§7f) has no `Controller/` or `State/`; a Session system may have no `Driver/`.

*   **Namespaces mirror the path**: `Worsen.[Layer].[System]` (`Worsen.Domain.Combat`, `Worsen.Presentation.CombatHUD`, `Worsen.Core`, `Worsen.Orchestrator`), `Worsen.Editor.[System]`, `Worsen.Tests.[System]`. The conformance test checks this (§13).
*   **Class suffix mirrors folder**: a `*Controller` lives in `Controller/`, a `*Driver`/`*Presenter`/`*DriverState` in `Driver/`, and so on per the taxonomy. The suffix *is* the layer declaration; do not invent new suffixes without adding a taxonomy row.
*   **`State/` is the canonical folder name.** The class suffix remains `...BehaviorState`, but the folder is `State/`.
*   **Nested systems are allowed** (`Domain/Combat/EnemyAI/Tutorial/`): each nested system carries the full skeleton, and the mirror trees mirror the nesting.
*   **Multi-Manager ("facet") systems are allowed** (`PatternCombat` hosts Combat, Encounter, and IK facet Managers): multiple Managers may share one system folder when they share State and Definitions. Each facet Manager individually obeys §1, and each may own its own Driver.
*   **The mirror hierarchy is normative in both directions**: `Assets/Scripts/[Path]/` ↔ `Assets/Resources/ScriptableObjects/[Path]/` ↔ `Assets/Resources/UI/[Path]/`.
*   **No dead folders**: empty directories are deleted, not kept "for later." `Archive/` is a temporary parking lot at best — prefer deletion; git history is the archive.

## 13. Enforcement & Agent Workflow

Prose rules are not enforced by prose. This document is backed by four mechanical layers, and a change is not done until all four are green.

### 13a. Compile-time: assembly definitions
The layer graph in §9 is encoded as asmdef references (§12). A Domain script that references a Presentation type does not compile. A Presentation script that references a Domain type does not compile. Editor and test assemblies are Editor-platform only, so editor code cannot leak into builds. **Never add a reference to an asmdef to make something compile** — that is the guard working; restructure the code (publish an event, move a type to Core).

### 13b. Structural lint: ast-grep
[ast-grep](https://ast-grep.github.io/) rules live in `tools/ast-grep/rules/` with `sgconfig.yml` at the repo root, and run via `ast-grep scan` in the pre-commit hook and CI. Each rule's `message` cites the section it enforces. The starting rule set (verify node names against `ast-grep run --debug-query` after any tree-sitter upgrade):

| Rule id | Files | Checks | § |
|---|---|---|---|
| `pure-layer-not-monobehaviour` | `**/Controller/**`, `**/State/**`, `Driver/*Presenter.cs`, `Driver/*DriverState.cs`, `Core/**` | class has a `MonoBehaviour` base | §2, §3, §7b, §7c |
| `pure-layer-no-engine-calls` | same as above, plus `Config/*SO.cs` | `Physics.*`, `Time.*`, `UnityEngine.Random.*`, `SceneManager.*`, `Resources.*`, `Instantiate`, `Destroy`, `GetComponent*`, `Find*`, `Camera.main` | §7 |
| `state-declares-no-events` | `**/State/**`, `Driver/*DriverState.cs` | `event` declarations | §3, §7c |
| `driver-no-instance-read` | `**/Driver/**` | `$X.Instance` member access | §7a, §9 |
| `so-no-public-setter` | `**/Config/**` | public `set` accessor in a `ScriptableObject` subclass | §4, §4b |
| `lower-layer-no-orchestrator-ref` | `Domain/**`, `Session/**`, `Presentation/**` | identifier ending in `Orchestrator` | §6 |
| `scene-load-only-in-sceneflow` | everything except `Session/SceneFlow/**` | `SceneManager.Load*` / `Unload*` | §8b |
| `instantiate-only-in-allowed-types` | everything except `**/Driver/**`, `*Factory.cs`, `*SceneRoot.cs` | `Instantiate(` outside a Manager's own-Driver creation | §1, §1c, §6b |
| `static-event-has-reset` | files declaring `static event` | no `[RuntimeInitializeOnLoadMethod]` in the same file | §9 |
| `subscribe-in-onenable-only` | `**/*.cs` | `+=` on an event inside `Awake`/`Start` | §9 |
| `menu-under-project-root` | `Assets/Editor/**` | `[MenuItem("...")]` not starting with `Worsen/` | §10 |

Three rules in full, as the template for the rest:

```yaml
# tools/ast-grep/rules/pure-layer-not-monobehaviour.yml
id: pure-layer-not-monobehaviour
language: csharp
severity: error
message: "Pure layers are plain C# — remove the MonoBehaviour base (§2, §3, §7b, §7c)."
files:
  - "Assets/Scripts/**/Controller/**/*.cs"
  - "Assets/Scripts/**/State/**/*.cs"
  - "Assets/Scripts/**/Driver/*Presenter.cs"
  - "Assets/Scripts/**/Driver/*DriverState.cs"
  - "Assets/Scripts/Core/**/*.cs"
rule:
  kind: class_declaration
  has:
    kind: base_list
    regex: '\bMonoBehaviour\b'
```

```yaml
# tools/ast-grep/rules/driver-no-instance-read.yml
id: driver-no-instance-read
language: csharp
severity: error
message: "Drivers never pull game state — data is pushed in by the owning Manager (§7a, §9)."
files:
  - "Assets/Scripts/**/Driver/**/*.cs"
rule:
  pattern: $OWNER.Instance
```

```yaml
# tools/ast-grep/rules/pure-layer-no-engine-calls.yml
id: pure-layer-no-engine-calls
language: csharp
severity: error
message: "Engine calls belong in a Driver; pass the result in as data (§7 'What counts as an engine call')."
files:
  - "Assets/Scripts/**/Controller/**/*.cs"
  - "Assets/Scripts/**/State/**/*.cs"
  - "Assets/Scripts/**/Driver/*Presenter.cs"
  - "Assets/Scripts/**/Driver/*DriverState.cs"
  - "Assets/Scripts/**/Config/*SO.cs"
  - "Assets/Scripts/Core/**/*.cs"
rule:
  any:
    - pattern: Physics.$M($$$A)
    - pattern: Physics2D.$M($$$A)
    - pattern: Time.$P
    - pattern: UnityEngine.Random.$M($$$A)
    - pattern: SceneManager.$M($$$A)
    - pattern: Resources.$M($$$A)
    - pattern: Instantiate($$$A)
    - pattern: Destroy($$$A)
    - pattern: GetComponent<$T>($$$A)
    - pattern: GetComponentInParent<$T>($$$A)
    - pattern: GetComponentInChildren<$T>($$$A)
    - pattern: Camera.main
```

### 13c. Graph-level: GitNexus
GitNexus indexes the repo into a dependency/call graph and exposes it to agents over MCP. It covers what ast-grep cannot see — relationships *between* files:
*   **Before any change**, run its impact analysis on the files you intend to touch and read the affected call chains. A change whose blast radius crosses a layer boundary needs an event, not an edit.
*   **After any change**, re-index (`gitnexus analyze`) so the graph is current for the next agent.
*   **Saved conformance queries** (written in Cypher against the repo's schema, kept in `tools/gitnexus/queries/`): (1) any call or type reference from Domain/Session into Presentation, or from Presentation into Domain; (2) any cycle among Domain systems, using each system's folder as the node; (3) any Driver-layer file that calls a Manager, Controller, or Registry; (4) any Orchestrator whose method bodies exceed a handful of statements; (5) every `*Controller`/`*Presenter` with no test file referencing it. Run them in CI alongside `ast-grep scan`.
*   GitNexus generates context files (`AGENTS.md`/`CLAUDE.md`) and per-cluster skills. Those are *descriptive*; this document is *normative*. If the generated map contradicts this document, the code is wrong — fix the code.

### 13d. Runtime reflection: ArchitectureConformanceTests
`Assets/Editor/Tests/Architecture/ArchitectureConformanceTests.cs` reflects over the runtime assemblies and walks `Assets/Scripts/`:
*   Every `.cs` file begins with the §0 header and its ARCHITECTURAL ROLE names a taxonomy type, a layer, and a system.
*   Namespace equals `Worsen.[Layer].[System]` derived from the path (§12).
*   Class suffix matches its folder for every taxonomy row; every `MonoBehaviour` under `Manager/`, `Driver/`, or `Orchestrator/` and nowhere else.
*   Every `*Manager` header declares a system kind (§1b) and a lifecycle tier (§8); every Entity Manager implements `IEntityHandle`.
*   Every `*BehaviorState` that is referenced by a foreign system has an `IReadOnly*State` interface.
*   Every `*Controller`, `*Presenter`, `*Utility`, and `*SO` with more than N statements has a `*Tests.cs`.
*   Every `[MenuItem]` path starts with the project root.

### 13e. Definition of done for any change
1. Headers updated on every touched file (§0).
2. Tests updated or added for every touched pure-layer script (§11).
3. `ast-grep scan` reports zero findings.
4. GitNexus impact analysis reviewed; index refreshed.
5. `ArchitectureConformanceTests` and all unit tests pass.
6. If any rule here was inconvenient: the code changed, or this document changed in the same commit with a reason. Never both silently diverging.

---

### Example Workflow (Adding a New System)
1.  **Pick the layer and kind** (§9, §1b): Domain / Session / Presentation; Service / Entity / Session. Write them into the Manager header before anything else.
2.  **Define types**: `NewSystemDefinitions.cs` for system-local types; anything another system will see goes in `Core/Definitions/` (§5).
3.  **Define data**: `NewSystemBehaviorState` (+ `IReadOnlyNewSystemState` if anyone reads it) and `NewSystemConfig` (§3, §4). Place the Config asset per §4a.
4.  **Define logic**: `NewSystemController` taking state, config, and a `System.Random` in its constructor, exposing `Tick(float dt)` and result-returning methods (§2).
5.  **Bridge to the engine**: `NewSystemManager` instantiates state and controller, runs `Update()`, feeds the controller, routes its results (§1). Entity systems add `NewSystemFactory` and implement `Initialize`/`Teardown` (§1c).
6.  **Present it**: `NewSystemDriver` (+ `NewSystemDriverConfig`); decompose into Presenter / DriverState / sub-drivers once a §7 trigger fires. Skip steps 3–4 for a Presentation-layer system (§7f).
7.  **Connect systems**: declare dependencies in the header. Downward needs: direct calls. Upward needs: publish a Core-typed event and extend (or add) the `[Target]Orchestrator` that routes it (§6, §9).
8.  **Declare lifecycle**: persistent vs. scene-owned (§8); add a Registry if a longer-lived system needs to find instances of this one.
9.  **Test**: `NewSystemControllerTests.cs` (and `*PresenterTests.cs`) under `Assets/Editor/Tests/NewSystem/` (§11).
10. **Survive re-imports**: if the system depends on prefab/asset/UXML wiring, ship a `Worsen/NewSystem/...` setup menu item (§10).
11. **Run the gates** (§13e).

---

## Appendix A — Known Debt (migrate when touched)

Not rules — a ledger of places where the current codebase does not yet match the rules above. Fix each item when you next touch the affected code; do not do a project-wide sweep unless asked. Remove entries as they are resolved.

| Area | Debt | Target |
|---|---|---|
| — | *(empty — this project was scaffolded against these rules from the start)* | — |

**Third-party code is out of scope.** `Assets/Synaptic AI Pro/` and anything else vendored ships as its author wrote it. The enforcement layers are scoped to `Assets/Scripts/` and `Assets/Editor/` and deliberately do not see it; do not "fix" vendor code to match this document.

**Last audit note**: scaffold created and all four §13 gates verified green on 2026-09-14.
