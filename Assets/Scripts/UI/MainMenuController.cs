using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using TopDownTacticalAI.UI.Themes;
using TopDownTacticalAI.UI.Animation;
using TopDownTacticalAI.UI.Components;
using TopDownTacticalAI.UI.Music;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Main Menu screen controller - uses the new UI System.
    /// Supports: Theme, Animation, Background Image, Sound
    ///
    /// Setup Instructions:
    /// 1. Create a UITheme: Create > TopDownTacticalAI > UI Theme
    /// 2. Attach this script to the Canvas in the MainMenu scene
    /// 3. Drag assets from Assets/UI/main/ into the Inspector
    /// 4. Add an AnimatedPanel component to each panel
    /// 5. (Optional) Drag a MusicPlaylistSO into the inspector if you want
    ///    per-track control; otherwise AudioManager uses its own playlist.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("── Theme ──")]
        [Tooltip("UI theme to use (create via Create > TopDownTacticalAI > UI Theme)")]
        public UITheme theme;

        [Header("── Background ──")]
        [Tooltip("Main menu background image (drag from Assets/UI/main/background.png)")]
        public Sprite backgroundImage;
        [Tooltip("Canvas/Image for background (auto-created if not assigned)")]
        public Image backgroundImageComponent;

        [Header("── Panels (using AnimatedPanel) ──")]
        [Tooltip("Main menu panel")]
        public AnimatedPanel mainMenuPanel;
        [Tooltip("Settings panel")]
        public AnimatedPanel settingsPanel;
        [Tooltip("Quit confirmation panel")]
        public AnimatedPanel quitConfirmPanel;
        [Tooltip("Controls panel")]
        public AnimatedPanel controlsPanel;
        [Tooltip("Credits panel")]
        public AnimatedPanel creditsPanel;

        [Header("── Buttons ──")]
        [Tooltip("Play button")]
        public Button playButton;
        [Tooltip("Settings button")]
        public Button settingsButton;
        [Tooltip("Controls button")]
        public Button controlsButton;
        [Tooltip("Credits button")]
        public Button creditsButton;
        [Tooltip("Quit button")]
        public Button quitButton;

        [Header("── Quit Confirm Buttons ──")]
        public Button quitConfirmYesButton;
        public Button quitConfirmNoButton;

        [Header("── Back Buttons (in each Panel) ──")]
        public Button settingsBackButton;
        public Button controlsBackButton;
        public Button creditsBackButton;

        [Header("── Scene Names ──")]
        public string levelSelectSceneName = "LevelSelect";

        [Header("── Audio ──")]
        [Tooltip("Button click sound (Optional)")]
        public AudioClip buttonClickSound;
        [Tooltip("Panel open/close sound (Optional)")]
        public AudioClip panelOpenSound;
        public AudioClip panelCloseSound;

        [Header("── Music ──")]
        [Tooltip("Assigned MusicPlaylistSO (if left null, AudioManager uses its default playlist).")]
        public MusicPlaylistSO musicPlaylist;

        private AudioSource _audioSource;
        private AnimatedPanel _currentPanel;
        private NeonMenuVFX _neonVFX;
        private NeonMenuVFX _settingsNeonVFX;

        private void Awake()
        {
            // Setup AudioSource for UI sounds
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            // Auto-setup background
            SetupBackground();

            // Auto-find quit confirm buttons if not assigned in Inspector
            // (the buttons are created by the RebuildQuitConfirm editor tool and may
            // not be wired to this field at build time)
            AutoFindQuitConfirmButtons();

            // Apply theme to all buttons
            ApplyThemeToAllButtons();

            // Setup button listeners
            SetupButtonListeners();

            // Add NeonMenuVFX to the main menu panel if not already present
            if (mainMenuPanel != null)
            {
                _neonVFX = mainMenuPanel.GetComponent<NeonMenuVFX>();
                if (_neonVFX == null) _neonVFX = mainMenuPanel.gameObject.AddComponent<NeonMenuVFX>();
            }

            // Add NeonMenuVFX to the settings panel too so it glows when opened
            if (settingsPanel != null)
            {
                _settingsNeonVFX = settingsPanel.GetComponent<NeonMenuVFX>();
                if (_settingsNeonVFX == null) _settingsNeonVFX = settingsPanel.gameObject.AddComponent<NeonMenuVFX>();
            }

            // Initially show only main menu
            ShowOnlyMainMenu();
        }

        private void Start()
        {
            // Play entrance animation
            mainMenuPanel?.Show();
            PlaySound(panelOpenSound);

            // Start main menu music (Day or Night picked randomly)
            // Uses the inspector's musicPlaylist if set, else falls back to AudioManager's.
            if (AudioManager.Instance != null)
            {
                var playlist = musicPlaylist ?? AudioManager.Instance.musicPlaylist;
                var dayOrNight = DayNightMusicSelector.PickDayOrNightTrack(playlist);
                if (dayOrNight != null && dayOrNight.clip != null)
                {
                    float? fade = playlist != null ? (float?)playlist.defaultCrossfadeDuration : null;
                    AudioManager.Instance.PlayContext(MusicContext.MainMenu, dayOrNight.sceneName, fade);
                }
                else
                {
                    AudioManager.Instance.PlayContext(MusicContext.MainMenu);
                }
            }

            // Neon menu VFX intentionally disabled for a clean, readable menu.
            _neonVFX?.Stop();
        }

        /// <summary>
        /// Searches the scene for Yes/No buttons inside any QuitConfirmPanel and
        /// wires them into the inspector fields if they are still null. This lets
        /// the Rebuild QuitConfirm editor tool create the buttons at any time and
        /// have the controller pick them up automatically on Awake/Start.
        /// </summary>
        private void AutoFindQuitConfirmButtons()
        {
            if (quitConfirmYesButton == null || quitConfirmNoButton == null)
            {
                // Find all QuitConfirmPanel instances
                GameObject[] allRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in allRoots)
                {
                    var panels = root.GetComponentsInChildren<Transform>(true);
                    foreach (var t in panels)
                    {
                        if (t.name == "QuitConfirmPanel")
                        {
                            var container = t.Find("ButtonContainer");
                            if (container != null)
                            {
                                if (quitConfirmYesButton == null)
                                {
                                    var yesT = container.Find("YesButton");
                                    if (yesT != null) quitConfirmYesButton = yesT.GetComponent<Button>();
                                }
                                if (quitConfirmNoButton == null)
                                {
                                    var noT = container.Find("NoButton");
                                    if (noT != null) quitConfirmNoButton = noT.GetComponent<Button>();
                                }
                            }
                        }
                    }
                }
            }
        }

        private void SetupBackground()
        {
            if (backgroundImageComponent == null)
            {
                // Create background image if not assigned
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
                backgroundImageComponent.color = Color.white;
            }
            else if (theme != null && theme.panelBackground != null)
            {
                // Fallback to theme panel background
                backgroundImageComponent.sprite = theme.panelBackground;
                backgroundImageComponent.type = Image.Type.Sliced;
                backgroundImageComponent.color = theme.backgroundColor;
            }
        }

        private void ApplyThemeToAllButtons()
        {
            if (theme == null) return;

            var allButtons = GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                theme.ApplyToButton(btn);
            }
        }

        private void SetupButtonListeners()
        {
            // Main Menu Buttons
            AddListener(playButton, OnPlayButton);
            AddListener(settingsButton, () => ShowPanel(settingsPanel));
            AddListener(controlsButton, () => ShowPanel(controlsPanel));
            AddListener(creditsButton, () => ShowPanel(creditsPanel));
            AddListener(quitButton, OnQuitButton);

            // Quit Confirm
            AddListener(quitConfirmYesButton, OnQuitConfirmYes);
            AddListener(quitConfirmNoButton, OnQuitConfirmNo);

            // Back Buttons
            AddListener(settingsBackButton, () => ShowPanel(mainMenuPanel));
            AddListener(controlsBackButton, () => ShowPanel(mainMenuPanel));
            AddListener(creditsBackButton, () => ShowPanel(mainMenuPanel));
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

        // ── Button Handlers ──

        public void OnPlayButton()
        {
            PlaySound(buttonClickSound);

            // Save current music position so LevelSelect can continue seamlessly
            AudioManager.Instance?.SaveMusicPosition();

            mainMenuPanel?.Hide(0.2f, null, () =>
            {
                if (UIManager.Instance != null) UIManager.Instance.LoadLevelSelect();
                else ScreenTransition.Instance?.LoadSceneWithTransition(levelSelectSceneName);
            });
        }

        public void OnSettingsButton() => ShowPanel(settingsPanel);
        public void OnControlsButton() => ShowPanel(controlsPanel);
        public void OnCreditsButton() => ShowPanel(creditsPanel);

        public void OnQuitButton()
        {
            PlaySound(buttonClickSound);
            if (quitConfirmPanel != null)
            {
                ShowPanel(quitConfirmPanel);
            }
            else
            {
                QuitGame();
            }
        }

        public void OnQuitConfirmYes()
        {
            PlaySound(buttonClickSound);
            QuitGame();
        }

        public void OnQuitConfirmNo()
        {
            PlaySound(buttonClickSound);
            ShowPanel(mainMenuPanel);
        }

        /// <summary>Shows the specified panel and hides all other panels.</summary>
        public void ShowPanel(AnimatedPanel panelToShow)
        {
            if (panelToShow == null) return;

            // Hide current panel instantly to avoid stacking panels
            if (_currentPanel != null && _currentPanel != panelToShow && _currentPanel.IsVisible)
            {
                _currentPanel.HideInstant();
                PlaySound(panelCloseSound);
                // Stop the VFX of the panel we just hid
                StopVFXFor(_currentPanel);
            }

            // Show new panel
            panelToShow.Show();
            PlaySound(panelOpenSound);
            PlayVFXFor(panelToShow);
            _currentPanel = panelToShow;
        }

        private void PlayVFXFor(AnimatedPanel panel)
        {
            if (panel == null) return;
            // Main-menu and settings glow intentionally disabled for clean UI readability.
            if (panel == mainMenuPanel && _neonVFX != null) _neonVFX.Stop();
            else if (panel == settingsPanel && _settingsNeonVFX != null) _settingsNeonVFX.Stop();
            else if (panel == quitConfirmPanel) { /* no VFX on confirm */ }
            else if (panel == controlsPanel) { /* optional */ }
            else if (panel == creditsPanel) { /* optional */ }
        }

        private void StopVFXFor(AnimatedPanel panel)
        {
            if (panel == null) return;
            if (panel == mainMenuPanel && _neonVFX != null) _neonVFX.Stop();
            else if (panel == settingsPanel && _settingsNeonVFX != null) _settingsNeonVFX.Stop();
        }

        private void ShowOnlyMainMenu()
        {
            _currentPanel = mainMenuPanel;

            // Hide all other panels instantly
            if (settingsPanel != null) settingsPanel.HideInstant();
            if (quitConfirmPanel != null) quitConfirmPanel.HideInstant();
            if (controlsPanel != null) controlsPanel.HideInstant();
            if (creditsPanel != null) creditsPanel.HideInstant();

            if (mainMenuPanel != null) mainMenuPanel.ShowInstant();
        }

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}