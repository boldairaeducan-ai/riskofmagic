# Risk of Magic

FPS roguelite coop (Risk of Rain-like), medieval fantasy. Unity 6 · Input System · Netcode for GameObjects.

## Setup
1. Unity Hub → **Add project from disk** → this folder (Unity 6000.0+).
2. Accept the prompt to enable the new Input System backend (restart).
3. **Tools → Risk of Magic → Build Prototype Scene**.
4. Play → **Host**. A second instance (ParrelSync / Multiplayer Play Mode / build) → **Client**.

## Controls
| Action | Keyboard/Mouse | Gamepad |
|---|---|---|
| Move | WASD (ZQSD on AZERTY) | Left stick |
| Look | Mouse | Right stick |
| Jump | Space | A |
| Sprint (toggle) | Left Ctrl | L3 |
| Primary / Secondary | LMB / RMB | RT / LT |
| Utility / Special | Left Shift / R | LB / RB |
| Interact | E | X |
| Toggle cursor (+ character picker) | Esc | Start |

## Characters
| | Primary | Secondary | Utility | Special |
|---|---|---|---|---|
| **Archer** | Tir rapide | Flèche perforante (charge) | Roulade | Pluie de flèches |
| **Guerrier** | Combo d'épée (3 coups) | Tourbillon | Charge | Frappe sismique |
| **Arcaniste** | Projectile magique (charge → jusqu'à 6 cibles, tête chercheuse) | Explosion arcanique (repousse) | Bouclier arcanique (-95% dégâts, 3 s) | Blocage temporel (cône, fige 4 s) |
| **Druide** | Dard empoisonné (poison cumulable) | Nuage toxique (zone ciblée) | Bond sylvestre (empoisonne au passage) | Floraison fatale (fait éclater les poisons) |

## Architecture (`Assets/_Project/Scripts`)
- **Core** — `GameCatalog` (Resources, network ids for characters/projectiles), `GameObjectPool`, `Team`, `SkillSlot`.
- **Combat** — `Health` (server-authoritative, events on all peers), `DamageInfo`, `IDamageable`, `Hitbox`, `AreaDamage`.
  - **Projectiles** — `ProjectileDefinition` SO, `Projectile` (simulated on every peer, server copy deals damage), `ProjectileNetwork` (replication + owner prediction).
- **Characters** — `CharacterDefinition` SO (stats, movement, 4 skills), `CharacterStats` (hook for items/buffs).
- **Skills** — `SkillDefinition` SO base (owner → server → remote pipeline), `SkillController` (stocks, cooldowns, charge, validation).
  - **Types** — `ProjectileSkill`, `MeleeComboSkill`, `AreaSkill`, `DashSkill`, `BlinkSkill`, `ChainLightningSkill`. New skills = new asset; new behaviour = new subclass.
- **Player** — `PlayerAvatar` (root, replicated character choice), `PlayerMotor` (owner-simulated, dash), `PlayerLook`, `PlayerInputHandler`.
- **Enemies** — `EnemyActor` (definition, level scaling, `Killed` server event), `EnemyBrain` (detection, persistent aggro, movement styles Direct / Flank / Kite, leaps), `EnemyDodge` (sidesteps when hit or aimed at), `EnemyLeap`, `EnemyAttack` base + `EnemyMeleeAttack` / `EnemyLungeAttack` (bound, charge) / `EnemyArtilleryAttack` (telegraphed lob) / `EnemyProjectileAttack`.

## Bestiary
| Enemy | Style | Attack | Notes |
|---|---|---|---|
| Goule | Direct | Melee | Occasional dodge |
| Loup spectral | Flank | Bound (7 m) | Fast, cheap, comes in packs |
| Lame d'ombre | Flank (behind) | Fast melee | Dodges a lot |
| Ogre | Direct | Charge (12 m) | Tank, from difficulty 1.25 |
| Pyromancien | Kite | Lobbed fire bomb, ground marker | Backs off when approached |
| Squelette archer | Kite | Arrows | Rare, from difficulty 1.6 |
| Colosse d'os | Direct | Heavy melee | Teleporter boss |
- **Run** — `RunDifficulty` (RoR2 coefficient: time × players × stages → enemy level), `CombatDirector` (stage population of idle enemies + credit-based waves hunting players), `StageBounds` (playable volume, placed in each map), `Teleporter` (boss event, charge zone), `StageManager` (next stage: cleanup, revive, respawn).

## Tuning the director
`Assets/_Project/Prefabs/GameSystems.prefab` → *Combat Director*: **Stage population** (credits, distance from spawns), **Waves** (interval, size, `wavesHuntPlayers`), **Placement** (`minSpawnDistance` / `maxSpawnDistance`). Defaults live in `CombatDirector.cs`; *Build Prototype Scene* regenerates the prefab with those defaults.
- **Items** — `ItemDefinition` SO (rarity, stacking stat modifiers, server proc hooks), `PlayerInventory` (replicated stacks, deferred proc dispatch), `ItemPickup`. Proc types: `HealOnHit`, `HealOnKill`, `ExplodeOnKill`, `ExplodeOnHit`, `ChainOnHit`, `ProjectileOnHit`. Item damage has `ProcCoefficient = 0` (no infinite chains).
- **Economy** — `PlayerWallet` (gold), `GoldRewarder` (kill → gold, scaled by difficulty).
- **Interaction** — `IInteractable`, `PlayerInteractor`, `PurchaseInteractable` (cost scales with difficulty), `ChestInteractable` (rarity weights), `HealingShrine`. `InteractableSpawner` scatters chests on the NavMesh.
- **Vfx / Networking** — pooled effects + `VfxNetwork` (server-triggered effects), `ServerBootstrap` (spawns `GameSystems`).
- **UI** (uGUI + TextMeshPro) — `HUD.prefab` built by *Tools → Risk of Magic → Rebuild HUD Prefab*. `HudController` binds `HudWidget`s to the local player (health, gold, character, skills, charge, prompt, inventory, run info, death overlay), plus damage numbers, pickup labels, item notification and `ConnectionMenu`.

## Plugging in UI art
| What | Where |
|---|---|
| Fonts, panel/frame/bar/slot/button sprites, crosshair, gold icon, default icons, all colors, damage number colors/sizes | `Assets/_Project/UI/HudTheme.asset` (never overwritten by the tools) |
| Skill icons | `icon` on each skill asset (`Configs/Skills`) |
| Item icons | `icon` on each item asset (`Configs/Items`) |
| Character portraits | `portrait` on each character asset (`Configs/Characters`) |
| Layout | `Assets/_Project/UI/HUD.prefab` (edit freely; only *Rebuild HUD Prefab* overwrites it) |

Sprites with borders are 9-sliced automatically. `ThemedImage` / `ThemedText` pull from the theme; remove them on an element to style it by hand. Empty icon slots show the name as placeholder text.

Authority rules: movement = owner, health/damage/AI/gold/spawns = server. Damage always goes through `IDamageable.ApplyDamage` on the server.
