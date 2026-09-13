using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using TopDownTacticalAI.UI;
using TopDownTacticalAI.UI.Music;
using TopDownTacticalAI.UI.Themes;
using TopDownTacticalAI.UI.Animation;
using TopDownTacticalAI.UI.Components;

namespace TopDownTacticalAI.Editor
{
    /// <summary>
    /// Wizard สำหรับสร้างเมนู UI อัตโนมัติ - ใช้ Assets จาก Assets/หน้าเกมui/
    /// รองรับ: Main Menu, Settings Panel, Level Select, Pause Menu
    /// </summary>
    public class MenuSetupWizard : EditorWindow
    {
        private enum MenuType { MainMenu, SettingsPanel, LevelSelect, PauseMenu, All }

        [Header("Menu Selection")]
        private MenuType _menuToCreate = MenuType.All;

        [Header("Asset Paths (Assets/หน้าเกมui/)")]
        private string _assetBasePath = "Assets/หน้าเกมui";

        // Main Menu Assets
        private Sprite _mainMenuBackground;
        private Sprite _mainMenuPlayBtn;
        private Sprite _mainMenuSettingsBtn;
        private Sprite _mainMenuControlsBtn;
        private Sprite _mainMenuCreditsBtn;
        private Sprite _mainMenuExitBtn;
        private Sprite _mainMenuPanelBg;

        // Settings Assets
        private Sprite _settingsBgGeneral;
        private Sprite _settingsAudioBgLarge;
        private Sprite _settingsAudioBgSmall;
        private Sprite _settingsVideoBgLarge;
        private Sprite _settingsVideoBgSmall;
        private Sprite _settingsControlsBgLarge;
        private Sprite _settingsControlsBgSmall;
        private Sprite _settingsPanelBg;

        // Level Select Assets
        private Sprite _levelSelectBackground;
        private Sprite[] _levelPreviewImages = new Sprite[3];
        private Sprite _levelLockedImage;

        // Pause Menu Assets
        private Sprite _pauseResumeBtn;
        private Sprite _pauseSettingsBtn;
        private Sprite _pauseRestartBtn;
        private Sprite _pauseMainMenuBtn;
        private Sprite _pauseQuitBtn;
        private Sprite _pauseQuitYesBtn;
        private Sprite _pauseQuitNoBtn;
        private Sprite _pausePanelBg;

        // Theme
        private UITheme _theme;

        // Common
        private AudioClip _buttonClickSound;
        private AudioClip _panelOpenSound;
        private AudioClip _panelCloseSound;

        [MenuItem("Tools/TopDownTacticalAI/Menu Setup Wizard", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<MenuSetupWizard>("Menu Setup Wizard");
            window.minSize = new Vector2(500, 700);
            window.Show();
        }

        private void OnEnable()
        {
            LoadAssetsFromDisk();
            FindOrCreateTheme();
        }

        private void LoadAssetsFromDisk()
        {
            // Main Menu
            _mainMenuBackground = LoadAsset<Sprite>("หน้าหลัก/พื้นหลัง.png");
            _mainMenuPlayBtn = LoadAsset<Sprite>("หน้าหลัก/เล่นหน้าแรก.png");
            _mainMenuSettingsBtn = LoadAsset<Sprite>("หน้าหลัก/ตังค่าหน้าแรก.png");
            _mainMenuExitBtn = LoadAsset<Sprite>("หน้าหลัก/ออกหน้าแรก.png");

            // Settings
            _settingsBgGeneral = LoadAsset<Sprite>("หน้าตั้งค่าเกม/สีฟ้า ใช้ตั้งค่าต่างๆ.png");
            _settingsAudioBgLarge = LoadAsset<Sprite>("หน้าตั้งค่าเกม/สีม่วงเสียงพื้นหลังอันใหญ่.png");
            _settingsAudioBgSmall = LoadAsset<Sprite>("หน้าตั้งค่าเกม/สีม่วงเสียงพื้นหลังอันเล็ก.png");
            _settingsVideoBgLarge = LoadAsset<Sprite>("หน้าตั้งค่าเกม/สีฟ้ากราฟฟิก.png");
            _settingsVideoBgSmall = LoadAsset<Sprite>("หน้าตั้งค่าเกม/สีฟ้าตั้งค่าพื้นหลังอันเล็ก.png");
            _settingsControlsBgLarge = LoadAsset<Sprite>("หน้าตั้งค่าเกม/สีเหลืองควบคุมพื้นหลังอันใหญ่.png");
            _settingsControlsBgSmall = LoadAsset<Sprite>("หน้าตั้งค่าเกม/สีเหลืองควบคุมพื้นหลังอันเล็ก.png");

            // Pause Menu
            _pauseResumeBtn = LoadAsset<Sprite>("หน้าหยุดเกม/เล่นต่อไอคอน.png");
            _pauseSettingsBtn = LoadAsset<Sprite>("หน้าหยุดเกม/ฟื้นหลังว่างไอคอน.png");
            _pauseMainMenuBtn = LoadAsset<Sprite>("หน้าหยุดเกม/ย้อนกลับไอคอน.png");
            _pauseQuitBtn = LoadAsset<Sprite>("หน้าหยุดเกม/ออกไแคอน.png");
            _pausePanelBg = LoadAsset<Sprite>("หน้าหยุดเกม/ตัวเต็มไอคอนที่ต้องว่าง.png");

            // Level Select (use generic)
            _levelSelectBackground = LoadAsset<Sprite>("หน้าหลัก/พื้นหลัง.png");
        }

        private T LoadAsset<T>(string relativePath) where T : Object
        {
            string fullPath = $"{_assetBasePath}/{relativePath}";
            return AssetDatabase.LoadAssetAtPath<T>(fullPath);
        }

        private void FindOrCreateTheme()
        {
            var guids = AssetDatabase.FindAssets("t:UITheme");
            if (guids.Length > 0)
            {
                _theme = AssetDatabase.LoadAssetAtPath<UITheme>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Menu Setup Wizard", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Theme Section
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("UI Theme", EditorStyles.boldLabel);
            _theme = (UITheme)EditorGUILayout.ObjectField("Theme", _theme, typeof(UITheme), false);
            if (GUILayout.Button("Create New Theme"))
            {
                CreateNewTheme();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Menu Type Selection
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Select Menu to Create", EditorStyles.boldLabel);
            _menuToCreate = (MenuType)EditorGUILayout.EnumPopup("Menu Type", _menuToCreate);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Asset Preview Section
            DrawAssetPreviewSection();

            GUILayout.Space(10);

            // Create Buttons
            EditorGUILayout.BeginVertical("box");
            if (GUILayout.Button("Create Selected Menu(s)", GUILayout.Height(40)))
            {
                CreateMenus();
            }

            if (GUILayout.Button("Create All Menus", GUILayout.Height(30)))
            {
                _menuToCreate = MenuType.All;
                CreateMenus();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Scene Setup
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Scene Setup", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Scenes to Build Settings"))
            {
                AddScenesToBuildSettings();
            }
            if (GUILayout.Button("Open Main Menu Scene"))
            {
                OpenScene("Assets/Scenes/MainMenu.unity");
            }
            if (GUILayout.Button("Open Level Select Scene"))
            {
                OpenScene("Assets/Scenes/LevelSelect.unity");
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawAssetPreviewSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Loaded Assets Preview", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawAssetPreview("Main Menu BG", _mainMenuBackground);
            DrawAssetPreview("Play Btn", _mainMenuPlayBtn);
            DrawAssetPreview("Settings Btn", _mainMenuSettingsBtn);
            DrawAssetPreview("Exit Btn", _mainMenuExitBtn);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawAssetPreview("Settings BG", _settingsBgGeneral);
            DrawAssetPreview("Audio BG", _settingsAudioBgLarge);
            DrawAssetPreview("Video BG", _settingsVideoBgLarge);
            DrawAssetPreview("Controls BG", _settingsControlsBgLarge);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawAssetPreview("Pause Resume", _pauseResumeBtn);
            DrawAssetPreview("Pause Settings", _pauseSettingsBtn);
            DrawAssetPreview("Pause MainMenu", _pauseMainMenuBtn);
            DrawAssetPreview("Pause Quit", _pauseQuitBtn);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawAssetPreview(string label, Sprite sprite)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            if (sprite != null)
            {
                var tex = AssetPreview.GetAssetPreview(sprite);
                if (tex != null)
                    GUILayout.Label(tex, GUILayout.Width(64), GUILayout.Height(64));
                else
                    GUILayout.Label(sprite.name, GUILayout.Width(64), GUILayout.Height(64));
            }
            else
            {
                GUILayout.Box("Missing", GUILayout.Width(64), GUILayout.Height(64));
            }
            GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndVertical();
        }

        private void CreateMenus()
        {
            switch (_menuToCreate)
            {
                case MenuType.MainMenu:
                    CreateMainMenu();
                    break;
                case MenuType.SettingsPanel:
                    CreateSettingsPanel();
                    break;
                case MenuType.LevelSelect:
                    CreateLevelSelect();
                    break;
                case MenuType.PauseMenu:
                    CreatePauseMenu();
                    break;
                case MenuType.All:
                    CreateMainMenu();
                    CreateSettingsPanel();
                    CreateLevelSelect();
                    CreatePauseMenu();
                    break;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Menu creation complete!");
        }

        private void CreateMainMenu()
        {
            // Open MainMenu scene
            OpenScene("Assets/Scenes/MainMenu.unity");

            var canvas = FindOrCreateCanvas("MainMenuCanvas");
            if (canvas == null) return;

            // Create Theme if not exists
            if (_theme == null) CreateNewTheme();

            // Background
            CreateBackgroundImage(canvas.transform, _mainMenuBackground, "Background");

            // Main Menu Panel
            var mainPanel = CreatePanel(canvas.transform, "MainMenuPanel", new Vector2(400, 600));
            AddAnimatedPanel(mainPanel);
            var mainMenuController = mainPanel.AddComponent<MainMenuController>();
            mainMenuController.theme = _theme;
            mainMenuController.backgroundImage = _mainMenuBackground;
            mainMenuController.backgroundImageComponent = mainPanel.GetComponentInChildren<Image>();
            mainMenuController.mainMenuPanel = mainPanel.GetComponent<AnimatedPanel>();
            mainMenuController.levelSelectSceneName = "LevelSelect";
            mainMenuController.buttonClickSound = _buttonClickSound;
            mainMenuController.panelOpenSound = _panelOpenSound;
            mainMenuController.panelCloseSound = _panelCloseSound;

            // Create Buttons
            CreateMainMenuButtons(mainPanel.transform, mainMenuController);

            // Settings Panel
            var settingsPanel = CreatePanel(canvas.transform, "SettingsPanel", new Vector2(600, 700));
            settingsPanel.SetActive(false);
            AddAnimatedPanel(settingsPanel);
            var settingsController = settingsPanel.AddComponent<SettingsPanelController>();
            settingsController.theme = _theme;
            settingsController.settingsPanel = settingsPanel.GetComponent<AnimatedPanel>();
            settingsController.audioBackgroundLarge = _settingsAudioBgLarge;
            settingsController.audioBackgroundSmall = _settingsAudioBgSmall;
            settingsController.videoBackgroundLarge = _settingsVideoBgLarge;
            settingsController.videoBackgroundSmall = _settingsVideoBgSmall;
            settingsController.controlsBackgroundLarge = _settingsControlsBgLarge;
            settingsController.controlsBackgroundSmall = _settingsControlsBgSmall;
            settingsController.settingsBackgroundGeneral = _settingsBgGeneral;
            settingsController.tabSwitchSound = _buttonClickSound;

            mainMenuController.settingsPanel = settingsPanel.GetComponent<AnimatedPanel>();
            mainMenuController.settingsButton.onClick.AddListener(() => mainMenuController.ShowPanel(settingsPanel.GetComponent<AnimatedPanel>()));

            // Quit Confirm Panel
            var quitPanel = CreatePanel(canvas.transform, "QuitConfirmPanel", new Vector2(400, 200));
            quitPanel.SetActive(false);
            AddAnimatedPanel(quitPanel);
            CreateQuitConfirmButtons(quitPanel.transform, mainMenuController);
            mainMenuController.quitConfirmPanel = quitPanel.GetComponent<AnimatedPanel>();

            // Controls Panel
            var controlsPanel = CreatePanel(canvas.transform, "ControlsPanel", new Vector2(500, 600));
            controlsPanel.SetActive(false);
            AddAnimatedPanel(controlsPanel);
            CreateControlsContent(controlsPanel.transform);
            mainMenuController.controlsPanel = controlsPanel.GetComponent<AnimatedPanel>();

            // Credits Panel
            var creditsPanel = CreatePanel(canvas.transform, "CreditsPanel", new Vector2(500, 400));
            creditsPanel.SetActive(false);
            AddAnimatedPanel(creditsPanel);
            CreateCreditsContent(creditsPanel.transform);
            mainMenuController.creditsPanel = creditsPanel.GetComponent<AnimatedPanel>();

            // Add SettingsManager if not exists
            EnsureSettingsManager();

            // Add AudioManager if not exists
            EnsureAudioManager();

            // Add ScreenTransition
            EnsureScreenTransition();

            EditorUtility.SetDirty(canvas.gameObject);
            Debug.Log("Main Menu created successfully!");
        }

        private void CreateSettingsPanel()
        {
            // Settings panel is created as part of Main Menu
            Debug.Log("Settings Panel is created automatically with Main Menu");
        }

        private void CreateLevelSelect()
        {
            OpenScene("Assets/Scenes/LevelSelect.unity");

            var canvas = FindOrCreateCanvas("LevelSelectCanvas");
            if (canvas == null) return;

            if (_theme == null) CreateNewTheme();

            // Background
            CreateBackgroundImage(canvas.transform, _levelSelectBackground, "Background");

            // Level Select Panel
            var mainPanel = CreatePanel(canvas.transform, "LevelSelectPanel", new Vector2(1000, 700));
            AddAnimatedPanel(mainPanel);
            var levelSelectController = mainPanel.AddComponent<LevelSelectController>();
            levelSelectController.theme = _theme;
            levelSelectController.backgroundImage = _levelSelectBackground;
            levelSelectController.backgroundImageComponent = mainPanel.GetComponentInChildren<Image>();
            levelSelectController.levelSelectPanel = mainPanel.GetComponent<AnimatedPanel>();
            levelSelectController.mainMenuSceneName = "MainMenu";
            levelSelectController.buttonClickSound = _buttonClickSound;
            levelSelectController.levelSelectSound = _buttonClickSound;
            levelSelectController.difficultyChangeSound = _buttonClickSound;

            // Create Level Button Container with Grid Layout
            var container = new GameObject("LevelButtonContainer");
            container.transform.SetParent(mainPanel.transform);
            var gridLayout = container.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(300, 400);
            gridLayout.spacing = new Vector2(20, 20);
            gridLayout.padding = new RectOffset(20, 20, 20, 20);
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            container.AddComponent<RectTransform>().anchorMin = Vector2.zero;
            container.GetComponent<RectTransform>().anchorMax = Vector2.one;
            container.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            levelSelectController.levelButtonContainer = container.transform;

            // Create Level Button Prefab
            var levelButtonPrefab = CreateLevelButtonPrefab();
            // แก้ CS0029: CreateLevelButtonPrefab() คืนค่าเป็น GameObject แต่ levelButtonPrefab (field)
            // เป็นชนิด LevelButton — ต้องดึง Component ออกมาก่อน เอา GameObject มาใส่ตรงๆ ไม่ได้
            levelSelectController.levelButtonPrefab = levelButtonPrefab.GetComponent<LevelSelectFlatButton>();

            // Setup Level Data
            levelSelectController.levels = new LevelSelectController.LevelData[]
            {
                new LevelSelectController.LevelData
                {
                    levelName = "Stage 01",
                    sceneName = "Level1",
                    previewImage = _levelPreviewImages[0] ?? _levelSelectBackground,
                    isUnlocked = true,
                    description = "STAGE 1 MAP"
                },
                new LevelSelectController.LevelData
                {
                    levelName = "Stage 02",
                    sceneName = "Level2",
                    previewImage = _levelPreviewImages[1] ?? _levelSelectBackground,
                    isUnlocked = false,
                    description = "STAGE 2 MAP"
                },
                new LevelSelectController.LevelData
                {
                    levelName = "Stage 03",
                    sceneName = "Level3",
                    previewImage = _levelPreviewImages[2] ?? _levelSelectBackground,
                    isUnlocked = false,
                    description = "STAGE 3 MAP"
                }
            };

            // Difficulty Buttons
            CreateDifficultyButtons(mainPanel.transform, levelSelectController);

            // Back Button
            var backBtn = CreateButton(mainPanel.transform, "BackButton", "ย้อนกลับ", new Vector2(200, 60));
            backBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -300);
            levelSelectController.backButton = backBtn.GetComponent<Button>();
            if (_theme != null) _theme.ApplyToButton(backBtn.GetComponent<Button>());

            // Add SettingsManager and AudioManager
            EnsureSettingsManager();
            EnsureAudioManager();
            EnsureScreenTransition();

            EditorUtility.SetDirty(canvas.gameObject);
            Debug.Log("Level Select created successfully!");
        }

        private GameObject CreateLevelButtonPrefab()
        {
            var prefab = new GameObject("LevelButtonPrefab");
            var btn = prefab.AddComponent<Button>();
            var image = prefab.AddComponent<Image>();
            image.color = new Color(0.2f, 0.25f, 0.35f, 1f);
            image.type = Image.Type.Sliced;

            var rt = prefab.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 400);

            // Preview Image
            var previewGO = new GameObject("PreviewImage");
            previewGO.transform.SetParent(prefab.transform);
            var previewImg = previewGO.AddComponent<Image>();
            previewImg.color = Color.white;
            previewImg.preserveAspect = true;
            var previewRt = previewGO.GetComponent<RectTransform>();
            previewRt.anchorMin = new Vector2(0, 0.5f);
            previewRt.anchorMax = Vector2.one;
            previewRt.offsetMin = new Vector2(10, 10);
            previewRt.offsetMax = new Vector2(-10, -10);

            // Level Name
            var nameGO = new GameObject("LevelName");
            nameGO.transform.SetParent(prefab.transform);
            var nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.text = "Level Name";
            nameText.fontSize = 28;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;
            var nameRt = nameGO.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(1, 0.3f);
            nameRt.offsetMin = new Vector2(10, 10);
            nameRt.offsetMax = new Vector2(-10, -10);

            // Description
            var descGO = new GameObject("Description");
            descGO.transform.SetParent(prefab.transform);
            var descText = descGO.AddComponent<TextMeshProUGUI>();
            descText.text = "Description";
            descText.fontSize = 20;
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = new Color(0.8f, 0.8f, 0.8f);
            var descRt = descGO.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0, 0.3f);
            descRt.anchorMax = new Vector2(1, 0.5f);
            descRt.offsetMin = new Vector2(10, 10);
            descRt.offsetMax = new Vector2(-10, -10);

            // Cleared Badge (shown in the top-right corner when the stage is cleared)
            var clearedBadgeGO = new GameObject("ClearedBadge");
            clearedBadgeGO.transform.SetParent(prefab.transform);
            var clearedBadgeImg = clearedBadgeGO.AddComponent<Image>();
            clearedBadgeImg.color = new Color(0.2f, 0.85f, 0.4f, 1f); // green check tint
            clearedBadgeImg.raycastTarget = false;
            clearedBadgeGO.SetActive(false);
            var clearedBadgeRt = clearedBadgeGO.GetComponent<RectTransform>();
            clearedBadgeRt.anchorMin = new Vector2(1, 1);
            clearedBadgeRt.anchorMax = new Vector2(1, 1);
            clearedBadgeRt.pivot = new Vector2(1, 1);
            clearedBadgeRt.sizeDelta = new Vector2(36, 36);
            clearedBadgeRt.anchoredPosition = new Vector2(-6, -6);

            // Locked Overlay
            var lockedGO = new GameObject("LockedOverlay");
            lockedGO.transform.SetParent(prefab.transform);
            var lockedImg = lockedGO.AddComponent<Image>();
            lockedImg.color = new Color(0, 0, 0, 0.7f);
            lockedImg.raycastTarget = true;
            lockedGO.SetActive(false);
            var lockedRt = lockedGO.GetComponent<RectTransform>();
            lockedRt.anchorMin = Vector2.zero;
            lockedRt.anchorMax = Vector2.one;
            lockedRt.sizeDelta = Vector2.zero;

            var lockIconGO = new GameObject("LockIcon");
            lockIconGO.transform.SetParent(lockedGO.transform);
            var lockIcon = lockIconGO.AddComponent<Image>();
            lockIcon.color = Color.white;
            lockIcon.preserveAspect = true;
            var lockRt = lockIconGO.GetComponent<RectTransform>();
            lockRt.sizeDelta = new Vector2(64, 64);
            lockRt.anchoredPosition = Vector2.zero;

            // LevelSelectFlatButton Component
            // Use LevelSelectFlatButton (the old LevelButton class was removed from LevelSelectController).
            var levelBtn = prefab.AddComponent<LevelSelectFlatButton>();
            levelBtn.previewImage = previewImg;
            levelBtn.backgroundImage = image;
            levelBtn.levelNameText = nameText;
            levelBtn.descriptionText = descText;
            levelBtn.clearedBadge = clearedBadgeGO;
            levelBtn.clearedBadgeImage = clearedBadgeImg;
            levelBtn.button = btn;
            levelBtn.lockedOverlay = lockedGO;
            levelBtn.lockedIcon = lockIcon;

            if (_theme != null) _theme.ApplyToButton(btn);

            // Save as Prefab
            string prefabPath = "Assets/Prefabs/UI/LevelButtonPrefab.prefab";
            EnsureFolderExists("Assets/Prefabs/UI");
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            DestroyImmediate(prefab);
            return savedPrefab;
        }

        private void CreateDifficultyButtons(Transform parent, LevelSelectController controller)
        {
            var container = new GameObject("DifficultyButtons");
            container.transform.SetParent(parent);
            var layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            var containerRt = container.GetComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0.5f, 1);
            containerRt.anchorMax = new Vector2(0.5f, 1);
            containerRt.anchoredPosition = new Vector2(0, -50);
            containerRt.sizeDelta = new Vector2(600, 60);

            controller.difficultyEasyButton = CreateButton(container.transform, "EasyButton", "EASY", new Vector2(150, 50)).GetComponent<Button>();
            controller.difficultyNormalButton = CreateButton(container.transform, "NormalButton", "NORMAL", new Vector2(150, 50)).GetComponent<Button>();
            controller.difficultyHardButton = CreateButton(container.transform, "HardButton", "HARD", new Vector2(150, 50)).GetComponent<Button>();

            if (_theme != null)
            {
                _theme.ApplyToButton(controller.difficultyEasyButton);
                _theme.ApplyToButton(controller.difficultyNormalButton);
                _theme.ApplyToButton(controller.difficultyHardButton);
            }

            // Difficulty Label
            var labelGO = new GameObject("DifficultyLabel");
            labelGO.transform.SetParent(parent);
            var labelText = labelGO.AddComponent<TextMeshProUGUI>();
            labelText.text = "DIFFICULTY:";
            labelText.fontSize = 28;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            var labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.5f, 1);
            labelRt.anchorMax = new Vector2(0.5f, 1);
            labelRt.anchoredPosition = new Vector2(0, -120);
            controller.difficultyLabel = labelText;
        }

        private void CreatePauseMenu()
        {
            OpenScene("Assets/Scenes/Level1.unity");

            var canvas = FindOrCreateCanvas("PauseMenuCanvas");
            if (canvas == null) return;

            canvas.GetComponent<Canvas>().sortingOrder = 100; // Higher than game UI

            if (_theme == null) CreateNewTheme();

            // Pause Panel
            var pausePanel = CreatePanel(canvas.transform, "PausePanel", new Vector2(500, 600));
            pausePanel.SetActive(false);
            AddAnimatedPanel(pausePanel);
            pausePanel.GetComponent<AnimatedPanel>()._slideDirection = AnimatedPanel.SlideDirection.FromTop;

            var pauseMenu = canvas.gameObject.AddComponent<PauseMenu>();
            pauseMenu.theme = _theme;
            pauseMenu.pausePanel = pausePanel.GetComponent<AnimatedPanel>();
            pauseMenu.mainMenuSceneName = "MainMenu";
            pauseMenu.buttonClickSound = _buttonClickSound;
            pauseMenu.panelOpenSound = _panelOpenSound;
            pauseMenu.panelCloseSound = _panelCloseSound;
            pauseMenu.pauseSound = _panelOpenSound;
            pauseMenu.resumeSound = _panelCloseSound;

            // Pause Buttons
            CreatePauseButtons(pausePanel.transform, pauseMenu);

            // Settings Panel (reuse from MainMenu or create new)
            var settingsPanel = CreatePanel(canvas.transform, "PauseSettingsPanel", new Vector2(600, 700));
            settingsPanel.SetActive(false);
            AddAnimatedPanel(settingsPanel);
            var settingsController = settingsPanel.AddComponent<SettingsPanelController>();
            settingsController.theme = _theme;
            settingsController.settingsPanel = settingsPanel.GetComponent<AnimatedPanel>();
            settingsController.tabSwitchSound = _buttonClickSound;

            pauseMenu.settingsPanel = settingsPanel.GetComponent<AnimatedPanel>();
            pauseMenu.settingsButton.onClick.AddListener(() => pauseMenu.OpenSettingsFromPause());

            // Quit Confirm Panel
            var quitPanel = CreatePanel(canvas.transform, "PauseQuitConfirmPanel", new Vector2(400, 200));
            quitPanel.SetActive(false);
            AddAnimatedPanel(quitPanel);
            CreateQuitConfirmButtons(quitPanel.transform, pauseMenu);
            pauseMenu.quitConfirmPanel = quitPanel.GetComponent<AnimatedPanel>();

            // Add PauseMenuBlur to Camera
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var blur = mainCamera.gameObject.GetComponent<PauseMenuBlur>();
                if (blur == null) blur = mainCamera.gameObject.AddComponent<PauseMenuBlur>();
                pauseMenu.pauseBlur = blur;
            }

            EnsureScreenTransition();

            EditorUtility.SetDirty(canvas.gameObject);
            Debug.Log("Pause Menu created successfully!");
        }

        private void CreateMainMenuButtons(Transform parent, MainMenuController controller)
        {
            var layoutGroup = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 15;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.padding = new RectOffset(20, 20, 50, 50);

            controller.playButton = CreateButton(parent, "PlayButton", "เริ่มเล่น", new Vector2(350, 80)).GetComponent<Button>();
            controller.settingsButton = CreateButton(parent, "SettingsButton", "ตั้งค่า", new Vector2(350, 80)).GetComponent<Button>();
            controller.controlsButton = CreateButton(parent, "ControlsButton", "ควบคุม", new Vector2(350, 80)).GetComponent<Button>();
            controller.creditsButton = CreateButton(parent, "CreditsButton", "เครดิต", new Vector2(350, 80)).GetComponent<Button>();
            controller.quitButton = CreateButton(parent, "QuitButton", "ออกจากเกม", new Vector2(350, 80)).GetComponent<Button>();

            if (_theme != null)
            {
                _theme.ApplyToButton(controller.playButton);
                _theme.ApplyToButton(controller.settingsButton);
                _theme.ApplyToButton(controller.controlsButton);
                _theme.ApplyToButton(controller.creditsButton);
                _theme.ApplyToButton(controller.quitButton);
            }
        }

        private void CreatePauseButtons(Transform parent, PauseMenu pauseMenu)
        {
            var layoutGroup = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 15;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.padding = new RectOffset(20, 20, 50, 50);

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(parent);
            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "หยุดชั่วคราว";
            titleText.fontSize = 48;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            if (_theme != null && _theme.headerFont != null) titleText.font = _theme.headerFont;

            pauseMenu.resumeButton = CreateButton(parent, "ResumeButton", "เล่นต่อ", new Vector2(350, 80)).GetComponent<Button>();
            pauseMenu.settingsButton = CreateButton(parent, "SettingsButton", "ตั้งค่า", new Vector2(350, 80)).GetComponent<Button>();
            pauseMenu.restartButton = CreateButton(parent, "RestartButton", "เล่นใหม่", new Vector2(350, 80)).GetComponent<Button>();
            pauseMenu.mainMenuButton = CreateButton(parent, "MainMenuButton", "กลับเมนูหลัก", new Vector2(350, 80)).GetComponent<Button>();
            pauseMenu.quitButton = CreateButton(parent, "QuitButton", "ออกจากเกม", new Vector2(350, 80)).GetComponent<Button>();

            if (_theme != null)
            {
                _theme.ApplyToButton(pauseMenu.resumeButton);
                _theme.ApplyToButton(pauseMenu.settingsButton);
                _theme.ApplyToButton(pauseMenu.restartButton);
                _theme.ApplyToButton(pauseMenu.mainMenuButton);
                _theme.ApplyToButton(pauseMenu.quitButton);
            }
        }

        private void CreateQuitConfirmButtons(Transform parent, MainMenuController controller)
        {
            var textGO = new GameObject("ConfirmText");
            textGO.transform.SetParent(parent);
            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = "ต้องการออกจากเกมใช่ไหม?";
            text.fontSize = 32;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0, 0.5f);
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(20, 20);
            textRt.offsetMax = new Vector2(-20, -20);

            var btnContainer = new GameObject("ButtonContainer");
            btnContainer.transform.SetParent(parent);
            var hLayout = btnContainer.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 20;
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            var btnRt = btnContainer.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0, 0);
            btnRt.anchorMax = new Vector2(1, 0.5f);
            btnRt.offsetMin = new Vector2(20, 20);
            btnRt.offsetMax = new Vector2(-20, -20);

            controller.quitConfirmYesButton = CreateButton(btnContainer.transform, "YesButton", "ใช่", new Vector2(150, 60)).GetComponent<Button>();
            controller.quitConfirmNoButton = CreateButton(btnContainer.transform, "NoButton", "ไม่", new Vector2(150, 60)).GetComponent<Button>();

            if (_theme != null)
            {
                _theme.ApplyToButton(controller.quitConfirmYesButton);
                _theme.ApplyToButton(controller.quitConfirmNoButton);
            }
        }

        private void CreateQuitConfirmButtons(Transform parent, PauseMenu pauseMenu)
        {
            var textGO = new GameObject("ConfirmText");
            textGO.transform.SetParent(parent);
            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = "ต้องการออกจากเกมใช่ไหม?";
            text.fontSize = 32;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0, 0.5f);
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(20, 20);
            textRt.offsetMax = new Vector2(-20, -20);

            var btnContainer = new GameObject("ButtonContainer");
            btnContainer.transform.SetParent(parent);
            var hLayout = btnContainer.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 20;
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            var btnRt = btnContainer.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0, 0);
            btnRt.anchorMax = new Vector2(1, 0.5f);
            btnRt.offsetMin = new Vector2(20, 20);
            btnRt.offsetMax = new Vector2(-20, -20);

            pauseMenu.quitConfirmYesButton = CreateButton(btnContainer.transform, "YesButton", "ใช่", new Vector2(150, 60)).GetComponent<Button>();
            pauseMenu.quitConfirmNoButton = CreateButton(btnContainer.transform, "NoButton", "ไม่", new Vector2(150, 60)).GetComponent<Button>();

            if (_theme != null)
            {
                _theme.ApplyToButton(pauseMenu.quitConfirmYesButton);
                _theme.ApplyToButton(pauseMenu.quitConfirmNoButton);
            }
        }

        private void CreateControlsContent(Transform parent)
        {
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(parent);
            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "ควบคุม";
            titleText.fontSize = 40;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            if (_theme != null && _theme.headerFont != null) titleText.font = _theme.headerFont;
            var titleRt = titleGO.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = Vector2.one;
            titleRt.anchoredPosition = new Vector2(0, -40);

            // Controls List
            var controls = new (string, string)[]
            {
                ("WASD / Arrow Keys", "เคลื่อนที่"),
                ("Shift", "วิ่ง"),
                ("Space", "Dash / กระโดด"),
                ("Mouse", "มอง / เล็ง"),
                ("Left Click", "ยิง"),
                ("R", "Reload"),
                ("Esc", "Pause Menu"),
                ("Tab", "ดูคะแนน/สถิติ")
            };

            var container = new GameObject("ControlsList");
            container.transform.SetParent(parent);
            var vLayout = container.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 10;
            vLayout.padding = new RectOffset(40, 40, 20, 20);
            var containerRt = container.GetComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.offsetMin = new Vector2(20, 80);
            containerRt.offsetMax = new Vector2(-20, -80);

            foreach (var (key, desc) in controls)
            {
                var row = new GameObject($"Control_{key}");
                row.transform.SetParent(container.transform);
                var hLayout = row.AddComponent<HorizontalLayoutGroup>();
                hLayout.spacing = 20;
                hLayout.childAlignment = TextAnchor.MiddleLeft;

                var keyGO = new GameObject("Key");
                keyGO.transform.SetParent(row.transform);
                var keyText = keyGO.AddComponent<TextMeshProUGUI>();
                keyText.text = key;
                keyText.fontSize = 24;
                keyText.alignment = TextAlignmentOptions.Left;
                keyText.color = _theme != null ? _theme.accentColor : Color.yellow;
                var keyRt = keyGO.GetComponent<RectTransform>();
                keyRt.sizeDelta = new Vector2(180, 40);

                var descGO = new GameObject("Desc");
                descGO.transform.SetParent(row.transform);
                var descText = descGO.AddComponent<TextMeshProUGUI>();
                descText.text = desc;
                descText.fontSize = 24;
                descText.alignment = TextAlignmentOptions.Left;
                descText.color = Color.white;
            }

            // Back Button
            var backBtn = CreateButton(parent, "BackButton", "ย้อนกลับ", new Vector2(200, 60));
            backBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -300);
        }

        private void CreateCreditsContent(Transform parent)
        {
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(parent);
            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "เครดิต";
            titleText.fontSize = 40;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            if (_theme != null && _theme.headerFont != null) titleText.font = _theme.headerFont;
            var titleRt = titleGO.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = Vector2.one;
            titleRt.anchoredPosition = new Vector2(0, -40);

            var credits = new string[]
            {
                "TopDown Tactical AI Framework",
                "",
                "Game Design & Programming",
                "AI State Machine & Behavior Tree",
                "UI System & Theme Framework",
                "",
                "Assets:",
                "Sprites from Assets/หน้าเกมui/",
                "TextMeshPro for Text Rendering",
                "Unity URP for Rendering",
                "",
                "Special Thanks:",
                "Unity Community",
                "Open Source Contributors"
            };

            var container = new GameObject("CreditsList");
            container.transform.SetParent(parent);
            var vLayout = container.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 5;
            vLayout.padding = new RectOffset(40, 40, 20, 20);
            vLayout.childAlignment = TextAnchor.UpperCenter;
            var containerRt = container.GetComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.offsetMin = new Vector2(20, 80);
            containerRt.offsetMax = new Vector2(-20, -80);

            foreach (var line in credits)
            {
                var lineGO = new GameObject("Line");
                lineGO.transform.SetParent(container.transform);
                var lineText = lineGO.AddComponent<TextMeshProUGUI>();
                lineText.text = line;
                lineText.fontSize = line == "" ? 10 : (line.StartsWith("TopDown") || line.StartsWith("Game") || line.StartsWith("Assets") || line.StartsWith("Special") ? 28 : 22);
                lineText.alignment = TextAlignmentOptions.Center;
                lineText.color = line.StartsWith("TopDown") ? (_theme != null ? _theme.accentColor : Color.yellow) : Color.white;
                if (_theme != null && _theme.headerFont != null && (line.StartsWith("TopDown") || line.StartsWith("Game") || line.StartsWith("Assets") || line.StartsWith("Special")))
                    lineText.font = _theme.headerFont;
            }

            // Back Button
            var backBtn = CreateButton(parent, "BackButton", "ย้อนกลับ", new Vector2(200, 60));
            backBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -300);
        }

        // Helper Methods
        private Canvas FindOrCreateCanvas(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing.GetComponent<Canvas>();

            var canvasGO = new GameObject(name);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            canvasGO.AddComponent<GraphicRaycaster>();
            // แก้ล่วงหน้า: เดิมใช้ UnityEngine.InputSystem.UI.InputSystemUIInputModule ซึ่งต้องมี Package
            // "Input System" (ตัวใหม่) ติดตั้งไว้ในโปรเจกต์ถึงจะคอมไพล์ผ่าน — จากที่เจอ Warning ก่อนหน้านี้ว่า
            // "This project uses Input Manager" (ระบบเดิม) แปลว่าโปรเจกต์นี้ยังไม่ได้ติดตั้ง Package ตัวใหม่
            // เปลี่ยนมาใช้ StandaloneInputModule แทน ซึ่งทำงานได้กับ Input Manager แบบเดิมโดยไม่ต้องติดตั้งอะไรเพิ่ม
            canvasGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Canvas Scaler
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Event System
            if (GameObject.Find("EventSystem") == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                // เหตุผลเดียวกับด้านบน: ใช้ StandaloneInputModule แทน InputSystemUIInputModule
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvas;
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.15f, 0.18f, 0.25f, 0.98f);
            image.type = Image.Type.Sliced;

            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            return panel;
        }

        private void AddAnimatedPanel(GameObject panel)
        {
            var animated = panel.AddComponent<AnimatedPanel>();
            animated._slideDirection = AnimatedPanel.SlideDirection.FromRight;
            animated._animateOnEnable = true;
        }

        private void CreateBackgroundImage(Transform parent, Sprite sprite, string name)
        {
            var bg = new GameObject(name);
            bg.transform.SetParent(parent);
            bg.transform.SetAsFirstSibling();
            var image = bg.AddComponent<Image>();

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }
            else
            {
                image.color = new Color(0.1f, 0.12f, 0.18f, 1f);
            }

            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private GameObject CreateButton(Transform parent, string name, string text, Vector2 size)
        {
            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent);
            var btn = btnGO.AddComponent<Button>();
            var image = btnGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.3f, 0.5f, 1f);
            image.type = Image.Type.Sliced;

            var rt = btnGO.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 32;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 20;
            tmp.fontSizeMax = 40;
            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            return btnGO;
        }

        private void EnsureSettingsManager()
        {
            // FindObjectOfType เลิกใช้แล้ว เปลี่ยนเป็น FindFirstObjectByType (แก้ Warning CS0618)
            if (FindFirstObjectByType<SettingsManager>() == null)
            {
                var go = new GameObject("SettingsManager");
                go.AddComponent<SettingsManager>();
                Debug.Log("SettingsManager created");
            }
        }

        private void EnsureAudioManager()
        {
            // FindObjectOfType เลิกใช้แล้ว เปลี่ยนเป็น FindFirstObjectByType (แก้ Warning CS0618)
            if (FindFirstObjectByType<AudioManager>() == null)
            {
                var go = new GameObject("AudioManager");
                var am = go.AddComponent<AudioManager>();
                // Create one music source + one SFX source (single-source playback, no crossfade)
                var musicSource = go.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                var sfxSource = go.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                am.musicSource = musicSource;
                am.sfxSource = sfxSource;
                // Try to auto-assign the playlist if it exists
                var playlist = AssetDatabase.LoadAssetAtPath<MusicPlaylistSO>("Assets/Music/MusicPlaylist.asset");
                if (playlist != null)
                {
                    am.musicPlaylist = playlist;
                    Debug.Log("AudioManager created with playlist assigned");
                }
                else
                {
                    Debug.Log("AudioManager created with 2 music sources + 1 sfx source (no playlist found at Assets/Music/MusicPlaylist.asset)");
                }
            }
        }

        private void EnsureScreenTransition()
        {
            if (ScreenTransition.Instance == null)
            {
                var go = new GameObject("ScreenTransition");
                go.AddComponent<ScreenTransition>();
                Debug.Log("ScreenTransition created");
            }
        }

        private void CreateNewTheme()
        {
            var theme = ScriptableObject.CreateInstance<UITheme>();
            EnsureFolderExists("Assets/Resources/Themes");
            AssetDatabase.CreateAsset(theme, "Assets/Resources/Themes/DefaultUITheme.asset");
            _theme = theme;
            Debug.Log("New UITheme created at Assets/Resources/Themes/DefaultUITheme.asset");
        }

        private void AddScenesToBuildSettings()
        {
            var scenes = new[]
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/LevelSelect.unity",
                "Assets/Scenes/Level1.unity"
            };

            // แก้ CS0234: ลบบรรทัด "new UnityEditor.BuildSettings()" ทิ้ง — คลาสนี้ไม่มีอยู่จริงใน UnityEditor
            // (พิมพ์ผิด/สับสนกับ EditorBuildSettings) แถมตัวแปรนี้ไม่เคยถูกใช้ที่ไหนต่อเลยด้วย ลบทิ้งได้เลยไม่กระทบอะไร
            var sceneList = new System.Collections.Generic.List<EditorBuildSettingsScene>();

            foreach (var scene in scenes)
            {
                if (System.IO.File.Exists(scene))
                {
                    sceneList.Add(new EditorBuildSettingsScene(scene, true));
                }
            }

            EditorBuildSettings.scenes = sceneList.ToArray();
            Debug.Log("Scenes added to Build Settings");
        }

        private void OpenScene(string path)
        {
            if (System.IO.File.Exists(path))
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
            }
            else
            {
                Debug.LogWarning($"Scene not found: {path}");
            }
        }

        private void EnsureFolderExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                var currentPath = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var newPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = newPath;
                }
            }
        }
    }
}
