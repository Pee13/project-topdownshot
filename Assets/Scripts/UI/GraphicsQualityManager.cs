using UnityEngine;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Global broadcast of graphics-quality changes. Any system (NeonMenuVFX,
    /// Camera, ParticleSystem, post-processing) can read <see cref="Current"/>
    /// or subscribe to <see cref="OnChanged"/> to react when the player
    /// adjusts the quality level from the Settings Panel.
    ///
    /// Values are 0..1 (0 = off/minimal, 1 = full effect intensity). The actual
    /// Unity <see cref="QualitySettings"/> level is still controlled separately
    /// by SettingsManager (Ultra/High/Medium/Low etc.); this class is for the
    /// *in-game* effects budget (VFX density, color saturation, screen shake, etc.).
    /// </summary>
    public static class GraphicsQualityManager
    {
        public const string KeyEffectsQuality = "EffectsQuality";      // 0..1
        public const string KeyMenuVFXEnabled = "MenuVFXEnabled";       // 0/1
        public const string KeyBackgroundVFXEnabled = "BackgroundVFXEnabled"; // 0/1
        public const string KeyScreenEffectsEnabled = "ScreenEffectsEnabled"; // 0/1

        // ── Current values (read-only from outside) ──
        public static float EffectsQuality { get; private set; } = 1f;        // 0..1
        public static bool MenuVFXEnabled { get; private set; } = true;
        public static bool BackgroundVFXEnabled { get; private set; } = true;
        public static bool ScreenEffectsEnabled { get; private set; } = true;

        /// <summary>Effective intensity multiplier (0..1) after applying all flags.
        /// Use this to drive visuals: if MenuVFX is off, returns 0 for menu-VFX
        /// consumers; if just ScreenEffects is off, screen effects get 0 etc.</summary>
        public static float MenuVFXIntensity => MenuVFXEnabled ? EffectsQuality : 0f;
        public static float BackgroundVFXIntensity => BackgroundVFXEnabled ? EffectsQuality : 0f;
        public static float ScreenEffectsIntensity => ScreenEffectsEnabled ? EffectsQuality : 0f;

        /// <summary>Fired whenever any of the values change. Listeners get the new
        /// graphics-quality value (0..1) so they can scale their effect in one
        /// pass without re-reading the properties.</summary>
        public static event System.Action<float> OnChanged;

        public static void Initialize()
        {
            // Load from PlayerPrefs (called once at game start by SettingsManager)
            EffectsQuality = PlayerPrefs.GetFloat(KeyEffectsQuality, 1f);
            MenuVFXEnabled = PlayerPrefs.GetInt(KeyMenuVFXEnabled, 1) == 1;
            BackgroundVFXEnabled = PlayerPrefs.GetInt(KeyBackgroundVFXEnabled, 1) == 1;
            ScreenEffectsEnabled = PlayerPrefs.GetInt(KeyScreenEffectsEnabled, 1) == 1;
        }

        // ── Setters (called by SettingsPanelBinder) ──
        public static void SetEffectsQuality(float v)
        {
            EffectsQuality = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(KeyEffectsQuality, EffectsQuality);
            PlayerPrefs.Save();
            Broadcast();
        }

        public static void SetMenuVFXEnabled(bool enabled)
        {
            MenuVFXEnabled = enabled;
            PlayerPrefs.SetInt(KeyMenuVFXEnabled, enabled ? 1 : 0);
            PlayerPrefs.Save();
            Broadcast();
        }

        public static void SetBackgroundVFXEnabled(bool enabled)
        {
            BackgroundVFXEnabled = enabled;
            PlayerPrefs.SetInt(KeyBackgroundVFXEnabled, enabled ? 1 : 0);
            PlayerPrefs.Save();
            Broadcast();
        }

        public static void SetScreenEffectsEnabled(bool enabled)
        {
            ScreenEffectsEnabled = enabled;
            PlayerPrefs.SetInt(KeyScreenEffectsEnabled, enabled ? 1 : 0);
            PlayerPrefs.Save();
            Broadcast();
        }

        /// <summary>Sync values from SettingsManager (called on startup).</summary>
        public static void ApplyAll()
        {
            Initialize();
            Broadcast();
        }

        private static void Broadcast()
        {
            try
            {
                OnChanged?.Invoke(EffectsQuality);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GraphicsQualityManager] Listener threw: {e}");
            }
        }
    }
}
