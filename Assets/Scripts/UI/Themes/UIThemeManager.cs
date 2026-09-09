using System;
using UnityEngine;

namespace TopDownTacticalAI.UI.Themes
{
    /// <summary>
    /// Global theme switcher. Holds two variants (Day / Night) and a shared
    /// UITheme asset. Other controllers subscribe to <see cref="OnThemeChanged"/>
    /// to re-apply colors when the user toggles between light and dark modes.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class UIThemeManager : MonoBehaviour
    {
        public const string DayNightPrefKey = "UITheme.DayNight";

        public static UIThemeManager Instance { get; private set; }

        [Header("── Theme Assets ──")]
        [Tooltip("The live UITheme asset that the rest of the UI binds to.")]
        public UITheme theme;

        [Header("── Variants ──")]
        public UIThemeVariant dayVariant = UIThemeVariant.CreateDay();
        public UIThemeVariant nightVariant = UIThemeVariant.CreateNight();

        [Header("── Initial State ──")]
        [Tooltip("Which variant to apply on Awake.")]
        public UIThemeVariant.Mode startingMode = UIThemeVariant.Mode.Night;

        public UIThemeVariant CurrentVariant { get; private set; }
        public UIThemeVariant.Mode CurrentMode { get; private set; }

        /// <summary>Fired after a variant has been applied to <see cref="theme"/>.</summary>
        public event Action<UIThemeVariant> OnThemeChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Restore last mode from PlayerPrefs, fall back to startingMode.
            int saved = PlayerPrefs.GetInt(DayNightPrefKey, (int)startingMode);
            Apply((UIThemeVariant.Mode)saved, persist: false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Switches to a different mode and re-applies it.</summary>
        public void Apply(UIThemeVariant.Mode mode, bool persist = true)
        {
            CurrentMode = mode;
            CurrentVariant = (mode == UIThemeVariant.Mode.Day) ? dayVariant : nightVariant;

            if (CurrentVariant == null)
            {
                CurrentVariant = (mode == UIThemeVariant.Mode.Day)
                    ? UIThemeVariant.CreateDay()
                    : UIThemeVariant.CreateNight();
            }

            CurrentVariant.BuildInto(theme);

            if (persist) PlayerPrefs.SetInt(DayNightPrefKey, (int)mode);

            OnThemeChanged?.Invoke(CurrentVariant);
        }

        /// <summary>Toggles between Day and Night.</summary>
        public void Toggle()
        {
            Apply(CurrentMode == UIThemeVariant.Mode.Day
                ? UIThemeVariant.Mode.Night
                : UIThemeVariant.Mode.Day);
        }

        public bool IsDay => CurrentMode == UIThemeVariant.Mode.Day;
        public bool IsNight => CurrentMode == UIThemeVariant.Mode.Night;
    }
}
