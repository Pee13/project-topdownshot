using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TopDownTacticalAI.UI.Themes;
using TopDownTacticalAI.UI.Animation;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Settings Panel controller with Tabbed Layout.
    /// Works with SettingsManager and SettingsPanelBinder.
    /// Supports Theme, Animation, and assets from Assets/UI/settings/
    /// </summary>
    public class SettingsPanelController : MonoBehaviour
    {
        public enum Tab { Audio, Video, Gameplay, Controls }

        [Header("── Theme ──")]
        [Tooltip("UI theme to use")]
        public UITheme theme;

        [Header("── Background Images (from Assets/UI/settings/) ──")]
        [Tooltip("Large background - purple for Audio tab")]
        public Sprite audioBackgroundLarge;
        [Tooltip("Small background - purple for Audio tab")]
        public Sprite audioBackgroundSmall;
        [Tooltip("Large background - blue for Video/Graphics tab")]
        public Sprite videoBackgroundLarge;
        [Tooltip("Small background - blue for Video/Graphics tab")]
        public Sprite videoBackgroundSmall;
        [Tooltip("Large background - yellow for Controls/Gameplay tab")]
        public Sprite controlsBackgroundLarge;
        [Tooltip("Small background - yellow for Controls/Gameplay tab")]
        public Sprite controlsBackgroundSmall;
        [Tooltip("Blue background for general Settings")]
        public Sprite settingsBackgroundGeneral;

        [Header("── Tab Buttons ──")]
        public Button audioTabButton;
        public Button videoTabButton;
        public Button gameplayTabButton;
        public Button controlsTabButton;

        [Header("── Tab Panels ──")]
        public GameObject audioTabContent;
        public GameObject videoTabContent;
        public GameObject gameplayTabContent;
        public GameObject controlsTabContent;

        [Header("── Audio Settings UI ──")]
        public Slider masterVolumeSlider;
        public Slider musicVolumeSlider;
        public Slider sfxVolumeSlider;

        [Header("── Video Settings UI ──")]
        public TMP_Dropdown resolutionDropdown;
        public Toggle fullscreenToggle;
        public TMP_Dropdown qualityDropdown;

        [Header("── Gameplay Settings UI ──")]
        public Slider mouseSensitivitySlider;
        public Toggle screenShakeToggle;
        public Toggle showFpsToggle;

        [Header("── Controls Settings UI ──")]
        [Tooltip("Panel that displays controls (key bindings)")]
        public GameObject controlsInfoPanel;

        [Header("── Back Button ──")]
        public Button backButton;

        [Header("── Animation ──")]
        public AnimatedPanel settingsPanel;
        public float tabSwitchDuration = 0.2f;

        [Header("── Audio ──")]
        public AudioClip tabSwitchSound;
        public AudioClip sliderChangeSound;

        private SettingsManager _settingsManager;
        private SettingsPanelBinder _settingsBinder;
        private Tab _currentTab = Tab.Audio;
        private AudioSource _audioSource;
        private Image _backgroundImage;

        private void Awake()
        {
            _settingsManager = SettingsManager.Instance;
            _settingsBinder = GetComponent<SettingsPanelBinder>();
            if (_settingsBinder == null) _settingsBinder = gameObject.AddComponent<SettingsPanelBinder>();

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            // Setup background image
            SetupBackground();

            // Apply theme
            ApplyTheme();

            // Setup tab buttons
            SetupTabButtons();

            // Auto-bind UI to SettingsManager via SettingsPanelBinder
            BindUIToSettingsBinder();
        }

        private void OnEnable()
        {
            // Refresh UI when panel opens
            _settingsBinder?.RefreshUIFromCurrentValues();
            ShowTab(_currentTab, false);
        }

        private void SetupBackground()
        {
            _backgroundImage = GetComponent<Image>();
            if (_backgroundImage == null) _backgroundImage = gameObject.AddComponent<Image>();

            if (settingsBackgroundGeneral != null)
            {
                _backgroundImage.sprite = settingsBackgroundGeneral;
                _backgroundImage.type = Image.Type.Sliced;
                _backgroundImage.color = Color.white;
            }
            else if (theme != null && theme.panelBackground != null)
            {
                _backgroundImage.sprite = theme.panelBackground;
                _backgroundImage.type = Image.Type.Sliced;
                _backgroundImage.color = theme.panelColor;
            }
        }

        private void ApplyTheme()
        {
            if (theme == null) return;

            // Apply to all buttons
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                theme.ApplyToButton(btn);
            }

            // Apply to sliders
            var sliders = GetComponentsInChildren<Slider>(true);
            foreach (var slider in sliders)
            {
                theme.ApplyToSlider(slider);
            }

            // Apply to toggles
            var toggles = GetComponentsInChildren<Toggle>(true);
            foreach (var toggle in toggles)
            {
                theme.ApplyToToggle(toggle);
            }

            // Apply to dropdowns
            var dropdowns = GetComponentsInChildren<TMP_Dropdown>(true);
            foreach (var dropdown in dropdowns)
            {
                theme.ApplyToDropdown(dropdown);
            }

            // Apply to texts
            var texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in texts)
            {
                bool isHeader = text.fontSize > 30;
                theme.ApplyToText(text, isHeader);
            }
        }

        private void SetupTabButtons()
        {
            AddTabListener(audioTabButton, Tab.Audio);
            AddTabListener(videoTabButton, Tab.Video);
            AddTabListener(gameplayTabButton, Tab.Gameplay);
            AddTabListener(controlsTabButton, Tab.Controls);

            AddListener(backButton, OnBackButton);
        }

        private void AddTabListener(Button button, Tab tab)
        {
            if (button != null)
            {
                button.onClick.AddListener(() =>
                {
                    PlaySound(tabSwitchSound);
                    ShowTab(tab);
                });
            }
        }

        private void AddListener(Button button, System.Action action)
        {
            if (button != null)
            {
                button.onClick.AddListener(() =>
                {
                    PlaySound(tabSwitchSound);
                    action?.Invoke();
                });
            }
        }

        private void BindUIToSettingsBinder()
        {
            if (_settingsBinder == null) return;

            // Audio
            _settingsBinder.masterVolumeSlider = masterVolumeSlider;
            _settingsBinder.musicVolumeSlider = musicVolumeSlider;
            _settingsBinder.sfxVolumeSlider = sfxVolumeSlider;

            // Video
            _settingsBinder.resolutionDropdown = resolutionDropdown;
            _settingsBinder.fullscreenToggle = fullscreenToggle;
            _settingsBinder.qualityDropdown = qualityDropdown;

            // Gameplay
            _settingsBinder.mouseSensitivitySlider = mouseSensitivitySlider;
            _settingsBinder.screenShakeToggle = screenShakeToggle;
            _settingsBinder.showFpsToggle = showFpsToggle;
        }

        public void ShowTab(Tab tab, bool animate = true)
        {
            if (_currentTab == tab) return;

            _currentTab = tab;

            // Update tab button visuals
            UpdateTabButtonVisuals();

            // Switch content
            if (animate)
            {
                SwitchTabContent(tab);
            }
            else
            {
                SetTabContentActive(tab);
            }

            // Update background based on tab
            UpdateBackgroundForTab(tab);
        }

        private void UpdateTabButtonVisuals()
        {
            var tabButtons = new[] { audioTabButton, videoTabButton, gameplayTabButton, controlsTabButton };
            var tabs = new[] { Tab.Audio, Tab.Video, Tab.Gameplay, Tab.Controls };

            for (int i = 0; i < tabButtons.Length; i++)
            {
                var btn = tabButtons[i];
                if (btn == null) continue;

                var colors = btn.colors;
                if (tabs[i] == _currentTab)
                {
                    // Selected tab - use accent color
                    colors.normalColor = theme != null ? theme.accentColor : new Color(1f, 0.85f, 0.2f, 1f);
                    colors.highlightedColor = theme != null ? theme.accentColor : new Color(1f, 0.85f, 0.2f, 1f);
                }
                else
                {
                    // Normal tab
                    colors.normalColor = theme != null ? theme.buttonNormal : new Color(0.2f, 0.3f, 0.5f, 1f);
                    colors.highlightedColor = theme != null ? theme.buttonHighlighted : new Color(0.3f, 0.5f, 0.8f, 1f);
                }
                btn.colors = colors;
            }
        }

        private void SwitchTabContent(Tab newTab)
        {
            // Fade out current, fade in new
            var currentContent = GetTabContent(_currentTab);
            var newContent = GetTabContent(newTab);

            if (currentContent != null)
            {
                var cg = currentContent.GetComponent<CanvasGroup>();
                if (cg == null) cg = currentContent.AddComponent<CanvasGroup>();
                StartCoroutine(FadeCanvasGroup(cg, 1f, 0f, tabSwitchDuration * 0.5f, () => currentContent.SetActive(false)));
            }

            if (newContent != null)
            {
                newContent.SetActive(true);
                var cg = newContent.GetComponent<CanvasGroup>();
                if (cg == null) cg = newContent.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                StartCoroutine(FadeCanvasGroup(cg, 0f, 1f, tabSwitchDuration * 0.5f));
            }
        }

        private void SetTabContentActive(Tab tab)
        {
            audioTabContent?.SetActive(tab == Tab.Audio);
            videoTabContent?.SetActive(tab == Tab.Video);
            gameplayTabContent?.SetActive(tab == Tab.Gameplay);
            controlsTabContent?.SetActive(tab == Tab.Controls);
        }

        private GameObject GetTabContent(Tab tab)
        {
            return tab switch
            {
                Tab.Audio => audioTabContent,
                Tab.Video => videoTabContent,
                Tab.Gameplay => gameplayTabContent,
                Tab.Controls => controlsTabContent,
                _ => null
            };
        }

        private void UpdateBackgroundForTab(Tab tab)
        {
            if (_backgroundImage == null) return;

            Sprite newBg = tab switch
            {
                Tab.Audio => audioBackgroundLarge ?? settingsBackgroundGeneral,
                Tab.Video => videoBackgroundLarge ?? settingsBackgroundGeneral,
                Tab.Gameplay => controlsBackgroundLarge ?? settingsBackgroundGeneral,
                Tab.Controls => controlsBackgroundLarge ?? settingsBackgroundGeneral,
                _ => settingsBackgroundGeneral
            };

            if (newBg != null)
            {
                _backgroundImage.sprite = newBg;
            }
        }

        private System.Collections.IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration, System.Action onComplete = null)
        {
            float elapsed = 0f;
            cg.alpha = from;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                cg.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            cg.alpha = to;
            onComplete?.Invoke();
        }

        private void OnBackButton()
        {
            PlaySound(tabSwitchSound);
            settingsPanel?.Hide(0.2f);
        }

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}