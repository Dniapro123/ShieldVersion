# 2D Strategic Platformer (Unity) — Thesis Project

## Project Overview (Concept)
This project is an **asymmetric 2D multiplayer game** in a **Builder vs Attacker** formula, where each player has a different role and objective:

- **Builder (Defender)**: constructs a bunker from **modular rooms placed on a grid**, then prepares defenses using **traps** and **AI bots**.
- **Attacker**: controls a single character in a **2D platformer** environment and attempts to **destroy the reactor (bunker core)** within a limited time.

Gameplay is organized into three explicit phases:
1. **BuildRooms** — base construction (placing rooms on the grid + rule validation).
2. **PlaceTraps** — defense placement (traps/bots + placement validation).
3. **Play** — the actual round: combat, damage, objective (reactor), timer.

Networking follows a **client–server model with server authority**, where the server validates actions and synchronizes state to clients.

---

## Tech Stack
- **Unity 6** (`6000.0.62f1`)
- **Universal Render Pipeline (URP) 2D** (e.g. `17.0.4`)
- **C#**
- **Mirror Networking** (client–server architecture, server-authoritative replication)

---

## Architecture (High Level)
The project is structured into functional modules responsible for specific subsystems:

### Networking & Session
- `MyNetworkManager` — session startup, player creation, initialization.
- `NetworkPlayerSetup` / `PlayerRoleNet` — player setup and role assignment.

> Roles are assigned on the server side (e.g., first player = Builder, second player = Attacker) and synchronized to clients (e.g., via `SyncVar` with a hook).

### Game Phases
- `GamePhaseNet`, `PhaseGate`, `PhaseCommands` — phase flow control and restricting actions outside the active phase.

### Base Building (Grid)
- `BuildCommands`, `BuildConfig`, `RoomBuildClient` — room placement, `Grid ↔ World` mapping, rule validation.
- `RoomNet` — networked room state.
- Auto-generated walls/doors based on adjacency rules / masks.

### Traps & Defense
- `TrapPlaceClient`, `TrapCommands`, `TrapPlacementManager`, `TrapConfig` — trap selection, rotation, placement validation, network spawning.

### Combat & Health
- `PlayerCombatNet`, `ProjectileNet`, `NetworkHealth`, `ReactorHP` — shooting, damage application, player HP and reactor HP.

### Bot AI (FSM)
- `BotDefenderNet` — simplified AI using an **FSM** approach (detect target → react → attack).

### UI
- `UIManager`, `RoundUI`, `HUDHealthUI` — HUD, timer, round info, role/phase dependent UI.

---

## Project Structure (Unity / Assets)
Suggested structure used in the project:

- `Assets/Scenes` — scenes (connection/menu scene + gameplay scene)
- `Assets/Prefabs` — prefabs (rooms, characters, traps, projectiles, network objects)
- `Assets/Scripts` — code grouped by feature (networking, player, building, traps, combat, AI)
- `Assets/Mirror` — Mirror package content

Configuration is largely exposed through scene-level config components (e.g., `BuildConfig`, `TrapConfig`) to enable tuning without code changes.

---

## Controls
### Character (Attacker / during Play)
- Move: **A/D** or **←/→**
- Jump: **Space**
- Shoot: **LMB**

### Builder — BuildRooms
- Place room: **LMB**
- Change room type: **RMB**

### Builder — PlaceTraps
- Place trap: **LMB**
- Change trap type: **RMB**
- Rotate trap: **R**

---

## Requirements
- **Unity 6** (recommended: `6000.0.62f1`)
- Project configured for **URP 2D**
- **Mirror** included in the project

---

## Running (Play Mode / Local Testing)
1. Open the project in Unity.
2. Start the **Menu** scene.
3. From the menu:
   - run **Host** in the first instance,
   - run **Client** in the second instance (connect to the host).
4. Once connected, the HUD is shown and phase logic starts.

> Testing can be done on two PCs (LAN) or on one machine (two game instances).

---

## Build Instructions
1. Go to `File -> Build Settings...`
2. Add **Scenes In Build**:
   - **Menu** scene
   - **Gameplay** scene
3. Select the target platform (e.g., Windows).
4. Click **Build** or **Build And Run**.

### Multiplayer Builds
- Launch one build as **Host**.
- Launch another build as **Client** and connect to the host (IP/connection options depend on the in-game menu implementation).

---

## Testing Scope (as described in the thesis)
The thesis validation covers (among others):
- connection stability and session persistence,
- correct phase flow (action gating per phase),
- grid-based building rules and room connectivity,
- network-validated trap placement and spawning,
- bot logic and the damage/HP system.

---

## Author
Uladzislau Budziankou — Engineering Thesis (Wrocław University of Science and Technology, 2026)
