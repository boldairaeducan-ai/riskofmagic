# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Risk of Magic: first-person coop roguelite in the spirit of Risk of Rain, medieval fantasy setting.
Unity 6 (see `ProjectSettings/ProjectVersion.txt`), Netcode for GameObjects 2.x (host/client, 1–4 players),
Input System (actions built in code), uGUI + TextMeshPro, AI Navigation (NavMesh).
Game-facing text (UI, item/skill names and descriptions) is in **French**; code, comments and commits in English.

## Building, running, testing

- There is no CLI build, linter or test suite. Compilation and testing happen in the Unity Editor, which is usually
  **not available** in the agent environment: code cannot be compiled locally, so review C# carefully (namespace
  clashes, `UnityEngine.Object` vs `?.`, NGO API signatures) and expect the user to report console errors.
- **Tools → Risk of Magic → Build Prototype Scene** (`Editor/PrototypeSceneBuilder.cs`) regenerates almost every
  asset: configs (characters, skills, projectiles, DoTs, items, equipment, enemies, spawn cards), prefabs, VFX,
  materials, `Resources/GameCatalog`, the `NetworkPrefabs` list, the `Prototype` and `MainMenu` scenes and build settings.
  Gameplay values live in this builder: tuning done by hand in the generated assets is overwritten on the next run,
  so persistent balance changes go into the builder (or code defaults).
- **Tools → Risk of Magic → Rebuild HUD Prefab** (`Editor/HudBuilder.cs`) rebuilds `UI/HUD.prefab`; needed whenever a
  HUD widget is added. It never overwrites `UI/HudTheme.asset` (where art is plugged in).
- Play from `Scenes/MainMenu` (Solo / online join code / lobby). Playing `Scenes/Prototype` directly still works for
  debugging (Host/Client-by-IP panel while disconnected; Esc = pause menu, `UI/PauseMenu`). Multi-instance tests use Multiplayer Play Mode.
- Online play needs the **Multiplayer Services** package + a linked Unity Cloud project. `Scripts/Online` is a separate
  assembly compiled only when that package is installed (`versionDefines` + `defineConstraints`); the runtime only knows
  `IOnlineSessionService` / `OnlineSessions`.
- Unity generates `.meta` files and assets on the user's machine; they are committed by the user. Before pushing,
  fetch and rebase on the remote branch (the user pushes generated assets to the same branch).

## Assemblies

- `RiskOfMagic.Runtime` (`Assets/_Project/Scripts`), `RiskOfMagic.Editor` (`Scripts/Editor`, builders),
  `RiskOfMagic.Online` (`Scripts/Online`, optional).

## Architecture (big picture)

**Authority.** Movement is owner-authoritative (`OwnerNetworkTransform`, `PlayerMotor`). Everything else is server
authoritative: health/damage, AI, director, gold, items, statuses. Clients send intents through `[Rpc(SendTo.Server)]`
and the server validates (range, cooldown, gold, origin distance).

**Damage pipeline.** All damage goes through `IDamageable.ApplyDamage(DamageInfo)` on the server (`Health`, or `Hitbox`
for weak points). `Health.ApplyDamage` applies, in order: attacker `IDamageDealerHooks` (item bonuses) → shield →
`IHealthHooks` on the victim (`StatusEffects`, `PlayerInventory`) → `DamageTakenMultiplier` → `FlatDamageReduction`
(min 1); `IHealthHooks.TryPreventDeath` can cancel a death. `DamageInfo.IgnoreModifiers` bypasses all of it
(health costs). Static events: `Health.ServerDamaged` / `ServerKilled` (server) and `AnyDamaged` (all peers, damage
numbers). `DamageInfo.ProcCoefficient` = 0 on item/DoT damage so it never re-triggers items. `AreaDamage` hits each
Health once and can apply a `DotEffect`; `DotSystem` (server, static) runs DoTs and replicates an active-DoT mask for tints.

**Shared registries.** `GameCatalog` (Resources) gives network ids (list index) to characters, projectiles, items,
equipment and replicated VFX; RPCs send indices, never asset references. Anything spawned at runtime must be in the
`NetworkPrefabs` list; in-scene NetworkObjects are avoided (the builder assigns stable `GlobalObjectIdHash` values to
generated prefabs).

**Skills.** `SkillDefinition` ScriptableObjects are shared, stateless; per-player state is `SkillSlotState`.
`SkillController` runs owner → server → remote: `PrepareActivation` / `OnOwnerExecute` (prediction, movement) →
`ActivateRpc` (server re-checks stocks) → `OnServerExecute` (damage) → `ExecutedRpc` → `OnRemoteExecute` (feedback).
Charge-mode skills get `OnChargeStarted/OnChargeTick`. New skills are usually new assets of the generic types in
`Skills/Types`; new behaviour = new subclass.

**Projectiles** are not NetworkObjects: `ProjectileNetwork.FireServer` simulates the authoritative copy on the server
and RPCs visual copies to clients (the shooter predicts its own). Supports gravity, pierce, explosion, homing,
strict `maxRange`, DoTs. Server-triggered effects use `VfxNetwork`; local ones `VfxUtility` (pooled).

**Characters & stats.** `PlayerAvatar` is the player root (registry `PlayerAvatar.All/Local`, replicated character
index). `CharacterStats.Recalculate` combines `CharacterDefinition` + inventory stat modifiers + `StatusEffects`, on
every peer, whenever the character, items or statuses change.

**Items & equipment.** `ItemDefinition` (rarity Common/Uncommon/Rare/Legendary/Boss, stacking `StatModifier`s, server
hooks: on hit, on kill, on owner damaged, outgoing damage, prevent death). `PlayerInventory` replicates stacks in a
`NetworkList` and defers hit/kill procs to the next frame to avoid re-entrant damage. `EquipmentDefinition` (key F)
activates on the server via `PlayerEquipment`. Sources: chests (`RarityWeights`), `ChoiceChest` (menu), equipment
barrels, `Merchant`, boss rewards in `Teleporter`.

**Enemies.** `EnemyActor` (definition, level scaling, freeze, tints, `Killed` event) + `EnemyBrain` (server NavMesh AI:
detection radius with permanent aggro, styles Direct/Flank/Kite, `EnemyDodge`, `EnemyLeap` across NavMesh gaps) +
one `EnemyAttack` subclass. Enemy behaviour data is in `EnemyDefinition`.

**Run loop.** `GameSystems` prefab (spawned by `GameBootstrap` in the game scene) holds `RunDifficulty` (RoR2-style
coefficient; Hard mode multipliers), `CombatDirector` (stage population + credit waves; far non-hunting waves outside
the teleporter event), `InteractableSpawner`, `StageManager` (teleporter, next stage, game over, restart),
`GoldRewarder`, `ProjectileNetwork`, `VfxNetwork`. Placement uses the scene's `StageBounds`.

**Flow.** `MainMenu` → `LobbyState` (replicated lobby) → `RunSettings` (static, read by the server in the game scene)
→ `GameFlow.StartGame` loads `Prototype` through `NetworkSceneManager`. `PersistentNetworkManager` keeps one
NetworkManager across scenes; `NetworkConfig.PlayerPrefab` is null and `GameBootstrap` spawns player objects.

**Meta progression.** Local, per player: `MetaProgression` (static API over `PlayerProfile` JSON), `ChallengeTracker`
(event → counters), `ChallengeDefinition` assets in `GameCatalog.Challenges`. A reward is locked iff a challenge rewards it.
`GameCatalog.RollItem` filters by unlocks (server = host profile). Skill alternatives: `CharacterDefinition.*Alt`, loadout
mask replicated on `PlayerAvatar` (lobby → `RunSettings` → server; the owner also sends its profile loadout).

**Audio.** Local only, never replicated: `GameAudioEvents` maps replicated events (Health, wallet, inventory, teleporter,
game over) to sounds; definitions carry `SoundEvent` slots played where their feedback already runs. Slot assets are
generated empty by `Editor/AudioSlots.cs` (never overwritten, re-linked after each build); `AudioService.Pick` treats a
clip-less slot as unset.

**Animation.** Cosmetic only. `PlayerAnimationDriver` / `EnemyAnimationDriver` spawn the `modelPrefab` (+ player
`viewModelPrefab`) of the definitions and drive their Animators on every peer through `AnimatorBridge` (only sets the
parameters the controller has, names in `AnimParams`). Server-only cues reach clients via existing RPCs or
`EnemyActor.BroadcastCue`.

**UI.** `HudController` binds every `HudWidget` to the local player. `ThemedImage` / `ThemedText` pull sprites, fonts
and colors from `HudTheme`; widgets only fill in game data (icons come from skill/item/equipment/character assets).

## Working with the user

The user wants terse answers and asks to be consulted on design choices (they often answer with multiple-choice
questions); propose values in tables and wait for validation on design-heavy features (items, classes).

## Output style
- don't show code changes.
- reduce code output unless asked.
- No preamble, no trailing summary of what you just did.
- For research questions, answer in 10 lines or fewer unless I ask for depth.