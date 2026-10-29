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
| Toggle cursor | Esc | Start |

## Architecture (`Assets/_Project/Scripts`)
- **Combat** — `Health` (server-authoritative, events on all peers), `DamageInfo`, `IDamageable`, `Hitbox` (weak points / multipliers).
- **Player** — `PlayerAvatar` (root + registry), `PlayerMotor` (owner-simulated, `MovementConfig` SO), `PlayerLook`, `PlayerInputHandler`, `DebugHitscanWeapon` (temporary until the skill system).
- **Enemies** — `EnemyActor` (definition, death, `Killed` server event for score), `EnemyBrain` (NavMesh AI), `EnemyAttack` base + `EnemyMeleeAttack`, `EnemyDefinition` SO.
- **Networking** — `OwnerNetworkTransform`, `NetworkDebugPanel`.

Authority rules: movement = owner, health/damage/AI = server. Damage always goes through `IDamageable.ApplyDamage` on the server.
