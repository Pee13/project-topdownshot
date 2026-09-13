# TopDown Tactical AI - UI System Documentation

## Overview

The new UI system is designed to be:
- **Reusable** - Works across every menu (Main Menu, Settings, Level Select, Pause Menu)
- **Theme-based** - A `UITheme` ScriptableObject controls colors, fonts, and shared styles
- **Animated** - Includes `AnimatedPanel` for Fade/Slide animations
- **Asset-driven** - Uses assets directly from `Assets/UI/`

## Folder Structure

```
Assets/Scripts/UI/
├── Themes/
│   └── UITheme.cs              # ScriptableObject for Theme
├── Components/
│   └── UIButtonScaler.cs       # Hover/Press scale animation
├── Animation/
│   ├── AnimatedPanel.cs        # Panel with Fade/Slide
│   ├── ScreenTransition.cs     # Scene transition (Fade)
│   └── PauseMenuBlur.cs        # Blur effect for Pause
├── MainMenuController.cs       # New Main Menu
├── SettingsPanelController.cs  # Tabbed Settings Panel
├── LevelSelectController.cs    # New Level Select
├── PauseMenu.cs                # New Pause Menu
├── GameDifficulty.cs           # Difficulty System (wired to AI)
├── SettingsManager.cs          # Settings Singleton (original)
├── SettingsPanelBinder.cs      # UI Binding (original)
└── AudioManager.cs             # Audio Singleton (original)
```

## Setup & Usage

### 1. Create a UITheme
```
Tools > TopDownTacticalAI > Create Default UI Theme
```
Or right-click in the Project: `Create > TopDownTacticalAI > UI Theme`

### 2. Use the Menu Setup Wizard
```
Tools > TopDownTacticalAI > Menu Setup Wizard
```
- Select the Menu Type you want to create
- Click "Create Selected Menu(s)" or "Create All Menus"

### 3. Add Scenes to Build Settings
Click the "Add Scenes to Build Settings" button in the Wizard.

## UITheme - Theme Configuration

`UITheme` is a ScriptableObject that stores:
- **Colors**: Primary, Secondary, Accent, Background, Panel, Text
- **Button States**: Normal, Highlighted, Pressed, Disabled
- **Fonts**: Main Font, Header Font, Font Sizes
- **Sprites**: 9-slice backgrounds for Button, Panel, Slider, Toggle, Dropdown
- **Animation**: Duration, Curves, Scale factors
- **Layout**: Padding, Spacing, Default sizes

### Applying a Theme to UI Elements
```csharp
// Button
theme.ApplyToButton(button);

// Panel
theme.ApplyToPanel(panelImage);

// Text
theme.ApplyToText(text, isHeader: false);

// Slider
theme.ApplyToSlider(slider);

// Toggle
theme.ApplyToToggle(toggle);

// Dropdown
theme.ApplyToDropdown(dropdown);
```

## AnimatedPanel - Panel Animation

```csharp
public class AnimatedPanel : MonoBehaviour
{
    public enum SlideDirection { None, FromLeft, FromRight, FromTop, FromBottom }
    
    // Show with animation
    panel.Show(duration: 0.3f, curve: myCurve, onComplete: () => {});
    
    // Hide with animation
    panel.Hide(duration: 0.2f, curve: myCurve, onComplete: () => {});
    
    // Instant show/hide
    panel.ShowInstant();
    panel.HideInstant();
}
```

## ScreenTransition - Scene Transition

```csharp
// Singleton, DontDestroyOnLoad
ScreenTransition.Instance.FadeIn(0.5f, onComplete);
ScreenTransition.Instance.FadeOut(0.5f, onComplete);
ScreenTransition.Instance.LoadSceneWithTransition("Level1", 0.5f, 0.5f);
```

## MainMenuController

### Features:
- Background image from assets
- Animated panels for each page (Main, Settings, Controls, Credits, Quit Confirm)
- Full theme support
- Sound effects for buttons and panels
- ScreenTransition integration

### Inspector Setup:
- `theme` - UITheme asset
- `backgroundImage` - Background sprite (from Assets/UI/main/background.png)
- `mainMenuPanel`, `settingsPanel`, `quitConfirmPanel`, `controlsPanel`, `creditsPanel` - AnimatedPanel for each
- Buttons: `playButton`, `settingsButton`, `controlsButton`, `creditsButton`, `quitButton`
- `quitConfirmYesButton`, `quitConfirmNoButton`
- Back buttons: `settingsBackButton`, `controlsBackButton`, `creditsBackButton`
- `levelSelectSceneName` - Level Select scene name
- Audio clips: `buttonClickSound`, `panelOpenSound`, `panelCloseSound`

## SettingsPanelController

### Features:
- **Tabbed Layout**: Audio, Video, Gameplay, Controls
- Uses `SettingsManager` + `SettingsPanelBinder` together
- Background changes per Tab (using purple/blue/yellow assets)
- Theme support
- Animated tab switching

### Tabs:
1. **Audio**: Master/Music/SFX Volume sliders
2. **Video**: Resolution Dropdown, Fullscreen Toggle, Quality Dropdown
3. **Gameplay**: Mouse Sensitivity, Screen Shake, Show FPS
4. **Controls**: Displays key bindings

## LevelSelectController

### Features:
- **Level Data Array**: Define multiple levels
- **Preview Images**: Preview image for each level
- **Difficulty Stars**: 1-3 stars per level
- **Locked/Unlocked**: Levels can be locked
- **Difficulty Selector**: Easy/Normal/Hard before entering a level
- **Star Display**: Shows selected difficulty
- Dynamic button creation from Prefab

### LevelData Structure:
```csharp
public class LevelData
{
    public string levelName;
    public string sceneName;
    public Sprite previewImage;
    public Sprite lockedImage;
    public int difficultyStars;  // 1-3
    public bool isUnlocked;
    public string description;
    public int recommendedLevel;
}
```

## PauseMenu

### Features:
- **Blur Background**: Uses URP Volume DepthOfField
- **Animated Panels**: Pause, Settings, Quit Confirm
- **ESC Toggle**: Press ESC to open/close (except when in Settings)
- **Resume Countdown**: Can be added
- Full theme support

### Setup:
- `pauseBlur` - PauseMenuBlur component on Camera
- `pausePanel`, `settingsPanel`, `quitConfirmPanel` - AnimatedPanel
- Buttons: `resumeButton`, `settingsButton`, `restartButton`, `mainMenuButton`, `quitButton`

## GameDifficulty - Wired to AI

### DifficultyMultipliers:
```csharp
public struct DifficultyMultipliers
{
    // Movement
    public float speedMultiplier;
    public float patrolSpeedMultiplier;
    public float chaseSpeedMultiplier;
    public float searchSpeedMultiplier;

    // Combat
    public float damageMultiplier;
    public float fireRateMultiplier;
    public float accuracyMultiplier;
    public float maxAmmoMultiplier;
    public float reloadSpeedMultiplier;

    // Vision
    public float viewRadiusMultiplier;
    public float viewAngleMultiplier;
    public float suspicionBuildTimeMultiplier;
    public float suspicionDecayTimeMultiplier;

    // Tactical
    public float dangerRangeMultiplier;
    public float coverSearchRadiusMultiplier;
    public float dodgeDetectRadiusMultiplier;
    public float dodgeSpeedMultiplier;

    // Health
    public float maxHPMultiplier;

    // Group AI
    public float alertShoutRadiusMultiplier;
    public float flankRadiusMultiplier;

    // Rewards
    public float scoreMultiplier;
    public float xpMultiplier;
}
```

### Using in EnemyBrain:
```csharp
// In Awake()
ApplyDifficultyMultipliers();

// Reading values
var mult = GameDifficulty.GetMultipliers();
PatrolSpeed *= mult.patrolSpeedMultiplier;
ViewRadius *= mult.viewRadiusMultiplier;
// ... etc
```

### Default Values:
| Setting | Easy | Normal | Hard |
|---------|------|--------|------|
| Speed | 0.8x | 1.0x | 1.2x |
| Damage | 0.6x | 1.0x | 1.4x |
| Fire Rate | 0.7x | 1.0x | 1.3x |
| View Radius | 0.8x | 1.0x | 1.3x |
| Max HP | 0.8x | 1.0x | 1.3x |
| Alert Radius | 0.8x | 1.0x | 1.5x |
| Score/XP | 0.8x | 1.0x | 1.5x/1.3x |

## Assets Used (Assets/UI/)

```
UI/
├── main/
│   ├── background.png
│   ├── play-main.png
│   ├── settings-main.png
│   └── quit-main.png
├── settings/
│   ├── purple-audio-bg-small.png
│   ├── purple-audio-bg-large.png
│   ├── blue-graphics.png
│   ├── blue-settings-bg-large.png
│   ├── blue-settings-bg-small.png
│   ├── yellow-controls-bg-small.png
│   ├── yellow-controls-bg-large.png
│   ├── blue-general-settings.png
│   └── yellow-controls.png
├── pause-menu/
│   ├── resume-icon.png
│   ├── empty-back-icon.png
│   ├── full-blank-icon.png
│   ├── back-icon.png
│   └── quit-icon.png
└── models/
    ├── normal-model.png
    ├── shield-model.png
    ├── spear-weapon.png
    └── fast-model.png
```

## Troubleshooting

### 1. Theme doesn't load
- Make sure the UITheme is in `Assets/Resources/Themes/`
- Or drag the Theme into the Inspector directly

### 2. Assets not found
- Verify the path in `Assets/UI/` matches
- Sprite must have Texture Type = Sprite (2D and UI)

### 3. Animation doesn't work
- Make sure the Panel has an `AnimatedPanel` component
- Verify `CanvasGroup` was added automatically

### 4. Blur doesn't work (Pause Menu)
- Requires URP (Universal Render Pipeline)
- Camera must have a Volume component
- DepthOfField override must be enabled in the Volume Profile

### 5. Settings don't save
- Make sure `SettingsManager` is in the MainMenu scene (DontDestroyOnLoad)
- `SettingsPanelBinder` must be on each Settings Panel

## Best Practices

1. **Use Prefabs** for Level Buttons, Settings Panel content
2. **One Theme** for the entire game - create a UITheme and reference it in every menu
3. **AudioManager** and **SettingsManager** are Singletons that persist across scenes
4. **ScreenTransition** should be used for every scene load
5. **AnimatedPanel** should be used with every Panel that needs Show/Hide

## Extending

### Adding a new Tab to Settings:
1. Add the enum in `SettingsPanelController.Tab`
2. Add a Button + Content Panel
3. Add a case in `ShowTab()`, `GetTabContent()`, `UpdateBackgroundForTab()`

### Adding a new Animation:
1. Create a new class that inherits from `AnimatedPanel` or create a new component
2. Use `Time.unscaledDeltaTime` so it works during Pause

### Adding a new Difficulty Setting:
1. Add a field in `DifficultyMultipliers`
2. Add the value in `GameDifficulty.GetMultipliers()` for every case
3. Apply it in EnemyBrain, Health, ShootController, AimController
