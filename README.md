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
| **Mage** | Trait de feu (4 charges) | Orbe de givre (charge) | Transfert | Chaîne d'éclairs |

## Architecture (`Assets/_Project/Scripts`)
- **Core** — `GameCatalog` (Resources, network ids for characters/projectiles), `GameObjectPool`, `Team`, `SkillSlot`.
- **Combat** — `Health` (server-authoritative, events on all peers), `DamageInfo`, `IDamageable`, `Hitbox`, `AreaDamage`.
  - **Projectiles** — `ProjectileDefinition` SO, `Projectile` (simulated on every peer, server copy deals damage), `ProjectileNetwork` (replication + owner prediction).
- **Characters** — `CharacterDefinition` SO (stats, movement, 4 skills), `CharacterStats` (hook for items/buffs).
- **Skills** — `SkillDefinition` SO base (owner → server → remote pipeline), `SkillController` (stocks, cooldowns, charge, validation).
  - **Types** — `ProjectileSkill`, `MeleeComboSkill`, `AreaSkill`, `DashSkill`, `BlinkSkill`, `ChainLightningSkill`. New skills = new asset; new behaviour = new subclass.
- **Player** — `PlayerAvatar` (root, replicated character choice), `PlayerMotor` (owner-simulated, dash), `PlayerLook`, `PlayerInputHandler`.
- **Enemies** — `EnemyActor` (definition, level scaling, `Killed` server event), `EnemyBrain` (NavMesh AI), `EnemyAttack` base + `EnemyMeleeAttack` / `EnemyProjectileAttack`.
- **Run** — `RunDifficulty` (RoR2 coefficient: time × players × stages → enemy level), `CombatDirector` (credits → `SpawnCard` waves).
- **Economy** — `PlayerWallet` (gold), `GoldRewarder` (kill → gold, scaled by difficulty).
- **Interaction** — `IInteractable`, `PlayerInteractor`, `PurchaseInteractable` (cost scales with difficulty), `HealingShrine`.
- **Vfx / UI / Networking** — pooled effects, prototype IMGUI HUD, connection panel.

Authority rules: movement = owner, health/damage/AI/gold/spawns = server. Damage always goes through `IDamageable.ApplyDamage` on the server.
