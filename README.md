# Risk of Magic

FPS roguelite coop (Risk of Rain-like), medieval fantasy. Unity 6 · Input System · Netcode for GameObjects.

## Setup
1. Unity Hub → **Add project from disk** → this folder.
2. Package Manager → Unity Registry → **Multiplayer Services** (online play by join code). Link the project to a Unity Cloud project (*Edit → Project Settings → Services*). Without it, only Solo is available.
3. **Tools → Risk of Magic → Build Prototype Scene** (builds the `MainMenu` and `Prototype` scenes, MainMenu first in the build).
4. Play from `MainMenu`: Solo, or *Créer une partie en ligne* → share the code → others *Rejoindre*. Lobby: character, ready, difficulty (host), start.
   Playing `Prototype` directly still works (debug panel: Host/Client by IP).

## Making a map
1. **Tools → Risk of Magic → Maps → New Map Scene** creates a ready-to-play map (Level root with `StageBounds`, `MapLayout`, `NavMeshSurface` collecting Physics Colliders, spawn points, example variant group and spots, `GameBootstrap`, HUD) and adds it to the build.
2. Build the level under `Level`. Alternatives go in a **`LayoutVariantGroup`**: each direct child is one option, one is picked per stage (same for every player); `allowNone` adds an empty option; groups can be nested.
3. Place **`SpawnSpot`**s: Teleporter and Merchant are put on one of them (random NavMesh point if none). Chests, barrels and shrines are scattered randomly. Spots inside an inactive variant are ignored.
4. Add the scene name to a stage pool in `Configs/MapRotation.asset` (stage N picks a random scene of pool N, then loops).
5. **Tools → Risk of Magic → Maps → Validate Open Map** reports missing pieces.

The NavMesh is rebaked by the server after the layout is applied; bake it in the editor too (NavMeshSurface → Bake) for editing.

### Map skeletons
**Tools → Risk of Magic → Maps → Generate Map Skeletons** creates 5 greybox maps in `Scenes/Maps` (asks before overwriting) and,
if `MapRotation` is still the default, sets them as stages 1–5:

| Scene | Content |
|---|---|
| `Map_GrimForest` | Gently rolling terrain, ~340 trees (trunks block sight lines), clearings with ruins (variants), optional thickets |
| `Map_Necropolis` | Walled crypt: open central plaza, two mausoleums (roofs 8 m, towers 16 m, ramps), tower bridge (full / broken / none), catacomb corridors with optional gates |
| `Map_MountainPeak` | Mountain terrain, temple on the summit (intact / collapsed), boulders; `BlizzardWeather` (irregular fog storms + snow, same on every peer) |
| `Map_VolcanoTomb` | Rolling terrain, lava basins (`HazardZone`: hurts players only), stepped temple with ramp variants, obsidian spires |
| `Map_ChaosSanctum` | Final arena (stage 5): walled disc, pillars, dais with the teleporter, optional cover |

**Out of the map:** below the `StageBounds` volume (minus `killMargin`, 5 m), a player is teleported back to a safe ground
position from ~1 s earlier and loses 20% max HP (never lethal, `FallRecovery` on the player prefab); an enemy dies.

Terrains, materials and NavMesh assets are generated next to the scenes (`Scenes/Maps/Terrain`). `HazardZone` (box/cylinder volume,
% max HP + flat damage per second) and `BlizzardWeather` can be reused in any map.

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
| Pause menu (solo: real pause; online: overlay) | Esc | Start |

## Characters
| | Primary | Secondary | Utility | Special |
|---|---|---|---|---|
| **Archer** | Tir rapide | Flèche perforante (charge) | Roulade | Pluie de flèches |
| **Guerrier** | Combo d'épée (3 coups) | Tourbillon | Charge | Frappe sismique |
| **Arcaniste** | Projectile magique (charge → jusqu'à 6 cibles, tête chercheuse) | Explosion arcanique (repousse) | Bouclier arcanique (-95% dégâts, 3 s) | Blocage temporel (cône, fige 4 s) |
| **Druide** | Dard empoisonné (poison cumulable) | Nuage toxique (zone ciblée) | Bond sylvestre (empoisonne au passage) | Floraison fatale (fait éclater les poisons) |

## Architecture (`Assets/_Project/Scripts`)
- **Audio** — `SoundEvent` SO, `AudioConfig` (Resources), `AudioService` (pooled voices, music crossfade, volumes), `GameAudioEvents` (event → sound), `PlayerAudio`, `EffectSound`, `SceneMusic`, `UiSoundTrigger`.
- **Core** — `GameCatalog` (Resources, network ids for characters/projectiles), `GameObjectPool`, `Team`, `SkillSlot`.
- **Combat** — `Health` (server-authoritative, events on all peers), `DamageInfo`, `IDamageable`, `Hitbox`, `AreaDamage`.
  - **Projectiles** — `ProjectileDefinition` SO, `Projectile` (simulated on every peer, server copy deals damage), `ProjectileNetwork` (replication + owner prediction).
- **Characters** — `CharacterDefinition` SO (stats, movement, 4 skills), `CharacterStats` (hook for items/buffs).
- **Skills** — `SkillDefinition` SO base (owner → server → remote pipeline), `SkillController` (stocks, cooldowns, charge, validation).
  - **Types** — `ProjectileSkill`, `MeleeComboSkill`, `AreaSkill`, `DashSkill`, `BlinkSkill`, `ChainLightningSkill`. New skills = new asset; new behaviour = new subclass.
- **Player** — `PlayerAvatar` (root, replicated character choice), `PlayerMotor` (owner-simulated, dash), `PlayerLook`, `PlayerInputHandler`.
- **Enemies** — `EnemyActor` (definition, level scaling, `Killed` server event), `EnemyBrain` (detection, persistent aggro, movement styles Direct / Flank / Kite, leaps), `EnemyDodge` (sidesteps when hit or aimed at), `EnemyLeap`, `EnemyAttack` base + `EnemyMeleeAttack` / `EnemyLungeAttack` (bound, charge) / `EnemyArtilleryAttack` (telegraphed lob) / `EnemyProjectileAttack`.

## Paladin & Ombre
| | Clic G | Clic D | Maj | R |
|---|---|---|---|---|
| **Paladin** (140 PV, holy) | Marteau béni (heals allies on hit) / *Lame radieuse* | Jugement (stun) / *Bouclier projeté* | Égide (-50% damage, allies) / *Charge du croisé* | Sol consacré (heal + damage zone) / *Intervention divine* |
| **Ombre** (85 PV, 2 jumps) | Dagues jumelles (backstab crits) / *Lames saignantes* | Dagues de lancer / *Fumigène* | Voile d'ombre (stealth) / *Pas de l'ombre* | Marque de mort / *Danse des lames* |

Unlocks: Paladin = reach stage 5, Ombre = 1 000 kills. Statuses: `Stealth` (enemies ignore the player, first hit x2),
`Marked` (+50% damage taken), `Ambush` (next hit crit). Backstab = attacker behind the target (`SkillContext.IsBehind`).

## Bestiary
| Enemy | Style | Attack | Notes |
|---|---|---|---|
| Goule | Direct | Melee | Occasional dodge |
| Loup spectral | Flank | Bound (7 m) | Fast, cheap, comes in packs |
| Lame d'ombre | Flank (behind) | Fast melee | Dodges a lot |
| Ogre | Direct | Charge (12 m) | Tank, from difficulty 1.25 |
| Pyromancien | Kite | Lobbed fire bomb, ground marker | Backs off when approached |
| Squelette archer | Kite | Arrows | Rare, from difficulty 1.6 |
| Colosse d'os | Direct | Ground slam + expanding wave (jump over it) / lobbed boulder / charge | Teleporter boss |
| Reine araignée | Kite | Poison spit / summons 4 spiderlings (max 8) / pounce | Teleporter boss |
| Drake de braise | Direct | Fire breath cone (burn, slow turn) / charge / lobbed fireball | Teleporter boss from stage 2 |
| Araignée | Direct | Melee | Summoned by the Spider Queen only |

Bosses: the teleporter picks a weighted random boss among the cards unlocked at the current stage (`SpawnCard.minStage`), never the previous boss; one extra boss from stage 4. Enemies can carry several `EnemyAttack` components: the brain picks a random ready one in range (`selectionWeight`), the primary one drives the approach distance.
- **Run** — `RunDifficulty` (RoR2 coefficient: time × players × stages → enemy level), `CombatDirector` (stage population of idle enemies + credit-based waves hunting players), `StageBounds` (playable volume, placed in each map), `Teleporter` (boss event, charge zone), `StageManager` (next stage: cleanup, revive, respawn).

## End of run
A loop is 5 stages (`StageManager.stagesPerLoop`). The 5th is the final stage: its teleporter spawns a final boss
(`Teleporter.finalBossCards`, one picked at random: add more avatars there). Once the event is over, a victory seal appears next
to the teleporter: the seal ends the run (victory), the teleporter starts a new loop (stage 6 = stage 1 pool, difficulty kept).
The `MapRotation` should have 5 pools (the 5th = final maps). The end screen (victory or defeat) shows the run stats, one row per
player (damage, kills, deaths, gold, items) and Rejouer (host) / Retour au menu. Needs **Rebuild HUD Prefab**.

**Avatar du chaos** (4000 HP, Kite): homing volley of 5 chaos bolts; meteor shower (one per player + 2 around, 45 dmg, 5 m, marked
1.6 s ahead). At 66%: splits into 3 illusions (4% HP, 40% damage, same look) and blinks among them, summons ghouls/wolves/
pyromancers from now on. At 33%: splits again, meteors twice as often (+1 per player). Illusions vanish after 25 s or at its death.

## Plugging in models & animations
| What | Where |
|---|---|
| Player body (third person, shadow only for its owner) | `modelPrefab` on each character (`Configs/Characters`) |
| First-person arms/weapon (parented to the owner's camera) | `viewModelPrefab` on each character |
| Enemy model (placeholder hidden, hitboxes kept) | `modelPrefab` on each enemy (`Configs/Enemies`), authored at scale 1, feet at the pivot |
| Per-skill animation | `animatorTrigger` on each skill (default `Cast`) + int `SkillSlot` |
| Per-attack enemy animation | `animatorTrigger` on each `EnemyAttack` (default `Attack`) + int `AttackIndex` (component order) |
| Animation events | `AnimationEventRelay` on the Animator object: `PlaySound(SoundEvent)`, `PlayVfx(GameObject)`, `Footstep()` |

`PlayerAnimationDriver` / `EnemyAnimationDriver` drive every Animator found in those models on every peer. Create only the
parameters you need (others are ignored, no warnings). Root motion is off (movement is gameplay-driven).

| Type | Parameters |
|---|---|
| Float | `Speed`, `MoveX`, `MoveY` (local m/s), `VerticalSpeed`, `AimPitch` (owner only) |
| Bool | `Grounded`, `Sprinting` (owner only), `Downed`, `Dead`, `Frozen` |
| Int | `SkillSlot`, `AttackIndex` |
| Trigger | `Jump`, `Land`, `Hit`, `Cast` (or the skill's trigger), `UseEquipment`, `Revive`, `Attack` (or the attack's trigger), `Dodge`, `Leap`, `Spawn` |

## Map events
0 to 2 per stage (never twice the same), placed like interactables (on `Interactable` SpawnSpots when the map has some).
**Every value is in `Assets/_Project/Resources/MapEventsConfig.asset`** (count per stage, toggles, weights, rewards): created once
by the builder, never overwritten.

| Event | Rule | Reward |
|---|---|---|
| Tombe à purifier | Purification only progresses while a player stands in the 6 m zone (30 s); 2–3 monsters every 8 s | Healing zone 8 m (5%/s, 20 s) + 1 item per player (Uncommon, 25% Rare) |
| Autel de défi | Free; wave of elites (4 + 1/player, one affix), hunting right away | All dead → 1 item per player (Uncommon, 35% Rare) |
| Coffre maudit | Costs 20% max HP until the end of the stage (stacks, `Withered` status) | Rare item (10% Legendary) |

## Player levels
RoR2-style: every kill gives the whole team experience equal to the enemy's gold reward (already scaled by the difficulty).
Level L needs `20 × (1.55^(L−1) − 1) / 0.55` total experience (`PlayerExperience`). Each level above 1: +20% base damage,
+30% base max health, +20% base regen (`CharacterDefinition.damagePerLevel / healthPerLevel / regenPerLevel`). Shown as a
thin bar under the health bar and a small "Niv. N" label (pulses on level up, optional `AudioConfig.levelUp` sound).
Kept across stages, reset on a new run.

## Meta progression
Local profile per player (`Application.persistentDataPath/profile.json`): counters (global and per character), completed challenges,
discovered items, bestiary, skill loadouts, best victory time. `Meta/ChallengeTracker` turns replicated events into counters on each
peer; `Configs/Challenges/*.asset` (`ChallengeDefinition`: stat, target, optional character, reward) are generated by the builder.
Anything rewarded by a challenge starts locked. In coop, items are rolled with the **host's** unlocks; each player picks among their
own characters and skill alternatives (lobby: one button per slot toggles the slot's alternative). Main menu **Journal**: statistics,
challenges, items, bestiary. Delete `profile.json` to reset.

| Unlock | Challenge |
|---|---|
| Arcaniste / Druide | Reach stage 3 / defeat 3 teleporter bosses |
| Hard mode | Win once |
| Plume de phénix, Œil du chaos, Orbe de tempête, Sablier ancien, Carquois enchanté, Lame du bourreau, Bouclier de chêne | 10 downs, beat the Chaos Avatar, 50 elite kills, a stage under 5 min, 50 chests, 3 tombs, 3 altars |
| 16 alternative skills (one per slot and character) | Character-specific challenges (see the Journal) |

## Elites
From run progress 1.5, a director wave has a 35% chance to be elite (one affix for the whole wave) if the boosted cost is affordable.
Bosses can be elite from stage 3 (50%). Affix assets: `Configs/Enemies/Elite_*.asset` (registered in the `GameCatalog`).

| Affix | Effect | Normal / boss multipliers |
|---|---|---|
| Ardent | Hits burn, fire trail on the ground (3 s, 3 dmg/0.5 s) | HP ×4 / ×1.5, damage ×2 / ×1.25, cost ×6, gold ×4 / ×1.5 |
| Glacial | Blizzard 6 m: 2 dmg/0.5 s + 30% slow | same |
| Foudroyant | Telegraphed lightning strike on its target every 4 s (12 dmg, 2.5 m) | same |
| Éthéré | Invulnerable 1 s after a hit (every 4 s max), blinks next to a target farther than 12 m | same |

Elites are tinted, have a ground aura and an adjective in their name (`EnemyDefinition.feminineName` picks the agreement).

## Tuning the director
`Assets/_Project/Prefabs/GameSystems.prefab` → *Combat Director*: **Stage population** (credits, distance from spawns), **Waves** (interval, size, `wavesHuntPlayers`), **Placement** (`farSpawnDistance` = distance to the nearest player for normal waves; `minSpawnDistance` / `maxSpawnDistance` = ring around a player during the teleporter event, where waves also hunt). Defaults live in `CombatDirector.cs`; *Build Prototype Scene* regenerates the prefab with those defaults.
- **Items** — `ItemDefinition` SO (rarity Common/Uncommon/Rare/Legendary/Boss, stacking stat modifiers, server hooks: on hit, on kill, on owner damaged, outgoing damage, prevent death), `PlayerInventory` (replicated stacks, deferred procs), `ItemPickup` (items auto-pickup, equipment with E), `RarityWeights`.
- **Equipment** — `EquipmentDefinition` SO (key F, cooldown, server activation), `PlayerEquipment` (one slot, swapping drops the old one): Corne de ralliement, Tome sombre, Pierre de foudre.
- **Status** — `StatusEffects` (replicated: Curse, Rally, Warhorn), `DotSystem` (Poison, Bleed, Burn), Health hooks (`IHealthHooks`, `IDamageDealerHooks`).
- **Economy** — `PlayerWallet` (gold), `GoldRewarder` (kill → gold, scaled by difficulty).
- **Interaction** — `IInteractable`, `PlayerInteractor`, `PurchaseInteractable` (cost scales with difficulty), `ChestInteractable` (rarity weights), `ChoiceChest` (pick 1 of 3), `EquipmentBarrel`, `Merchant` (shop + reroll), `HealingShrine`. `InteractableSpawner` scatters chests on the NavMesh.
- **Flow** — `GameFlow` (scenes, return to menu), `LobbyState` (replicated lobby), `RunSettings` (lobby choices → game), `GameBootstrap` (spawns systems and players in the game scene), `PersistentNetworkManager`.
- **Online** (separate assembly, compiled only with the Multiplayer Services package) — join-code games behind `IOnlineSessionService`.
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

## Plugging in audio
Run **Tools → Risk of Magic → Create Audio Slots** (also run by *Build Prototype Scene*): it creates `Resources/AudioConfig.asset`
and one empty `SoundEvent` per sound slot in `Assets/_Project/Audio/Sounds/<folder>/<owner>_<field>.asset`, linked to its owner.
Slots are never overwritten: drop `AudioClip`s into them (several clips = random pick). Empty slots are silent.

| What | Where |
|---|---|
| Mixer (optional, groups Effects/UI/Music + exposed `MusicVolume`/`EffectsVolume` in dB), UI click/hover, hit/crit/kill confirm, player hurt/downed/revived/death, gold, purchase, item pickup per rarity, teleporter, game over | `Resources/AudioConfig.asset` |
| Music: main menu, stage, boss (teleporter event), game over | `AudioConfig` music tracks; a `SceneMusic` component in a map scene overrides the stage music |
| Skill cast | `castSound` on each skill (`Configs/Skills`) |
| Projectile fire / impact | `fireSound` / `impactSound` (`Configs/Projectiles`) |
| Enemy spawn / attack windup / hurt / death | `Configs/Enemies` |
| Footsteps, jump, landing | `Configs/Characters` |
| Equipment activation | `activateSound` (`Configs/Equipment`) |
| Explosions, lightning, pulses... | `EffectSound` on each VFX prefab (`Prefabs/Vfx`) |
| Per-item pickup / per-interactable purchase overrides | optional `pickupSound` / `purchaseSound` (empty = AudioConfig default) |

`SoundEvent`: clips, volume/pitch ranges, 2D/3D + distances, cooldown and max voices (anti-spam). Nothing is replicated:
each peer plays sounds from events it already receives. Code: `AudioService.PlayAt / Play2D / SetOverrideMusic`.

Authority rules: movement = owner, health/damage/AI/gold/spawns = server. Damage always goes through `IDamageable.ApplyDamage` on the server.
