using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using TopDownTacticalAI.UI.Themes;
using TopDownTacticalAI.UI.Animation;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Scene-wide UI manager singleton.
    /// Responsibilities:
    ///   - Owns the active <see cref="UIThemeManager"/> + applies the saved theme on Awake.
    ///   - Provides a single modal-stack so only one panel is visible at a time
    ///     (used by <see cref="MainMenuController"/>, <see cref="PauseMenu"/>, etc.).
    ///   - Centralises "load scene with transition" so callers don't have to
    ///     null-check <see cref="ScreenTransition"/>.
    ///   - Wires the global AudioSource for UI sounds.
    /// Persists across scene loads (DontDestroyOnLoad).
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("── Theme ──")]
        [Tooltip("Optional explicit theme manager. If null, the singleton from ThemeManager is used.")]
        public UIThemeManager themeManager;

        [Header("── Audio ──")]
        public AudioSource audioSource;

        /// <summary>Stack of currently open panels. Top = most recent.</summary>
        private readonly Stack<AnimatedPanel> _modalStack = new Stack<AnimatedPanel>();

        public AnimatedPanel CurrentModal => _modalStack.Count > 0 ? _modalStack.Peek() : null;

        /// <summary>Fired when a modal is pushed onto the stack.</summary>
        public event Action<AnimatedPanel> OnModalPushed;
        /// <summary>Fired when a modal is popped from the stack.</summary>
        public event Action<AnimatedPanel> OnModalPopped;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            if (themeManager == null) themeManager = GetComponent<UIThemeManager>();
            if (themeManager == null) themeManager = UIThemeManager.Instance;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Theme ──

        /// <summary>
        /// Applies the theme mode saved in PlayerPrefs (defaults to Night).
        /// Call from any scene that needs the night neon look on first load.
        /// </summary>
        public void ApplySavedTheme()
        {
            if (themeManager == null) return;
            int mode = PlayerPrefs.GetInt(UIThemeManager.DayNightPrefKey, (int)UIThemeVariant.Mode.Night);
            themeManager.Apply((UIThemeVariant.Mode)mode, persist: false);
        }

        // ── Scene Loading ──

        /// <summary>Loads a scene via <see cref="ScreenTransition"/> if one exists, otherwise falls back to direct load.</summary>
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (ScreenTransition.Instance != null)
            {
                ScreenTransition.Instance.LoadSceneWithTransition(sceneName);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        // ── Modal Stack ──

        /// <summary>
        /// Pushes a modal panel onto the stack. Hides the currently visible one
        /// (if any) and shows the new one. Use for stacked dialogs (e.g. settings
        /// opened from a pause menu).
        /// </summary>
        public void PushModal(AnimatedPanel panel)
        {
            if (panel == null) return;
            if (_modalStack.Count > 0)
            {
                var current = _modalStack.Peek();
                if (current != null && current != panel && current.IsVisible) current.Hide();
            }
            _modalStack.Push(panel);
            panel.Show();
            OnModalPushed?.Invoke(panel);
        }

        /// <summary>
        /// Pops the top modal from the stack. If there is one beneath it, that
        /// one is shown again.
        /// </summary>
        public void PopModal()
        {
            if (_modalStack.Count == 0) return;
            var top = _modalStack.Pop();
            if (top != null && top.IsVisible) top.Hide();
            OnModalPopped?.Invoke(top);

            if (_modalStack.Count > 0)
            {
                var next = _modalStack.Peek();
                if (next != null) next.Show();
            }
        }

        /// <summary>Hides every modal currently in the stack without popping it. Useful when loading a new scene.</summary>
        public void HideAllModals()
        {
            foreach (var p in _modalStack)
            {
                if (p != null && p.IsVisible) p.HideInstant();
            }
        }

        /// <summary>Clears the modal stack (does not change panel visibility).</summary>
        public void ClearModalStack()
        {
            _modalStack.Clear();
        }

        // ── Audio ──

        public void PlaySound(AudioClip clip, float volume = 1f)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
        }

        // ── Convenience helpers ──

        /// <summary>Loads the level-select scene (with transition if available).</summary>
        public void LoadLevelSelect()
        {
            LoadScene("LevelSelect");
        }

        /// <summary>Loads the main-menu scene (with transition if available).</summary>
        public void LoadMainMenu()
        {
            LoadScene("MainMenu");
        }

        /// <summary>Resets the run-state PlayerPrefs and returns to main menu.</summary>
        public void QuitToMainMenu()
        {
            // Wipe transient flags (mana / ammo / cooldowns) but keep settings + progress.
            PlayerPrefs.DeleteKey("Run.ActiveWeapon");
            PlayerPrefs.Save();
            LoadMainMenu();
        }
    }
}
