using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// ตัวจัดการหน้า Settings ทั้งหมด: เสียง (Master/Music/SFX), หน้าจอ (ความละเอียด/Fullscreen), คุณภาพกราฟิก
    /// บันทึกค่าอัตโนมัติผ่าน PlayerPrefs ทุกครั้งที่เปลี่ยน และโหลดค่าที่เคยตั้งไว้กลับมาอัตโนมัติตอนเริ่มเกม
    ///
    /// เป็น Singleton อยู่ข้ามฉาก (DontDestroyOnLoad) — สร้างแค่ตัวเดียวในฉาก MainMenu ก็พอ ไม่ต้องสร้างซ้ำในฉากอื่น
    ///
    /// วิธีติดตั้ง: สร้าง Empty GameObject ชื่อ "SettingsManager" ในฉาก MainMenu แปะสคริปต์นี้
    /// แล้วลาก UI Element จาก Settings Panel มาใส่ในช่อง Inspector ให้ครบ (ดูรายละเอียดที่ README)
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        [Header("── Audio UI (ลากจาก Settings Panel มาใส่) ──")]
        public Slider masterVolumeSlider;
        public Slider musicVolumeSlider;
        public Slider sfxVolumeSlider;

        [Header("── Display UI ──")]
        public TMP_Dropdown resolutionDropdown;
        public Toggle fullscreenToggle;
        public TMP_Dropdown qualityDropdown;

        [Header("── Gameplay Extras (Option เสริมที่แนะนำเพิ่ม) ──")]
        [Tooltip("ความไวเมาส์ตอนเล็ง — เกมยิงมุมมองบนควรปรับความไวเมาส์ได้ เพราะแต่ละคนถนัดไม่เท่ากัน")]
        public Slider mouseSensitivitySlider;
        [Tooltip("สั่นกล้องตอนโดนตี/ยิง — บางคนเวียนหัวง่าย ควรปิดได้")]
        public Toggle screenShakeToggle;
        [Tooltip("แสดง FPS มุมจอ — มีประโยชน์ตอนทดสอบว่าเครื่องแรงพอไหม")]
        public Toggle showFpsToggle;

        // ── ค่าปัจจุบัน (อ่านได้จากสคริปต์อื่น เช่น CameraFollow อ่าน ScreenShakeEnabled ไปใช้) ──
        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 0.7f;
        public float SFXVolume { get; private set; } = 1f;
        public float MouseSensitivity { get; private set; } = 1f;
        public bool ScreenShakeEnabled { get; private set; } = true;
        public bool ShowFps { get; private set; } = false;

        private Resolution[] _availableResolutions;
        private GameObject _fpsCounterObj;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // ถอดตัวเองออกจากพ่อแม่ก่อนเสมอ (เช่นถ้าเผลอวางเป็นลูกของ Canvas) เพราะ DontDestroyOnLoad
            // ใช้ได้เฉพาะกับ GameObject ที่เป็น "ราก" (Root) เท่านั้น ไม่งั้นจะขึ้น Warning และไม่ทำงานจริง
            // (การถอดออกจาก Canvas ไม่กระทบ Reference ของ Slider/Dropdown ที่ลากไว้ใน Inspector แต่อย่างใด)
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            LoadSettings();
        }

        private void Start()
        {
            SetupResolutionDropdown();
            SetupQualityDropdown();
            RefreshUIFromCurrentValues();
            ApplyAll();
            WireUpListeners();
        }

        /// <summary>ผูก Event ของ UI แต่ละตัวให้เรียกฟังก์ชันตั้งค่าอัตโนมัติ (เผื่อไม่ได้ผูกผ่าน Inspector เอง)</summary>
        private void WireUpListeners()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
            if (screenShakeToggle != null) screenShakeToggle.onValueChanged.AddListener(SetScreenShake);
            if (showFpsToggle != null) showFpsToggle.onValueChanged.AddListener(SetShowFps);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(SetQuality);
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
        private void SetupResolutionDropdown()
        {
            if (resolutionDropdown == null) return;

            _availableResolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();

            var options = new List<string>();
            int currentIndex = 0;
            for (int i = 0; i < _availableResolutions.Length; i++)
            {
                var r = _availableResolutions[i];
                options.Add($"{r.width} x {r.height} @{Mathf.RoundToInt((float)r.refreshRateRatio.value)}Hz");
                if (r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height)
                    currentIndex = i;
            }
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("ResolutionIndex", currentIndex));
        }

        private void SetupQualityDropdown()
        {
            if (qualityDropdown == null) return;
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        }

        public void SetResolution(int index)
        {
            if (_availableResolutions == null || index < 0 || index >= _availableResolutions.Length) return;
            var r = _availableResolutions[index];
            Screen.SetResolution(r.width, r.height, Screen.fullScreen);
            PlayerPrefs.SetInt("ResolutionIndex", index);
            SaveSettings();
        }

        public void SetFullscreen(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
            SaveSettings();
        }

        public void SetQuality(int index)
        {
            QualitySettings.SetQualityLevel(index, true);
            SaveSettings();
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
            PlayerPrefs.SetInt("Fullscreen", Screen.fullScreen ? 1 : 0);
            PlayerPrefs.SetInt("QualityLevel", QualitySettings.GetQualityLevel());
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
        }

        private void RefreshUIFromCurrentValues()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(MasterVolume);
            if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(MusicVolume);
            if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(SFXVolume);
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.SetValueWithoutNotify(MouseSensitivity);
            if (screenShakeToggle != null) screenShakeToggle.SetIsOnWithoutNotify(ScreenShakeEnabled);
            if (showFpsToggle != null) showFpsToggle.SetIsOnWithoutNotify(ShowFps);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1);
            if (qualityDropdown != null) qualityDropdown.SetValueWithoutNotify(PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel()));
        }

        /// <summary>เรียกค่าที่โหลดมาทั้งหมดไปใช้จริงตอนเริ่มเกม (Apply ทุกอย่างพร้อมกันครั้งเดียว)</summary>
        private void ApplyAll()
        {
            AudioListener.volume = MasterVolume;
            AudioManager.Instance?.SetMusicVolume(MusicVolume);
            AudioManager.Instance?.SetSFXVolume(SFXVolume);
            Screen.fullScreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            QualitySettings.SetQualityLevel(PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel()), true);
            if (ShowFps) EnsureFpsCounter();
        }
    }
}
