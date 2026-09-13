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
    /// Pause Menu controller - uses the new UI System.
    /// Supports: Blur Background, Theme, Animation, Tabbed Settings Panel
    /// Uses assets from Assets/UI/pause-menu/
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("── Theme ──")]
        [Tooltip("UI theme to use")]
        public UITheme theme;

        [Header("── Blur Effect ──")]
        [Tooltip("Blur component for background (Optional)")]
        public PauseMenuBlur pauseBlur;

        [Header("── Panels (using AnimatedPanel) ──")]
        [Tooltip("Main pause menu panel")]
        public AnimatedPanel pausePanel;
        [Tooltip("Settings panel (used with SettingsPanelController)")]
        public AnimatedPanel settingsPanel;
        [Tooltip("Quit confirmation panel")]
        public AnimatedPanel quitConfirmPanel;

        [Header("── Pause Menu Buttons ──")]
        public Button resumeButton;
        public Button settingsButton;
        public Button restartButton;
        public Button mainMenuButton;
        public Button quitButton;

        [Header("── Quit Confirm Buttons ──")]
        public Button quitConfirmYesButton;
        public Button quitConfirmNoButton;

        [Header("── Settings Back Button ──")]
        public Button settingsBackButton;

        [Header("── Scene Names ──")]
        public string mainMenuSceneName = "MainMenu";

        [Header("── Audio ──")]
        public AudioClip buttonClickSound;
        public AudioClip panelOpenSound;
        public AudioClip panelCloseSound;
        public AudioClip pauseSound;
        public AudioClip resumeSound;

        [Header("── Music ──")]
        public MusicPlaylistSO musicPlaylist;

        private AudioSource _audioSource;
        private AnimatedPanel _currentPanel;
        private bool _isPaused = false;
        private SettingsPanelController _settingsController;
        private NeonMenuVFX _neonVFX;

        public bool IsPaused => _isPaused;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            // Get SettingsPanelController if exists on settingsPanel
            if (settingsPanel != null)
                _settingsController = settingsPanel.GetComponent<SettingsPanelController>();

            ApplyTheme();
            SetupButtonListeners();

            // Add NeonMenuVFX to pause panel if not present
            if (pausePanel != null)
            {
                _neonVFX = pausePanel.GetComponent<NeonMenuVFX>();
                if (_neonVFX == null) _neonVFX = pausePanel.gameObject.AddComponent<NeonMenuVFX>();
            }

            // Initially hide all panels
            HideAllPanelsInstant();
            _isPaused = false;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            // ESC to toggle pause (only when not in settings sub-menu)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_currentPanel == settingsPanel && settingsPanel != null && settingsPanel.IsVisible)
                {
                    CloseSettingsBackToPause();
                }
                else if (_isPaused)
                {
                    Resume();
                }
                else
                {
                    Pause();
                }
            }
        }

        private void ApplyTheme()
        {
            if (theme == null) return;

            // Apply style per button based on its function. This gives visual feedback about
            // which actions are safe (Normal) vs. dangerous (Destructive — Quit, Yes-quit).
            // Resume/Settings/Restart/Main Menu = Normal; Quit = Destructive;
            // Yes (confirm quit) = Destructive; No (cancel) = Normal.
            ApplyButtonStyle(resumeButton, UITheme.ButtonStyle.Normal);
            ApplyButtonStyle(settingsButton, UITheme.ButtonStyle.Normal);
            ApplyButtonStyle(restartButton, UITheme.ButtonStyle.Normal);
            ApplyButtonStyle(mainMenuButton, UITheme.ButtonStyle.Normal);
            ApplyButtonStyle(quitButton, UITheme.ButtonStyle.Destructive);

            // Quit confirm sub-panel
            ApplyButtonStyle(quitConfirmYesButton, UITheme.ButtonStyle.Destructive);
            ApplyButtonStyle(quitConfirmNoButton, UITheme.ButtonStyle.Normal);

            // Settings back button
            ApplyButtonStyle(settingsBackButton, UITheme.ButtonStyle.Normal);

            // Any other buttons we didn't enumerate above (e.g. inside the SettingsPanel prefab)
            // still get the default Normal styling, so they don't look un-themed.
            var styledSet = new System.Collections.Generic.HashSet<Button>
            {
                resumeButton, settingsButton, restartButton, mainMenuButton, quitButton,
                quitConfirmYesButton, quitConfirmNoButton, settingsBackButton
            };
            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                if (btn != null && !styledSet.Contains(btn))
                    theme.ApplyToButton(btn);
            }

            // Apply to settings panel if it has its own controller
            if (_settingsController != null && _settingsController.theme == null)
            {
                _settingsController.theme = theme;
            }
        }

        private void ApplyButtonStyle(Button button, UITheme.ButtonStyle style)
        {
            if (button == null || theme == null) return;
            theme.ApplyToButton(button, style);
        }

        private void SetupButtonListeners()
        {
            // Pause Menu Buttons
            AddListener(resumeButton, Resume);
            AddListener(settingsButton, OpenSettingsFromPause);
            AddListener(restartButton, RestartLevel);
            AddListener(mainMenuButton, ReturnToMainMenu);
            AddListener(quitButton, OnQuitButton);

            // Quit Confirm
            AddListener(quitConfirmYesButton, OnQuitConfirmYes);
            AddListener(quitConfirmNoButton, OnQuitConfirmNo);

            // Settings Back
            AddListener(settingsBackButton, CloseSettingsBackToPause);
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

        public void Pause()
        {
            if (_isPaused) return;

            _isPaused = true;
            Time.timeScale = 0f;

            // Enable blur
            pauseBlur?.EnableBlur();

            // Show pause panel
            _currentPanel = pausePanel;
            pausePanel?.Show();
            PlaySound(pauseSound);

            // Switch to Pause music context (seamless crossfade from Gameplay)
            AudioManager.Instance?.PlayContext(MusicContext.Pause);

            // Start neon VFX on the pause panel
            _neonVFX?.Play();
        }

        public void Resume()
        {
            if (!_isPaused) return;

            _isPaused = false;
            Time.timeScale = 1f;

            // Disable blur
            pauseBlur?.DisableBlur();

            // Hide all panels
            HideAllPanels();
            PlaySound(resumeSound);

            // Return to Gameplay music context
            AudioManager.Instance?.PlayContext(MusicContext.Gameplay);

            // Stop neon VFX on the pause panel
            _neonVFX?.Stop();
        }

        public void OpenSettingsFromPause()
        {
            if (!_isPaused) return;

            // Hide pause panel, show settings panel
            pausePanel?.Hide();
            PlaySound(panelCloseSound);

            settingsPanel?.Show();
            PlaySound(panelOpenSound);
            _currentPanel = settingsPanel;
        }

        public void CloseSettingsBackToPause()
        {
            if (!_isPaused) return;

            // Hide settings panel, show pause panel
            settingsPanel?.Hide();
            PlaySound(panelCloseSound);

            pausePanel?.Show();
            PlaySound(panelOpenSound);
            _currentPanel = pausePanel;
        }

        public void RestartLevel()
        {
            PlaySound(buttonClickSound);
            Resume(); // This will set Time.timeScale = 1f
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToMainMenu()
        {
            PlaySound(buttonClickSound);
            Resume(); // Important: resume first to reset Time.timeScale

            // Prefer the nice transition if ScreenTransition is alive in the scene.
            // Fall back to a direct scene load if not — otherwise pressing "Return to Menu"
            // would silently do nothing (the bug this comment is trying to prevent).
            if (ScreenTransition.Instance != null)
            {
                ScreenTransition.Instance.LoadSceneWithTransition(mainMenuSceneName);
            }
            else
            {
                Debug.Log($"[PauseMenu] ScreenTransition missing — loading '{mainMenuSceneName}' directly.");
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        public void OnQuitButton()
        {
            PlaySound(buttonClickSound);
            if (quitConfirmPanel != null)
            {
                pausePanel?.Hide();
                quitConfirmPanel?.Show();
                _currentPanel = quitConfirmPanel;
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
            quitConfirmPanel?.Hide();
            pausePanel?.Show();
            _currentPanel = pausePanel;
        }

        private void HideAllPanels()
        {
            pausePanel?.Hide();
            settingsPanel?.Hide();
            quitConfirmPanel?.Hide();
        }

        private void HideAllPanelsInstant()
        {
            pausePanel?.HideInstant();
            settingsPanel?.HideInstant();
            quitConfirmPanel?.HideInstant();
        }

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }

        private void QuitGame()
        {
            Time.timeScale = 1f; // Ensure time scale is reset
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            // Ensure time scale is reset if scene changes while paused
            Time.timeScale = 1f;
            pauseBlur?.DisableBlur();
        }
    }
}