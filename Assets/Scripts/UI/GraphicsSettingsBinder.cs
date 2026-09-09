using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TopDownTacticalAI.UI
{
    /// <summary>
    /// Companion binder for the Graphics &amp; Effects section of the Settings Panel.
    /// Wires the effects-quality Slider and three Toggles (Menu VFX, Background VFX,
    /// Screen Effects) to <see cref="GraphicsQualityManager"/> without depending
    /// on <see cref="SettingsPanelBinder"/>.
    ///
    /// Setup: attach to the SettingsPanel GameObject alongside
    /// <see cref="SettingsPanelBinder"/>, then drag the new row GameObjects into
    /// the Inspector fields below. All rows are optional — missing references
    /// are simply ignored so you can add them one at a time.
    /// </summary>
    public class GraphicsSettingsBinder : MonoBehaviour
    {
        [Header("── Effects Quality ──")]
        [Tooltip("Slider that controls the overall VFX intensity (0..1).")]
        public Slider effectsQualitySlider;

        [Header("── VFX Toggles ──")]
        [Tooltip("Toggle for the neon menu VFX (halo + dust behind panels).")]
        public Toggle menuVFXToggle;
        [Tooltip("Toggle for background/atmosphere VFX during gameplay.")]
        public Toggle backgroundVFXToggle;
        [Tooltip("Toggle for screen effects (dim overlay, chromatic aberration, etc.).")]
        public Toggle screenEffectsToggle;

        private bool _listenersWired = false;

        private void OnEnable()
        {
            RefreshUIFromCurrentValues();

            if (!_listenersWired)
            {
                WireUpListeners();
                _listenersWired = true;
            }
        }

        /// <summary>Reads current values from GraphicsQualityManager and updates the UI.</summary>
        public void RefreshUIFromCurrentValues()
        {
            if (effectsQualitySlider != null)
                effectsQualitySlider.SetValueWithoutNotify(
                    GraphicsQualityManager.EffectsQuality);

            if (menuVFXToggle != null)
                menuVFXToggle.SetIsOnWithoutNotify(GraphicsQualityManager.MenuVFXEnabled);

            if (backgroundVFXToggle != null)
                backgroundVFXToggle.SetIsOnWithoutNotify(GraphicsQualityManager.BackgroundVFXEnabled);

            if (screenEffectsToggle != null)
                screenEffectsToggle.SetIsOnWithoutNotify(GraphicsQualityManager.ScreenEffectsEnabled);
        }

        private void WireUpListeners()
        {
            if (effectsQualitySlider != null)
                effectsQualitySlider.onValueChanged.AddListener(
                    GraphicsQualityManager.SetEffectsQuality);

            if (menuVFXToggle != null)
                menuVFXToggle.onValueChanged.AddListener(
                    GraphicsQualityManager.SetMenuVFXEnabled);

            if (backgroundVFXToggle != null)
                backgroundVFXToggle.onValueChanged.AddListener(
                    GraphicsQualityManager.SetBackgroundVFXEnabled);

            if (screenEffectsToggle != null)
                screenEffectsToggle.onValueChanged.AddListener(
                    GraphicsQualityManager.SetScreenEffectsEnabled);
        }
    }
}
