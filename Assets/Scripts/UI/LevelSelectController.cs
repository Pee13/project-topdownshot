using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.UI.Themes;
using TopDownTacticalAI.UI.Animation;
using TopDownTacticalAI.UI.Components;
using TopDownTacticalAI.UI.Music;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Level Select screen controller.
    /// Layout follows the SELECT STAGE mockup:
    ///   - Top bar: BACK | SELECT STAGE | Stage progress
    ///   - Stage cards in a horizontal row, locked stages show a lock icon
    ///   - Bottom bar: DIFFICULTY (Easy / Normal / Hard) | START MISSION button
    /// Reacts to UIThemeManager theme changes (Day / Night).
    /// Stage unlocking is handled by <see cref="LevelProgressManager"/> - a
    /// stage unlocks when the previous one has been cleared at least once.
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        [System.Serializable]
        public class LevelData
        {
            public string levelName = "Level 1";
            public string sceneName = "Level1";
            public Sprite previewImage;           // Preview image of the level
            public Sprite lockedImage;            // Image shown when locked
            public Sprite clearedImage;           // Image shown on the card when cleared
            public bool isUnlocked = true;        // Initial unlock state (stage 0 is always true)
            public string description = "";
        }

        [Header("── Theme ──")]
        public UITheme theme;
        public UIThemeManager themeManager;
        public LevelProgressManager progressManager;

        [Header("── Background ──")]
        public Sprite backgroundImage;
        public Image backgroundImageComponent;

        [Header("── Top Bar ──")]
        public TMP_Text titleText;
        public TMP_Text progressText;          // "2 / 4"
        public Button backButton;

        [Header("── Level Buttons ──")]
        [Tooltip("Prefab for level buttons (must have LevelSelectFlatButton component)")]
        public LevelSelectFlatButton levelButtonPrefab;
        [Tooltip("Parent container for level buttons (Grid Layout Group)")]
        public Transform levelButtonContainer;

        [Header("── Difficulty Selector ──")]
        public Button difficultyEasyButton;
        public Button difficultyNormalButton;
        public Button difficultyHardButton;
        public TMP_Text difficultyLabel;

        [Header("── Start Mission ──")]
        public Button startMissionButton;
        public TMP_Text startMissionLabel;

        [Header("── Level Data ──")]
        public LevelData[] levels;

        [Header("── Panel Animation ──")]
        public AnimatedPanel levelSelectPanel;

        [Header("── Audio ──")]
        public AudioClip buttonClickSound;
        public AudioClip levelSelectSound;
        public AudioClip difficultyChangeSound;

        [Header("── Music ──")]
        public MusicPlaylistSO musicPlaylist;

        [Header("── Scene Names ──")]
        public string mainMenuSceneName = "MainMenu";

        private AudioSource _audioSource;
        private NeonMenuVFX _neonVFX;
        private GameDifficulty.Level _selectedDifficulty = GameDifficulty.Level.Normal;
        private LevelData _selectedLevel;
        private int _selectedIndex = -1;
        private List<LevelSelectFlatButton> _spawnedButtons = new List<LevelSelectFlatButton>();

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            if (progressManager == null) progressManager = LevelProgressManager.Instance;

            // Drop any stale cleared-scene entries from previous builds so the
            // "X / Y CLEARED" counter matches the current level list.
            if (progressManager != null) progressManager.PruneUnknownStages(GetSceneNamesInOrder());

            SetupBackground();
            SetupTopBar();
            ApplyTheme();
            SetupDifficultyButtons();
            SetupStartMissionButton();
            SetupBackButton();
            CreateLevelButtons();
            RefreshDifficultyUI();
            RefreshStartMissionState();

            // Add NeonMenuVFX to the level select panel if not already present
            if (levelSelectPanel != null)
            {
                _neonVFX = levelSelectPanel.GetComponent<NeonMenuVFX>();
                if (_neonVFX == null) _neonVFX = levelSelectPanel.gameObject.AddComponent<NeonMenuVFX>();
            }
        }

        private void Start()
        {
            levelSelectPanel?.Show();
            PlaySound(buttonClickSound);

            // Start level select music (context-aware)
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayContext(MusicContext.LevelSelect);
            }

            // Neon VFX intentionally disabled on level select for readability.
            _neonVFX?.Stop();
        }

        private void OnEnable()
        {
            if (themeManager != null) themeManager.OnThemeChanged += OnThemeChanged;
            if (progressManager != null) progressManager.OnStageCleared += OnStageCleared;
        }

        private void OnDisable()
        {
            if (themeManager != null) themeManager.OnThemeChanged -= OnThemeChanged;
            if (progressManager != null) progressManager.OnStageCleared -= OnStageCleared;
        }

        private void OnThemeChanged(UIThemeVariant _)
        {
            ApplyTheme();
            RefreshDifficultyUI();
            RefreshStartMissionState();
        }

        private void OnStageCleared(string _)
        {
            RefreshAllCards();
            RefreshProgressText();
            RefreshStartMissionState();
        }

        private void SetupBackground()
        {
            if (backgroundImageComponent == null)
            {
                var bgGO = new GameObject("BackgroundImage");
                bgGO.transform.SetParent(transform);
                bgGO.transform.SetAsFirstSibling();
                backgroundImageComponent = bgGO.AddComponent<Image>();
                var rt = backgroundImageComponent.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
            }

            if (backgroundImage != null)
            {
                backgroundImageComponent.sprite = backgroundImage;
                backgroundImageComponent.type = Image.Type.Simple;
                backgroundImageComponent.preserveAspect = true;
            }
            else if (theme != null && theme.panelBackground != null)
            {
                backgroundImageComponent.sprite = theme.panelBackground;
                backgroundImageComponent.type = Image.Type.Sliced;
                backgroundImageComponent.color = theme.backgroundColor;
            }
            else if (theme != null)
            {
                backgroundImageComponent.color = theme.backgroundColor;
            }
        }

        private void SetupTopBar()
        {
            if (titleText != null) titleText.text = "SELECT STAGE";
            RefreshProgressText();
        }

        private void RefreshProgressText()
        {
            if (progressText == null) return;
            int cleared = progressManager != null
                ? progressManager.CountCleared(GetSceneNamesInOrder())
                : 0;
            int total = levels != null ? levels.Length : 0;
            progressText.text = $"{cleared} / {total} CLEARED";
        }

        private List<string> GetSceneNamesInOrder()
        {
            var list = new List<string>();
            if (levels == null) return list;
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] != null) list.Add(levels[i].sceneName);
            }
            return list;
        }

        private void ApplyTheme()
        {
            if (theme == null) return;

            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                theme.ApplyToButton(btn);
            }

            var texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in texts)
            {
                bool isHeader = text == titleText;
                theme.ApplyToText(text, isHeader);
            }
        }

        private void SetupDifficultyButtons()
        {
            AddListener(difficultyEasyButton, () => SetDifficulty(GameDifficulty.Level.Easy));
            AddListener(difficultyNormalButton, () => SetDifficulty(GameDifficulty.Level.Normal));
            AddListener(difficultyHardButton, () => SetDifficulty(GameDifficulty.Level.Hard));
        }

        private void SetupStartMissionButton()
        {
            if (startMissionButton == null) return;
            startMissionButton.onClick.AddListener(() =>
            {
                PlaySound(levelSelectSound);
                if (_selectedLevel == null)
                {
                    // Auto-pick the first unlocked level if user did not click one.
                    PickDefaultLevel();
                }
                if (_selectedLevel == null) return;

                // Save music position so Level1 can continue seamlessly
                AudioManager.Instance?.SaveMusicPosition();

                GameDifficulty.Current = _selectedDifficulty;
                levelSelectPanel?.Hide(0.2f, null, () =>
                {
                    ScreenTransition.Instance?.LoadSceneWithTransition(_selectedLevel.sceneName);
                });
            });
        }

        private void PickDefaultLevel()
        {
            if (levels == null) return;
            var sceneNames = GetSceneNamesInOrder();
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null) continue;
                bool unlocked = progressManager != null
                    ? progressManager.IsUnlockedByIndex(i, sceneNames)
                    : levels[i].isUnlocked;
                if (unlocked)
                {
                    _selectedLevel = levels[i];
                    _selectedIndex = i;
                    return;
                }
            }
        }

        private void SetupBackButton()
        {
            AddListener(backButton, OnBackButton);
        }

        private void AddListener(Button button, System.Action action)
        {
            if (button != null)
            {
                button.onClick.AddListener(() =>
                {
                    PlaySound(buttonClickSound);
                    action?.Invoke();
                });
            }
        }

        private void CreateLevelButtons()
        {
            if (levelButtonContainer == null || levelButtonPrefab == null || levels == null) return;

            foreach (Transform child in levelButtonContainer)
            {
                DestroyImmediate(child.gameObject);
            }
            _spawnedButtons.Clear();

            var sceneNames = GetSceneNamesInOrder();
            for (int i = 0; i < levels.Length; i++)
            {
                var levelData = levels[i];
                var btn = Instantiate(levelButtonPrefab, levelButtonContainer);
                bool isUnlocked = progressManager != null
                    ? progressManager.IsUnlockedByIndex(i, sceneNames)
                    : levelData.isUnlocked;
                bool isCleared = progressManager != null
                    && !string.IsNullOrEmpty(levelData.sceneName)
                    && progressManager.IsCleared(levelData.sceneName);
                btn.Setup(levelData, i, isUnlocked, isCleared, OnLevelSelected);
                _spawnedButtons.Add(btn);
            }
        }

        /// <summary>
        /// Updates the unlock/cleared visuals of every spawned card without
        /// rebuilding the whole list. Called after a stage is cleared.
        /// </summary>
        private void RefreshAllCards()
        {
            if (levels == null) return;
            var sceneNames = GetSceneNamesInOrder();
            for (int i = 0; i < _spawnedButtons.Count && i < levels.Length; i++)
            {
                var levelData = levels[i];
                bool isUnlocked = progressManager != null
                    ? progressManager.IsUnlockedByIndex(i, sceneNames)
                    : levelData.isUnlocked;
                bool isCleared = progressManager != null
                    && !string.IsNullOrEmpty(levelData.sceneName)
                    && progressManager.IsCleared(levelData.sceneName);
                _spawnedButtons[i].SetState(isUnlocked, isCleared, levelData);
            }
        }

        public void OnLevelSelected(LevelData levelData, int index)
        {
            if (levelData == null) return;

            var sceneNames = GetSceneNamesInOrder();
            bool isUnlocked = progressManager != null
                ? progressManager.IsUnlockedByIndex(index, sceneNames)
                : levelData.isUnlocked;
            if (!isUnlocked)
            {
                PlaySound(buttonClickSound);
                return;
            }

            _selectedLevel = levelData;
            _selectedIndex = index;
            RefreshStartMissionState();
            PlaySound(levelSelectSound);
        }

        public void SetDifficulty(GameDifficulty.Level difficulty)
        {
            _selectedDifficulty = difficulty;
            GameDifficulty.Current = difficulty;
            RefreshDifficultyUI();
            PlaySound(difficultyChangeSound);
        }

        private void RefreshDifficultyUI()
        {
            if (difficultyLabel != null)
            {
                difficultyLabel.text = $"DIFFICULTY:";
            }
            UpdateDifficultyButtonHighlights();
        }

        private void UpdateDifficultyButtonHighlights()
        {
            var buttons = new[] { difficultyEasyButton, difficultyNormalButton, difficultyHardButton };
            var diffs = new[] { GameDifficulty.Level.Easy, GameDifficulty.Level.Normal, GameDifficulty.Level.Hard };

            if (theme == null) return;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;

                var colors = buttons[i].colors;
                if (diffs[i] == _selectedDifficulty)
                {
                    colors.normalColor = theme.primaryColor;
                    colors.highlightedColor = theme.primaryColor;
                    colors.pressedColor = theme.buttonPressed;
                }
                else
                {
                    colors.normalColor = theme.buttonNormal;
                    colors.highlightedColor = theme.buttonHighlighted;
                }
                buttons[i].colors = colors;
            }
        }

        private void RefreshStartMissionState()
        {
            if (startMissionButton == null) return;

            if (_selectedLevel == null) PickDefaultLevel();

            bool canStart = _selectedLevel != null;
            if (canStart && progressManager != null)
            {
                var sceneNames = GetSceneNamesInOrder();
                int idx = _selectedIndex >= 0 ? _selectedIndex : 0;
                canStart = progressManager.IsUnlockedByIndex(idx, sceneNames);
            }

            startMissionButton.interactable = canStart;
            if (startMissionLabel != null)
            {
                startMissionLabel.text = canStart ? "START MISSION" : "SELECT A STAGE";
            }
        }

        public void OnBackButton()
        {
            PlaySound(buttonClickSound);
            // Save music position so MainMenu can continue seamlessly
            AudioManager.Instance?.SaveMusicPosition();
            levelSelectPanel?.Hide(0.2f, null, () =>
            {
                SceneManager.LoadScene(mainMenuSceneName);
            });
        }

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}
