using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Attach to each Settings Panel instance (there can be multiple across different scenes, e.g. one in the main menu + one in the Pause Menu).
    /// Holds references to the Slider/Dropdown/Toggle widgets "specific to this panel" and syncs them with SettingsManager.Instance
    /// (the persistent singleton) every time this panel is opened (OnEnable). This fixes the previous problem where SettingsManager
    /// held direct references that would break on scene changes, making it impossible to use with a second or third Settings Panel.
    ///
    /// Setup: Attach this script to the Settings Panel itself (not to the SettingsManager), then drag the Slider/Dropdown/Toggle
    /// widgets from this panel into the Inspector fields. Do the same for every Settings Panel instance in the project (any number is fine).
    /// </summary>
    public class SettingsPanelBinder : MonoBehaviour
    {
        [Header("── Audio UI (drag from this Panel instance) ──")]
        public Slider masterVolumeSlider;
        public Slider musicVolumeSlider;
        public Slider sfxVolumeSlider;

        [Header("── Display UI ──")]
        public TMP_Dropdown resolutionDropdown;
        public Toggle fullscreenToggle;
        public TMP_Dropdown qualityDropdown;

        [Header("── Gameplay Extras ──")]
        public Slider mouseSensitivitySlider;
        public Toggle screenShakeToggle;
        public Toggle showFpsToggle;

        [Header("── Graphics & Effects ──")]
        [Tooltip("0..1 — drives the intensity of all in-game VFX (halo, dust, particles, screen shake, etc.). 1 = full, 0 = off.")]
        public Slider effectsQualitySlider;
        [Tooltip("Toggle the neon menu VFX (halo + dust particles behind menu/pause panels).")]
        public Toggle menuVFXToggle;
        [Tooltip("Toggle background VFX (atmospheric effects during gameplay).")]
        public Toggle backgroundVFXToggle;
        [Tooltip("Toggle screen effects (chromatic aberration, screen shake, etc. during gameplay).")]
        public Toggle screenEffectsToggle;

        private bool _listenersWired = false;
        // touched: force-recompile marker 2026-09-07

        /// <summary>Called every time this panel becomes active — refreshes current values and wires listeners if not already done.</summary>
        private void OnEnable()
        {
            EnsureSettingsManagerExists();

            SetupDropdownsIfNeeded();
            RefreshUIFromCurrentValues();

            if (!_listenersWired)
            {
                WireUpListeners();
                _listenersWired = true;
            }
        }

        /// <summary>
        /// Creates a SettingsManager automatically if one isn't in the scene, instead of silently doing nothing.
        /// (The previous code returned immediately if not found, which broke the Settings Panel completely when
        /// testing by pressing Play from any scene other than MainMenu — because SettingsManager is only
        /// created in the MainMenu scene. This was a common frustration during testing since developers
        /// often press Play from whichever scene is currently open, not always the first one.)
        /// </summary>
        private void EnsureSettingsManagerExists()
        {
            if (SettingsManager.Instance != null) return;
            var obj = new GameObject("SettingsManager (auto-created)");
            obj.AddComponent<SettingsManager>();
        }

        private bool _dropdownsSetup = false;

        /// <summary>
        /// Populates the Dropdown widgets with real screen resolutions / graphics quality options
        /// instead of the placeholder "Option A/B/C" values Unity adds when a new Dropdown is created.
        /// (The old check `options.Count == 0` could never be true, because new Dropdowns always come
        /// pre-populated with 3 placeholders — so the real options were never filled in. Using the
        /// _dropdownsSetup flag instead preserves the original "fill only once per panel" behavior.)
        /// </summary>
        private void SetupDropdownsIfNeeded()
        {
            if (_dropdownsSetup) return;
            var mgr = SettingsManager.Instance;

            // Make sure each dropdown has its own ItemText template so the popup items
            // can show different text per option (otherwise they all show the caption text).
            EnsureItemText(resolutionDropdown);
            EnsureItemText(qualityDropdown);

            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(mgr.GetResolutionOptionLabels());
            }

            if (qualityDropdown != null)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(mgr.GetQualityOptionLabels());
            }

            _dropdownsSetup = true;
        }

        /// <summary>
        /// TMP_Dropdown uses m_ItemText as the template for items in the popup list. If it is null,
        /// Unity falls back to using the caption as the template, which causes every item to display
        /// the same text as the caption (the "all items show the same text" bug).
        ///
        /// This method fixes that by either:
        ///   (a) finding an existing "ItemText" / "Item Label" TMP_Text inside the dropdown's template, or
        ///   (b) creating a new TMP_Text child under the ItemTemplate and assigning it.
        /// Done at runtime so it works with any existing scene/prefab that was created in the editor.
        /// </summary>
        private void EnsureItemText(TMP_Dropdown dropdown)
        {
            if (dropdown == null) return;

            // Already wired up by the editor — nothing to do.
            if (dropdown.itemText != null) return;

            // TMP_Dropdown stores its template on the 'template' field. Inside that template is an
            // "Item" GameObject with an "Item Label" child that holds a TMP_Text. We can use that
            // existing one as our itemText.
            var template = dropdown.template;
            if (template == null)
            {
                Debug.LogWarning($"[SettingsPanelBinder] Dropdown '{dropdown.name}' has no template assigned; cannot fix item text.");
                return;
            }

            // Look for an existing TMP_Text inside the template that we can use.
            var existingLabel = template.GetComponentInChildren<TMP_Text>(true);
            if (existingLabel != null)
            {
                dropdown.itemText = existingLabel;
                return;
            }

            // No label exists inside the template — create one ourselves as a child of the
            // "Item" GameObject (which is what TMP_Dropdown clones per option).
            var itemTemplate = template.Find("Item");
            GameObject host = itemTemplate != null ? itemTemplate.gameObject : template.gameObject;

            var go = new GameObject("ItemText (auto-created)", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(host.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = " ";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontSize = 16;
            tmp.raycastTarget = false;

            dropdown.itemText = tmp;
        }

        /// <summary>Refreshes the UI to match the current values in SettingsManager — exposed as public so SettingsPanelController.cs can call it directly.</summary>
        public void RefreshUIFromCurrentValues()
        {
            var mgr = SettingsManager.Instance;

            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(mgr.MasterVolume);
            if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(mgr.MusicVolume);
            if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(mgr.SFXVolume);
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.SetValueWithoutNotify(mgr.MouseSensitivity);
            if (screenShakeToggle != null) screenShakeToggle.SetIsOnWithoutNotify(mgr.ScreenShakeEnabled);
            if (showFpsToggle != null) showFpsToggle.SetIsOnWithoutNotify(mgr.ShowFps);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(mgr.Fullscreen);
            if (resolutionDropdown != null)
            {
                resolutionDropdown.SetValueWithoutNotify(mgr.ResolutionIndex);
                // Force TMP_Dropdown to update its caption text to match the current value
                resolutionDropdown.RefreshShownValue();
            }
            if (qualityDropdown != null)
            {
                qualityDropdown.SetValueWithoutNotify(mgr.QualityLevel);
                qualityDropdown.RefreshShownValue();
            }

            // Graphics & Effects
            if (effectsQualitySlider != null) effectsQualitySlider.SetValueWithoutNotify(mgr.EffectsQuality);
            if (menuVFXToggle != null) menuVFXToggle.SetIsOnWithoutNotify(mgr.MenuVFXEnabled);
            if (backgroundVFXToggle != null) backgroundVFXToggle.SetIsOnWithoutNotify(mgr.BackgroundVFXEnabled);
            if (screenEffectsToggle != null) screenEffectsToggle.SetIsOnWithoutNotify(mgr.ScreenEffectsEnabled);
        }

        /// <summary>Wires each UI widget's events to call SettingsManager.Instance directly — done only once per panel.</summary>
        private void WireUpListeners()
        {
            var mgr = SettingsManager.Instance;

            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(mgr.SetMasterVolume);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(mgr.SetMusicVolume);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(mgr.SetSFXVolume);
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.AddListener(mgr.SetMouseSensitivity);
            if (screenShakeToggle != null) screenShakeToggle.onValueChanged.AddListener(mgr.SetScreenShake);
            if (showFpsToggle != null) showFpsToggle.onValueChanged.AddListener(mgr.SetShowFps);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(mgr.SetFullscreen);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(mgr.SetResolution);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(mgr.SetQuality);

            // Graphics & Effects
            if (effectsQualitySlider != null) effectsQualitySlider.onValueChanged.AddListener(mgr.SetEffectsQuality);
            if (menuVFXToggle != null) menuVFXToggle.onValueChanged.AddListener(mgr.SetMenuVFX);
            if (backgroundVFXToggle != null) backgroundVFXToggle.onValueChanged.AddListener(mgr.SetBackgroundVFX);
            if (screenEffectsToggle != null) screenEffectsToggle.onValueChanged.AddListener(mgr.SetScreenEffects);

            // Also force the caption to refresh on value change (some TMP_Dropdown setups don't auto-update)
            if (resolutionDropdown != null)
            {
                resolutionDropdown.onValueChanged.AddListener((idx) => resolutionDropdown.RefreshShownValue());
            }
            if (qualityDropdown != null)
            {
                qualityDropdown.onValueChanged.AddListener((idx) => qualityDropdown.RefreshShownValue());
            }
        }
    }
}
