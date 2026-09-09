using UnityEngine;
using System.Collections.Generic;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Stores all settings values (audio/display/quality/extras) + Apply/Save/Load logic only.
    /// Does NOT hold direct references to any Slider/Dropdown in any scene — because it is a cross-scene Singleton (DontDestroyOnLoad).
    /// If it held direct references to one scene's UI, those references would be lost when that scene is unloaded, making it impossible
    /// to use with a second Settings Panel (e.g. the one in the Pause Menu). This was fixed by separating the UI-binding
    /// logic into SettingsPanelBinder.cs instead. SettingsPanelBinder can be attached to any number of panel instances across any scenes,
    /// and they will all sync with this singleton automatically.
    ///
    /// Setup: Create an empty GameObject named "SettingsManager" in the MainMenu scene and attach this script (no UI drag-in required).
    /// For each UI set (MainMenu, Pause Menu), attach SettingsPanelBinder.cs to that panel instead.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // ── Current values (readable from other scripts, e.g. CameraFollow reads ScreenShakeEnabled) ──
        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 0.7f;
        public float SFXVolume { get; private set; } = 1f;
        public float MouseSensitivity { get; private set; } = 1f;
        public bool ScreenShakeEnabled { get; private set; } = true;
        public bool ShowFps { get; private set; } = false;
        public bool Fullscreen { get; private set; } = true;
        public int QualityLevel { get; private set; }
        public int ResolutionIndex { get; private set; }

        // ── Graphics & Effects (delegated to GraphicsQualityManager) ──
        public float EffectsQuality => GraphicsQualityManager.EffectsQuality;
        public bool MenuVFXEnabled => GraphicsQualityManager.MenuVFXEnabled;
        public bool BackgroundVFXEnabled => GraphicsQualityManager.BackgroundVFXEnabled;
        public bool ScreenEffectsEnabled => GraphicsQualityManager.ScreenEffectsEnabled;

        public Resolution[] AvailableResolutions { get; private set; }

        private GameObject _fpsCounterObj;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Detach from any parent (e.g. if accidentally placed as a child of Canvas) because DontDestroyOnLoad
            // only works on root GameObjects; otherwise it will warn and not function correctly.
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // Initialize graphics quality settings before loading PlayerPrefs
            GraphicsQualityManager.Initialize();

            AvailableResolutions = Screen.resolutions;
            LoadSettings();
            ApplyAll();
        }

        // ── Audio ──
        public void SetMasterVolume(float v)
        {
            MasterVolume = v;
            AudioListener.volume = v;
            SaveSettings();
        }

        public void SetMusicVolume(float v)
        {
            MusicVolume = v;
            AudioManager.Instance?.SetMusicVolume(v);
            SaveSettings();
        }

        public void SetSFXVolume(float v)
        {
            SFXVolume = v;
            AudioManager.Instance?.SetSFXVolume(v);
            SaveSettings();
        }

        // ── Gameplay Extras ──
        public void SetMouseSensitivity(float v)
        {
            MouseSensitivity = v;
            SaveSettings();
        }

        public void SetScreenShake(bool enabled)
        {
            ScreenShakeEnabled = enabled;
            SaveSettings();
        }

        public void SetShowFps(bool enabled)
        {
            ShowFps = enabled;
            if (enabled) EnsureFpsCounter();
            if (_fpsCounterObj != null) _fpsCounterObj.SetActive(enabled);
            SaveSettings();
        }

        private void EnsureFpsCounter()
        {
            if (_fpsCounterObj != null) return;
            _fpsCounterObj = new GameObject("FPSCounter");
            DontDestroyOnLoad(_fpsCounterObj);
            _fpsCounterObj.AddComponent<FpsCounterDisplay>();
        }

        // ── Display ──
        public void SetResolution(int index)
        {
            if (AvailableResolutions == null || index < 0 || index >= AvailableResolutions.Length) return;
            ResolutionIndex = index;
            var r = AvailableResolutions[index];
            Screen.SetResolution(r.width, r.height, Fullscreen);
            SaveSettings();
        }

        public void SetFullscreen(bool isFullscreen)
        {
            Fullscreen = isFullscreen;
            Screen.fullScreen = isFullscreen;
            SaveSettings();
        }

        public void SetQuality(int index)
        {
            QualityLevel = index;
            QualitySettings.SetQualityLevel(index, true);
            SaveSettings();
        }

        // ── Graphics & Effects ──
        public void SetEffectsQuality(float v)
        {
            GraphicsQualityManager.SetEffectsQuality(v);
        }

        public void SetMenuVFX(bool enabled)
        {
            GraphicsQualityManager.SetMenuVFXEnabled(enabled);
        }

        public void SetBackgroundVFX(bool enabled)
        {
            GraphicsQualityManager.SetBackgroundVFXEnabled(enabled);
        }

        public void SetScreenEffects(bool enabled)
        {
            GraphicsQualityManager.SetScreenEffectsEnabled(enabled);
        }

        /// <summary>Returns all available screen resolutions as ready-to-use text labels for SettingsPanelBinder to populate the Dropdown.
        /// Deduplicates by width+height (keeps the highest refresh rate for each), and sorts from highest to lowest so users see 1920x1080 first.</summary>
        public List<string> GetResolutionOptionLabels()
        {
            // Group by width+height, keep the entry with the highest refresh rate
            var bestBySize = new System.Collections.Generic.Dictionary<(int w, int h), Resolution>();
            if (AvailableResolutions != null)
            {
                foreach (var r in AvailableResolutions)
                {
                    var key = (r.width, r.height);
                    if (!bestBySize.TryGetValue(key, out var existing) ||
                        r.refreshRateRatio.value > existing.refreshRateRatio.value)
                    {
                        bestBySize[key] = r;
                    }
                }
            }

            // If Screen.resolutions returned nothing (e.g. running headless or in some editor setups),
            // fall back to a common list of resolutions so the user still has choices.
            if (bestBySize.Count == 0)
            {
                int[] commonWidths = { 3840, 2560, 1920, 1680, 1600, 1440, 1366, 1280, 1176, 1024, 800, 640 };
                int[] commonHeights = { 2160, 1440, 1080, 1050, 900, 900, 768, 720, 664, 768, 600, 480 };
                for (int i = 0; i < commonWidths.Length; i++)
                {
                    bestBySize[(commonWidths[i], commonHeights[i])] = new Resolution
                    {
                        width = commonWidths[i],
                        height = commonHeights[i],
                        refreshRateRatio = new RefreshRate { numerator = 60000, denominator = 1000 }
                    };
                }
            }

            // Sort by total pixel count, descending (largest first) so the dropdown shows
            // the highest resolution at the top.
            var sorted = new List<Resolution>(bestBySize.Values);
            sorted.Sort((a, b) => (b.width * b.height).CompareTo(a.width * b.height));

            var options = new List<string>();
            foreach (var r in sorted)
                options.Add($"{r.width} x {r.height} @{Mathf.RoundToInt((float)r.refreshRateRatio.value)}Hz");
            return options;
        }

        /// <summary>
        /// Returns the list of graphics quality levels as labels, ordered from highest quality
        /// (Ultra) at the top to lowest (Very Low) at the bottom — matching how a user expects
        /// a "best → worst" dropdown to read. Unity's QualitySettings.names is the opposite.
        /// </summary>
        public List<string> GetQualityOptionLabels()
        {
            var names = new List<string>(QualitySettings.names);
            names.Reverse(); // Ultra → ... → Very Low
            return names;
        }

        // ── Save / Load (PlayerPrefs) ──
        private void SaveSettings()
        {
            PlayerPrefs.SetFloat("MasterVolume", MasterVolume);
            PlayerPrefs.SetFloat("MusicVolume", MusicVolume);
            PlayerPrefs.SetFloat("SFXVolume", SFXVolume);
            PlayerPrefs.SetFloat("MouseSensitivity", MouseSensitivity);
            PlayerPrefs.SetInt("ScreenShake", ScreenShakeEnabled ? 1 : 0);
            PlayerPrefs.SetInt("ShowFps", ShowFps ? 1 : 0);
            PlayerPrefs.SetInt("Fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("QualityLevel", QualityLevel);
            PlayerPrefs.SetInt("ResolutionIndex", ResolutionIndex);
            PlayerPrefs.Save();
        }

        private void LoadSettings()
        {
            MasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            MusicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            SFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
            MouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
            ScreenShakeEnabled = PlayerPrefs.GetInt("ScreenShake", 1) == 1;
            ShowFps = PlayerPrefs.GetInt("ShowFps", 0) == 1;
            Fullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            QualityLevel = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());

            int defaultResIndex = 0;
            for (int i = 0; i < AvailableResolutions.Length; i++)
            {
                if (AvailableResolutions[i].width == Screen.currentResolution.width &&
                    AvailableResolutions[i].height == Screen.currentResolution.height)
                { defaultResIndex = i; break; }
            }
            ResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", defaultResIndex);
        }

        /// <summary>Applies all loaded values for real at game start (applies everything at once).</summary>
        private void ApplyAll()
        {
            AudioListener.volume = MasterVolume;
            AudioManager.Instance?.SetMusicVolume(MusicVolume);
            AudioManager.Instance?.SetSFXVolume(SFXVolume);
            Screen.fullScreen = Fullscreen;
            QualitySettings.SetQualityLevel(QualityLevel, true);
            if (ShowFps) EnsureFpsCounter();
        }
    }
}
