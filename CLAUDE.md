# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**TopDown Tactical AI Framework** — A Unity 6 (2D) AI framework for top-down shooters. Built around a state machine architecture with Blackboard pattern, featuring Vision, Memory, Patrol, Chase, Search, Combat, Cover, Dodge, Tactical Decision, Group AI, Animation, Audio, and Debug systems.

**Namespace:** `TopDownTacticalAI`

## Build & Run Commands

This is a Unity project — there are no CLI build/test commands. Development workflow:

- **Open in Unity Hub** → Select Unity 6 (2022.3 LTS or 6000.x)
- **Scenes:** `Assets/Scenes/MainMenu.unity` → `LevelSelect.unity` → `Level1.unity`
- **Play Mode** in Editor to test
- **Build:** File > Build Settings > Build (Windows/Mac/Linux/WebGL)

### Unity Package Dependencies (Packages/manifest.json)

Key packages:
- `com.unity.render-pipelines.universal` 17.3.0 (URP)
- `com.unity.inputsystem` 1.19.0 (New Input System)
- `com.unity.ai.navigation` 2.0.13 (NavMesh)
- `com.unity.2d.*` packages (Animation, Tilemap, SpriteShape, PSD Importer, Aseprite)
- `com.unity.test-framework` 1.6.0 (for unit tests)
- `com.unity.visualscripting` 1.9.11
- `com.unity.ugui` 2.0.0
- `com.unity.timeline` 1.8.12

## High-Level Architecture

### Core AI Loop (EnemyBrain.cs)

`EnemyBrain` is the central hub that composes all systems and drives the state machine each frame:

```
Update():
  1. Sync HP from Health component
  2. Sync Player Mana awareness (Utility AI bias)
  3. VisionSensor.Tick() — updates CanSeeTarget, DistanceToTarget, CurrentTarget
  4. Record sighting to EnemyMemory if visible
  5. EnemyMemory.Tick() — auto-forget after MemoryDuration
  6. ReloadController.Tick()
  7. Alert Phase: accumulate/decay SuspicionLevel, set IsTargetConfirmed
  8. TacticalDecision.DecideNextState() — selects EnemyState with hysteresis
  9. PrioritySystem guards against lower-priority state interrupting Dodge
  10. StateMachine.Tick() — runs current state logic
  11. Accumulate DeterminationScore during active pursuit
  12. MapBounds.Clamp() — keep inside map bounds
  13. Update AnimationController parameters
```

**FixedUpdate()** runs `StateMachine.FixedTick()` for physics-dependent states.

### State Machine

- `StateMachine` (Core) — Dictionary<EnemyState, IAIState>, handles Enter/Tick/FixedTick/Exit
- `IAIState` interface — Enter, Tick(deltaTime), FixedTick(fixedDeltaTime), Exit
- `EnemyState` enum — Patrol, Suspicious, Chase, Search, Combat, Cover, Dodge

### Blackboard (Core/Blackboard.cs)

Central data store shared by all systems (no direct dependencies between systems):

- **Target Info:** CurrentTarget, CanSeeTarget, DistanceToTarget
- **Memory:** LastSeenData, HasMemory
- **Combat:** CurrentAmmo, MaxAmmo, IsReloading, TargetIsReloading
- **Health:** CurrentHP, MaxHP, IsLowHP (≤30%)
- **Cover:** CurrentCover, InCover, IsPeeking
- **Dodge:** IsDodging, DodgeDirection
- **Group:** NearbyAllyCount
- **Utility AI:** PlayerManaLow (drives Cautious vs Aggressive bias)
- **Alert Phase:** SuspicionLevel (0-1), IsTargetConfirmed
- **Path Visualization:** CurrentDestination
- **Meta:** DeterminationScore (accumulated "effort" for debug HUD)

### Key Subsystems

| Folder | Purpose |
|--------|---------|
| `Vision/` | VisionSensor, FieldOfView (cone), RaycastDetector (LoS), TargetDetector |
| `Memory/` | EnemyMemory (timer), LastSeenData, TargetMemory (breadcrumb trail), MemoryTimer |
| `Patrol/` | PatrolState, WaypointPatrol, CoveragePatrol (sunflower distribution), IdleLookAround |
| `Chase/` | ChaseState, ChaseMovement, TargetPrediction, PathFollowing |
| `Search/` | SearchState (systematic, returns to center between spokes), SearchPattern, Investigation |
| `Combat/` | CombatState, AimController, ShootController, ReloadController, AttackDecision |
| `Cover/` | CoverState, CoverScanner, CoverPoint, CoverDecision, PeekSystem, RetreatSystem |
| `Dodge/` | DodgeState, BulletDetector, DodgeDecision, DodgeMovement, SafePositionFinder |
| `Tactical/` | TacticalDecision (top-level), PrioritySystem, RiskEvaluation, Flanking, GroupAI |
| `Animation/` | AnimationController (sets Animator params), AimAnimation, MovementAnimation |
| `Audio/` | FootstepAudio, AlertAudio, CombatAudio |
| `Debug/` | StateDebugger, MemoryDebugger, CoverDebugger, GizmosDrawer, AIDebugDisplay |
| `Utilities/` | MathUtility, PhysicsUtility, TimerUtility, ExtensionMethods, SteeringMovement, StuckDetector |
| `Map/` | MapBounds (auto-generates boundary walls), BoundsClamper |
| `Player/` | PlayerController (WASD + Shift run + Space dash), PlayerShoot, Health, Bullet |
| `UI/` | SettingsManager (singleton, DontDestroyOnLoad), SettingsPanelBinder (per-panel), MainMenuController, PauseMenu, LevelSelectController, FpsCounterDisplay, GameDifficulty |

### Player Mana Awareness (Utility AI)

Per design doc Chapter 3.1.2: `Blackboard.PlayerManaLow` biases tactical decisions:
- **Mana High (not Low)** → Cautious: `dangerRange * 1.3`, seeks cover earlier
- **Mana Low** → Aggressive: `dangerRange * 0.6`, takes more risks, presses attack

### Group AI (Tactical/GroupAI.cs)

Singleton coordinating multiple enemies:
- `Register/Unregister` enemies
- `BroadcastAlert` — when damaged or first sighting, alerts allies within `AlertShoutRadius`
- `EmitNoise` — player footsteps/gunshots attract nearby enemies
- `GetActiveChaseIndex` — assigns flanking roles (index 0 = direct chase, others = flank)
- `ComputeSeparationPush` — prevents enemies stacking on each other

### Alert Phase (Suspicious State)

Before confirming target, AI accumulates `SuspicionLevel`:
- Builds while `CanSeeTarget` at rate `1/SuspicionBuildTime` (default 1.2s)
- Decays when not seeing at rate `1/SuspicionDecayTime` (default 2s)
- At 1.0 → `IsTargetConfirmed = true` → enters Chase/Combat/Cover
- Forgets completely → resets to Patrol

### Enemy Roles (EnemyRole enum)

Presets applied in `EnemyBrain.ApplyRolePreset()`:
- **Aggressive** — close range, fast chase, low dodge sensitivity
- **Defensive** — long range, seeks cover early, slower chase
- **Sniper** — very long range, minimal chase, extended vision
- **Scout** — wide vision/patrol, large alert radius, fast patrol
- **Custom** — no preset applied, all Inspector values used as-is

## Key Patterns & Conventions

### Rigidbody2D Handling

- **Enemies:** `EnemyBrain.Awake()` forces `RigidbodyType2D.Kinematic` + `freezeRotation = true` — AI writes `transform.position` directly
- **Player:** `PlayerController.Awake()` sets `freezeRotation = true`, `interpolation = Interpolate`, `collisionDetectionMode = Continuous` — uses `Rigidbody2D.linearVelocity` for movement
- **Bullets:** Dynamic with `isTrigger` Collider2D, stick on impact (`Bullet.cs`)

### Layer Mask Setup (Required)

| Layer | Used By |
|-------|---------|
| `Player` | TargetMask |
| `Obstacle` | ObstacleMask (walls, MapBounds walls) |
| `Cover` | CoverMask (CoverPoint triggers) |
| `PlayerBullet` | PlayerBulletMask (Dodge detection) |
| `EnemyBullet` | Prevents friendly fire |

### Settings Persistence

- `SettingsManager` (Singleton, DontDestroyOnLoad) stores all settings in `PlayerPrefs`
- `SettingsPanelBinder` attaches to each Settings Panel (Main Menu, Pause Menu) — wires UI to SettingsManager on `OnEnable`
- Supports: Master/Music/SFX Volume, Mouse Sensitivity, Screen Shake, FPS Counter, Fullscreen, Resolution, Quality Level

### Debug & Visualization

- `AIDebugDisplay` — OnGUI HUD above enemy showing State, Reason, Target info, HP, Ammo, DeterminationScore
- `VisionDebugger` — draws vision cone in Scene View
- `AIPathVisualizer` — draws purple line to `Blackboard.CurrentDestination`
- `StateDebugger` / `MemoryDebugger` / `CoverDebugger` — Gizmos for selected objects

## File Structure Highlights

```
Assets/Scripts/
├── Core/           # EnemyBrain, StateMachine, Blackboard, IAIState, EnemyState, EnemyRole
├── Vision/         # VisionSensor, FieldOfView, RaycastDetector, TargetDetector, VisionDebugger
├── Memory/         # EnemyMemory, LastSeenData, TargetMemory, MemoryTimer
├── Patrol/         # PatrolState, WaypointPatrol, CoveragePatrol, RandomPatrol, IdleLookAround
├── Chase/          # ChaseState, ChaseMovement, TargetPrediction, PathFollowing
├── Search/         # SearchState, SearchPattern, Investigation, ReturnPatrol
├── Combat/         # CombatState, AimController, ShootController, ReloadController, AttackDecision
├── Cover/          # CoverState, CoverScanner, CoverPoint, CoverDecision, PeekSystem, RetreatSystem
├── Dodge/          # DodgeState, BulletDetector, DodgeDecision, DodgeMovement, SafePositionFinder
├── Tactical/       # TacticalDecision, PrioritySystem, RiskEvaluation, Flanking, GroupAI
├── Animation/      # AnimationController, AimAnimation, MovementAnimation
├── Audio/          # FootstepAudio, AlertAudio, CombatAudio
├── Debug/          # StateDebugger, MemoryDebugger, CoverDebugger, GizmosDrawer, AIDebugDisplay, AIPathVisualizer
├── Utilities/      # MathUtility, PhysicsUtility, TimerUtility, ExtensionMethods, SteeringMovement, StuckDetector
├── Map/            # MapBounds, BoundsClamper
├── Player/         # PlayerController, PlayerShoot, Health, Bullet, PlayerMana, PlayerEquipment
└── UI/             # SettingsManager, SettingsPanelBinder, MainMenuController, PauseMenu, LevelSelectController, FpsCounterDisplay, GameDifficulty, AudioManager
```

## Common Development Tasks

### Adding a New AI State

1. Create `NewState.cs` in appropriate folder implementing `IAIState`
2. Add `EnemyState.NewState` to `EnemyState` enum
3. Register in `EnemyBrain.Awake()`: `_stateMachine.RegisterState(new NewState(...))`
4. Add transition logic in `TacticalDecision.DecideNextState()`
5. Add priority in `PrioritySystem.GetPriority()`

### Adding a New Enemy Role

1. Add entry to `EnemyRole` enum
2. Add case in `EnemyBrain.ApplyRolePreset()` with parameter overrides

### Modifying Tactical Decisions

Edit `TacticalDecision.DecideNextState()` — it's the single decision point with hysteresis guards for Cover/Combat/Chase transitions.

### Adding Settings Options

1. Add field + property in `SettingsManager`
2. Add `SetXxx()` method that saves to PlayerPrefs
3. Update `SaveSettings()` / `LoadSettings()` / `ApplyAll()`
4. Add UI field in `SettingsPanelBinder` and wire in `WireUpListeners()`

## Testing

- Unit tests: Use Unity Test Framework (`com.unity.test-framework` 1.6.0)
- Play Mode tests in `Assets/Tests/PlayMode/`
- Edit Mode tests in `Assets/Tests/EditMode/`
- Run via Test Runner window (Window > General > Test Runner)

## Important Notes

- **Thai comments** throughout codebase — variable/method names are English
- **No Pathfinding integration yet** — `PathFollowing.cs` is simple steering; consider NavMesh2D or A* Pathfinding Project for complex maps
- **No Object Pooling** — `ShootController`/`Bullet` uses `Instantiate`/`Destroy`; add pooling for high fire rates
- **Death Handling** — `Health.OnDeath` is empty UnityEvent; hook up disable EnemyBrain / play death anim / destroy in Inspector or code
- **Font Issue** — `AIDebugDisplay` uses IMGUI (OnGUI) with built-in font; Thai may render as boxes. Fix: create GUISkin with Thai font.